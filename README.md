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

---

## 부록: QA 팀 활용 가이드

### A. 최초 설정 (첫 사용자용)

#### 1단계: 환경 준비

```
1. GitHub Releases에서 최신 ZIP 다운로드
2. 원하는 경로에 압축 해제 (예: D:\Tools\AutoRegressionVM)
3. settings.example.json → settings.json으로 복사
4. settings.json을 열어 환경에 맞게 수정:
```

```json
{
  "VMwareInstallPath": "C:\\Program Files (x86)\\VMware\\VMware Workstation",
  "DefaultVMPath": "D:\\VMware",
  "ResultOutputPath": ".\\Results",
  "ScenariosPath": ".\\Scenarios"
}
```

#### 2단계: VM 준비

```
1. VMware Workstation에서 테스트용 VM 생성
2. VM 내에 VMware Tools 설치 (필수 — Guest 파일 복사/실행에 필요)
3. VM의 Guest OS에 로그인 계정 확인 (예: Administrator / P@ssw0rd)
4. 테스트 전 기준이 되는 "깨끗한 상태" 스냅샷 생성 (예: "Clean")
```

#### 3단계: 프로그램 시작

```
1. AutoRegressionVM.exe 실행
2. 좌측 상단 "연결" 버튼 클릭 → VMware에 연결
3. "VM 추가" 버튼으로 테스트 대상 VM 등록
   - VM 이름, VMX 경로, Guest 계정/비밀번호 입력
4. Ctrl+N으로 새 시나리오 생성
```

---

### B. 활용 사례

#### 케이스 1: 단일 VM 설치 테스트

> **상황**: 신규 빌드된 설치 파일(setup.exe)이 정상 설치되는지 확인

```json
{
  "Name": "설치 테스트 - v2.5.0",
  "MaxParallelVMs": 1,
  "MaxRetryCount": 1,
  "ContinueOnFailure": false,
  "Steps": [
    {
      "Name": "설치 파일 실행",
      "TargetVmxPath": "D:\\VMware\\Win10-QA\\Win10-QA.vmx",
      "SnapshotName": "Clean",
      "FilesToCopyToVM": [
        {
          "SourcePath": "D:\\Builds\\v2.5.0\\setup.exe",
          "DestinationPath": "C:\\Temp\\setup.exe"
        }
      ],
      "Execution": {
        "Type": "Program",
        "ExecutablePath": "C:\\Temp\\setup.exe",
        "Arguments": "/S /NORESTART",
        "TimeoutSeconds": 300
      },
      "SuccessCriteria": {
        "ExpectedExitCode": 0
      },
      "ForceNetworkDisconnect": false,
      "ForceSnapshotRevertAfter": true
    }
  ]
}
```

**포인트:**
- `/S /NORESTART`: 무인 설치 + 재부팅 방지
- `ForceSnapshotRevertAfter: true`: 테스트 후 VM을 깨끗한 상태로 복원
- `MaxRetryCount: 1`: 실패 시 1회 자동 재시도 (스냅샷 롤백 후)

---

#### 케이스 2: 다중 VM 병렬 회귀 테스트

> **상황**: Windows 10, Windows 11, Windows Server 2019에서 동시에 회귀 테스트 실행

