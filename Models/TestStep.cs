using System.Collections.Generic;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// 개별 테스트 Step
    /// </summary>
    public class TestStep
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }

        /// <summary>
        /// 대상 VM의 VMX 경로
        /// </summary>
        public string TargetVmxPath { get; set; }

        /// <summary>
        /// 롤백할 스냅샷 이름
        /// </summary>
        public string SnapshotName { get; set; }

        /// <summary>
        /// 호스트 → VM 복사할 파일 목록
        /// </summary>
        public List<FileCopyInfo> FilesToCopyToVM { get; set; } = new List<FileCopyInfo>();

        /// <summary>
        /// 실행 설정
        /// </summary>
        public ExecutionInfo Execution { get; set; } = new ExecutionInfo();

        /// <summary>
        /// VM → 호스트 복사할 결과 파일 목록
        /// </summary>
        public List<FileCopyInfo> ResultFilesToCollect { get; set; } = new List<FileCopyInfo>();

        /// <summary>
        /// 성공 조건
        /// </summary>
        public SuccessCriteria SuccessCriteria { get; set; } = new SuccessCriteria();

        /// <summary>
        /// 실행 전 네트워크 강제 해제 (악성코드 테스트용)
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
        /// 완료 후 무조건 스냅샷 롤백
        /// </summary>
        public bool ForceSnapshotRevertAfter { get; set; } = true;
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
    /// 성공 조건
    /// </summary>
    public class SuccessCriteria
    {
        /// <summary>
        /// 예상 Exit Code (null이면 체크 안함)
        /// </summary>
        public int? ExpectedExitCode { get; set; } = 0;

        /// <summary>
        /// 결과 파일에서 확인할 JSON 경로와 값
        /// </summary>
        public string ResultJsonPath { get; set; }
        public string ExpectedJsonValue { get; set; }

        /// <summary>
        /// 결과에 포함되어야 할 문자열
        /// </summary>
        public string ContainsText { get; set; }

        /// <summary>
        /// 결과에 포함되면 안되는 문자열
        /// </summary>
        public string NotContainsText { get; set; }
    }
}
