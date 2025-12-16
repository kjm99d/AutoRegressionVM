using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AutoRegressionVM.Helpers;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services;
using AutoRegressionVM.Services.Notification;
using AutoRegressionVM.Services.TestExecution;
using AutoRegressionVM.Services.VMware;
using AutoRegressionVM.Views;
using Microsoft.Win32;

namespace AutoRegressionVM.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IVMwareService _vmwareService;
        private readonly SettingsService _settingsService;
        private readonly NotificationManager _notificationManager;
        private readonly ReportService _reportService;
        private AppSettings _appSettings;
        private ITestRunner _testRunner;
        private ScenarioResult _lastScenarioResult;

        #region Properties

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private double _progressPercent;
        public double ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        private string _currentPhase;
        public string CurrentPhase
        {
            get => _currentPhase;
            set => SetProperty(ref _currentPhase, value);
        }

        // VM 목록
        public ObservableCollection<VMInfo> VMs { get; } = new ObservableCollection<VMInfo>();

        private VMInfo _selectedVM;
        public VMInfo SelectedVM
        {
            get => _selectedVM;
            set => SetProperty(ref _selectedVM, value);
        }

        // 시나리오 목록
        public ObservableCollection<TestScenario> Scenarios { get; } = new ObservableCollection<TestScenario>();

        private TestScenario _selectedScenario;
        public TestScenario SelectedScenario
        {
            get => _selectedScenario;
            set => SetProperty(ref _selectedScenario, value);
        }

        // 테스트 결과
        public ObservableCollection<TestResult> TestResults { get; } = new ObservableCollection<TestResult>();

        // 로그
        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        // VM 실행 상태 (병렬 실행 시각화)
        public ObservableCollection<VMExecutionStatus> VMExecutionStatuses { get; } = new ObservableCollection<VMExecutionStatus>();

        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand RefreshVMsCommand { get; }
        public ICommand AddVMCommand { get; }
        public ICommand RemoveVMCommand { get; }
        public ICommand RunScenarioCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand NewScenarioCommand { get; }
        public ICommand EditScenarioCommand { get; }
        public ICommand DeleteScenarioCommand { get; }
        public ICommand CloneScenarioCommand { get; }
        public ICommand ExportScenarioCommand { get; }
        public ICommand ImportScenarioCommand { get; }
        public ICommand ExportReportCommand { get; }
        public ICommand ViewHistoryCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SettingsCommand { get; }

        #endregion

        public MainViewModel()
        {
            _settingsService = new SettingsService();
            _reportService = new ReportService();
            _appSettings = _settingsService.LoadSettings();
            _vmwareService = new VixService(_appSettings.VMwareInstallPath);
            _notificationManager = new NotificationManager(_appSettings.Notification);

            // Commands 초기화
            ConnectCommand = new AsyncRelayCommand(async _ => await ConnectAsync(), _ => !IsConnected);
            DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected);
            RefreshVMsCommand = new AsyncRelayCommand(async _ => await RefreshVMsAsync(), _ => IsConnected);
            AddVMCommand = new RelayCommand(_ => AddVM());
            RemoveVMCommand = new RelayCommand(_ => RemoveVM(), _ => SelectedVM != null);
            RunScenarioCommand = new AsyncRelayCommand(async _ => await RunScenarioAsync(), _ => IsConnected && !IsRunning && SelectedScenario != null);
            StopCommand = new RelayCommand(_ => StopExecution(), _ => IsRunning);
            NewScenarioCommand = new RelayCommand(_ => CreateNewScenario());
            EditScenarioCommand = new RelayCommand(_ => EditScenario(), _ => SelectedScenario != null);
            DeleteScenarioCommand = new RelayCommand(_ => DeleteScenario(), _ => SelectedScenario != null);
            CloneScenarioCommand = new RelayCommand(_ => CloneScenario(), _ => SelectedScenario != null);
            ExportScenarioCommand = new RelayCommand(_ => ExportScenario(), _ => SelectedScenario != null);
            ImportScenarioCommand = new RelayCommand(_ => ImportScenario());
            ExportReportCommand = new RelayCommand(_ => ExportReport(), _ => _lastScenarioResult != null);
            ViewHistoryCommand = new RelayCommand(_ => ViewHistory());
            SaveCommand = new RelayCommand(_ => SaveAll());
            SettingsCommand = new RelayCommand(_ => OpenSettings());

            StatusMessage = "준비됨 - VMware에 연결하세요";

            // 저장된 데이터 로드
            LoadSavedData();
        }

        private async Task ConnectAsync()
        {
            StatusMessage = "VMware 연결 중...";

            try
            {
                var connected = await _vmwareService.ConnectAsync();
                IsConnected = connected;

                if (connected)
                {
                    StatusMessage = "VMware 연결됨";
                    AddLog("VMware 연결 성공");
                }
                else
                {
                    StatusMessage = "VMware 연결 실패";
                    AddLog("VMware 연결 실패 - VMware Workstation이 설치되어 있는지 확인하세요");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"연결 실패: {ex.Message}";
                AddLog($"오류: {ex.Message}");
            }
        }

        private void Disconnect()
        {
            _vmwareService.Disconnect();
            IsConnected = false;
            StatusMessage = "연결 해제됨";
            AddLog("VMware 연결 해제");
        }

        private async Task RefreshVMsAsync()
        {
            // VM 목록 새로고침
            AddLog("VM 목록 새로고침");
            await Task.CompletedTask;
        }

        private void AddVM()
        {
            var dialog = new AddVMDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                VMs.Add(dialog.Result);
                _appSettings.RegisteredVMs.Add(dialog.Result);
                SaveAll();
                AddLog($"VM 추가됨: {dialog.Result.Name}");
            }
        }

        private void RemoveVM()
        {
            if (SelectedVM == null) return;

            var result = MessageBox.Show(
                $"'{SelectedVM.Name}'을(를) 삭제하시겠습니까?",
                "VM 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var vmToRemove = SelectedVM;
                VMs.Remove(vmToRemove);
                _appSettings.RegisteredVMs.RemoveAll(v => v.VmxPath == vmToRemove.VmxPath);
                SaveAll();
                AddLog($"VM 삭제됨: {vmToRemove.Name}");
            }
        }

        private async Task RunScenarioAsync()
        {
            if (SelectedScenario == null) return;

            IsRunning = true;
            TestResults.Clear();
            ProgressPercent = 0;

            try
            {
                _testRunner = new TestRunner(_vmwareService, VMs);
                _testRunner.ProgressChanged += OnProgressChanged;
                _testRunner.LogGenerated += OnLogGenerated;

                AddLog($"시나리오 시작: {SelectedScenario.Name}");
                StatusMessage = $"실행 중: {SelectedScenario.Name}";

                // 시작 알림
                await _notificationManager.NotifyTestStartedAsync(SelectedScenario);

                var result = await _testRunner.RunScenarioAsync(SelectedScenario);

                // 결과 표시
                foreach (var testResult in result.TestResults)
                {
                    TestResults.Add(testResult);
                }

                // 결과 저장
                _settingsService.SaveResult(result);
                _lastScenarioResult = result;
                SelectedScenario.LastRunAt = DateTime.Now;
                _settingsService.SaveScenario(SelectedScenario);

                // 리포트 자동 생성
                var htmlPath = _reportService.GenerateHtmlReport(result);
                var jsonPath = _reportService.GenerateJsonReport(result);
                AddLog($"리포트 생성됨: {Path.GetFileName(htmlPath)}");

                StatusMessage = $"완료: 성공 {result.PassedCount}, 실패 {result.FailedCount}";
                ProgressPercent = 100;

                // 완료 알림
                await _notificationManager.NotifyTestCompletedAsync(result);
            }
            catch (Exception ex)
            {
                StatusMessage = $"실행 실패: {ex.Message}";
                AddLog($"오류: {ex.Message}");
                await _notificationManager.NotifyErrorAsync(ex.Message);
            }
            finally
            {
                if (_testRunner != null)
                {
                    _testRunner.ProgressChanged -= OnProgressChanged;
                    _testRunner.LogGenerated -= OnLogGenerated;
                }
                IsRunning = false;
            }
        }

        private void StopExecution()
        {
            _testRunner?.Cancel();
            StatusMessage = "중지 요청됨...";
            AddLog("테스트 중지 요청");
        }

        private void CreateNewScenario()
        {
            var dialog = new ScenarioEditorDialog(VMs, null, _vmwareService)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                Scenarios.Add(dialog.Result);
                SelectedScenario = dialog.Result;
                _settingsService.SaveScenario(dialog.Result);
                AddLog($"새 시나리오 생성: {dialog.Result.Name}");
            }
        }

        private void EditScenario()
        {
            if (SelectedScenario == null) return;

            var dialog = new ScenarioEditorDialog(VMs, SelectedScenario, _vmwareService)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                var index = Scenarios.IndexOf(SelectedScenario);
                if (index >= 0)
                {
                    Scenarios[index] = dialog.Result;
                    SelectedScenario = dialog.Result;
                }
                _settingsService.SaveScenario(dialog.Result);
                AddLog($"시나리오 수정됨: {dialog.Result.Name}");
            }
        }

        private void DeleteScenario()
        {
            if (SelectedScenario == null) return;

            var result = MessageBox.Show(
                $"'{SelectedScenario.Name}'을(를) 삭제하시겠습니까?",
                "시나리오 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var scenarioToRemove = SelectedScenario;
                Scenarios.Remove(scenarioToRemove);
                _settingsService.DeleteScenario(scenarioToRemove);
                AddLog($"시나리오 삭제됨: {scenarioToRemove.Name}");
            }
        }

        private void CloneScenario()
        {
            if (SelectedScenario == null) return;

            var cloned = new TestScenario
            {
                Id = Guid.NewGuid().ToString(),
                Name = SelectedScenario.Name + " (복사본)",
                Description = SelectedScenario.Description,
                MaxParallelVMs = SelectedScenario.MaxParallelVMs,
                ContinueOnFailure = SelectedScenario.ContinueOnFailure,
                CreatedAt = DateTime.Now,
                Steps = SelectedScenario.Steps.Select(s => new TestStep
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = s.Name,
                    Description = s.Description,
                    Order = s.Order,
                    TargetVmxPath = s.TargetVmxPath,
                    SnapshotName = s.SnapshotName,
                    FilesToCopyToVM = s.FilesToCopyToVM?.Select(f => new FileCopyInfo
                    {
                        SourcePath = f.SourcePath,
                        DestinationPath = f.DestinationPath
                    }).ToList() ?? new List<FileCopyInfo>(),
                    ResultFilesToCollect = s.ResultFilesToCollect?.Select(f => new FileCopyInfo
                    {
                        SourcePath = f.SourcePath,
                        DestinationPath = f.DestinationPath
                    }).ToList() ?? new List<FileCopyInfo>(),
                    Execution = new ExecutionInfo
                    {
                        Type = s.Execution?.Type ?? ExecutionType.Program,
                        ExecutablePath = s.Execution?.ExecutablePath,
                        Arguments = s.Execution?.Arguments,
                        WorkingDirectory = s.Execution?.WorkingDirectory,
                        TimeoutSeconds = s.Execution?.TimeoutSeconds ?? 300,
                        WaitForExit = s.Execution?.WaitForExit ?? true
                    },
                    SuccessCriteria = new SuccessCriteria
                    {
                        ExpectedExitCode = s.SuccessCriteria?.ExpectedExitCode,
                        ResultJsonPath = s.SuccessCriteria?.ResultJsonPath,
                        ExpectedJsonValue = s.SuccessCriteria?.ExpectedJsonValue,
                        ContainsText = s.SuccessCriteria?.ContainsText,
                        NotContainsText = s.SuccessCriteria?.NotContainsText
                    },
                    ForceNetworkDisconnect = s.ForceNetworkDisconnect,
                    CaptureScreenshots = s.CaptureScreenshots,
                    ScreenshotIntervalSeconds = s.ScreenshotIntervalSeconds,
                    ForceSnapshotRevertAfter = s.ForceSnapshotRevertAfter
                }).ToList()
            };

            Scenarios.Add(cloned);
            SelectedScenario = cloned;
            _settingsService.SaveScenario(cloned);
            AddLog($"시나리오 복제됨: {cloned.Name}");
        }

        private void ExportScenario()
        {
            if (SelectedScenario == null) return;

            var dialog = new SaveFileDialog
            {
                Title = "시나리오 내보내기",
                Filter = "JSON 파일 (*.json)|*.json",
                FileName = SelectedScenario.Name + ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = SimpleJson.Serialize(SelectedScenario);
                    File.WriteAllText(dialog.FileName, json);
                    AddLog($"시나리오 내보내기 완료: {dialog.FileName}");
                    MessageBox.Show("시나리오를 내보냈습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AddLog($"시나리오 내보내기 실패: {ex.Message}");
                    MessageBox.Show($"내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportScenario()
        {
            var dialog = new OpenFileDialog
            {
                Title = "시나리오 가져오기",
                Filter = "JSON 파일 (*.json)|*.json",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var fileName in dialog.FileNames)
                {
                    try
                    {
                        var json = File.ReadAllText(fileName);
                        var scenario = SimpleJson.Deserialize<TestScenario>(json);

                        if (scenario != null)
                        {
                            // 새 ID 부여
                            scenario.Id = Guid.NewGuid().ToString();
                            scenario.CreatedAt = DateTime.Now;

                            // 이름 중복 확인
                            var baseName = scenario.Name;
                            int counter = 1;
                            while (Scenarios.Any(s => s.Name == scenario.Name))
                            {
                                scenario.Name = $"{baseName} ({counter++})";
                            }

                            Scenarios.Add(scenario);
                            _settingsService.SaveScenario(scenario);
                            AddLog($"시나리오 가져오기 완료: {scenario.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"시나리오 가져오기 실패 ({Path.GetFileName(fileName)}): {ex.Message}");
                    }
                }

                MessageBox.Show($"{dialog.FileNames.Length}개 시나리오를 가져왔습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportReport()
        {
            if (_lastScenarioResult == null)
            {
                MessageBox.Show("내보낼 테스트 결과가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "리포트 내보내기",
                Filter = "HTML 파일 (*.html)|*.html|JSON 파일 (*.json)|*.json",
                FileName = $"{_lastScenarioResult.ScenarioName}_{_lastScenarioResult.StartTime:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FilterIndex == 1)
                    {
                        _reportService.GenerateHtmlReport(_lastScenarioResult, dialog.FileName);
                    }
                    else
                    {
                        _reportService.GenerateJsonReport(_lastScenarioResult, dialog.FileName);
                    }

                    AddLog($"리포트 내보내기 완료: {dialog.FileName}");

                    var result = MessageBox.Show("리포트를 열어보시겠습니까?", "완료", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"리포트 내보내기 실패: {ex.Message}");
                    MessageBox.Show($"내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ViewHistory()
        {
            var dialog = new TestHistoryDialog(_settingsService)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }

        private void SaveAll()
        {
            _settingsService.SaveSettings(_appSettings);
            AddLog("설정 저장됨");
        }

        private void OpenSettings()
        {
            var dialog = new SettingsDialog(_appSettings, _settingsService, _vmwareService)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialog.SettingsChanged)
            {
                // 설정이 변경되었으면 다시 로드
                _appSettings = _settingsService.LoadSettings();
                _notificationManager.UpdateSettings(_appSettings.Notification);

                // VM 목록 갱신
                VMs.Clear();
                foreach (var vm in _appSettings.RegisteredVMs)
                {
                    VMs.Add(vm);
                }

                AddLog("설정이 변경되었습니다");
            }
        }

        private void OnProgressChanged(object sender, TestProgressEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ProgressPercent = e.ProgressPercent;
                CurrentPhase = GetPhaseText(e.Phase);
                StatusMessage = $"[{e.VMName}] {e.CurrentStepName} - {CurrentPhase}";
            });
        }

        private void OnLogGenerated(object sender, TestLogEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                AddLog($"[{e.Timestamp:HH:mm:ss}] {e.Message}");
            });
        }

        private string GetPhaseText(TestProgressPhase phase)
        {
            switch (phase)
            {
                case TestProgressPhase.Initializing: return "초기화";
                case TestProgressPhase.RevertingSnapshot: return "스냅샷 복원";
                case TestProgressPhase.WaitingForBoot: return "부팅 대기";
                case TestProgressPhase.CopyingFiles: return "파일 복사";
                case TestProgressPhase.ExecutingTest: return "테스트 실행";
                case TestProgressPhase.CollectingResults: return "결과 수집";
                case TestProgressPhase.Completed: return "완료";
                case TestProgressPhase.Failed: return "실패";
                default: return phase.ToString();
            }
        }

        private void AddLog(string message)
        {
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

            // 로그 최대 개수 제한
            while (Logs.Count > 1000)
            {
                Logs.RemoveAt(0);
            }
        }

        private void LoadSavedData()
        {
            // 저장된 VM 로드
            foreach (var vm in _appSettings.RegisteredVMs)
            {
                VMs.Add(vm);
            }

            // 저장된 시나리오 로드
            var scenarios = _settingsService.LoadAllScenarios();
            foreach (var scenario in scenarios)
            {
                Scenarios.Add(scenario);
            }

            if (Scenarios.Count > 0)
            {
                SelectedScenario = Scenarios[0];
            }

            AddLog($"로드 완료: VM {VMs.Count}개, 시나리오 {Scenarios.Count}개");
        }
    }
}
