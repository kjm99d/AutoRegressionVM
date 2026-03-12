# AutoRegressionVM

VMware 가상 머신을 활용한 자동화된 회귀 테스트 도구입니다. GUI와 CLI 모드를 모두 지원하며, 스냅샷 기반의 반복 테스트 환경을 제공합니다.

## 주요 기능

- **VM 관리**: VMware Workstation VM 등록, 전원 상태 모니터링, 스냅샷 관리
- **스냅샷 기반 테스트**: 테스트 전/후 스냅샷으로 자동 복원하여 일관된 테스트 환경 보장
- **시나리오 기반 실행**: 여러 테스트 스텝을 시나리오로 구성하여 순차/병렬 실행
- **병렬 VM 실행**: 최대 N개 VM 동시 실행, VM별 독립적 예외 처리
- **실패 TC 자동 재시도**: 실패한 테스트를 스냅샷 롤백 후 자동으로 재시도 (MaxRetryCount 설정)
- **조건부 스텝 실행**: 이전 스텝 결과에 따라 실행 여부 결정
- **파일 전송**: 호스트 ↔ VM 간 파일 복사 지원
- **결과 수집 및 검증**: Exit Code, JSON Path, 텍스트 매칭 기반 성공 판정
- **네트워크 차단**: 오프라인 테스트를 위한 네트워크 강제 분리/복원
- **스크린샷 캡처**: 실행 중 주기적 스크린샷 자동 캡처
- **매크로 변수**: 경로/명령어에 `{DATE}`, `{VM_NAME}` 등 20+ 매크로 지원
- **스케줄러**: 일회/매일/매주/매월/간격 기반 자동 실행 예약
- **Pre/Post 이벤트**: 시나리오 실행 전후 스크립트/명령어 훅
- **알림**: Email, Slack, Teams 알림 연동
- **리포트**: HTML, JSON, XML(JUnit) 형식 결과 리포트 생성
- **CLI 지원**: CI/CD 파이프라인 연동을 위한 명령줄 인터페이스
- **비밀번호 보호**: DPAPI 암호화로 Guest 자격 증명 안전 저장

## 요구 사항

- Windows 10/11
- .NET Framework 4.7.2 이상
- VMware Workstation Pro
- VM 내 VMware Tools 설치 필요 (Guest 작업용)

## 설치

1. 저장소를 클론합니다.
2. Visual Studio 2019 이상에서 솔루션을 열고 빌드합니다.
3. `settings.example.json`을 `settings.json`으로 복사하고 환경에 맞게 수정합니다.

## 사용법

### GUI 모드

```bash
AutoRegressionVM.exe
```

#### 키보드 단축키

| 단축키 | 동작 |
|--------|------|
| `F5` | 시나리오 실행 |
| `Esc` | 실행 중지 |
| `Ctrl+E` | 시나리오 편집 |
| `Ctrl+N` | 새 시나리오 |
| `Ctrl+S` | 저장 |

### CLI 모드

```bash
# 시나리오 목록 조회
AutoRegressionVM.exe --list-scenarios

# VM 목록 조회
AutoRegressionVM.exe --list-vms

# 시나리오 실행
AutoRegressionVM.exe --scenario "테스트시나리오명"

# 특정 VM에서만 실행
AutoRegressionVM.exe --scenario "시나리오명" --vm "VM이름"

# 병렬 실행 (최대 2개 VM 동시 실행)
AutoRegressionVM.exe --scenario "시나리오명" --parallel 2

# 실행 제한 시간 설정 (분 단위)
AutoRegressionVM.exe --scenario "시나리오명" --timeout 60

# 출력 형식 지정 (text, json, xml)
AutoRegressionVM.exe --scenario "시나리오명" --output json
AutoRegressionVM.exe --scenario "시나리오명" --output xml

# 결과 리포트 저장
AutoRegressionVM.exe --scenario "시나리오명" --report "C:\Reports\result.json"

# 드라이런 (실제 실행 없이 검증)
AutoRegressionVM.exe --scenario "시나리오명" --dry-run

# 상세 로그 출력
AutoRegressionVM.exe --scenario "시나리오명" --verbose
```

#### CLI Exit Code

| 코드 | 의미 |
|------|------|
| `0` | 모든 테스트 성공 |
| `1` | 일부 테스트 실패 |
| `2` | 시나리오 또는 VM 없음 |
| `3` | VMware 연결 실패 |
| `4` | 잘못된 파라미터 |
| `5` | 타임아웃 또는 처리되지 않은 오류 |

## 테스트 시나리오 구성

시나리오는 여러 테스트 스텝으로 구성됩니다. 각 스텝에서는:

