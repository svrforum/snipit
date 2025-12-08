using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using SnipIt.Utils;

namespace SnipIt.Services;

/// <summary>
/// Service for managing capture history with async support and modern patterns
/// </summary>
public sealed class CaptureHistoryService : IDisposable
{
    private static readonly Lazy<CaptureHistoryService> _lazy = new(
        () => new CaptureHistoryService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static CaptureHistoryService Instance => _lazy.Value;

    private readonly List<CaptureHistoryItem> _history = [];
    private readonly string _historyFolder;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private const int MaxHistoryCount = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public event Action? HistoryChanged;

    public IReadOnlyList<CaptureHistoryItem> History => _history.AsReadOnly();

    private CaptureHistoryService()
    {
        _historyFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SnipIt",
            "History");

        Directory.CreateDirectory(_historyFolder);
        LoadHistory();
    }

    public CaptureHistoryItem AddCapture(Bitmap bitmap)
    {
        var item = new CaptureHistoryItem
        {
            Id = Guid.NewGuid().ToString("N"),
            CapturedAt = DateTime.Now,
            Width = bitmap.Width,
            Height = bitmap.Height
        };

        // Save full image as PNG for lossless quality
        var imagePath = Path.Combine(_historyFolder, $"{item.Id}.png");
        bitmap.Save(imagePath, ImageFormat.Png);
        item.ImagePath = imagePath;

        // Create and save thumbnail using high-performance helper
        var thumbnailPath = Path.Combine(_historyFolder, $"{item.Id}_thumb.jpg");
        using (var thumbnail = ImageProcessingHelper.CreateThumbnail(bitmap, 160, 100))
        {
            SaveJpeg(thumbnail, thumbnailPath, 85);
        }
        item.ThumbnailPath = thumbnailPath;

        // Add to history with lock for thread safety
        _lock.Wait();
        try
        {
            _history.Insert(0, item);

            // Remove old items if exceeding max
            while (_history.Count > MaxHistoryCount)
            {
                var oldItem = _history[^1];
                DeleteHistoryItemFiles(oldItem);
                _history.RemoveAt(_history.Count - 1);
            }

            SaveHistoryIndex();
        }
        finally
        {
            _lock.Release();
        }

        HistoryChanged?.Invoke();
        return item;
    }

    public async Task<CaptureHistoryItem> AddCaptureAsync(Bitmap bitmap, CancellationToken cancellationToken = default)
    {
        var item = new CaptureHistoryItem
        {
            Id = Guid.NewGuid().ToString("N"),
            CapturedAt = DateTime.Now,
            Width = bitmap.Width,
            Height = bitmap.Height
        };

        var imagePath = Path.Combine(_historyFolder, $"{item.Id}.png");
        var thumbnailPath = Path.Combine(_historyFolder, $"{item.Id}_thumb.jpg");

        // Save on background thread
        await Task.Run(() =>
        {
            // Save as PNG for lossless quality
            bitmap.Save(imagePath, ImageFormat.Png);
            using var thumbnail = ImageProcessingHelper.CreateThumbnail(bitmap, 160, 100);
            SaveJpeg(thumbnail, thumbnailPath, 85);
        }, cancellationToken);

        item.ImagePath = imagePath;
        item.ThumbnailPath = thumbnailPath;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _history.Insert(0, item);

            while (_history.Count > MaxHistoryCount)
            {
                var oldItem = _history[^1];
                DeleteHistoryItemFiles(oldItem);
                _history.RemoveAt(_history.Count - 1);
            }

            await SaveHistoryIndexAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        HistoryChanged?.Invoke();
        return item;
    }

