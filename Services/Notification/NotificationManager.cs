using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.Notification
{
    /// <summary>
    /// 알림 서비스 팩토리 및 매니저
    /// OCP: 새 알림 유형 추가 시 RegisterFactory만 호출하면 됨
    /// </summary>
    public class NotificationManager
    {
        private readonly NotificationSettings _settings;
        private INotificationService _service;

        private static readonly Dictionary<NotificationType, Func<NotificationSettings, INotificationService>> _factories
            = new Dictionary<NotificationType, Func<NotificationSettings, INotificationService>>
            {
                { NotificationType.Slack, s => new SlackNotificationService(s.SlackWebhookUrl) },
                { NotificationType.Teams, s => new TeamsNotificationService(s.TeamsWebhookUrl) },
                { NotificationType.Email, s => new EmailNotificationService(s.SmtpServer, s.SmtpPort, s.SmtpUsername, s.SmtpPassword, s.EmailTo) }
            };

        /// <summary>
        /// 새 알림 유형 팩토리 등록 (OCP 확장점)
        /// </summary>
        public static void RegisterFactory(NotificationType type, Func<NotificationSettings, INotificationService> factory)
        {
            _factories[type] = factory;
        }

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

            if (_factories.TryGetValue(_settings.Type, out var factory))
            {
                _service = factory(_settings);
            }
            else
            {
                _service = null;
            }
        }

        public void UpdateSettings(NotificationSettings settings)
        {
            if (settings == null) return;

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
