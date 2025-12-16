using System;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.TestExecution
{
    /// <summary>
    /// 테스트 실행기 인터페이스
    /// </summary>
    public interface ITestRunner
    {
        /// <summary>
        /// 시나리오 실행
        /// </summary>
        Task<ScenarioResult> RunScenarioAsync(TestScenario scenario);

        /// <summary>
        /// 단일 Step 실행
        /// </summary>
        Task<TestResult> RunStepAsync(TestStep step, VMInfo vm);

        /// <summary>
        /// 실행 취소
        /// </summary>
        void Cancel();

        /// <summary>
        /// 실행 중 여부
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 진행 상황 이벤트
        /// </summary>
        event EventHandler<TestProgressEventArgs> ProgressChanged;

        /// <summary>
        /// 로그 이벤트
        /// </summary>
        event EventHandler<TestLogEventArgs> LogGenerated;
    }

    public class TestProgressEventArgs : EventArgs
    {
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public string CurrentStepName { get; set; }
        public string VMName { get; set; }
        public TestProgressPhase Phase { get; set; }
        public double ProgressPercent => TotalSteps > 0 ? (double)CurrentStep / TotalSteps * 100 : 0;
    }

    public enum TestProgressPhase
    {
        Initializing,
        RevertingSnapshot,
        WaitingForBoot,
        CopyingFiles,
        ExecutingTest,
        CollectingResults,
        Completed,
        Failed
    }

    public class TestLogEventArgs : EventArgs
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public TestLogLevel Level { get; set; }
        public string Message { get; set; }
        public string VMName { get; set; }
    }

    public enum TestLogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
