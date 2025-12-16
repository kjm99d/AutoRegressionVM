using System;
using System.Collections.Generic;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// 테스트 결과
    /// </summary>
    public class TestResult
    {
        public string TestStepId { get; set; }
        public string TestStepName { get; set; }
        public string VMName { get; set; }
        
        public TestResultStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;

        public int? ExitCode { get; set; }
        public string Output { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 수집된 스크린샷 경로 목록
        /// </summary>
        public List<string> ScreenshotPaths { get; set; } = new List<string>();

        /// <summary>
        /// 수집된 결과 파일 경로 목록
        /// </summary>
        public List<string> CollectedFilePaths { get; set; } = new List<string>();
    }

    public enum TestResultStatus
    {
        Pending,
        Running,
        Passed,
        Failed,
        Skipped,
        Timeout,
        Error
    }

    /// <summary>
    /// 시나리오 전체 실행 결과
    /// </summary>
    public class ScenarioResult
    {
        public string ScenarioId { get; set; }
        public string ScenarioName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;

        public List<TestResult> TestResults { get; set; } = new List<TestResult>();

        public int TotalCount => TestResults.Count;
        public int PassedCount => TestResults.FindAll(r => r.Status == TestResultStatus.Passed).Count;
        public int FailedCount => TestResults.FindAll(r => r.Status == TestResultStatus.Failed).Count;
        public int SkippedCount => TestResults.FindAll(r => r.Status == TestResultStatus.Skipped).Count;
        public int ErrorCount => TestResults.FindAll(r => r.Status == TestResultStatus.Error || r.Status == TestResultStatus.Timeout).Count;

        public bool IsSuccess => FailedCount == 0 && ErrorCount == 0;
    }
}
