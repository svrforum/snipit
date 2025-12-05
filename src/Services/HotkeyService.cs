using System.Windows;
using System.Windows.Interop;
using SnipIt.Utils;

namespace SnipIt.Services;

public class HotkeyService : IDisposable
{
    private static HotkeyService? _instance;
    public static HotkeyService Instance => _instance ??= new HotkeyService();

    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private readonly Dictionary<string, int> _hotkeyNames = new();
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private int _currentId = 9000;
    private bool _isInitialized;

    private HotkeyService() { }

    public void Initialize(Window window)
    {
        if (_isInitialized) return;

        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);
        _isInitialized = true;

        // Re-register any pending hotkeys that were added before Initialize
        var config = Models.AppSettingsConfig.Instance;

        // Clear and re-register all hotkeys now that we have a window handle
        var pendingActions = _hotkeyActions.ToDictionary(x => x.Key, x => x.Value);
        var pendingNames = _hotkeyNames.ToDictionary(x => x.Key, x => x.Value);

        _hotkeyActions.Clear();
        _hotkeyNames.Clear();
        _currentId = 9000;

        // Re-register with actual Windows API
        RegisterHotkey("FullScreen", config.FullScreenHotkey.Modifiers, config.FullScreenHotkey.Key, () => App.CaptureFullScreen());
        RegisterHotkey("ActiveWindow", config.ActiveWindowHotkey.Modifiers, config.ActiveWindowHotkey.Key, () => App.CaptureActiveWindow());
        RegisterHotkey("Region", config.RegionHotkey.Modifiers, config.RegionHotkey.Key, () => App.CaptureRegion());
    }

    public bool RegisterHotkey(string name, ModifierKeys modifiers, System.Windows.Forms.Keys key, Action callback)
    {
        if (_hotkeyNames.ContainsKey(name))
        {
            UnregisterHotkey(name);
        }

        int id = _currentId++;
        uint fsModifiers = ConvertModifiers(modifiers);

        if (_isInitialized)
        {
            if (!NativeMethods.RegisterHotKey(_windowHandle, id, fsModifiers | NativeMethods.MOD_NOREPEAT, (uint)key))
            {
                return false;
            }
        }

        _hotkeyActions[id] = callback;
        _hotkeyNames[name] = id;
        return true;
    }

    public void UnregisterHotkey(string name)
    {
        if (_hotkeyNames.TryGetValue(name, out int id))
        {
            if (_isInitialized)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, id);
            }
            _hotkeyActions.Remove(id);
            _hotkeyNames.Remove(name);
        }
    }

    /// <summary>
    /// Re-registers all hotkeys. Useful when hotkeys stop working.
    /// </summary>
    public int ReregisterAllHotkeys()
    {
        if (!_isInitialized) return 0;

        int successCount = 0;
        var config = Models.AppSettingsConfig.Instance;

        // Unregister all first
        foreach (var id in _hotkeyActions.Keys.ToList())
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }
        _hotkeyActions.Clear();
        _hotkeyNames.Clear();
        _currentId = 9000;

        // Re-register with current config
        if (RegisterHotkey("FullScreen", config.FullScreenHotkey.Modifiers, config.FullScreenHotkey.Key, () => App.CaptureFullScreen()))
            successCount++;

        if (RegisterHotkey("ActiveWindow", config.ActiveWindowHotkey.Modifiers, config.ActiveWindowHotkey.Key, () => App.CaptureActiveWindow()))
            successCount++;

        if (RegisterHotkey("Region", config.RegionHotkey.Modifiers, config.RegionHotkey.Key, () => App.CaptureRegion()))
            successCount++;

        return successCount;
    }

    private uint ConvertModifiers(ModifierKeys modifiers)
    {
        uint result = NativeMethods.MOD_NONE;

        if (modifiers.HasFlag(ModifierKeys.Alt))
            result |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control))
            result |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift))
            result |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows))
            result |= NativeMethods.MOD_WIN;

        return result;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out Action? callback))
            {
                // Use Dispatcher.BeginInvoke to avoid blocking the message pump
                // This prevents UI freeze when showing dialogs from hotkey callbacks
                System.Windows.Application.Current.Dispatcher.BeginInvoke(callback, System.Windows.Threading.DispatcherPriority.Normal);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _hotkeyActions.Keys.ToList())
        {
            if (_isInitialized)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, id);
            }
        }
        _hotkeyActions.Clear();
        _hotkeyNames.Clear();
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
        _instance = null;
    }
}

[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}
