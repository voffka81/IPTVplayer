using TV_Player.Maui.Services;
using TV_Player.Updates;

namespace TV_Player.Maui;

public partial class UpdatePage : ContentPage
{
    private readonly AndroidAppUpdater _updater = AndroidAppUpdater.Current;
    private AvailableUpdate? _available;
    private string? _downloaded;
    private CancellationTokenSource? _operation;
    private bool _initializing = true;
    private bool _appeared;
    private bool _visible;

    public UpdatePage()
    {
        InitializeComponent();
        InstalledLabel.Text = TV_Player.Localization.I18n.Format("T56de2d982241", _updater.InstalledVersion, _updater.InstalledVersionCode);
        ChannelPicker.ItemsSource = new[] { "Release", "Dev" };
        ChannelPicker.SelectedIndex = _updater.Channel == UpdateChannel.Dev ? 1 : 0;
        foreach (var button in new[] { CheckButton, DownloadButton, InstallButton, CancelButton })
        {
            button.Focused += (_, _) => { button.BorderColor = Color.FromArgb("#7BD0FF"); button.BorderWidth = 2; };
            button.Unfocused += (_, _) => { button.BorderColor = Color.FromArgb("#304054"); button.BorderWidth = 1; };
        }
        _initializing = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        if (_operation == null)
        {
            CheckButton.IsEnabled = ChannelPicker.IsEnabled = DownloadButton.IsEnabled = InstallButton.IsEnabled = true;
            CancelButton.IsVisible = DownloadProgress.IsVisible = false;
        }
        if (!_appeared)
        {
            _appeared = true;
            _ = CheckAsync();
        }
        if (_downloaded != null && _updater.CanInstall)
            UpdateStatus.Text = TV_Player.Localization.I18n.Get("T169a319f79dd");
    }

    protected override void OnDisappearing()
    {
        _visible = false;
        _operation?.Cancel();
        base.OnDisappearing();
    }

    private async void OnChannelChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        _updater.Channel = ChannelPicker.SelectedIndex == 1 ? UpdateChannel.Dev : UpdateChannel.Release;
        await CheckAsync();
    }

    private async void OnCheckClicked(object sender, EventArgs e) => await CheckAsync();

    private async Task CheckAsync()
    {
        if (_operation != null) return;
        _available = null;
        DeleteDownload();
        AvailableLabel.Text = ReleaseNotes.Text = string.Empty;
        DownloadButton.IsVisible = InstallButton.IsVisible = false;
        if (_updater.IsStoreBuild)
        {
            UpdateStatus.Text = TV_Player.Localization.I18n.Get("Te61e89117c28");
            CheckButton.IsEnabled = ChannelPicker.IsEnabled = false;
            return;
        }
        await RunAsync(async token =>
        {
            UpdateStatus.Text = TV_Player.Localization.I18n.Get("T3c79086e779e");
            var result = await _updater.CheckAsync(token);
            token.ThrowIfCancellationRequested();
            _available = result.Update;
            UpdateStatus.Text = result.Status switch
            {
                UpdateCheckStatus.Available => TV_Player.Localization.I18n.Get("T71439f9e99e9"),
                UpdateCheckStatus.UpToDate => TV_Player.Localization.I18n.Get("T86f96cd8c85a"),
                _ => TV_Player.Localization.I18n.Get("T035b8311150a")
            };
            if (_available != null)
            {
                AvailableLabel.Text = TV_Player.Localization.I18n.Format("T25b97088046f", _available.Manifest.VersionName, _available.Manifest.VersionCode);
                ReleaseNotes.Text = _available.Notes.Length > 8000 ? _available.Notes[..8000] : _available.Notes;
                DownloadButton.IsVisible = true;
            }
        });
    }

    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        if (_available == null || _operation != null) return;
        var update = _available;
        await RunAsync(async token =>
        {
            DeleteDownload();
            DownloadProgress.Progress = 0;
            DownloadProgress.IsVisible = true;
            var progress = new Progress<TV_Player.Updates.DownloadProgress>(value =>
            {
                if (!_visible || token.IsCancellationRequested || _operation == null || _operation.Token != token) return;
                DownloadProgress.Progress = (double)value.Received / value.Total;
                UpdateStatus.Text = TV_Player.Localization.I18n.Format("Tc5c284232a10", value.Received / 1048576.0, value.Total / 1048576.0);
            });
            var path = await _updater.DownloadAsync(update, progress, token);
            if (token.IsCancellationRequested || !_visible) { File.Delete(path); token.ThrowIfCancellationRequested(); return; }
            _downloaded = path;
            DownloadButton.IsVisible = false;
            InstallButton.IsVisible = true;
            UpdateStatus.Text = TV_Player.Localization.I18n.Get("T7d604c92dc15");
        });
    }

    private void OnInstallClicked(object sender, EventArgs e)
    {
        if (_downloaded == null || _available == null) return;
        try
        {
            if (!_updater.CanInstall)
            {
                UpdateStatus.Text = TV_Player.Localization.I18n.Get("T441fa3dbda52");
                _updater.OpenInstallPermission();
                return;
            }
            _updater.Install(_downloaded, _available.Manifest);
            UpdateStatus.Text = TV_Player.Localization.I18n.Get("Tde459516621d");
        }
        catch (Exception ex) { UpdateStatus.Text = TV_Player.Localization.I18n.Get("T0a170d46c187") + ex.Message; }
    }

    private void OnCancelClicked(object sender, EventArgs e) => _operation?.Cancel();

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        using var operation = new CancellationTokenSource();
        _operation = operation;
        CheckButton.IsEnabled = ChannelPicker.IsEnabled = DownloadButton.IsEnabled = InstallButton.IsEnabled = false;
        CancelButton.IsVisible = true;
        try { await action(operation.Token); }
        catch (OperationCanceledException)
        {
            if (_visible) UpdateStatus.Text = operation.IsCancellationRequested ? TV_Player.Localization.I18n.Get("T8a066b9b21ae") : TV_Player.Localization.I18n.Get("Tdb5f446b3fb0");
        }
        catch (Exception ex)
        {
            if (_visible) UpdateStatus.Text = TV_Player.Localization.I18n.Get("Td2b90a91f484") + ex.Message + TV_Player.Localization.I18n.Get("Tda8725a18704");
        }
        finally
        {
            _operation = null;
            if (_visible)
            {
                CheckButton.IsEnabled = ChannelPicker.IsEnabled = DownloadButton.IsEnabled = InstallButton.IsEnabled = true;
                CancelButton.IsVisible = false;
                DownloadProgress.IsVisible = false;
                if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is MainActivity activity && activity.IsRemoteNavigation)
                    (InstallButton.IsVisible ? InstallButton : DownloadButton.IsVisible ? DownloadButton : CheckButton).Focus();
            }
        }
    }

    private void DeleteDownload()
    {
        if (_downloaded == null) return;
        try { File.Delete(_downloaded); } catch (IOException) { }
        _downloaded = null;
    }
}
