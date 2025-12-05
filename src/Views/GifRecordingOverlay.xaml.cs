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
    private DispatcherTimer? _blinkTimer;
    private int _countdownValue = 3;

    public GifRecordingOverlay()
    {
        _screenBitmap = ScreenCaptureService.CaptureFullScreen();
        InitializeComponent();
        Loaded += GifRecordingOverlay_Loaded;
    }

    private void GifRecordingOverlay_Loaded(object sender, RoutedEventArgs e)
    {
        if (_screenBitmap != null)
        {
            BackgroundImage.Source = BitmapToImageSource(_screenBitmap);
            Canvas.SetLeft(BackgroundImage, 0);
            Canvas.SetTop(BackgroundImage, 0);
        }

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
        if (_state != RecordingState.Selecting) return;

        _currentPoint = e.GetPosition(OverlayCanvas);
        UpdateInfoPanel(_currentPoint);

        if (_isSelecting)
        {
            var rect = GetSelectionRect();
            UpdateOverlays(rect);
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
        _state = RecordingState.Countdown;

        // Hide selection UI
        SelectionHelpPanel.Visibility = Visibility.Collapsed;
        InfoPanel.Visibility = Visibility.Collapsed;
        OverlayCanvas.Background = System.Windows.Media.Brushes.Transparent;

        // Clear overlay rectangles
        _topOverlay!.Fill = System.Windows.Media.Brushes.Transparent;
        _bottomOverlay!.Fill = System.Windows.Media.Brushes.Transparent;
        _leftOverlay!.Fill = System.Windows.Media.Brushes.Transparent;
        _rightOverlay!.Fill = System.Windows.Media.Brushes.Transparent;

        // Show countdown
        CountdownPanel.Visibility = Visibility.Visible;
        _countdownValue = 3;
        CountdownText.Text = _countdownValue.ToString();

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

        // Hide countdown, show recording indicator
        CountdownPanel.Visibility = Visibility.Collapsed;
        RecordingPanel.Visibility = Visibility.Visible;

        // Hide background image
        BackgroundImage.Visibility = Visibility.Collapsed;
        _selectionRect!.Visibility = Visibility.Collapsed;

        // Show recording border around selected region
        RecordingBorder.Visibility = Visibility.Visible;
        RecordingBorder.Margin = new Thickness(
            _selectedRegion.Left - 3,
            _selectedRegion.Top - 3,
            0, 0);
        RecordingBorder.Width = _selectedRegion.Width + 6;
        RecordingBorder.Height = _selectedRegion.Height + 6;

        // Make window click-through except for the recording border
        Background = System.Windows.Media.Brushes.Transparent;
        OverlayCanvas.Visibility = Visibility.Collapsed;

        // Start blinking recording dot
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += (s, e) =>
        {
            RecordingDot.Visibility = RecordingDot.Visibility == Visibility.Visible
                ? Visibility.Hidden
                : Visibility.Visible;
        };
        _blinkTimer.Start();

        // Start recording
        var virtualScreen = ScreenCaptureService.GetVirtualScreenBounds();
        var recordRegion = new System.Drawing.Rectangle(
            (int)_selectedRegion.Left + virtualScreen.Left,
            (int)_selectedRegion.Top + virtualScreen.Top,
            (int)_selectedRegion.Width,
            (int)_selectedRegion.Height);

        // Get FPS from settings
        var fps = AppSettingsConfig.Instance.GifFps;
        _recorder = new GifRecorderService(fps);
        _recorder.RecordingProgress += OnRecordingProgress;
        _recorder.RecordingCompleted += OnRecordingCompleted;
        _recorder.RecordingError += OnRecordingError;
        _recorder.StartRecording(recordRegion);

        // Update recording time display
        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _recordingTimer.Tick += RecordingTimer_Tick;
        _recordingTimer.Start();
    }

    private void RecordingTimer_Tick(object? sender, EventArgs e)
    {
        if (_recorder != null)
        {
            var duration = _recorder.RecordingDuration;
            RecordingTimeText.Text = $"REC {duration:mm\\:ss} ({_recorder.TargetFps}fps)";
        }
    }

    private void OnRecordingProgress(TimeSpan duration)
    {
        // Progress updates handled by timer
    }

    private void OnRecordingCompleted(string filePath)
    {
        Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(
                $"GIF 저장 완료!\n\n{filePath}\n\n프레임 수: {_recorder?.FrameCount ?? 0}",
                "녹화 완료",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
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

    private async void StopRecording()
    {
        if (_recorder == null) return;

        _recordingTimer?.Stop();
        _blinkTimer?.Stop();

        RecordingTimeText.Text = "저장 중...";
        RecordingDot.Visibility = Visibility.Visible;

        await _recorder.StopRecordingAsync();

        _recorder.Dispose();
        _recorder = null;

        Close();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
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
        _blinkTimer?.Stop();
        _recorder?.CancelRecording();
        _recorder?.Dispose();
        _screenBitmap?.Dispose();
        base.OnClosed(e);
    }
}
