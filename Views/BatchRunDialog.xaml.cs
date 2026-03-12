using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services.TestExecution;

namespace AutoRegressionVM.Views
{
    public partial class BatchRunDialog : Window
    {
        private readonly IBatchRunnerService _batchRunner;
        private readonly DispatcherTimer _elapsedTimer;
        private DateTime _startTime;

        public ObservableCollection<ScenarioCheckItem> ScenarioItems { get; } = new ObservableCollection<ScenarioCheckItem>();
        public ObservableCollection<VMCheckItem> VMItems { get; } = new ObservableCollection<VMCheckItem>();
        public ObservableCollection<BatchStatusItem> StatusItems { get; } = new ObservableCollection<BatchStatusItem>();

        /// <summary>
        /// 일괄 실행 결과 (실행 완료 후 참조)
        /// </summary>
        public BatchRunResult Result { get; private set; }

        public BatchRunDialog(IBatchRunnerService batchRunner,
                              IEnumerable<TestScenario> scenarios,
                              IEnumerable<VMInfo> vms)
        {
            InitializeComponent();

            _batchRunner = batchRunner;

            foreach (var scenario in scenarios)
            {
                ScenarioItems.Add(new ScenarioCheckItem { Scenario = scenario, Name = scenario.Name, IsSelected = true });
            }

            foreach (var vm in vms)
            {
                VMItems.Add(new VMCheckItem { VM = vm, Name = vm.Name, IsSelected = true });
            }

            lstScenarios.ItemsSource = ScenarioItems;
            lstVMPool.ItemsSource = VMItems;
            lstBatchStatus.ItemsSource = StatusItems;

            runVMCount.Text = vms.Count().ToString();
            txtMaxVMs.Text = Math.Min(4, vms.Count()).ToString();
            UpdateSelectedCount();

            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _elapsedTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _startTime;
                txtElapsed.Text = elapsed.ToString(@"hh\:mm\:ss");
            };
        }

