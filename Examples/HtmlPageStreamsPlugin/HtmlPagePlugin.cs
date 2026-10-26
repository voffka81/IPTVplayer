using AngleSharp.Html.Parser;
using TV_Player.Plugins;

namespace TV_Player.Examples.HtmlPageStreamsPlugin;

public sealed class HtmlPagePlugin : IChannelProviderPlugin
{
    public string Id => "sample.html-page";
    public string Name => "HTML page streams";
    public Version Version => new(1, 0);

    public IReadOnlyList<PluginSettingDefinition> Settings =>
    [
        new("pageUrl", "Page URL", IsRequired: true, Description: "HTTP or HTTPS page to parse."),
        new("streamSelector", "Stream CSS selector", IsRequired: true, DefaultValue: "a[href]", Description: "Elements whose URL attribute points to a stream."),
        new("urlAttribute", "Stream URL attribute", DefaultValue: "href"),
        new("nameSelector", "Channel name selector", Description: "Optional child selector within each stream element."),
        new("groupSelector", "Group selector", Description: "Optional child selector within each stream element.")
    ];

    public IChannelProvider CreateProvider(ProviderSourceConfiguration configuration) =>
        new HtmlPageProvider(configuration);
}

internal sealed class HtmlPageProvider : IChannelProvider, IDisposable
{
    private readonly ProviderSourceConfiguration _source;
    private readonly HttpClient _httpClient = new();
    private readonly HtmlParser _parser = new();
    private readonly Dictionary<string, string> _streamUrls = new(StringComparer.Ordinal);

    public HtmlPageProvider(ProviderSourceConfiguration source) => _source = source;

    public string Id => _source.Id;
    public string Name => _source.Name;
    public Version Version => new(1, 0);

    public async Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var pageUri = GetPageUri();
        using var response = await _httpClient.GetAsync(pageUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await _parser.ParseDocumentAsync(html, cancellationToken);

        _streamUrls.Clear();
        var channels = new List<ChannelInfo>();
        var streamSelector = Setting("streamSelector", "a[href]");
        var urlAttribute = Setting("urlAttribute", "href");
        var nameSelector = Setting("nameSelector", string.Empty);
        var groupSelector = Setting("groupSelector", string.Empty);

        foreach (var element in document.QuerySelectorAll(streamSelector))
        {
            var rawUrl = element.GetAttribute(urlAttribute)?.Trim();
            if (string.IsNullOrWhiteSpace(rawUrl) || !Uri.TryCreate(pageUri, rawUrl, out var streamUri))
                continue;

            var channelName = SelectText(element, nameSelector);
            if (string.IsNullOrWhiteSpace(channelName))
                channelName = element.TextContent.Trim();
            if (string.IsNullOrWhiteSpace(channelName))
                channelName = streamUri.Host;

            var groupName = SelectText(element, groupSelector);
            var channelId = streamUri.AbsoluteUri;
            _streamUrls[channelId] = channelId;
            channels.Add(new ChannelInfo(channelId, channelName, groupName));
        }

        return channels;
    }

    public Task<StreamInfo> ResolveStreamAsync(string channelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_streamUrls.TryGetValue(channelId, out var streamUrl))
            throw new KeyNotFoundException($"Stream '{channelId}' is not in the current page results. Reload the source.");

        return Task.FromResult(new StreamInfo(streamUrl));
    }

    private Uri GetPageUri()
    {
        var value = Setting("pageUrl", string.Empty);
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return uri;

        throw new InvalidOperationException("Page URL must be an absolute HTTP or HTTPS URL.");
    }

    private string SelectText(AngleSharp.Dom.IParentNode element, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return string.Empty;

        return element.QuerySelector(selector)?.TextContent.Trim() ?? string.Empty;
    }

    private string Setting(string key, string fallback) =>
        _source.Settings.TryGetValue(key, out var value) ? value : fallback;

    public void Dispose() => _httpClient.Dispose();
}
