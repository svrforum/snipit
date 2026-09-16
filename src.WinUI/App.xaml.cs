using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using SnipIt.Models;
using SnipIt.Services;
using SnipIt.Views;
using System.Drawing;
using Windows.ApplicationModel.DataTransfer;
namespace SnipIt;
internal static class Program
{
    internal static SingleInstance? Instance;
    internal static bool StartedByWindows;
    internal static bool ShouldStartInTray(string[] args, bool startMinimized) => startMinimized && args.Contains("--startup", StringComparer.OrdinalIgnoreCase);
    [STAThread]
    static void Main(string[] args)
    {
        if (args.FirstOrDefault() == "--apply-update") { try { UpdateInstaller.RunHelperAsync(args).GetAwaiter().GetResult(); } catch (Exception ex) { System.Windows.Forms.MessageBox.Show(ex.Message, Ui.L("SnipIt 업데이트")); Environment.ExitCode = 1; } return; }
        StartedByWindows = args.Contains("--startup", StringComparer.OrdinalIgnoreCase);
        using var instance = new SingleInstance();
        if (!instance.IsPrimary) { instance.ActivateExisting(); return; }
        Instance = instance;
        WinRT.ComWrappersSupport.InitializeComWrappers(); Application.Start(_ => { SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread())); new App(); });
    }
}
public sealed partial class App : Application
{
    internal static DispatcherQueue Queue { get; private set; } = null!;
    internal static MainWindow Main { get; private set; } = null!;
    internal static readonly List<EditorWindow> Editors = new();
    internal static bool Busy { get; set; }
    internal static bool Exiting;
    private static Hotkeys? hotkeys;
    private static System.Windows.Forms.NotifyIcon? tray;
    private static Bitmap? silent;
    internal static int Recordings, SettingsWindows;
    private static UpdateWindow? updateWindow;
    internal static void ShowUpdates()
    {
        if (updateWindow == null) { updateWindow = new UpdateWindow(); updateWindow.Closed += (_, _) => updateWindow = null; }
        updateWindow.Activate();
    }
    public App() { RequestedTheme = ApplicationTheme.Light; InitializeComponent(); Queue = DispatcherQueue.GetForCurrentThread(); UnhandledException += (_, e) => { Notify(e.Message); e.Handled = true; }; }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppSettingsConfig.Instance.ApplyToAppSettings(); LocalizationService.Instance.CurrentLanguage = AppSettingsConfig.Instance.Language; Main = new MainWindow(); Main.Activate();
        Program.Instance?.Listen(() => Queue.TryEnqueue(ActivateExisting));
#if SNIPIT_SMOKE_TESTS
        if(Environment.GetEnvironmentVariable("SNIPIT_DATA_DIRECTORY")!=null){_ = SmokeTests.Run(); return;}
#endif
        tray = new System.Windows.Forms.NotifyIcon { Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets/icon.ico")), Text = "SnipIt", Visible = true };
        var menu = new System.Windows.Forms.ContextMenuStrip(); menu.Items.Add(Ui.L("영역 캡처"), null, (_, _) => Capture("Region")); menu.Items.Add(Ui.L("전체 화면"), null, (_, _) => Capture("Full")); menu.Items.Add(Ui.L("최근 캡처 편집"), null, (_, _) => EditRecent()); menu.Items.Add(Ui.L("열기"), null, (_, _) => Main.Activate()); menu.Items.Add(Ui.L("설정"), null, (_, _) => OpenSettings()); menu.Items.Add(Ui.L("업데이트"), null, (_, _) => ShowUpdates()); menu.Items.Add(Ui.L("종료"), null, (_, _) => ExitApp()); tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => Main.Activate();
        hotkeys = new Hotkeys(); hotkeys.Register(AppSettingsConfig.Instance);
        if (Program.ShouldStartInTray(Program.StartedByWindows ? new[]{"--startup"} : Array.Empty<string>(), AppSettingsConfig.Instance.StartMinimized)) Main.AppWindow.Hide();
        _ = Updater.Instance.Automatic();
        var open = Environment.GetEnvironmentVariable("SNIPIT_OPEN_IMAGE"); if (!string.IsNullOrEmpty(open) && File.Exists(open)) _ = OpenEditor(new Bitmap(open));
    }
    internal static async Task SaveRecent(Window owner){if(silent==null||Busy)return;Busy=true;try{using var copy=(Bitmap)silent.Clone();var config=AppSettingsConfig.Instance;var name=$"SnipIt_{DateTime.Now:yyyyMMdd_HHmmss_fff}.{config.DefaultFormat}";var path=config.SilentModeAutoSave?Path.Combine(config.SavePath,name):Ui.SaveFile(owner,ImageExport.Filter,name);if(path!=null){await Task.Run(()=>ImageExport.Save(copy,path));Notify(Ui.L("저장했습니다."));}}finally{Busy=false;}}
    internal static void OpenSettings(){if(!Busy&&SettingsWindows==0)new SettingsWindow().Activate();}
    internal static void EditRecent() => _ = EditRecentAsync();
    internal static async Task EditRecentAsync()
    {
        if (Busy) return;
        Busy = true;
        try
        {
            if (silent != null) await OpenEditor((Bitmap)silent.Clone());
            else if (HistoryStore.Instance.Items.FirstOrDefault() is { } recent)
                await OpenEditor(await HistoryStore.Load(recent), recent);
            else Notify(Ui.L("저장된 캡처가 없습니다."));
        }
        catch (Exception ex) { Notify(Ui.L("최근 캡처 열기 실패: ") + ex.Message); }
        finally { Busy = false; }
    }
    internal static void RegisterHotkeys(){hotkeys?.Register(AppSettingsConfig.Instance);Main.RefreshHotkeys();}
    private static void ActivateExisting()
    {
        if (Busy || Exiting) return;
        Window window = Main;
        window.AppWindow.Show();
        if (window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter
            && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized) presenter.Restore();
        window.Activate();
    }
    internal static void Notify(string message) { if (tray != null) { tray.BalloonTipTitle = "SnipIt"; tray.BalloonTipText = message; tray.ShowBalloonTip(3000); } }
    internal static async void ExitApp()
    {
        if (Exiting) return;
        if (Busy || Recordings > 0) { Notify("진행 중인 캡처·업데이트 또는 녹화를 마친 뒤 종료해 주세요."); return; }
        Exiting = true; Busy = true;
        try
        {
            foreach (var editor in Editors.ToArray()) await editor.CloseForAppExit();
            Updater.Instance.Stop(); hotkeys?.Dispose(); tray?.Dispose(); silent?.Dispose(); Current.Exit();
        }
        catch (Exception ex)
        {
            Exiting = false; Busy = false;
            Notify("편집 이력을 저장하지 못해 종료를 중단했습니다: " + ex.Message);
        }
    }
    internal static async Task OpenEditor(Bitmap bitmap, CaptureHistoryItem? item = null)
    {
        // Ownership is transferred here, including when an existing editor rejects a switch.
        try
        {
            if (Editors.FirstOrDefault() is { } existing)
            {
                await existing.OpenCapture(bitmap, item);
                return;
            }
            var editor = new EditorWindow(bitmap, item);
            Editors.Add(editor);
            editor.Closed += (_, _) => Editors.Remove(editor);
            editor.Activate();
        }
        catch (Exception ex) { Notify(Ui.L("편집기 열기 실패: ") + ex.Message); }
    }
    internal static async Task Copy(Bitmap bitmap)
    {
        using var memory = new MemoryStream(); bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png); var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream(); await stream.WriteAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(memory.ToArray())); stream.Seek(0);
        var data = new DataPackage(); data.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(stream)); Clipboard.SetContent(data); Clipboard.Flush(); stream.Dispose();
    }
    internal static void Capture(string mode) => _ = CaptureAsync(mode);
    internal static async Task CaptureAsync(string mode)
    {
        if (Busy || Recordings > 0) return; Busy = true; var target = SnipIt.Utils.NativeMethods.GetForegroundWindow();
        var hiddenEditors = Editors.Where(editor => editor.AppWindow.IsVisible).ToArray();
        var ownTarget = target == Ui.Handle(Main) || Editors.Any(editor => target == Ui.Handle(editor));
        var completed = false;
        try
        {
            Main.AppWindow.Hide();
            foreach (var editor in hiddenEditors) editor.AppWindow.Hide();
            await Task.Delay(150);
            if (mode == "Window" && ownTarget) { target = SnipIt.Utils.NativeMethods.GetForegroundWindow(); if (target == Ui.Handle(Main) || Editors.Any(editor => target == Ui.Handle(editor)) || target == IntPtr.Zero) throw new InvalidOperationException(Ui.L("캡처할 창을 선택한 뒤 활성 창 단축키를 사용해 주세요.")); }
            if (mode == "Region" || mode == "Gif")
            {
                var bounds = ScreenCaptureService.GetVirtualScreenBoundsPhysical(); using var full = ScreenCaptureService.CaptureFullScreen(); var selection = await CaptureOverlay.Select(full, bounds);
                if (selection == null) return;
                if (mode == "Gif") { new RecordingWindow(selection.Value).Activate(); completed = true; return; }
                var relative = new Rectangle(selection.Value.X - bounds.X, selection.Value.Y - bounds.Y, selection.Value.Width, selection.Value.Height);
                await Complete(full.Clone(relative, System.Drawing.Imaging.PixelFormat.Format32bppArgb));
                completed = true;
            }
            else { var bitmap = mode == "Window" ? ScreenCaptureService.CaptureWindow(target) : ScreenCaptureService.CaptureFullScreen(); if (bitmap != null) { await Complete(bitmap); completed = true; } }
        }
        catch (Exception ex) { Notify(Ui.L("캡처 실패: ") + ex.Message); }
        finally
        {
            foreach (var editor in hiddenEditors)
                if (!completed && Editors.Contains(editor) && !editor.AppWindow.IsVisible) editor.AppWindow.Show(false);
            Busy = false;
        }
    }
    private static async Task Complete(Bitmap bitmap)
    {
        try
        {
            CaptureHistoryItem? item = null;
            try { item = await HistoryStore.Instance.Add(bitmap); }
            catch (Exception ex) { Notify(Ui.L("캡처 이력 저장 실패: ") + ex.Message); }
            if (AppSettingsConfig.Instance.CopyToClipboard || AppSettingsConfig.Instance.SilentMode)
            {
                try { await Copy(bitmap); }
                catch (Exception ex) { Notify(Ui.L("클립보드 복사 실패: ") + ex.Message); }
            }
            if (AppSettingsConfig.Instance.PlaySound) System.Media.SystemSounds.Asterisk.Play();
            if (AppSettingsConfig.Instance.SilentMode) { silent?.Dispose(); silent = (Bitmap)bitmap.Clone(); new QuickCaptureWindow().Activate(); } else { var editing = bitmap; bitmap = null!; await OpenEditor(editing, item); }
        }
        finally { bitmap?.Dispose(); }
    }
}