```json
{
  "Name": "크로스 플랫폼 회귀 테스트",
  "MaxParallelVMs": 3,
  "MaxRetryCount": 2,
  "ContinueOnFailure": true,
  "TargetVMPaths": [
    "D:\\VMware\\Win10-QA\\Win10-QA.vmx",
    "D:\\VMware\\Win11-QA\\Win11-QA.vmx",
    "D:\\VMware\\WinSvr2019\\WinSvr2019.vmx"
  ],
  "Steps": [
    {
      "Name": "테스트 스위트 실행",
      "SnapshotName": "TestReady",
      "FilesToCopyToVM": [
        {
          "SourcePath": "D:\\Builds\\latest\\product.msi",
          "DestinationPath": "C:\\Temp\\product.msi"
        },
        {
          "SourcePath": "D:\\Tests\\regression_suite.bat",
          "DestinationPath": "C:\\Temp\\regression_suite.bat"
        }
      ],
      "Execution": {
        "Type": "Script",
        "ExecutablePath": "C:\\Temp\\regression_suite.bat",
        "TimeoutSeconds": 1800
      },
      "ResultFilesToCollect": [
        {
          "SourcePath": "C:\\Temp\\test_result.json",
          "DestinationPath": "{RESULT_DIR}\\{VM_NAME}_result.json"
        }
      ],
      "SuccessCriteria": {
        "ExpectedExitCode": 0,
        "ResultJsonPath": "$.summary.failed",
        "ExpectedJsonValue": "0"
      },
      "ForceSnapshotRevertAfter": true
    }
  ]
}
```

**포인트:**
- `MaxParallelVMs: 3`: 3개 VM이 **동시에** 실행
- `ContinueOnFailure: true`: 한 VM이 실패해도 나머지는 계속 진행
- `MaxRetryCount: 2`: 실패 시 최대 2회 재시도 (스냅샷 롤백 포함)
- `{VM_NAME}` 매크로로 VM별 결과 파일 분리
- `ResultJsonPath`: 결과 JSON에서 `$.summary.failed` 값이 `0`인지 검증

---

#### 케이스 3: 오프라인(네트워크 차단) 테스트

> **상황**: 인터넷 없이 제품이 정상 동작하는지 확인 (라이선스 서버 미연결 등)

```json
{
  "Name": "오프라인 동작 테스트",
  "Steps": [
    {
      "Name": "네트워크 차단 후 기능 테스트",
      "TargetVmxPath": "D:\\VMware\\Win10-QA\\Win10-QA.vmx",
      "SnapshotName": "ProductInstalled",
      "ForceNetworkDisconnect": true,
      "Execution": {
        "Type": "Program",
        "ExecutablePath": "C:\\Program Files\\MyProduct\\MyProduct.exe",
        "Arguments": "--self-test",
        "TimeoutSeconds": 120
      },
      "SuccessCriteria": {
        "ExpectedExitCode": 0,
        "ContainsText": "All tests passed",
        "NotContainsText": "License error"
      },
      "CaptureScreenshots": true,
      "ScreenshotIntervalSeconds": 15,
      "ForceSnapshotRevertAfter": true
    }
  ]
}
```

**포인트:**
- `ForceNetworkDisconnect: true`: 파일 복사 후 네트워크를 자동 비활성화, 결과 수집 전 재활성화
- `ContainsText` / `NotContainsText`: 출력 텍스트로 성공 판정
- `CaptureScreenshots`: 15초 간격으로 스크린샷 → 실패 시 원인 분석에 활용

---

#### 케이스 4: 설치 → 테스트 → 실패 시 로그 수집 (조건부 실행)

> **상황**: 설치 성공 시에만 테스트 진행, 실패 시 디버그 로그 수집

