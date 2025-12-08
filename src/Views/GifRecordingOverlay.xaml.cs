using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SnipIt.Models;
using SnipIt.Services;
using SnipIt.Utils;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SnipIt.Views;

public partial class GifRecordingOverlay : Window
{
    private enum RecordingState
    {
        Selecting,
        Countdown,
        Recording
    }

    private Bitmap? _screenBitmap;
    private System.Windows.Point _startPoint;
    private System.Windows.Point _currentPoint;
    private bool _isSelecting;
    private Rectangle? _selectionRect;
    private Rectangle? _topOverlay;
    private Rectangle? _bottomOverlay;
    private Rectangle? _leftOverlay;
    private Rectangle? _rightOverlay;

    private RecordingState _state = RecordingState.Selecting;
    private Rect _selectedRegion;
    private GifRecorderService? _recorder;
    private DispatcherTimer? _countdownTimer;
    private DispatcherTimer? _recordingTimer;
    private int _countdownValue = 3;
    private RecordingControlWindow? _controlWindow;
    private RecordingBorderWindow? _borderWindow;

    // Throttle magnifier updates to reduce GC pressure (synced to display refresh rate)
    private DateTime _lastMagnifierUpdate = DateTime.MinValue;
    private readonly int _magnifierUpdateIntervalMs = NativeMethods.GetFrameIntervalMs();

    public GifRecordingOverlay()
    {
        _screenBitmap = ScreenCaptureService.CaptureFullScreen();
        InitializeComponent();

        // Initial size (will be adjusted in SourceInitialized)
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        SourceInitialized += GifRecordingOverlay_SourceInitialized;
        Loaded += GifRecordingOverlay_Loaded;
    }

    private void GifRecordingOverlay_SourceInitialized(object? sender, EventArgs e)
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

