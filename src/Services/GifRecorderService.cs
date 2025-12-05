using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using AnimatedGif;

namespace SnipIt.Services;

/// <summary>
/// Service for recording screen region as animated GIF
/// </summary>
public sealed class GifRecorderService : IDisposable
{
    private readonly List<Bitmap> _frames = [];
    private readonly System.Timers.Timer _captureTimer;
    private Rectangle _captureRegion;
    private bool _isRecording;
    private DateTime _recordingStartTime;

    // Configurable FPS (15, 30, or 60)
    private readonly int _targetFps;
    private readonly int _frameDelayMs;

    public event Action<TimeSpan>? RecordingProgress;
    public event Action<string>? RecordingCompleted;
    public event Action<string>? RecordingError;

    public bool IsRecording => _isRecording;
    public int FrameCount => _frames.Count;
    public int TargetFps => _targetFps;
    public TimeSpan RecordingDuration => _isRecording
        ? DateTime.Now - _recordingStartTime
        : TimeSpan.Zero;

    public GifRecorderService(int fps = 30)
    {
        _targetFps = fps switch
        {
            15 => 15,
            60 => 60,
            _ => 30
        };
        _frameDelayMs = 1000 / _targetFps;

        _captureTimer = new System.Timers.Timer(_frameDelayMs);
        _captureTimer.Elapsed += CaptureTimer_Elapsed;
        _captureTimer.AutoReset = true;
    }

    /// <summary>
    /// Start recording the specified region
    /// </summary>
    public void StartRecording(Rectangle region)
    {
        if (_isRecording) return;

        _captureRegion = region;
        _frames.Clear();
        _isRecording = true;
        _recordingStartTime = DateTime.Now;
        _captureTimer.Start();
    }

    /// <summary>
    /// Stop recording and save the GIF
    /// </summary>
    public async Task<string?> StopRecordingAsync()
    {
        if (!_isRecording) return null;

        _isRecording = false;
        _captureTimer.Stop();

        if (_frames.Count == 0)
        {
            RecordingError?.Invoke("녹화된 프레임이 없습니다.");
            return null;
        }

        return await Task.Run(() => SaveGif());
    }

    /// <summary>
    /// Cancel recording without saving
    /// </summary>
    public void CancelRecording()
    {
        _isRecording = false;
        _captureTimer.Stop();
        ClearFrames();
    }

    private void CaptureTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_isRecording) return;

        try
        {
            var frame = CaptureFrame();
            if (frame != null)
            {
                lock (_frames)
                {
                    _frames.Add(frame);
                }
                RecordingProgress?.Invoke(RecordingDuration);
            }
        }
        catch
        {
            // Skip frame on error
        }
    }

    private Bitmap? CaptureFrame()
    {
        try
        {
            var bitmap = new Bitmap(_captureRegion.Width, _captureRegion.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    _captureRegion.Left,
                    _captureRegion.Top,
                    0, 0,
                    _captureRegion.Size,
                    CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private string? SaveGif()
    {
        try
        {
            // Create save dialog path
            var savePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                $"SnipIt_Recording_{DateTime.Now:yyyyMMdd_HHmmss}.gif");

            // Show save dialog on UI thread
            string? finalPath = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "GIF Image|*.gif",
                    DefaultExt = ".gif",
                    FileName = Path.GetFileName(savePath),
                    InitialDirectory = Path.GetDirectoryName(savePath)
                };

                if (dialog.ShowDialog() == true)
                {
                    finalPath = dialog.FileName;
                }
            });

            if (string.IsNullOrEmpty(finalPath))
            {
                ClearFrames();
                return null;
            }

            // Create animated GIF
            using (var gif = AnimatedGif.AnimatedGif.Create(finalPath, _frameDelayMs))
            {
                lock (_frames)
                {
                    foreach (var frame in _frames)
                    {
                        // Convert to Image for AnimatedGif library
                        gif.AddFrame(frame, delay: -1, quality: GifQuality.Bit8);
                    }
                }
            }

            ClearFrames();
            RecordingCompleted?.Invoke(finalPath);
            return finalPath;
        }
        catch (Exception ex)
        {
            RecordingError?.Invoke($"GIF 저장 실패: {ex.Message}");
            ClearFrames();
            return null;
        }
    }

    private void ClearFrames()
    {
        lock (_frames)
        {
            foreach (var frame in _frames)
            {
                frame.Dispose();
            }
            _frames.Clear();
        }
    }

    public void Dispose()
    {
        _captureTimer.Stop();
        _captureTimer.Dispose();
        ClearFrames();
    }
}
