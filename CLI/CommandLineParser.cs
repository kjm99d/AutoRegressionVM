using System;
using System.Linq;

namespace AutoRegressionVM.CLI
{
    /// <summary>
    /// 커맨드라인 인수 파서
    /// </summary>
    public static class CommandLineParser
    {
        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLower();

                switch (arg)
                {
                    case "--cli":
                    case "-c":
                        options.CliMode = true;
                        break;

                    case "--scenario":
                    case "-s":
                        if (i + 1 < args.Length)
                            options.ScenarioName = args[++i];
                        break;

                    case "--vm":
                        if (i + 1 < args.Length)
                            options.VMName = args[++i];
                        break;

                    case "--parallel":
                    case "-p":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int parallel))
                            options.Parallel = parallel;
                        break;

                    case "--output":
                    case "-o":
                        if (i + 1 < args.Length)
                            options.OutputFormat = args[++i].ToLower();
                        break;

                    case "--report":
                    case "-r":
                        if (i + 1 < args.Length)
                            options.ReportPath = args[++i];
                        break;

                    case "--timeout":
                    case "-t":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int timeout))
                            options.TimeoutMinutes = timeout;
                        break;

                    case "--list-scenarios":
                        options.ListScenarios = true;
                        options.CliMode = true;
                        break;

                    case "--list-vms":
                        options.ListVMs = true;
                        options.CliMode = true;
                        break;

                    case "--help":
                    case "-h":
                    case "/?":
                        options.ShowHelp = true;
                        options.CliMode = true;
                        break;

                    case "--verbose":
                    case "-v":
                        options.Verbose = true;
                        break;

                    case "--dry-run":
                        options.DryRun = true;
                        break;
                }
            }

            return options;
        }

        public static void PrintHelp()
        {
            Console.WriteLine(@"
????????????????????????????????????????????????????????????????
?              AutoRegressionVM - CLI Mode                     ?
????????????????????????????????????????????????????????????????

사용법:
  AutoRegressionVM.exe [옵션]

옵션:
  --cli, -c                 CLI 모드로 실행
  --scenario, -s <이름>     실행할 시나리오 이름
  --vm <이름>               특정 VM에서만 실행
  --parallel, -p <수>       병렬 실행 VM 수
  --output, -o <형식>       출력 형식 (text, json, xml)
  --report, -r <경로>       리포트 저장 경로
  --timeout, -t <분>        전체 타임아웃 (분)
  --list-scenarios          시나리오 목록 조회
  --list-vms                VM 목록 조회
  --verbose, -v             상세 로그 출력
  --dry-run                 실제 실행 없이 검증만
  --help, -h                도움말 표시

예제:
  # 시나리오 실행
  AutoRegressionVM.exe --cli --scenario ""악성코드 분석 테스트""

  # 병렬 실행 (최대 3개 VM)
  AutoRegressionVM.exe --cli -s ""전체 리그레션"" --parallel 3

  # JSON 출력
  AutoRegressionVM.exe --cli -s ""테스트"" --output json

  # 리포트 저장
  AutoRegressionVM.exe --cli -s ""테스트"" --report ""C:\Reports\result.html""

Exit Codes:
  0  모든 테스트 성공
  1  일부 테스트 실패
  2  시나리오를 찾을 수 없음
  3  VM 연결 실패
  4  잘못된 인수
  5  타임아웃
");
        }
    }
}