    public Bitmap? LoadImage(CaptureHistoryItem item)
    {
        if (!File.Exists(item.ImagePath))
            return null;

        // GDI+ Bitmap keeps a reference to the underlying stream.
        // To create a fully independent bitmap that survives stream disposal,
        // we must clone it to a new bitmap with explicit pixel format.
        using var fileStream = new FileStream(item.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var tempBitmap = new Bitmap(fileStream);

        // Create a new independent bitmap by drawing the original onto it
        var result = new Bitmap(tempBitmap.Width, tempBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.DrawImage(tempBitmap, 0, 0, tempBitmap.Width, tempBitmap.Height);
        }
        return result;
    }

    public async Task<Bitmap?> LoadImageAsync(CaptureHistoryItem item, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(item.ImagePath))
            return null;

        return await Task.Run(() =>
        {
            // Create a fully independent bitmap that doesn't rely on stream
            using var fileStream = new FileStream(item.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var tempBitmap = new Bitmap(fileStream);

            var result = new Bitmap(tempBitmap.Width, tempBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.DrawImage(tempBitmap, 0, 0, tempBitmap.Width, tempBitmap.Height);
            }
            return result;
        }, cancellationToken);
    }

    public BitmapImage? LoadThumbnail(CaptureHistoryItem item)
    {
        if (!File.Exists(item.ThumbnailPath))
            return null;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(item.ThumbnailPath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public void DeleteHistoryItem(CaptureHistoryItem item, bool removeFromList = true)
    {
        DeleteHistoryItemFiles(item);

        if (removeFromList)
        {
            _lock.Wait();
            try
            {
                _history.Remove(item);
                SaveHistoryIndex();
            }
            finally
            {
                _lock.Release();
            }
            HistoryChanged?.Invoke();
        }
    }

    public void ClearHistory()
    {
        _lock.Wait();
        try
        {
            foreach (var item in _history)
            {
                DeleteHistoryItemFiles(item);
            }
            _history.Clear();
            SaveHistoryIndex();
        }
        finally
        {
            _lock.Release();
        }
        HistoryChanged?.Invoke();
    }

    private static void DeleteHistoryItemFiles(CaptureHistoryItem item)
    {
        try
        {
            if (File.Exists(item.ImagePath))
                File.Delete(item.ImagePath);
            if (File.Exists(item.ThumbnailPath))
                File.Delete(item.ThumbnailPath);
        }
        catch
        {
            // Ignore deletion errors
        }
    }

    private void SaveJpeg(Bitmap bitmap, string path, int quality)
    {
        var encoder = GetJpegEncoder();
        if (encoder is null)
        {
            bitmap.Save(path, ImageFormat.Jpeg);
            return;
        }

        using var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
        bitmap.Save(path, encoder, encoderParams);
    }

    private static ImageCodecInfo? GetJpegEncoder()
    {
        return ImageCodecInfo.GetImageDecoders()
            .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
    }

    private void LoadHistory()
    {
        var indexPath = Path.Combine(_historyFolder, "index.json");
        if (!File.Exists(indexPath))
            return;

        try
        {
            var json = File.ReadAllText(indexPath);
            var items = JsonSerializer.Deserialize<List<CaptureHistoryItem>>(json, JsonOptions);

            if (items is not null)
            {
                foreach (var item in items.Where(i => File.Exists(i.ImagePath) && File.Exists(i.ThumbnailPath)))
                {
                    _history.Add(item);
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    private void SaveHistoryIndex()
    {
        var indexPath = Path.Combine(_historyFolder, "index.json");
        try
        {
            var json = JsonSerializer.Serialize(_history, JsonOptions);
            File.WriteAllText(indexPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private async Task SaveHistoryIndexAsync(CancellationToken cancellationToken = default)
    {
        var indexPath = Path.Combine(_historyFolder, "index.json");
        try
        {
            var json = JsonSerializer.Serialize(_history, JsonOptions);
            await File.WriteAllTextAsync(indexPath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}

/// <summary>
/// Capture history item record with improved immutability
/// </summary>
public sealed class CaptureHistoryItem
{
    public required string Id { get; init; }
    public DateTime CapturedAt { get; init; }
    public string ImagePath { get; set; } = "";
    public string ThumbnailPath { get; set; } = "";
    public int Width { get; init; }
    public int Height { get; init; }

    public string DisplayTime => CapturedAt.ToString("HH:mm:ss");
    public string DisplayDate => CapturedAt.ToString("MM/dd");
    public string DisplaySize => $"{Width}x{Height}";
}