```json
{
  "Name": "설치 후 검증 + 실패 시 로그 수집",
  "ContinueOnFailure": true,
  "Steps": [
    {
      "Name": "Step 1: 제품 설치",
      "Order": 0,
      "TargetVmxPath": "D:\\VMware\\Win10-QA\\Win10-QA.vmx",
      "SnapshotName": "Clean",
      "FilesToCopyToVM": [
        {
          "SourcePath": "D:\\Builds\\latest\\setup.exe",
          "DestinationPath": "C:\\Temp\\setup.exe"
        }
      ],
      "Execution": {
        "Type": "Program",
        "ExecutablePath": "C:\\Temp\\setup.exe",
        "Arguments": "/S",
        "TimeoutSeconds": 300
      },
      "SuccessCriteria": { "ExpectedExitCode": 0 },
      "ForceSnapshotRevertAfter": false
    },
    {
      "Name": "Step 2: 기능 테스트 (설치 성공 시만)",
      "Order": 1,
      "Condition": {
        "Type": "PreviousStepPassed"
      },
      "Execution": {
        "Type": "Program",
        "ExecutablePath": "C:\\Program Files\\MyProduct\\test_runner.exe",
        "TimeoutSeconds": 600
      },
      "ResultFilesToCollect": [
        {
          "SourcePath": "C:\\Temp\\test_report.html",
          "DestinationPath": "{RESULT_DIR}\\test_report.html"
        }
      ],
      "SuccessCriteria": { "ExpectedExitCode": 0 },
      "ForceSnapshotRevertAfter": false
    },
    {
      "Name": "Step 3: 실패 로그 수집 (이전 스텝 실패 시만)",
      "Order": 2,
      "Condition": {
        "Type": "AnyPreviousFailed"
      },
      "Execution": {
        "Type": "Command",
        "ExecutablePath": "cmd.exe",
        "Arguments": "/c copy C:\\ProgramData\\MyProduct\\logs\\*.log C:\\Temp\\debug_logs\\",
        "TimeoutSeconds": 60
      },
      "ResultFilesToCollect": [
        {
          "SourcePath": "C:\\Temp\\debug_logs\\*",
          "DestinationPath": "{RESULT_DIR}\\debug_logs\\"
        }
      ],
      "ForceSnapshotRevertAfter": true
    }
  ]
}
```

**포인트:**
- Step 2: `PreviousStepPassed` → 설치(Step 1) 성공 시에만 실행
- Step 3: `AnyPreviousFailed` → 어떤 스텝이든 실패했으면 로그 수집
- `ForceSnapshotRevertAfter: false` → Step 1~2는 스냅샷 유지 (이어서 진행), Step 3에서 복원

---

#### 케이스 5: 파일 분배 병렬 테스트

> **상황**: 10개의 테스트 파일을 3개 VM에 분배하여 병렬 실행

```json
{
  "Name": "테스트 파일 분배 실행",
  "MaxParallelVMs": 3,
  "TargetVMPaths": [
    "D:\\VMware\\VM1\\VM1.vmx",
    "D:\\VMware\\VM2\\VM2.vmx",
    "D:\\VMware\\VM3\\VM3.vmx"
  ],
  "TestTargetFiles": [
    { "HostFilePath": "D:\\Tests\\test_01.dat", "VMDestinationPath": "C:\\Temp\\test.dat", "Description": "로그인 테스트" },
    { "HostFilePath": "D:\\Tests\\test_02.dat", "VMDestinationPath": "C:\\Temp\\test.dat", "Description": "결제 테스트" },
    { "HostFilePath": "D:\\Tests\\test_03.dat", "VMDestinationPath": "C:\\Temp\\test.dat", "Description": "검색 테스트" },
    { "HostFilePath": "D:\\Tests\\test_04.dat", "VMDestinationPath": "C:\\Temp\\test.dat", "Description": "알림 테스트" },
    { "HostFilePath": "D:\\Tests\\test_05.dat", "VMDestinationPath": "C:\\Temp\\test.dat", "Description": "설정 테스트" },
    { "HostFilePath": "D:\\Tests\\test_06.dat", "VMDestinationPath": "C:\\Temp\\test.dat", "Description": "내보내기 테스트" }
  ],
  "Steps": [
    {
      "Name": "분배된 테스트 실행",
      "SnapshotName": "TestReady",
      "Execution": {
        "Type": "Program",
        "ExecutablePath": "C:\\Tools\\test_executor.exe",
        "Arguments": "--input C:\\Temp\\test.dat --output C:\\Temp\\result.json",
        "TimeoutSeconds": 600
      },
      "ResultFilesToCollect": [
        {
          "SourcePath": "C:\\Temp\\result.json",
          "DestinationPath": "{RESULT_DIR}\\{VM_NAME}_result.json"
        }
      ],
      "SuccessCriteria": { "ExpectedExitCode": 0 }
    }
  ]
}
```

