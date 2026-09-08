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
    private const long MaxFrameBytes = 256L * 1024 * 1024;
    private long _frameBytes;
    private bool _limitReached;
    private bool _memoryLimitReached;

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
        ClearFrames();
        _duplicateFrameCount = 0;
        _limitReached = false;
        _memoryLimitReached = false;
        _isRecording = true;
        _recordingStartTime = DateTime.Now;
        _captureTimer.Start();
    }

    /// <summary>
    /// Stop recording and save the GIF
    /// </summary>
    public async Task<string?> StopRecordingAsync()
    {
        lock (_frames)
        {
            if (!_isRecording) return null;
            _isRecording = false;
            _captureTimer.Stop();
        }

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
        lock (_frames)
        {
            _isRecording = false;
            _captureTimer.Stop();
            ClearFrames();
        }
    }

    private void CaptureTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Drop overlapping timer callbacks instead of queuing more screen copies.
        if (!Monitor.TryEnter(_frames)) return;
        bool limitReached = false;
        TimeSpan? progress = null;
        try
        {
            if (!_isRecording || _limitReached) return;
            if (RecordingDuration.TotalSeconds >= _maxDurationSeconds)
            {
                _captureTimer.Stop();
                // StopRecordingAsync still owns the transition to the saving state.
                limitReached = true;
                _limitReached = true;
            }
            else
            {
                var frame = CaptureFrame();
                if (frame != null)
                {
                    ProcessFrame(frame);
                    progress = RecordingDuration;
                    if (_memoryLimitReached)
                    {
                        _captureTimer.Stop();
                        limitReached = _limitReached = true;
                    }
                }
            }
        }
        catch
        {
            // Skip frame on error
        }
        finally
        {
            Monitor.Exit(_frames);
        }
        // Subscribers can dispatch to the UI and stop recording: never invoke under the lock.
        if (limitReached) MaxDurationReached?.Invoke();
        else if (progress is { } elapsed) RecordingProgress?.Invoke(elapsed);
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

            long frameBytes = ((long)frame.Width * Image.GetPixelFormatSize(frame.PixelFormat) + 31) / 32 * 4 * frame.Height;
            if (_frames.Count > 0 && _frameBytes + frameBytes > MaxFrameBytes)
            {
                frame.Dispose();
                _memoryLimitReached = true;
                return;
            }
            // Drawing on a Bitmap retains a native GDI backing surface. Store a
            // pixel-only bitmap, but only after duplicate/limit checks avoid this copy.
            Bitmap storedFrame;
            try { storedFrame = DetachPixels(frame); }
            finally { frame.Dispose(); }
            frame = storedFrame;
            _frameBytes += frameBytes;
            // Store frame with duration
            _frames.Add((frame, _frameDelayMs));

            // Update previous frame reference
            // Stored frames are immutable and owned by _frames; no full-size clone needed.
            _previousFrame = frame;
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
            var format = frame1.PixelFormat == frame2.PixelFormat && Image.GetPixelFormatSize(frame1.PixelFormat) == 32 ? frame1.PixelFormat : PixelFormat.Format24bppRgb;
            data1 = frame1.LockBits(rect, ImageLockMode.ReadOnly, format);
            data2 = frame2.LockBits(rect, ImageLockMode.ReadOnly, format);

            int bytesPerPixel = Image.GetPixelFormatSize(format) / 8;
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
                        int offset2 = y * data2.Stride + x * bytesPerPixel;

                        int diff = Math.Abs(ptr1[offset] - ptr2[offset2]) +
                                   Math.Abs(ptr1[offset + 1] - ptr2[offset2 + 1]) +
                                   Math.Abs(ptr1[offset + 2] - ptr2[offset2 + 2]);

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
        Bitmap? originalBitmap = null;
        Bitmap? scaledBitmap = null;
        try
        {
            // Capture at original resolution
            originalBitmap = new Bitmap(_captureRegion.Width, _captureRegion.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(originalBitmap))
            {
                graphics.CopyFromScreen(
                    _captureRegion.Left,
                    _captureRegion.Top,
                    0, 0,
                    _captureRegion.Size,
                    CopyPixelOperation.SourceCopy);
            }

            // Transfer ownership; accepted frames detach pixels in ProcessFrame.
            if (_resolutionScale >= 1.0)
            {
                var result = originalBitmap;
                originalBitmap = null;
                return result;
            }

            // Scale down for smaller file size
            int scaledWidth = Math.Max(1, (int)(_captureRegion.Width * _resolutionScale));
            int scaledHeight = Math.Max(1, (int)(_captureRegion.Height * _resolutionScale));

            scaledBitmap = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(scaledBitmap))
            {
                graphics.InterpolationMode = InterpolationMode.Bilinear;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.SmoothingMode = SmoothingMode.HighSpeed;
                graphics.DrawImage(originalBitmap, 0, 0, scaledWidth, scaledHeight);
            }

            var scaledResult = scaledBitmap;
            scaledBitmap = null;
            return scaledResult;
        }
        catch
        {
            return null;
        }
        finally
        {
            originalBitmap?.Dispose();
            scaledBitmap?.Dispose();
        }
    }

    // Copy pixels without carrying GDI's drawing-surface backing buffers into stored frames.
    private static unsafe Bitmap DetachPixels(Bitmap source)
    {
        var result = new Bitmap(source.Width, source.Height, source.PixelFormat);
        var rect = new Rectangle(0, 0, source.Width, source.Height);
        try
        {
            var input = source.LockBits(rect, ImageLockMode.ReadOnly, source.PixelFormat);
            try
            {
                var output = result.LockBits(rect, ImageLockMode.WriteOnly, result.PixelFormat);
                try
                {
                    var bytes = source.Width * Image.GetPixelFormatSize(source.PixelFormat) / 8;
                    for (int y = 0; y < source.Height; y++)
                        Buffer.MemoryCopy((byte*)input.Scan0 + y * input.Stride,
                            (byte*)output.Scan0 + y * output.Stride, Math.Abs(output.Stride), bytes);
                }
                finally { result.UnlockBits(output); }
            }
            finally { source.UnlockBits(input); }
            return result;
        }
        catch { result.Dispose(); throw; }
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
                    Title = _memoryLimitReached ? "메모리 한도 도달 — 녹화한 GIF 저장" : "녹화한 GIF 저장",
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
            _frameBytes = 0;
            _previousFrame = null;
        }
    }

    public void Dispose()
    {
        CancelRecording();
        _captureTimer.Dispose();
    }
}
