using TV_Player.Maui.Services;

namespace TV_Player.Maui;

public partial class PlaylistPage : ContentPage
{
    private readonly AppSession _session;
    private static bool _startupUpdateChecked;
    private CancellationTokenSource? _updateCheck;
    public IReadOnlyList<SourceItem> Sources => _session.Sources.Keys.Select(name => new SourceItem(name)).ToList();

    public PlaylistPage(AppSession session)
    {
        InitializeComponent();
        if (DeviceLayout.IsTelevision)
        {
            SourcesView.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 };
            var sourceStyle = (Style)Resources["SourceButtonStyle"];
            sourceStyle.Setters.First(setter => setter.Property == VisualElement.HeightRequestProperty).Value = 64d;
            sourceStyle.Setters.Add(new Setter { Property = VisualElement.WidthRequestProperty, Value = 320d });
            sourceStyle.Setters.Add(new Setter { Property = View.HorizontalOptionsProperty, Value = LayoutOptions.Start });
            UpdateAvailableButton.HorizontalOptions = LayoutOptions.Start;
            UpdateAvailableButton.HeightRequest = 48;
        }
        _session = session;
        BindingContext = this;
        ClockLabel.Text = DateTime.Now.ToString("HH:mm");
        Dispatcher.StartTimer(TimeSpan.FromSeconds(30), () => { ClockLabel.Text = DateTime.Now.ToString("HH:mm"); return true; });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SourcesView.ItemsSource = Sources;
        if (!_startupUpdateChecked)
        {
            _startupUpdateChecked = true;
            _updateCheck = new CancellationTokenSource();
            _ = CheckForUpdateAsync(_updateCheck.Token);
        }
    }

    protected override void OnDisappearing()
    {
        _updateCheck?.Cancel();
        _updateCheck?.Dispose();
        _updateCheck = null;
        base.OnDisappearing();
    }

    private async Task CheckForUpdateAsync(CancellationToken token)
    {
        try
        {
            var updater = AndroidAppUpdater.Current;
            updater.CleanOldDownloads();
            if (updater.IsStoreBuild) return;
            var result = await updater.CheckAsync(token);
            if (token.IsCancellationRequested || result.Update == null) return;
            UpdateAvailableButton.Text = $"Доступна версия {result.Update.Manifest.VersionName} — обновить";
            UpdateAvailableButton.IsVisible = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"Startup update check: {ex.Message}");
        }
    }

    private async void OnUpdateClicked(object sender, EventArgs e) => await Navigation.PushAsync(new UpdatePage());

    private async void OnSourceClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not SourceItem source) return;
        _session.SelectSource(source.Name);
        await Navigation.PushAsync(new GroupPage(_session, source.Name));
    }

    private async void OnSettingsClicked(object sender, EventArgs e) =>
        await Navigation.PushAsync(new SettingsPage(_session));

    public sealed record SourceItem(string Name);
}
