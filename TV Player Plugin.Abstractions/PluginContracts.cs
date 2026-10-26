namespace TV_Player.Plugins;

public interface IPlayerPlugin
{
    string Id { get; }
    string Name { get; }
    Version Version { get; }
}

public interface IChannelProvider : IPlayerPlugin
{
    Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default);
    Task<StreamInfo> ResolveStreamAsync(string channelId, CancellationToken cancellationToken = default);
}

public sealed record ChannelInfo(
    string Id,
    string Name,
    string GroupTitle,
    string? Logo = null,
    string? EpgId = null);

public sealed record StreamInfo(
    string PrimaryUrl,
    IReadOnlyList<string>? FallbackUrls = null,
    DateTimeOffset? ExpiresAt = null);
