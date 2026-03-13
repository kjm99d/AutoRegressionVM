using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services;
using AutoRegressionVM.Services.Notification;
using AutoRegressionVM.Services.TestExecution;
using AutoRegressionVM.Services.VMware;

namespace AutoRegressionVM.CLI
{
    /// <summary>
    /// CLI 모드 실행기
    /// </summary>
    public class CliRunner
    {
        private readonly CliOptions _options;
        private readonly ISettingsService _settingsService;
        private readonly AppSettings _appSettings;
        private readonly IVMwareService _vmwareService;
        private NotificationManager _notificationManager;

        public CliRunner(CliOptions options)
        {
            _options = options;
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();
            _vmwareService = new VixService(_appSettings.VMwareInstallPath);
            _notificationManager = new NotificationManager(_appSettings.Notification);
        }

        public async Task<int> RunAsync()
        {
            try
            {
                PrintHeader();

                if (_options.ShowHelp)
                {
                    CommandLineParser.PrintHelp();
                    return 0;
                }

                if (_options.ListScenarios)
                {
                    return ListScenarios();
                }

                if (_options.ListVMs)
                {
                    return ListVMs();
                }

                if (string.IsNullOrEmpty(_options.ScenarioName))
                {
                    Console.WriteLine("[ERROR] 시나리오 이름을 지정하세요. (--scenario <이름>)");
                    return 4;
                }

                return await RunScenarioAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                if (_options.Verbose)
                {
                    Console.WriteLine(ex.StackTrace);
                }
                return 5;
            }
        }

        private void PrintHeader()
        {
            Console.WriteLine(@"
================================================================
              AutoRegressionVM - CLI Mode
================================================================
");
        }

        private int ListScenarios()
        {
            Console.WriteLine("[INFO] 등록된 시나리오 목록:\n");

            var scenarios = _settingsService.LoadAllScenarios();
            if (scenarios.Count == 0)
            {
                Console.WriteLine("  (등록된 시나리오가 없습니다)");
                return 0;
            }

            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"  - {scenario.Name}");
                Console.WriteLine($"    설명: {scenario.Description ?? "(없음)"}");
                Console.WriteLine($"    Steps: {scenario.Steps.Count}개");
                Console.WriteLine();
            }

            return 0;
        }

        private int ListVMs()
        {
            Console.WriteLine("[INFO] 등록된 VM 목록:\n");

            if (_appSettings.RegisteredVMs.Count == 0)
            {
                Console.WriteLine("  (등록된 VM이 없습니다)");
                return 0;
            }

            foreach (var vm in _appSettings.RegisteredVMs)
            {
                Console.WriteLine($"  - {vm.Name}");
                Console.WriteLine($"    경로: {vm.VmxPath}");
                Console.WriteLine();
            }

            return 0;
        }

        private async Task<int> RunScenarioAsync()
        {
            // 시나리오 찾기
            var scenarios = _settingsService.LoadAllScenarios();
            var scenario = scenarios.FirstOrDefault(s =>
                s.Name.Equals(_options.ScenarioName, StringComparison.OrdinalIgnoreCase));

            if (scenario == null)
            {
                Console.WriteLine($"[ERROR] 시나리오를 찾을 수 없음: {_options.ScenarioName}");
                return 2;
            }

            // 옵션 오버라이드
            if (_options.Parallel.HasValue)
            {
                scenario.MaxParallelVMs = _options.Parallel.Value;
            }

            // --vm 옵션: 특정 VM만 대상으로 필터링
            if (!string.IsNullOrEmpty(_options.VMName))
            {
                var targetVM = _appSettings.RegisteredVMs.FirstOrDefault(v =>
                    v.Name.Equals(_options.VMName, StringComparison.OrdinalIgnoreCase));

                if (targetVM == null)
                {
                    Console.WriteLine($"[ERROR] VM not found: {_options.VMName}");
                    return 2;
                }

                scenario.TargetVMPaths = new System.Collections.Generic.List<string> { targetVM.VmxPath };
                Console.WriteLine($"[INFO] Target VM: {targetVM.Name}");
            }

            Console.WriteLine($"[INFO] 시나리오 로드: {scenario.Name}");
            Console.WriteLine($"[INFO] Steps: {scenario.Steps.Count}개");
            Console.WriteLine($"[INFO] 병렬 VM 수: {scenario.MaxParallelVMs}");
            Console.WriteLine();

            if (_options.DryRun)
            {
                Console.WriteLine("[INFO] 드라이런 모드 - 실제 실행 없이 종료");
                return 0;
            }

            // VMware 연결
            Console.WriteLine("[INFO] VMware 연결 중...");
            if (!await _vmwareService.ConnectAsync())
            {
                Console.WriteLine("[ERROR] VMware 연결 실패");
                return 3;
            }
            Console.WriteLine("[INFO] VMware 연결 성공");

            // 알림 - 시작
            await _notificationManager.NotifyTestStartedAsync(scenario);

            // 테스트 실행
            var testRunner = new TestRunner(_vmwareService, _appSettings.RegisteredVMs);
            testRunner.ProgressChanged += OnProgressChanged;
            testRunner.LogGenerated += OnLogGenerated;

            Console.WriteLine();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 테스트 시작...");
            Console.WriteLine();

            // --timeout 옵션: 전체 실행 타임아웃
            ScenarioResult result;
            if (_options.TimeoutMinutes.HasValue)
            {
                Console.WriteLine($"[INFO] Timeout: {_options.TimeoutMinutes} min");
                using (var cts = new System.Threading.CancellationTokenSource(
                    TimeSpan.FromMinutes(_options.TimeoutMinutes.Value)))
                {
                    var runTask = testRunner.RunScenarioAsync(scenario);
                    var completed = await Task.WhenAny(runTask, Task.Delay(-1, cts.Token).ContinueWith(_ => (ScenarioResult)null));
                    if (completed != runTask)
                    {
                        testRunner.Cancel();
                        Console.WriteLine($"[ERROR] Timeout exceeded ({_options.TimeoutMinutes} min)");
                        return 5;
                    }
                    result = await runTask;
                }
            }
            else
            {
                result = await testRunner.RunScenarioAsync(scenario);
            }

            // 결과 출력
            PrintResult(result);

            // 결과 저장
            _settingsService.SaveResult(result, _options.ReportPath);

            // 알림 - 완료
            await _notificationManager.NotifyTestCompletedAsync(result);

            // Exit code
            return result.IsSuccess ? 0 : 1;
        }

