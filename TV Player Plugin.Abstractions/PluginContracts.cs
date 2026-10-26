namespace TV_Player.Plugins;

public interface IPlayerPlugin
{
    string Id { get; }
    string Name { get; }
    Version Version { get; }
}

/// <summary>Installed plugin that creates configured channel-provider instances.</summary>
public interface IChannelProviderPlugin : IPlayerPlugin
{
    IReadOnlyList<PluginSettingDefinition> Settings { get; }
    IChannelProvider CreateProvider(ProviderSourceConfiguration configuration);
}

/// <summary>A user-configured source created by a provider plugin.</summary>
public sealed class ProviderSourceConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>One text setting shown in the host's generic plugin-source form.</summary>
public sealed record PluginSettingDefinition(
    string Key,
    string Label,
    bool IsRequired = false,
    string? DefaultValue = null,
    string? Description = null);

public interface IChannelProvider : IPlayerPlugin
{
    Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default);
    Task<StreamInfo> ResolveStreamAsync(string channelId, CancellationToken cancellationToken = default);
}

/// <summary>Optional XMLTV guide source for a channel provider.</summary>
public interface IProgramGuideSource
{
    string? ProgramGuideUrl { get; }
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
