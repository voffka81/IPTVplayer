using TV_Player.Maui.Services;
using TV_Player;
using System.Reactive.Linq;

namespace TV_Player.Maui;

public partial class ChannelPage : ContentPage
{
    private readonly AppSession _session;
    private readonly string _groupName;
    private readonly List<M3UInfo> _allChannels = new();
    private IDisposable? _channelsSubscription;

    public ChannelPage(AppSession session, GroupInfo group)
    {
        InitializeComponent();
        _session = session;
        _groupName = group.Name;
        Title = group.Name;
        SizeChanged += (_, _) => ChannelGridLayout.Span = Math.Clamp((int)(Width / 185), 2, 6);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _channelsSubscription?.Dispose();
        _channelsSubscription = _session.CurrentSource!.AllPrograms.Subscribe(channels => MainThread.BeginInvokeOnMainThread(() =>
        {
            _allChannels.Clear();
            _allChannels.AddRange(channels.Where(channel => channel.GroupTitle == _groupName &&
                (!string.IsNullOrWhiteSpace(channel.Url) ||
                 (!string.IsNullOrWhiteSpace(channel.ProviderId) && !string.IsNullOrWhiteSpace(channel.ChannelId)))));
            ApplyFilter();
        }));
    }

    protected override void OnDisappearing()
    {
        _channelsSubscription?.Dispose();
        _channelsSubscription = null;
        base.OnDisappearing();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void ApplyFilter() => ChannelsView.ItemsSource = _allChannels
        .Where(channel => string.IsNullOrWhiteSpace(SearchEntry.Text) || channel.Name.Contains(SearchEntry.Text, StringComparison.CurrentCultureIgnoreCase))
        .Select(channel => new ChannelTile(channel.Name, channel.Logo, channel)).ToList();

    private async void OnChannelTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ChannelTile { Channel: not null } tile)
            await Navigation.PushAsync(new PlayerPage(tile.Channel, _allChannels, _session.CurrentSource!));
    }
}
