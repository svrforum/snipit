using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SnipIt.Services;

internal static class UpdateTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    private static async Task Reject<T>(Func<Task> action, string message) where T : Exception
    {
        try { await action(); } catch (T) { Check(true, message); return; }
        throw new Exception("Expected rejection: " + message);
    }
    private static string Metadata(byte[] bytes, string tag = "v2.10.0", bool prerelease = false, string host = "github.com", bool sums = true) =>
        JsonSerializer.Serialize(new
        {
            tag_name = tag, draft = false, prerelease, body = "Release notes",
            assets = new[] {
                new { name = "SnipIt.exe", browser_download_url = $"https://{host}/svrforum/snipit/releases/download/{tag}/SnipIt.exe", size = bytes.Length, digest = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() },
                new { name = sums ? "SHA256SUMS.txt" : "other.txt", browser_download_url = $"https://{host}/svrforum/snipit/releases/download/{tag}/SHA256SUMS.txt", size = 78, digest = "" }
            }
        });
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!request.Headers.UserAgent.Any()) throw new Exception("Missing GitHub User-Agent");
            return Task.FromResult(response(request));
        }
    }
    internal static async Task RunAsync(string directory, bool live)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("test executable payload");
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var json = Metadata(bytes);
        var release = GitHubUpdateClient.ParseRelease(json, new Version(2, 9, 0, 0))!;
        Check(release.Version == new Version(2, 10, 0, 0), "Updates compare numeric versions, including 2.10 > 2.9");
        Check(GitHubUpdateClient.ParseRelease(json, release.Version) == null, "Current release is not offered again");
        Check(GitHubUpdateClient.ParseRelease(json, new Version(3, 0, 0, 0)) == null, "Updates never downgrade");
        Check(GitHubUpdateClient.ParseRelease(Metadata(bytes, prerelease: true), new Version(0, 0)) == null, "Prereleases are ignored");
        Check(GitHubUpdateClient.ParseVersion("v2.6.0-beta") == null, "Unstable version tags are ignored");
        await Reject<InvalidDataException>(() => Task.FromResult(GitHubUpdateClient.ParseRelease(Metadata(bytes, host: "example.com"), new Version(0, 0))), "Foreign download hosts are rejected");
        await Reject<InvalidDataException>(() => Task.FromResult(GitHubUpdateClient.ParseRelease(Metadata(bytes, sums: false), new Version(0, 0))), "Missing checksum assets are rejected");
        Check(GitHubUpdateClient.ParseChecksum(hash + "  SnipIt.exe\r\n") == hash, "Published checksum format is accepted");
        await Reject<InvalidDataException>(() => Task.FromResult(GitHubUpdateClient.ParseChecksum(hash + "  SnipIt.exe\n" + hash + "  SnipIt.exe")), "Duplicate checksum entries are rejected");
        string checksum = hash;
        byte[] served = bytes;
        using var http = new HttpClient(new Handler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.EndsWith("/latest") ? new StringContent(json) :
                request.RequestUri.AbsolutePath.EndsWith(".txt") ? new StringContent(checksum + "  SnipIt.exe\n") : new ByteArrayContent(served)
        }));
        var client = new GitHubUpdateClient(http);
        Check(await client.CheckAsync(new Version(0, 0), default) != null, "GitHub release check works through HTTP client");
        string folder = Path.Combine(directory, "download-ok");
        var prepared = await client.DownloadAsync(release, folder, null, default);
        Check(File.ReadAllBytes(prepared.Path).SequenceEqual(bytes) && !File.Exists(Path.Combine(folder, "download.part")), "Verified download is staged without partial files");
        var cached = Path.Combine(directory, "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cached);
        File.Copy(prepared.Path, Path.Combine(cached, "SnipIt.update.exe"));
        await File.WriteAllTextAsync(Path.Combine(cached, "prepared.json"), JsonSerializer.Serialize(new[] { release.Tag, hash }));
        Check(UpdateService.RestorePrepared(release)?.Sha256 == hash, "Verified download metadata is reusable after restart");
        await File.WriteAllTextAsync(Path.Combine(cached, "prepared.json"), "[\"v2.10.0\",null]");
        Check(UpdateService.RestorePrepared(release) == null, "Corrupted cached metadata is ignored");
        served = Enumerable.Repeat((byte)'X', bytes.Length).ToArray();
        folder = Path.Combine(directory, "download-corrupt");
        await Reject<InvalidDataException>(() => client.DownloadAsync(release, folder, null, default), "Corrupted downloads fail SHA-256 verification");
        Check(!File.Exists(Path.Combine(folder, "download.part")) && !File.Exists(Path.Combine(folder, "SnipIt.update.exe")), "Corrupted downloads leave no installable payload");
        served = bytes;
        checksum = new string('0', 64);
        await Reject<InvalidDataException>(() => client.DownloadAsync(release, Path.Combine(directory, "bad-digest"), null, default), "GitHub digest and checksum mismatch is rejected");
        checksum = hash;
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            await Reject<OperationCanceledException>(() => client.DownloadAsync(release, Path.Combine(directory, "cancel"), null, cancelled.Token), "Cancelled download cannot install");
        }
        using (var offline = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden))))
            await Reject<HttpRequestException>(() => new GitHubUpdateClient(offline).CheckAsync(new Version(0, 0), default), "API rate-limit/auth errors are surfaced");
        string target = Path.Combine(directory, "target.exe");
        await File.WriteAllTextAsync(target, "old version");
        bool restarted = false;
        await UpdateInstaller.ReplaceAndRestartAsync(prepared.Path, target, hash, _ => { restarted = true; return Task.CompletedTask; });
        Check(restarted && File.ReadAllBytes(target).SequenceEqual(bytes), "Installer replaces the executable and restarts");
        Check(Directory.GetFiles(directory, "target.exe.previous-*").Any(path => File.ReadAllText(path) == "old version"), "Successful update retains the old executable backup");
        await File.WriteAllTextAsync(target, "rollback version");
        int starts = 0;
        await Reject<IOException>(() => UpdateInstaller.ReplaceAndRestartAsync(prepared.Path, target, hash, _ =>
        {
            if (++starts == 1) throw new IOException("Simulated launch failure");
            return Task.CompletedTask;
        }), "Failed restart triggers rollback");
        Check(File.ReadAllText(target) == "rollback version" && starts == 2, "Rollback restores and restarts the previous executable");
        await Reject<InvalidDataException>(() => UpdateInstaller.ReplaceAndRestartAsync(prepared.Path, target, new string('0', 64), _ => Task.CompletedTask), "Installer revalidates payload before replacing files");
        Check(File.ReadAllText(target) == "rollback version", "Verification failure leaves the original executable intact");
        using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
            await Reject<IOException>(() => UpdateInstaller.ReplaceAndRestartAsync(prepared.Path, target, hash, _ => Task.CompletedTask), "Locked executable is not overwritten");
        if (live)
        {
            using var network = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var online = new GitHubUpdateClient(network);
            var latest = await online.CheckAsync(new Version(0, 0), default);
            Check(latest != null, "LIVE GitHub release metadata validated");
            var downloaded = await online.DownloadAsync(latest!, Path.Combine(directory, "live"), null, default);
            Check(new FileInfo(downloaded.Path).Length == latest!.Size, "LIVE release executable downloaded and checksum verified (not executed)");
        }
    }
}
