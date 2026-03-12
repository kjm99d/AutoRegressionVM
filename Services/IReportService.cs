using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services
{
    /// <summary>
    /// 테스트 결과 리포트 생성 서비스 인터페이스
    /// </summary>
    public interface IReportService
    {
        string GenerateHtmlReport(ScenarioResult result, string outputPath = null);
        string GenerateJsonReport(ScenarioResult result, string outputPath = null);
        string GetReportsDirectory();
    }
}
