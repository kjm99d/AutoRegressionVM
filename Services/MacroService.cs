using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services
{
    /// <summary>
    /// 매크로/변수 치환 서비스
    /// </summary>
    public class MacroService
    {
        private readonly Dictionary<string, Func<MacroContext, string>> _macros;

        public MacroService()
        {
            _macros = new Dictionary<string, Func<MacroContext, string>>(StringComparer.OrdinalIgnoreCase)
            {
                // 날짜/시간 매크로
                { "DATE", ctx => DateTime.Now.ToString("yyyyMMdd") },
                { "TIME", ctx => DateTime.Now.ToString("HHmmss") },
                { "DATETIME", ctx => DateTime.Now.ToString("yyyyMMdd_HHmmss") },
                { "TIMESTAMP", ctx => DateTimeOffset.Now.ToUnixTimeSeconds().ToString() },
                { "YEAR", ctx => DateTime.Now.Year.ToString() },
                { "MONTH", ctx => DateTime.Now.Month.ToString("D2") },
                { "DAY", ctx => DateTime.Now.Day.ToString("D2") },
                { "HOUR", ctx => DateTime.Now.Hour.ToString("D2") },
                { "MINUTE", ctx => DateTime.Now.Minute.ToString("D2") },
                { "SECOND", ctx => DateTime.Now.Second.ToString("D2") },

                // VM 관련 매크로
                { "VM_NAME", ctx => ctx.VMName ?? "Unknown" },
                { "VM_PATH", ctx => ctx.VMPath ?? "" },

                // 스텝 관련 매크로
                { "STEP_NAME", ctx => ctx.StepName ?? "Unknown" },
                { "STEP_INDEX", ctx => ctx.StepIndex.ToString() },

                // 시나리오 관련 매크로
                { "SCENARIO_NAME", ctx => ctx.ScenarioName ?? "Unknown" },
                { "SCENARIO_ID", ctx => ctx.ScenarioId ?? "" },

                // 환경 매크로
                { "USERNAME", ctx => Environment.UserName },
                { "MACHINE", ctx => Environment.MachineName },
                { "TEMP", ctx => System.IO.Path.GetTempPath().TrimEnd('\\') },
                { "APPDIR", ctx => AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') },

                // 결과 디렉토리
                { "RESULT_DIR", ctx => System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Results",
                    DateTime.Now.ToString("yyyyMMdd"),
                    ctx.StepName ?? "Step") },
            };
        }

        /// <summary>
        /// 텍스트 내 매크로를 치환합니다
        /// </summary>
        public string ExpandMacros(string text, MacroContext context)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // {MACRO} 또는 ${MACRO} 형식 지원
            var pattern = @"\{([A-Z_]+)\}|\$\{([A-Z_]+)\}";

            return Regex.Replace(text, pattern, match =>
            {
                var macroName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

                if (_macros.TryGetValue(macroName, out var resolver))
                {
                    try
                    {
                        return resolver(context);
                    }
                    catch
                    {
                        return match.Value; // 오류 시 원본 유지
                    }
                }

                // 사용자 정의 변수 확인
                if (context.CustomVariables != null &&
                    context.CustomVariables.TryGetValue(macroName, out var value))
                {
                    return value;
                }

                return match.Value; // 매칭되는 매크로 없으면 원본 유지
            }, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// TestStep의 모든 경로에 매크로를 적용합니다
        /// </summary>
        public TestStep ExpandStepMacros(TestStep step, MacroContext context)
        {
            context.StepName = step.Name;

            // 실행 경로
            if (step.Execution != null)
            {
                step.Execution.ExecutablePath = ExpandMacros(step.Execution.ExecutablePath, context);
                step.Execution.Arguments = ExpandMacros(step.Execution.Arguments, context);
                step.Execution.WorkingDirectory = ExpandMacros(step.Execution.WorkingDirectory, context);
            }

            // 파일 복사 경로
            if (step.FilesToCopyToVM != null)
            {
                foreach (var file in step.FilesToCopyToVM)
                {
                    file.SourcePath = ExpandMacros(file.SourcePath, context);
                    file.DestinationPath = ExpandMacros(file.DestinationPath, context);
                }
            }

            // 결과 파일 경로
            if (step.ResultFilesToCollect != null)
            {
                foreach (var file in step.ResultFilesToCollect)
                {
                    file.SourcePath = ExpandMacros(file.SourcePath, context);
                    file.DestinationPath = ExpandMacros(file.DestinationPath, context);
                }
            }

            return step;
        }

        /// <summary>
        /// 사용 가능한 매크로 목록 반환
        /// </summary>
        public IEnumerable<string> GetAvailableMacros()
        {
            return _macros.Keys;
        }
    }

    /// <summary>
    /// 매크로 확장을 위한 컨텍스트 정보
    /// </summary>
    public class MacroContext
    {
        public string VMName { get; set; }
        public string VMPath { get; set; }
        public string StepName { get; set; }
        public int StepIndex { get; set; }
        public string ScenarioName { get; set; }
        public string ScenarioId { get; set; }
        public Dictionary<string, string> CustomVariables { get; set; }
    }
}
