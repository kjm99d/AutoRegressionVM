using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// 일괄 실행 전체 결과
    /// </summary>
    public class BatchRunResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;

        public int MaxConcurrentVMs { get; set; }
        public List<BatchScenarioResult> ScenarioResults { get; set; } = new List<BatchScenarioResult>();

        public int TotalScenarios => ScenarioResults.Count;
        public int CompletedScenarios => ScenarioResults.Count(r => r.IsCompleted);
        public int SucceededScenarios => ScenarioResults.Count(r => r.IsCompleted && r.Result != null && r.Result.IsSuccess);
        public int FailedScenarios => ScenarioResults.Count(r => r.IsCompleted && (r.Result == null || !r.Result.IsSuccess));
        public bool IsSuccess => FailedScenarios == 0 && CompletedScenarios == TotalScenarios;
    }

    /// <summary>
    /// 일괄 실행 내 개별 시나리오 결과
    /// </summary>
    public class BatchScenarioResult
    {
        public string ScenarioId { get; set; }
        public string ScenarioName { get; set; }
        public string AssignedVMName { get; set; }
        public string AssignedVMPath { get; set; }
        public BatchScenarioStatus Status { get; set; } = BatchScenarioStatus.Queued;
        public ScenarioResult Result { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsCompleted => Status == BatchScenarioStatus.Succeeded || Status == BatchScenarioStatus.Failed;
    }

    public enum BatchScenarioStatus
    {
        Queued,
        Running,
        Succeeded,
        Failed,
        Cancelled
    }
}
