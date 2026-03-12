using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services.VMware;
using AutoRegressionVM.Services;

namespace AutoRegressionVM.Services.TestExecution
{
    /// <summary>
    /// 테스트 실행기 구현
    /// </summary>
    public class TestRunner : ITestRunner
    {
        private readonly IVMwareService _vmwareService;
        private readonly Dictionary<string, VMInfo> _vmCache;
        private readonly MacroService _macroService = new MacroService();
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

        private int _totalStepCount;
        private int _completedStepCount;
        private int _maxRetryCount;

        public async Task<ScenarioResult> RunScenarioAsync(TestScenario scenario)
        {
            if (_isRunning)
                throw new InvalidOperationException("이미 실행 중입니다.");

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _completedStepCount = 0;
            _maxRetryCount = scenario.MaxRetryCount;

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

                // 병렬 파일 분배 모드: TestTargetFiles와 TargetVMPaths가 모두 있을 때
                if (scenario.TestTargetFiles.Count > 0 && scenario.TargetVMPaths.Count > 0)
                {
                    _totalStepCount = orderedSteps.Count * scenario.TestTargetFiles.Count;
                    Log(TestLogLevel.Info, $"파일 분배 병렬 모드: {scenario.TestTargetFiles.Count}개 파일, {scenario.TargetVMPaths.Count}개 VM");
                    await RunScenarioParallelAsync(scenario, orderedSteps, result);
                }
                else if (scenario.MaxParallelVMs > 1)
                {
                    _totalStepCount = orderedSteps.Count;
                    // 기존 병렬 실행
                    Log(TestLogLevel.Info, $"병렬 실행 모드 (최대 {scenario.MaxParallelVMs}개 VM)");
                    await RunStepsParallelAsync(orderedSteps, scenario.MaxParallelVMs, result, scenario.ContinueOnFailure);
                }
                else
                {
                    _totalStepCount = orderedSteps.Count;
                    // 순차 실행
                    Log(TestLogLevel.Info, "순차 실행 모드");
                    int currentStep = 0;
                    foreach (var step in orderedSteps)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Log(TestLogLevel.Warning, "사용자에 의해 취소됨");
                            break;
                        }

                        currentStep++;
                        var vmName = GetVMName(step.TargetVmxPath);
                        ReportProgress(currentStep, _totalStepCount, step.Name, vmName, TestProgressPhase.Initializing);

                        // 조건 평가
                        if (!EvaluateStepCondition(step, result.TestResults))
                        {
                            Log(TestLogLevel.Info, $"[{vmName}] 스텝 '{step.Name}' 조건 미충족으로 건너뜀");
                            var skipped = new TestResult
                            {
                                TestStepId = step.Id,
                                TestStepName = step.Name,
                                VMName = vmName,
                                StartTime = DateTime.Now,
                                EndTime = DateTime.Now,
                                Status = TestResultStatus.Skipped
                            };
                            result.TestResults.Add(skipped);
                            continue;
                        }

                        var vm = GetVMInfo(step.TargetVmxPath);
                        var stepResult = await RunStepWithRetryAsync(step, vm);
                        result.TestResults.Add(stepResult);

                        if (stepResult.Status == TestResultStatus.Failed && !scenario.ContinueOnFailure)
                        {
                            Log(TestLogLevel.Error, $"테스트 실패로 시나리오 중단: {step.Name}");
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
                if (ex is AggregateException ae)
                {
                    foreach (var inner in ae.Flatten().InnerExceptions)
                    {
                        if (!(inner is OperationCanceledException))
                            Log(TestLogLevel.Error, $"시나리오 실행 오류: {inner.Message}");
                    }
                }
                else
                {
                    Log(TestLogLevel.Error, $"시나리오 실행 오류: {ex.Message}");
                }
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

        /// <summary>
        /// 파일 분배 병렬 실행: 각 TestTargetFile을 VM에 라운드로빈으로 할당하여 병렬 실행
        /// </summary>
        private async Task RunScenarioParallelAsync(TestScenario scenario, List<TestStep> orderedSteps, ScenarioResult result)
        {
            var targetFiles = scenario.TestTargetFiles;
            var vmPaths = scenario.TargetVMPaths;
            using (var semaphore = new SemaphoreSlim(scenario.MaxParallelVMs > 0 ? scenario.MaxParallelVMs : 1))
            {
            var tasks = new List<Task>();

            try
            {
                for (int i = 0; i < targetFiles.Count; i++)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    var targetFile = targetFiles[i];
                    var vmxPath = vmPaths[i % vmPaths.Count];
                    var vm = GetVMInfo(vmxPath);
                    var vmDisplayName = vm.Name ?? Path.GetFileNameWithoutExtension(vmxPath);
                    var fileIndex = i;

                    await semaphore.WaitAsync(_cancellationTokenSource.Token);

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            Log(TestLogLevel.Info, $"[{vmDisplayName}] 파일 배정: {targetFile.HostFilePath} (파일 #{fileIndex + 1})");

                            foreach (var step in orderedSteps)
                            {
                                if (_cancellationTokenSource.Token.IsCancellationRequested)
                                    break;

                                // 조건 평가 (현재 VM의 결과만 참조)
                                List<TestResult> currentVmResults;
                                lock (result.TestResults)
                                {
                                    currentVmResults = result.TestResults
                                        .Where(r => r.VMName == vmDisplayName)
                                        .ToList();
                                }

                                if (!EvaluateStepCondition(step, currentVmResults))
                                {
                                    Log(TestLogLevel.Info, $"[{vmDisplayName}] 스텝 '{step.Name}' 조건 미충족으로 건너뜀");
                                    var skipped = new TestResult
                                    {
                                        TestStepId = step.Id,
                                        TestStepName = step.Name,
                                        VMName = vmDisplayName,
                                        StartTime = DateTime.Now,
                                        EndTime = DateTime.Now,
                                        Status = TestResultStatus.Skipped
                                    };
                                    lock (result.TestResults)
                                    {
                                        result.TestResults.Add(skipped);
                                    }
                                    continue;
                                }

                                // 스텝 복제 후 대상 VM 경로 및 배정 파일 주입
                                var stepForVm = CloneStepWithTargetFile(step, vmxPath, targetFile);
                                var stepResult = await RunStepWithRetryAsync(stepForVm, vm);

                                lock (result.TestResults)
                                {
                                    result.TestResults.Add(stepResult);
                                }

                                if (stepResult.Status == TestResultStatus.Failed && !scenario.ContinueOnFailure)
                                {
                                    Log(TestLogLevel.Error, $"[{vmDisplayName}] 테스트 실패로 해당 VM 실행 중단: {step.Name}");
                                    break;
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Log(TestLogLevel.Warning, $"[{vmDisplayName}] 취소됨");
                        }
                        catch (Exception ex)
                        {
                            Log(TestLogLevel.Error, $"[{vmDisplayName}] 병렬 실행 중 예외: {ex.Message}");
                            var errorResult = new TestResult
                            {
                                TestStepName = $"[{vmDisplayName}] 병렬 실행 오류",
                                VMName = vmDisplayName,
                                StartTime = DateTime.Now,
                                EndTime = DateTime.Now,
                                Status = TestResultStatus.Error,
                                ErrorMessage = ex.Message
                            };
                            lock (result.TestResults)
                            {
                                result.TestResults.Add(errorResult);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, _cancellationTokenSource.Token);

                    tasks.Add(task);
                }
            }
            catch (OperationCanceledException)
            {
                Log(TestLogLevel.Warning, "병렬 파일 분배 취소 - 실행 중인 VM 완료 대기...");
            }

            // 취소/예외 발생해도 이미 실행 중인 Task들은 반드시 대기
            if (tasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                {
                    // 취소된 Task는 무시
                }
                catch (AggregateException ae)
                {
                    foreach (var inner in ae.Flatten().InnerExceptions)
                    {
                        if (!(inner is OperationCanceledException))
                            Log(TestLogLevel.Error, $"병렬 Task 오류: {inner.Message}");
                    }
                }
            }
            } // end using semaphore
        }

        /// <summary>
        /// 스텝을 복제하여 대상 VM과 배정 파일을 주입한다.
        /// 배정 파일은 기존 FilesToCopyToVM 앞에 삽입된다.
        /// </summary>
        private TestStep CloneStepWithTargetFile(TestStep original, string vmxPath, TestTargetFile targetFile)
        {
            var cloned = new TestStep
            {
                Id = original.Id,
                Name = original.Name,
                Description = original.Description,
                Order = original.Order,
                TargetVmxPath = vmxPath,
                SnapshotName = original.SnapshotName,
                Execution = original.Execution,
                WaitAfterExecution = original.WaitAfterExecution,
                SuccessCriteria = original.SuccessCriteria,
                ForceNetworkDisconnect = original.ForceNetworkDisconnect,
                CaptureScreenshots = original.CaptureScreenshots,
                ScreenshotIntervalSeconds = original.ScreenshotIntervalSeconds,
                ForceSnapshotRevertAfter = original.ForceSnapshotRevertAfter,
                Condition = original.Condition,
                ResultFilesToCollect = original.ResultFilesToCollect
            };

            // 배정 파일을 맨 앞에 추가, 그 뒤에 기존 파일들
            cloned.FilesToCopyToVM = new List<FileCopyInfo>();
            cloned.FilesToCopyToVM.Add(new FileCopyInfo
            {
                SourcePath = targetFile.HostFilePath,
                DestinationPath = targetFile.VMDestinationPath
            });
            cloned.FilesToCopyToVM.AddRange(original.FilesToCopyToVM);

            return cloned;
        }

        private async Task RunStepsParallelAsync(List<TestStep> steps, int maxParallel, ScenarioResult result, bool continueOnFailure)
        {
            using (var semaphore = new SemaphoreSlim(maxParallel))
            {
            var tasks = new List<Task>();

            try
            {
                foreach (var step in steps)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    await semaphore.WaitAsync(_cancellationTokenSource.Token);

                    var currentStep = step; // 클로저 캡처용
                    var task = Task.Run(async () =>
                    {
                        var vmName = GetVMName(currentStep.TargetVmxPath);
                        try
                        {
                            var vm = GetVMInfo(currentStep.TargetVmxPath);
                            var stepResult = await RunStepWithRetryAsync(currentStep, vm);

                            lock (result.TestResults)
                            {
                                result.TestResults.Add(stepResult);
                            }

                            if (stepResult.Status == TestResultStatus.Failed && !continueOnFailure)
                            {
                                Log(TestLogLevel.Error, $"[{vmName}] 테스트 실패 - 나머지 실행 취소 요청");
                                _cancellationTokenSource?.Cancel();
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Log(TestLogLevel.Warning, $"[{vmName}] 스텝 '{currentStep.Name}' 취소됨");
                        }
                        catch (Exception ex)
                        {
                            Log(TestLogLevel.Error, $"[{vmName}] 스텝 '{currentStep.Name}' 예외: {ex.Message}");
                            var errorResult = new TestResult
                            {
                                TestStepId = currentStep.Id,
                                TestStepName = currentStep.Name,
                                VMName = vmName,
                                StartTime = DateTime.Now,
                                EndTime = DateTime.Now,
                                Status = TestResultStatus.Error,
                                ErrorMessage = ex.Message
                            };
                            lock (result.TestResults)
                            {
                                result.TestResults.Add(errorResult);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, _cancellationTokenSource.Token);

                    tasks.Add(task);
                }
            }
            catch (OperationCanceledException)
            {
                Log(TestLogLevel.Warning, "병렬 스텝 실행 취소 - 실행 중인 스텝 완료 대기...");
            }

            // 취소/예외 발생해도 이미 실행 중인 Task들은 반드시 대기
            if (tasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                {
                    // 취소된 Task는 무시
                }
                catch (AggregateException ae)
                {
                    foreach (var inner in ae.Flatten().InnerExceptions)
                    {
                        if (!(inner is OperationCanceledException))
                            Log(TestLogLevel.Error, $"병렬 스텝 Task 오류: {inner.Message}");
                    }
                }
            }
            } // end using semaphore
        }

        /// <summary>
        /// 실패한 TC를 자동으로 재시도하는 래퍼.
        /// 스냅샷 복원부터 전체 스텝을 다시 실행한다.
        /// </summary>
        private async Task<TestResult> RunStepWithRetryAsync(TestStep step, VMInfo vm)
        {
            var vmName = vm?.Name ?? Path.GetFileNameWithoutExtension(step.TargetVmxPath);
            TestResult lastResult = null;

            for (int attempt = 0; attempt <= _maxRetryCount; attempt++)
            {
                if (attempt > 0)
                {
                    Log(TestLogLevel.Warning, $"[{vmName}] '{step.Name}' 재시도 {attempt}/{_maxRetryCount}...");
                    // 재시도 전 짧은 대기
                    await Task.Delay(3000, _cancellationTokenSource.Token);
                }

                lastResult = await RunStepAsync(step, vm);

                if (lastResult.Status == TestResultStatus.Passed)
                {
                    if (attempt > 0)
                    {
                        Log(TestLogLevel.Info, $"[{vmName}] '{step.Name}' 재시도 {attempt}회 만에 성공");
                        lastResult.ErrorMessage = null; // 성공 시 이전 에러 클리어
                    }
                    return lastResult;
                }

                // Error 상태(인프라 문제)도 재시도
                if (lastResult.Status == TestResultStatus.Failed || lastResult.Status == TestResultStatus.Error)
                {
                    if (attempt < _maxRetryCount)
                    {
                        Log(TestLogLevel.Warning, $"[{vmName}] '{step.Name}' 실패 (시도 {attempt + 1}/{_maxRetryCount + 1}): {lastResult.ErrorMessage ?? lastResult.Status.ToString()}");
                    }
                    continue;
                }

                // Skipped 등은 재시도하지 않음
                break;
            }

            if (lastResult != null && _maxRetryCount > 0 &&
                (lastResult.Status == TestResultStatus.Failed || lastResult.Status == TestResultStatus.Error))
            {
                Log(TestLogLevel.Error, $"[{vmName}] '{step.Name}' {_maxRetryCount + 1}회 시도 모두 실패");
            }

            return lastResult;
        }

        public async Task<TestResult> RunStepAsync(TestStep step, VMInfo vm)
        {
            var stopwatch = Stopwatch.StartNew();
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

                // 1. 스냅샷 복원
                var myStep = System.Threading.Interlocked.Increment(ref _completedStepCount);
                ReportProgress(myStep, _totalStepCount, step.Name, result.VMName, TestProgressPhase.RevertingSnapshot);

                if (!string.IsNullOrWhiteSpace(step.SnapshotName))
                {
                    Log(TestLogLevel.Info, $"[{result.VMName}] 스냅샷 복원: {step.SnapshotName}");
                    if (!await RevertSnapshotWithRetryAsync(vmxPath, step.SnapshotName, result.VMName))
                    {
                        throw new Exception($"스냅샷 복원 실패 (재시도 후): {step.SnapshotName}");
                    }
                }
                else
                {
                    Log(TestLogLevel.Warning, $"[{result.VMName}] 스냅샷 미지정 - 현재 상태에서 실행");
                }

                // 2. VM 부팅 대기
                ReportProgress(myStep, _totalStepCount, step.Name, result.VMName, TestProgressPhase.WaitingForBoot);
                Log(TestLogLevel.Info, $"[{result.VMName}] VM 부팅 대기 중...");

                if (!await _vmwareService.PowerOnAsync(vmxPath))
                {
                    throw new Exception("VM 전원 켜기 실패");
                }

                if (!await _vmwareService.WaitForToolsAsync(vmxPath, 300))
                {
                    throw new Exception("VMware Tools 준비 대기 시간 초과");
                }

                // 3. Guest 로그인
                Log(TestLogLevel.Info, $"[{result.VMName}] Guest 로그인: {username}");
                if (!await _vmwareService.LoginToGuestAsync(vmxPath, username, password))
                {
                    throw new Exception("Guest OS 로그인 실패");
                }

                // 3.5. 네트워크 분리 (오프라인 테스트용) — 파일 복사 전에는 연결 유지
                // ForceNetworkDisconnect는 파일 복사 후, 테스트 실행 직전에 처리

                // 4. 파일 복사 (호스트 → VM)
                ReportProgress(myStep, _totalStepCount, step.Name, result.VMName, TestProgressPhase.CopyingFiles);
                Log(TestLogLevel.Info, $"[{result.VMName}] 파일 복사 시작 ({step.FilesToCopyToVM.Count}개)");
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

                // 4.5. 네트워크 분리 (파일 복사 완료 후)
                if (step.ForceNetworkDisconnect)
                {
                    Log(TestLogLevel.Info, $"[{result.VMName}] 네트워크 분리 (오프라인 테스트)");
                    await DisconnectNetworkAsync(vmxPath, username, password);
                }

                // 5. 테스트 실행
                ReportProgress(myStep, _totalStepCount, step.Name, result.VMName, TestProgressPhase.ExecutingTest);
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

                Log(TestLogLevel.Debug, $"[{result.VMName}] 실행 완료: Exit Code={execResult.ExitCode}, 경과={stopwatch.Elapsed:hh\\:mm\\:ss}");

                // 5.1. 네트워크 복원 (결과 수집 전)
                if (step.ForceNetworkDisconnect)
                {
                    Log(TestLogLevel.Info, $"[{result.VMName}] 네트워크 복원");
                    await ReconnectNetworkAsync(vmxPath, username, password);
                }

                // 5.5. 실행 후 대기
                if (step.WaitAfterExecution != null && step.WaitAfterExecution.HasWait)
                {
                    var waitTime = step.WaitAfterExecution.ToTimeSpan();
                    Log(TestLogLevel.Info, $"[{result.VMName}] 실행 후 대기: {step.WaitAfterExecution}");
                    ReportProgress(myStep, _totalStepCount, step.Name, result.VMName, TestProgressPhase.WaitingAfterExecution);

                    var elapsed = TimeSpan.Zero;
                    var interval = TimeSpan.FromSeconds(10);
                    var screenshotInterval = step.CaptureScreenshots && step.ScreenshotIntervalSeconds > 0
                        ? TimeSpan.FromSeconds(step.ScreenshotIntervalSeconds) : TimeSpan.Zero;
                    var lastScreenshotTime = TimeSpan.Zero;

                    while (elapsed < waitTime)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested) break;
                        var remaining = waitTime - elapsed;
                        var sleepTime = remaining < interval ? remaining : interval;
                        await Task.Delay(sleepTime, _cancellationTokenSource.Token);
                        elapsed += sleepTime;
                        Log(TestLogLevel.Debug, $"[{result.VMName}] 대기 중... {elapsed:hh\\:mm\\:ss} / {waitTime:hh\\:mm\\:ss}");

                        // 주기적 스크린샷 캡처
                        if (screenshotInterval > TimeSpan.Zero && (elapsed - lastScreenshotTime) >= screenshotInterval)
                        {
                            lastScreenshotTime = elapsed;
                            var ssPath = Path.Combine(GetResultDirectory(step),
                                $"{result.VMName}_{step.Name}_{elapsed.TotalSeconds:F0}s.png");
                            if (await _vmwareService.CaptureScreenshotAsync(vmxPath, ssPath))
                            {
                                result.ScreenshotPaths.Add(ssPath);
                            }
                        }
                    }
                    Log(TestLogLevel.Info, $"[{result.VMName}] 대기 완료");
                }

                // 6. 결과 파일 수집
                ReportProgress(myStep, _totalStepCount, step.Name, result.VMName, TestProgressPhase.CollectingResults);
                Log(TestLogLevel.Info, $"[{result.VMName}] 결과 파일 수집 ({step.ResultFilesToCollect.Count}개)");

                var macroContext = new MacroContext
                {
                    VMName = result.VMName,
                    VMPath = vmxPath,
                    StepName = step.Name,
                    ScenarioName = result.TestStepName
                };

                foreach (var file in step.ResultFilesToCollect)
                {
                    // 기존 매크로와 MacroService 매크로 모두 지원
                    var hostPath = file.DestinationPath
                        .Replace("{ResultDir}", GetResultDirectory(step))
                        .Replace("{VMName}", result.VMName)
                        .Replace("{StepName}", step.Name)
                        .Replace("{Timestamp}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    hostPath = _macroService.ExpandMacros(hostPath, macroContext);

                    var guestPath = _macroService.ExpandMacros(file.SourcePath, macroContext);

                    Log(TestLogLevel.Debug, $"[{result.VMName}] 결과 수집: {guestPath} → {hostPath}");

                    if (await _vmwareService.CopyFileFromGuestAsync(vmxPath, guestPath, hostPath))
                    {
                        result.CollectedFilePaths.Add(hostPath);
                    }
                }

                // 7. 스크린샷 캡처 (옵션) — 최종 스크린샷
                if (step.CaptureScreenshots)
                {
                    var screenshotPath = Path.Combine(GetResultDirectory(step), $"{result.VMName}_{step.Name}_final.png");
                    Log(TestLogLevel.Debug, $"[{result.VMName}] 최종 스크린샷 캡처: {screenshotPath}");
                    if (await _vmwareService.CaptureScreenshotAsync(vmxPath, screenshotPath))
                    {
                        result.ScreenshotPaths.Add(screenshotPath);
                    }
                }

                // 8. 성공 여부 판단
                result.Status = EvaluateSuccess(step.SuccessCriteria, result)
                    ? TestResultStatus.Passed
                    : TestResultStatus.Failed;

                // 9. 스냅샷 복원 (비파괴적 테스트용 - 완료 후 스냅샷 복원)
                if (step.ForceSnapshotRevertAfter && !string.IsNullOrWhiteSpace(step.SnapshotName))
                {
                    Log(TestLogLevel.Info, $"[{result.VMName}] 완료 후 스냅샷 복원: {step.SnapshotName}");
                    await _vmwareService.RevertToSnapshotAsync(vmxPath, step.SnapshotName);
                }
                else if (step.ForceSnapshotRevertAfter)
                {
                    Log(TestLogLevel.Warning, $"[{result.VMName}] 완료 후 스냅샷 복원 건너뜀 - 스냅샷 미지정");
                }

                ReportProgress(myStep, _totalStepCount, step.Name, result.VMName,
                    result.Status == TestResultStatus.Passed ? TestProgressPhase.Completed : TestProgressPhase.Failed);

                Log(result.Status == TestResultStatus.Passed ? TestLogLevel.Info : TestLogLevel.Error,
                    $"[{result.VMName}] {step.Name}: {result.Status} (총 소요={stopwatch.Elapsed:hh\\:mm\\:ss})");
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

        /// <summary>
        /// 스텝 실행 조건 평가
        /// </summary>
        private bool EvaluateStepCondition(TestStep step, List<TestResult> previousResults)
        {
            if (step.Condition == null)
                return true;

            switch (step.Condition.Type)
            {
                case ConditionType.Always:
                    return true;

                case ConditionType.PreviousStepPassed:
                    if (previousResults.Count == 0) return true;
                    return previousResults[previousResults.Count - 1].Status == TestResultStatus.Passed;

                case ConditionType.PreviousStepFailed:
                    if (previousResults.Count == 0) return false;
                    return previousResults[previousResults.Count - 1].Status == TestResultStatus.Failed;

                case ConditionType.SpecificStepResult:
                {
                    var refId = step.Condition.ReferenceStepId;
                    if (string.IsNullOrEmpty(refId)) return true;
                    var refResult = previousResults.FirstOrDefault(r => r.TestStepId == refId);
                    if (refResult == null) return false;
                    switch (step.Condition.ExpectedResult)
                    {
                        case ExpectedResult.Passed:
                            return refResult.Status == TestResultStatus.Passed;
                        case ExpectedResult.Failed:
                            return refResult.Status == TestResultStatus.Failed;
                        case ExpectedResult.Any:
                            return true;
                        default:
                            return true;
                    }
                }

                case ConditionType.AllPreviousPassed:
                    if (previousResults.Count == 0) return true;
                    return previousResults.All(r => r.Status == TestResultStatus.Passed);

                case ConditionType.AnyPreviousFailed:
                    return previousResults.Any(r => r.Status == TestResultStatus.Failed);

                default:
                    return true;
            }
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

            // 미포함 문자열 체크
            if (!string.IsNullOrEmpty(criteria.NotContainsText))
            {
                if (!string.IsNullOrEmpty(result.Output) && result.Output.Contains(criteria.NotContainsText))
                    return false;
            }

            // JSON 경로 기반 값 체크
            if (!string.IsNullOrEmpty(criteria.ResultJsonPath) && !string.IsNullOrEmpty(criteria.ExpectedJsonValue))
            {
                if (!EvaluateJsonPath(result, criteria.ResultJsonPath, criteria.ExpectedJsonValue))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 수집된 결과 파일에서 간단한 JSON 키 경로로 값을 비교한다.
        /// 경로 형식: "key1.key2.key3" (단순 dot 표기)
        /// </summary>
        private bool EvaluateJsonPath(TestResult result, string jsonPath, string expectedValue)
        {
            try
            {
                // 수집된 JSON 결과 파일에서 확인
                foreach (var filePath in result.CollectedFilePaths)
                {
                    if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
                        continue;

                    var json = File.ReadAllText(filePath);
                    var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                    var token = obj.SelectToken(jsonPath);

                    if (token != null)
                    {
                        var actualValue = token.ToString();
                        if (string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }

                // 출력에서도 JSON 파싱 시도
                if (!string.IsNullOrEmpty(result.Output))
                {
                    try
                    {
                        var obj = Newtonsoft.Json.Linq.JObject.Parse(result.Output);
                        var token = obj.SelectToken(jsonPath);
                        if (token != null)
                        {
                            return string.Equals(token.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    catch
                    {
                        // 출력이 JSON이 아니면 무시
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Log(TestLogLevel.Warning, $"JSON 경로 평가 실패: {jsonPath} - {ex.Message}");
                return false;
            }
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

        /// <summary>
        /// 스냅샷 복원을 최대 3회 재시도 (VMware 일시적 오류 대응)
        /// </summary>
        private async Task<bool> RevertSnapshotWithRetryAsync(string vmxPath, string snapshotName, string vmName, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                if (await _vmwareService.RevertToSnapshotAsync(vmxPath, snapshotName))
                    return true;

                if (attempt < maxRetries)
                {
                    var waitSeconds = attempt * 5; // 5초, 10초 대기
                    Log(TestLogLevel.Warning, $"[{vmName}] 스냅샷 복원 실패 (시도 {attempt}/{maxRetries}), {waitSeconds}초 후 재시도...");
                    await Task.Delay(waitSeconds * 1000, _cancellationTokenSource.Token);
                }
                else
                {
                    Log(TestLogLevel.Error, $"[{vmName}] 스냅샷 복원 최종 실패 ({maxRetries}회 시도): {snapshotName}");
                }
            }
            return false;
        }

        /// <summary>
        /// VM 네트워크 어댑터 분리 (vmrun을 통한 스크립트 실행)
        /// </summary>
        private async Task DisconnectNetworkAsync(string vmxPath, string username, string password)
        {
            try
            {
                // Windows Guest: netsh로 모든 네트워크 어댑터 비활성화
                await _vmwareService.RunScriptInGuestAsync(vmxPath, "cmd.exe",
                    "/c \"wmic path win32_networkadapter where \\\"NetConnectionStatus=2\\\" call disable\"", 30);
            }
            catch (Exception ex)
            {
                Log(TestLogLevel.Warning, $"네트워크 분리 실패 (계속 진행): {ex.Message}");
            }
        }

        /// <summary>
        /// VM 네트워크 어댑터 복원
        /// </summary>
        private async Task ReconnectNetworkAsync(string vmxPath, string username, string password)
        {
            try
            {
                await _vmwareService.RunScriptInGuestAsync(vmxPath, "cmd.exe",
                    "/c \"wmic path win32_networkadapter where \\\"NetConnectionStatus!=2\\\" call enable\"", 30);
                // 네트워크 복원 후 안정화 대기
                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                Log(TestLogLevel.Warning, $"네트워크 복원 실패: {ex.Message}");
            }
        }

        private string GetResultDirectory(TestStep step)
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results", DateTime.Now.ToString("yyyyMMdd"), step.Name);
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
            double percent = total > 0 ? (double)current / total * 100.0 : 0;
            // phase 내 세부 진행률 반영
            if (total > 0)
            {
                double stepBase = (double)(current - 1) / total * 100.0;
                double stepRange = 100.0 / total;
                double phaseWeight = GetPhaseWeight(phase);
                percent = stepBase + stepRange * phaseWeight;
            }

            ProgressChanged?.Invoke(this, new TestProgressEventArgs
            {
                CurrentStep = current,
                TotalSteps = total,
                ProgressPercent = Math.Min(percent, 100),
                CurrentStepName = stepName,
                VMName = vmName,
                Phase = phase
            });
        }

        private double GetPhaseWeight(TestProgressPhase phase)
        {
            switch (phase)
            {
                case TestProgressPhase.Initializing: return 0.0;
                case TestProgressPhase.RevertingSnapshot: return 0.1;
                case TestProgressPhase.WaitingForBoot: return 0.2;
                case TestProgressPhase.CopyingFiles: return 0.35;
                case TestProgressPhase.ExecutingTest: return 0.5;
                case TestProgressPhase.WaitingAfterExecution: return 0.7;
                case TestProgressPhase.CollectingResults: return 0.85;
                case TestProgressPhase.Completed: return 1.0;
                case TestProgressPhase.Failed: return 1.0;
                default: return 0.5;
            }
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
                .Replace("{ResultDir}", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results", DateTime.Now.ToString("yyyyMMdd")));

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
