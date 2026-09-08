using System.Diagnostics;
using System.IO;

namespace SnipIt.Services;

internal static class UpdateInstaller
{
    internal static async Task StartHelperAsync(PreparedUpdate update)
    {
        // The distributed app is self-contained and single-file. A framework-dependent
        // development app cannot be copied and run as a standalone update helper.
#pragma warning disable IL3000
        if (!string.IsNullOrEmpty(typeof(App).Assembly.Location))
#pragma warning restore IL3000
            throw new InvalidOperationException("자동 설치는 배포용 단일 EXE에서 지원됩니다. 개발 빌드에서는 다운로드까지만 사용할 수 있습니다.");
        string target = Environment.ProcessPath ?? throw new InvalidOperationException("실행 파일 경로를 확인할 수 없습니다.");
        string directory = Path.GetDirectoryName(target)!;
        // Fail while the application is still running if the installation folder is read-only.
        string probe = Path.Combine(directory, ".snipit-write-" + Guid.NewGuid().ToString("N"));
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        File.Delete(probe);
        string helper = Path.Combine(Path.GetDirectoryName(update.Path)!, "UpdateHost.exe");
        File.Copy(target, helper, overwrite: true);
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
        using var parent = Process.GetCurrentProcess();
        foreach (var argument in new[] { "--apply-update", parent.Id.ToString(), parent.StartTime.ToUniversalTime().Ticks.ToString(),
            target, update.Path, update.Sha256 }) start.ArgumentList.Add(argument);
        string ready = Path.Combine(Path.GetDirectoryName(update.Path)!, "helper.ready");
        if (File.Exists(ready)) File.Delete(ready);
        using var process = Process.Start(start) ?? throw new IOException("업데이트 도우미를 시작하지 못했습니다.");
        var timer = Stopwatch.StartNew();
        while (!File.Exists(ready))
        {
            if (process.HasExited) throw new IOException("업데이트 도우미가 시작 중 종료되었습니다.");
            if (timer.Elapsed > TimeSpan.FromSeconds(20))
            {
                process.Kill();
                throw new TimeoutException("업데이트 도우미가 응답하지 않습니다. 프로그램은 종료하지 않았습니다.");
            }
            await Task.Delay(100);
        }
    }

    internal static async Task RunHelperAsync(string[] args)
    {
        if (args.Length != 6 || !int.TryParse(args[1], out int pid) || !long.TryParse(args[2], out long started))
            throw new ArgumentException("업데이트 도우미 인수가 올바르지 않습니다.");
        string target = Path.GetFullPath(args[3]);
        string payload = Path.GetFullPath(args[4]);
        string helperDirectory = Path.GetDirectoryName(Environment.ProcessPath!)!;
        if (!string.Equals(Path.GetDirectoryName(payload), helperDirectory, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetDirectoryName(target), helperDirectory, StringComparison.OrdinalIgnoreCase) ||
            !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("업데이트 경로가 올바르지 않습니다.");
        await GitHubUpdateClient.VerifyAsync(payload, args[5]);
        await File.WriteAllTextAsync(Path.Combine(helperDirectory, "helper.ready"), "ready");
        Process? parent = null;
        try { parent = Process.GetProcessById(pid); }
        catch (ArgumentException) { }
        if (parent != null)
        {
            using (parent)
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
            {
                if (parent.StartTime.ToUniversalTime().Ticks != started)
                    throw new InvalidOperationException("원래 프로그램을 확인할 수 없어 교체를 중단했습니다.");
                await parent.WaitForExitAsync(timeout.Token);
            }
        }
        await ReplaceAndRestartAsync(payload, target, args[5], async path =>
        {
            using var process = Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(path)!
            }) ?? throw new IOException("새 버전을 시작하지 못했습니다.");
            await Task.Delay(2000);
            if (process.HasExited && process.ExitCode != 0) throw new IOException("새 버전이 시작 중 종료되었습니다.");
        });
    }

    internal static async Task ReplaceAndRestartAsync(string payload, string target, string sha256, Func<string, Task> restart)
    {
        string candidate = target + ".update-" + Guid.NewGuid().ToString("N");
        string backup = target + ".previous-" + Guid.NewGuid().ToString("N");
        bool replaced = false;
        try
        {
            // Copy alongside the target so replacement also works when the download
            // cache and the portable application live on different drives.
            File.Copy(payload, candidate);
            await GitHubUpdateClient.VerifyAsync(candidate, sha256);
            File.Replace(candidate, target, backup);
            replaced = true;
            await restart(target);
        }
        catch
        {
            if (replaced)
            {
                File.Move(backup, target, overwrite: true);
                try { await restart(target); } catch { /* Keep the restored executable for manual launch. */ }
            }
            throw;
        }
        finally
        {
            if (File.Exists(candidate)) File.Delete(candidate);
        }
    }
}
