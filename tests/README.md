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
