using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services;
using AutoRegressionVM.Services.Notification;
using AutoRegressionVM.Services.VMware;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace AutoRegressionVM.Views
{
    public partial class SettingsDialog : Window
    {
        private readonly AppSettings _settings;
        private readonly SettingsService _settingsService;
        private readonly IVMwareService _vmwareService;
        private readonly ObservableCollection<VMInfo> _vmList;

        public bool SettingsChanged { get; private set; }

        public SettingsDialog(AppSettings settings, SettingsService settingsService, IVMwareService vmwareService)
        {
            InitializeComponent();

            _settings = settings ?? new AppSettings();
            _settingsService = settingsService;
            _vmwareService = vmwareService;
            _vmList = new ObservableCollection<VMInfo>();

            dgVMs.ItemsSource = _vmList;

            LoadSettings();
        }

        private void LoadSettings()
        {
            // 일반 설정
            txtVMwareInstallPath.Text = _settings.VMwareInstallPath;
            txtScenariosPath.Text = _settings.ScenariosPath;
            txtResultOutputPath.Text = _settings.ResultOutputPath;

            // 알림 설정
            var notification = _settings.Notification ?? new NotificationSettings();
            chkNotificationEnabled.IsChecked = notification.Enabled;

            // 알림 유형 선택
            switch (notification.Type)
            {
                case NotificationType.Slack:
                    cboNotificationType.SelectedIndex = 0;
                    break;
                case NotificationType.Teams:
                    cboNotificationType.SelectedIndex = 1;
                    break;
                case NotificationType.Email:
                    cboNotificationType.SelectedIndex = 2;
                    break;
                default:
                    cboNotificationType.SelectedIndex = 0;
                    break;
            }

            txtSlackWebhookUrl.Text = notification.SlackWebhookUrl;
            txtTeamsWebhookUrl.Text = notification.TeamsWebhookUrl;
            txtSmtpServer.Text = notification.SmtpServer;
            txtSmtpPort.Text = notification.SmtpPort.ToString();
            txtSmtpUsername.Text = notification.SmtpUsername;
            txtSmtpPassword.Password = notification.SmtpPassword;
            txtEmailTo.Text = notification.EmailTo;

            chkNotifyOnStart.IsChecked = notification.NotifyOnStart;
            chkNotifyOnComplete.IsChecked = notification.NotifyOnComplete;
            chkNotifyOnFailure.IsChecked = notification.NotifyOnFailure;
            chkNotifyOnError.IsChecked = notification.NotifyOnError;

            // VM 목록
            _vmList.Clear();
            foreach (var vm in _settings.RegisteredVMs)
            {
                _vmList.Add(vm);
            }

            UpdateNotificationPanelVisibility();
        }

        private void SaveSettings()
        {
            // 일반 설정
            _settings.VMwareInstallPath = txtVMwareInstallPath.Text.Trim();
            _settings.ScenariosPath = txtScenariosPath.Text.Trim();
            _settings.ResultOutputPath = txtResultOutputPath.Text.Trim();

            // 알림 설정
            if (_settings.Notification == null)
                _settings.Notification = new NotificationSettings();

            _settings.Notification.Enabled = chkNotificationEnabled.IsChecked ?? false;

            var selectedType = (cboNotificationType.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            switch (selectedType)
            {
                case "Slack":
                    _settings.Notification.Type = NotificationType.Slack;
                    break;
                case "Teams":
                    _settings.Notification.Type = NotificationType.Teams;
                    break;
                case "Email":
                    _settings.Notification.Type = NotificationType.Email;
                    break;
                default:
                    _settings.Notification.Type = NotificationType.None;
                    break;
            }

            _settings.Notification.SlackWebhookUrl = txtSlackWebhookUrl.Text.Trim();
            _settings.Notification.TeamsWebhookUrl = txtTeamsWebhookUrl.Text.Trim();
            _settings.Notification.SmtpServer = txtSmtpServer.Text.Trim();
            _settings.Notification.SmtpPort = int.TryParse(txtSmtpPort.Text, out var port) ? port : 587;
            _settings.Notification.SmtpUsername = txtSmtpUsername.Text.Trim();
            _settings.Notification.SmtpPassword = txtSmtpPassword.Password;
            _settings.Notification.EmailTo = txtEmailTo.Text.Trim();

            _settings.Notification.NotifyOnStart = chkNotifyOnStart.IsChecked ?? false;
            _settings.Notification.NotifyOnComplete = chkNotifyOnComplete.IsChecked ?? true;
            _settings.Notification.NotifyOnFailure = chkNotifyOnFailure.IsChecked ?? true;
            _settings.Notification.NotifyOnError = chkNotifyOnError.IsChecked ?? true;

            // VM 목록
            _settings.RegisteredVMs.Clear();
            foreach (var vm in _vmList)
            {
                _settings.RegisteredVMs.Add(vm);
            }

            // 파일에 저장
            _settingsService.SaveSettings(_settings);
        }

        #region 이벤트 핸들러

        private void NotificationEnabled_Changed(object sender, RoutedEventArgs e)
        {
            var enabled = chkNotificationEnabled.IsChecked ?? false;
            grpNotificationSettings.IsEnabled = enabled;
            grpNotificationConditions.IsEnabled = enabled;
        }

        private void NotificationType_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateNotificationPanelVisibility();
        }

        private void UpdateNotificationPanelVisibility()
        {
            if (pnlSlackSettings == null) return;

            pnlSlackSettings.Visibility = Visibility.Collapsed;
            pnlTeamsSettings.Visibility = Visibility.Collapsed;
            pnlEmailSettings.Visibility = Visibility.Collapsed;

            var selectedType = (cboNotificationType.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            switch (selectedType)
            {
                case "Slack":
                    pnlSlackSettings.Visibility = Visibility.Visible;
                    break;
                case "Teams":
                    pnlTeamsSettings.Visibility = Visibility.Visible;
                    break;
                case "Email":
                    pnlEmailSettings.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void BrowseVMwarePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "VMware Workstation 설치 폴더를 선택하세요",
                ShowNewFolderButton = false
            };

            if (!string.IsNullOrEmpty(txtVMwareInstallPath.Text))
                dialog.SelectedPath = txtVMwareInstallPath.Text;

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtVMwareInstallPath.Text = dialog.SelectedPath;
            }
        }

        private void BrowseScenariosPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "시나리오 저장 폴더를 선택하세요"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtScenariosPath.Text = dialog.SelectedPath;
            }
        }

        private void BrowseResultPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "결과 저장 폴더를 선택하세요"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtResultOutputPath.Text = dialog.SelectedPath;
            }
        }

        private async void TestNotification_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 임시로 현재 UI 설정으로 테스트
                var testSettings = new NotificationSettings
                {
                    Enabled = true,
                    SlackWebhookUrl = txtSlackWebhookUrl.Text.Trim(),
                    TeamsWebhookUrl = txtTeamsWebhookUrl.Text.Trim(),
                    SmtpServer = txtSmtpServer.Text.Trim(),
                    SmtpPort = int.TryParse(txtSmtpPort.Text, out var port) ? port : 587,
                    SmtpUsername = txtSmtpUsername.Text.Trim(),
                    SmtpPassword = txtSmtpPassword.Password,
                    EmailTo = txtEmailTo.Text.Trim()
                };

                var selectedType = (cboNotificationType.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                switch (selectedType)
                {
                    case "Slack":
                        testSettings.Type = NotificationType.Slack;
                        break;
                    case "Teams":
                        testSettings.Type = NotificationType.Teams;
                        break;
                    case "Email":
                        testSettings.Type = NotificationType.Email;
                        break;
                }

                var manager = new NotificationManager(testSettings);
                var success = await manager.TestConnectionAsync();

                if (success)
                {
                    MessageBox.Show("연결 테스트 성공!", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("연결 테스트 실패. 설정을 확인하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"연결 테스트 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ImportFromVMware_Click(object sender, RoutedEventArgs e)
        {
            if (!_vmwareService.IsConnected)
            {
                MessageBox.Show("먼저 VMware에 연결하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var vms = await _vmwareService.GetRegisteredVMsAsync();

                if (vms.Count == 0)
                {
                    MessageBox.Show("VMware에 등록된 VM이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int added = 0;
                foreach (var vm in vms)
                {
                    // 이미 등록된 VM인지 확인
                    bool exists = false;
                    foreach (var existing in _vmList)
                    {
                        if (existing.VmxPath.Equals(vm.VmxPath, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        _vmList.Add(vm);
                        added++;
                    }
                }

                MessageBox.Show($"총 {vms.Count}개 VM 중 {added}개가 추가되었습니다.",
                    "가져오기 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"VM 가져오기 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddVM_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddVMDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                _vmList.Add(dialog.Result);
            }
        }

        private void RemoveVM_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgVMs.SelectedItem as VMInfo;
            if (selected == null)
            {
                MessageBox.Show("제거할 VM을 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"'{selected.Name}'을(를) 목록에서 제거하시겠습니까?",
                "확인", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _vmList.Remove(selected);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettings();
                SettingsChanged = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion
    }
}
