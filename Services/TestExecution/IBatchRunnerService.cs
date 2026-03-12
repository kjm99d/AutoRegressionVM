using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.TestExecution
{
    /// <summary>
    /// VM 풀 기반 시나리오 일괄 실행 서비스 인터페이스
    /// </summary>
    public interface IBatchRunnerService
    {
        /// <summary>
        /// 시나리오 목록을 VM 풀에서 병렬 실행
        /// </summary>
        Task<BatchRunResult> RunBatchAsync(IList<TestScenario> scenarios, IList<VMInfo> vmPool, int maxConcurrentVMs);

        /// <summary>
        /// 일괄 실행 취소
        /// </summary>
        void Cancel();

        /// <summary>
        /// 실행 중 여부
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 개별 시나리오 진행 상황 변경 이벤트
        /// </summary>
        event EventHandler<BatchProgressEventArgs> BatchProgressChanged;

        /// <summary>
        /// 로그 이벤트
        /// </summary>
        event EventHandler<TestLogEventArgs> LogGenerated;
    }

    public class BatchProgressEventArgs : EventArgs
    {
        public int TotalScenarios { get; set; }
        public int CompletedScenarios { get; set; }
        public string CurrentScenarioName { get; set; }
        public string AssignedVMName { get; set; }
        public BatchScenarioStatus Status { get; set; }
        public double OverallProgressPercent { get; set; }
    }
}
