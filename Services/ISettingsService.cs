using System.Collections.Generic;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services
{
    /// <summary>
    /// 설정 및 시나리오 저장/로드 서비스 인터페이스
    /// </summary>
    public interface ISettingsService
    {
        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);

        List<TestScenario> LoadAllScenarios();
        void SaveScenario(TestScenario scenario);
        void DeleteScenario(TestScenario scenario);

        void SaveResult(ScenarioResult result, string outputPath = null);
    }
}
