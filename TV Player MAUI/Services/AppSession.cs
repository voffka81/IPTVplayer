using TV_Player.Providers;
using TV_Player.ViewModels;

namespace TV_Player.Maui.Services;

public sealed class AppSession : IDisposable
{
    public static AppSession Current { get; } = new();
    public Dictionary<string, ProgramsData> Sources { get; } = new(StringComparer.Ordinal);
    public ProgramsData? CurrentSource { get; private set; }
    public string? CurrentSourceName { get; private set; }

    private AppSession()
    {
        SettingsModel.LoadSettings();
        LoadPlaylistSources();
        // MAUI Android cannot load arbitrary desktop plugin DLLs; built-in M3U sources stay shared.
    }

    private void LoadPlaylistSources()
    {
        foreach (var name in SettingsModel.PlaylistOrder.Where(SettingsModel.Playlists.ContainsKey)
                     .Concat(SettingsModel.Playlists.Keys.Except(SettingsModel.PlaylistOrder, StringComparer.Ordinal)))
            Sources[name] = new ProgramsData(name, SettingsModel.Playlists[name]);
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
    }
}
