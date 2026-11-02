using TV_Player.Providers;
using TV_Player.Plugins;
using TV_Player.ViewModels;
using TV_Player.Examples.HtmlPageStreamsPlugin;

namespace TV_Player.Maui.Services;

public sealed class AppSession : IDisposable
{
    public static AppSession Current { get; } = new();
    public Dictionary<string, ProgramsData> Sources { get; } = new(StringComparer.Ordinal);
    private readonly ChannelProviderRegistry _providerRegistry = new();
    public IReadOnlyCollection<IChannelProviderPlugin> ProviderPlugins => _providerRegistry.Plugins;
    public ProgramsData? CurrentSource { get; private set; }
    public string? CurrentSourceName { get; private set; }

    private AppSession()
    {
        SettingsModel.LoadSettings();
        _providerRegistry.RegisterPlugin(new ViksTvPlugin());
        _providerRegistry.RegisterPlugin(new KlubnichkaPlugin());
        LoadPlaylistSources();
    }

    private void LoadPlaylistSources()
    {
        foreach (var name in SettingsModel.PlaylistOrder.Where(SettingsModel.Playlists.ContainsKey)
                     .Concat(SettingsModel.Playlists.Keys.Except(SettingsModel.PlaylistOrder, StringComparer.Ordinal)))
            Sources[name] = new ProgramsData(name, SettingsModel.Playlists[name]);

        foreach (var configuration in SettingsModel.ProviderSources.Where(source => source.Enabled))
        {
            try
            {
                var provider = _providerRegistry.CreateProvider(configuration);
                Sources[configuration.Name] = new ProgramsData(configuration.Name, provider);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not initialize Android provider '{configuration.Name}': {ex.Message}");
            }
        }
    }

    public Task ReloadSourcesAsync()
    {
        foreach (var source in Sources.Values) source.Dispose();
        Sources.Clear();
        CurrentSource = null;
        CurrentSourceName = null;
        LoadPlaylistSources();
        return Task.CompletedTask;
    }

    public void SelectSource(string name)
    {
        CurrentSourceName = name;
        CurrentSource = Sources[name];
    }

    public void Dispose()
    {
        foreach (var source in Sources.Values)
            source.Dispose();
        Sources.Clear();
        _providerRegistry.Dispose();
    }
}
