using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services.VMware;
using Microsoft.Win32;

namespace AutoRegressionVM.Views
{
    public partial class ScenarioEditorDialog : Window
    {
        public TestScenario Result { get; private set; }

        private readonly ObservableCollection<VMInfo> _availableVMs;
        private readonly ObservableCollection<TestStep> _steps = new ObservableCollection<TestStep>();
        private readonly IVMwareService _vmwareService;
        private readonly ObservableCollection<Snapshot> _snapshots = new ObservableCollection<Snapshot>();
        private TestStep _currentStep;
        private bool _isEditing;
        private string _existingId;
        private DateTime _existingCreatedAt;
        private bool _isLoadingStep;
        private Point _dragStartPoint;
        private bool _isDragging;

        public ScenarioEditorDialog(IEnumerable<VMInfo> availableVMs, TestScenario existingScenario = null, IVMwareService vmwareService = null)
        {
            InitializeComponent();

            _vmwareService = vmwareService;
            _availableVMs = new ObservableCollection<VMInfo>(availableVMs);
            cboTargetVM.ItemsSource = _availableVMs;
            cboSnapshot.ItemsSource = _snapshots;
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

            // 이벤트 설정 로드
            LoadPreEventToUI(scenario.PreTestEvent);
            LoadPostEventToUI(scenario.PostTestEvent);
        }

        private void LoadPreEventToUI(ScenarioEvent evt)
        {
            if (evt == null)
            {
                chkPreEventEnabled.IsChecked = false;
                return;
            }

            chkPreEventEnabled.IsChecked = evt.IsEnabled;
            cboPreEventType.SelectedIndex = (int)evt.Type;
            txtPreEventCommand.Text = evt.Command;
            txtPreEventArgs.Text = evt.Arguments;
            txtPreEventWorkDir.Text = evt.WorkingDirectory;
            txtPreEventTimeout.Text = evt.TimeoutSeconds.ToString();
            chkPreEventStopOnFailure.IsChecked = evt.StopOnFailure;
            chkPreEventHideWindow.IsChecked = evt.HideWindow;
        }

        private void LoadPostEventToUI(ScenarioEvent evt)
        {
            if (evt == null)
            {
                chkPostEventEnabled.IsChecked = false;
                return;
            }

            chkPostEventEnabled.IsChecked = evt.IsEnabled;
            cboPostEventType.SelectedIndex = (int)evt.Type;
            txtPostEventCommand.Text = evt.Command;
            txtPostEventArgs.Text = evt.Arguments;
            txtPostEventWorkDir.Text = evt.WorkingDirectory;
            txtPostEventTimeout.Text = evt.TimeoutSeconds.ToString();
            cboPostEventCondition.SelectedIndex = (int)evt.RunCondition;
            chkPostEventHideWindow.IsChecked = evt.HideWindow;
        }

        private ScenarioEvent GetPreEventFromUI()
        {
            if (chkPreEventEnabled.IsChecked != true || string.IsNullOrWhiteSpace(txtPreEventCommand.Text))
                return null;

            return new ScenarioEvent
            {
                IsEnabled = chkPreEventEnabled.IsChecked ?? false,
                Type = (EventType)cboPreEventType.SelectedIndex,
                Command = txtPreEventCommand.Text,
                Arguments = txtPreEventArgs.Text,
                WorkingDirectory = txtPreEventWorkDir.Text,
                TimeoutSeconds = int.TryParse(txtPreEventTimeout.Text, out var timeout) ? timeout : 300,
                StopOnFailure = chkPreEventStopOnFailure.IsChecked ?? true,
                HideWindow = chkPreEventHideWindow.IsChecked ?? true
            };
        }

