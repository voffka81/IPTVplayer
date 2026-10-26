using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using TV_Player.Plugins;

namespace TV_Player.Examples.HtmlPageStreamsPlugin;

public sealed class KlubnichkaPlugin : IChannelProviderPlugin
{
    public string Id => "klubnichka.live";
    public string Name => "Klubnichka.live";
    public Version Version => new(1, 0);

    public IReadOnlyList<PluginSettingDefinition> Settings =>
    [
        new("catalogUrl", "Channel catalog URL", IsRequired: true, DefaultValue: "https://klubnichka.live/")
    ];

    public IChannelProvider CreateProvider(ProviderSourceConfiguration configuration) =>
        new KlubnichkaProvider(configuration);
}

internal sealed class KlubnichkaProvider : IChannelProvider, IDisposable
{
    private const int MaxPlayerFrameDepth = 5;
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36";
    private static readonly Regex GeneratedFilePattern = new(
        @"\bvar\s+generatedFile\s*=\s*(['""'])(?<value>.*?)\1\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly ProviderSourceConfiguration _source;
    private readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                 System.Net.DecompressionMethods.Deflate |
                                 System.Net.DecompressionMethods.Brotli
    });
    private readonly HtmlParser _parser = new();
    private readonly Dictionary<string, Uri> _channelPages = new(StringComparer.Ordinal);

    public KlubnichkaProvider(ProviderSourceConfiguration source) => _source = source;

    public string Id => _source.Id;
    public string Name => _source.Name;
    public Version Version => new(1, 0);

    public async Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var catalogUri = GetCatalogUri();
        Log($"Loading channel catalog: {FormatUriForLog(catalogUri)}");
        var document = await GetDocumentAsync(catalogUri, cancellationToken);
        _channelPages.Clear();

        var channels = new List<ChannelInfo>();
        foreach (var card in document.QuerySelectorAll("a.channel-card"))
        {
            var href = card.GetAttribute("href")?.Trim();
            if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(catalogUri, href, out var channelUri))
                continue;
            if (!string.Equals(channelUri.Host, catalogUri.Host, StringComparison.OrdinalIgnoreCase))
                continue;

            var name = card.QuerySelector(".channel-card-title")?.TextContent.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = card.TextContent.Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var id = channelUri.AbsoluteUri;
            var logoPath = card.QuerySelector("img")?.GetAttribute("src");
            var logo = Uri.TryCreate(catalogUri, logoPath, out var logoUri) ? logoUri.AbsoluteUri : null;
            _channelPages[id] = channelUri;
            channels.Add(new ChannelInfo(id, name, "Live TV", logo));
        }

        Log($"Catalog parsed: {channels.Count} channel(s)");
        return channels;
    }

    public async Task<StreamInfo> ResolveStreamAsync(string channelId, CancellationToken cancellationToken = default)
    {
        if (!_channelPages.TryGetValue(channelId, out var channelPageUri))
            throw new KeyNotFoundException("Channel is not in the current catalog. Reload the source and try again.");

        Log($"Resolving channel page: {FormatUriForLog(channelPageUri)}");
        var currentUri = channelPageUri;
        Uri? referrer = GetCatalogUri();
        for (var depth = 0; depth < MaxPlayerFrameDepth; depth++)
        {
            var document = await GetDocumentAsync(currentUri, cancellationToken, referrer, isIframeRequest: depth > 0);
            var generatedFile = FindGeneratedFile(document);
            if (!string.IsNullOrWhiteSpace(generatedFile))
            {
                Log($"generatedFile raw: {generatedFile}");
                var normalizedGeneratedFile = generatedFile.Replace("\\:", ":", StringComparison.Ordinal)
                    .Replace("\\/", "/", StringComparison.Ordinal);
                var urls = normalizedGeneratedFile.Split(" or ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Where(IsHttpUrl)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (urls.Count > 0)
                {
                    Log($"Extracted generatedFile: primary={FormatUriForLog(new Uri(urls[0]))}; fallbackCount={urls.Count - 1}");
                    var mediaReferer = new Uri($"{currentUri.GetLeftPart(UriPartial.Authority)}/");
                    Log($"VLC media headers: User-Agent={BrowserUserAgent}; Referer={mediaReferer.AbsoluteUri}; Origin={mediaReferer.GetLeftPart(UriPartial.Authority)}");
                    foreach (var fallbackUrl in urls.Skip(1))
                        Log($"Extracted fallback URL: {FormatUriForLog(new Uri(fallbackUrl))}");
                    return new StreamInfo(
                        AddVlcRequestHeaders(urls[0], mediaReferer),
                        urls.Skip(1).Select(url => AddVlcRequestHeaders(url, mediaReferer)).ToArray());
                }
            }

            var directVideoUrl = FindVideoUrl(document, currentUri);
            if (directVideoUrl != null)
            {
                Log($"Extracted video source: {FormatUriForLog(new Uri(directVideoUrl))}");
                return new StreamInfo(directVideoUrl);
            }

            var frameSelector = depth == 0 ? ".channel-player iframe[src], iframe[src]" : "iframe[src]";
            var frameSource = document.QuerySelector(frameSelector)?.GetAttribute("src")?.Trim();
            if (string.IsNullOrWhiteSpace(frameSource) || !Uri.TryCreate(currentUri, frameSource, out var frameUri))
                break;

            Log($"Following iframe: {FormatUriForLog(frameUri)}");
            referrer = currentUri;
            currentUri = frameUri;
        }

        var error = "No generatedFile stream URL or video source was found in the channel player iframe chain.";
        Log($"Resolution failed at {FormatUriForLog(currentUri)}: {error}");
        throw new InvalidOperationException(error);
    }

    private async Task<AngleSharp.Dom.IDocument> GetDocumentAsync(
        Uri uri,
        CancellationToken cancellationToken,
        Uri? referrer = null,
        bool isIframeRequest = false)
    {
        Log($"GET {FormatUriForLog(uri)}; Referer={referrer?.AbsoluteUri ?? "<none>"}");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/jxl,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9,ru;q=0.8");
            request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");
            request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Google Chrome\";v=\"155\", \"Chromium\";v=\"155\", \"Not(A:Brand\";v=\"24\"");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.TryAddWithoutValidation("sec-fetch-dest", isIframeRequest ? "iframe" : "document");
            request.Headers.TryAddWithoutValidation("sec-fetch-mode", "navigate");
            request.Headers.TryAddWithoutValidation("sec-fetch-site", referrer != null &&
                string.Equals(referrer.Host, uri.Host, StringComparison.OrdinalIgnoreCase) ? "same-origin" : "none");
            request.Headers.TryAddWithoutValidation("upgrade-insecure-requests", "1");
            request.Headers.TryAddWithoutValidation("priority", "u=0, i");
            if (referrer != null)
                request.Headers.Referrer = referrer;

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            Log($"HTTP {(int)response.StatusCode} {response.StatusCode} for {FormatUriForLog(response.RequestMessage?.RequestUri ?? uri)}");
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return await _parser.ParseDocumentAsync(html, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log($"Request failed for {FormatUriForLog(uri)}: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private Uri GetCatalogUri()
    {
        var value = _source.Settings.TryGetValue("catalogUrl", out var configuredUrl)
            ? configuredUrl
            : "https://klubnichka.live/";
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return uri;

        throw new InvalidOperationException("Catalog URL must be an absolute HTTP or HTTPS URL.");
    }

    private static string? FindGeneratedFile(AngleSharp.Dom.IDocument document)
    {
        foreach (var script in document.Scripts)
        {
            var match = GeneratedFilePattern.Match(script.TextContent);
            if (match.Success)
                return match.Groups["value"].Value;
        }

        return null;
    }

    private static string? FindVideoUrl(AngleSharp.Dom.IDocument document, Uri baseUri)
    {
        var rawUrl = document.QuerySelector("video source[src]")?.GetAttribute("src")
                     ?? document.QuerySelector("video[src]")?.GetAttribute("src");
        return Uri.TryCreate(baseUri, rawUrl, out var videoUri) && IsHttpUrl(videoUri.AbsoluteUri)
            ? videoUri.AbsoluteUri
            : null;
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string AddVlcRequestHeaders(string streamUrl, Uri referer)
    {
        var options = $"user-agent={Uri.EscapeDataString(BrowserUserAgent)}" +
                      $"&referer={Uri.EscapeDataString(referer.AbsoluteUri)}" +
                      $"&origin={Uri.EscapeDataString(referer.GetLeftPart(UriPartial.Authority))}";
        return $"{streamUrl}|{options}";
    }

    private static string FormatUriForLog(Uri uri)
    {
        return uri.AbsoluteUri;
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [KlubnichkaPlugin] {message}{Environment.NewLine}";
        System.Diagnostics.Debug.Write(line);
        Console.Write(line);

        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TVPlayer", "Logs", "player.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, line);
        }
        catch
        {
            // Keep diagnostics from interrupting channel loading when the log is unavailable.
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
