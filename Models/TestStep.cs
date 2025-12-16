using System.Collections.Generic;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// ���� �׽�Ʈ Step
    /// </summary>
    public class TestStep
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }

        /// <summary>
        /// ��� VM�� VMX ���
        /// </summary>
        public string TargetVmxPath { get; set; }

        /// <summary>
        /// �ѹ��� ������ �̸�
        /// </summary>
        public string SnapshotName { get; set; }

        /// <summary>
        /// ȣ��Ʈ �� VM ������ ���� ���
        /// </summary>
        public List<FileCopyInfo> FilesToCopyToVM { get; set; } = new List<FileCopyInfo>();

        /// <summary>
        /// ���� ����
        /// </summary>
        public ExecutionInfo Execution { get; set; } = new ExecutionInfo();

        /// <summary>
        /// VM �� ȣ��Ʈ ������ ��� ���� ���
        /// </summary>
        public List<FileCopyInfo> ResultFilesToCollect { get; set; } = new List<FileCopyInfo>();

        /// <summary>
        /// ���� ����
        /// </summary>
        public SuccessCriteria SuccessCriteria { get; set; } = new SuccessCriteria();

        /// <summary>
        /// ���� �� ��Ʈ��ũ ���� ���� (�Ǽ��ڵ� �׽�Ʈ��)
        /// </summary>
        public bool ForceNetworkDisconnect { get; set; } = true;

        /// <summary>
        /// ���� �� ��ũ���� ĸó
        /// </summary>
        public bool CaptureScreenshots { get; set; } = false;

        /// <summary>
        /// ��ũ���� ĸó ���� (��)
        /// </summary>
        public int ScreenshotIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// 완료 후 스냅샷 복원
        /// </summary>
        public bool ForceSnapshotRevertAfter { get; set; } = true;

        /// <summary>
        /// 조건부 실행 설정
        /// </summary>
        public StepCondition Condition { get; set; }
    }

    /// <summary>
    /// 스텝 실행 조건
    /// </summary>
    public class StepCondition
    {
        /// <summary>
        /// 조건 유형
        /// </summary>
        public ConditionType Type { get; set; } = ConditionType.Always;

        /// <summary>
        /// 참조할 이전 스텝 ID (특정 스텝 결과 참조 시)
        /// </summary>
        public string ReferenceStepId { get; set; }

        /// <summary>
        /// 참조할 이전 스텝 이름 (표시용)
        /// </summary>
        public string ReferenceStepName { get; set; }

        /// <summary>
        /// 기대하는 결과 상태
        /// </summary>
        public ExpectedResult ExpectedResult { get; set; } = ExpectedResult.Passed;
    }

    public enum ConditionType
    {
        Always,              // 항상 실행
        PreviousStepPassed,  // 바로 이전 스텝 성공 시
        PreviousStepFailed,  // 바로 이전 스텝 실패 시
        SpecificStepResult,  // 특정 스텝 결과에 따라
        AllPreviousPassed,   // 모든 이전 스텝 성공 시
        AnyPreviousFailed    // 하나라도 실패 시
    }

    public enum ExpectedResult
    {
        Passed,
        Failed,
        Any
    }

    /// <summary>
    /// ���� ���� ����
    /// </summary>
    public class FileCopyInfo
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
    }

    /// <summary>
    /// ���� ����
    /// </summary>
    public class ExecutionInfo
    {
        public ExecutionType Type { get; set; } = ExecutionType.Program;
        public string ExecutablePath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public int TimeoutSeconds { get; set; } = 300;
        public bool WaitForExit { get; set; } = true;
    }

    public enum ExecutionType
    {
        Program,    // exe ���� ����
        Script,     // bat, ps1 ��ũ��Ʈ
        Command     // cmd /c "���ɾ�"
    }

    /// <summary>
    /// ���� ����
    /// </summary>
    public class SuccessCriteria
    {
        /// <summary>
        /// ���� Exit Code (null�̸� üũ ����)
        /// </summary>
        public int? ExpectedExitCode { get; set; } = 0;

        /// <summary>
        /// ��� ���Ͽ��� Ȯ���� JSON ��ο� ��
        /// </summary>
        public string ResultJsonPath { get; set; }
        public string ExpectedJsonValue { get; set; }

        /// <summary>
        /// ����� ���ԵǾ�� �� ���ڿ�
        /// </summary>
        public string ContainsText { get; set; }

        /// <summary>
        /// ����� ���ԵǸ� �ȵǴ� ���ڿ�
        /// </summary>
        public string NotContainsText { get; set; }
    }
}
