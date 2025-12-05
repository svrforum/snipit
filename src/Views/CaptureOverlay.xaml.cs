using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SnipIt.Services;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SnipIt.Views;

public partial class CaptureOverlay : Window
{
    private Bitmap? _screenBitmap;
    private System.Windows.Point _startPoint;
    private System.Windows.Point _currentPoint;
    private bool _isSelecting;
    private Rectangle? _selectionRect;
    private Rectangle? _topOverlay;
    private Rectangle? _bottomOverlay;
    private Rectangle? _leftOverlay;
    private Rectangle? _rightOverlay;

    // Throttle magnifier updates to reduce GC pressure
    private DateTime _lastMagnifierUpdate = DateTime.MinValue;
    private const int MagnifierUpdateIntervalMs = 33; // ~30fps

    public Bitmap? CapturedImage { get; private set; }

    public CaptureOverlay()
    {
        // Capture screen before showing overlay
        _screenBitmap = ScreenCaptureService.CaptureFullScreen();

        InitializeComponent();

        // Initial size (will be adjusted in SourceInitialized)
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        SourceInitialized += CaptureOverlay_SourceInitialized;
        Loaded += CaptureOverlay_Loaded;
    }

    private void CaptureOverlay_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

        // Use EnumDisplayMonitors to get actual physical screen bounds
        var monitors = new List<MONITORINFO>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var mi = new MONITORINFO();
            mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>();
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                monitors.Add(mi);
            }
            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0) return;

        // Calculate bounding rectangle of all monitors
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var mi in monitors)
        {
            minX = Math.Min(minX, mi.rcMonitor.Left);
            minY = Math.Min(minY, mi.rcMonitor.Top);
            maxX = Math.Max(maxX, mi.rcMonitor.Right);
            maxY = Math.Max(maxY, mi.rcMonitor.Bottom);
        }

        // Position window to cover all monitors
        SetWindowPos(hwnd, IntPtr.Zero,
            minX, minY,
            maxX - minX, maxY - minY,
            SWP_NOZORDER | SWP_NOACTIVATE);

        // Store the offset for coordinate conversion
        _screenOffsetX = minX;
        _screenOffsetY = minY;
    }

    private int _screenOffsetX;
    private int _screenOffsetY;

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private void CaptureOverlay_Loaded(object sender, RoutedEventArgs e)
    {
        // Set the background image
        if (_screenBitmap != null)
        {
            BackgroundImage.Source = BitmapToImageSource(_screenBitmap);
        }

        // Initialize overlay rectangles
        InitializeOverlays();

        // Move cursor position to be relative to virtual screen and update info panel
        var virtualScreen = ScreenCaptureService.GetVirtualScreenBounds();
        if (GetCursorPos(out var cursorPos))
        {
            // Convert screen coordinates to window coordinates
            var windowPos = new System.Windows.Point(
                cursorPos.X - virtualScreen.Left,
                cursorPos.Y - virtualScreen.Top);
            UpdateInfoPanel(windowPos);

            // Move the info panel to cursor location
            InfoPanel.Margin = new Thickness(windowPos.X + 10, windowPos.Y + 10, 0, 0);
        }
        else
        {
            UpdateInfoPanel(new System.Windows.Point(0, 0));
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private void InitializeOverlays()
    {
        var overlayBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 0, 0));

        _topOverlay = new Rectangle { Fill = overlayBrush };
        _bottomOverlay = new Rectangle { Fill = overlayBrush };
        _leftOverlay = new Rectangle { Fill = overlayBrush };
        _rightOverlay = new Rectangle { Fill = overlayBrush };

        // Selection rectangle with border
        _selectionRect = new Rectangle
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 33, 150, 243)),
            StrokeThickness = 2,
            Fill = System.Windows.Media.Brushes.Transparent,
            Visibility = Visibility.Collapsed
        };

        OverlayCanvas.Children.Add(_topOverlay);
        OverlayCanvas.Children.Add(_bottomOverlay);
        OverlayCanvas.Children.Add(_leftOverlay);
        OverlayCanvas.Children.Add(_rightOverlay);
        OverlayCanvas.Children.Add(_selectionRect);

        // Cover entire canvas initially
        UpdateOverlays(new Rect(0, 0, 0, 0));
    }

    private void UpdateOverlays(Rect selection)
    {
        double canvasWidth = OverlayCanvas.ActualWidth;
        double canvasHeight = OverlayCanvas.ActualHeight;

        if (selection.Width > 0 && selection.Height > 0)
        {
            // Top overlay
            Canvas.SetLeft(_topOverlay!, 0);
            Canvas.SetTop(_topOverlay!, 0);
            _topOverlay!.Width = canvasWidth;
            _topOverlay!.Height = selection.Top;

            // Bottom overlay
            Canvas.SetLeft(_bottomOverlay!, 0);
            Canvas.SetTop(_bottomOverlay!, selection.Bottom);
            _bottomOverlay!.Width = canvasWidth;
            _bottomOverlay!.Height = canvasHeight - selection.Bottom;

            // Left overlay
            Canvas.SetLeft(_leftOverlay!, 0);
            Canvas.SetTop(_leftOverlay!, selection.Top);
            _leftOverlay!.Width = selection.Left;
            _leftOverlay!.Height = selection.Height;

            // Right overlay
            Canvas.SetLeft(_rightOverlay!, selection.Right);
            Canvas.SetTop(_rightOverlay!, selection.Top);
            _rightOverlay!.Width = canvasWidth - selection.Right;
            _rightOverlay!.Height = selection.Height;

            // Selection rectangle
            Canvas.SetLeft(_selectionRect!, selection.Left);
            Canvas.SetTop(_selectionRect!, selection.Top);
            _selectionRect!.Width = selection.Width;
            _selectionRect!.Height = selection.Height;
            _selectionRect!.Visibility = Visibility.Visible;
        }
        else
        {
            // Cover everything when no selection
            Canvas.SetLeft(_topOverlay!, 0);
            Canvas.SetTop(_topOverlay!, 0);
            _topOverlay!.Width = canvasWidth;
            _topOverlay!.Height = canvasHeight;

            _bottomOverlay!.Width = 0;
            _leftOverlay!.Width = 0;
            _rightOverlay!.Width = 0;
            _selectionRect!.Visibility = Visibility.Collapsed;
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(OverlayCanvas);
        _isSelecting = true;
        OverlayCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _currentPoint = e.GetPosition(OverlayCanvas);

        UpdateInfoPanel(_currentPoint);

        // Throttle magnifier updates to ~30fps to reduce GC pressure
        var now = DateTime.Now;
        if ((now - _lastMagnifierUpdate).TotalMilliseconds >= MagnifierUpdateIntervalMs)
        {
            UpdateMagnifier(_currentPoint);
            _lastMagnifierUpdate = now;
        }

        if (_isSelecting)
        {
            var rect = GetSelectionRect();
            UpdateOverlays(rect);
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting)
        {
            _isSelecting = false;
            OverlayCanvas.ReleaseMouseCapture();

            var rect = GetSelectionRect();
            if (rect.Width > 5 && rect.Height > 5)
            {
                CaptureSelection(rect);
            }
        }
    }

    private Rect GetSelectionRect()
    {
        double x = Math.Min(_startPoint.X, _currentPoint.X);
        double y = Math.Min(_startPoint.Y, _currentPoint.Y);
        double width = Math.Abs(_currentPoint.X - _startPoint.X);
        double height = Math.Abs(_currentPoint.Y - _startPoint.Y);

        return new Rect(x, y, width, height);
    }

    private void UpdateInfoPanel(System.Windows.Point position)
    {
        if (_isSelecting)
        {
            var rect = GetSelectionRect();
            SizeText.Text = $"{(int)rect.Width} x {(int)rect.Height}";
        }
        else
        {
            SizeText.Text = $"X: {(int)position.X}  Y: {(int)position.Y}";
        }
    }

    private void UpdateMagnifier(System.Windows.Point position)
    {
        if (_screenBitmap == null) return;

        // Position magnifier at top-left of cursor
        double magX = position.X - 140;
        double magY = position.Y - 140;

        // Adjust if near screen edge
        if (magX < 10) magX = position.X + 20;
        if (magY < 10) magY = position.Y + 20;

        Magnifier.Margin = new Thickness(magX, magY, 0, 0);
        Magnifier.Visibility = Visibility.Visible;

        // Position InfoPanel below magnifier
        InfoPanel.Margin = new Thickness(magX, magY + 125, 0, 0);

        // Create magnified view (account for DPI scaling)
        int srcX = Math.Max(0, (int)(position.X * _screenBitmap.Width / ActualWidth) - 30);
        int srcY = Math.Max(0, (int)(position.Y * _screenBitmap.Height / ActualHeight) - 30);
        int srcWidth = Math.Min(60, _screenBitmap.Width - srcX);
        int srcHeight = Math.Min(60, _screenBitmap.Height - srcY);

        if (srcWidth > 0 && srcHeight > 0)
        {
            try
            {
                var cropRect = new System.Drawing.Rectangle(srcX, srcY, srcWidth, srcHeight);
                using var cropped = _screenBitmap.Clone(cropRect, _screenBitmap.PixelFormat);
                using var scaled = new Bitmap(120, 120);
                using var g = Graphics.FromImage(scaled);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(cropped, 0, 0, 120, 120);

                MagnifierImage.Source = BitmapToImageSource(scaled);
            }
            catch
            {
                // Ignore magnifier errors
            }
        }
    }

    private void CaptureSelection(Rect rect)
    {
        if (_screenBitmap == null) return;

        var virtualScreen = ScreenCaptureService.GetVirtualScreenBounds();

        int x = (int)rect.X;
        int y = (int)rect.Y;
        int width = (int)rect.Width;
        int height = (int)rect.Height;

        // Ensure within bounds
        x = Math.Max(0, Math.Min(x, _screenBitmap.Width - 1));
        y = Math.Max(0, Math.Min(y, _screenBitmap.Height - 1));
        width = Math.Min(width, _screenBitmap.Width - x);
        height = Math.Min(height, _screenBitmap.Height - y);

        if (width > 0 && height > 0)
        {
            var cropRect = new System.Drawing.Rectangle(x, y, width, height);
            CapturedImage = _screenBitmap.Clone(cropRect, _screenBitmap.PixelFormat);
        }

        Close();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CapturedImage = null;
                Close();
                break;

            case Key.Enter:
                if (_isSelecting || (_selectionRect?.Visibility == Visibility.Visible))
                {
                    var rect = GetSelectionRect();
                    if (rect.Width > 5 && rect.Height > 5)
                    {
                        CaptureSelection(rect);
                    }
                }
                break;
        }
    }

    private static BitmapSource BitmapToImageSource(Bitmap bitmap)
    {
        // Use CreateBitmapSourceFromHBitmap for much faster conversion
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            hBitmap = bitmap.GetHbitmap();
            var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            // Fallback to MemoryStream method if GetHbitmap fails
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Bmp);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    protected override void OnClosed(EventArgs e)
    {
        _screenBitmap?.Dispose();
        base.OnClosed(e);
    }
}
