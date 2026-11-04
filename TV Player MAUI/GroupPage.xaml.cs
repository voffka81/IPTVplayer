using TV_Player.Maui.Services;
using TV_Player;
using System.Reactive.Linq;

namespace TV_Player.Maui;

public partial class GroupPage : ContentPage
{
    private readonly AppSession _session;
    private readonly List<GroupInfo> _allGroups = new();
    private IDisposable? _groupSubscription;
    private IDisposable? _errorSubscription;

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
            LoadStatus.Text = groups.Count == 0 ? "В плейлисте нет каналов." : string.Empty;
            ApplyFilter();
        }));
        _errorSubscription?.Dispose();
        _errorSubscription = _session.CurrentSource.LoadErrors.Subscribe(error => MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadStatus.Text = $"Не удалось загрузить плейлист: {error}";
        }));
    }

    protected override void OnDisappearing()
    {
        _groupSubscription?.Dispose();
        _groupSubscription = null;
        _errorSubscription?.Dispose();
        _errorSubscription = null;
        base.OnDisappearing();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void ApplyFilter() => GroupsView.ItemsSource = _allGroups
        .Where(group => string.IsNullOrWhiteSpace(SearchEntry.Text) || group.Name.Contains(SearchEntry.Text, StringComparison.CurrentCultureIgnoreCase))
        .ToList();

    private async void OnGroupTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is GroupInfo group)
            await Navigation.PushAsync(new ChannelPage(_session, group));
    }
}
