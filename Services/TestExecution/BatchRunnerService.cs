using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services.VMware;

namespace AutoRegressionVM.Services.TestExecution
{
    /// <summary>
    /// VM 풀 기반 시나리오 일괄 실행 서비스
    /// ConcurrentQueue로 VM 풀을 관리하고, SemaphoreSlim으로 동시 실행 수를 제어
    /// </summary>
    public class BatchRunnerService : IBatchRunnerService
    {
        private readonly IVMwareService _vmwareService;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public event EventHandler<BatchProgressEventArgs> BatchProgressChanged;
        public event EventHandler<TestLogEventArgs> LogGenerated;

        public BatchRunnerService(IVMwareService vmwareService)
        {
            _vmwareService = vmwareService;
        }

        public async Task<BatchRunResult> RunBatchAsync(IList<TestScenario> scenarios, IList<VMInfo> vmPool, int maxConcurrentVMs)
        {
            if (_isRunning)
                throw new InvalidOperationException("일괄 실행이 이미 진행 중입니다.");

            if (scenarios == null || scenarios.Count == 0)
                throw new ArgumentException("실행할 시나리오가 없습니다.");

            if (vmPool == null || vmPool.Count == 0)
                throw new ArgumentException("사용 가능한 VM이 없습니다.");

            _isRunning = true;
            _cts = new CancellationTokenSource();

            var result = new BatchRunResult
            {
                StartTime = DateTime.Now,
                MaxConcurrentVMs = maxConcurrentVMs
            };

            // 시나리오별 결과 초기화
            foreach (var scenario in scenarios)
            {
                result.ScenarioResults.Add(new BatchScenarioResult
                {
                    ScenarioId = scenario.Id,
                    ScenarioName = scenario.Name,
                    Status = BatchScenarioStatus.Queued
                });
            }

            var actualConcurrent = Math.Min(maxConcurrentVMs, vmPool.Count);
            actualConcurrent = Math.Min(actualConcurrent, scenarios.Count);

            Log($"일괄 실행 시작: 시나리오 {scenarios.Count}개, VM 풀 {vmPool.Count}대, 동시 실행 {actualConcurrent}대");

            // VM 풀을 ConcurrentQueue로 관리
            var availableVMs = new ConcurrentQueue<VMInfo>();
            foreach (var vm in vmPool.Take(actualConcurrent))
            {
                availableVMs.Enqueue(vm);
            }

            // 시나리오 큐
            var scenarioQueue = new ConcurrentQueue<int>();
            for (int i = 0; i < scenarios.Count; i++)
            {
                scenarioQueue.Enqueue(i);
            }

            // Per-VM semaphore로 같은 VM 동시 사용 방지
            var vmSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
            foreach (var vm in vmPool)
            {
                vmSemaphores[vm.VmxPath] = new SemaphoreSlim(1, 1);
            }

            int completedCount = 0;

            try
            {
                // 동시 실행 제어
                using (var concurrencySemaphore = new SemaphoreSlim(actualConcurrent))
                {
                    var tasks = new List<Task>();

                    for (int i = 0; i < scenarios.Count; i++)
                    {
                        if (_cts.Token.IsCancellationRequested) break;

                        await concurrencySemaphore.WaitAsync(_cts.Token);

                        var scenarioIndex = i;
                        var scenario = scenarios[scenarioIndex];
                        var batchResult = result.ScenarioResults[scenarioIndex];

                        var task = Task.Run(async () =>
                        {
                            VMInfo assignedVM = null;

                            try
                            {
                                // VM 풀에서 사용 가능한 VM 할당
                                while (!availableVMs.TryDequeue(out assignedVM))
                                {
                                    if (_cts.Token.IsCancellationRequested) return;
                                    await Task.Delay(100, _cts.Token);
                                }

                                // Per-VM semaphore 획득
                                await vmSemaphores[assignedVM.VmxPath].WaitAsync(_cts.Token);

                                batchResult.AssignedVMName = assignedVM.Name;
                                batchResult.AssignedVMPath = assignedVM.VmxPath;
                                batchResult.Status = BatchScenarioStatus.Running;

                                Log($"[{assignedVM.Name}] 시나리오 시작: {scenario.Name}");
                                RaiseProgress(result, scenario.Name, assignedVM.Name, BatchScenarioStatus.Running);

                                // 시나리오에 VM 자동 할당 (원본 변경 없이 복사본 사용)
                                var scenarioCopy = CloneScenarioForVM(scenario, assignedVM.VmxPath);

                                // 새 TestRunner 인스턴스 생성 (인스턴스 상태 격리)
                                var runner = new TestRunner(_vmwareService, new[] { assignedVM });
                                runner.LogGenerated += (s, e) =>
                                {
                                    LogGenerated?.Invoke(this, e);
                                };

                                _cts.Token.Register(() => runner.Cancel());

                                var scenarioResult = await runner.RunScenarioAsync(scenarioCopy);
                                batchResult.Result = scenarioResult;
                                batchResult.Status = scenarioResult.IsSuccess
                                    ? BatchScenarioStatus.Succeeded
                                    : BatchScenarioStatus.Failed;

                                Log($"[{assignedVM.Name}] 시나리오 완료: {scenario.Name} → {(scenarioResult.IsSuccess ? "성공" : "실패")} (성공 {scenarioResult.PassedCount}, 실패 {scenarioResult.FailedCount})");
                            }
                            catch (OperationCanceledException)
                            {
                                batchResult.Status = BatchScenarioStatus.Cancelled;
                                batchResult.ErrorMessage = "사용자에 의해 취소됨";
                                Log($"시나리오 취소됨: {scenario.Name}");
                            }
                            catch (Exception ex)
                            {
                                batchResult.Status = BatchScenarioStatus.Failed;
                                batchResult.ErrorMessage = ex.Message;
                                Log($"시나리오 오류: {scenario.Name} - {ex.Message}");
                            }
                            finally
                            {
                                // Per-VM semaphore 해제
                                if (assignedVM != null && vmSemaphores.ContainsKey(assignedVM.VmxPath))
                                {
                                    vmSemaphores[assignedVM.VmxPath].Release();
                                }

                                // VM 풀에 반환
                                if (assignedVM != null)
                                {
                                    availableVMs.Enqueue(assignedVM);
                                }

                                Interlocked.Increment(ref completedCount);
                                RaiseProgress(result, scenario.Name, assignedVM?.Name, batchResult.Status);
                                concurrencySemaphore.Release();
                            }
                        }, _cts.Token);

                        tasks.Add(task);
                    }

                    await Task.WhenAll(tasks);
                }
            }
            catch (OperationCanceledException)
            {
                // 미실행 시나리오 취소 처리
                foreach (var sr in result.ScenarioResults.Where(r => r.Status == BatchScenarioStatus.Queued))
                {
                    sr.Status = BatchScenarioStatus.Cancelled;
                    sr.ErrorMessage = "일괄 실행 취소";
                }
                Log("일괄 실행이 취소되었습니다.");
            }
            finally
            {
                result.EndTime = DateTime.Now;
                _isRunning = false;

                // semaphore 정리
                foreach (var sem in vmSemaphores.Values)
                {
                    sem.Dispose();
                }

                Log($"일괄 실행 완료: 총 {result.TotalScenarios}개, 성공 {result.SucceededScenarios}개, 실패 {result.FailedScenarios}개, 소요 {result.Duration:hh\\:mm\\:ss}");
            }

            return result;
        }

