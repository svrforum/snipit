using System.Windows;
using System.Windows.Interop;

namespace SnipIt.Views;

public partial class RecordingBorderWindow : Window
{
    public RecordingBorderWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Set the position and size to match the recording region
    /// </summary>
    public void SetRegion(System.Drawing.Rectangle region)
    {
        // Position the window to surround the recording area
        // Border thickness is 3, so offset by 3 pixels
        Left = region.Left - 3;
        Top = region.Top - 3;
        Width = region.Width + 6;
        Height = region.Height + 6;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Make window click-through (WS_EX_TRANSPARENT)
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }

    #region Native Methods
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    #endregion
}
