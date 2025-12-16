using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.Notification
{
    /// <summary>
    /// 알림 서비스 팩토리 및 관리자
    /// </summary>
    public class NotificationManager
    {
        private readonly NotificationSettings _settings;
        private INotificationService _service;

        public NotificationManager(NotificationSettings settings)
        {
            _settings = settings;
            InitializeService();
        }

        private void InitializeService()
        {
            if (_settings == null || !_settings.Enabled)
            {
                _service = null;
                return;
            }

            switch (_settings.Type)
            {
                case NotificationType.Slack:
                    _service = new SlackNotificationService(_settings.SlackWebhookUrl);
                    break;

                case NotificationType.Teams:
                    _service = new TeamsNotificationService(_settings.TeamsWebhookUrl);
                    break;

                case NotificationType.Email:
                    _service = new EmailNotificationService(
                        _settings.SmtpServer,
                        _settings.SmtpPort,
                        _settings.SmtpUsername,
                        _settings.SmtpPassword,
                        _settings.EmailTo);
                    break;

                default:
                    _service = null;
                    break;
            }
        }

        public void UpdateSettings(NotificationSettings settings)
        {
            if (settings == null) return;
            
            // 설정 복사
            _settings.Enabled = settings.Enabled;
            _settings.Type = settings.Type;
            _settings.SlackWebhookUrl = settings.SlackWebhookUrl;
            _settings.TeamsWebhookUrl = settings.TeamsWebhookUrl;
            _settings.SmtpServer = settings.SmtpServer;
            _settings.SmtpPort = settings.SmtpPort;
            _settings.SmtpUsername = settings.SmtpUsername;
            _settings.SmtpPassword = settings.SmtpPassword;
            _settings.EmailTo = settings.EmailTo;
            _settings.NotifyOnComplete = settings.NotifyOnComplete;
            _settings.NotifyOnFailure = settings.NotifyOnFailure;
            _settings.NotifyOnStart = settings.NotifyOnStart;
            _settings.NotifyOnError = settings.NotifyOnError;
            
            InitializeService();
        }

        public async Task NotifyTestStartedAsync(TestScenario scenario)
        {
            if (_service == null || !_settings.Enabled || !_settings.NotifyOnStart)
                return;

            await _service.SendTestStartedAsync(scenario);
        }

        public async Task NotifyTestCompletedAsync(ScenarioResult result)
        {
            if (_service == null || !_settings.Enabled || !_settings.NotifyOnComplete)
                return;

            await _service.SendTestCompletedAsync(result);
        }

        public async Task NotifyTestFailedAsync(TestResult result)
        {
            if (_service == null || !_settings.Enabled || !_settings.NotifyOnFailure)
                return;

            await _service.SendTestFailedAsync(result);
        }

        public async Task NotifyErrorAsync(string errorMessage)
        {
            if (_service == null || !_settings.Enabled || !_settings.NotifyOnError)
                return;

            await _service.SendErrorAsync(errorMessage);
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (_service == null)
                return false;

            return await _service.TestConnectionAsync();
        }
    }
}
