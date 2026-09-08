# Windows regression checks

Install a .NET 9 SDK, then run from the repository root:

```powershell
dotnet run --project tests/SnipIt.Regression -c Release
```

The executable performs isolated capture-history, GIF lifecycle and memory-limit,
image-effect boundary, editor rendering/undo, and WPF layout checks. It uses a
temporary profile and does not clear the user's history, capture the screen,
register hotkeys, or write to the clipboard. A failed assertion returns a nonzero
exit code. An optional output-directory argument writes main-window layout PNGs.

For manual development testing alongside an existing installation, set
`SNIPIT_DATA_DIRECTORY` to an absolute temporary directory **for the launched
process only**. This isolates settings and capture history. Without that variable,
the existing AppData locations are unchanged. Global hotkeys can still conflict
with another running copy; use the buttons or configure different hotkeys.

Manual checks still needed: mixed-DPI monitors, region selection/cancellation,
GIF capture through the save dialog, OCR with installed language packs, and rapid
history switching while editing. The automated GIF limit test checks the transition
into saving without opening a native save dialog.

## Updater tests

The regression runner additionally checks numeric versions, prerelease/downgrade
filtering, allowed GitHub asset URLs, checksums, download cancellation, corrupted
files/cache metadata, HTTP failures, replacement, rollback, file locks and the
editor restart guard. All replacement tests use disposable fixtures.

To also download and hash-check the real latest release (without executing it):

```powershell
dotnet run --project tests/SnipIt.Regression -c Release -- artifacts/previews --live-update
```

After publishing the single-file app, verify the actual helper process with:

```powershell
./tests/Test-UpdateHelper.ps1 -SnipItExe ./Publish/SnipIt.exe
```

This builds two harmless console fixtures and verifies readiness, parent exit,
replacement and restart using paths with spaces and Korean characters. It does
not replace an installed SnipIt, register hotkeys or open the real app's UI.
The automatic rollback covers replacement/initial process-launch failure; it is
not a guarantee against crashes later in a new version's session.

### 성능 측정

회귀 실행기에 `--benchmark <결과.json>`을 전달하면 격리된 임시 이력으로 4K PNG 저장/재열기, 편집기 undo 스트레스, 실제 화면 캡처, 세로 이력 100장, 정적 GIF 1800프레임을 측정합니다.
`--capture-memory <결과.json>`은 720p 실제 캡처 90프레임의 저장/해제 메모리를 비교합니다.
`--live-gif <결과.json>`은 실제 화면 일부를 60초 녹화하며 10초마다 메모리/CPU를 기록한 뒤 저장 없이 폐기합니다.
실제 화면을 읽는 측정은 사용자 동의가 있는 로컬 환경에서만 실행하세요. 캡처 픽셀은 결과 파일에 포함하지 않습니다.
CPU 시간은 프로세스 누적 CPU 시간이며 전체 시스템 CPU 백분율이 아닙니다. 진단용 GC는 벤치마크에만 있습니다.