**포인트:**
- `TestTargetFiles` 6개 → 3개 VM에 2개씩 자동 분배
- 각 VM은 동일한 스텝을 실행하되, 할당된 파일만 다름
- VM1: test_01, test_02 / VM2: test_03, test_04 / VM3: test_05, test_06

---

#### 케이스 6: Pre/Post 이벤트 활용

> **상황**: 테스트 전에 빌드 서버에서 최신 바이너리 다운로드, 테스트 후 결과를 공유 폴더에 복사

```json
{
  "Name": "빌드 연동 테스트",
  "PreTestEvent": {
    "IsEnabled": true,
    "Type": "PowerShell",
    "Command": "Invoke-WebRequest -Uri 'http://build-server/latest/setup.exe' -OutFile 'D:\\Builds\\latest\\setup.exe'",
    "TimeoutSeconds": 120,
    "StopOnFailure": true,
    "HideWindow": true
  },
  "PostTestEvent": {
    "IsEnabled": true,
    "Type": "BatchFile",
    "Command": "xcopy /Y /S .\\Results\\* \\\\file-server\\QA\\Results\\",
    "RunCondition": "Always",
    "TimeoutSeconds": 60,
    "EnvironmentVariables": {
      "BUILD_VERSION": "2.5.0",
      "TESTER": "QA-Team"
    }
  },
  "Steps": [
    {
      "Name": "설치 테스트",
      "TargetVmxPath": "D:\\VMware\\Win10-QA\\Win10-QA.vmx",
      "SnapshotName": "Clean",
      "FilesToCopyToVM": [
        {
          "SourcePath": "D:\\Builds\\latest\\setup.exe",
          "DestinationPath": "C:\\Temp\\setup.exe"
        }
      ],
      "Execution": {
        "Type": "Program",
        "ExecutablePath": "C:\\Temp\\setup.exe",
        "Arguments": "/S",
        "TimeoutSeconds": 300
      },
      "SuccessCriteria": { "ExpectedExitCode": 0 }
    }
  ]
}
```

**포인트:**
- `PreTestEvent`: 테스트 시작 **전에** 빌드 서버에서 최신 파일 다운로드
  - `StopOnFailure: true`: 다운로드 실패 시 테스트 진행하지 않음
- `PostTestEvent`: 테스트 완료 **후에** 결과를 공유 폴더로 복사
  - `RunCondition: Always`: 성공/실패 관계없이 항상 실행
  - `EnvironmentVariables`: 환경 변수 주입 가능

---

#### 케이스 7: 야간 자동 회귀 테스트 (스케줄러)

> **상황**: 매일 새벽 2시에 자동으로 전체 회귀 테스트 실행

**설정 방법:**
1. GUI에서 ⏰ 스케줄러 버튼 클릭
2. 스케줄 추가:
   - **이름**: 야간 회귀 테스트
   - **시나리오**: "전체 회귀 테스트" 선택
   - **유형**: Daily
   - **실행 시각**: 02:00
3. 활성화 후 프로그램을 켜둔 상태로 유지

**Slack 알림과 함께 사용하면** 출근 시 결과를 바로 확인할 수 있습니다:

```json
{
  "Notification": {
    "Enabled": true,
    "Type": "Slack",
    "SlackWebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
    "NotifyOnComplete": true,
    "NotifyOnFailure": true,
    "NotifyOnStart": true
  }
}
```

---

#### 케이스 8: CI/CD 파이프라인 연동

> **상황**: Jenkins/GitHub Actions에서 빌드 후 자동으로 VM 테스트 실행

**Jenkins Pipeline 예시:**
```groovy
pipeline {
    agent { label 'qa-machine' }
    stages {
        stage('Build') {
            steps {
                bat 'msbuild MyProduct.sln /p:Configuration=Release'
            }
        }
        stage('VM Regression Test') {
            steps {
                bat '''
                    AutoRegressionVM.exe --scenario "회귀 테스트" ^
                        --parallel 3 ^
                        --timeout 120 ^
                        --output xml ^
                        --report "%WORKSPACE%\\test-results.xml" ^
                        --verbose
                '''
            }
            post {
                always {
                    junit 'test-results.xml'
                }
            }
        }
    }
}
```

