using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SnipIt.Services;
using SnipIt.Models;
namespace SnipIt.Views;
internal sealed class RecordingWindow : Window
{
    private readonly GifRecorderService recorder; private bool saving, allowClose;
    private readonly RecordingBorder border;
    private readonly CancellationTokenSource countdown = new();
    private bool started, closed;
    public RecordingWindow(System.Drawing.Rectangle region)
    {
        Title = Ui.L("SnipIt GIF 녹화"); var panel = new StackPanel { Padding = new Thickness(16), Spacing = 8 }; Content = new ContentControl { Content = panel }; var status = Ui.Text(Ui.L("녹화 중…"), 18); panel.Children.Add(status); var stop = Ui.AsyncButton(Ui.L("녹화 완료 · 저장"), Stop, true); panel.Children.Add(stop); panel.Children.Add(Ui.Button(Ui.L("취소"), Cancel));
        var config = AppSettingsConfig.Instance; recorder = new GifRecorderService(config.GifFps, config.GifQuality, config.GifMaxDurationSeconds);
        border = new RecordingBorder(region);
        recorder.SavePathProvider = () => Ui.OnUi(() => Ui.SaveFile(this, "GIF|*.gif", $"SnipIt_{DateTime.Now:yyyyMMdd_HHmmss}.gif")).GetAwaiter().GetResult();
        recorder.RecordingProgress += elapsed => App.Queue.TryEnqueue(() => status.Text = Ui.L("녹화") + $" {elapsed:mm\\:ss} · {recorder.FrameCount} " + Ui.L("프레임"));
        recorder.MaxDurationReached += () => App.Queue.TryEnqueue(async () => await Stop()); recorder.RecordingError += message => App.Notify(message);
        AppWindow.Closing += (_, e) => { if (saving && !allowClose) e.Cancel = true; };
        Closed += (_, _) => { closed = true; countdown.Cancel(); border.Dispose(); recorder.Dispose(); countdown.Dispose(); App.Recordings--; };
        panel.KeyDown += async (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) { e.Handled = true; if (started) await Stop(); else Cancel(); } };
        panel.Loaded += (_, _) => stop.Focus(FocusState.Programmatic);
        Ui.Size(this, 290, 190); App.Recordings++;
        if (!CaptureVisibility.Exclude(this)) App.Notify(Ui.L("녹화 창을 캡처 영역 밖으로 옮겨 주세요."));
        _ = BeginRecording(region, status);
    }
    private async Task BeginRecording(System.Drawing.Rectangle region, TextBlock status)
    {
        try
        {
            var token = countdown.Token;
            for (var remaining = 3; remaining > 0; remaining--)
            {
                status.Text = Ui.L("녹화") + $" · {remaining}";
                await Task.Delay(1000, token);
            }
            if (closed) return;
            recorder.StartRecording(region); started = true; status.Text = Ui.L("녹화 중…");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { App.Notify(ex.Message); if (!closed) Cancel(); }
    }
    private void Cancel() { if (saving) return; recorder.CancelRecording(); border.Dispose(); Close(); }
    private async Task Stop() { if (saving || closed) return; if (!started) { Cancel(); return; } saving = true; border.Dispose(); ((ContentControl)Content).IsEnabled = false; try { await recorder.StopRecordingAsync(); allowClose = true; Close(); } finally { saving = false; if (!closed) ((ContentControl)Content).IsEnabled = true; } }
#if SNIPIT_SMOKE_TESTS
    internal static async Task VerifyLifecycle(string folder)
    {
        var before = App.Recordings;
        var region = new System.Drawing.Rectangle(100,100,160,100);
        var cancelled = new RecordingWindow(region); cancelled.Activate();
        await Task.Delay(100);
        if (cancelled.started || cancelled.recorder.FrameCount != 0) throw new Exception("GIF recorded before countdown");
        cancelled.Cancel();
        if (App.Recordings != before) throw new Exception("Cancelled GIF window retained resources");
        var recording = new RecordingWindow(region); recording.Activate();
        var path = System.IO.Path.Combine(folder,"window-recording.gif"); recording.recorder.SavePathProvider = () => path;
        await Task.Delay(3400);
        if (!recording.started || recording.recorder.FrameCount == 0) throw new Exception("GIF did not start after countdown");
        await recording.Stop();
        using var image = new System.Drawing.Bitmap(path);
        if (image.Size != region.Size || App.Recordings != before || !recording.closed) throw new Exception("GIF window stop/save/close failed");
    }
#endif
}
