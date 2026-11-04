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
        SubscribeToSource();
    }

    private void SubscribeToSource()
    {
        _groupSubscription?.Dispose();
        _groupSubscription = _session.CurrentSource!.GroupsInformation.Subscribe(groups => MainThread.BeginInvokeOnMainThread(() =>
        {
            RetryButton.IsVisible = groups.Count == 0;
            _allGroups.Clear();
            _allGroups.AddRange(groups);
            LoadStatus.Text = groups.Count == 0 ? TV_Player.Localization.I18n.Get("Tf901d2dc9965") : string.Empty;
            ApplyFilter();
        }));
        _errorSubscription?.Dispose();
        _errorSubscription = _session.CurrentSource.LoadErrors.Subscribe(error => MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadStatus.Text = TV_Player.Localization.I18n.Format("T2acfa47c3d0e", error);
            RetryButton.IsVisible = true;
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

    private void OnRetryClicked(object sender, EventArgs e)
    {
        RetryButton.IsVisible = false;
        LoadStatus.Text = TV_Player.Localization.I18n.Get("T8ead34a86610");
        _groupSubscription?.Dispose();
        _errorSubscription?.Dispose();
        try
        {
            _session.ReloadSource(Title);
            SubscribeToSource();
        }
        catch (Exception error)
        {
            LoadStatus.Text = TV_Player.Localization.I18n.Format("Td9dd05a865bb", error.Message);
            RetryButton.IsVisible = true;
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void ApplyFilter() => GroupsView.ItemsSource = _allGroups
        .Where(group => string.IsNullOrWhiteSpace(SearchEntry.Text) || group.Name.Contains(SearchEntry.Text, StringComparison.CurrentCultureIgnoreCase))
        .ToList();

    private async void OnGroupTapped(object sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is GroupInfo group)
            await Navigation.PushAsync(new ChannelPage(_session, group));
    }
}
