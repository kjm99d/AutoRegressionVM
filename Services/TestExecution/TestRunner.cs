using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services.VMware;

namespace AutoRegressionVM.Services.TestExecution
{
    /// <summary>
    /// �׽�Ʈ ����� ����
    /// </summary>
    public class TestRunner : ITestRunner
    {
        private readonly IVMwareService _vmwareService;
        private readonly Dictionary<string, VMInfo> _vmCache;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public event EventHandler<TestProgressEventArgs> ProgressChanged;
        public event EventHandler<TestLogEventArgs> LogGenerated;

        public TestRunner(IVMwareService vmwareService, IEnumerable<VMInfo> registeredVMs)
        {
            _vmwareService = vmwareService;
            _vmCache = registeredVMs?.ToDictionary(v => v.VmxPath, v => v) 
                       ?? new Dictionary<string, VMInfo>();
        }

        public async Task<ScenarioResult> RunScenarioAsync(TestScenario scenario)
        {
            if (_isRunning)
                throw new InvalidOperationException("�̹� ���� ���Դϴ�.");

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            var result = new ScenarioResult
            {
                ScenarioId = scenario.Id,
                ScenarioName = scenario.Name,
                StartTime = DateTime.Now
            };

            try
            {
                Log(TestLogLevel.Info, $"시나리오 시작: {scenario.Name}");

                // 테스트 전 이벤트 실행
                if (scenario.PreTestEvent != null && scenario.PreTestEvent.IsEnabled)
                {
                    Log(TestLogLevel.Info, "테스트 전 이벤트 실행 중...");
                    var preEventResult = await RunEventAsync(scenario.PreTestEvent, scenario.Name);

                    if (!preEventResult.Success && scenario.PreTestEvent.StopOnFailure)
                    {
                        Log(TestLogLevel.Error, $"테스트 전 이벤트 실패: {preEventResult.ErrorMessage}");
                        result.EndTime = DateTime.Now;
                        return result;
                    }

                    Log(TestLogLevel.Info, $"테스트 전 이벤트 완료 (Exit Code: {preEventResult.ExitCode})");
                }

                // 연결 확인
                if (!_vmwareService.IsConnected)
                {
                    Log(TestLogLevel.Info, "VMware 연결 중...");
                    if (!await _vmwareService.ConnectAsync())
                    {
                        throw new Exception("VMware 연결 실패");
                    }
                }

                var orderedSteps = scenario.Steps.OrderBy(s => s.Order).ToList();
                int totalSteps = orderedSteps.Count;
                int currentStep = 0;

                if (scenario.MaxParallelVMs > 1)
                {
                    // ���� ����
                    await RunStepsParallelAsync(orderedSteps, scenario.MaxParallelVMs, result, scenario.ContinueOnFailure);
                }
                else
                {
                    // ���� ����
                    foreach (var step in orderedSteps)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Log(TestLogLevel.Warning, "����ڿ� ���� ��ҵ�");
                            break;
                        }

                        currentStep++;
                        ReportProgress(currentStep, totalSteps, step.Name, GetVMName(step.TargetVmxPath), TestProgressPhase.Initializing);

                        var vm = GetVMInfo(step.TargetVmxPath);
                        var stepResult = await RunStepAsync(step, vm);
                        result.TestResults.Add(stepResult);

                        if (stepResult.Status == TestResultStatus.Failed && !scenario.ContinueOnFailure)
                        {
                            Log(TestLogLevel.Error, $"�׽�Ʈ ���з� �ó����� �ߴ�: {step.Name}");
                            break;
                        }
                    }
                }

                result.EndTime = DateTime.Now;

                // 테스트 후 이벤트 실행
                if (scenario.PostTestEvent != null && scenario.PostTestEvent.IsEnabled)
                {
                    bool shouldRunPostEvent = ShouldRunPostEvent(scenario.PostTestEvent, result);

                    if (shouldRunPostEvent)
                    {
                        Log(TestLogLevel.Info, "테스트 후 이벤트 실행 중...");
                        var postEventResult = await RunEventAsync(scenario.PostTestEvent, scenario.Name, result);
                        Log(TestLogLevel.Info, $"테스트 후 이벤트 완료 (Exit Code: {postEventResult.ExitCode})");
                    }
                    else
                    {
                        Log(TestLogLevel.Info, "테스트 후 이벤트 조건 미충족으로 스킵됨");
                    }
                }

