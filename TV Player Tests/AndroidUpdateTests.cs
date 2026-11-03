using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TV_Player.Updates;
using Xunit;

namespace TV_Player.Tests;

public class AndroidUpdateTests
{
    private const string BaseUrl = "https://github.com/voffka81/IPTVplayer/releases/download/v2.0.2/";
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("test-apk-payload");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static AndroidUpdateManifest Manifest(long code = 200) => new()
    {
        SchemaVersion = 1, PackageId = AndroidUpdatePolicy.PackageId, VersionCode = code,
        VersionName = "2.0.2", ApkAssetName = "app.apk", Sha256 = Convert.ToHexString(SHA256.HashData(Payload)),
        Size = Payload.Length, MinSdk = 23
    };
    private static ReleaseAsset Apk() => new() { Name = "app.apk", Size = Payload.Length, DownloadUrl = BaseUrl + "app.apk" };
    private static AppRelease Release(bool prerelease = false, bool draft = false) => new()
    {
        Prerelease = prerelease, Draft = draft, Tag = "v2.0.2", Body = "Release notes",
        Assets = new() { Apk(), new() { Name = "android-update.json", DownloadUrl = BaseUrl + "android-update.json" } }
    };
    private static AvailableUpdate Update(AndroidUpdateManifest? manifest = null) => new(manifest ?? Manifest(), Apk().DownloadUrl, "Notes", "v2.0.2");
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value, JsonOptions)) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => handle(request, token);
    }
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> handle) => new(new Handler((request, _) => Task.FromResult(handle(request))));

    [Theory]
    [InlineData(false, false, UpdateChannel.Release, true)]
    [InlineData(true, false, UpdateChannel.Release, false)]
    [InlineData(true, false, UpdateChannel.Dev, true)]
    [InlineData(false, false, UpdateChannel.Dev, true)]
    [InlineData(false, true, UpdateChannel.Dev, false)]
    [InlineData(false, true, UpdateChannel.Release, false)]
    public void ChannelFiltersDraftsAndPrereleases(bool prerelease, bool draft, UpdateChannel channel, bool eligible)
        => Assert.Equal(eligible, AndroidUpdatePolicy.IsEligible(Release(prerelease, draft), channel));

    [Theory]
    [InlineData(199, UpdateCheckStatus.Available)]
    [InlineData(200, UpdateCheckStatus.UpToDate)]
    [InlineData(201, UpdateCheckStatus.UpToDate)]
    public void SameOrOlderReleaseCannotDowngradeInstalledBuild(long installed, UpdateCheckStatus expected)
        => Assert.Equal(expected, AndroidUpdatePolicy.Select(new[] { Update() }, installed).Status);

    [Fact]
    public void HighestVersionCodeWinsRegardlessOfListingOrder()
    {
        var result = AndroidUpdatePolicy.Select(new[] { Update(Manifest(250)), Update(Manifest(210)), Update(Manifest(300)) }, 200);
        Assert.Equal(300, result.Update!.Manifest.VersionCode);
    }

    [Theory]
    [InlineData("http://github.com/voffka81/IPTVplayer/releases/download/v/app.apk")]
    [InlineData("https://evil.example/voffka81/IPTVplayer/releases/download/v/app.apk")]
    [InlineData("https://github.com/other/IPTVplayer/releases/download/v/app.apk")]
    [InlineData("https://github.com/voffka81/IPTVplayer/actions/artifacts/123")]
    [InlineData("https://github.com/voffka81/IPTVplayer/releases/download/v/app.apk?redirect=evil")]
    [InlineData("https://user@github.com/voffka81/IPTVplayer/releases/download/v/app.apk")]
    public void OnlyOfficialPersistentReleaseAssetsAreTrusted(string url)
        => Assert.False(AndroidUpdatePolicy.IsOfficialAssetUrl(url));

    [Fact]
    public void RejectsInvalidMetadataAndIncompatibleSdk()
    {
        var good = Manifest();
        Assert.True(AndroidUpdatePolicy.IsValid(good, Apk(), 23));
        foreach (var invalid in new[]
        {
            good with { SchemaVersion = 2 }, good with { PackageId = "other" }, good with { VersionCode = 0 },
            good with { VersionCode = 2100000001 }, good with { Sha256 = "bad" }, good with { Sha256 = null! },
            good with { Size = 0 }, good with { Size = good.Size + 1 }, good with { ApkAssetName = "../app.apk" },
            good with { VersionName = "" }, good with { MinSdk = 24 }
        }) Assert.False(AndroidUpdatePolicy.IsValid(invalid, Apk(), 23));
    }

    [Fact]
    public async Task ReleaseSelectionSkipsDraftsAndDevAndLoadsVerifiedMetadata()
    {
        var requests = 0;
        using var client = Client(request =>
        {
            requests++;
            Assert.Contains("IPTVplayer", request.Headers.UserAgent.ToString());
            return request.RequestUri!.Host == "api.github.com" ? Json(new[] { Release(true), Release(false, true), Release() }) : Json(Manifest());
        });
        var result = await new GitHubUpdateService(client).CheckAsync(UpdateChannel.Release, 100, 34, default);
        Assert.Equal(UpdateCheckStatus.Available, result.Status);
        Assert.Equal("Release notes", result.Update!.Notes);
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task FindsStableReleaseAfterFullPageOfDevBuilds()
    {
        using var client = Client(request => request.RequestUri!.Host != "api.github.com" ? Json(Manifest()) :
            request.RequestUri.Query.Contains("page=1") ? Json(Enumerable.Repeat(Release(true), 30).ToArray()) : Json(new[] { Release() }));
        var result = await new GitHubUpdateService(client).CheckAsync(UpdateChannel.Release, 100, 34, default);
        Assert.Equal(UpdateCheckStatus.Available, result.Status);
    }

    [Fact]
    public async Task ReturningFromDevToReleaseWaitsForNewerCode()
    {
        using var client = Client(request => request.RequestUri!.Host == "api.github.com" ? Json(new[] { Release() }) : Json(Manifest(200)));
        var result = await new GitHubUpdateService(client).CheckAsync(UpdateChannel.Release, 300, 34, default);
        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.Update);
    }

    [Fact]
    public async Task ReleasesWithoutUpdaterAssetsAreNotOffered()
    {
        using var client = Client(_ => Json(new[] { Release() with { Assets = new() } }));
        Assert.Equal(UpdateCheckStatus.NoCompatibleRelease,
            (await new GitHubUpdateService(client).CheckAsync(UpdateChannel.Dev, 1, 34, default)).Status);
    }

    [Fact]
    public async Task ApiFailureRemainsRetryableInsteadOfReportingUpToDate()
    {
        using var client = Client(_ => new(HttpStatusCode.ServiceUnavailable));
        await Assert.ThrowsAsync<HttpRequestException>(() => new GitHubUpdateService(client).CheckAsync(UpdateChannel.Dev, 1, 34, default));
    }

    [Theory]
    [InlineData(UpdateChannel.Dev, UpdateCheckStatus.Available)]
    [InlineData(UpdateChannel.Release, UpdateCheckStatus.NoCompatibleRelease)]
    public async Task DevChannelOffersPrereleaseWithoutChangingStablePolicy(UpdateChannel channel, UpdateCheckStatus status)
    {
        using var client = Client(request => request.RequestUri!.Host == "api.github.com" ? Json(new[] { Release(true) }) : Json(Manifest()));
        Assert.Equal(status, (await new GitHubUpdateService(client).CheckAsync(channel, 1, 34, default)).Status);
    }

    [Fact]
    public async Task OversizedMetadataIsRejected()
    {
        using var client = Client(request => request.RequestUri!.Host == "api.github.com" ? Json(new[] { Release() }) :
            new(HttpStatusCode.OK) { Content = new StringContent(new string(' ', 65537)) });
        await Assert.ThrowsAsync<InvalidDataException>(() => new GitHubUpdateService(client).CheckAsync(UpdateChannel.Release, 1, 34, default));
    }

    [Fact]
    public async Task CheckCancellationReachesHttpRequest()
    {
        using var source = new CancellationTokenSource();
        using var client = new HttpClient(new Handler(async (_, token) => { source.Cancel(); await Task.Delay(Timeout.Infinite, token); return Json(new object()); }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GitHubUpdateService(client).CheckAsync(UpdateChannel.Release, 1, 34, source.Token));
    }

    [Fact]
    public async Task DownloadsExactBytesAndReportsProgress()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var client = Client(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Payload) });
            var progress = new RecordingProgress();
            var path = await new GitHubUpdateService(client).DownloadAsync(Update(), directory, progress, default);
            Assert.Equal(Payload, await File.ReadAllBytesAsync(path));
            Assert.Equal(new DownloadProgress(Payload.Length, Payload.Length), progress.Values.Last());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CorruptOrTruncatedDownloadIsDeleted(bool corrupt)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var bytes = corrupt ? new byte[Payload.Length] : Payload[..^1];
            using var client = Client(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            await Assert.ThrowsAsync<InvalidDataException>(() => new GitHubUpdateService(client).DownloadAsync(Update(), directory, null, default));
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CancelledDownloadLeavesNoPartialApk()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var source = new CancellationTokenSource();
            using var client = Client(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Payload) });
            var progress = new RecordingProgress(value => { if (value.Received > 0) source.Cancel(); });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GitHubUpdateService(client).DownloadAsync(Update(), directory, progress, source.Token));
            Assert.Contains(progress.Values, value => value.Received > 0);
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class RecordingProgress(Action<DownloadProgress>? onReport = null) : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Values { get; } = new();
        public void Report(DownloadProgress value) { Values.Add(value); onReport?.Invoke(value); }
    }
}