1. **스냅샷 복원**: 지정된 스냅샷으로 VM 상태 복원
2. **VMware Tools 대기**: Guest OS 부팅 완료까지 대기
3. **파일 복사**: 테스트에 필요한 파일을 VM으로 전송
4. **네트워크 차단**: 오프라인 테스트 시 네트워크 비활성화 (선택)
5. **테스트 실행**: VM 내에서 프로그램/스크립트 실행
6. **스크린샷 캡처**: 실행 중 주기적 스크린샷 저장 (선택)
7. **대기**: 실행 후 지정 시간 대기 (선택)
8. **네트워크 복원**: 차단된 네트워크 재활성화
9. **결과 수집**: VM에서 결과 파일을 호스트로 복사
10. **성공 판정**: Exit Code, JSON Path, 텍스트 기반 검증
11. **정리**: 스냅샷으로 VM 상태 복원 (선택)

### 실패 시 자동 재시도

시나리오에 `MaxRetryCount`를 설정하면, 실패한 TC를 자동으로 재시도합니다. 재시도 시 스냅샷을 롤백하여 깨끗한 상태에서 다시 실행합니다. 병렬 실행 중에도 개별 TC 단위로 재시도됩니다.

### 성공 기준 (SuccessCriteria)

| 기준 | 설명 |
|------|------|
| `ExpectedExitCode` | 기대하는 프로세스 종료 코드 (null이면 체크 안 함) |
| `ResultJsonPath` + `ExpectedJsonValue` | 결과 JSON 파일에서 특정 경로의 값 검증 |
| `ContainsText` | 출력에 반드시 포함되어야 할 문자열 |
| `NotContainsText` | 출력에 포함되면 안 되는 문자열 |

### 조건부 실행 (StepCondition)

| 조건 | 설명 |
|------|------|
| `Always` | 항상 실행 (기본값) |
| `PreviousStepPassed` | 바로 이전 스텝 성공 시 |
| `PreviousStepFailed` | 바로 이전 스텝 실패 시 |
| `SpecificStepResult` | 특정 스텝의 결과에 따라 |
| `AllPreviousPassed` | 모든 이전 스텝 성공 시 |
| `AnyPreviousFailed` | 이전 스텝 중 하나라도 실패 시 |

## 매크로 변수

경로, 명령어, 인자 등에서 `{MACRO}` 또는 `${MACRO}` 형식으로 사용할 수 있습니다.

| 매크로 | 설명 | 예시 |
|--------|------|------|
| `{DATE}` | 현재 날짜 | `20260312` |
| `{TIME}` | 현재 시간 | `143052` |
| `{DATETIME}` | 날짜+시간 | `20260312_143052` |
| `{TIMESTAMP}` | Unix 타임스탬프 | `1773496252` |
| `{YEAR}`, `{MONTH}`, `{DAY}` | 연/월/일 | `2026`, `03`, `12` |
| `{HOUR}`, `{MINUTE}`, `{SECOND}` | 시/분/초 | `14`, `30`, `52` |
| `{VM_NAME}` | 대상 VM 이름 | `Win10-QA` |
| `{VM_PATH}` | VM의 VMX 경로 | `D:\VMware\Win10\Win10.vmx` |
| `{STEP_NAME}` | 현재 스텝 이름 | `설치 테스트` |
| `{STEP_INDEX}` | 현재 스텝 인덱스 | `0` |
| `{SCENARIO_NAME}` | 시나리오 이름 | `회귀 테스트 v2` |
| `{SCENARIO_ID}` | 시나리오 ID | `a1b2c3d4-...` |
| `{USERNAME}` | Windows 사용자명 | `Developer` |
| `{MACHINE}` | 컴퓨터 이름 | `QA-PC-01` |
| `{TEMP}` | 임시 폴더 경로 | `C:\Users\...\Temp` |
| `{APPDIR}` | 애플리케이션 경로 | `D:\AutoRegressionVM\bin` |
| `{RESULT_DIR}` | 결과 저장 경로 | `...\Results\20260312\스텝명` |

**사용 예시:**
```
실행 파일: C:\Installers\{DATE}\setup.exe
결과 수집: {RESULT_DIR}\{VM_NAME}_result.json
```

## 스케줄러

GUI에서 ⏰ 버튼으로 스케줄러 다이얼로그를 열 수 있습니다.

| 유형 | 설명 |
|------|------|
| `Once` | 지정 시각에 한 번만 실행 |
| `Daily` | 매일 지정 시각에 실행 |
| `Weekly` | 매주 특정 요일/시각에 실행 |
| `Monthly` | 매월 특정 일/시각에 실행 |
| `Interval` | 지정 간격으로 반복 실행 |

스케줄 트리거 시 자동으로 VMware에 연결하고 지정된 시나리오를 실행합니다.

## 알림 설정

`settings.json`에서 알림 설정을 구성할 수 있습니다:

- **Email**: SMTP 서버, 포트, 인증 정보, 수신자 설정
- **Slack**: Incoming Webhook URL 설정
- **Teams**: Incoming Webhook URL 설정 (Adaptive Card 형식)

