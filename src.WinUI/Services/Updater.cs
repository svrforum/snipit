using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.UI.Xaml;
using SnipIt.Models;
using SnipIt.Utils;

namespace SnipIt.Services;

public sealed class Updater : INotifyPropertyChanged
{
    public static Updater Instance { get; } = new();
    private readonly GitHubUpdateClient _client = new(new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
    private CancellationTokenSource? _operation;
    private readonly CancellationTokenSource _lifetime = new();
    private UpdateRelease? _release;
    private PreparedUpdate? _prepared;
    private bool _busy;
    private bool _started;
    private bool _confirming;
    private string? _lastNotifiedTag;
    private static string CacheRoot => Path.Combine(AppDataPaths.GetFolder(Environment.SpecialFolder.LocalApplicationData), "Updates-WinUI");
    public event PropertyChangedEventHandler? PropertyChanged;
    public string CurrentVersion => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(App).Assembly)?.InformationalVersion.Split('+')[0] ?? "0.0.0";
    private bool IsPrerelease => CurrentVersion.Contains('-');
    public string Status { get; private set; } = Ui.L("새 버전을 확인해 보세요.");
    public string ReleaseNotes => _release?.Notes ?? "";
    public string AvailableVersion => _release == null ? "" : Ui.L("새 버전") + " " + _release.Tag;
    public bool IsBusy => _busy;
    public bool CanCheck => !_busy;
    public bool CanDownload => !_busy && _release != null && _prepared == null;
    public bool CanInstall => !_busy && !_confirming && _prepared != null;
    public int Progress { get; private set; }
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    private CancellationToken Begin()
    {
        _busy = true;
        _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        Notify();
        return _operation.Token;
    }
    private void End() { _operation?.Dispose(); _operation = null; _busy = false; Notify(); }
    public void Cancel() => _operation?.Cancel();
    internal void Stop() { _lifetime.Cancel(); Cancel(); }

    public async Task CheckAsync(bool automatic = false)
    {
        if (_busy) return;
        bool checkedSuccessfully = false;
        var token = Begin();
        Status = Ui.L("GitHub에서 최신 버전을 확인하고 있어요…"); Notify();
        try
        {
            var release = await _client.CheckAsync(typeof(App).Assembly.GetName().Version!, token, allowSameVersion:IsPrerelease);
            if (release?.Tag != _release?.Tag) _prepared = null;
            _release = release;
            if (release != null && _prepared == null) _prepared = RestorePrepared(release);
            Status = release == null ? Ui.L("최신 버전을 사용 중입니다.") + $" (v{CurrentVersion})" : release.Tag + " " + Ui.L("버전이 준비됐어요.");
            if (_prepared != null) Status = Ui.L("다운로드한 새 버전이 준비돼 있어요. 업데이트 후 재시작을 선택해 주세요.");
            checkedSuccessfully = true;
            if (automatic && release != null && _lastNotifiedTag != release.Tag)
            {
                _lastNotifiedTag = release.Tag;
                App.Notify(release.Tag + " " + Ui.L("출시 · 업데이트에서 확인하세요."));
            }
        }
        catch (OperationCanceledException) { Status = token.IsCancellationRequested ? Ui.L("확인을 취소했습니다.") : Ui.L("서버 응답이 늦습니다. 나중에 다시 확인해 주세요."); }
        catch (Exception ex) { Status = Ui.L("업데이트 확인 실패: ") + ex.Message; }
        finally { End(); }
        if (checkedSuccessfully && automatic && !_lifetime.IsCancellationRequested && AppSettingsConfig.Instance.AutoDownloadUpdates && CanDownload)
            await DownloadAsync();
    }

