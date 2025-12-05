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

    public Bitmap? CapturedImage { get; private set; }

    public CaptureOverlay()
    {
        // Capture screen before showing overlay
        _screenBitmap = ScreenCaptureService.CaptureFullScreen();

        InitializeComponent();

        // Set window to cover all monitors (virtual screen)
        var virtualScreen = ScreenCaptureService.GetVirtualScreenBounds();
        Left = virtualScreen.Left;
        Top = virtualScreen.Top;
        Width = virtualScreen.Width;
        Height = virtualScreen.Height;

        Loaded += CaptureOverlay_Loaded;
    }

    private void CaptureOverlay_Loaded(object sender, RoutedEventArgs e)
    {
        // Set the background image
        if (_screenBitmap != null)
        {
            BackgroundImage.Source = BitmapToImageSource(_screenBitmap);

            // Position the image at virtual screen origin
            var virtualScreen = ScreenCaptureService.GetVirtualScreenBounds();
            Canvas.SetLeft(BackgroundImage, 0);
            Canvas.SetTop(BackgroundImage, 0);
        }

        // Initialize overlay rectangles
        InitializeOverlays();

        UpdateInfoPanel(new System.Windows.Point(0, 0));
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
        UpdateMagnifier(_currentPoint);

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

        // Position magnifier
        double offsetX = 20;
        double offsetY = 20;

        // Adjust if near screen edge
        if (position.X + 140 > ActualWidth)
            offsetX = -140;
        if (position.Y + 140 > ActualHeight)
            offsetY = -140;

        Canvas.SetLeft(Magnifier, position.X + offsetX);
        Canvas.SetTop(Magnifier, position.Y + offsetY);
        Magnifier.Visibility = Visibility.Visible;

        // Create magnified view
        int srcX = Math.Max(0, (int)position.X - 30);
        int srcY = Math.Max(0, (int)position.Y - 30);
        int srcWidth = Math.Min(60, _screenBitmap.Width - srcX);
        int srcHeight = Math.Min(60, _screenBitmap.Height - srcY);

        if (srcWidth > 0 && srcHeight > 0)
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