**GitHub Actions 예시:**
```yaml
jobs:
  vm-test:
    runs-on: self-hosted  # QA PC에 self-hosted runner 설치 필요
    steps:
      - name: Run VM Regression
        run: |
          AutoRegressionVM.exe --scenario "스모크 테스트" `
            --parallel 2 --timeout 60 --output xml `
            --report "results.xml"
        shell: pwsh

      - name: Publish Results
        uses: dorny/test-reporter@v1
        if: always()
        with:
          name: VM Test Results
          path: results.xml
          reporter: java-junit
```

**Exit Code 활용:**
```bash
AutoRegressionVM.exe --scenario "스모크 테스트" --timeout 30
EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo "모든 테스트 통과 → 배포 진행"
elif [ $EXIT_CODE -eq 1 ]; then
    echo "일부 테스트 실패 → 배포 중단"
elif [ $EXIT_CODE -eq 5 ]; then
    echo "타임아웃 → 재시도 필요"
fi
```

---

#### 케이스 9: 장시간 실행 + 스크린샷 모니터링

> **상황**: 업데이트 설치에 30분 이상 소요, 진행 상황을 스크린샷으로 기록

```json
{
  "Name": "대규모 업데이트 테스트",
  "Steps": [
    {
      "Name": "업데이트 실행 및 모니터링",
      "TargetVmxPath": "D:\\VMware\\Win10-QA\\Win10-QA.vmx",
      "SnapshotName": "v2.4_Installed",
      "FilesToCopyToVM": [
        {
          "SourcePath": "D:\\Builds\\v2.5.0\\update_patch.exe",
          "DestinationPath": "C:\\Temp\\update_patch.exe"
        }
      ],
      "Execution": {
        "Type": "Program",
        "ExecutablePath": "C:\\Temp\\update_patch.exe",
        "Arguments": "/S /UPDATE",
        "TimeoutSeconds": 3600
      },
      "WaitAfterExecution": {
        "Minutes": 5,
        "Seconds": 0
      },
      "CaptureScreenshots": true,
      "ScreenshotIntervalSeconds": 30,
      "SuccessCriteria": {
        "ExpectedExitCode": 0
      },
      "ForceSnapshotRevertAfter": true
    }
  ]
}
```

**포인트:**
- `TimeoutSeconds: 3600`: 최대 1시간 대기
- `WaitAfterExecution`: 실행 후 추가 5분 대기 (재부팅 등 후처리 고려)
- `CaptureScreenshots` + `ScreenshotIntervalSeconds: 30`: 30초 간격으로 스크린샷
- 스크린샷은 결과 폴더에 자동 저장 → 실패 시 어느 단계에서 멈췄는지 확인 가능

---

#### 케이스 10: 매크로 활용 — 날짜별 결과 정리

> **상황**: 매일 실행되는 테스트 결과를 날짜/VM별로 자동 분류

```json
{
  "Name": "일일 스모크 테스트",
  "Steps": [
    {
      "Name": "스모크 테스트 실행",
      "TargetVmxPath": "D:\\VMware\\Win10-QA\\Win10-QA.vmx",
      "SnapshotName": "ProductInstalled",
      "Execution": {
        "Type": "Script",
        "ExecutablePath": "C:\\Tests\\smoke_test.bat",
        "Arguments": "{SCENARIO_NAME} {DATETIME}",
        "TimeoutSeconds": 300
      },
      "ResultFilesToCollect": [
        {
          "SourcePath": "C:\\Temp\\smoke_result.json",
          "DestinationPath": "D:\\QA_Results\\{DATE}\\{VM_NAME}\\smoke_result_{TIME}.json"
        },
        {
          "SourcePath": "C:\\Temp\\smoke_log.txt",
          "DestinationPath": "D:\\QA_Results\\{DATE}\\{VM_NAME}\\smoke_log_{TIME}.txt"
        }
      ],
      "SuccessCriteria": {
        "ExpectedExitCode": 0,
        "ContainsText": "SMOKE TEST PASSED"
      }
    }
  ]
}
```

