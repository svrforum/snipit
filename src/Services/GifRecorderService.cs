using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using AnimatedGif;
using SnipIt.Models;

namespace SnipIt.Services;

/// <summary>
/// Service for recording screen region as animated GIF with optimization
/// </summary>
public sealed class GifRecorderService : IDisposable
{
    private readonly List<(Bitmap frame, int duration)> _frames = [];
    private readonly System.Timers.Timer _captureTimer;
    private Rectangle _captureRegion;
    private bool _isRecording;
    private DateTime _recordingStartTime;
    private Bitmap? _previousFrame;
    private int _duplicateFrameCount;

    // Configurable settings
    private readonly int _targetFps;
    private readonly int _frameDelayMs;
    private readonly GifQualityPreset _quality;
    private readonly double _resolutionScale;
    private readonly int _colorDepth;
    private readonly bool _skipDuplicates;
    private readonly double _duplicateThreshold;
    private readonly int _maxDurationSeconds;

    public event Action<TimeSpan>? RecordingProgress;
    public event Action<string>? RecordingCompleted;
    public event Action<string>? RecordingError;
    public event Action? MaxDurationReached;

    public bool IsRecording => _isRecording;
    public int FrameCount => _frames.Count;
    public int TargetFps => _targetFps;
    public int SkippedFrames => _duplicateFrameCount;
    public TimeSpan RecordingDuration => _isRecording
        ? DateTime.Now - _recordingStartTime
        : TimeSpan.Zero;

    public GifRecorderService(int fps = 30, GifQualityPreset quality = GifQualityPreset.SkipFrames, int maxDurationSeconds = 60)
    {
        _targetFps = fps switch
        {
            15 => 15,
            60 => 60,
            _ => 30
        };
        _frameDelayMs = 1000 / _targetFps;
        _quality = quality;
        _maxDurationSeconds = maxDurationSeconds > 0 ? maxDurationSeconds : 60;

        // Configure based on quality preset
        (_resolutionScale, _colorDepth, _skipDuplicates, _duplicateThreshold) = quality switch
        {
            GifQualityPreset.Original => (1.0, 256, false, 0.0),              // 원본: 100%, 스킵 안함
            GifQualityPreset.SkipFrames => (1.0, 256, true, 0.01),            // 중복 스킵: 100%, 1% 이하 스킵
            GifQualityPreset.SkipFramesHalfSize => (0.5, 256, true, 0.01),    // 중복 스킵+50%: 50%, 1% 이하 스킵
            _ => (1.0, 256, true, 0.01)
        };

        _captureTimer = new System.Timers.Timer(_frameDelayMs);
        _captureTimer.Elapsed += CaptureTimer_Elapsed;
        _captureTimer.AutoReset = true;
    }

    public int MaxDurationSeconds => _maxDurationSeconds;

    /// <summary>
    /// Start recording the specified region
    /// </summary>
    public void StartRecording(Rectangle region)
    {
        if (_isRecording) return;

        _captureRegion = region;
        _frames.Clear();
        _previousFrame?.Dispose();
        _previousFrame = null;
        _duplicateFrameCount = 0;
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

        // Check max duration limit
        if (RecordingDuration.TotalSeconds >= _maxDurationSeconds)
        {
            _isRecording = false;
            _captureTimer.Stop();
            MaxDurationReached?.Invoke();
            return;
        }

        try
        {
            var frame = CaptureFrame();
            if (frame != null)
            {
                ProcessFrame(frame);
                RecordingProgress?.Invoke(RecordingDuration);
            }
        }
        catch
        {
            // Skip frame on error
        }
    }

