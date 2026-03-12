using System;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.TestExecution
{
    /// <summary>
    /// �׽�Ʈ ����� �������̽�
    /// </summary>
    public interface ITestRunner
    {
        /// <summary>
        /// �ó����� ����
        /// </summary>
        Task<ScenarioResult> RunScenarioAsync(TestScenario scenario);

        /// <summary>
        /// ���� Step ����
        /// </summary>
        Task<TestResult> RunStepAsync(TestStep step, VMInfo vm);

        /// <summary>
        /// ���� ���
        /// </summary>
        void Cancel();

        /// <summary>
        /// ���� �� ����
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// ���� ��Ȳ �̺�Ʈ
        /// </summary>
        event EventHandler<TestProgressEventArgs> ProgressChanged;

        /// <summary>
        /// �α� �̺�Ʈ
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
        public double ProgressPercent { get; set; }
    }

    public enum TestProgressPhase
    {
        Initializing,
        RevertingSnapshot,
        WaitingForBoot,
        CopyingFiles,
        ExecutingTest,
        WaitingAfterExecution,
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
