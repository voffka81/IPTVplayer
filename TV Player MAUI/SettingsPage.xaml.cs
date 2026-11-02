using TV_Player.Maui.Services;
using TV_Player.ViewModels;

namespace TV_Player.Maui;

public partial class SettingsPage : ContentPage
{
    private readonly AppSession _session;
    private readonly List<PlaylistItem> _items;

    public SettingsPage(AppSession session)
    {
        InitializeComponent();
        _session = session;
        _items = SettingsModel.Playlists.Select(pair => new PlaylistItem(pair.Key, pair.Value)).ToList();
        PlaylistView.ItemsSource = _items;
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
        if (_items.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusLabel.Text = "Плейлист с таким именем уже существует.";
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

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        SettingsModel.Playlists = _items.ToDictionary(item => item.Name, item => item.Url);
        SettingsModel.PlaylistOrder = _items.Select(item => item.Name).ToList();
        SettingsModel.SaveSetttings();
        StatusLabel.Text = "Настройки сохранены.";
        await _session.ReloadSourcesAsync();
    }

    public sealed record PlaylistItem(string Name, string Url);
}
