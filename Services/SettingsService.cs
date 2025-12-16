using System;
using System.Collections.Generic;
using System.IO;
using AutoRegressionVM.Helpers;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services
{
    /// <summary>
    /// 설정 및 시나리오 저장/로드 서비스
    /// </summary>
    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly string _scenariosDirectory;

        public SettingsService(string basePath = null)
        {
            var baseDir = basePath ?? AppDomain.CurrentDomain.BaseDirectory;
            _settingsPath = Path.Combine(baseDir, "settings.json");
            _scenariosDirectory = Path.Combine(baseDir, "Scenarios");

            if (!Directory.Exists(_scenariosDirectory))
            {
                Directory.CreateDirectory(_scenariosDirectory);
            }
        }

        #region AppSettings

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    return SimpleJson.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 로드 실패: {ex.Message}");
            }

            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = SimpleJson.Serialize(settings);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 저장 실패: {ex.Message}");
            }
        }

        #endregion

        #region Scenarios

        public List<TestScenario> LoadAllScenarios()
        {
            var scenarios = new List<TestScenario>();

            try
            {
                if (Directory.Exists(_scenariosDirectory))
                {
                    foreach (var file in Directory.GetFiles(_scenariosDirectory, "*.json"))
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var scenario = SimpleJson.Deserialize<TestScenario>(json);
                            if (scenario != null)
                            {
                                scenarios.Add(scenario);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"시나리오 로드 실패 ({file}): {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"시나리오 목록 로드 실패: {ex.Message}");
            }

            return scenarios;
        }

        public void SaveScenario(TestScenario scenario)
        {
            try
            {
                var fileName = SanitizeFileName(scenario.Name) + ".json";
                var filePath = Path.Combine(_scenariosDirectory, fileName);

                var json = SimpleJson.Serialize(scenario);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"시나리오 저장 실패: {ex.Message}");
                throw;
            }
        }

        public void DeleteScenario(TestScenario scenario)
        {
            try
            {
                var fileName = SanitizeFileName(scenario.Name) + ".json";
                var filePath = Path.Combine(_scenariosDirectory, fileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"시나리오 삭제 실패: {ex.Message}");
            }
        }

        private string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        #endregion

        #region Results

        public void SaveResult(ScenarioResult result, string outputPath = null)
        {
            try
            {
                var resultDir = outputPath ?? Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    "Results", 
                    DateTime.Now.ToString("yyyyMMdd"));

                if (!Directory.Exists(resultDir))
                {
                    Directory.CreateDirectory(resultDir);
                }

                var fileName = $"{SanitizeFileName(result.ScenarioName)}_{result.StartTime:HHmmss}.json";
                var filePath = Path.Combine(resultDir, fileName);

                var json = SimpleJson.Serialize(result);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"결과 저장 실패: {ex.Message}");
            }
        }

        #endregion
    }
}
