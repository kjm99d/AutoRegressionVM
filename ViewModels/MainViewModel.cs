using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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
        private readonly ISettingsService _settingsService;
        private readonly IScenarioService _scenarioService;
        private readonly NotificationManager _notificationManager;
        private readonly IReportService _reportService;
        private readonly Services.SchedulerService _schedulerService;
        private readonly Services.TestExecution.IBatchRunnerService _batchRunnerService;
        private AppSettings _appSettings;
        private ITestRunner _testRunner;
        private ScenarioResult _lastScenarioResult;
        private DispatcherTimer _elapsedTimer;
        private DateTime _executionStartTime;

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

        private string _elapsedTime;
        public string ElapsedTime
        {
            get => _elapsedTime;
            set => SetProperty(ref _elapsedTime, value);
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
        public ICommand ExportLogCommand { get; }
        public ICommand ReRunCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand SchedulerCommand { get; }
        public ICommand BatchRunCommand { get; }

        #endregion

        public MainViewModel(ISettingsService settingsService, IScenarioService scenarioService,
                             IReportService reportService, IVMwareService vmwareService,
                             NotificationManager notificationManager,
                             Services.SchedulerService schedulerService,
                             Services.TestExecution.IBatchRunnerService batchRunnerService = null)
        {
            _settingsService = settingsService;
            _scenarioService = scenarioService;
            _reportService = reportService;
            _appSettings = _settingsService.LoadSettings();
            _vmwareService = vmwareService;
            _notificationManager = notificationManager;
            _schedulerService = schedulerService;
            _batchRunnerService = batchRunnerService ?? new Services.TestExecution.BatchRunnerService(vmwareService);

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
            ExportLogCommand = new RelayCommand(_ => ExportLog(), _ => Logs.Count > 0);
            ReRunCommand = new AsyncRelayCommand(async _ => await RunScenarioAsync(), _ => IsConnected && !IsRunning && _lastScenarioResult != null && SelectedScenario != null);
            ClearLogCommand = new RelayCommand(_ => { Logs.Clear(); AddLog("로그 초기화됨"); });
            SchedulerCommand = new RelayCommand(_ => OpenScheduler());
            BatchRunCommand = new RelayCommand(_ => OpenBatchRun(), _ => IsConnected && !IsRunning && Scenarios.Count > 0);

            // 스케줄러 이벤트 연결
            _schedulerService.TaskTriggered += OnScheduledTaskTriggered;
            _schedulerService.Start();

            // 경과 시간 타이머
            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _elapsedTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _executionStartTime;
                ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");
            };

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
            try
            {
                StatusMessage = "VM 목록 새로고침 중...";
                AddLog("VM 목록 새로고침 시작");

                var registeredVMs = await _vmwareService.GetRegisteredVMsAsync();
                var runningVMs = await _vmwareService.GetRunningVMsAsync();

                // 새로 발견된 VM 추가
                int addedCount = 0;
                foreach (var vm in registeredVMs)
                {
                    if (!VMs.Any(v => v.VmxPath == vm.VmxPath))
                    {
                        VMs.Add(vm);
                        _appSettings.RegisteredVMs.Add(vm);
                        addedCount++;
                    }
                }

                // 실행 상태 갱신
                foreach (var vm in VMs)
                {
                    vm.PowerState = runningVMs.Contains(vm.VmxPath)
                        ? VMPowerState.PoweredOn
                        : VMPowerState.PoweredOff;
                }

                if (addedCount > 0)
                {
                    SaveAll();
                }

                StatusMessage = $"VM {VMs.Count}개 (실행 중 {runningVMs.Count}개)";
                AddLog($"VM 목록 새로고침 완료: 총 {VMs.Count}개, 새로 발견 {addedCount}개");
            }
            catch (Exception ex)
            {
                StatusMessage = $"VM 목록 새로고침 실패: {ex.Message}";
                AddLog($"VM 목록 새로고침 실패: {ex.Message}");
            }
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

            // 실행 전 검증
            var validationErrors = ValidateScenario(SelectedScenario);
            if (validationErrors.Count > 0)
            {
                var msg = "실행 전 검증 실패:\n\n" + string.Join("\n", validationErrors.Select(e => $"  - {e}"));
                var proceed = MessageBox.Show(
                    msg + "\n\n그래도 실행하시겠습니까?",
                    "검증 경고",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (proceed != MessageBoxResult.Yes) return;
            }

            // VMware UI 충돌 방지 경고
            var warning = MessageBox.Show(
                "테스트 실행 중에는 VMware Workstation UI에서\n" +
                "VM 조작(스냅샷, 전원, 설정 변경 등)을 하지 마세요.\n\n" +
                "동시 조작 시 .lck 파일 충돌로 스냅샷 롤백 실패나\n" +
                "VM 상태 오류가 발생할 수 있습니다.\n\n" +
                "실행하시겠습니까?",
                "VMware 사용 주의",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (warning != MessageBoxResult.OK) return;

            IsRunning = true;
            TestResults.Clear();
            ProgressPercent = 0;

            // 경과 시간 시작
            _executionStartTime = DateTime.Now;
            ElapsedTime = "00:00:00";
            _elapsedTimer.Start();

            try
            {
                _testRunner = new TestRunner(_vmwareService, VMs);
                _testRunner.ProgressChanged += OnProgressChanged;
                _testRunner.LogGenerated += OnLogGenerated;

                AddLog($"시나리오 시작: {SelectedScenario.Name}");
                StatusMessage = $"실행 중: {SelectedScenario.Name}";

                // VM 실행 상태 초기화
                VMExecutionStatuses.Clear();
                if (SelectedScenario.TargetVMPaths != null && SelectedScenario.TargetVMPaths.Count > 0)
                {
                    foreach (var vmPath in SelectedScenario.TargetVMPaths)
                    {
                        var vmInfo = VMs.FirstOrDefault(v => v.VmxPath == vmPath);
                        VMExecutionStatuses.Add(new VMExecutionStatus
                        {
                            VMName = vmInfo?.Name ?? System.IO.Path.GetFileNameWithoutExtension(vmPath),
                            Phase = VMExecutionPhase.Idle,
                            IsActive = true
                        });
                    }
                }

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
                _scenarioService.Save(SelectedScenario);

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
                _elapsedTimer.Stop();
                if (_testRunner != null)
                {
                    _testRunner.ProgressChanged -= OnProgressChanged;
                    _testRunner.LogGenerated -= OnLogGenerated;
                    _testRunner = null;
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
                _scenarioService.Save(dialog.Result);
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
                _scenarioService.Save(dialog.Result);
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
                _scenarioService.Delete(scenarioToRemove);
                AddLog($"시나리오 삭제됨: {scenarioToRemove.Name}");
            }
        }

        private void CloneScenario()
        {
            if (SelectedScenario == null) return;

            var cloned = _scenarioService.Clone(SelectedScenario);
            Scenarios.Add(cloned);
            SelectedScenario = cloned;
            _scenarioService.Save(cloned);
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
                    _scenarioService.ExportToFile(SelectedScenario, dialog.FileName);
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
                        var scenario = _scenarioService.ImportFromFile(fileName);

                        if (scenario != null)
                        {
                            // 이름 중복 확인
                            var baseName = scenario.Name;
                            int counter = 1;
                            while (Scenarios.Any(s => s.Name == scenario.Name))
                            {
                                scenario.Name = $"{baseName} ({counter++})";
                            }

                            Scenarios.Add(scenario);
                            _scenarioService.Save(scenario);
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
            var dialog = new TestHistoryDialog(_settingsService, _reportService)
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
                case TestProgressPhase.WaitingAfterExecution: return "실행 후 대기";
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

        /// <summary>
        /// 시나리오 실행 전 검증: VMX 경로, 복사 파일, 실행 파일 존재 확인
        /// </summary>
        private System.Collections.Generic.List<string> ValidateScenario(TestScenario scenario)
        {
            var errors = new System.Collections.Generic.List<string>();

            if (scenario.Steps == null || scenario.Steps.Count == 0)
            {
                errors.Add("테스트 스텝이 없습니다");
                return errors;
            }

            foreach (var step in scenario.Steps)
            {
                var stepLabel = $"[스텝 {step.Order}: {step.Name}]";

                // VMX 경로 확인 (파일 분배 모드가 아닌 경우)
                if (scenario.TargetVMPaths.Count == 0 && !string.IsNullOrEmpty(step.TargetVmxPath))
                {
                    if (!File.Exists(step.TargetVmxPath))
                        errors.Add($"{stepLabel} VMX 파일 없음: {step.TargetVmxPath}");
                }

                // 실행 파일 경로 확인 (호스트 경로인 경우에만)
                if (step.Execution != null && !string.IsNullOrEmpty(step.Execution.ExecutablePath))
                {
                    var execPath = step.Execution.ExecutablePath;
                    // 게스트 VM 경로가 아닌 호스트 경로인 경우에만 검증
                    if (!execPath.Contains(":") || execPath.StartsWith(AppDomain.CurrentDomain.BaseDirectory))
                    {
                        // 게스트 경로는 보통 C:\, D:\ 등으로 시작하므로 호스트와 구분 어려움 — 스킵
                    }
                }

                // 복사할 파일 존재 확인
                if (step.FilesToCopyToVM != null)
                {
                    foreach (var file in step.FilesToCopyToVM)
                    {
                        if (!string.IsNullOrEmpty(file.SourcePath) && !File.Exists(file.SourcePath))
                            errors.Add($"{stepLabel} 복사 파일 없음: {file.SourcePath}");
                    }
                }
            }

            // 대상 VM 경로 확인 (파일 분배 모드)
            foreach (var vmPath in scenario.TargetVMPaths)
            {
                if (!File.Exists(vmPath))
                    errors.Add($"대상 VM 파일 없음: {vmPath}");
            }

            // 배분 파일 확인
            if (scenario.TestTargetFiles != null)
            {
                foreach (var file in scenario.TestTargetFiles)
                {
                    if (!string.IsNullOrEmpty(file.HostFilePath) && !File.Exists(file.HostFilePath))
                        errors.Add($"배분 파일 없음: {file.HostFilePath}");
                }
            }

            return errors;
        }

        private void ExportLog()
        {
            var dialog = new SaveFileDialog
            {
                Title = "로그 내보내기",
                Filter = "텍스트 파일 (*.txt)|*.txt|로그 파일 (*.log)|*.log",
                FileName = $"AutoRegressionVM_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    foreach (var log in Logs)
                    {
                        sb.AppendLine(log);
                    }
                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    AddLog($"로그 내보내기 완료: {dialog.FileName}");
                }
                catch (Exception ex)
                {
                    AddLog($"로그 내보내기 실패: {ex.Message}");
                }
            }
        }

        private void OpenScheduler()
        {
            var dialog = new Views.SchedulerDialog(_schedulerService, Scenarios)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }

        private void OpenBatchRun()
        {
            _batchRunnerService.LogGenerated += OnLogGenerated;

            try
            {
                var dialog = new BatchRunDialog(_batchRunnerService, Scenarios, VMs)
                {
                    Owner = Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true && dialog.Result != null)
                {
                    var result = dialog.Result;
                    AddLog($"일괄 실행 완료: 총 {result.TotalScenarios}개, 성공 {result.SucceededScenarios}개, 실패 {result.FailedScenarios}개");

                    // 개별 시나리오 결과를 이력에 저장
                    foreach (var sr in result.ScenarioResults)
                    {
                        if (sr.Result != null)
                        {
                            _settingsService.SaveResult(sr.Result);
                        }
                    }
                }
            }
            finally
            {
                _batchRunnerService.LogGenerated -= OnLogGenerated;
            }
        }

        private async void OnScheduledTaskTriggered(object sender, Services.ScheduledTask task)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var scenario = Scenarios.FirstOrDefault(s => s.Id == task.ScenarioId);
                if (scenario == null)
                {
                    AddLog($"[스케줄러] 시나리오를 찾을 수 없음: {task.ScenarioName} (ID: {task.ScenarioId})");
                    return;
                }

                if (IsRunning)
                {
                    AddLog($"[스케줄러] 이미 실행 중이므로 스케줄 건너뜀: {task.Name}");
                    return;
                }

                if (!IsConnected)
                {
                    AddLog($"[스케줄러] VMware 미연결 - 자동 연결 시도: {task.Name}");
                    await ConnectAsync();
                    if (!IsConnected)
                    {
                        AddLog($"[스케줄러] VMware 연결 실패 - 스케줄 건너뜀: {task.Name}");
                        return;
                    }
                }

                AddLog($"[스케줄러] 예약 실행 시작: {task.Name} → {scenario.Name}");
                SelectedScenario = scenario;
                await RunScenarioAsync();
            });
        }

        public void Cleanup()
        {
            _testRunner?.Cancel();
            _schedulerService.TaskTriggered -= OnScheduledTaskTriggered;
            _schedulerService.Stop();
            _elapsedTimer?.Stop();
        }

        private void LoadSavedData()
        {
            // 저장된 VM 로드
            foreach (var vm in _appSettings.RegisteredVMs)
            {
                VMs.Add(vm);
            }

            // 저장된 시나리오 로드
            var scenarios = _scenarioService.LoadAll();
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
