using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using TV_Player.Plugins;

namespace TV_Player.Examples.HtmlPageStreamsPlugin;

public sealed class ViksTvPlugin : IChannelProviderPlugin
{
    public string Id => "viks.tv";
    public string Name => "Viks.TV";
    public Version Version => new(1, 0);

    // Viks has one fixed catalog; users should not need to supply an M3U or catalog URL.
    public IReadOnlyList<PluginSettingDefinition> Settings => [];

    public IChannelProvider CreateProvider(ProviderSourceConfiguration configuration) => new ViksTvProvider(configuration);
}

internal sealed class ViksTvProvider : IChannelProvider, IChannelProgramGuideSource, IDisposable
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36";
    private const string SiteOrigin = "https://live.viks.tv";
    private const int MaxFrameDepth = 4;
    // PlayerJS 17.93 encryption settings embedded by Viks.TV in its player configuration.
    private const string PlayerFileSeparator = "F";
    private static readonly string[] PlayerFileKeys = ["556GG", "D4FG5F", "R4RGR3V", "3SFVFF1O", "2TF5D8X2NQ"];

    private static readonly Regex KodkPattern = new(
        "\\bvar\\s+kodk\\s*=\\s*(['\"])(?<url>.*?)\\1\\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex DirectFilePattern = new(
        "\\bfile\\s*:\\s*(['\"])(?<url>https?://[^'\"]+\\.m3u8(?:\\?[^'\"]*)?)\\1",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex EncryptedFilePattern = new(
        "\\bfile\\s*:\\s*(['\"])(?<value>#2.*?)\\1",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex KosPattern = new(
        "\\bvar\\s+kos\\s*=\\s*(['\"])(?<value>.*?)\\1\\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly ProviderSourceConfiguration _source;
    private readonly HttpClient _httpClient;
    private readonly HtmlParser _parser = new();
    private readonly Dictionary<string, Uri> _channelPages = new(StringComparer.OrdinalIgnoreCase);

    public ViksTvProvider(ProviderSourceConfiguration source)
    {
        _source = source;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect = true
        });
    }

    public string Id => _source.Id;
    public string Name => _source.Name;
    public Version Version => new(1, 0);

    public async Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var catalogUri = GetCatalogUri();
        Log($"Loading catalog {catalogUri}");
        var document = await GetDocumentAsync(catalogUri, catalogUri, cancellationToken);
        _channelPages.Clear();

        var channels = new List<ChannelInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = anchor.GetAttribute("href")?.Trim();
            if (string.IsNullOrEmpty(href) || !Uri.TryCreate(catalogUri, href, out var pageUri))
                continue;
            var pagePath = pageUri.AbsolutePath.Trim('/');
            var pageSlug = Path.GetFileNameWithoutExtension(pagePath);
            if (!string.Equals(pageUri.Host, catalogUri.Host, StringComparison.OrdinalIgnoreCase) ||
                !pageUri.AbsolutePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(pagePath) || pagePath.Contains('/') ||
                pageSlug.Equals("chto-seychas-na-tv", StringComparison.OrdinalIgnoreCase) ||
                pageSlug.Equals("izbran", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = anchor.TextContent.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || !seen.Add(pageUri.AbsoluteUri))
                continue;

            var imageSrc = anchor.QuerySelector("img")?.GetAttribute("src");
            var logo = Uri.TryCreate(catalogUri, imageSrc, out var imageUri) ? imageUri.AbsoluteUri : null;
            _channelPages[pageUri.AbsoluteUri] = pageUri;
            channels.Add(new ChannelInfo(pageUri.AbsoluteUri, name, "Viks.TV", logo));
        }

        Log($"Catalog parsed: {channels.Count} channel(s)");
        return channels;
    }

    public async Task<StreamInfo> ResolveStreamAsync(string channelId, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(channelId, UriKind.Absolute, out var channelUri) ||
            !_channelPages.TryGetValue(channelUri.AbsoluteUri, out channelUri))
            throw new KeyNotFoundException("Channel is not in the current Viks.TV catalog. Reload the source and try again.");

        var currentUri = channelUri;
        var referrer = GetCatalogUri();
        for (var depth = 0; depth < MaxFrameDepth; depth++)
        {
            Log($"Loading channel/player page {currentUri}");
            var document = await GetDocumentAsync(currentUri, referrer, cancellationToken, isIframe: depth > 0);

            var kodk = FindJavaScriptValue(document, KodkPattern);
            var encryptedFile = FindJavaScriptValue(document, EncryptedFilePattern);
            var kos = FindJavaScriptValue(document, KosPattern);
            var directUrl = FindJavaScriptValue(document, DirectFilePattern);
            string? streamUrl = null;
            if (!string.IsNullOrWhiteSpace(kodk) && !string.IsNullOrWhiteSpace(kos) && !string.IsNullOrWhiteSpace(encryptedFile))
            {
                var normalizedKodk = NormalizeUrl(kodk);
                if (normalizedKodk != null)
                {
                    var decryptedFile = DecryptPlayerJsFile(encryptedFile, normalizedKodk, kos);
                    if (string.Equals(decryptedFile, normalizedKodk, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("PlayerJS returned only the short kodk URL; the access-token suffix is missing.");
                    streamUrl = NormalizeUrl(decryptedFile);
                    if (streamUrl == null)
                        throw new InvalidOperationException("PlayerJS decryption did not produce a valid HTTP stream URL.");
                }
            }

            // Some Viks player pages expose an already complete HLS URL directly.
            streamUrl ??= NormalizeUrl(directUrl);
            if (streamUrl != null && IsHls(streamUrl))
            {
                Log($"Resolved Viks stream URL: {streamUrl}");
                Log($"VLC headers: User-Agent={UserAgent}; Referer={SiteOrigin}/; Origin={SiteOrigin}");
                return new StreamInfo(AddVlcHeaders(streamUrl));
            }

            var frameSrc = document.QuerySelector("iframe[src]")?.GetAttribute("src")?.Trim();
            if (string.IsNullOrWhiteSpace(frameSrc) || !Uri.TryCreate(currentUri, frameSrc, out var frameUri))
                break;
            if (frameUri.Scheme is not ("http" or "https"))
                break;

            Log($"Following player iframe {frameUri}");
            referrer = currentUri;
            currentUri = frameUri;
        }

        var message = "Could not find a current Viks.TV HLS URL (kodk) in the channel page or its player iframe.";
        Log($"Stream resolution failed for {channelUri}: {message}");
        throw new InvalidOperationException(message);
    }

    public async Task<ProviderProgramGuide?> GetProgramGuideAsync(
        string channelId,
        IReadOnlyCollection<string> channelNames,
        CancellationToken cancellationToken = default)
    {
        if (!_channelPages.TryGetValue(channelId, out var channelUri) &&
            (!Uri.TryCreate(channelId, UriKind.Absolute, out channelUri) ||
             !string.Equals(channelUri.Host, "live.viks.tv", StringComparison.OrdinalIgnoreCase)))
            return null;

        Log($"Loading Viks programme schedule for {channelUri}");
        var document = await GetDocumentAsync(channelUri, channelUri, cancellationToken);
        var schedule = document.QuerySelector(".Teleprograma");
        if (schedule == null)
        {
            Log($"No programme schedule found on {channelUri}");
            return null;
        }

        var activeDayName = schedule.QuerySelector(".tabs__caption li.active")?.TextContent.Trim();
        var activeDay = ParseRussianDay(activeDayName) ?? DateTime.Today.DayOfWeek;
        var daysUntilSchedule = ((int)activeDay - (int)DateTime.Today.DayOfWeek + 7) % 7;
        var scheduleDate = DateTime.Today.AddDays(daysUntilSchedule);
        var activeContent = schedule.QuerySelector(".tabs__content.active") ?? schedule.QuerySelector(".tabs__content");
        var rows = activeContent?.QuerySelectorAll(".epg_prog li").ToArray() ?? Array.Empty<AngleSharp.Dom.IElement>();
        var items = new List<(TimeOnly Time, string Title)>();
        foreach (var row in rows)
        {
            var timeText = row.QuerySelector(".time")?.TextContent.Trim();
            var title = row.QuerySelector(".prname2")?.TextContent.Trim();
            if (!TimeOnly.TryParseExact(timeText, "HH:mm", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var time) || string.IsNullOrWhiteSpace(title))
                continue;
            items.Add((time, title));
        }

        if (items.Count == 0)
        {
            Log($"Programme schedule contained no parseable entries for {channelUri}");
            return null;
        }

        var starts = new List<DateTime>(items.Count);
        var currentDate = scheduleDate;
        TimeOnly? previousTime = null;
        foreach (var item in items)
        {
            if (previousTime.HasValue && item.Time < previousTime.Value)
                currentDate = currentDate.AddDays(1);
            starts.Add(currentDate.Add(item.Time.ToTimeSpan()));
            previousTime = item.Time;
        }

        var programs = new List<ProviderProgramInfo>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var end = index + 1 < starts.Count
                ? starts[index + 1]
                : starts[0].AddDays(1);
            if (end <= starts[index])
                continue;
            programs.Add(new ProviderProgramInfo(starts[index], end, items[index].Title));
        }

        var displayName = document.QuerySelector("h1")?.TextContent.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName)
            ? channelNames.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? channelUri.AbsolutePath
            : displayName;
        Log($"Parsed {programs.Count} Viks programme entries for {displayName}; schedule day={activeDayName ?? activeDay.ToString()}");
        return new ProviderProgramGuide(channelUri.AbsoluteUri, displayName, programs);
    }

    private static DayOfWeek? ParseRussianDay(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "понедельник" => DayOfWeek.Monday,
        "вторник" => DayOfWeek.Tuesday,
        "среда" => DayOfWeek.Wednesday,
        "четверг" => DayOfWeek.Thursday,
        "пятница" => DayOfWeek.Friday,
        "суббота" => DayOfWeek.Saturday,
        "воскресенье" => DayOfWeek.Sunday,
        _ => null
    };

    private static string DecryptPlayerJsFile(string encryptedFile, string kodk, string kos)
    {
        var value = encryptedFile.Trim();
        for (var pass = 0; pass < 2 && value.StartsWith("#2", StringComparison.Ordinal); pass++)
        {
            var payload = value[2..];
            for (var index = PlayerFileKeys.Length - 1; index >= 0; index--)
            {
                var marker = PlayerFileSeparator + Convert.ToBase64String(Encoding.UTF8.GetBytes(PlayerFileKeys[index]));
                var markerIndex = payload.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex >= 0)
                    payload = payload.Remove(markerIndex, marker.Length);
            }

            try
            {
                var bytes = Convert.FromBase64String(payload);
                value = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
            {
                throw new InvalidOperationException($"Could not decode PlayerJS file layer {pass + 1}.", ex);
            }
        }

        // Viks configures PlayerJS placeholders v1 and v2 to the page globals kodk and kos.
        value = value.Replace("{v1}", kodk, StringComparison.OrdinalIgnoreCase)
            .Replace("{v2}", kos, StringComparison.OrdinalIgnoreCase);
        if (value.Contains("{v", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The decrypted PlayerJS URL contains an unresolved placeholder.");
        if (!value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The decrypted PlayerJS file is not an absolute HTTP URL.");

        return value;
    }

    private async Task<AngleSharp.Dom.IDocument> GetDocumentAsync(
        Uri uri,
        Uri referer,
        CancellationToken cancellationToken,
        bool isIframe = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9,ru;q=0.8");
        request.Headers.Referrer = referer;
        request.Headers.TryAddWithoutValidation("Origin", SiteOrigin);
        request.Headers.TryAddWithoutValidation("sec-fetch-dest", isIframe ? "iframe" : "document");
        request.Headers.TryAddWithoutValidation("sec-fetch-mode", "navigate");
        request.Headers.TryAddWithoutValidation("sec-fetch-site",
            string.Equals(referer.Host, uri.Host, StringComparison.OrdinalIgnoreCase) ? "same-origin" : "none");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            Log($"HTTP {(int)response.StatusCode} {response.StatusCode} for {response.RequestMessage?.RequestUri ?? uri}");
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return await _parser.ParseDocumentAsync(html, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log($"GET failed for {uri}: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private Uri GetCatalogUri()
    {
        var value = _source.Settings.TryGetValue("catalogUrl", out var configured) && !string.IsNullOrWhiteSpace(configured)
            ? configured.Trim()
            : "https://live.viks.tv/";
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            return uri;
        throw new InvalidOperationException("Viks.TV catalog URL must be an absolute HTTP or HTTPS URL.");
    }

    private static string? FindJavaScriptValue(AngleSharp.Dom.IDocument document, Regex pattern)
    {
        foreach (var script in document.Scripts)
        {
            var match = pattern.Match(script.TextContent);
            if (match.Success)
                return match.Groups["url"].Success
                    ? match.Groups["url"].Value
                    : match.Groups["value"].Value;
        }
        return null;
    }

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().Replace("\\:", ":", StringComparison.Ordinal).Replace("\\/", "/", StringComparison.Ordinal);
        return Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.AbsoluteUri
            : null;
    }

    private static bool IsHls(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);

    private static string AddVlcHeaders(string url)
    {
        var suffix = $"user-agent={Uri.EscapeDataString(UserAgent)}" +
                     $"&referer={Uri.EscapeDataString(SiteOrigin + "/")}" +
                     $"&origin={Uri.EscapeDataString(SiteOrigin)}";
        return $"{url}|{suffix}";
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ViksTvPlugin] {message}{Environment.NewLine}";
        System.Diagnostics.Debug.Write(line);
        Console.Write(line);
        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVPlayer", "Logs", "player.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, line);
        }
        catch { /* Logging must never interrupt playback. */ }
    }

    public void Dispose() => _httpClient.Dispose();
}