**결과 폴더 구조 예시:**
```
D:\QA_Results\
├── 20260312\
│   └── Win10-QA\
│       ├── smoke_result_020015.json
│       └── smoke_log_020015.txt
├── 20260313\
│   └── Win10-QA\
│       ├── smoke_result_020012.json
│       └── smoke_log_020012.txt
```

---

### C. 자주 묻는 질문 (FAQ)

**Q: VM이 부팅되지 않아 테스트가 실패합니다.**
> VMware Tools가 Guest OS에 설치되어 있는지 확인하세요. 스냅샷 복원 후 VMware Tools가 "running" 상태가 되어야 파일 복사 및 프로그램 실행이 가능합니다. 기본 대기 시간은 300초입니다.

**Q: 병렬 실행 시 하나의 VM이 실패하면 나머지도 멈추나요?**
> 아닙니다. 각 VM은 독립적으로 실행됩니다. 한 VM이 실패해도 다른 VM은 계속 진행합니다. `ContinueOnFailure: true`이면 실패한 VM의 나머지 스텝도 계속 실행합니다.

**Q: 실패한 TC는 어떻게 재시도되나요?**
> `MaxRetryCount` 설정값만큼 자동 재시도합니다. 재시도 시 스냅샷을 롤백하여 깨끗한 상태에서 처음부터 다시 실행합니다. 재시도 사이에 3초 대기합니다.

**Q: Guest 비밀번호는 안전한가요?**
> 비밀번호는 Windows DPAPI (CurrentUser scope)로 암호화되어 settings.json에 저장됩니다. 같은 Windows 계정에서만 복호화할 수 있으므로, 다른 사용자가 파일을 복사해도 비밀번호를 알 수 없습니다.

**Q: 테스트 결과를 팀과 공유하려면?**
> 세 가지 방법이 있습니다:
> 1. **알림**: Slack/Teams/Email로 실시간 결과 수신
> 2. **리포트**: HTML 리포트 생성 후 공유 폴더에 저장
> 3. **PostTestEvent**: 테스트 완료 후 결과를 자동으로 공유 폴더에 복사

**Q: CLI에서 특정 VM만 테스트하고 싶습니다.**
> `--vm` 옵션을 사용하세요:
> ```bash
> AutoRegressionVM.exe --scenario "회귀 테스트" --vm "Win10-QA"
> ```

**Q: settings.json을 다른 PC로 옮기면 비밀번호가 깨집니다.**
> DPAPI는 Windows 사용자 계정에 종속됩니다. 다른 PC에서는 VM을 다시 등록하고 비밀번호를 재입력해야 합니다. 기존 평문 비밀번호가 있으면 자동으로 암호화 마이그레이션됩니다.

---

### D. 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| "VMware 연결 실패" | vmrun.exe 경로 불일치 | settings.json의 `VMwareInstallPath` 확인 |
| "스냅샷 복원 실패" | 스냅샷 이름 불일치 | VM의 스냅샷 이름과 시나리오 설정 비교 |
| "파일 복사 실패" | VMware Tools 미설치/미실행 | Guest OS에서 VMware Tools 상태 확인 |
| "프로그램 실행 타임아웃" | TimeoutSeconds 부족 | 스텝의 `TimeoutSeconds` 값 증가 |
| "Guest 로그인 실패" | 자격 증명 불일치 | VM 설정에서 계정/비밀번호 재확인 |
| "네트워크 차단 후 복원 안됨" | wmic 명령 권한 부족 | Guest OS에서 관리자 계정 사용 |
| CLI에서 출력 없음 | Console 연결 실패 | 명령 프롬프트에서 직접 실행 (PowerShell ISE 제외) |

## 라이선스

MIT License
