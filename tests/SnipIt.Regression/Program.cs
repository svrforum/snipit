using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipIt.Models;
using SnipIt.Services;
using SnipIt.Views;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethod(method, Private)!.Invoke(target, args);

    [STAThread]
    private static void Main(string[] args)
    {
        // Isolated files only; never touch the user's capture history.
        var directory = Path.Combine(Path.GetTempPath(), "SnipIt-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Environment.SetEnvironmentVariable("SNIPIT_DATA_DIRECTORY", directory);
        try
        {
            using var history = (CaptureHistoryService)Activator.CreateInstance(
                typeof(CaptureHistoryService), Private, null, [directory], null)!;
            using var bitmap = new Bitmap(40, 30, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(System.Drawing.Color.CornflowerBlue);
            var first = history.AddCapture(bitmap);
            var snapshot = history.History;
            history.AddCapture(bitmap);
            Check(snapshot.Count == 1 && history.History.Count == 2, "History snapshots stay stable after new captures");
            var thumbnail = history.LoadThumbnail(first);
            Check(thumbnail is { IsFrozen: true } && ReferenceEquals(thumbnail, history.LoadThumbnail(first)),
                "Thumbnails are frozen and reused");
            using (var loaded = history.LoadImageAsync(first).GetAwaiter().GetResult())
                Check(loaded?.GetPixel(0, 0).ToArgb() == System.Drawing.Color.CornflowerBlue.ToArgb(), "Async history loading preserves pixels");
            Check(File.Exists(Path.Combine(directory, "index.json")) && !File.Exists(Path.Combine(directory, "index.json.tmp")),
                "History index is atomically replaced");
            using (var reopened = (CaptureHistoryService)Activator.CreateInstance(typeof(CaptureHistoryService), Private, null, [directory], null)!)
                Check(reopened.History.Count == 2, "History index survives reopening");

            using (var recorder = new GifRecorderService())
            {
                Call(recorder, "ProcessFrame", bitmap.Clone());
                Call(recorder, "ProcessFrame", bitmap.Clone());
                Check(recorder.FrameCount == 1 && recorder.SkippedFrames == 1, "Duplicate GIF frames merge");
                var frames = (List<(Bitmap frame, int duration)>)typeof(GifRecorderService).GetField("_frames", Private)!.GetValue(recorder)!;
                Check(frames[0].duration == 66, "Merged GIF frames preserve duration");
                Check(ReferenceEquals(frames[0].frame, typeof(GifRecorderService).GetField("_previousFrame", Private)!.GetValue(recorder)),
                    "GIF comparison reuses the stored frame without cloning");
                recorder.CancelRecording();
                Check(recorder.FrameCount == 0, "Cancel releases all GIF frames");
            }
            using (var recorder = new GifRecorderService(quality: GifQualityPreset.Original))
            {
                Call(recorder, "ProcessFrame", bitmap.Clone());
                typeof(GifRecorderService).GetField("_frameBytes", Private)!.SetValue(recorder, 256L * 1024 * 1024);
                Call(recorder, "ProcessFrame", bitmap.Clone());
                Check(recorder.FrameCount == 1 && (bool)typeof(GifRecorderService).GetField("_memoryLimitReached", Private)!.GetValue(recorder)!,
                    "GIF memory limit rejects additional frame allocation");
            }
            using (var effects = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(effects)) graphics.Clear(System.Drawing.Color.Blue);
                effects.SetPixel(15, 15, System.Drawing.Color.Red);
                SnipIt.Utils.ImageProcessingHelper.ApplyBlur(effects, new System.Drawing.Rectangle(12, 12, 4, 4));
                Check(effects.GetPixel(0, 0).ToArgb() == System.Drawing.Color.Blue.ToArgb() && effects.GetPixel(15, 15).A == 255,
                    "Blur at bottom-right edge preserves pixels outside the region and alpha");
                effects.SetPixel(5, 0, System.Drawing.Color.Red);
                SnipIt.Utils.ImageProcessingHelper.ApplyMosaic(effects, new System.Drawing.Rectangle(-4, 0, 8, 8));
                Check(effects.GetPixel(5, 0).ToArgb() == System.Drawing.Color.Red.ToArgb(), "Negative mosaic bounds are clipped without extending the edited region");
            }
            using (var recorder = new GifRecorderService(maxDurationSeconds: 1))
            {
                // Simulate the time limit without recording the user's screen or opening a save dialog.
                typeof(GifRecorderService).GetField("_isRecording", Private)!.SetValue(recorder, true);
                typeof(GifRecorderService).GetField("_recordingStartTime", Private)!.SetValue(recorder, DateTime.Now.AddSeconds(-2));
                bool reached = false;
                recorder.MaxDurationReached += () => reached = true;
                Call(recorder, "CaptureTimer_Elapsed", null, null);
                Check(reached && recorder.IsRecording, "Time limit keeps recording eligible for saving");
                bool stopEntered = false;
                recorder.RecordingError += _ => stopEntered = true;
                recorder.StopRecordingAsync().GetAwaiter().GetResult();
                Check(stopEntered && !recorder.IsRecording, "Stop executes after automatic time limit");
            }

            var app = new SnipIt.App();
            app.InitializeComponent();
            AppSettingsConfig.Instance.CopyToClipboard = false;
            var original = new Bitmap(80, 60);
            using (var graphics = Graphics.FromImage(original)) graphics.Clear(System.Drawing.Color.Red);
            var editor = new EditorWindow(original);
            Call(editor, "LoadImage", original);
            Call(editor, "SaveState");
            Call(editor, "Undo");
            var redo = (LinkedList<Bitmap>)typeof(EditorWindow).GetField("_redoStack", Private)!.GetValue(editor)!;
            Check(ReferenceEquals(redo.First!.Value, original), "Undo transfers image ownership without a clone");
            Call(editor, "Redo");
            Check(ReferenceEquals(typeof(EditorWindow).GetField("_originalBitmap", Private)!.GetValue(editor), original),
                "Redo restores the same image instance");
            var canvas = (Canvas)editor.FindName("DrawingCanvas");
            canvas.Measure(new System.Windows.Size(80, 60));
            canvas.Arrange(new Rect(0, 0, 80, 60));
            using (var rendered = (Bitmap)Call(editor, "RenderFinalImage")!)
                Check(rendered.GetPixel(40, 30).ToArgb() == System.Drawing.Color.Red.ToArgb(), "Direct native render preserves image pixels");
            if (args.Length > 0)
            {
                var editorContent = (FrameworkElement)editor.Content;
                ((Grid)editorContent).Background = editor.Background;
                editorContent.Measure(new System.Windows.Size(1150, 700));
                editorContent.Arrange(new Rect(0, 0, 1150, 700));
                editorContent.UpdateLayout();
                SavePreview(editorContent, 1150, 700, Path.Combine(args[0], "editor-window.png"));
                var settings = new SettingsWindow();
                Call(settings, "LoadSettings");
                var settingsContent = (FrameworkElement)settings.Content;
                ((Grid)settingsContent).Background = settings.Background;
                settingsContent.Measure(new System.Windows.Size(760, 680));
                settingsContent.Arrange(new Rect(0, 0, 760, 680));
                settingsContent.UpdateLayout();
                SavePreview(settingsContent, 760, 680, Path.Combine(args[0], "settings-window.png"));
                settings.Close();
            }
            editor.Close();
            var main = new MainWindow();
            Call(main, "RefreshShortcutLabels");
            Check(((TextBlock)main.FindName("RegionShortcutText")).Text == AppSettingsConfig.Instance.RegionHotkey.ToString(),
                "Main window displays configured hotkeys");
            var content = (FrameworkElement)main.Content;
            ((Grid)content).Background = main.Background;
            foreach (int width in new[] { 480, 580 })
            {
                content.Measure(new System.Windows.Size(width, 740));
                content.Arrange(new Rect(0, 0, width, 740));
                content.UpdateLayout();
                Check(content.DesiredSize.Width <= width, $"Main layout fits {width}px width");
                if (args.Length > 0)
                {
                    Directory.CreateDirectory(args[0]);
                    var render = new RenderTargetBitmap(width, 740, 96, 96, PixelFormats.Pbgra32);
                    render.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(render));
                    using var output = File.Create(Path.Combine(args[0], $"main-window-{width}.png"));
                    encoder.Save(output);
                }
            }
            history.ClearHistory();
            Check(history.History.Count == 0 && !File.Exists(first.ImagePath), "Clear history removes stored captures");
            Console.WriteLine("All regression checks passed.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void SavePreview(FrameworkElement content, int width, int height, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var render = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        render.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(render));
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