        private ScenarioEvent GetPostEventFromUI()
        {
            if (chkPostEventEnabled.IsChecked != true || string.IsNullOrWhiteSpace(txtPostEventCommand.Text))
                return null;

            return new ScenarioEvent
            {
                IsEnabled = chkPostEventEnabled.IsChecked ?? false,
                Type = (EventType)cboPostEventType.SelectedIndex,
                Command = txtPostEventCommand.Text,
                Arguments = txtPostEventArgs.Text,
                WorkingDirectory = txtPostEventWorkDir.Text,
                TimeoutSeconds = int.TryParse(txtPostEventTimeout.Text, out var timeout) ? timeout : 300,
                RunCondition = (PostEventCondition)cboPostEventCondition.SelectedIndex,
                HideWindow = chkPostEventHideWindow.IsChecked ?? true
            };
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

        private async void LoadStepToUI(TestStep step)
        {
            _isLoadingStep = true;
            try
            {
                txtStepName.Text = step.Name;

                var vm = _availableVMs.FirstOrDefault(v => v.VmxPath == step.TargetVmxPath);
                cboTargetVM.SelectedItem = vm;

                // VM이 선택되어 있으면 스냅샷 목록 로드
                if (vm != null)
                {
                    await LoadSnapshotsAsync(vm.VmxPath);
                }

                // 저장된 스냅샷 선택
                var snapshot = _snapshots.FirstOrDefault(s => s.Name == step.SnapshotName);
                if (snapshot != null)
                {
                    cboSnapshot.SelectedItem = snapshot;
                }
                else if (!string.IsNullOrEmpty(step.SnapshotName))
                {
                    // 목록에 없으면 임시로 추가
                    var tempSnapshot = new Snapshot { Name = step.SnapshotName };
                    _snapshots.Add(tempSnapshot);
                    cboSnapshot.SelectedItem = tempSnapshot;
                }
            }
            finally
            {
                _isLoadingStep = false;
            }

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

            // 조건부 실행 로드
            LoadConditionToUI(step);
        }

        private void LoadConditionToUI(TestStep step)
        {
            // 참조 스텝 목록 갱신
            var currentIndex = _steps.IndexOf(step);
            var previousSteps = _steps.Take(currentIndex).ToList();
            cboRefStep.ItemsSource = previousSteps;

            if (step.Condition != null)
            {
                cboConditionType.SelectedIndex = (int)step.Condition.Type;

                if (!string.IsNullOrEmpty(step.Condition.ReferenceStepId))
                {
                    var refStep = previousSteps.FirstOrDefault(s => s.Id == step.Condition.ReferenceStepId);
                    cboRefStep.SelectedItem = refStep;
                }
            }
            else
            {
                cboConditionType.SelectedIndex = 0;
            }

            UpdateConditionVisibility();
        }

        private void SaveCurrentStep()
        {
            if (_currentStep == null) return;

            _currentStep.Name = txtStepName.Text;

            var selectedSnapshot = cboSnapshot.SelectedItem as Snapshot;
            _currentStep.SnapshotName = selectedSnapshot?.Name ?? "";

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

            // 조건부 실행 저장
            SaveCondition();

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
                Steps = _steps.ToList(),
                PreTestEvent = GetPreEventFromUI(),
                PostTestEvent = GetPostEventFromUI()
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

        private async void TargetVM_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingStep) return;

            var selectedVM = cboTargetVM.SelectedItem as VMInfo;
            if (selectedVM != null)
            {
                await LoadSnapshotsAsync(selectedVM.VmxPath);
            }
            else
            {
                _snapshots.Clear();
            }
        }

        private async void RefreshSnapshots_Click(object sender, RoutedEventArgs e)
        {
            var selectedVM = cboTargetVM.SelectedItem as VMInfo;
            if (selectedVM == null)
            {
                MessageBox.Show("먼저 VM을 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await LoadSnapshotsAsync(selectedVM.VmxPath);
        }

        private async System.Threading.Tasks.Task LoadSnapshotsAsync(string vmxPath)
        {
            _snapshots.Clear();

            if (_vmwareService == null || !_vmwareService.IsConnected)
            {
                return;
            }

            try
            {
                btnRefreshSnapshots.IsEnabled = false;
                btnRefreshSnapshots.Content = "...";

                var snapshots = await _vmwareService.GetSnapshotsAsync(vmxPath);
                foreach (var snapshot in snapshots)
                {
                    _snapshots.Add(snapshot);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"스냅샷 로드 실패: {ex.Message}");
            }
            finally
            {
                btnRefreshSnapshots.IsEnabled = true;
                btnRefreshSnapshots.Content = "↻";
            }
        }

        private void BrowseExecPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "실행 파일 선택",
                Filter = "실행 파일 (*.exe;*.bat;*.cmd;*.ps1)|*.exe;*.bat;*.cmd;*.ps1|모든 파일 (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                txtExecPath.Text = dialog.FileName;
            }
        }

        private void AddFileToVM_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "VM에 복사할 파일 선택",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                var files = dgFilesToVM.ItemsSource as ObservableCollection<FileCopyInfo>;
                if (files == null)
                {
                    files = new ObservableCollection<FileCopyInfo>();
                    dgFilesToVM.ItemsSource = files;
                }

