using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace TV_Player.Updates;

/// <summary>Reads only published official Release assets. Caller owns HttpClient and cancellation.</summary>
public sealed class GitHubUpdateService(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UpdateCheckResult> CheckAsync(UpdateChannel channel, long installedVersion, int sdk,
        CancellationToken cancellationToken)
    {
        var updates = new List<AvailableUpdate>();
        // Bound requests while checking multiple pages (stable releases may follow many prereleases).
        for (var page = 1; page <= 5; page++)
        {
            var releases = JsonSerializer.Deserialize<List<AppRelease>>(await ReadAsync(
                $"https://api.github.com/repos/{AndroidUpdatePolicy.Repository}/releases?per_page=30&page={page}",
                2 * 1024 * 1024, cancellationToken), JsonOptions) ?? new();
            foreach (var release in releases.Where(release => AndroidUpdatePolicy.IsEligible(release, channel)))
            {
                var metadata = release.Assets.SingleOrDefault(asset => asset.Name == "android-update.json");
                if (metadata == null || !AndroidUpdatePolicy.IsOfficialAssetUrl(metadata.DownloadUrl)) continue;
                // A broken published manifest must produce a retryable error, not a false 'up to date'.
                var manifest = JsonSerializer.Deserialize<AndroidUpdateManifest>(
                    await ReadAsync(metadata.DownloadUrl, 64 * 1024, cancellationToken), JsonOptions);
                if (manifest == null) continue;
                var apk = release.Assets.SingleOrDefault(asset => asset.Name == manifest.ApkAssetName);
                if (apk != null && AndroidUpdatePolicy.IsValid(manifest, apk, sdk))
                    updates.Add(new(manifest, apk.DownloadUrl, release.Body ?? "", release.Tag));
            }
            if (releases.Count < 30) break;
        }
        return AndroidUpdatePolicy.Select(updates, installedVersion);
    }

    public async Task<string> DownloadAsync(AvailableUpdate update, string directory,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var manifest = update.Manifest;
        var asset = new ReleaseAsset { Name = manifest.ApkAssetName, Size = manifest.Size, DownloadUrl = update.DownloadUrl };
        if (!AndroidUpdatePolicy.IsValid(manifest, asset, int.MaxValue)) throw new InvalidDataException("Invalid update metadata.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".apk");
        try
        {
            using var request = CreateRequest(update.DownloadUrl);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length != manifest.Size)
                throw new InvalidDataException("APK size differs from release metadata.");
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                long received = 0;
                progress?.Report(new(0, manifest.Size));
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    received += count;
                    if (received > manifest.Size) throw new InvalidDataException("APK exceeds expected size.");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    progress?.Report(new(received, manifest.Size));
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (received != manifest.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("APK checksum or size verification failed.");
            }
            return path;
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    private async Task<byte[]> ReadAsync(string url, int limit, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(url);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Release response is too large.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("IPTVplayer", "2.0"));
        return request;
    }
}
