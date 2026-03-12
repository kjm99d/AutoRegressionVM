using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using AutoRegressionVM.CLI;
using AutoRegressionVM.Services;
using AutoRegressionVM.Services.Notification;
using AutoRegressionVM.Services.VMware;
using AutoRegressionVM.ViewModels;

namespace AutoRegressionVM
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        private const int ATTACH_PARENT_PROCESS = -1;

        private VixService _vmwareService;
        private SchedulerService _schedulerService;
        private MainViewModel _viewModel;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            var options = CommandLineParser.Parse(args);

            if (options.CliMode)
            {
                // CLI 모드
                if (!AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    AllocConsole();
                }

                try
                {
                    var runner = new CliRunner(options);
                    var exitCode = await runner.RunAsync();
                    Environment.Exit(exitCode);
                }
                finally
                {
                    FreeConsole();
                }
            }
            else
            {
                // GUI 모드 — Composition Root: 서비스 조립 후 주입
                var settingsService = new SettingsService();
                var appSettings = settingsService.LoadSettings();
                var reportService = new ReportService();
                _vmwareService = new VixService(appSettings.VMwareInstallPath);
                var notificationManager = new NotificationManager(appSettings.Notification);
                _schedulerService = new SchedulerService();

                var scenarioService = new ScenarioService(settingsService);
                _viewModel = new MainViewModel(settingsService, scenarioService, reportService,
                    _vmwareService, notificationManager, _schedulerService);

                var mainWindow = new MainWindow();
                mainWindow.DataContext = _viewModel;
                mainWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _viewModel?.Cleanup();
            _schedulerService?.Dispose();
            _vmwareService?.Dispose();
            base.OnExit(e);
        }
    }
}
