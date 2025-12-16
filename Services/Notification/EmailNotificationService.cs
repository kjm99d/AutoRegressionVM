using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.Notification
{
    /// <summary>
    /// Email 알림 서비스
    /// </summary>
    public class EmailNotificationService : INotificationService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _username;
        private readonly string _password;
        private readonly string _toAddress;
        private readonly string _fromAddress;

        public EmailNotificationService(string smtpServer, int smtpPort, string username, string password, string toAddress)
        {
            _smtpServer = smtpServer;
            _smtpPort = smtpPort;
            _username = username;
            _password = password;
            _toAddress = toAddress;
            _fromAddress = username;
        }

        public async Task SendTestStartedAsync(TestScenario scenario)
        {
            var subject = $"[AutoRegressionVM] 테스트 시작: {scenario.Name}";
            var body = $@"
<h2>?? 테스트 시작</h2>
<table>
    <tr><td><strong>시나리오:</strong></td><td>{scenario.Name}</td></tr>
    <tr><td><strong>Steps:</strong></td><td>{scenario.Steps.Count}개</td></tr>
    <tr><td><strong>시작 시간:</strong></td><td>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td></tr>
</table>
";
            await SendEmailAsync(subject, body);
        }

        public async Task SendTestCompletedAsync(ScenarioResult result)
        {
            var status = result.IsSuccess ? "성공 ?" : "실패 ?";
            var subject = $"[AutoRegressionVM] 테스트 완료 - {status}: {result.ScenarioName}";
            
            var resultRows = "";
            foreach (var testResult in result.TestResults)
            {
                var statusEmoji = testResult.Status == TestResultStatus.Passed ? "?" : "?";
                resultRows += $"<tr><td>{statusEmoji}</td><td>{testResult.TestStepName}</td><td>{testResult.VMName}</td><td>{testResult.Duration:mm\\:ss}</td></tr>";
            }

            var body = $@"
<h2>{(result.IsSuccess ? "?" : "?")} 테스트 완료 - {status}</h2>
<table>
    <tr><td><strong>시나리오:</strong></td><td>{result.ScenarioName}</td></tr>
    <tr><td><strong>소요시간:</strong></td><td>{result.Duration:hh\:mm\:ss}</td></tr>
    <tr><td><strong>성공:</strong></td><td>{result.PassedCount}개</td></tr>
    <tr><td><strong>실패:</strong></td><td>{result.FailedCount}개</td></tr>
    <tr><td><strong>스킵:</strong></td><td>{result.SkippedCount}개</td></tr>
    <tr><td><strong>오류:</strong></td><td>{result.ErrorCount}개</td></tr>
</table>

<h3>상세 결과</h3>
<table border='1' cellpadding='5'>
    <tr><th>상태</th><th>테스트</th><th>VM</th><th>소요시간</th></tr>
    {resultRows}
</table>
";
            await SendEmailAsync(subject, body);
        }

        public async Task SendTestFailedAsync(TestResult result)
        {
            var subject = $"[AutoRegressionVM] 테스트 실패: {result.TestStepName}";
            var body = $@"
<h2>? 테스트 실패</h2>
<table>
    <tr><td><strong>테스트:</strong></td><td>{result.TestStepName}</td></tr>
    <tr><td><strong>VM:</strong></td><td>{result.VMName}</td></tr>
    <tr><td><strong>오류:</strong></td><td>{result.ErrorMessage ?? "알 수 없음"}</td></tr>
</table>
";
            await SendEmailAsync(subject, body);
        }

        public async Task SendErrorAsync(string errorMessage)
        {
            var subject = "[AutoRegressionVM] 오류 발생";
            var body = $@"
<h2>?? 오류 발생</h2>
<p>{errorMessage}</p>
";
            await SendEmailAsync(subject, body);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var subject = "[AutoRegressionVM] 알림 테스트";
                var body = "<p>AutoRegressionVM 알림 테스트 메시지입니다.</p>";
                await SendEmailAsync(subject, body);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task SendEmailAsync(string subject, string body)
        {
            if (string.IsNullOrEmpty(_smtpServer) || string.IsNullOrEmpty(_toAddress))
                return;

            try
            {
                using (var client = new SmtpClient(_smtpServer, _smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(_username, _password);

                    var message = new MailMessage
                    {
                        From = new MailAddress(_fromAddress, "AutoRegressionVM"),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };
                    message.To.Add(_toAddress);

                    await client.SendMailAsync(message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email 전송 실패: {ex.Message}");
            }
        }
    }
}
