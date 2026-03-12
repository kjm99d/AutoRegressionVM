using System;
using System.Collections.Generic;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// 개별 테스트 Step
    /// </summary>
    public class TestStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }

        /// <summary>
        /// 대상 VM의 VMX 경로
        /// </summary>
        public string TargetVmxPath { get; set; }

        /// <summary>
        /// 복원할 스냅샷 이름
        /// </summary>
        public string SnapshotName { get; set; }

        /// <summary>
        /// 호스트 → VM 파일 복사 목록
        /// </summary>
        public List<FileCopyInfo> FilesToCopyToVM { get; set; } = new List<FileCopyInfo>();

        /// <summary>
        /// 실행 설정
        /// </summary>
        public ExecutionInfo Execution { get; set; } = new ExecutionInfo();

        /// <summary>
        /// 실행 후 대기 시간 설정
        /// </summary>
        public WaitTime WaitAfterExecution { get; set; } = new WaitTime();

        /// <summary>
        /// VM → 호스트 결과 파일 수집 목록
        /// </summary>
        public List<FileCopyInfo> ResultFilesToCollect { get; set; } = new List<FileCopyInfo>();

        /// <summary>
        /// 성공 기준
        /// </summary>
        public SuccessCriteria SuccessCriteria { get; set; } = new SuccessCriteria();

        /// <summary>
        /// 실행 중 네트워크 강제 분리 (오프라인 테스트용)
        /// </summary>
        public bool ForceNetworkDisconnect { get; set; } = true;

        /// <summary>
        /// 실행 중 스크린샷 캡처
        /// </summary>
        public bool CaptureScreenshots { get; set; } = false;

        /// <summary>
        /// 스크린샷 캡처 간격 (초)
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
    /// 실행 후 대기 시간
    /// </summary>
    public class WaitTime
    {
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }

        public TimeSpan ToTimeSpan() => new TimeSpan(Hours, Minutes, Seconds);

        public bool HasWait => Hours > 0 || Minutes > 0 || Seconds > 0;

        public override string ToString()
        {
            if (!HasWait) return "없음";
            var parts = new List<string>();
            if (Hours > 0) parts.Add($"{Hours}시간");
            if (Minutes > 0) parts.Add($"{Minutes}분");
            if (Seconds > 0) parts.Add($"{Seconds}초");
            return string.Join(" ", parts);
        }
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
    /// 파일 복사 정보
    /// </summary>
    public class FileCopyInfo
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
    }

    /// <summary>
    /// 실행 정보
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
        Program,    // exe 직접 실행
        Script,     // bat, ps1 스크립트
        Command     // cmd /c "명령어"
    }

    /// <summary>
    /// 성공 기준
    /// </summary>
    public class SuccessCriteria
    {
        /// <summary>
        /// 기대 Exit Code (null이면 체크 안함)
        /// </summary>
        public int? ExpectedExitCode { get; set; } = 0;

        /// <summary>
        /// 결과 파일에서 확인할 JSON 경로와 값
        /// </summary>
        public string ResultJsonPath { get; set; }
        public string ExpectedJsonValue { get; set; }

        /// <summary>
        /// 출력에 포함되어야 할 문자열
        /// </summary>
        public string ContainsText { get; set; }

        /// <summary>
        /// 출력에 포함되면 안되는 문자열
        /// </summary>
        public string NotContainsText { get; set; }
    }
}
