using System.ComponentModel;
using System.Runtime.CompilerServices;

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
public sealed class ProviderSourceConfiguration : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _enabled = true;

    public string Id { get; set; } = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
        }
    }
    public string PluginId { get; set; } = string.Empty;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            OnPropertyChanged();
        }
    }
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

/// <summary>Optional provider-supplied programme data for sources that do not use XMLTV.</summary>
public interface IChannelProgramGuideSource
{
    Task<ProviderProgramGuide?> GetProgramGuideAsync(
        string channelId,
        IReadOnlyCollection<string> channelNames,
        CancellationToken cancellationToken = default);
}

public sealed record ProviderProgramGuide(
    string Id,
    string DisplayName,
    IReadOnlyList<ProviderProgramInfo> Programs);

public sealed record ProviderProgramInfo(DateTime StartTime, DateTime EndTime, string Title);

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
