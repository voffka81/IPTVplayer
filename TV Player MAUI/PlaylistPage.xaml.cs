using TV_Player.Maui.Services;

namespace TV_Player.Maui;

public partial class PlaylistPage : ContentPage
{
    private readonly AppSession _session;
    public IReadOnlyList<SourceItem> Sources => _session.Sources.Keys.Select(name => new SourceItem(name)).ToList();

    public PlaylistPage(AppSession session)
    {
        InitializeComponent();
        _session = session;
        BindingContext = this;
        ClockLabel.Text = DateTime.Now.ToString("HH:mm");
        Dispatcher.StartTimer(TimeSpan.FromSeconds(30), () => { ClockLabel.Text = DateTime.Now.ToString("HH:mm"); return true; });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SourcesView.ItemsSource = Sources;
    }

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
