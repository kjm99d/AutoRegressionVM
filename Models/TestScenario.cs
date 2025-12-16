using System;
using System.Collections.Generic;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// �׽�Ʈ �ó����� (�׽�Ʈ Step���� ����)
    /// </summary>
    public class TestScenario
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastRunAt { get; set; }

        /// <summary>
        /// �ó������� ���Ե� �׽�Ʈ Step ���
        /// </summary>
        public List<TestStep> Steps { get; set; } = new List<TestStep>();

        /// <summary>
        /// ���� ���� �� �ִ� ���� VM ��
        /// </summary>
        public int MaxParallelVMs { get; set; } = 1;

        /// <summary>
        /// ���� �� ��� ���� ����
        /// </summary>
        public bool ContinueOnFailure { get; set; } = true;

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
        /// <summary>
        /// 명령줄 실행
        /// </summary>
        Command,

        /// <summary>
        /// PowerShell 스크립트
        /// </summary>
        PowerShell,

        /// <summary>
        /// 배치 파일
        /// </summary>
        BatchFile,

        /// <summary>
        /// 실행 파일
        /// </summary>
        Executable
    }

    /// <summary>
    /// Post 이벤트 실행 조건
    /// </summary>
    public enum PostEventCondition
    {
        /// <summary>
        /// 항상 실행
        /// </summary>
        Always,

        /// <summary>
        /// 테스트 성공 시에만
        /// </summary>
        OnSuccess,

        /// <summary>
        /// 테스트 실패 시에만
        /// </summary>
        OnFailure
    }
}