    private void ProcessFrame(Bitmap frame)
    {
        lock (_frames)
        {
            // Check for duplicate frame
            if (_skipDuplicates && _previousFrame != null)
            {
                double difference = CalculateFrameDifference(frame, _previousFrame);

                if (difference < _duplicateThreshold)
                {
                    // Frame is similar to previous, extend previous frame duration
                    _duplicateFrameCount++;
                    if (_frames.Count > 0)
                    {
                        var last = _frames[^1];
                        _frames[^1] = (last.frame, last.duration + _frameDelayMs);
                    }
                    frame.Dispose();
                    return;
                }
            }

            // Store frame with duration
            _frames.Add((frame, _frameDelayMs));

            // Update previous frame reference
            _previousFrame?.Dispose();
            _previousFrame = (Bitmap)frame.Clone();
        }
    }

    /// <summary>
    /// Calculate difference between two frames (0.0 = identical, 1.0 = completely different)
    /// </summary>
    private static double CalculateFrameDifference(Bitmap frame1, Bitmap frame2)
    {
        if (frame1.Width != frame2.Width || frame1.Height != frame2.Height)
            return 1.0;

        // Sample pixels for performance (check every 10th pixel)
        int sampleStep = 10;
        int totalSamples = 0;
        long totalDifference = 0;

        var rect = new Rectangle(0, 0, frame1.Width, frame1.Height);

        BitmapData? data1 = null;
        BitmapData? data2 = null;

        try
        {
            data1 = frame1.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            data2 = frame2.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int bytesPerPixel = 3;
            int stride = data1.Stride;

            unsafe
            {
                byte* ptr1 = (byte*)data1.Scan0;
                byte* ptr2 = (byte*)data2.Scan0;

                for (int y = 0; y < frame1.Height; y += sampleStep)
                {
                    for (int x = 0; x < frame1.Width; x += sampleStep)
                    {
                        int offset = y * stride + x * bytesPerPixel;

                        int diff = Math.Abs(ptr1[offset] - ptr2[offset]) +
                                   Math.Abs(ptr1[offset + 1] - ptr2[offset + 1]) +
                                   Math.Abs(ptr1[offset + 2] - ptr2[offset + 2]);

                        totalDifference += diff;
                        totalSamples++;
                    }
                }
            }
        }
        finally
        {
            if (data1 != null) frame1.UnlockBits(data1);
            if (data2 != null) frame2.UnlockBits(data2);
        }

        if (totalSamples == 0) return 0;

        // Normalize: max difference per pixel is 255*3 = 765
        return totalDifference / (totalSamples * 765.0);
    }

    private Bitmap? CaptureFrame()
    {
        try
        {
            // Capture at original resolution
            using var originalBitmap = new Bitmap(_captureRegion.Width, _captureRegion.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(originalBitmap))
            {
                graphics.CopyFromScreen(
                    _captureRegion.Left,
                    _captureRegion.Top,
                    0, 0,
                    _captureRegion.Size,
                    CopyPixelOperation.SourceCopy);
            }

            // If no scaling needed, return clone
            if (_resolutionScale >= 1.0)
            {
                return (Bitmap)originalBitmap.Clone();
            }

            // Scale down for smaller file size
            int scaledWidth = (int)(_captureRegion.Width * _resolutionScale);
            int scaledHeight = (int)(_captureRegion.Height * _resolutionScale);

            var scaledBitmap = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(scaledBitmap))
            {
                graphics.InterpolationMode = InterpolationMode.Bilinear;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.SmoothingMode = SmoothingMode.HighSpeed;
                graphics.DrawImage(originalBitmap, 0, 0, scaledWidth, scaledHeight);
            }

            return scaledBitmap;
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

            // Create animated GIF with variable frame durations
            lock (_frames)
            {
                using var gif = AnimatedGif.AnimatedGif.Create(finalPath, _frameDelayMs);

                foreach (var (frame, duration) in _frames)
                {
                    // Add frame with its specific duration (256 colors)
                    gif.AddFrame(frame, delay: duration, quality: GifQuality.Bit8);
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
            foreach (var (frame, _) in _frames)
            {
                frame.Dispose();
            }
            _frames.Clear();
        }
        _previousFrame?.Dispose();
        _previousFrame = null;
    }

    public void Dispose()
    {
        _captureTimer.Stop();
        _captureTimer.Dispose();
        ClearFrames();
    }
}
