using System.Runtime.InteropServices;
using System.Text;

namespace SnipIt.Utils;

/// <summary>
/// Native Windows API declarations with Windows 11 support
/// </summary>
public static partial class NativeMethods
{
    #region Hotkey Registration
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;

    // Modifier keys for RegisterHotKey
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;
    #endregion

    #region Window Management
    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetDesktopWindow();

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetWindowDC(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    public static partial int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    #endregion

    #region Windows 11 DWM Features
    /// <summary>
    /// Windows 11 window corner preference
    /// </summary>
    public enum DWM_WINDOW_CORNER_PREFERENCE
    {
        DWMWCP_DEFAULT = 0,
        DWMWCP_DONOTROUND = 1,
        DWMWCP_ROUND = 2,
        DWMWCP_ROUNDSMALL = 3
    }

    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_MICA_EFFECT = 1029;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    /// <summary>
    /// Windows 11 system backdrop types
    /// </summary>
    public enum DWM_SYSTEMBACKDROP_TYPE
    {
        DWMSBT_AUTO = 0,
        DWMSBT_NONE = 1,
        DWMSBT_MAINWINDOW = 2,    // Mica
        DWMSBT_TRANSIENTWINDOW = 3,  // Acrylic
        DWMSBT_TABBEDWINDOW = 4   // Tabbed Mica
    }

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    /// <summary>
    /// Apply Windows 11 rounded corners to a window
    /// </summary>
    public static void ApplyRoundedCorners(IntPtr hwnd, DWM_WINDOW_CORNER_PREFERENCE preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND)
    {
        if (!IsWindows11OrNewer()) return;

        int value = (int)preference;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
    }

    /// <summary>
    /// Apply Windows 11 Mica effect to a window
    /// </summary>
    public static void ApplyMicaEffect(IntPtr hwnd, bool enable = true)
    {
        if (!IsWindows11OrNewer()) return;

        int value = enable ? (int)DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW : (int)DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref value, sizeof(int));
    }

    /// <summary>
    /// Apply dark mode to window title bar (Windows 10 20H1+ and Windows 11)
    /// </summary>
    public static void ApplyDarkMode(IntPtr hwnd, bool enable = true)
    {
        int value = enable ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    /// <summary>
    /// Check if running on Windows 11 or newer
    /// </summary>
    public static bool IsWindows11OrNewer()
    {
        var version = Environment.OSVersion.Version;
        return version.Major >= 10 && version.Build >= 22000;
    }

    /// <summary>
    /// Check if running on Windows 10 20H1 or newer
    /// </summary>
    public static bool IsWindows10_20H1OrNewer()
    {
        var version = Environment.OSVersion.Version;
        return version.Major >= 10 && version.Build >= 19041;
    }
    #endregion

    #region Cursor
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetCursor();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorInfo(out CURSORINFO pci);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

    [LibraryImport("user32.dll")]
    public static partial IntPtr CopyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr hIcon);

    public const int CURSOR_SHOWING = 0x00000001;
    #endregion

    #region GDI
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(IntPtr hObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(IntPtr hdc);

    public const int SRCCOPY = 0x00CC0020;
    public const int CAPTUREBLT = 0x40000000;
    #endregion

    #region Shell - Snap Layout Support
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_FRAMECHANGED = 0x0020;

    public static readonly IntPtr HWND_TOP = IntPtr.Zero;
    #endregion

    #region High DPI Support
    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(IntPtr hwnd);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    public static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    public const uint MONITOR_DEFAULTTONEAREST = 2;
    #endregion

    #region Structures
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;

        public readonly System.Drawing.Rectangle ToRectangle() => new(Left, Top, Width, Height);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public readonly System.Drawing.Point ToPoint() => new(X, Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }
    #endregion
}

/// <summary>
/// Windows 11 specific helper methods
/// </summary>
public static class Windows11Helper
{
    /// <summary>
    /// Apply Windows 11 styling to a WPF window
    /// </summary>
    public static void ApplyWindows11Style(System.Windows.Window window, bool useMica = false, bool useDarkMode = false)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();

        // Apply rounded corners
        NativeMethods.ApplyRoundedCorners(hwnd);

        // Apply dark mode if requested
        if (useDarkMode)
        {
            NativeMethods.ApplyDarkMode(hwnd, true);
        }

        // Apply Mica if requested (Windows 11 only)
        if (useMica && NativeMethods.IsWindows11OrNewer())
        {
            // Set window background to transparent for Mica to show through
            window.Background = System.Windows.Media.Brushes.Transparent;
            NativeMethods.ApplyMicaEffect(hwnd, true);
        }
    }

    /// <summary>
    /// Get the current system accent color
    /// </summary>
    public static System.Windows.Media.Color GetAccentColor()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\DWM");

            if (key?.GetValue("AccentColor") is int accentColor)
            {
                return System.Windows.Media.Color.FromArgb(
                    (byte)((accentColor >> 24) & 0xFF),
                    (byte)(accentColor & 0xFF),
                    (byte)((accentColor >> 8) & 0xFF),
                    (byte)((accentColor >> 16) & 0xFF));
            }
        }
        catch
        {
            // Ignore
        }

        return System.Windows.Media.Color.FromRgb(0, 120, 215); // Default Windows blue
    }

    /// <summary>
    /// Check if system is using dark theme
    /// </summary>
    public static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the DPI scale factor for a window
    /// </summary>
    public static double GetDpiScale(System.Windows.Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return 1.0;

        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        return dpi / 96.0;
    }
}