                Log(TestLogLevel.Info, $"시나리오 완료: 성공 {result.PassedCount}, 실패 {result.FailedCount}, 소요시간 {result.Duration:hh\\:mm\\:ss}");
            }
            catch (Exception ex)
            {
                Log(TestLogLevel.Error, $"시나리오 실행 오류: {ex.Message}");
                result.EndTime = DateTime.Now;

                // 오류 발생 시에도 Post 이벤트 실행 (Always 또는 OnFailure 조건일 경우)
                if (scenario.PostTestEvent != null && scenario.PostTestEvent.IsEnabled)
                {
                    if (scenario.PostTestEvent.RunCondition == PostEventCondition.Always ||
                        scenario.PostTestEvent.RunCondition == PostEventCondition.OnFailure)
                    {
                        Log(TestLogLevel.Info, "오류 발생 후 테스트 후 이벤트 실행 중...");
                        try
                        {
                            await RunEventAsync(scenario.PostTestEvent, scenario.Name, result);
                        }
                        catch (Exception postEx)
                        {
                            Log(TestLogLevel.Warning, $"테스트 후 이벤트 실행 실패: {postEx.Message}");
                        }
                    }
                }
            }
            finally
            {
                _isRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }

            return result;
        }

        private async Task RunStepsParallelAsync(List<TestStep> steps, int maxParallel, ScenarioResult result, bool continueOnFailure)
        {
            var semaphore = new SemaphoreSlim(maxParallel);
            var tasks = new List<Task>();

            foreach (var step in steps)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                await semaphore.WaitAsync(_cancellationTokenSource.Token);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        var vm = GetVMInfo(step.TargetVmxPath);
                        var stepResult = await RunStepAsync(step, vm);

                        lock (result.TestResults)
                        {
                            result.TestResults.Add(stepResult);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, _cancellationTokenSource.Token);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        public async Task<TestResult> RunStepAsync(TestStep step, VMInfo vm)
        {
            var result = new TestResult
            {
                TestStepId = step.Id,
                TestStepName = step.Name,
                VMName = vm?.Name ?? Path.GetFileNameWithoutExtension(step.TargetVmxPath),
                StartTime = DateTime.Now,
                Status = TestResultStatus.Running
            };

            try
            {
                var vmxPath = step.TargetVmxPath;
                var username = vm?.GuestUsername ?? "Administrator";
                var password = vm?.GuestPassword ?? "";

                // 1. ������ �ѹ�
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.RevertingSnapshot);
                Log(TestLogLevel.Info, $"[{result.VMName}] ������ �ѹ�: {step.SnapshotName}");

                if (!await _vmwareService.RevertToSnapshotAsync(vmxPath, step.SnapshotName))
                {
                    throw new Exception($"������ �ѹ� ����: {step.SnapshotName}");
                }

                // 2. VM ���� ���
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.WaitingForBoot);
                Log(TestLogLevel.Info, $"[{result.VMName}] VM ���� ��� ��...");

                if (!await _vmwareService.PowerOnAsync(vmxPath))
                {
                    throw new Exception("VM ���� �ѱ� ����");
                }

                if (!await _vmwareService.WaitForToolsAsync(vmxPath, 300))
                {
                    throw new Exception("VMware Tools ���� ��� �ð� �ʰ�");
                }

                // 3. Guest �α���
                Log(TestLogLevel.Info, $"[{result.VMName}] Guest �α���: {username}");
                if (!await _vmwareService.LoginToGuestAsync(vmxPath, username, password))
                {
                    throw new Exception("Guest OS �α��� ����");
                }

                // 4. ���� ���� (ȣ��Ʈ �� VM)
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.CopyingFiles);
                foreach (var file in step.FilesToCopyToVM)
                {
                    Log(TestLogLevel.Debug, $"[{result.VMName}] ���� ����: {file.SourcePath} �� {file.DestinationPath}");

                    // ��� ���丮 ����
                    var guestDir = Path.GetDirectoryName(file.DestinationPath);
                    if (!string.IsNullOrEmpty(guestDir))
                    {
                        await _vmwareService.CreateDirectoryInGuestAsync(vmxPath, guestDir);
                    }

                    if (!await _vmwareService.CopyFileToGuestAsync(vmxPath, file.SourcePath, file.DestinationPath))
                    {
                        throw new Exception($"���� ���� ����: {file.SourcePath}");
                    }
                }

                // 5. �׽�Ʈ ����
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.ExecutingTest);
                Log(TestLogLevel.Info, $"[{result.VMName}] �׽�Ʈ ����: {step.Execution.ExecutablePath}");

                GuestProcessResult execResult;
                if (step.Execution.Type == ExecutionType.Script)
                {
                    execResult = await _vmwareService.RunScriptInGuestAsync(
                        vmxPath,
                        "cmd.exe",
                        $"/c \"{step.Execution.ExecutablePath}\" {step.Execution.Arguments}",
                        step.Execution.TimeoutSeconds);
                }
                else
                {
                    execResult = await _vmwareService.RunProgramInGuestAsync(
                        vmxPath,
                        step.Execution.ExecutablePath,
                        step.Execution.Arguments,
                        step.Execution.TimeoutSeconds);
                }

                result.ExitCode = execResult.ExitCode;
                result.Output = execResult.StandardOutput;

                if (!execResult.Success)
                {
                    result.ErrorMessage = execResult.ErrorMessage ?? execResult.StandardError;
                }

                // 6. ��� ���� ����
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.CollectingResults);
                foreach (var file in step.ResultFilesToCollect)
                {
                    var hostPath = file.DestinationPath
                        .Replace("{ResultDir}", GetResultDirectory(step))
                        .Replace("{VMName}", result.VMName)
                        .Replace("{StepName}", step.Name)
                        .Replace("{Timestamp}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                    Log(TestLogLevel.Debug, $"[{result.VMName}] ��� ����: {file.SourcePath} �� {hostPath}");

                    if (await _vmwareService.CopyFileFromGuestAsync(vmxPath, file.SourcePath, hostPath))
                    {
                        result.CollectedFilePaths.Add(hostPath);
                    }
                }

                // 7. ��ũ���� ĸó (�ɼ�)
                if (step.CaptureScreenshots)
                {
                    var screenshotPath = Path.Combine(GetResultDirectory(step), $"{result.VMName}_{step.Name}_final.png");
                    if (await _vmwareService.CaptureScreenshotAsync(vmxPath, screenshotPath))
                    {
                        result.ScreenshotPaths.Add(screenshotPath);
                    }
                }

                // 8. ���� ���� �Ǵ�
                result.Status = EvaluateSuccess(step.SuccessCriteria, result) 
                    ? TestResultStatus.Passed 
                    : TestResultStatus.Failed;

                // 9. ������ �ѹ� (�Ǽ��ڵ� �׽�Ʈ�� - �Ϸ� �� ������ �ѹ�)
                if (step.ForceSnapshotRevertAfter)
                {
                    Log(TestLogLevel.Info, $"[{result.VMName}] �Ϸ� �� ������ �ѹ�");
                    await _vmwareService.RevertToSnapshotAsync(vmxPath, step.SnapshotName);
                }

                ReportProgress(0, 1, step.Name, result.VMName, 
                    result.Status == TestResultStatus.Passed ? TestProgressPhase.Completed : TestProgressPhase.Failed);

                Log(result.Status == TestResultStatus.Passed ? TestLogLevel.Info : TestLogLevel.Error,
                    $"[{result.VMName}] {step.Name}: {result.Status}");
            }
            catch (Exception ex)
            {
                result.Status = TestResultStatus.Error;
                result.ErrorMessage = ex.Message;
                Log(TestLogLevel.Error, $"[{result.VMName}] ����: {ex.Message}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
            }

            return result;
        }

        private bool EvaluateSuccess(SuccessCriteria criteria, TestResult result)
        {
            if (criteria == null) return true;

            // Exit Code üũ
            if (criteria.ExpectedExitCode.HasValue)
            {
                if (result.ExitCode != criteria.ExpectedExitCode.Value)
                    return false;
            }

            // ���� ���ڿ� üũ
            if (!string.IsNullOrEmpty(criteria.ContainsText))
            {
                if (string.IsNullOrEmpty(result.Output) || !result.Output.Contains(criteria.ContainsText))
                    return false;
            }

            // ���� ���ڿ� üũ
            if (!string.IsNullOrEmpty(criteria.NotContainsText))
            {
                if (!string.IsNullOrEmpty(result.Output) && result.Output.Contains(criteria.NotContainsText))
                    return false;
            }

            return true;
        }

        private VMInfo GetVMInfo(string vmxPath)
        {
            if (_vmCache.TryGetValue(vmxPath, out var vm))
                return vm;

            return new VMInfo
            {
                Name = Path.GetFileNameWithoutExtension(vmxPath),
                VmxPath = vmxPath
            };
        }

        private string GetVMName(string vmxPath)
        {
            return GetVMInfo(vmxPath)?.Name ?? Path.GetFileNameWithoutExtension(vmxPath);
        }

        private string GetResultDirectory(TestStep step)
        {
            var dir = Path.Combine("Results", DateTime.Now.ToString("yyyyMMdd"), step.Name);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
            Log(TestLogLevel.Warning, "�׽�Ʈ ��� ��û��");
        }

        private void ReportProgress(int current, int total, string stepName, string vmName, TestProgressPhase phase)
        {
            ProgressChanged?.Invoke(this, new TestProgressEventArgs
            {
                CurrentStep = current,
                TotalSteps = total,
                CurrentStepName = stepName,
                VMName = vmName,
                Phase = phase
            });
        }

        private void Log(TestLogLevel level, string message, string vmName = null)
        {
            LogGenerated?.Invoke(this, new TestLogEventArgs
            {
                Level = level,
                Message = message,
                VMName = vmName
            });
        }

        #region Event Execution

        /// <summary>
        /// 시나리오 이벤트 실행
        /// </summary>
        private async Task<EventExecutionResult> RunEventAsync(ScenarioEvent evt, string scenarioName, ScenarioResult result = null)
        {
            var execResult = new EventExecutionResult();

            try
            {
                var processStartInfo = BuildProcessStartInfo(evt, scenarioName, result);

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();

                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            Log(TestLogLevel.Debug, $"[Event] {e.Data}");
                        }
                    };

                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorBuilder.AppendLine(e.Data);
                            Log(TestLogLevel.Warning, $"[Event Error] {e.Data}");
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var completed = await Task.Run(() => process.WaitForExit(evt.TimeoutSeconds * 1000));

                    if (!completed)
                    {
                        try { process.Kill(); } catch { }
                        execResult.Success = false;
                        execResult.ErrorMessage = $"이벤트 실행 타임아웃 ({evt.TimeoutSeconds}초 초과)";
                        return execResult;
                    }

                    execResult.ExitCode = process.ExitCode;
                    execResult.StandardOutput = outputBuilder.ToString();
                    execResult.StandardError = errorBuilder.ToString();
                    execResult.Success = process.ExitCode == 0;

                    if (!execResult.Success)
                    {
                        execResult.ErrorMessage = $"Exit Code: {process.ExitCode}";
                        if (!string.IsNullOrWhiteSpace(execResult.StandardError))
                        {
                            execResult.ErrorMessage += $"\n{execResult.StandardError}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                execResult.Success = false;
                execResult.ErrorMessage = ex.Message;
            }

            return execResult;
        }

        /// <summary>
        /// 프로세스 시작 정보 구성
        /// </summary>
        private ProcessStartInfo BuildProcessStartInfo(ScenarioEvent evt, string scenarioName, ScenarioResult result)
        {
            string fileName;
            string arguments;

            // 명령에 매크로 치환
            var command = ExpandEventMacros(evt.Command, scenarioName, result);
            var args = ExpandEventMacros(evt.Arguments ?? "", scenarioName, result);

            switch (evt.Type)
            {
                case EventType.PowerShell:
                    fileName = "powershell.exe";
                    arguments = $"-ExecutionPolicy Bypass -File \"{command}\" {args}";
                    break;

                case EventType.BatchFile:
                    fileName = "cmd.exe";
                    arguments = $"/c \"{command}\" {args}";
                    break;

                case EventType.Command:
                    fileName = "cmd.exe";
                    arguments = $"/c {command} {args}";
                    break;

                case EventType.Executable:
                default:
                    fileName = command;
                    arguments = args;
                    break;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = evt.HideWindow,
                WorkingDirectory = string.IsNullOrEmpty(evt.WorkingDirectory)
                    ? Environment.CurrentDirectory
                    : ExpandEventMacros(evt.WorkingDirectory, scenarioName, result)
            };

            // 환경 변수 설정
            if (evt.EnvironmentVariables != null)
            {
                foreach (var envVar in evt.EnvironmentVariables)
                {
                    startInfo.EnvironmentVariables[envVar.Key] = ExpandEventMacros(envVar.Value, scenarioName, result);
                }
            }

            // 기본 환경 변수 추가
            startInfo.EnvironmentVariables["SCENARIO_NAME"] = scenarioName;
            startInfo.EnvironmentVariables["TEST_DATE"] = DateTime.Now.ToString("yyyy-MM-dd");
            startInfo.EnvironmentVariables["TEST_TIME"] = DateTime.Now.ToString("HH:mm:ss");

            if (result != null)
            {
                startInfo.EnvironmentVariables["TEST_PASSED"] = result.PassedCount.ToString();
                startInfo.EnvironmentVariables["TEST_FAILED"] = result.FailedCount.ToString();
                startInfo.EnvironmentVariables["TEST_TOTAL"] = result.TotalCount.ToString();
                startInfo.EnvironmentVariables["TEST_SUCCESS"] = (result.FailedCount == 0).ToString();
            }

            return startInfo;
        }

        /// <summary>
        /// 이벤트 문자열의 매크로 치환
        /// </summary>
        private string ExpandEventMacros(string input, string scenarioName, ScenarioResult result)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var output = input
                .Replace("{ScenarioName}", scenarioName)
                .Replace("{Date}", DateTime.Now.ToString("yyyy-MM-dd"))
                .Replace("{Time}", DateTime.Now.ToString("HH-mm-ss"))
                .Replace("{DateTime}", DateTime.Now.ToString("yyyyMMdd_HHmmss"))
                .Replace("{ResultDir}", Path.Combine("Results", DateTime.Now.ToString("yyyyMMdd")));

            if (result != null)
            {
                output = output
                    .Replace("{PassedCount}", result.PassedCount.ToString())
                    .Replace("{FailedCount}", result.FailedCount.ToString())
                    .Replace("{TotalCount}", result.TotalCount.ToString())
                    .Replace("{Duration}", result.Duration.ToString(@"hh\:mm\:ss"))
                    .Replace("{Success}", (result.FailedCount == 0).ToString());
            }

            return output;
        }

        /// <summary>
        /// Post 이벤트 실행 조건 확인
        /// </summary>
        private bool ShouldRunPostEvent(ScenarioEvent postEvent, ScenarioResult result)
        {
            switch (postEvent.RunCondition)
            {
                case PostEventCondition.Always:
                    return true;

                case PostEventCondition.OnSuccess:
                    return result.FailedCount == 0;

                case PostEventCondition.OnFailure:
                    return result.FailedCount > 0;

                default:
                    return true;
            }
        }

        #endregion
    }

    /// <summary>
    /// 이벤트 실행 결과
    /// </summary>
    public class EventExecutionResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public string ErrorMessage { get; set; }
    }
}
