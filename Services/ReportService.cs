using System;
using System.IO;
using System.Linq;
using System.Text;
using AutoRegressionVM.Helpers;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services
{
    /// <summary>
    /// 테스트 결과 리포트 생성 서비스
    /// </summary>
    public class ReportService
    {
        private readonly string _reportsDirectory;

        public ReportService(string basePath = null)
        {
            var baseDir = basePath ?? AppDomain.CurrentDomain.BaseDirectory;
            _reportsDirectory = Path.Combine(baseDir, "Reports");

            if (!Directory.Exists(_reportsDirectory))
            {
                Directory.CreateDirectory(_reportsDirectory);
            }
        }

        /// <summary>
        /// HTML 리포트 생성
        /// </summary>
        public string GenerateHtmlReport(ScenarioResult result, string outputPath = null)
        {
            var fileName = $"{SanitizeFileName(result.ScenarioName)}_{result.StartTime:yyyyMMdd_HHmmss}.html";
            var filePath = outputPath ?? Path.Combine(_reportsDirectory, fileName);

            var html = GenerateHtmlContent(result);
            File.WriteAllText(filePath, html, Encoding.UTF8);

            return filePath;
        }

        /// <summary>
        /// JSON 리포트 생성
        /// </summary>
        public string GenerateJsonReport(ScenarioResult result, string outputPath = null)
        {
            var fileName = $"{SanitizeFileName(result.ScenarioName)}_{result.StartTime:yyyyMMdd_HHmmss}.json";
            var filePath = outputPath ?? Path.Combine(_reportsDirectory, fileName);

            var json = SimpleJson.Serialize(result);
            File.WriteAllText(filePath, json, Encoding.UTF8);

            return filePath;
        }

        private string GenerateHtmlContent(ScenarioResult result)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"ko\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"    <title>테스트 리포트 - {HtmlEncode(result.ScenarioName)}</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(GetCssStyles());
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // 헤더
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <header>");
            sb.AppendLine($"            <h1>{HtmlEncode(result.ScenarioName)}</h1>");
            sb.AppendLine($"            <p class=\"timestamp\">생성일시: {result.StartTime:yyyy-MM-dd HH:mm:ss}</p>");
            sb.AppendLine("        </header>");

            // 요약
            sb.AppendLine("        <section class=\"summary\">");
            sb.AppendLine("            <h2>테스트 요약</h2>");
            sb.AppendLine("            <div class=\"summary-grid\">");
            sb.AppendLine($"                <div class=\"summary-item\"><span class=\"label\">총 테스트</span><span class=\"value\">{result.TotalCount}</span></div>");
            sb.AppendLine($"                <div class=\"summary-item passed\"><span class=\"label\">성공</span><span class=\"value\">{result.PassedCount}</span></div>");
            sb.AppendLine($"                <div class=\"summary-item failed\"><span class=\"label\">실패</span><span class=\"value\">{result.FailedCount}</span></div>");
            sb.AppendLine($"                <div class=\"summary-item skipped\"><span class=\"label\">건너뜀</span><span class=\"value\">{result.SkippedCount}</span></div>");
            sb.AppendLine($"                <div class=\"summary-item error\"><span class=\"label\">오류</span><span class=\"value\">{result.ErrorCount}</span></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine($"            <p class=\"duration\">총 소요시간: {result.Duration:hh\\:mm\\:ss}</p>");
            sb.AppendLine($"            <p class=\"result-status {(result.IsSuccess ? "success" : "failure")}\">결과: {(result.IsSuccess ? "성공" : "실패")}</p>");
            sb.AppendLine("        </section>");

            // 상세 결과
            sb.AppendLine("        <section class=\"details\">");
            sb.AppendLine("            <h2>상세 결과</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead>");
            sb.AppendLine("                    <tr>");
            sb.AppendLine("                        <th>스텝</th>");
            sb.AppendLine("                        <th>VM</th>");
            sb.AppendLine("                        <th>상태</th>");
            sb.AppendLine("                        <th>소요시간</th>");
            sb.AppendLine("                        <th>Exit Code</th>");
            sb.AppendLine("                        <th>메시지</th>");
            sb.AppendLine("                    </tr>");
            sb.AppendLine("                </thead>");
            sb.AppendLine("                <tbody>");

            foreach (var testResult in result.TestResults)
            {
                var statusClass = GetStatusClass(testResult.Status);
                var statusText = GetStatusText(testResult.Status);

                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td>{HtmlEncode(testResult.TestStepName)}</td>");
                sb.AppendLine($"                        <td>{HtmlEncode(testResult.VMName)}</td>");
                sb.AppendLine($"                        <td class=\"{statusClass}\">{statusText}</td>");
                sb.AppendLine($"                        <td>{testResult.Duration:mm\\:ss}</td>");
                sb.AppendLine($"                        <td>{testResult.ExitCode?.ToString() ?? "-"}</td>");
                sb.AppendLine($"                        <td>{HtmlEncode(testResult.ErrorMessage ?? "-")}</td>");
                sb.AppendLine("                    </tr>");

                // 출력이 있으면 펼침 가능한 영역 추가
                if (!string.IsNullOrEmpty(testResult.Output))
                {
                    sb.AppendLine("                    <tr class=\"output-row\">");
                    sb.AppendLine("                        <td colspan=\"6\">");
                    sb.AppendLine("                            <details>");
                    sb.AppendLine("                                <summary>출력 보기</summary>");
                    sb.AppendLine($"                                <pre>{HtmlEncode(testResult.Output)}</pre>");
                    sb.AppendLine("                            </details>");
                    sb.AppendLine("                        </td>");
                    sb.AppendLine("                    </tr>");
                }

                // 스크린샷이 있으면 표시
                if (testResult.ScreenshotPaths != null && testResult.ScreenshotPaths.Count > 0)
                {
                    sb.AppendLine("                    <tr class=\"screenshot-row\">");
                    sb.AppendLine("                        <td colspan=\"6\">");
                    sb.AppendLine("                            <details>");
                    sb.AppendLine("                                <summary>스크린샷 보기</summary>");
                    sb.AppendLine("                                <div class=\"screenshots\">");
                    foreach (var screenshot in testResult.ScreenshotPaths)
                    {
                        var fileName = Path.GetFileName(screenshot);
                        sb.AppendLine($"                                    <img src=\"{HtmlEncode(screenshot)}\" alt=\"{HtmlEncode(fileName)}\" />");
                    }
                    sb.AppendLine("                                </div>");
                    sb.AppendLine("                            </details>");
                    sb.AppendLine("                        </td>");
                    sb.AppendLine("                    </tr>");
                }
            }

            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </section>");

            // 푸터
            sb.AppendLine("        <footer>");
            sb.AppendLine("            <p>AutoRegressionVM - 자동 리그레션 테스트</p>");
            sb.AppendLine("        </footer>");
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GetCssStyles()
        {
            return @"
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Segoe UI', Tahoma, sans-serif; background: #f5f5f5; color: #333; line-height: 1.6; }
        .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
        header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; border-radius: 10px; margin-bottom: 20px; }
        header h1 { font-size: 24px; margin-bottom: 10px; }
        header .timestamp { opacity: 0.9; font-size: 14px; }
        section { background: white; border-radius: 10px; padding: 20px; margin-bottom: 20px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        h2 { color: #333; margin-bottom: 15px; padding-bottom: 10px; border-bottom: 2px solid #eee; }
        .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 15px; margin-bottom: 20px; }
        .summary-item { text-align: center; padding: 15px; border-radius: 8px; background: #f8f9fa; }
        .summary-item .label { display: block; font-size: 12px; color: #666; margin-bottom: 5px; }
        .summary-item .value { display: block; font-size: 28px; font-weight: bold; }
        .summary-item.passed .value { color: #28a745; }
        .summary-item.failed .value { color: #dc3545; }
        .summary-item.skipped .value { color: #ffc107; }
        .summary-item.error .value { color: #fd7e14; }
        .duration { color: #666; margin-bottom: 10px; }
        .result-status { font-size: 18px; font-weight: bold; padding: 10px; border-radius: 5px; display: inline-block; }
        .result-status.success { background: #d4edda; color: #155724; }
        .result-status.failure { background: #f8d7da; color: #721c24; }
        table { width: 100%; border-collapse: collapse; }
        th, td { padding: 12px; text-align: left; border-bottom: 1px solid #eee; }
        th { background: #f8f9fa; font-weight: 600; }
        tr:hover { background: #f8f9fa; }
        .status-passed { color: #28a745; font-weight: bold; }
        .status-failed { color: #dc3545; font-weight: bold; }
        .status-error { color: #fd7e14; font-weight: bold; }
        .status-skipped { color: #ffc107; font-weight: bold; }
        .status-running { color: #17a2b8; font-weight: bold; }
        .output-row td, .screenshot-row td { background: #f8f9fa; }
        details { cursor: pointer; }
        summary { padding: 5px; color: #667eea; }
        pre { background: #2d2d2d; color: #f8f8f2; padding: 15px; border-radius: 5px; overflow-x: auto; margin-top: 10px; font-size: 12px; }
        .screenshots { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 10px; }
        .screenshots img { max-width: 300px; border-radius: 5px; box-shadow: 0 2px 5px rgba(0,0,0,0.2); }
        footer { text-align: center; padding: 20px; color: #666; font-size: 14px; }
        @media print { body { background: white; } .container { max-width: none; } section { box-shadow: none; border: 1px solid #ddd; } }
            ";
        }

        private string GetStatusClass(TestResultStatus status)
        {
            switch (status)
            {
                case TestResultStatus.Passed: return "status-passed";
                case TestResultStatus.Failed: return "status-failed";
                case TestResultStatus.Error:
                case TestResultStatus.Timeout: return "status-error";
                case TestResultStatus.Skipped: return "status-skipped";
                case TestResultStatus.Running: return "status-running";
                default: return "";
            }
        }

        private string GetStatusText(TestResultStatus status)
        {
            switch (status)
            {
                case TestResultStatus.Passed: return "성공";
                case TestResultStatus.Failed: return "실패";
                case TestResultStatus.Error: return "오류";
                case TestResultStatus.Timeout: return "타임아웃";
                case TestResultStatus.Skipped: return "건너뜀";
                case TestResultStatus.Running: return "실행중";
                case TestResultStatus.Pending: return "대기중";
                default: return status.ToString();
            }
        }

        private string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return System.Net.WebUtility.HtmlEncode(text);
        }

        private string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        /// <summary>
        /// 리포트 디렉토리 경로 반환
        /// </summary>
        public string GetReportsDirectory() => _reportsDirectory;
    }
}
