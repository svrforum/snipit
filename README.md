<p align="center">
  <img src="snipit_logo.png" alt="Snipit Logo" width="100%">
</p>

# Snipit

<p>가볍고 포터블한 Windows 화면 캡쳐 도구</p>
<img width="386" height="366" alt="image" src="https://github.com/user-attachments/assets/0c6db9ac-c7c8-4c29-bd2a-0a1ac94110ff" />
<img width="1134" height="689" alt="image" src="https://github.com/user-attachments/assets/391c2d53-9adf-4a7c-a564-0064688912c0" />

## 기능

### 화면 캡쳐
- **전체 화면 캡쳐** - `PrintScreen`
- **활성 창 캡쳐** - `Alt + PrintScreen`
- **영역 선택 캡쳐** - `Ctrl + Shift + C`
- **GIF 녹화** - `Ctrl + Shift + G` (15/30/60fps 설정 가능)
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

### 글로벌 (어디서든)
| 동작 | 단축키 |
|------|--------|
| 전체 화면 캡쳐 | `PrintScreen` |
| 활성 창 캡쳐 | `Alt + PrintScreen` |
| 영역 선택 캡쳐 | `Ctrl + Shift + C` |
| GIF 녹화 | `Ctrl + Shift + G` |

### 편집기 - 파일/편집
| 동작 | 단축키 |
|------|--------|
| 저장 | `Ctrl + S` |
| 클립보드 복사 | `Ctrl + C` |
| 실행 취소 | `Ctrl + Z` |
| 다시 실행 | `Ctrl + Y` |
| 줌 인/아웃 | `Ctrl + 마우스휠` |

### 편집기 - 도구 선택
| 도구 | 단축키 |
|------|--------|
| 선택 | `V` |
| 펜 | `P` |
| 화살표 | `A` |
| 직선 | `L` |
| 사각형 | `R` |
| 타원 | `E` |
| 텍스트 | `T` |
| 형광펜 | `H` |
| 모자이크 | `M` |
| 자르기 | `C` |
| 취소/선택 해제 | `Esc` |

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
│   │   ├── GifRecorderService.cs # GIF 녹화 서비스
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
- AnimatedGif (GIF 녹화)
- Windows 11 DWM API (라운드 코너, Mica 효과)

## 보안

### VirusTotal 검증
- [최신 검증 결과](https://www.virustotal.com/gui/file/d475941779a624d962c46dd35f3d490b04b81f74b595955bbde2db8c6634442b/detection)
- 1/71 탐지 (Bkav Pro AI 오탐 - Self-contained .NET 앱 특성)
- 소스코드 100% 공개 - 직접 빌드 가능

### 왜 일부 백신에서 오탐이 발생하나요?
Self-contained .NET 앱은 다음 특성으로 인해 AI 기반 백신에서 오탐될 수 있습니다:
- 대용량 단일 실행파일 (런타임 포함)
- 화면 캡쳐, 글로벌 핫키 등 시스템 API 사용
- 클립보드 접근

이는 악성코드와 무관하며, 70개 이상의 주요 백신에서 정상 판정을 받았습니다.

## 기여하기

1. Fork 후 새 브랜치 생성
2. 변경사항 커밋
3. Pull Request 생성

버그 리포트나 기능 제안은 Issues를 이용해주세요.

## 라이선스

MIT License - 자유롭게 사용, 수정, 배포 가능
