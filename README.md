# WinCapture

가볍고 포터블한 Windows 화면 캡쳐 도구

## 기능

### 화면 캡쳐
- **전체 화면 캡쳐** - `PrintScreen`
- **활성 창 캡쳐** - `Alt + PrintScreen`
- **영역 선택 캡쳐** - `Ctrl + Shift + A`
- 다중 모니터 지원
- 커서 포함 옵션

### 이미지 편집기
- 펜/자유 그리기
- 화살표
- 직선
- 사각형
- 원/타원
- 텍스트 삽입
- 하이라이트 (형광펜)
- 모자이크/블러 처리
- 자르기
- 실행 취소/다시 실행

### 저장 및 공유
- PNG, JPG, BMP, GIF 형식 지원
- 클립보드 복사
- 파일 저장

### 시스템 트레이
- 최소화 시 시스템 트레이로 이동
- 트레이 아이콘에서 빠른 캡쳐 접근
- 글로벌 단축키 지원

## 요구 사항

- Windows 10/11 (Windows 11 권장 - 라운드 코너, Mica 효과 지원)
- .NET 9 Runtime (Self-contained 버전은 불필요)

## 빌드 방법

### 필수 조건
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) 설치

### 빌드
```bash
# PowerShell
.\build.ps1

# 또는 Command Prompt
build.bat
```

### 수동 빌드
```bash
cd src

# 개발용 빌드
dotnet build

# 포터블 EXE 생성
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

빌드 결과물: `Publish/SnipIt.exe`

## 단축키

| 동작 | 단축키 |
|------|--------|
| 전체 화면 캡쳐 | `PrintScreen` |
| 활성 창 캡쳐 | `Alt + PrintScreen` |
| 영역 선택 캡쳐 | `Ctrl + Shift + A` |
| 저장 (편집기) | `Ctrl + S` |
| 클립보드 복사 (편집기) | `Ctrl + C` |
| 실행 취소 (편집기) | `Ctrl + Z` |
| 다시 실행 (편집기) | `Ctrl + Y` |

## 프로젝트 구조

```
WinCapture/
├── src/
│   ├── App.xaml              # 앱 진입점
│   ├── Views/
│   │   ├── MainWindow.xaml   # 메인 윈도우
│   │   ├── CaptureOverlay.xaml # 영역 선택 오버레이
│   │   ├── EditorWindow.xaml # 이미지 편집기
│   │   └── SettingsWindow.xaml # 설정 창
│   ├── ViewModels/
│   │   └── HistoryItemViewModel.cs # MVVM 뷰모델
│   ├── Services/
│   │   ├── ScreenCaptureService.cs # 화면 캡쳐 로직
│   │   ├── CaptureHistoryService.cs # 캡쳐 히스토리 관리
│   │   ├── HotkeyService.cs  # 글로벌 단축키
│   │   ├── LocalizationService.cs # 다국어 지원
│   │   └── TrayIconService.cs # 시스템 트레이
│   ├── Utils/
│   │   ├── NativeMethods.cs  # Win32 API + Windows 11 DWM
│   │   └── ImageProcessingHelper.cs # 고성능 이미지 처리
│   └── Models/
│       ├── AppSettingsConfig.cs # 앱 설정
│       └── HotkeyConfig.cs   # 단축키 설정
├── build.bat                 # Windows 빌드 스크립트
├── build.ps1                 # PowerShell 빌드 스크립트
├── LICENSE                   # MIT 라이선스
├── .gitignore
└── README.md
```

## 기술 스택

- .NET 9 / C# 13
- WPF (Windows Presentation Foundation)
- CommunityToolkit.Mvvm (MVVM 패턴)
- Windows 11 DWM API (라운드 코너, Mica 효과)

## 기여하기

1. Fork 후 새 브랜치 생성
2. 변경사항 커밋
3. Pull Request 생성

버그 리포트나 기능 제안은 Issues를 이용해주세요.

## 라이선스

MIT License - 자유롭게 사용, 수정, 배포 가능
