using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace SnipIt.Services;

internal sealed record UpdateRelease(Version Version, string Tag, string Notes, Uri DownloadUrl,
    Uri ChecksumUrl, long Size, string? Digest);
internal sealed record PreparedUpdate(UpdateRelease Release, string Path, string Sha256);

internal sealed class GitHubUpdateClient(HttpClient http)
{
    internal const string LatestUrl = "https://api.github.com/repos/svrforum/snipit/releases/latest";
    private const long MaxDownloadBytes = 300L * 1024 * 1024;

    internal static Version? ParseVersion(string tag) =>
        Version.TryParse(tag.TrimStart('v', 'V'), out var version) && version.Build >= 0
            ? new Version(version.Major, version.Minor, version.Build, Math.Max(version.Revision, 0)) : null;

    internal static UpdateRelease? ParseRelease(string json, Version current)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var version = ParseVersion(tag);
        if (version == null || version <= current) return null;
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        var exe = assets.SingleOrDefault(a => a.GetProperty("name").GetString() == "SnipIt.exe");
        var sums = assets.SingleOrDefault(a => a.GetProperty("name").GetString() == "SHA256SUMS.txt");
        if (exe.ValueKind == JsonValueKind.Undefined || sums.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException("릴리즈에 실행 파일 또는 체크섬 파일이 없습니다.");
        Uri AssetUri(JsonElement asset)
        {
            var uri = new Uri(asset.GetProperty("browser_download_url").GetString()!, UriKind.Absolute);
            var prefix = $"/svrforum/snipit/releases/download/{Uri.EscapeDataString(tag)}/";
            if (uri.Scheme != "https" || uri.Host != "github.com" || !uri.IsDefaultPort ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
                !uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("허용되지 않은 업데이트 다운로드 주소입니다.");
            return uri;
        }
        long size = exe.GetProperty("size").GetInt64();
        if (size <= 0 || size > MaxDownloadBytes) throw new InvalidDataException("업데이트 파일 크기가 올바르지 않습니다.");
        string? digest = exe.TryGetProperty("digest", out var value) ? value.GetString() : null;
        return new(version, tag, root.TryGetProperty("body", out var notes) ? notes.GetString() ?? "" : "",
            AssetUri(exe), AssetUri(sums), size, digest);
    }

    private static HttpRequestMessage Request(string uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("SnipIt-Updater/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return request;
    }

    internal async Task<UpdateRelease?> CheckAsync(Version current, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = Request(LatestUrl);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return ParseRelease(await ReadLimitedText(response, 1024 * 1024, timeout.Token).ConfigureAwait(false), current);
    }

    private static async Task<string> ReadLimitedText(HttpResponseMessage response, int limit, CancellationToken token)
    {
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            if (output.Length + read > limit) throw new InvalidDataException("서버 응답이 너무 큽니다.");
            output.Write(buffer, 0, read);
        }
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }

    internal static string ParseChecksum(string text)
    {
        var candidates = text.Split('\n').Select(line => line.Trim().TrimStart('\uFEFF'))
            .Select(line => line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2 && parts[1].TrimStart('*') == "SnipIt.exe").ToArray();
        if (candidates.Length != 1 || candidates[0][0].Length != 64 || !candidates[0][0].All(Uri.IsHexDigit))
            throw new InvalidDataException("실행 파일 체크섬이 없거나 올바르지 않습니다.");
        return candidates[0][0].ToLowerInvariant();
    }

    internal async Task<PreparedUpdate> DownloadAsync(UpdateRelease release, string directory,
        IProgress<int>? progress, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        token = timeout.Token;
        using var checksumRequest = Request(release.ChecksumUrl.AbsoluteUri);
        using var checksumResponse = await http.SendAsync(checksumRequest, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        checksumResponse.EnsureSuccessStatusCode();
        var expected = ParseChecksum(await ReadLimitedText(checksumResponse, 16384, token).ConfigureAwait(false));
        if (release.Digest != null && !string.Equals(release.Digest, "sha256:" + expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("GitHub 자산과 체크섬 파일이 일치하지 않습니다.");
        Directory.CreateDirectory(directory);
        string partial = System.IO.Path.Combine(directory, "download.part");
        string ready = System.IO.Path.Combine(directory, "SnipIt.update.exe");
        try
        {
            using var request = Request(release.DownloadUrl.AbsoluteUri);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length != release.Size)
                throw new InvalidDataException("다운로드 크기가 릴리즈 정보와 다릅니다.");
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int read, lastPercent = -1;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > release.Size) throw new InvalidDataException("다운로드가 예상 크기를 초과했습니다.");
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    int percent = (int)(total * 100 / release.Size);
                    if (percent != lastPercent) { progress?.Report(percent); lastPercent = percent; }
                }
                if (total != release.Size) throw new InvalidDataException("다운로드가 완료되지 않았습니다.");
            }
            await VerifyAsync(partial, expected, token).ConfigureAwait(false);
            File.Move(partial, ready);
            return new(release, ready, expected);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    internal static async Task VerifyAsync(string path, string expected, CancellationToken token = default)
    {
        await using var input = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(input, token).ConfigureAwait(false);
        if (!string.Equals(Convert.ToHexString(hash), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("체크섬 검증에 실패했습니다. 업데이트를 다시 다운로드해 주세요.");
    }
}
