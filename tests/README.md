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
