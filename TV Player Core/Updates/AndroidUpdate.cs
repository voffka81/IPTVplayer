using System.Text.Json.Serialization;

namespace TV_Player.Updates;

public enum UpdateChannel { Release, Dev }
public enum UpdateCheckStatus { Available, UpToDate, NoCompatibleRelease }

public sealed record AndroidUpdateManifest
{
    public int SchemaVersion { get; init; }
    public string PackageId { get; init; } = "";
    public long VersionCode { get; init; }
    public string VersionName { get; init; } = "";
    public string ApkAssetName { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public long Size { get; init; }
    public int MinSdk { get; init; }
}

public sealed record ReleaseAsset
{
    public string Name { get; init; } = "";
    public long Size { get; init; }
    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; init; } = "";
}

public sealed record AppRelease
{
    public bool Draft { get; init; }
    public bool Prerelease { get; init; }
    public string Body { get; init; } = "";
    [JsonPropertyName("tag_name")]
    public string Tag { get; init; } = "";
    public List<ReleaseAsset> Assets { get; init; } = new();
}

public sealed record AvailableUpdate(AndroidUpdateManifest Manifest, string DownloadUrl, string Notes, string Tag);
public sealed record UpdateCheckResult(UpdateCheckStatus Status, AvailableUpdate? Update);
public readonly record struct DownloadProgress(long Received, long Total);

public static class AndroidUpdatePolicy
{
    public const string PackageId = "com.voffka81.iptvplayer";
    public const string Repository = "voffka81/IPTVplayer";
    public const long MaxApkSize = 1024L * 1024 * 1024;

    public static bool IsOfficialAssetUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.Host == "github.com" && uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        uri.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.Ordinal);

    public static bool IsEligible(AppRelease release, UpdateChannel channel) =>
        !release.Draft && (!release.Prerelease || channel == UpdateChannel.Dev);

    public static bool IsValid(AndroidUpdateManifest manifest, ReleaseAsset apk, int sdk) =>
        manifest.SchemaVersion == 1 && manifest.PackageId == PackageId &&
        manifest.VersionCode is > 0 and <= 2100000000 && !string.IsNullOrWhiteSpace(manifest.VersionName) &&
        manifest.MinSdk >= 23 && sdk >= manifest.MinSdk && manifest.Size is > 0 and <= MaxApkSize &&
        manifest.Size == apk.Size && manifest.ApkAssetName == apk.Name &&
        Path.GetFileName(manifest.ApkAssetName) == manifest.ApkAssetName &&
        !string.IsNullOrWhiteSpace(manifest.ApkAssetName) && manifest.ApkAssetName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) &&
        manifest.Sha256 is { Length: 64 } && manifest.Sha256.All(Uri.IsHexDigit) && IsOfficialAssetUrl(apk.DownloadUrl);

    public static UpdateCheckResult Select(IEnumerable<AvailableUpdate> updates, long installedVersion)
    {
        var newest = updates.OrderByDescending(update => update.Manifest.VersionCode).FirstOrDefault();
        return newest == null ? new(UpdateCheckStatus.NoCompatibleRelease, null)
            : newest.Manifest.VersionCode <= installedVersion ? new(UpdateCheckStatus.UpToDate, null)
            : new(UpdateCheckStatus.Available, newest);
    }
}
