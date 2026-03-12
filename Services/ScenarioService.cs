using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services
{
    /// <summary>
    /// 시나리오 CRUD 및 Import/Export (SRP: MainViewModel에서 분리)
    /// </summary>
    public class ScenarioService : IScenarioService
    {
        private readonly ISettingsService _settingsService;

        public ScenarioService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public List<TestScenario> LoadAll()
        {
            return _settingsService.LoadAllScenarios();
        }

        public void Save(TestScenario scenario)
        {
            _settingsService.SaveScenario(scenario);
        }

        public void Delete(TestScenario scenario)
        {
            _settingsService.DeleteScenario(scenario);
        }

        public TestScenario Clone(TestScenario source)
        {
            return new TestScenario
            {
                Id = Guid.NewGuid().ToString(),
                Name = source.Name + " (복사본)",
                Description = source.Description,
                MaxParallelVMs = source.MaxParallelVMs,
                ContinueOnFailure = source.ContinueOnFailure,
                CreatedAt = DateTime.Now,
                Steps = source.Steps.Select(s => new TestStep
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = s.Name,
                    Description = s.Description,
                    Order = s.Order,
                    TargetVmxPath = s.TargetVmxPath,
                    SnapshotName = s.SnapshotName,
                    FilesToCopyToVM = s.FilesToCopyToVM?.Select(f => new FileCopyInfo
                    {
                        SourcePath = f.SourcePath,
                        DestinationPath = f.DestinationPath
                    }).ToList() ?? new List<FileCopyInfo>(),
                    ResultFilesToCollect = s.ResultFilesToCollect?.Select(f => new FileCopyInfo
                    {
                        SourcePath = f.SourcePath,
                        DestinationPath = f.DestinationPath
                    }).ToList() ?? new List<FileCopyInfo>(),
                    Execution = new ExecutionInfo
                    {
                        Type = s.Execution?.Type ?? ExecutionType.Program,
                        ExecutablePath = s.Execution?.ExecutablePath,
                        Arguments = s.Execution?.Arguments,
                        WorkingDirectory = s.Execution?.WorkingDirectory,
                        TimeoutSeconds = s.Execution?.TimeoutSeconds ?? 300,
                        WaitForExit = s.Execution?.WaitForExit ?? true
                    },
                    SuccessCriteria = new SuccessCriteria
                    {
                        ExpectedExitCode = s.SuccessCriteria?.ExpectedExitCode,
                        ResultJsonPath = s.SuccessCriteria?.ResultJsonPath,
                        ExpectedJsonValue = s.SuccessCriteria?.ExpectedJsonValue,
                        ContainsText = s.SuccessCriteria?.ContainsText,
                        NotContainsText = s.SuccessCriteria?.NotContainsText
                    },
                    ForceNetworkDisconnect = s.ForceNetworkDisconnect,
                    CaptureScreenshots = s.CaptureScreenshots,
                    ScreenshotIntervalSeconds = s.ScreenshotIntervalSeconds,
                    ForceSnapshotRevertAfter = s.ForceSnapshotRevertAfter,
                    WaitAfterExecution = new WaitTime
                    {
                        Hours = s.WaitAfterExecution?.Hours ?? 0,
                        Minutes = s.WaitAfterExecution?.Minutes ?? 0,
                        Seconds = s.WaitAfterExecution?.Seconds ?? 0
                    }
                }).ToList(),
                TestTargetFiles = source.TestTargetFiles?.Select(f => new TestTargetFile
                {
                    HostFilePath = f.HostFilePath,
                    VMDestinationPath = f.VMDestinationPath,
                    Description = f.Description
                }).ToList() ?? new List<TestTargetFile>(),
                TargetVMPaths = new List<string>(source.TargetVMPaths ?? new List<string>())
            };
        }

        public void ExportToFile(TestScenario scenario, string filePath)
        {
            var json = JsonConvert.SerializeObject(scenario, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public TestScenario ImportFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var scenario = JsonConvert.DeserializeObject<TestScenario>(json);

            if (scenario != null)
            {
                scenario.Id = Guid.NewGuid().ToString();
                scenario.CreatedAt = DateTime.Now;
            }

            return scenario;
        }
    }
}
