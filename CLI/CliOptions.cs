namespace AutoRegressionVM.CLI
{
    /// <summary>
    /// CLI 옵션
    /// </summary>
    public class CliOptions
    {
        /// <summary>
        /// CLI 모드 실행
        /// </summary>
        public bool CliMode { get; set; }

        /// <summary>
        /// 실행할 시나리오 이름
        /// </summary>
        public string ScenarioName { get; set; }

        /// <summary>
        /// 특정 VM에서만 실행
        /// </summary>
        public string VMName { get; set; }

        /// <summary>
        /// 병렬 실행 VM 수
        /// </summary>
        public int? Parallel { get; set; }

        /// <summary>
        /// 출력 형식 (text, json, xml)
        /// </summary>
        public string OutputFormat { get; set; } = "text";

        /// <summary>
        /// 리포트 저장 경로
        /// </summary>
        public string ReportPath { get; set; }

        /// <summary>
        /// 타임아웃 (분)
        /// </summary>
        public int? TimeoutMinutes { get; set; }

        /// <summary>
        /// 시나리오 목록 조회
        /// </summary>
        public bool ListScenarios { get; set; }

        /// <summary>
        /// VM 목록 조회
        /// </summary>
        public bool ListVMs { get; set; }

        /// <summary>
        /// 도움말 표시
        /// </summary>
        public bool ShowHelp { get; set; }

        /// <summary>
        /// 상세 로그 출력
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// 드라이런 (실제 실행 없이 검증만)
        /// </summary>
        public bool DryRun { get; set; }
    }
}