        private void SelectAllScenarios_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ScenarioItems) item.IsSelected = true;
            lstScenarios.Items.Refresh();
            UpdateSelectedCount();
        }

        private void DeselectAllScenarios_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ScenarioItems) item.IsSelected = false;
            lstScenarios.Items.Refresh();
            UpdateSelectedCount();
        }

        private void SelectAllVMs_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in VMItems) item.IsSelected = true;
            lstVMPool.Items.Refresh();
        }

        private void DeselectAllVMs_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in VMItems) item.IsSelected = false;
            lstVMPool.Items.Refresh();
        }

        private void Scenario_CheckChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void VM_CheckChanged(object sender, RoutedEventArgs e) { }

        private void UpdateSelectedCount()
        {
            var count = ScenarioItems.Count(s => s.IsSelected);
            txtSelectedCount.Text = $"선택됨: {count}개";
        }

        private async void Run_Click(object sender, RoutedEventArgs e)
        {
            var selectedScenarios = ScenarioItems.Where(s => s.IsSelected).Select(s => s.Scenario).ToList();
            var selectedVMs = VMItems.Where(v => v.IsSelected).Select(v => v.VM).ToList();

            if (selectedScenarios.Count == 0)
            {
                MessageBox.Show("실행할 시나리오를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedVMs.Count == 0)
            {
                MessageBox.Show("VM 풀에 사용할 VM을 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtMaxVMs.Text, out int maxVMs) || maxVMs < 1)
            {
                MessageBox.Show("동시 실행 VM 수를 1 이상으로 입력하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // UI 상태 전환
            btnRun.IsEnabled = false;
            btnCancel.IsEnabled = true;
            lstScenarios.IsEnabled = false;
            lstVMPool.IsEnabled = false;
            txtMaxVMs.IsEnabled = false;

            StatusItems.Clear();
            foreach (var scenario in selectedScenarios)
            {
                StatusItems.Add(new BatchStatusItem
                {
                    ScenarioName = scenario.Name,
                    StatusText = "대기",
                    StatusIcon = "⏳"
                });
            }

            progressOverall.Value = 0;
            txtProgress.Text = $"0 / {selectedScenarios.Count}";

            _startTime = DateTime.Now;
            _elapsedTimer.Start();

            _batchRunner.BatchProgressChanged += OnBatchProgress;
            _batchRunner.LogGenerated += OnLogGenerated;

            try
            {
                Result = await _batchRunner.RunBatchAsync(selectedScenarios, selectedVMs, maxVMs);

                // 최종 상태 업데이트
                progressOverall.Value = 100;
                txtProgress.Text = $"{Result.CompletedScenarios} / {Result.TotalScenarios} 완료";

                var msg = $"일괄 실행 완료\n\n" +
                          $"총 시나리오: {Result.TotalScenarios}개\n" +
                          $"성공: {Result.SucceededScenarios}개\n" +
                          $"실패: {Result.FailedScenarios}개\n" +
                          $"소요 시간: {Result.Duration:hh\\:mm\\:ss}";

                MessageBox.Show(msg, "일괄 실행 완료",
                    MessageBoxButton.OK,
                    Result.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"일괄 실행 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _elapsedTimer.Stop();
                _batchRunner.BatchProgressChanged -= OnBatchProgress;
                _batchRunner.LogGenerated -= OnLogGenerated;

                btnRun.IsEnabled = true;
                btnCancel.IsEnabled = false;
                lstScenarios.IsEnabled = true;
                lstVMPool.IsEnabled = true;
                txtMaxVMs.IsEnabled = true;
            }
        }

        private void OnBatchProgress(object sender, BatchProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                progressOverall.Value = e.OverallProgressPercent;
                txtProgress.Text = $"{e.CompletedScenarios} / {e.TotalScenarios}";

                // 해당 시나리오 상태 업데이트
                var item = StatusItems.FirstOrDefault(s => s.ScenarioName == e.CurrentScenarioName);
                if (item != null)
                {
                    item.VMName = e.AssignedVMName ?? "";
                    switch (e.Status)
                    {
                        case BatchScenarioStatus.Running:
                            item.StatusText = "실행중";
                            item.StatusIcon = "🔄";
                            break;
                        case BatchScenarioStatus.Succeeded:
                            item.StatusText = "성공";
                            item.StatusIcon = "✅";
                            break;
                        case BatchScenarioStatus.Failed:
                            item.StatusText = "실패";
                            item.StatusIcon = "❌";
                            break;
                        case BatchScenarioStatus.Cancelled:
                            item.StatusText = "취소";
                            item.StatusIcon = "⏹";
                            break;
                    }
                    // Force refresh
                    var idx = StatusItems.IndexOf(item);
                    if (idx >= 0)
                    {
                        StatusItems.RemoveAt(idx);
                        StatusItems.Insert(idx, item);
                    }
                }
            });
        }

        private void OnLogGenerated(object sender, TestLogEventArgs e)
        {
            // 로그는 MainViewModel에서 처리
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("일괄 실행을 취소하시겠습니까?\n실행 중인 시나리오는 완료 후 중지됩니다.",
                "취소 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _batchRunner.Cancel();
                btnCancel.IsEnabled = false;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_batchRunner.IsRunning)
            {
                var result = MessageBox.Show("일괄 실행이 진행 중입니다. 취소하고 닫으시겠습니까?",
                    "확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
                _batchRunner.Cancel();
            }

            DialogResult = Result != null;
            Close();
        }
    }

    /// <summary>
    /// 시나리오 체크 항목
    /// </summary>
    public class ScenarioCheckItem : INotifyPropertyChanged
    {
        public TestScenario Scenario { get; set; }
        public string Name { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// VM 체크 항목
    /// </summary>
    public class VMCheckItem : INotifyPropertyChanged
    {
        public VMInfo VM { get; set; }
        public string Name { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// 배치 상태 표시 항목
    /// </summary>
    public class BatchStatusItem
    {
        public string ScenarioName { get; set; }
        public string VMName { get; set; } = "";
        public string StatusText { get; set; }
        public string StatusIcon { get; set; }
    }
}
