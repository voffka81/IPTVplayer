using TV_Player.Plugins;

namespace TV_Player.Providers;

/// <summary>Adapter for existing local and remote M3U playlists.</summary>
public sealed class M3UChannelProvider : IChannelProvider
{
    private readonly string _playlistLocation;
    private readonly Dictionary<string, M3UInfo> _channels = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public M3UChannelProvider(string id, string name, string playlistLocation)
    {
        Id = id;
        Name = name;
        _playlistLocation = playlistLocation;
    }

    public string Id { get; }
    public string Name { get; }
    public Version Version => new(1, 0);

    public async Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (programs, _) = await LoadPlaylistAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var catalog = new List<ChannelInfo>(programs.Count);
        for (var index = 0; index < programs.Count; index++)
        {
            var program = programs[index];
            catalog.Add(new ChannelInfo(program.ChannelId!, program.Name, program.GroupTitle,
                program.Logo, program.TvgID));
        }
        return catalog;
    }

    public async Task<(List<M3UInfo> programList, string programGuide)> LoadPlaylistAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var location = _playlistLocation;
        if (!Uri.TryCreate(location, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            if (!Path.IsPathRooted(location))
                location = Path.Combine(AppContext.BaseDirectory, location);
        }
        var result = await M3UParser.DownloadM3UFromWebAsync(location);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _channels.Clear();
            for (var i = 0; i < result.programList.Count; i++)
            {
                var channel = result.programList[i];
                channel.ProviderId = Id;
                channel.ChannelId = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _channels[channel.ChannelId] = channel;
            }
        }
        return result;
    }

    public Task<StreamInfo> ResolveStreamAsync(string channelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_channels.TryGetValue(channelId, out var channel))
                throw new KeyNotFoundException($"Unknown channel ID '{channelId}' for provider '{Id}'.");
            return Task.FromResult(new StreamInfo(channel.Url));
        }
    }
}