        private void OnProgressChanged(object sender, TestProgressEventArgs e)
        {
            if (_options.Verbose)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{e.VMName}] {e.CurrentStepName} - {e.Phase}");
            }
        }

        private void OnLogGenerated(object sender, TestLogEventArgs e)
        {
            string prefix;
            if (e.Level == TestLogLevel.Error)
                prefix = "ERROR";
            else if (e.Level == TestLogLevel.Warning)
                prefix = "WARN";
            else if (e.Level == TestLogLevel.Debug && !_options.Verbose)
                prefix = null;
            else
                prefix = "INFO";

            if (prefix != null)
            {
                var vmPrefix = string.IsNullOrEmpty(e.VMName) ? "" : $"[{e.VMName}] ";
                Console.WriteLine($"[{e.Timestamp:HH:mm:ss}] [{prefix}] {vmPrefix}{e.Message}");
            }
        }

        private void PrintResult(ScenarioResult result)
        {
            Console.WriteLine();
            Console.WriteLine("================================================================");
            Console.WriteLine("                        TEST SUMMARY");
            Console.WriteLine("================================================================");

            if (_options.OutputFormat == "json")
            {
                var json = SerializeToJson(result);
                Console.WriteLine(json);
            }
            else if (_options.OutputFormat == "xml")
            {
                Console.WriteLine(SerializeToXml(result));
            }
            else
            {
                Console.WriteLine($"시나리오: {result.ScenarioName}");
                Console.WriteLine($"시작: {result.StartTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"종료: {result.EndTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"소요시간: {result.Duration:hh\\:mm\\:ss}");
                Console.WriteLine();
                Console.WriteLine($"Total: {result.TotalCount} | Passed: {result.PassedCount} | Failed: {result.FailedCount} | Skipped: {result.SkippedCount}");
                Console.WriteLine();

                // 상세 결과
                foreach (var testResult in result.TestResults)
                {
                    string statusIcon;
                    if (testResult.Status == TestResultStatus.Passed)
                        statusIcon = "[PASS]";
                    else if (testResult.Status == TestResultStatus.Failed)
                        statusIcon = "[FAIL]";
                    else if (testResult.Status == TestResultStatus.Error)
                        statusIcon = "[ERR]";
                    else
                        statusIcon = "[SKIP]";

                    Console.WriteLine($"  {statusIcon} [{testResult.VMName}] {testResult.TestStepName} - {testResult.Status} ({testResult.Duration:mm\\:ss})");

                    if (testResult.Status != TestResultStatus.Passed && !string.IsNullOrEmpty(testResult.ErrorMessage))
                    {
                        Console.WriteLine($"      Error: {testResult.ErrorMessage}");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Exit Code: {(result.IsSuccess ? 0 : 1)} ({(result.IsSuccess ? "Success" : "Some tests failed")})");
        }

        private string SerializeToJson(ScenarioResult result)
        {
            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }

        private string SerializeToXml(ScenarioResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine($"<TestRun scenario=\"{EscapeXml(result.ScenarioName)}\" start=\"{result.StartTime:o}\" end=\"{result.EndTime:o}\">");
            sb.AppendLine($"  <Summary total=\"{result.TotalCount}\" passed=\"{result.PassedCount}\" failed=\"{result.FailedCount}\" skipped=\"{result.SkippedCount}\" success=\"{result.IsSuccess}\" />");
            sb.AppendLine("  <Results>");
            foreach (var tr in result.TestResults)
            {
                sb.AppendLine($"    <Test name=\"{EscapeXml(tr.TestStepName)}\" vm=\"{EscapeXml(tr.VMName)}\" status=\"{tr.Status}\" exitCode=\"{tr.ExitCode}\" duration=\"{tr.Duration:hh\\:mm\\:ss}\">");
                if (!string.IsNullOrEmpty(tr.ErrorMessage))
                    sb.AppendLine($"      <Error>{EscapeXml(tr.ErrorMessage)}</Error>");
                sb.AppendLine("    </Test>");
            }
            sb.AppendLine("  </Results>");
            sb.AppendLine("</TestRun>");
            return sb.ToString();
        }

        private static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
