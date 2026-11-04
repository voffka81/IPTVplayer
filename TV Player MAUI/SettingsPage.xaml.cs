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
    private Android.Views.View? _settingsRemoteTarget;

    public SettingsPage(AppSession session)
    {
        InitializeComponent();
        if (DeviceLayout.IsTelevision)
        {
            SettingsScroll.MaximumWidthRequest = 760;
            SettingsScroll.HorizontalOptions = LayoutOptions.Start;
            var buttonStyle = (Style)Resources["SettingsButtonStyle"];
            buttonStyle.Setters.Add(new Setter { Property = VisualElement.HeightRequestProperty, Value = 48d });
        }
        _session = session;
        LanguagePicker.ItemsSource = TV_Player.Localization.I18n.LanguageNames.ToList();
        LanguagePicker.SelectedIndex = SettingsModel.Language == "ru" ? 0 : 1;
        _items = SettingsModel.Playlists.Select(pair => new PlaylistItem(pair.Key, pair.Value)).ToList();
        _providerSources = SettingsModel.ProviderSources.Select(CloneSource).ToList();
        RefreshPlaylistView();
        ProviderSourceView.ItemsSource = _providerSources;
        ProviderPicker.ItemsSource = _session.ProviderPlugins.OrderBy(plugin => plugin.Name).ToList();
    }

    private async void OnUpdatesClicked(object sender, EventArgs e) => await Navigation.PushAsync(new UpdatePage());

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _settingsRemoteTarget = null;
        if (DeviceLayout.IsTelevision)
            MainActivity.PlayerKeyPressed += OnSettingsRemoteKey;
    }

    protected override void OnDisappearing()
    {
        MainActivity.PlayerKeyPressed -= OnSettingsRemoteKey;
        base.OnDisappearing();
    }

    private bool OnSettingsRemoteKey(TV_Player.Input.PlayerRemoteKey key, bool nativeControlFocused)
    {
        if (key is not (TV_Player.Input.PlayerRemoteKey.Up or TV_Player.Input.PlayerRemoteKey.Down) ||
            SettingsScroll.Handler?.PlatformView is not Android.Views.ViewGroup root || !root.HasWindowFocus)
            return false;

        var controls = new List<Android.Views.View>();
        void Collect(Android.Views.View view)
        {
            if (view.Visibility != Android.Views.ViewStates.Visible || !view.Enabled) return;
            if (view.Focusable && view is Android.Widget.Button or Android.Widget.EditText or Android.Widget.Spinner)
                controls.Add(view);
            else if (view is Android.Views.ViewGroup group)
                for (var i = 0; i < group.ChildCount; i++)
                    if (group.GetChildAt(i) is { } child) Collect(child);
        }
        Collect(root);
        if (controls.Count == 0) return false;
        var index = controls.IndexOf(_settingsRemoteTarget ?? root.FindFocus()!);
        var direction = key == TV_Player.Input.PlayerRemoteKey.Down ? 1 : -1;
        var next = index < 0 ? (direction > 0 ? 0 : controls.Count - 1)
            : Math.Clamp(index + direction, 0, controls.Count - 1);
        var target = controls[next];
        _settingsRemoteTarget = target;
        Android.Util.Log.Info("TVSettingsFocus", $"Move {direction}: {index} -> {next}, controls={controls.Count}");
        _ = RevealSettingsControlAsync(target, root);
        return true;
    }

    private async Task RevealSettingsControlAsync(Android.Views.View target, Android.Views.View root)
    {
        var targetPosition = new int[2];
        var viewportPosition = new int[2];
        target.GetLocationInWindow(targetPosition);
        root.GetLocationInWindow(viewportPosition);
        var density = root.Resources?.DisplayMetrics?.Density ?? 1f;
        var viewportHeight = root.Height / density;
        var top = (targetPosition[1] - viewportPosition[1]) / density;
        var bottom = top + target.Height / density;
        var scroll = SettingsScroll.ScrollY;
        if (top < 16) scroll += top - 16;
        else if (bottom > viewportHeight - 16) scroll += bottom - viewportHeight + 16;
        await SettingsScroll.ScrollToAsync(0, Math.Max(0, scroll), false);
        if (target.IsAttachedToWindow && root.HasWindowFocus) target.RequestFocus();
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        var url = UrlEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            StatusLabel.Text = TV_Player.Localization.I18n.Get("T0481a0dc1daf");
            return;
        }
        if (_items.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            _providerSources.Any(source => source.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusLabel.Text = TV_Player.Localization.I18n.Get("T767b248ef680");
            return;
        }
        _items.Add(new PlaylistItem(name, url!));
        RefreshPlaylistView();
        NameEntry.Text = UrlEntry.Text = string.Empty;
        StatusLabel.Text = TV_Player.Localization.I18n.Get("Te6fc663afd7f");
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not PlaylistItem item) return;
        _items.Remove(item);
        RefreshPlaylistView();
    }

    private void RefreshPlaylistView()
    {
        PlaylistView.ItemsSource = null;
        PlaylistView.ItemsSource = _items;
        PlaylistView.IsVisible = _items.Count > 0;
        PlaylistView.HeightRequest = _items.Count == 0 ? 0 : Math.Min(280, _items.Count * 64);
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
                    Text = TV_Player.Localization.I18n.TranslateKnown(definition.Description),
                    FontSize = 12,
                    TextColor = Color.FromArgb("#91A6BF")
                });

            var entry = new Entry
            {
                Placeholder = TV_Player.Localization.I18n.TranslateKnown(definition.Label) + (definition.IsRequired ? " *" : string.Empty),
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
            StatusLabel.Text = TV_Player.Localization.I18n.Get("T9a1d6cbbfcb1");
            return;
        }
        if (_items.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            _providerSources.Any(source => source.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusLabel.Text = TV_Player.Localization.I18n.Get("T767b248ef680");
            return;
        }
        var missing = _providerSettings.FirstOrDefault(setting =>
            setting.Definition.IsRequired && string.IsNullOrWhiteSpace(setting.Entry.Text));
        if (missing != null)
        {
            StatusLabel.Text = TV_Player.Localization.I18n.Format("Tf13de46c617e", missing.Definition.Label);
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
        StatusLabel.Text = TV_Player.Localization.I18n.Get("T5f44984beb63");
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
        var language = LanguagePicker.SelectedIndex == 0 ? "ru" : "en";
        var languageChanged = language != SettingsModel.Language;
        SettingsModel.Language = language;
        TV_Player.Localization.I18n.SetLanguage(language);
        SettingsModel.Playlists = _items.ToDictionary(item => item.Name, item => item.Url);
        SettingsModel.PlaylistOrder = _items.Select(item => item.Name).ToList();
        SettingsModel.ProviderSources = _providerSources;
        SettingsModel.SaveSetttings();
        StatusLabel.Text = TV_Player.Localization.I18n.Get("Tdb022e4a824b");
        await _session.ReloadSourcesAsync();
        if (languageChanged && Window is { } window)
            window.Page = new NavigationPage(new PlaylistPage(_session))
            {
                BarBackgroundColor = Color.FromArgb("#080F19"),
                BarTextColor = Colors.White
            };
        else
            await Navigation.PopAsync();
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
