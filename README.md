# AutoRegressionVM

VMware 가상 머신을 활용한 자동화된 회귀 테스트 도구입니다. GUI와 CLI 모드를 모두 지원하며, 스냅샷 기반의 반복 테스트 환경을 제공합니다.

## 주요 기능

- **VM 관리**: VMware Workstation/Player VM 등록 및 관리
- **스냅샷 기반 테스트**: 테스트 전 스냅샷으로 자동 복원하여 일관된 테스트 환경 보장
- **시나리오 기반 실행**: 여러 테스트 스텝을 시나리오로 구성하여 순차/병렬 실행
- **파일 전송**: 호스트 ↔ VM 간 파일 복사 지원
- **결과 수집**: 테스트 결과 파일 자동 수집
- **알림 지원**: Email, Slack, Teams 알림 연동
- **CLI 지원**: CI/CD 파이프라인 연동을 위한 명령줄 인터페이스

## 요구 사항

- Windows 10/11
- .NET Framework 4.7.2 이상
- VMware Workstation 또는 VMware Player
- VMware VIX SDK

## 설치

1. 저장소를 클론합니다.
2. Visual Studio에서 솔루션을 열고 빌드합니다.
3. VMware VIX SDK가 설치되어 있는지 확인합니다.

## 사용법

### GUI 모드

```bash
AutoRegressionVM.exe
```

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

# JSON 형식 출력
AutoRegressionVM.exe --scenario "시나리오명" --output json

# 결과 리포트 저장
AutoRegressionVM.exe --scenario "시나리오명" --report "C:\Reports\result.json"

# 드라이런 (실제 실행 없이 검증)
AutoRegressionVM.exe --scenario "시나리오명" --dry-run

# 상세 로그 출력
AutoRegressionVM.exe --scenario "시나리오명" --verbose
```

## 프로젝트 구조

```
AutoRegressionVM/
├── CLI/                    # 명령줄 인터페이스
│   ├── CliOptions.cs
│   ├── CliRunner.cs
│   └── CommandLineParser.cs
├── Helpers/                # 유틸리티 클래스
│   ├── RelayCommand.cs
│   ├── SimpleJson.cs
│   └── ViewModelBase.cs
├── Models/                 # 데이터 모델
│   ├── AppSettings.cs
│   ├── Snapshot.cs
│   ├── TestResult.cs
│   ├── TestScenario.cs
│   ├── TestStep.cs
│   └── VMInfo.cs
├── Services/               # 서비스 계층
│   ├── Notification/       # 알림 서비스
│   ├── TestExecution/      # 테스트 실행 엔진
│   └── VMware/             # VMware VIX 연동
├── ViewModels/             # MVVM ViewModel
│   └── MainViewModel.cs
├── Views/                  # WPF 다이얼로그
│   ├── AddVMDialog.xaml
│   └── ScenarioEditorDialog.xaml
├── App.xaml
└── MainWindow.xaml
```

## 테스트 시나리오 구성

시나리오는 여러 테스트 스텝으로 구성됩니다. 각 스텝에서는:

1. **스냅샷 복원**: 지정된 스냅샷으로 VM 상태 복원
2. **파일 복사**: 테스트에 필요한 파일을 VM으로 전송
3. **테스트 실행**: VM 내에서 프로그램/스크립트 실행
4. **결과 수집**: VM에서 결과 파일을 호스트로 복사
5. **정리**: 스냅샷으로 VM 상태 복원 (선택)

## 알림 설정

`appsettings.json`에서 알림 설정을 구성할 수 있습니다:

- **Email**: SMTP 서버 설정
- **Slack**: Webhook URL 설정
- **Teams**: Incoming Webhook URL 설정

## 라이선스

MIT License
