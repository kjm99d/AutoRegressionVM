using System.Collections.Generic;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services
{
    /// <summary>
    /// 시나리오 CRUD 및 Import/Export 서비스 인터페이스 (SRP)
    /// </summary>
    public interface IScenarioService
    {
        List<TestScenario> LoadAll();
        void Save(TestScenario scenario);
        void Delete(TestScenario scenario);
        TestScenario Clone(TestScenario source);
        void ExportToFile(TestScenario scenario, string filePath);
        TestScenario ImportFromFile(string filePath);
    }
}
