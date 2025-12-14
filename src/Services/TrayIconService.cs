using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace SnipIt.Services;

public class TrayIconService : IDisposable
{
    private static TrayIconService? _instance;
    public static TrayIconService Instance => _instance ??= new TrayIconService();

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private IntPtr _iconHandle = IntPtr.Zero;

    // Low-level keyboard hook for notification E key
    private IntPtr _keyboardHookHandle = IntPtr.Zero;
    private LowLevelKeyboardProc? _keyboardProc;
    private System.Threading.Timer? _notificationTimer;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private TrayIconService()
    {
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _contextMenu = new ContextMenuStrip();
        var loc = LocalizationService.Instance;

        // Capture menu items
        var captureMenu = new ToolStripMenuItem("캡쳐");
        captureMenu.DropDownItems.Add($"{loc["FullScreenCapture"]}\tPrtSc", null, (s, e) => App.CaptureFullScreen());
        captureMenu.DropDownItems.Add($"{loc["ActiveWindowCapture"]}\tAlt+PrtSc", null, (s, e) => App.CaptureActiveWindow());
        captureMenu.DropDownItems.Add($"{loc["RegionCapture"]}\tCtrl+Shift+A", null, (s, e) => App.CaptureRegion());

        _contextMenu.Items.Add(captureMenu);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // Hotkey re-register
        _contextMenu.Items.Add("🔄 핫키 재등록", null, (s, e) => ReregisterHotkeys());

        // Settings
        _contextMenu.Items.Add(loc["Settings"], null, (s, e) => ShowSettings());
        _contextMenu.Items.Add(new ToolStripSeparator());

        // Exit
        _contextMenu.Items.Add(loc["Exit"], null, (s, e) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "SnipIt - 화면 캡쳐 도구",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
    }

    private Icon CreateDefaultIcon()
    {
        // Try to load icon from WPF resource
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/icon.ico", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                var icon = new Icon(streamInfo.Stream, 32, 32);
                System.Diagnostics.Debug.WriteLine("[TrayIcon] Loaded icon from resources");
                return icon;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TrayIcon] Failed to load icon from resources: {ex.Message}");
        }

        // Fallback: Create a simple icon programmatically
        System.Diagnostics.Debug.WriteLine("[TrayIcon] Creating fallback icon");
        var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw circle background
            using var brush = new SolidBrush(Color.FromArgb(49, 130, 246)); // Primary blue
            g.FillEllipse(brush, 2, 2, 28, 28);

            // Draw scissors icon
            using var pen = new Pen(Color.White, 2.5f);
            g.DrawLine(pen, 10, 10, 22, 22);
            g.DrawLine(pen, 22, 10, 10, 22);

            using var whiteBrush = new SolidBrush(Color.White);
            g.FillEllipse(whiteBrush, 8, 8, 6, 6);
            g.FillEllipse(whiteBrush, 18, 18, 6, 6);
        }

        // Clean up previous icon handle if exists
        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
        }

        _iconHandle = bitmap.GetHicon();
        bitmap.Dispose();
        return Icon.FromHandle(_iconHandle);
    }

    private void ShowMainWindow()
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }
    }

    private void ShowSettings()
    {
        var settingsWindow = new Views.SettingsWindow();
        settingsWindow.ShowDialog();
    }

    private void ReregisterHotkeys()
    {
        int count = HotkeyService.Instance.ReregisterAllHotkeys();
        ShowNotification("SnipIt", $"핫키 {count}개 재등록 완료", 2000);
    }

    private void ExitApplication()
    {
        Dispose();
        Application.Current.Shutdown();
    }

    private Action? _pendingBalloonAction;

    public void ShowNotification(string title, string message, int timeout = 2000)
    {
        _pendingBalloonAction = null;
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var toast = new Views.ToastNotification(title, message, timeout);
            toast.Show();
        });
    }

    public void ShowNotificationWithAction(string title, string message, int timeout, Action onClick)
    {
        _pendingBalloonAction = onClick;

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var toast = new Views.ToastNotification(title, message, timeout, () =>
            {
                RemoveKeyboardHook();
                onClick?.Invoke();
            });
            toast.Show();
        });

        // Install keyboard hook for E key
        InstallKeyboardHook();

        // Set timer to remove hook after timeout
        _notificationTimer?.Dispose();
        _notificationTimer = new System.Threading.Timer(_ =>
        {
            Application.Current?.Dispatcher.BeginInvoke(() => RemoveKeyboardHook());
        }, null, timeout + 500, System.Threading.Timeout.Infinite);
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        RemoveKeyboardHook();
        _pendingBalloonAction?.Invoke();
        _pendingBalloonAction = null;
    }

    private void OnBalloonTipClosed(object? sender, EventArgs e)
    {
        RemoveKeyboardHook();
        _pendingBalloonAction = null;
    }

    private void InstallKeyboardHook()
    {
        if (_keyboardHookHandle != IntPtr.Zero) return;

        _keyboardProc = KeyboardHookCallback;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        if (curModule != null)
        {
            _keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc,
                GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private void RemoveKeyboardHook()
    {
        _notificationTimer?.Dispose();
        _notificationTimer = null;

        if (_keyboardHookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }
        _keyboardProc = null;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            // E key = 0x45 (69)
            if (vkCode == 0x45 && _pendingBalloonAction != null)
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    RemoveKeyboardHook();
                    _pendingBalloonAction?.Invoke();
                    _pendingBalloonAction = null;
                });
                // Don't consume the key - let it pass through
            }
        }
        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        RemoveKeyboardHook();
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();

        // Clean up GDI icon handle
        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        _instance = null;
    }
}

// Extension method for rounded rectangles
public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int width, int height, int radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
        path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
        path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
