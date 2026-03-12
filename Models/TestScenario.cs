using System;
using System.Collections.Generic;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// 테스트 시나리오 (테스트 Step들의 묶음)
    /// </summary>
    public class TestScenario
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastRunAt { get; set; }

        /// <summary>
        /// 시나리오에 포함된 테스트 Step 목록
        /// </summary>
        public List<TestStep> Steps { get; set; } = new List<TestStep>();

        /// <summary>
        /// 동시 실행 할 수 있는 최대 VM 수
        /// </summary>
        public int MaxParallelVMs { get; set; } = 1;

        /// <summary>
        /// 실패 시 계속 진행 여부
        /// </summary>
        public bool ContinueOnFailure { get; set; } = true;

        /// <summary>
        /// 실패한 TC 자동 재시도 횟수 (0=재시도 없음)
        /// </summary>
        public int MaxRetryCount { get; set; } = 1;

        /// <summary>
        /// 테스트 대상 파일 목록 (VM별로 분배)
        /// 여러 파일을 미리 지정하면 각 VM에 하나씩 분배됨
        /// </summary>
        public List<TestTargetFile> TestTargetFiles { get; set; } = new List<TestTargetFile>();

        /// <summary>
        /// 대상 VM 경로 목록 (병렬 실행 시 사용할 VM들)
        /// </summary>
        public List<string> TargetVMPaths { get; set; } = new List<string>();

        /// <summary>
        /// 테스트 실행 전 이벤트
        /// </summary>
        public ScenarioEvent PreTestEvent { get; set; }

        /// <summary>
        /// 테스트 실행 후 이벤트
        /// </summary>
        public ScenarioEvent PostTestEvent { get; set; }
    }

    /// <summary>
    /// 테스트 대상 파일 (VM에 분배할 파일)
    /// </summary>
    public class TestTargetFile
    {
        /// <summary>
        /// 호스트 PC의 파일 경로
        /// </summary>
        public string HostFilePath { get; set; }

        /// <summary>
        /// VM 내 업로드 대상 경로
        /// </summary>
        public string VMDestinationPath { get; set; }

        /// <summary>
        /// 파일 설명
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// 시나리오 이벤트 설정
    /// </summary>
    public class ScenarioEvent
    {
        /// <summary>
        /// 이벤트 활성화 여부
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 이벤트 유형
        /// </summary>
        public EventType Type { get; set; } = EventType.Command;

        /// <summary>
        /// 실행할 명령 또는 스크립트 경로
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// 명령 인수
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// 작업 디렉토리
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// 타임아웃 (초)
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// 실패 시 테스트 중단 여부 (Pre 이벤트용)
        /// </summary>
        public bool StopOnFailure { get; set; } = true;

        /// <summary>
        /// 조건부 실행 (Post 이벤트용)
        /// </summary>
        public PostEventCondition RunCondition { get; set; } = PostEventCondition.Always;

        /// <summary>
        /// 창 숨김 여부
        /// </summary>
        public bool HideWindow { get; set; } = true;

        /// <summary>
        /// 환경 변수 (키=값 형태)
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 이벤트 유형
    /// </summary>
    public enum EventType
    {
        Command,
        PowerShell,
        BatchFile,
        Executable
    }

    /// <summary>
    /// Post 이벤트 실행 조건
    /// </summary>
    public enum PostEventCondition
    {
        Always,
        OnSuccess,
        OnFailure
    }
}
