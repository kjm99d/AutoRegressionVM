using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services.VMware;

namespace AutoRegressionVM.Services.TestExecution
{
    /// <summary>
    /// 테스트 실행기 구현
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
                throw new InvalidOperationException("이미 실행 중입니다.");

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
                    // 병렬 실행
                    await RunStepsParallelAsync(orderedSteps, scenario.MaxParallelVMs, result, scenario.ContinueOnFailure);
                }
                else
                {
                    // 순차 실행
                    foreach (var step in orderedSteps)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Log(TestLogLevel.Warning, "사용자에 의해 취소됨");
                            break;
                        }

                        currentStep++;
                        ReportProgress(currentStep, totalSteps, step.Name, GetVMName(step.TargetVmxPath), TestProgressPhase.Initializing);

                        var vm = GetVMInfo(step.TargetVmxPath);
                        var stepResult = await RunStepAsync(step, vm);
                        result.TestResults.Add(stepResult);

                        if (stepResult.Status == TestResultStatus.Failed && !scenario.ContinueOnFailure)
                        {
                            Log(TestLogLevel.Error, $"테스트 실패로 시나리오 중단: {step.Name}");
                            break;
                        }
                    }
                }

                result.EndTime = DateTime.Now;
                Log(TestLogLevel.Info, $"시나리오 완료: 성공 {result.PassedCount}, 실패 {result.FailedCount}, 소요시간 {result.Duration:hh\\:mm\\:ss}");
            }
            catch (Exception ex)
            {
                Log(TestLogLevel.Error, $"시나리오 실행 오류: {ex.Message}");
                result.EndTime = DateTime.Now;
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

                // 1. 스냅샷 롤백
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.RevertingSnapshot);
                Log(TestLogLevel.Info, $"[{result.VMName}] 스냅샷 롤백: {step.SnapshotName}");

                if (!await _vmwareService.RevertToSnapshotAsync(vmxPath, step.SnapshotName))
                {
                    throw new Exception($"스냅샷 롤백 실패: {step.SnapshotName}");
                }

                // 2. VM 부팅 대기
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.WaitingForBoot);
                Log(TestLogLevel.Info, $"[{result.VMName}] VM 부팅 대기 중...");

                if (!await _vmwareService.PowerOnAsync(vmxPath))
                {
                    throw new Exception("VM 전원 켜기 실패");
                }

                if (!await _vmwareService.WaitForToolsAsync(vmxPath, 300))
                {
                    throw new Exception("VMware Tools 응답 대기 시간 초과");
                }

                // 3. Guest 로그인
                Log(TestLogLevel.Info, $"[{result.VMName}] Guest 로그인: {username}");
                if (!await _vmwareService.LoginToGuestAsync(vmxPath, username, password))
                {
                    throw new Exception("Guest OS 로그인 실패");
                }

                // 4. 파일 복사 (호스트 → VM)
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.CopyingFiles);
                foreach (var file in step.FilesToCopyToVM)
                {
                    Log(TestLogLevel.Debug, $"[{result.VMName}] 파일 복사: {file.SourcePath} → {file.DestinationPath}");

                    // 대상 디렉토리 생성
                    var guestDir = Path.GetDirectoryName(file.DestinationPath);
                    if (!string.IsNullOrEmpty(guestDir))
                    {
                        await _vmwareService.CreateDirectoryInGuestAsync(vmxPath, guestDir);
                    }

                    if (!await _vmwareService.CopyFileToGuestAsync(vmxPath, file.SourcePath, file.DestinationPath))
                    {
                        throw new Exception($"파일 복사 실패: {file.SourcePath}");
                    }
                }

                // 5. 테스트 실행
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.ExecutingTest);
                Log(TestLogLevel.Info, $"[{result.VMName}] 테스트 실행: {step.Execution.ExecutablePath}");

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

                // 6. 결과 파일 수집
                ReportProgress(0, 1, step.Name, result.VMName, TestProgressPhase.CollectingResults);
                foreach (var file in step.ResultFilesToCollect)
                {
                    var hostPath = file.DestinationPath
                        .Replace("{ResultDir}", GetResultDirectory(step))
                        .Replace("{VMName}", result.VMName)
                        .Replace("{StepName}", step.Name)
                        .Replace("{Timestamp}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                    Log(TestLogLevel.Debug, $"[{result.VMName}] 결과 수집: {file.SourcePath} → {hostPath}");

                    if (await _vmwareService.CopyFileFromGuestAsync(vmxPath, file.SourcePath, hostPath))
                    {
                        result.CollectedFilePaths.Add(hostPath);
                    }
                }

                // 7. 스크린샷 캡처 (옵션)
                if (step.CaptureScreenshots)
                {
                    var screenshotPath = Path.Combine(GetResultDirectory(step), $"{result.VMName}_{step.Name}_final.png");
                    if (await _vmwareService.CaptureScreenshotAsync(vmxPath, screenshotPath))
                    {
                        result.ScreenshotPaths.Add(screenshotPath);
                    }
                }

                // 8. 성공 여부 판단
                result.Status = EvaluateSuccess(step.SuccessCriteria, result) 
                    ? TestResultStatus.Passed 
                    : TestResultStatus.Failed;

                // 9. 스냅샷 롤백 (악성코드 테스트용 - 완료 후 무조건 롤백)
                if (step.ForceSnapshotRevertAfter)
                {
                    Log(TestLogLevel.Info, $"[{result.VMName}] 완료 후 스냅샷 롤백");
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
                Log(TestLogLevel.Error, $"[{result.VMName}] 오류: {ex.Message}");
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

            // Exit Code 체크
            if (criteria.ExpectedExitCode.HasValue)
            {
                if (result.ExitCode != criteria.ExpectedExitCode.Value)
                    return false;
            }

            // 포함 문자열 체크
            if (!string.IsNullOrEmpty(criteria.ContainsText))
            {
                if (string.IsNullOrEmpty(result.Output) || !result.Output.Contains(criteria.ContainsText))
                    return false;
            }

            // 제외 문자열 체크
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
            Log(TestLogLevel.Warning, "테스트 취소 요청됨");
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
    }
}
