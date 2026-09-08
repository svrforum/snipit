using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text.Json;
using SnipIt.Services;
using SnipIt.Views;

internal static class PerformanceBenchmarks
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void CaptureMemory(string output)
    {
        using var recorder = new GifRecorderService(quality: SnipIt.Models.GifQualityPreset.Original);
        var bounds = ScreenCaptureService.GetVirtualScreenBoundsPhysical();
        typeof(GifRecorderService).GetField("_captureRegion", Private)!.SetValue(recorder, new Rectangle(bounds.X, bounds.Y, 1280, 720));
        var capture = typeof(GifRecorderService).GetMethod("CaptureFrame", Private)!;
        var append = typeof(GifRecorderService).GetMethod("ProcessFrame", Private)!;
        var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 90; i++) append.Invoke(recorder, [capture.Invoke(recorder, null)]);
        process.Refresh();
        var retained = process.PrivateMemorySize64 / 1048576.0;
        var cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
        recorder.CancelRecording(); process.Refresh();
        File.WriteAllText(output, JsonSerializer.Serialize(new { retainedMiB = retained,
            afterCancelMiB = process.PrivateMemorySize64 / 1048576.0, cpuMs, elapsedMs = watch.Elapsed.TotalMilliseconds }));
    }
    public static void LiveGif(string output)
    {
        using var recorder = new GifRecorderService(maxDurationSeconds: 60);
        var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var bounds = ScreenCaptureService.GetVirtualScreenBoundsPhysical();
        var region = new Rectangle(bounds.X, bounds.Y, Math.Min(1280, bounds.Width), Math.Min(720, bounds.Height));
        var samples = new List<object>();
        recorder.StartRecording(region);
        for (int i = 0; i < 6; i++)
        {
            Thread.Sleep(10000);
            process.Refresh();
            samples.Add(new { seconds = (i + 1) * 10, privateMiB = process.PrivateMemorySize64 / 1048576.0,
                frames = recorder.FrameCount, skipped = recorder.SkippedFrames,
                cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds });
            Console.WriteLine($"GIF {(i + 1) * 10}s: {recorder.FrameCount} retained, {recorder.SkippedFrames} skipped");
        }
        recorder.CancelRecording();
        File.WriteAllText(output, JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static void Run(string output)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnipIt-perf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("SNIPIT_DATA_DIRECTORY", root);
        var results = new List<object>();
        var process = Process.GetCurrentProcess();
        void Measure(string name, int count, Action action)
        {
            var cpu = process.TotalProcessorTime;
            var allocated = GC.GetTotalAllocatedBytes();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++) action();
            watch.Stop(); process.Refresh();
            results.Add(new { name, count, elapsedMs = watch.Elapsed.TotalMilliseconds,
                cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds,
                allocatedMiB = (GC.GetTotalAllocatedBytes() - allocated) / 1048576.0,
                privateMiB = process.PrivateMemorySize64 / 1048576.0,
                workingSetMiB = process.WorkingSet64 / 1048576.0 });
            Console.WriteLine($"BENCH {name}: {watch.Elapsed.TotalMilliseconds / count:F2} ms/op");
        }
        try
        {
            var app = new SnipIt.App(); app.InitializeComponent();
            SnipIt.Models.AppSettingsConfig.Instance.CopyToClipboard = false;
            using var history = new CaptureHistoryService(root);
            using var source = new Bitmap(3840, 2160, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(source)) graphics.Clear(Color.CornflowerBlue);
            Measure("4K PNG save", 10, () => history.AddCapture(source));
            var item = history.History[0];
            Measure("4K PNG reopen", 10, () => { using var image = history.LoadImage(item); });
            Measure("4K editor 15 undo states and close", 5, () =>
            {
                var editor = new EditorWindow((Bitmap)source.Clone());
                for (int i = 0; i < 15; i++) typeof(EditorWindow).GetMethod("SaveState", Private)!.Invoke(editor, null);
                editor.Close();
            });
            // Real screen pixels are immediately disposed, never saved or displayed.
            Measure("Screen capture and dispose", 30, () => { using var image = ScreenCaptureService.CaptureFullScreen(); });
            using var portrait = new Bitmap(200, 4000);
            using (var graphics = Graphics.FromImage(portrait)) graphics.Clear(Color.LightBlue);
            for (int i = 0; i < 100; i++) history.AddCapture(portrait);
            Measure("100 portrait thumbnails cold", 1, () => { foreach (var entry in history.History) history.LoadThumbnail(entry); });
            var pixels = history.History.Sum(entry => { var thumb = history.LoadThumbnail(entry)!; return (long)thumb.PixelWidth * thumb.PixelHeight; });
            results.Add(new { name = "Decoded thumbnail pixel storage", MiB = pixels * 4 / 1048576.0 });
            Measure("100 thumbnails warm", 20, () => { foreach (var entry in history.History) history.LoadThumbnail(entry); });
            using var frame = new Bitmap(1920, 1080, PixelFormat.Format24bppRgb);
            using var recorder = new GifRecorderService();
            var method = typeof(GifRecorderService).GetMethod("ProcessFrame", Private)!;
            Measure("1080p static GIF 1800 frames (60s at 30fps)", 1800, () => method.Invoke(recorder, [(Bitmap)frame.Clone()]));
            results.Add(new { name = "GIF retained frames", frames = recorder.FrameCount, skipped = recorder.SkippedFrames });
            recorder.CancelRecording();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Measure("After disposal and diagnostic GC", 1, () => { });
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Directory.Delete(root, true); }
    }
}
