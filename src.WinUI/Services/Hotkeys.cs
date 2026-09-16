using SnipIt.Models;
using SnipIt.Utils;
namespace SnipIt.Services;
[Flags] public enum ModifierKeys { None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8 }
internal sealed class Hotkeys : System.Windows.Forms.NativeWindow, IDisposable
{
    private readonly Dictionary<int, Action> actions = new();
    public Hotkeys() { CreateHandle(new System.Windows.Forms.CreateParams { Caption = "SnipIt WinUI Hotkeys", Parent = new nint(-3) }); }
    public void Register(AppSettingsConfig config)
    {
        foreach (var id in actions.Keys) NativeMethods.UnregisterHotKey(Handle, id); actions.Clear();
        var entries = new[] { (config.FullScreenHotkey, (Action)(() => App.Capture("Full"))), (config.ActiveWindowHotkey, (Action)(() => App.Capture("Window"))), (config.RegionHotkey, (Action)(() => App.Capture("Region"))), (config.GifHotkey, (Action)(() => App.Capture("Gif"))), (new HotkeyConfig(ModifierKeys.Control | ModifierKeys.Shift, System.Windows.Forms.Keys.E), (Action)App.EditRecent) };
        for (int i = 0; i < entries.Length; i++) { var (key, action) = entries[i]; if (NativeMethods.RegisterHotKey(Handle, 9000 + i, (uint)key.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)key.Key)) actions[9000 + i] = action; else App.Notify(Ui.L("단축키 등록 실패: ") + key); }
    }
    protected override void WndProc(ref System.Windows.Forms.Message m) { if (m.Msg == NativeMethods.WM_HOTKEY && actions.TryGetValue((int)m.WParam, out var action)) App.Queue.TryEnqueue(() => { if (!App.Busy) action(); }); base.WndProc(ref m); }
    public void Dispose() { foreach (var id in actions.Keys) NativeMethods.UnregisterHotKey(Handle, id); actions.Clear(); DestroyHandle(); }
}
