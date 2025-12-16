using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services;

namespace AutoRegressionVM.Views
{
    public partial class SchedulerDialog : Window
    {
        private readonly SchedulerService _schedulerService;
        private readonly ObservableCollection<ScheduledTask> _tasks = new ObservableCollection<ScheduledTask>();
        private readonly List<TestScenario> _scenarios;
        private ScheduledTask _currentTask;

        public SchedulerDialog(SchedulerService schedulerService, IEnumerable<TestScenario> scenarios)
        {
            InitializeComponent();

            _schedulerService = schedulerService;
            _scenarios = scenarios.ToList();

            dgSchedules.ItemsSource = _tasks;
            cboScenario.ItemsSource = _scenarios;

            LoadTasks();
        }

        private void LoadTasks()
        {
            _tasks.Clear();
            foreach (var task in _schedulerService.GetAllTasks())
            {
                _tasks.Add(task);
            }
        }

        private void Schedule_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentTask = dgSchedules.SelectedItem as ScheduledTask;

            if (_currentTask != null)
            {
                LoadTaskToUI(_currentTask);
                gridEdit.IsEnabled = true;
            }
            else
            {
                gridEdit.IsEnabled = false;
            }
        }

        private void LoadTaskToUI(ScheduledTask task)
        {
            txtName.Text = task.Name;
            cboScenario.SelectedItem = _scenarios.FirstOrDefault(s => s.Id == task.ScenarioId);
            cboType.SelectedIndex = (int)task.ScheduleType;
            txtHour.Text = task.RunTime.Hours.ToString("D2");
            txtMinute.Text = task.RunTime.Minutes.ToString("D2");
            cboDayOfWeek.SelectedIndex = (int)task.DayOfWeek;
            txtDayOfMonth.Text = task.DayOfMonth.ToString();
            chkEnabled.IsChecked = task.IsEnabled;

            UpdateVisibility();
        }

        private void SaveTaskFromUI()
        {
            if (_currentTask == null) return;

            _currentTask.Name = txtName.Text;

            var scenario = cboScenario.SelectedItem as TestScenario;
            if (scenario != null)
            {
                _currentTask.ScenarioId = scenario.Id;
                _currentTask.ScenarioName = scenario.Name;
            }

            _currentTask.ScheduleType = (ScheduleType)cboType.SelectedIndex;

            int.TryParse(txtHour.Text, out var hour);
            int.TryParse(txtMinute.Text, out var minute);
            _currentTask.RunTime = new TimeSpan(hour, minute, 0);

            _currentTask.DayOfWeek = (DayOfWeek)cboDayOfWeek.SelectedIndex;
            int.TryParse(txtDayOfMonth.Text, out var dayOfMonth);
            _currentTask.DayOfMonth = dayOfMonth;

            _currentTask.IsEnabled = chkEnabled.IsChecked ?? true;

            // 다음 실행 시간 계산
            CalculateNextRunTime(_currentTask);
        }

        private void CalculateNextRunTime(ScheduledTask task)
        {
            var now = DateTime.Now;

            switch (task.ScheduleType)
            {
                case ScheduleType.Once:
                    var onceDate = now.Date.Add(task.RunTime);
                    if (onceDate <= now) onceDate = onceDate.AddDays(1);
                    task.NextRunTime = onceDate;
                    break;

                case ScheduleType.Daily:
                    var dailyDate = now.Date.Add(task.RunTime);
                    if (dailyDate <= now) dailyDate = dailyDate.AddDays(1);
                    task.NextRunTime = dailyDate;
                    break;

                case ScheduleType.Weekly:
                    var daysUntilNext = ((int)task.DayOfWeek - (int)now.DayOfWeek + 7) % 7;
                    var weeklyDate = now.Date.AddDays(daysUntilNext).Add(task.RunTime);
                    if (weeklyDate <= now) weeklyDate = weeklyDate.AddDays(7);
                    task.NextRunTime = weeklyDate;
                    break;

                case ScheduleType.Monthly:
                    var monthlyDate = new DateTime(now.Year, now.Month,
                        Math.Min(task.DayOfMonth, DateTime.DaysInMonth(now.Year, now.Month))).Add(task.RunTime);
                    if (monthlyDate <= now)
                    {
                        var nextMonth = now.AddMonths(1);
                        monthlyDate = new DateTime(nextMonth.Year, nextMonth.Month,
                            Math.Min(task.DayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month))).Add(task.RunTime);
                    }
                    task.NextRunTime = monthlyDate;
                    break;

                case ScheduleType.Interval:
                    task.NextRunTime = now.Add(task.Interval);
                    break;
            }
        }

        private void Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (cboType == null) return;

            var type = (ScheduleType)cboType.SelectedIndex;

            lblDayOfWeek.Visibility = type == ScheduleType.Weekly ? Visibility.Visible : Visibility.Collapsed;
            cboDayOfWeek.Visibility = type == ScheduleType.Weekly ? Visibility.Visible : Visibility.Collapsed;

            lblDayOfMonth.Visibility = type == ScheduleType.Monthly ? Visibility.Visible : Visibility.Collapsed;
            txtDayOfMonth.Visibility = type == ScheduleType.Monthly ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var newTask = new ScheduledTask
            {
                Name = "새 스케줄",
                ScheduleType = ScheduleType.Daily,
                RunTime = new TimeSpan(9, 0, 0),
                IsEnabled = true
            };

            _schedulerService.AddTask(newTask);
            _tasks.Add(newTask);
            dgSchedules.SelectedItem = newTask;
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTask == null) return;

            var result = MessageBox.Show("선택한 스케줄을 삭제하시겠습니까?", "확인",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _schedulerService.RemoveTask(_currentTask.Id);
                _tasks.Remove(_currentTask);
                _currentTask = null;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTask == null) return;

            SaveTaskFromUI();
            _schedulerService.UpdateTask(_currentTask);
            dgSchedules.Items.Refresh();

            MessageBox.Show("저장되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
