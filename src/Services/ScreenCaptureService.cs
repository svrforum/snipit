using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SnipIt.Utils;

namespace SnipIt.Services;

public static class ScreenCaptureService
{
    // DPI awareness context values
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>
    /// Gets the DPI scale factor for a specific screen location
    /// </summary>
    public static double GetDpiScaleForPoint(int x, int y)
    {
        try
        {
            var point = new POINT { X = x, Y = y };
            var monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _);
                return dpiX / 96.0;
            }
        }
        catch { }
        return 1.0;
    }

    /// <summary>
    /// Captures the entire screen (all monitors) with proper DPI handling
    /// </summary>
    public static Bitmap CaptureFullScreen()
    {
        // Temporarily set thread to per-monitor aware for accurate screen capture
        var prevContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            var bounds = GetVirtualScreenBoundsPhysical();
            return CaptureRegion(bounds);
        }
        finally
        {
            if (prevContext != IntPtr.Zero)
                SetThreadDpiAwarenessContext(prevContext);
        }
    }

    /// <summary>
    /// Gets the virtual screen bounds in physical pixels (accounting for DPI)
    /// </summary>
    public static Rectangle GetVirtualScreenBoundsPhysical()
    {
        var allScreens = Screen.AllScreens;
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var screen in allScreens)
        {
            // Get the DPI scale for this monitor
            var dpiScale = GetDpiScaleForPoint(screen.Bounds.Left + 10, screen.Bounds.Top + 10);

            // Calculate physical pixel bounds
            int physLeft = (int)(screen.Bounds.Left * dpiScale);
            int physTop = (int)(screen.Bounds.Top * dpiScale);
            int physRight = (int)((screen.Bounds.Left + screen.Bounds.Width) * dpiScale);
            int physBottom = (int)((screen.Bounds.Top + screen.Bounds.Height) * dpiScale);

            minX = Math.Min(minX, screen.Bounds.Left);
            minY = Math.Min(minY, screen.Bounds.Top);
            maxX = Math.Max(maxX, screen.Bounds.Right);
            maxY = Math.Max(maxY, screen.Bounds.Bottom);
        }

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Captures the primary screen only
    /// </summary>
    public static Bitmap CapturePrimaryScreen()
    {
        var screen = Screen.PrimaryScreen!;
        return CaptureRegion(screen.Bounds);
    }

    /// <summary>
    /// Captures the currently active window
    /// </summary>
    public static Bitmap? CaptureActiveWindow()
    {
        IntPtr hWnd = NativeMethods.GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
            return null;

        return CaptureWindow(hWnd);
    }

    /// <summary>
    /// Captures a specific window by handle
    /// </summary>
    public static Bitmap? CaptureWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return null;

        // Try to get the actual window bounds using DWM
        Rectangle bounds;
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out NativeMethods.RECT rect, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) == 0)
        {
            bounds = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
        }
        else
        {
            // Fallback to GetWindowRect
            NativeMethods.GetWindowRect(hWnd, out rect);
            bounds = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        return CaptureRegion(bounds);
    }

    /// <summary>
    /// Captures a specific region of the screen
    /// </summary>
    public static Bitmap CaptureRegion(Rectangle region)
    {
        // Use 32-bit ARGB for best quality (no color loss)
        var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            // Set high quality rendering
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            graphics.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size, CopyPixelOperation.SourceCopy);

            // Optionally capture cursor
            if (AppSettings.Instance.CaptureCursor)
            {
                DrawCursor(graphics, region);
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Draws the cursor on the captured image
    /// </summary>
    private static void DrawCursor(Graphics graphics, Rectangle captureRegion)
    {
        var cursorInfo = new NativeMethods.CURSORINFO();
        cursorInfo.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(cursorInfo);

        if (NativeMethods.GetCursorInfo(out cursorInfo) && cursorInfo.flags == NativeMethods.CURSOR_SHOWING)
        {
            var hIcon = NativeMethods.CopyIcon(cursorInfo.hCursor);
            if (hIcon != IntPtr.Zero)
            {
                if (NativeMethods.GetIconInfo(hIcon, out NativeMethods.ICONINFO iconInfo))
                {
                    int x = cursorInfo.ptScreenPos.X - captureRegion.Left - iconInfo.xHotspot;
                    int y = cursorInfo.ptScreenPos.Y - captureRegion.Top - iconInfo.yHotspot;

                    using var cursorBitmap = Icon.FromHandle(hIcon).ToBitmap();
                    graphics.DrawImage(cursorBitmap, x, y);

                    if (iconInfo.hbmMask != IntPtr.Zero)
                        NativeMethods.DeleteObject(iconInfo.hbmMask);
                    if (iconInfo.hbmColor != IntPtr.Zero)
                        NativeMethods.DeleteObject(iconInfo.hbmColor);
                }
                NativeMethods.DestroyIcon(hIcon);
            }
        }
    }

    /// <summary>
    /// Gets the virtual screen bounds (all monitors combined)
    /// </summary>
    public static Rectangle GetVirtualScreenBounds()
    {
        int left = SystemInformation.VirtualScreen.Left;
        int top = SystemInformation.VirtualScreen.Top;
        int width = SystemInformation.VirtualScreen.Width;
        int height = SystemInformation.VirtualScreen.Height;

        return new Rectangle(left, top, width, height);
    }

    /// <summary>
    /// Gets all screen information
    /// </summary>
    public static Screen[] GetAllScreens()
    {
        return Screen.AllScreens;
    }
}

/// <summary>
/// Simple settings singleton (will be expanded later)
/// </summary>
public class AppSettings
{
    private static AppSettings? _instance;
    public static AppSettings Instance => _instance ??= new AppSettings();

    public bool CaptureCursor { get; set; } = true;
    public string SavePath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public string DefaultFormat { get; set; } = "png";
    public bool CopyToClipboard { get; set; } = true;
    public bool PlaySound { get; set; } = true;
}
