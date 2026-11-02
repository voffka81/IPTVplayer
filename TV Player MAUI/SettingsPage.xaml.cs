using TV_Player.Maui.Services;
using TV_Player.Plugins;
using TV_Player.ViewModels;

namespace TV_Player.Maui;

public partial class SettingsPage : ContentPage
{
    private readonly AppSession _session;
    private readonly List<PlaylistItem> _items;
    private readonly List<ProviderSourceConfiguration> _providerSources;
    private readonly List<ProviderSettingEntry> _providerSettings = new();
    private IChannelProviderPlugin? _selectedProvider;

    public SettingsPage(AppSession session)
    {
        InitializeComponent();
        _session = session;
        _items = SettingsModel.Playlists.Select(pair => new PlaylistItem(pair.Key, pair.Value)).ToList();
        _providerSources = SettingsModel.ProviderSources.Select(CloneSource).ToList();
        PlaylistView.ItemsSource = _items;
        ProviderSourceView.ItemsSource = _providerSources;
        ProviderPicker.ItemsSource = _session.ProviderPlugins.OrderBy(plugin => plugin.Name).ToList();
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        var url = UrlEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            StatusLabel.Text = "Укажите имя и корректную HTTP/HTTPS ссылку.";
            return;
        }
        if (_items.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            _providerSources.Any(source => source.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusLabel.Text = "Источник с таким именем уже существует.";
            return;
        }
        _items.Add(new PlaylistItem(name, url!));
        PlaylistView.ItemsSource = null;
        PlaylistView.ItemsSource = _items;
        NameEntry.Text = UrlEntry.Text = string.Empty;
        StatusLabel.Text = "Добавлено. Нажмите «Сохранить».";
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not PlaylistItem item) return;
        _items.Remove(item);
        PlaylistView.ItemsSource = null;
        PlaylistView.ItemsSource = _items;
    }

    private void OnProviderSelected(object? sender, EventArgs e)
    {
        _selectedProvider = ProviderPicker.SelectedItem as IChannelProviderPlugin;
        ProviderNameEntry.Text = _selectedProvider?.Name ?? string.Empty;
        ProviderSettingsLayout.Children.Clear();
        _providerSettings.Clear();
        if (_selectedProvider == null) return;

        foreach (var definition in _selectedProvider.Settings)
        {
            if (!string.IsNullOrWhiteSpace(definition.Description))
                ProviderSettingsLayout.Children.Add(new Label
                {
                    Text = definition.Description,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#91A6BF")
                });

            var entry = new Entry
            {
                Placeholder = definition.Label + (definition.IsRequired ? " *" : string.Empty),
                Text = definition.DefaultValue ?? string.Empty,
                Keyboard = Uri.TryCreate(definition.DefaultValue, UriKind.Absolute, out _) ? Keyboard.Url : Keyboard.Default
            };
            ProviderSettingsLayout.Children.Add(entry);
            _providerSettings.Add(new ProviderSettingEntry(definition, entry));
        }
    }

    private void OnAddProviderSourceClicked(object sender, EventArgs e)
    {
        var provider = _selectedProvider;
        var name = ProviderNameEntry.Text?.Trim();
        if (provider == null || string.IsNullOrWhiteSpace(name))
        {
            StatusLabel.Text = "Выберите провайдера и укажите имя источника.";
            return;
        }
        if (_items.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            _providerSources.Any(source => source.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusLabel.Text = "Источник с таким именем уже существует.";
            return;
        }
        var missing = _providerSettings.FirstOrDefault(setting =>
            setting.Definition.IsRequired && string.IsNullOrWhiteSpace(setting.Entry.Text));
        if (missing != null)
        {
            StatusLabel.Text = $"Заполните поле «{missing.Definition.Label}».";
            return;
        }

        _providerSources.Add(new ProviderSourceConfiguration
        {
            Id = $"{provider.Id}:{Guid.NewGuid():N}",
            Name = name,
            PluginId = provider.Id,
            Enabled = true,
            Settings = _providerSettings.ToDictionary(setting => setting.Definition.Key,
                setting => setting.Entry.Text?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        });
        ProviderSourceView.ItemsSource = null;
        ProviderSourceView.ItemsSource = _providerSources;
        ProviderNameEntry.Text = provider.Name;
        StatusLabel.Text = "Источник добавлен. Нажмите «Сохранить».";
    }

    private void OnProviderSourceDeleteClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not ProviderSourceConfiguration source) return;
        _providerSources.Remove(source);
        ProviderSourceView.ItemsSource = null;
        ProviderSourceView.ItemsSource = _providerSources;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        SettingsModel.Playlists = _items.ToDictionary(item => item.Name, item => item.Url);
        SettingsModel.PlaylistOrder = _items.Select(item => item.Name).ToList();
        SettingsModel.ProviderSources = _providerSources;
        SettingsModel.SaveSetttings();
        StatusLabel.Text = "Настройки сохранены.";
        await _session.ReloadSourcesAsync();
    }

    public sealed record PlaylistItem(string Name, string Url);

    private static ProviderSourceConfiguration CloneSource(ProviderSourceConfiguration source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        PluginId = source.PluginId,
        Enabled = source.Enabled,
        Settings = new Dictionary<string, string>(source.Settings ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
    };

    private sealed record ProviderSettingEntry(PluginSettingDefinition Definition, Entry Entry);
}
