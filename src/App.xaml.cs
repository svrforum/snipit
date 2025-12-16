using SnipIt.Models;
using SnipIt.Services;

namespace SnipIt;

public partial class App : System.Windows.Application
{
    private HotkeyService? _hotkeyService;
    private TrayIconService? _trayIconService;
    private static Views.EditorWindow? _currentEditor;
    private static bool _isCapturing = false;
    private static System.Drawing.Bitmap? _lastSilentCapture;

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

        // Register Ctrl+Shift+E for opening last capture in editor (silent mode)
        _hotkeyService?.RegisterHotkey("OpenEditor",
            ModifierKeys.Control | ModifierKeys.Shift,
            System.Windows.Forms.Keys.E,
            () => OpenLastSilentCapture());

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

        // Check if silent mode is enabled
        var config = AppSettingsConfig.Instance;
        if (config.SilentMode)
        {
            // Store the bitmap for later editing
            _lastSilentCapture?.Dispose();
            _lastSilentCapture = (System.Drawing.Bitmap)bitmap.Clone();

            // Just copy to clipboard and show notification with click handler
            CopyBitmapToClipboard(bitmap);
            TrayIconService.Instance.ShowNotificationWithAction(
                "캡처 완료",
                "E: 편집 | S: 저장 | 클릭: 편집",
                3000,
                OpenLastSilentCapture,
                SaveLastSilentCapture);
            return;
        }

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

    private static void CopyBitmapToClipboard(System.Drawing.Bitmap bitmap)
    {
        try
        {
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bitmap.GetHbitmap();
                var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze();
                System.Windows.Clipboard.SetImage(bitmapSource);
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
            }
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public static void OpenLastSilentCapture()
    {
        if (_lastSilentCapture != null)
        {
            // Close existing editor window
            if (_currentEditor != null)
            {
                _currentEditor.Close();
                _currentEditor = null;
            }

            // Open editor with the last captured image
            _currentEditor = new Views.EditorWindow((System.Drawing.Bitmap)_lastSilentCapture.Clone());
            _currentEditor.Closed += (s, e) => _currentEditor = null;
            _currentEditor.Show();
        }
        else
        {
            // Try to open the most recent capture from history
            var history = CaptureHistoryService.Instance.History;
            if (history.Count > 0)
            {
                var latestCapture = CaptureHistoryService.Instance.LoadImage(history[0]);
                if (latestCapture != null)
                {
                    if (_currentEditor != null)
                    {
                        _currentEditor.Close();
                        _currentEditor = null;
                    }

                    _currentEditor = new Views.EditorWindow(latestCapture);
                    _currentEditor.Closed += (s, e) => _currentEditor = null;
                    _currentEditor.Show();
                }
            }
        }
    }

    public static void SaveLastSilentCapture()
    {
        if (_lastSilentCapture == null)
        {
            TrayIconService.Instance.ShowNotification("SnipIt", "저장할 캡처가 없습니다", 2000);
            return;
        }

        try
        {
            var config = AppSettingsConfig.Instance;
            var format = config.DefaultFormat.ToLower();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultFilename = $"SnipIt_{timestamp}.{format}";
            string fullPath;

            if (config.SilentModeAutoSave)
            {
                // Auto save without dialog
                fullPath = System.IO.Path.Combine(config.SavePath, defaultFilename);
            }
            else
            {
                // Show save dialog
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "캡처 저장",
                    FileName = defaultFilename,
                    InitialDirectory = config.SavePath,
                    Filter = "PNG 이미지|*.png|JPEG 이미지|*.jpg|BMP 이미지|*.bmp|GIF 이미지|*.gif",
                    FilterIndex = format switch
                    {
                        "jpg" or "jpeg" => 2,
                        "bmp" => 3,
                        "gif" => 4,
                        _ => 1
                    }
                };

                if (saveDialog.ShowDialog() != true)
                {
                    return; // User cancelled
                }

                fullPath = saveDialog.FileName;
                format = System.IO.Path.GetExtension(fullPath).TrimStart('.').ToLower();
            }

            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            // Save with appropriate format
            System.Drawing.Imaging.ImageFormat imageFormat = format switch
            {
                "jpg" or "jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                "bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                "gif" => System.Drawing.Imaging.ImageFormat.Gif,
                _ => System.Drawing.Imaging.ImageFormat.Png
            };

            _lastSilentCapture.Save(fullPath, imageFormat);
            TrayIconService.Instance.ShowNotification("저장 완료", System.IO.Path.GetFileName(fullPath), 2000);
        }
        catch (Exception ex)
        {
            TrayIconService.Instance.ShowNotification("저장 실패", ex.Message, 3000);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        base.OnExit(e);
    }
}