        public void Cancel()
        {
            _cts?.Cancel();
            Log("일괄 실행 취소 요청됨");
        }

        /// <summary>
        /// 시나리오 복사본을 만들어 VM을 자동 할당
        /// </summary>
        private TestScenario CloneScenarioForVM(TestScenario original, string vmxPath)
        {
            var clone = new TestScenario
            {
                Id = original.Id,
                Name = original.Name,
                Description = original.Description,
                MaxParallelVMs = 1, // 일괄 실행에서는 시나리오당 VM 1대
                ContinueOnFailure = original.ContinueOnFailure,
                MaxRetryCount = original.MaxRetryCount,
                PreTestEvent = original.PreTestEvent,
                PostTestEvent = original.PostTestEvent,
                TestTargetFiles = original.TestTargetFiles != null
                    ? new List<TestTargetFile>(original.TestTargetFiles)
                    : new List<TestTargetFile>()
            };

            // 시나리오에 대상 VM이 이미 지정되어 있으면 그대로 사용, 없으면 풀에서 할당된 VM 사용
            if (original.TargetVMPaths != null && original.TargetVMPaths.Count > 0)
            {
                clone.TargetVMPaths = new List<string>(original.TargetVMPaths);
                clone.MaxParallelVMs = original.MaxParallelVMs;
            }
            else
            {
                clone.TargetVMPaths = new List<string> { vmxPath };
            }

            // Steps 복사 — VM 미지정 Step에 할당 VM 설정
            clone.Steps = new List<TestStep>();
            foreach (var step in original.Steps)
            {
                var stepCopy = new TestStep
                {
                    Id = step.Id,
                    Order = step.Order,
                    Name = step.Name,
                    Description = step.Description,
                    TargetVmxPath = string.IsNullOrEmpty(step.TargetVmxPath) ? vmxPath : step.TargetVmxPath,
                    SnapshotName = step.SnapshotName,
                    ForceSnapshotRevertAfter = step.ForceSnapshotRevertAfter,
                    ForceNetworkDisconnect = step.ForceNetworkDisconnect,
                    CaptureScreenshots = step.CaptureScreenshots,
                    ScreenshotIntervalSeconds = step.ScreenshotIntervalSeconds,
                    Execution = step.Execution,
                    FilesToCopyToVM = step.FilesToCopyToVM,
                    ResultFilesToCollect = step.ResultFilesToCollect,
                    SuccessCriteria = step.SuccessCriteria,
                    WaitAfterExecution = step.WaitAfterExecution,
                    Condition = step.Condition
                };
                clone.Steps.Add(stepCopy);
            }

            return clone;
        }

        private void Log(string message)
        {
            LogGenerated?.Invoke(this, new TestLogEventArgs
            {
                Timestamp = DateTime.Now,
                Level = TestLogLevel.Info,
                Message = $"[일괄실행] {message}"
            });
        }

        private void RaiseProgress(BatchRunResult result, string scenarioName, string vmName, BatchScenarioStatus status)
        {
            var completed = result.ScenarioResults.Count(r => r.IsCompleted || r.Status == BatchScenarioStatus.Cancelled);
            var total = result.TotalScenarios;

            BatchProgressChanged?.Invoke(this, new BatchProgressEventArgs
            {
                TotalScenarios = total,
                CompletedScenarios = completed,
                CurrentScenarioName = scenarioName,
                AssignedVMName = vmName,
                Status = status,
                OverallProgressPercent = total > 0 ? (double)completed / total * 100 : 0
            });
        }
    }
}
