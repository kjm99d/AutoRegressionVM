using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services
{
    /// <summary>
    /// ���� �� �ó����� ����/�ε� ����
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
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"���� �ε� ����: {ex.Message}");
            }

            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"���� ���� ����: {ex.Message}");
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
                            var scenario = JsonConvert.DeserializeObject<TestScenario>(json);
                            if (scenario != null)
                            {
                                scenarios.Add(scenario);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"�ó����� �ε� ���� ({file}): {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�ó����� ��� �ε� ����: {ex.Message}");
            }

            return scenarios;
        }

        public void SaveScenario(TestScenario scenario)
        {
            try
            {
                // ID 기반 저장으로 이름 변경 시 고아 파일 방지
                var fileName = SanitizeFileName(scenario.Id) + ".json";
                var filePath = Path.Combine(_scenariosDirectory, fileName);

                // 기존 이름 기반 파일이 있으면 삭제 (마이그레이션)
                var legacyPath = Path.Combine(_scenariosDirectory, SanitizeFileName(scenario.Name) + ".json");
                if (legacyPath != filePath && File.Exists(legacyPath))
                {
                    try { File.Delete(legacyPath); } catch { }
                }

                var json = JsonConvert.SerializeObject(scenario, Formatting.Indented);
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
                // ID 기반 파일 삭제
                var fileName = SanitizeFileName(scenario.Id) + ".json";
                var filePath = Path.Combine(_scenariosDirectory, fileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // 기존 이름 기반 파일도 삭제
                var legacyPath = Path.Combine(_scenariosDirectory, SanitizeFileName(scenario.Name) + ".json");
                if (legacyPath != filePath && File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
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

                var json = JsonConvert.SerializeObject(result, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��� ���� ����: {ex.Message}");
            }
        }

        #endregion
    }
}