    public async Task DownloadAsync()
    {
        if (!CanDownload || _release == null) return;
        var release = _release;
        var token = Begin();
        Progress = 0;
        Status = Ui.L("업데이트 다운로드 중…"); Notify();
        string folder = Path.Combine(CacheRoot, Guid.NewGuid().ToString("N"));
        try
        {
            var prepared = await _client.DownloadAsync(release, folder, new Progress<int>(value => { Progress = value; Notify(); }), token);
            await File.WriteAllTextAsync(Path.Combine(folder, "prepared.json"), JsonSerializer.Serialize(new[] { release.Tag, prepared.Sha256 }), token);
            _prepared = prepared;
            Status = Ui.L("검증 완료. 작업을 마친 뒤 ‘업데이트 후 재시작’을 눌러 주세요.");
        }
        catch (OperationCanceledException) { Status = token.IsCancellationRequested ? Ui.L("다운로드를 취소했습니다.") : Ui.L("다운로드 시간이 초과됐습니다. 다시 시도해 주세요."); }
        catch (Exception ex) { Status = Ui.L("다운로드 실패: ") + ex.Message; }
        finally { End(); }
    }

    public async Task InstallAsync(Window owner)
    {
        if (!CanInstall || _prepared == null) return;
        if (App.Busy || App.Editors.Count > 0 || App.Recordings > 0 || App.SettingsWindows > 0)
        { Status = Ui.L("편집·녹화·캡처를 마친 뒤 업데이트해 주세요."); Notify(); return; }
        _confirming=true;Notify();bool confirmed;try{confirmed=await Ui.Confirm(owner,Ui.L("업데이트 설치"),Ui.L("앱을 종료하고 새 버전으로 다시 시작할까요?"));}finally{_confirming=false;Notify();}if(!confirmed||!CanInstall||_prepared==null)return;
        if (App.Busy || App.Editors.Count > 0 || App.Recordings > 0 || App.SettingsWindows > 0) return;
        var prepared=_prepared;var token = Begin(); App.Busy = true;
        try { await GitHubUpdateClient.VerifyAsync(prepared.Path, prepared.Sha256, token); await UpdateInstaller.StartHelperAsync(prepared); App.Busy = false; App.ExitApp(); }
        catch (Exception ex) { Status = Ui.L("설치 실패: ") + ex.Message; }
        finally { App.Busy = false; End(); }
    }
    internal static PreparedUpdate? RestorePrepared(UpdateRelease release)
    {
        if (!Directory.Exists(CacheRoot)) return null;
        foreach (var folder in Directory.EnumerateDirectories(CacheRoot))
        {
            if (!Guid.TryParseExact(Path.GetFileName(folder), "N", out _) ||
                (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
            try
            {
                var manifest = Path.Combine(folder, "prepared.json");
                var exe = Path.Combine(folder, "SnipIt.update.exe");
                if (!File.Exists(manifest) || new FileInfo(manifest).Length > 4096) continue;
                var values = JsonSerializer.Deserialize<string[]>(File.ReadAllText(manifest));
                if (values?.Length == 2 && values[0] == release.Tag && values[1] is { Length: 64 } && values[1].All(Uri.IsHexDigit) &&
                    File.Exists(exe) && new FileInfo(exe).Length == release.Size &&
                    (release.Digest == null || string.Equals(release.Digest, "sha256:" + values[1], StringComparison.OrdinalIgnoreCase)))
                    return new(release, exe, values[1]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return null;
    }

    internal async Task Automatic()
    {
        if (_started) return;
        _started = true;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), _lifetime.Token);
            CleanupCache();
            while (!_lifetime.IsCancellationRequested)
            {
                if (AppSettingsConfig.Instance.CheckForUpdates) await CheckAsync(automatic: true);
                await Task.Delay(TimeSpan.FromHours(6), _lifetime.Token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static void CleanupCache()
    {
        // Only remove this updater's GUID folders, never installation backups or profiles.
        try
        {
            if (!Directory.Exists(CacheRoot)) return;
            foreach (var folder in Directory.EnumerateDirectories(CacheRoot))
            {
                try
                {
                    if (Guid.TryParseExact(Path.GetFileName(folder), "N", out _) &&
                        (File.GetAttributes(folder) & FileAttributes.ReparsePoint) == 0 &&
                        Directory.GetLastWriteTimeUtc(folder) < DateTime.UtcNow.AddDays(-7))
                        Directory.Delete(folder, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
