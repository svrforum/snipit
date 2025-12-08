# CLAUDE.md

이 파일은 Claude Code가 이 프로젝트 작업 시 참고하는 가이드입니다.

## 빌드 명령어

```bash
# 빌드
$HOME/.dotnet/dotnet build src/WinCapture.csproj -c Release

# 배포 (단일 실행 파일)
$HOME/.dotnet/dotnet publish src/WinCapture.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../Publish
```

## 릴리즈 가이드

### GitHub 릴리즈 생성 시 주의사항
- 릴리즈 노트에 `🤖 Generated with Claude Code` 문구 포함하지 않음
- 커밋 메시지에는 포함해도 됨

### 릴리즈 노트 형식 예시
```
## SnipIt vX.X.X - 제목

### 새로운 기능
- 기능 설명

### 수정된 버그
- 버그 수정 내용

### VirusTotal 검사 결과
- X/70 탐지 (필요시 오탐 설명)

### 다운로드
- `SnipIt.exe` - Windows 10/11 64bit (별도 런타임 설치 불필요)
```

## 프로젝트 구조

- `src/` - 소스 코드
  - `Services/` - 핵심 서비스 (캡처, 히스토리, 핫키 등)
  - `Views/` - WPF 윈도우 및 XAML
  - `ViewModels/` - MVVM ViewModel
  - `Utils/` - 유틸리티 클래스
  - `Models/` - 데이터 모델
- `Publish/` - 빌드 출력 (SnipIt.exe)

## 기술 스택

- .NET 9.0 / C# 13
- WPF + Windows Forms (하이브리드)
- CommunityToolkit.Mvvm
- AnimatedGif (GIF 녹화)
