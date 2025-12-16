using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AutoRegressionVM.Helpers;
using AutoRegressionVM.Models;
using AutoRegressionVM.Services;
using Microsoft.Win32;

namespace AutoRegressionVM.Views
{
    public partial class TestHistoryDialog : Window
    {
        private readonly SettingsService _settingsService;
        private readonly ReportService _reportService;
        private readonly ObservableCollection<ScenarioResult> _results = new ObservableCollection<ScenarioResult>();
        private List<ScenarioResult> _allResults = new List<ScenarioResult>();

        public TestHistoryDialog(SettingsService settingsService)
        {
            InitializeComponent();

            _settingsService = settingsService;
            _reportService = new ReportService();
            dgHistory.ItemsSource = _results;

            // 기본 날짜 설정
            dpFrom.SelectedDate = DateTime.Now.AddDays(-30);
            dpTo.SelectedDate = DateTime.Now;

            LoadHistory();
            LoadScenarioFilter();
        }

        private void LoadHistory()
        {
            _allResults.Clear();
            _results.Clear();

            var resultsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results");
            if (!Directory.Exists(resultsDir))
            {
                return;
            }

            // 날짜별 폴더 탐색
            foreach (var dateDir in Directory.GetDirectories(resultsDir))
            {
                foreach (var file in Directory.GetFiles(dateDir, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var result = SimpleJson.Deserialize<ScenarioResult>(json);
                        if (result != null)
                        {
                            _allResults.Add(result);
                        }
                    }
                    catch
                    {
                        // 파싱 실패 무시
                    }
                }
            }

            // 최신순 정렬
            _allResults = _allResults.OrderByDescending(r => r.StartTime).ToList();

            foreach (var result in _allResults)
            {
                _results.Add(result);
            }
        }

        private void LoadScenarioFilter()
        {
            var scenarios = _allResults.Select(r => r.ScenarioName).Distinct().OrderBy(n => n).ToList();
            scenarios.Insert(0, "(전체)");
            cboScenario.ItemsSource = scenarios;
            cboScenario.SelectedIndex = 0;
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = DateTime.Now.AddDays(-30);
            dpTo.SelectedDate = DateTime.Now;
            cboScenario.SelectedIndex = 0;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            _results.Clear();

            var filtered = _allResults.AsEnumerable();

            // 날짜 필터
            if (dpFrom.SelectedDate.HasValue)
            {
                filtered = filtered.Where(r => r.StartTime >= dpFrom.SelectedDate.Value);
            }
            if (dpTo.SelectedDate.HasValue)
            {
                filtered = filtered.Where(r => r.StartTime <= dpTo.SelectedDate.Value.AddDays(1));
            }

            // 시나리오 필터
            var selectedScenario = cboScenario.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedScenario) && selectedScenario != "(전체)")
            {
                filtered = filtered.Where(r => r.ScenarioName == selectedScenario);
            }

            foreach (var result in filtered)
            {
                _results.Add(result);
            }
        }

        private void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 선택된 항목 처리
        }

        private void OpenHtmlReport_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgHistory.SelectedItem as ScenarioResult;
            if (selected == null)
            {
                MessageBox.Show("항목을 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var htmlPath = _reportService.GenerateHtmlReport(selected);
                Process.Start(new ProcessStartInfo(htmlPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"리포트 생성 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgHistory.SelectedItem as ScenarioResult;
            if (selected == null)
            {
                MessageBox.Show("항목을 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "JSON 내보내기",
                Filter = "JSON 파일 (*.json)|*.json",
                FileName = $"{selected.ScenarioName}_{selected.StartTime:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _reportService.GenerateJsonReport(selected, dialog.FileName);
                    MessageBox.Show("내보내기 완료", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgHistory.SelectedItem as ScenarioResult;
            if (selected == null)
            {
                MessageBox.Show("항목을 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("선택한 이력을 삭제하시겠습니까?", "확인",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 파일 삭제
                var resultsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results");
                var dateDir = Path.Combine(resultsDir, selected.StartTime.ToString("yyyyMMdd"));

                if (Directory.Exists(dateDir))
                {
                    var pattern = $"*_{selected.StartTime:HHmmss}.json";
                    foreach (var file in Directory.GetFiles(dateDir, pattern))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // 삭제 실패 무시
                        }
                    }
                }

                _allResults.Remove(selected);
                _results.Remove(selected);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
