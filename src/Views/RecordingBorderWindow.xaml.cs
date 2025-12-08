using System.Windows;
using System.Windows.Interop;
using SnipIt.Utils;

namespace SnipIt.Views;

public partial class RecordingBorderWindow : Window
{
    public RecordingBorderWindow()
    {
        InitializeComponent();
    }

    private System.Drawing.Rectangle _physicalRegion;
    private const int BorderThicknessPixels = 4; // Physical pixels for the border
    private const int BorderGapPixels = 2; // Extra gap between recording region and border

    /// <summary>
    /// Set the position and size to match the recording region (in physical pixels)
    /// </summary>
    public void SetRegion(System.Drawing.Rectangle region)
    {
        _physicalRegion = region;

        // If window is already shown, update position immediately
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            UpdatePositionAndSize();
        }
    }

    private void UpdatePositionAndSize()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // Get DPI scale
        double dpiScale = GetDpiScale();

        // Calculate sizes in logical units (for WPF Grid)
        double logicalBorderThickness = BorderThicknessPixels / dpiScale;
        double logicalGap = BorderGapPixels / dpiScale;

        // Update Grid row definitions:
        // Row 0 = top border, Row 1 = top gap, Row 2 = center (*), Row 3 = bottom gap, Row 4 = bottom border
        BorderGrid.RowDefinitions[0].Height = new GridLength(logicalBorderThickness);
        BorderGrid.RowDefinitions[1].Height = new GridLength(logicalGap);
        BorderGrid.RowDefinitions[3].Height = new GridLength(logicalGap);
        BorderGrid.RowDefinitions[4].Height = new GridLength(logicalBorderThickness);

        // Update Grid column definitions:
        // Col 0 = left border, Col 1 = left gap, Col 2 = center (*), Col 3 = right gap, Col 4 = right border
        BorderGrid.ColumnDefinitions[0].Width = new GridLength(logicalBorderThickness);
        BorderGrid.ColumnDefinitions[1].Width = new GridLength(logicalGap);
        BorderGrid.ColumnDefinitions[3].Width = new GridLength(logicalGap);
        BorderGrid.ColumnDefinitions[4].Width = new GridLength(logicalBorderThickness);

        // Position window so that the border is COMPLETELY OUTSIDE the recording region
        // Total offset = border + gap
        int totalOffset = BorderThicknessPixels + BorderGapPixels;
        int left = _physicalRegion.Left - totalOffset;
        int top = _physicalRegion.Top - totalOffset;
        int width = _physicalRegion.Width + (totalOffset * 2);
        int height = _physicalRegion.Height + (totalOffset * 2);

        SetWindowPos(hwnd, IntPtr.Zero, left, top, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private double GetDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            return source.CompositionTarget.TransformToDevice.M11;
        }
        return 1.0;
    }

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Make window click-through (WS_EX_TRANSPARENT)
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);

        // Update position after hwnd is available
        if (_physicalRegion.Width > 0 && _physicalRegion.Height > 0)
        {
            UpdatePositionAndSize();
        }
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
