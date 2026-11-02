using TV_Player.Maui.Services;
using TV_Player;
using System.Reactive.Linq;

namespace TV_Player.Maui;

public partial class GroupPage : ContentPage
{
    private readonly AppSession _session;
    private readonly List<GroupInfo> _allGroups = new();
    private IDisposable? _groupSubscription;

    public GroupPage(AppSession session, string sourceName)
    {
        InitializeComponent();
        _session = session;
        Title = sourceName;
        SizeChanged += (_, _) => GroupGridLayout.Span = Math.Clamp((int)(Width / 210), 2, 6);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _groupSubscription?.Dispose();
        _groupSubscription = _session.CurrentSource!.GroupsInformation.Subscribe(groups => MainThread.BeginInvokeOnMainThread(() =>
        {
            _allGroups.Clear();
            _allGroups.AddRange(groups);
            ApplyFilter();
        }));
    }

    protected override void OnDisappearing()
    {
        _groupSubscription?.Dispose();
        _groupSubscription = null;
        base.OnDisappearing();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void ApplyFilter() => GroupsView.ItemsSource = _allGroups
        .Where(group => string.IsNullOrWhiteSpace(SearchEntry.Text) || group.Name.Contains(SearchEntry.Text, StringComparison.CurrentCultureIgnoreCase))
        .ToList();

    private async void OnGroupClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is GroupInfo group)
            await Navigation.PushAsync(new ChannelPage(_session, group));
    }
}
