using SnipIt.Models;
using SnipIt.Services;

namespace SnipIt;

public partial class App : System.Windows.Application
{
    private HotkeyService? _hotkeyService;
    private TrayIconService? _trayIconService;
    private static Views.EditorWindow? _currentEditor;
    private static bool _isCapturing = false;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load saved settings
        var config = AppSettingsConfig.Instance;
        config.ApplyToAppSettings();

        // Apply language setting
        LocalizationService.Instance.CurrentLanguage = config.Language;

        // Initialize services
        _hotkeyService = HotkeyService.Instance;
        _trayIconService = TrayIconService.Instance;

        // Create a hidden window for hotkey registration
        // This ensures hotkeys work immediately, regardless of MainWindow state
        var hotkeyWindow = new System.Windows.Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = System.Windows.WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = System.Windows.Visibility.Hidden
        };
        hotkeyWindow.Show();
        hotkeyWindow.Hide();

        // Initialize hotkey service with the hidden window
        _hotkeyService.Initialize(hotkeyWindow);

        // Register hotkeys from saved config
        RegisterHotkeysFromConfig(config);
    }

    private void RegisterHotkeysFromConfig(AppSettingsConfig config)
    {
        var failedHotkeys = new List<string>();

        if (!(_hotkeyService?.RegisterHotkey("FullScreen",
            config.FullScreenHotkey.Modifiers,
            config.FullScreenHotkey.Key,
            () => CaptureFullScreen()) ?? false))
        {
            failedHotkeys.Add($"전체 화면 캡처: {config.FullScreenHotkey}");
        }

        if (!(_hotkeyService?.RegisterHotkey("ActiveWindow",
            config.ActiveWindowHotkey.Modifiers,
            config.ActiveWindowHotkey.Key,
            () => CaptureActiveWindow()) ?? false))
        {
            failedHotkeys.Add($"활성 창 캡처: {config.ActiveWindowHotkey}");
        }

        if (!(_hotkeyService?.RegisterHotkey("Region",
            config.RegionHotkey.Modifiers,
            config.RegionHotkey.Key,
            () => CaptureRegion()) ?? false))
        {
            failedHotkeys.Add($"영역 캡처: {config.RegionHotkey}");
        }

        if (!(_hotkeyService?.RegisterHotkey("GifRecord",
            config.GifHotkey.Modifiers,
            config.GifHotkey.Key,
            () => CaptureGif()) ?? false))
        {
            failedHotkeys.Add($"GIF 녹화: {config.GifHotkey}");
        }

        // Show warning if any hotkeys failed to register
        if (failedHotkeys.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Failed to register hotkeys: {string.Join(", ", failedHotkeys)}");
        }
    }

    public static void CaptureFullScreen()
    {
        if (_isCapturing) return;
        _isCapturing = true;

        try
        {
            HideEditorAndWait();

            var bitmap = ScreenCaptureService.CaptureFullScreen();
            if (bitmap != null)
            {
                OpenEditor(bitmap);
            }
        }
        finally
        {
            _isCapturing = false;
        }
    }

    public static void CaptureActiveWindow()
    {
        if (_isCapturing) return;
        _isCapturing = true;

        try
        {
            HideEditorAndWait();

            var bitmap = ScreenCaptureService.CaptureActiveWindow();
            if (bitmap != null)
            {
                OpenEditor(bitmap);
            }
        }
        finally
        {
            _isCapturing = false;
        }
    }

    public static void CaptureRegion()
    {
        if (_isCapturing) return;
        _isCapturing = true;

        try
        {
            HideEditorAndWait();

            var overlay = new Views.CaptureOverlay();
            overlay.ShowDialog();

            if (overlay.CapturedImage != null)
            {
                OpenEditor(overlay.CapturedImage);
            }
        }
        finally
        {
            _isCapturing = false;
        }
    }

    public static void CaptureGif()
    {
        if (_isCapturing) return;
        _isCapturing = true;

        try
        {
            HideEditorAndWait();

            var overlay = new Views.GifRecordingOverlay();
            overlay.ShowDialog();
        }
        finally
        {
            _isCapturing = false;
        }
    }

    private static double _savedLeft, _savedTop;

    private static void HideEditorAndWait()
    {
        if (_currentEditor != null && _currentEditor.IsVisible)
        {
            // Save position
            _savedLeft = _currentEditor.Left;
            _savedTop = _currentEditor.Top;

            // Hide the window
            _currentEditor.Hide();

            // Force UI update and wait for window to be fully hidden
            _currentEditor.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // Wait for the window to be completely hidden from screen
            System.Threading.Thread.Sleep(150);

            // Restore position for next show
            _currentEditor.Left = _savedLeft;
            _currentEditor.Top = _savedTop;
        }
    }

    private static void OpenEditor(System.Drawing.Bitmap bitmap)
    {
        // Save to history
        CaptureHistoryService.Instance.AddCapture(bitmap);

        // Close existing editor window
        if (_currentEditor != null)
        {
            _currentEditor.Close();
            _currentEditor = null;
        }

        // Open new editor
        _currentEditor = new Views.EditorWindow(bitmap);
        _currentEditor.Closed += (s, e) => _currentEditor = null;
        _currentEditor.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        base.OnExit(e);
    }
}