알림 트리거를 선택적으로 구성할 수 있습니다:
- `NotifyOnStart`: 테스트 시작 시
- `NotifyOnComplete`: 테스트 완료 시
- `NotifyOnFailure`: 테스트 실패 시
- `NotifyOnError`: 오류 발생 시

## 설정 파일

`settings.example.json`을 참고하여 `settings.json`을 작성합니다:

```json
{
  "VMwareInstallPath": "C:\\Program Files (x86)\\VMware\\VMware Workstation",
  "DefaultVMPath": "D:\\VMware",
  "ResultOutputPath": ".\\Results",
  "ScenariosPath": ".\\Scenarios",
  "Notification": {
    "Enabled": true,
    "Type": "Slack",
    "SlackWebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
    "TeamsWebhookUrl": "",
    "SmtpServer": "",
    "SmtpPort": 587,
    "SmtpUsername": "",
    "SmtpPassword": "",
    "EmailTo": "",
    "NotifyOnComplete": true,
    "NotifyOnFailure": true,
    "NotifyOnStart": false,
    "NotifyOnError": true
  },
  "RegisteredVMs": []
}
```

## 프로젝트 구조

```
AutoRegressionVM/
├── CLI/                        # 명령줄 인터페이스
│   ├── CliOptions.cs           # CLI 옵션 모델
│   ├── CliRunner.cs            # CLI 실행 엔진
│   └── CommandLineParser.cs    # 인자 파서
├── Helpers/                    # 유틸리티 클래스
│   ├── CredentialProtector.cs  # DPAPI 비밀번호 암호화
│   ├── NullToCollapsedConverter.cs # XAML 값 변환기
│   ├── RelayCommand.cs         # ICommand 구현
│   ├── SimpleJson.cs           # JSON 유틸리티
│   └── ViewModelBase.cs        # MVVM 베이스 클래스
├── Models/                     # 데이터 모델
│   ├── AppSettings.cs          # 앱 설정
│   ├── Snapshot.cs             # VM 스냅샷
│   ├── TestResult.cs           # 테스트 결과
│   ├── TestScenario.cs         # 시나리오 정의
│   ├── TestStep.cs             # 스텝/실행 정보/성공 기준
│   ├── VMExecutionStatus.cs    # VM별 실행 상태
│   └── VMInfo.cs               # VM 정보 (DPAPI 암호화)
├── Services/                   # 서비스 계층
│   ├── Notification/           # 알림 서비스
│   │   ├── INotificationService.cs
│   │   ├── NotificationManager.cs  # 팩토리 패턴 알림 관리
│   │   ├── EmailNotificationService.cs
│   │   ├── SlackNotificationService.cs
│   │   └── TeamsNotificationService.cs
│   ├── TestExecution/          # 테스트 실행 엔진
│   │   ├── ITestRunner.cs
│   │   └── TestRunner.cs       # 순차/병렬 실행, 재시도
│   ├── VMware/                 # VMware 연동
│   │   ├── IVMwareService.cs
│   │   └── VixService.cs       # vmrun.exe 래퍼
│   ├── IReportService.cs
│   ├── IScenarioService.cs
│   ├── ISettingsService.cs
│   ├── MacroService.cs         # 매크로 변수 치환
│   ├── ReportService.cs        # HTML/JSON 리포트 생성
│   ├── ScenarioService.cs      # 시나리오 CRUD
│   ├── SchedulerService.cs     # 예약 실행 스케줄러
│   └── SettingsService.cs      # 설정 관리
├── ViewModels/                 # MVVM ViewModel
│   └── MainViewModel.cs
├── Views/                      # WPF 다이얼로그
│   ├── AddVMDialog.xaml        # VM 추가
│   ├── ScenarioEditorDialog.xaml # 시나리오 편집기
│   ├── SchedulerDialog.xaml    # 스케줄러 설정
│   ├── SettingsDialog.xaml     # 앱 설정
│   └── TestHistoryDialog.xaml  # 테스트 이력 조회
├── App.xaml                    # WPF 앱 루트
├── MainWindow.xaml             # 메인 윈도우
├── settings.example.json       # 설정 파일 예시
└── AutoRegressionVM.csproj     # 프로젝트 파일
```

## 아키텍처

- **패턴**: MVVM (Model-View-ViewModel)
- **DI**: 생성자 주입 (App.xaml.cs Composition Root)
- **SOLID**: 인터페이스 분리 (ISettingsService, IScenarioService, IReportService, ITestRunner, IVMwareService)
- **팩토리**: NotificationManager - 확장 가능한 알림 채널 등록
- **직렬화**: Newtonsoft.Json 통일 사용
- **보안**: DPAPI (CurrentUser scope) Guest 비밀번호 암호화

## 라이선스

MIT License
