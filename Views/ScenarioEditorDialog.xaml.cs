using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Views
{
    public partial class ScenarioEditorDialog : Window
    {
        public TestScenario Result { get; private set; }

        private readonly ObservableCollection<VMInfo> _availableVMs;
        private readonly ObservableCollection<TestStep> _steps = new ObservableCollection<TestStep>();
        private TestStep _currentStep;
        private bool _isEditing;
        private string _existingId;
        private DateTime _existingCreatedAt;

        public ScenarioEditorDialog(IEnumerable<VMInfo> availableVMs, TestScenario existingScenario = null)
        {
            InitializeComponent();

            _availableVMs = new ObservableCollection<VMInfo>(availableVMs);
            cboTargetVM.ItemsSource = _availableVMs;
            lstSteps.ItemsSource = _steps;

            if (existingScenario != null)
            {
                _isEditing = true;
                _existingId = existingScenario.Id;
                _existingCreatedAt = existingScenario.CreatedAt;
                LoadScenario(existingScenario);
            }
        }

        private void LoadScenario(TestScenario scenario)
        {
            txtName.Text = scenario.Name;
            txtDescription.Text = scenario.Description;
            txtMaxParallel.Text = scenario.MaxParallelVMs.ToString();
            chkContinueOnFailure.IsChecked = scenario.ContinueOnFailure;

            foreach (var step in scenario.Steps.OrderBy(s => s.Order))
            {
                _steps.Add(step);
            }
        }

        private void StepList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveCurrentStep();

            var step = lstSteps.SelectedItem as TestStep;
            if (step != null)
            {
                _currentStep = step;
                LoadStepToUI(step);
                gridStepDetail.IsEnabled = true;
            }
            else
            {
                _currentStep = null;
                gridStepDetail.IsEnabled = false;
            }
        }

        private void LoadStepToUI(TestStep step)
        {
            txtStepName.Text = step.Name;
            txtSnapshotName.Text = step.SnapshotName;

            var vm = _availableVMs.FirstOrDefault(v => v.VmxPath == step.TargetVmxPath);
            cboTargetVM.SelectedItem = vm;

            dgFilesToVM.ItemsSource = new ObservableCollection<FileCopyInfo>(step.FilesToCopyToVM ?? new List<FileCopyInfo>());
            dgResultFiles.ItemsSource = new ObservableCollection<FileCopyInfo>(step.ResultFilesToCollect ?? new List<FileCopyInfo>());

            if (step.Execution != null)
            {
                cboExecType.SelectedIndex = (int)step.Execution.Type;
                txtExecPath.Text = step.Execution.ExecutablePath;
                txtExecArgs.Text = step.Execution.Arguments;
                txtTimeout.Text = step.Execution.TimeoutSeconds.ToString();
            }

            chkForceNetworkDisconnect.IsChecked = step.ForceNetworkDisconnect;
            chkForceSnapshotRevert.IsChecked = step.ForceSnapshotRevertAfter;
            chkCaptureScreenshots.IsChecked = step.CaptureScreenshots;
            txtScreenshotInterval.Text = step.ScreenshotIntervalSeconds.ToString();
        }

        private void SaveCurrentStep()
        {
            if (_currentStep == null) return;

            _currentStep.Name = txtStepName.Text;
            _currentStep.SnapshotName = txtSnapshotName.Text;

            var selectedVM = cboTargetVM.SelectedItem as VMInfo;
            _currentStep.TargetVmxPath = selectedVM?.VmxPath;

            _currentStep.FilesToCopyToVM = (dgFilesToVM.ItemsSource as ObservableCollection<FileCopyInfo>)?
                .Where(f => !string.IsNullOrWhiteSpace(f.SourcePath)).ToList() ?? new List<FileCopyInfo>();

            _currentStep.ResultFilesToCollect = (dgResultFiles.ItemsSource as ObservableCollection<FileCopyInfo>)?
                .Where(f => !string.IsNullOrWhiteSpace(f.SourcePath)).ToList() ?? new List<FileCopyInfo>();

            _currentStep.Execution = new ExecutionInfo
            {
                Type = (ExecutionType)cboExecType.SelectedIndex,
                ExecutablePath = txtExecPath.Text,
                Arguments = txtExecArgs.Text,
                TimeoutSeconds = int.TryParse(txtTimeout.Text, out var timeout) ? timeout : 300
            };

            _currentStep.ForceNetworkDisconnect = chkForceNetworkDisconnect.IsChecked ?? true;
            _currentStep.ForceSnapshotRevertAfter = chkForceSnapshotRevert.IsChecked ?? true;
            _currentStep.CaptureScreenshots = chkCaptureScreenshots.IsChecked ?? false;
            _currentStep.ScreenshotIntervalSeconds = int.TryParse(txtScreenshotInterval.Text, out var interval) ? interval : 10;

            // Refresh list display
            var index = _steps.IndexOf(_currentStep);
            if (index >= 0)
            {
                lstSteps.Items.Refresh();
            }
        }

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            var newStep = new TestStep
            {
                Name = $"Step {_steps.Count + 1}",
                Order = _steps.Count
            };
            _steps.Add(newStep);
            lstSteps.SelectedItem = newStep;
        }

        private void RemoveStep_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == null) return;

            var result = MessageBox.Show("선택한 스텝을 삭제하시겠습니까?", "확인",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _steps.Remove(_currentStep);
                _currentStep = null;
                UpdateStepOrders();
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == null) return;

            var index = _steps.IndexOf(_currentStep);
            if (index > 0)
            {
                _steps.Move(index, index - 1);
                UpdateStepOrders();
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == null) return;

            var index = _steps.IndexOf(_currentStep);
            if (index < _steps.Count - 1)
            {
                _steps.Move(index, index + 1);
                UpdateStepOrders();
            }
        }

        private void UpdateStepOrders()
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].Order = i;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("시나리오 이름을 입력하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveCurrentStep();

            Result = new TestScenario
            {
                Name = txtName.Text.Trim(),
                Description = txtDescription.Text?.Trim(),
                MaxParallelVMs = int.TryParse(txtMaxParallel.Text, out var maxParallel) ? maxParallel : 1,
                ContinueOnFailure = chkContinueOnFailure.IsChecked ?? true,
                Steps = _steps.ToList()
            };

            if (_isEditing)
            {
                // Keep original ID and creation date when editing
                Result.Id = _existingId;
                Result.CreatedAt = _existingCreatedAt;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
