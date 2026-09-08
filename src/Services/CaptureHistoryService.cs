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

    public IReadOnlyList<CaptureHistoryItem> History
    {
        get
        {
            _lock.Wait();
            try { return _history.ToArray(); }
            finally { _lock.Release(); }
        }
    }

    private CaptureHistoryService() : this(Path.Combine(
        AppDataPaths.GetFolder(Environment.SpecialFolder.LocalApplicationData), "History"))
    {
    }

    internal CaptureHistoryService(string historyFolder)
    {
        _historyFolder = historyFolder;

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
        var thumbnailPath = Path.Combine(_historyFolder, $"{item.Id}_thumb.png");
        using (var thumbnail = ImageProcessingHelper.CreateThumbnail(bitmap, 480, 300))
        {
            thumbnail.Save(thumbnailPath, ImageFormat.Png);
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

    public Task<CaptureHistoryItem> AddCaptureAsync(Bitmap bitmap, CancellationToken cancellationToken = default)
    {
        // Once saving starts, commit the image and index together. The caller owns the
        // bitmap and must keep it alive until this task completes.
        return Task.Run(() => AddCapture(bitmap), cancellationToken);
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

        return tempBitmap.Clone(new Rectangle(0, 0, tempBitmap.Width, tempBitmap.Height), PixelFormat.Format32bppArgb);
    }

    public Task<Bitmap?> LoadImageAsync(CaptureHistoryItem item, CancellationToken cancellationToken = default) =>
        Task.Run(() => LoadImage(item), cancellationToken);
    public BitmapImage? LoadThumbnail(CaptureHistoryItem item)
    {
        if (item.CachedThumbnail is { } cached) return cached;
        var path = item.ThumbnailPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && File.Exists(item.ThumbnailPath) ? item.ThumbnailPath : item.ImagePath;
        if (!File.Exists(path))
            return null;

        try
        {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = Math.Min(Math.Max(item.Width, 1), 480);
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        item.CachedThumbnail = bitmap;
        return bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException)
        {
            return null;
        }
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
                foreach (var item in items.Where(i => File.Exists(i.ImagePath)))
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
            var temporaryPath = indexPath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, indexPath, overwrite: true);
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
    // Frozen thumbnails can be reused across editor windows without retaining file handles.
    internal BitmapImage? CachedThumbnail { get; set; }
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