    private void GifRecordingOverlay_Loaded(object sender, RoutedEventArgs e)
    {
        if (_screenBitmap != null)
        {
            BackgroundImage.Source = BitmapToImageSource(_screenBitmap);
        }

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

            // Move the WPF mouse position hint to cursor location
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

        _selectionRect = new Rectangle
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 82, 82)),
            StrokeThickness = 2,
            Fill = System.Windows.Media.Brushes.Transparent,
            Visibility = Visibility.Collapsed
        };

        OverlayCanvas.Children.Add(_topOverlay);
        OverlayCanvas.Children.Add(_bottomOverlay);
        OverlayCanvas.Children.Add(_leftOverlay);
        OverlayCanvas.Children.Add(_rightOverlay);
        OverlayCanvas.Children.Add(_selectionRect);

        UpdateOverlays(new Rect(0, 0, 0, 0));
    }

    private void UpdateOverlays(Rect selection)
    {
        double canvasWidth = OverlayCanvas.ActualWidth;
        double canvasHeight = OverlayCanvas.ActualHeight;

        if (selection.Width > 0 && selection.Height > 0)
        {
            Canvas.SetLeft(_topOverlay!, 0);
            Canvas.SetTop(_topOverlay!, 0);
            _topOverlay!.Width = canvasWidth;
            _topOverlay!.Height = selection.Top;

            Canvas.SetLeft(_bottomOverlay!, 0);
            Canvas.SetTop(_bottomOverlay!, selection.Bottom);
            _bottomOverlay!.Width = canvasWidth;
            _bottomOverlay!.Height = canvasHeight - selection.Bottom;

            Canvas.SetLeft(_leftOverlay!, 0);
            Canvas.SetTop(_leftOverlay!, selection.Top);
            _leftOverlay!.Width = selection.Left;
            _leftOverlay!.Height = selection.Height;

            Canvas.SetLeft(_rightOverlay!, selection.Right);
            Canvas.SetTop(_rightOverlay!, selection.Top);
            _rightOverlay!.Width = canvasWidth - selection.Right;
            _rightOverlay!.Height = selection.Height;

            Canvas.SetLeft(_selectionRect!, selection.Left);
            Canvas.SetTop(_selectionRect!, selection.Top);
            _selectionRect!.Width = selection.Width;
            _selectionRect!.Height = selection.Height;
            _selectionRect!.Visibility = Visibility.Visible;
        }
        else
        {
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
        if (_state != RecordingState.Selecting) return;

        _startPoint = e.GetPosition(OverlayCanvas);
        _isSelecting = true;
        OverlayCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Only show magnifier and info during selection state
        if (_state != RecordingState.Selecting)
        {
            // Ensure UI elements are hidden in non-selecting states
            Magnifier.Visibility = Visibility.Collapsed;
            InfoPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _currentPoint = e.GetPosition(OverlayCanvas);
        UpdateInfoPanel(_currentPoint);

        // Throttle magnifier updates synced to display refresh rate
        var now = DateTime.Now;
        if ((now - _lastMagnifierUpdate).TotalMilliseconds >= _magnifierUpdateIntervalMs)
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

        // Create magnified view (2x zoom of 60x60 area)
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

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_state != RecordingState.Selecting) return;

        if (_isSelecting)
        {
            _isSelecting = false;
            OverlayCanvas.ReleaseMouseCapture();

            var rect = GetSelectionRect();
            if (rect.Width > 10 && rect.Height > 10)
            {
                _selectedRegion = rect;
                StartCountdown();
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

    private void StartCountdown()
    {
        // Change state FIRST to prevent further mouse events from showing magnifier
        _state = RecordingState.Countdown;

        // Hide ALL selection UI elements immediately
        Magnifier.Visibility = Visibility.Collapsed;
        InfoPanel.Visibility = Visibility.Collapsed;
        SelectionHelpPanel.Visibility = Visibility.Collapsed;
        BackgroundImage.Visibility = Visibility.Collapsed;
        OverlayCanvas.Visibility = Visibility.Collapsed;

        // Make window transparent
        Background = System.Windows.Media.Brushes.Transparent;

        // Force immediate UI update to ensure elements are hidden
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

        // Show recording border to indicate selected region
        RecordingBorder.Visibility = Visibility.Visible;
        RecordingBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7)); // Yellow during countdown
        RecordingBorder.Margin = new Thickness(
            _selectedRegion.Left - 3,
            _selectedRegion.Top - 3,
            0, 0);
        RecordingBorder.Width = _selectedRegion.Width + 6;
        RecordingBorder.Height = _selectedRegion.Height + 6;

        // Show countdown banner at top center of primary screen
        CountdownPanel.Visibility = Visibility.Visible;
        _countdownValue = 3;
        CountdownText.Text = _countdownValue.ToString();

        // Position countdown panel at top center of primary screen
        CountdownPanel.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var panelWidth = CountdownPanel.DesiredSize.Width;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var virtualScreen = ScreenCaptureService.GetVirtualScreenBounds();

        // Calculate center position relative to window coordinates
        double centerX = (screenWidth - panelWidth) / 2 - virtualScreen.Left;
        CountdownPanel.Margin = new Thickness(Math.Max(0, centerX), 0, 0, 0);

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += CountdownTimer_Tick;
        _countdownTimer.Start();
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _countdownValue--;

        if (_countdownValue > 0)
        {
            CountdownText.Text = _countdownValue.ToString();
        }
        else
        {
            _countdownTimer?.Stop();
            StartRecording();
        }
    }

    private void StartRecording()
    {
        _state = RecordingState.Recording;

        // Calculate scale factor: screen bitmap uses physical pixels, WPF uses logical units
        double scaleX = _screenBitmap != null ? (double)_screenBitmap.Width / ActualWidth : 1.0;
        double scaleY = _screenBitmap != null ? (double)_screenBitmap.Height / ActualHeight : 1.0;

        // Calculate recording region in physical pixels (not WPF logical units)
        var recordRegion = new System.Drawing.Rectangle(
            (int)(_selectedRegion.Left * scaleX) + _screenOffsetX,
            (int)(_selectedRegion.Top * scaleY) + _screenOffsetY,
            (int)(_selectedRegion.Width * scaleX),
            (int)(_selectedRegion.Height * scaleY));

        // Hide the overlay completely so it doesn't get recorded
        CountdownPanel.Visibility = Visibility.Collapsed;
        RecordingBorder.Visibility = Visibility.Collapsed;
        Hide();

        // Create separate control window outside recording area
        _controlWindow = new RecordingControlWindow();
        _controlWindow.StopRequested += () => Dispatcher.Invoke(StopRecording);

        // Position control window at top center of primary screen
        _controlWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        _controlWindow.Show();

        // Calculate top center position after window is shown (to get ActualWidth)
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        _controlWindow.Left = (screenWidth - _controlWindow.ActualWidth) / 2;
        _controlWindow.Top = 20; // 20px from top

        // Show recording border window OUTSIDE the recording area
        // Border is positioned outside recordRegion so it won't appear in the recording
        _borderWindow = new RecordingBorderWindow();
        _borderWindow.SetRegion(recordRegion);
        _borderWindow.Show();

        // Get FPS, quality, and max duration from settings
        var fps = AppSettingsConfig.Instance.GifFps;
        var quality = AppSettingsConfig.Instance.GifQuality;
        var maxDuration = AppSettingsConfig.Instance.GifMaxDurationSeconds;
        _recorder = new GifRecorderService(fps, quality, maxDuration);
        _recorder.RecordingProgress += OnRecordingProgress;
        _recorder.RecordingCompleted += OnRecordingCompleted;
        _recorder.RecordingError += OnRecordingError;
        _recorder.MaxDurationReached += OnMaxDurationReached;
        _recorder.StartRecording(recordRegion);

        // Update recording time display
        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _recordingTimer.Tick += RecordingTimer_Tick;
        _recordingTimer.Start();
    }

    private void RecordingTimer_Tick(object? sender, EventArgs e)
    {
        if (_recorder != null && _controlWindow != null)
        {
            var duration = _recorder.RecordingDuration;
            var maxDuration = _recorder.MaxDurationSeconds;
            var remaining = maxDuration - (int)duration.TotalSeconds;
            _controlWindow.UpdateTime($"REC {duration:mm\\:ss} / {maxDuration}s ({_recorder.TargetFps}fps)");
        }
    }

    private void OnRecordingProgress(TimeSpan duration)
    {
        // Progress updates handled by timer
    }

    private void OnRecordingCompleted(string filePath)
    {
        // No popup - just close silently
    }

    private void OnRecordingError(string error)
    {
        Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(
                error,
                "녹화 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }

    private void OnMaxDurationReached()
    {
        Dispatcher.Invoke(() =>
        {
            // Auto-stop recording when max duration reached
            StopRecording();
        });
    }

    private void StopRecording()
    {
        if (_recorder == null) return;

        _recordingTimer?.Stop();

        // Close control window and border window
        _controlWindow?.Close();
        _controlWindow = null;

        _borderWindow?.Close();
        _borderWindow = null;

        // Capture recorder reference before closing
        var recorder = _recorder;
        _recorder = null;

        // Close this window
        Close();

        // Save GIF in background
        Task.Run(async () =>
        {
            try
            {
                await recorder.StopRecordingAsync();
            }
            finally
            {
                recorder.Dispose();
            }
        });
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Arrow keys for pixel-level cursor control (only during selection)
        if (_state == RecordingState.Selecting)
        {
            int moveAmount = Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift) ? 10 : 1;

            switch (e.Key)
            {
                case Key.Left:
                    MoveCursor(-moveAmount, 0);
                    e.Handled = true;
                    return;

                case Key.Right:
                    MoveCursor(moveAmount, 0);
                    e.Handled = true;
                    return;

                case Key.Up:
                    MoveCursor(0, -moveAmount);
                    e.Handled = true;
                    return;

                case Key.Down:
                    MoveCursor(0, moveAmount);
                    e.Handled = true;
                    return;

                case Key.Space:
                    // Space to start/confirm selection at current position
                    if (!_isSelecting)
                    {
                        _startPoint = _currentPoint;
                        _isSelecting = true;
                        OverlayCanvas.CaptureMouse();
                    }
                    else
                    {
                        _isSelecting = false;
                        OverlayCanvas.ReleaseMouseCapture();
                        var rect = GetSelectionRect();
                        if (rect.Width > 5 && rect.Height > 5)
                        {
                            _selectedRegion = rect;
                            StartCountdown();
                        }
                    }
                    e.Handled = true;
                    return;

                case Key.Enter:
                    if (_isSelecting || (_selectionRect?.Visibility == Visibility.Visible))
                    {
                        var rect = GetSelectionRect();
                        if (rect.Width > 5 && rect.Height > 5)
                        {
                            _selectedRegion = rect;
                            StartCountdown();
                        }
                    }
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Escape)
        {
            switch (_state)
            {
                case RecordingState.Selecting:
                    Close();
                    break;

                case RecordingState.Countdown:
                    _countdownTimer?.Stop();
                    Close();
                    break;

                case RecordingState.Recording:
                    StopRecording();
                    break;
            }
        }
    }

    private void MoveCursor(int deltaX, int deltaY)
    {
        if (NativeMethods.GetCursorPos(out NativeMethods.POINT currentPos))
        {
            int newX = currentPos.X + deltaX;
            int newY = currentPos.Y + deltaY;

            // Clamp to screen bounds
            newX = Math.Max(0, Math.Min(newX, (int)SystemParameters.VirtualScreenWidth - 1));
            newY = Math.Max(0, Math.Min(newY, (int)SystemParameters.VirtualScreenHeight - 1));

            NativeMethods.SetCursorPos(newX, newY);

            // Update internal position and UI
            _currentPoint = new System.Windows.Point(
                newX - SystemParameters.VirtualScreenLeft,
                newY - SystemParameters.VirtualScreenTop);

            UpdateInfoPanel(_currentPoint);
            UpdateMagnifier(_currentPoint);

            if (_isSelecting)
            {
                var rect = GetSelectionRect();
                UpdateOverlays(rect);
            }
        }
    }

    private void StopButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_state == RecordingState.Recording)
        {
            StopRecording();
        }
    }

    private static BitmapSource BitmapToImageSource(Bitmap bitmap)
    {
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
        _countdownTimer?.Stop();
        _recordingTimer?.Stop();
        _controlWindow?.Close();
        _borderWindow?.Close();
        _recorder?.CancelRecording();
        _recorder?.Dispose();
        _screenBitmap?.Dispose();
        base.OnClosed(e);
    }
}