                foreach (var file in dialog.FileNames)
                {
                    files.Add(new FileCopyInfo
                    {
                        SourcePath = file,
                        DestinationPath = $"C:\\Test\\{Path.GetFileName(file)}"
                    });
                }
            }
        }

        private void AddFolderToVM_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "VM에 복사할 폴더 선택"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var files = dgFilesToVM.ItemsSource as ObservableCollection<FileCopyInfo>;
                if (files == null)
                {
                    files = new ObservableCollection<FileCopyInfo>();
                    dgFilesToVM.ItemsSource = files;
                }

                var folderName = Path.GetFileName(dialog.SelectedPath);
                files.Add(new FileCopyInfo
                {
                    SourcePath = dialog.SelectedPath,
                    DestinationPath = $"C:\\Test\\{folderName}"
                });
            }
        }

        private void SelectResultFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "결과 파일 저장 폴더 선택"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var files = dgResultFiles.ItemsSource as ObservableCollection<FileCopyInfo>;
                if (files == null)
                {
                    files = new ObservableCollection<FileCopyInfo>();
                    dgResultFiles.ItemsSource = files;
                }

                // 선택한 폴더를 대상 경로로 추가 (소스 경로는 사용자가 직접 입력)
                files.Add(new FileCopyInfo
                {
                    SourcePath = "C:\\Test\\결과.txt",
                    DestinationPath = Path.Combine(dialog.SelectedPath, "{VMName}_{StepName}_{Timestamp}")
                });
            }
        }

        private void SaveCondition()
        {
            if (_currentStep == null) return;

            var conditionType = (ConditionType)cboConditionType.SelectedIndex;

            if (conditionType == ConditionType.Always)
            {
                _currentStep.Condition = null;
            }
            else
            {
                _currentStep.Condition = new StepCondition
                {
                    Type = conditionType
                };

                if (conditionType == ConditionType.SpecificStepResult)
                {
                    var refStep = cboRefStep.SelectedItem as TestStep;
                    if (refStep != null)
                    {
                        _currentStep.Condition.ReferenceStepId = refStep.Id;
                        _currentStep.Condition.ReferenceStepName = refStep.Name;
                    }
                }
            }
        }

        private void ConditionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateConditionVisibility();
        }

        private void UpdateConditionVisibility()
        {
            if (cboConditionType == null || lblRefStep == null || cboRefStep == null) return;

            var showRefStep = cboConditionType.SelectedIndex == (int)ConditionType.SpecificStepResult;
            lblRefStep.Visibility = showRefStep ? Visibility.Visible : Visibility.Collapsed;
            cboRefStep.Visibility = showRefStep ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Event Handlers for Pre/Post Events

        private void BrowsePreEventCommand_Click(object sender, RoutedEventArgs e)
        {
            var path = BrowseEventFile();
            if (!string.IsNullOrEmpty(path))
            {
                txtPreEventCommand.Text = path;
            }
        }

        private void BrowsePreEventWorkDir_Click(object sender, RoutedEventArgs e)
        {
            var path = BrowseFolder("테스트 전 이벤트 작업 디렉토리 선택");
            if (!string.IsNullOrEmpty(path))
            {
                txtPreEventWorkDir.Text = path;
            }
        }

        private void BrowsePostEventCommand_Click(object sender, RoutedEventArgs e)
        {
            var path = BrowseEventFile();
            if (!string.IsNullOrEmpty(path))
            {
                txtPostEventCommand.Text = path;
            }
        }

        private void BrowsePostEventWorkDir_Click(object sender, RoutedEventArgs e)
        {
            var path = BrowseFolder("테스트 후 이벤트 작업 디렉토리 선택");
            if (!string.IsNullOrEmpty(path))
            {
                txtPostEventWorkDir.Text = path;
            }
        }

        private string BrowseEventFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "스크립트/실행 파일 선택",
                Filter = "실행 파일 (*.exe;*.bat;*.cmd;*.ps1)|*.exe;*.bat;*.cmd;*.ps1|모든 파일 (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }

        private string BrowseFolder(string description)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = description
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return dialog.SelectedPath;
            }
            return null;
        }

        #endregion

        #region Drag and Drop

        private void StepList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void StepList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging)
                return;

            var currentPosition = e.GetPosition(null);
            var diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var listBox = sender as ListBox;
                var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);

                if (listBoxItem != null)
                {
                    var step = listBox.ItemContainerGenerator.ItemFromContainer(listBoxItem) as TestStep;
                    if (step != null)
                    {
                        _isDragging = true;
                        var data = new DataObject("TestStep", step);
                        DragDrop.DoDragDrop(listBoxItem, data, DragDropEffects.Move);
                        _isDragging = false;
                    }
                }
            }
        }

        private void StepList_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TestStep"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void StepList_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TestStep"))
                return;

            var droppedStep = e.Data.GetData("TestStep") as TestStep;
            if (droppedStep == null)
                return;

            var listBox = sender as ListBox;
            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);

            if (targetItem != null)
            {
                var targetStep = listBox.ItemContainerGenerator.ItemFromContainer(targetItem) as TestStep;
                if (targetStep != null && droppedStep != targetStep)
                {
                    var oldIndex = _steps.IndexOf(droppedStep);
                    var newIndex = _steps.IndexOf(targetStep);

                    if (oldIndex >= 0 && newIndex >= 0)
                    {
                        _steps.Move(oldIndex, newIndex);
                        UpdateStepOrders();
                        lstSteps.SelectedItem = droppedStep;
                    }
                }
            }
            else
            {
                // 빈 공간에 드롭하면 맨 끝으로 이동
                var oldIndex = _steps.IndexOf(droppedStep);
                if (oldIndex >= 0 && oldIndex < _steps.Count - 1)
                {
                    _steps.Move(oldIndex, _steps.Count - 1);
                    UpdateStepOrders();
                    lstSteps.SelectedItem = droppedStep;
                }
            }

            e.Handled = true;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);

            return null;
        }

        #endregion
    }
}
