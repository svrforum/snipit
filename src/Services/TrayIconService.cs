using System.Drawing;
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
                return new Icon(streamInfo.Stream, 32, 32);
            }
        }
        catch { }

        // Fallback: Create a simple icon programmatically
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

        return Icon.FromHandle(bitmap.GetHicon());
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

    public void ShowNotification(string title, string message, int timeout = 2000)
    {
        _notifyIcon?.ShowBalloonTip(timeout, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
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
