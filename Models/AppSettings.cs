using System.Collections.Generic;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// 애플리케이션 설정
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// VMware Workstation 설치 경로
        /// </summary>
        public string VMwareInstallPath { get; set; } = @"C:\Program Files (x86)\VMware\VMware Workstation";

        /// <summary>
        /// 기본 VM 저장 경로
        /// </summary>
        public string DefaultVMPath { get; set; }

        /// <summary>
        /// 테스트 결과 저장 경로
        /// </summary>
        public string ResultOutputPath { get; set; } = @".\Results";

        /// <summary>
        /// 시나리오 저장 경로
        /// </summary>
        public string ScenariosPath { get; set; } = @".\Scenarios";

        /// <summary>
        /// 알림 설정
        /// </summary>
        public NotificationSettings Notification { get; set; } = new NotificationSettings();

        /// <summary>
        /// 등록된 VM 목록
        /// </summary>
        public List<VMInfo> RegisteredVMs { get; set; } = new List<VMInfo>();
    }

    /// <summary>
    /// 알림 설정
    /// </summary>
    public class NotificationSettings
    {
        public bool Enabled { get; set; } = false;
        public NotificationType Type { get; set; } = NotificationType.None;

        // Slack 설정
        public string SlackWebhookUrl { get; set; }

        // Teams 설정
        public string TeamsWebhookUrl { get; set; }

        // Email 설정
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; }
        public string SmtpPassword { get; set; }
        public string EmailTo { get; set; }

        // 알림 조건
        public bool NotifyOnComplete { get; set; } = true;
        public bool NotifyOnFailure { get; set; } = true;
        public bool NotifyOnStart { get; set; } = false;
        public bool NotifyOnError { get; set; } = true;
    }

    public enum NotificationType
    {
        None,
        Slack,
        Teams,
        Email
    }
}
