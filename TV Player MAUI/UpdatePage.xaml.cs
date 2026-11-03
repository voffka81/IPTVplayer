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
        InstalledLabel.Text = $"Установлена версия {_updater.InstalledVersion} ({_updater.InstalledVersionCode})";
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
            UpdateStatus.Text = "APK готов. Нажмите «Установить обновление» и подтвердите установку Android.";
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
            UpdateStatus.Text = "Для этой установки обновления доступны через Google Play.";
            CheckButton.IsEnabled = ChannelPicker.IsEnabled = false;
            return;
        }
        await RunAsync(async token =>
        {
            UpdateStatus.Text = "Проверка обновлений…";
            var result = await _updater.CheckAsync(token);
            token.ThrowIfCancellationRequested();
            _available = result.Update;
            UpdateStatus.Text = result.Status switch
            {
                UpdateCheckStatus.Available => "Доступно обновление. Скачивание начнётся после нажатия кнопки.",
                UpdateCheckStatus.UpToDate => "В выбранном канале более новой версии нет. Понижение версии не выполняется.",
                _ => "В выбранном канале пока нет совместимых опубликованных APK."
            };
            if (_available != null)
            {
                AvailableLabel.Text = $"Доступна версия {_available.Manifest.VersionName} ({_available.Manifest.VersionCode})";
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
                UpdateStatus.Text = $"Загружено {value.Received / 1048576.0:F1} из {value.Total / 1048576.0:F1} МБ";
            });
            var path = await _updater.DownloadAsync(update, progress, token);
            if (token.IsCancellationRequested || !_visible) { File.Delete(path); token.ThrowIfCancellationRequested(); return; }
            _downloaded = path;
            DownloadButton.IsVisible = false;
            InstallButton.IsVisible = true;
            UpdateStatus.Text = "APK проверен. Нажмите «Установить обновление». Android запросит подтверждение.";
        });
    }

    private void OnInstallClicked(object sender, EventArgs e)
    {
        if (_downloaded == null || _available == null) return;
        try
        {
            if (!_updater.CanInstall)
            {
                UpdateStatus.Text = "Разрешите установку из этого источника, вернитесь и снова нажмите «Установить обновление».";
                _updater.OpenInstallPermission();
                return;
            }
            _updater.Install(_downloaded, _available.Manifest);
            UpdateStatus.Text = "Подтвердите обновление в установщике Android. При отмене можно повторить установку.";
        }
        catch (Exception ex) { UpdateStatus.Text = "Не удалось начать установку: " + ex.Message; }
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
            if (_visible) UpdateStatus.Text = operation.IsCancellationRequested ? "Отменено. Можно повторить." : "Время ожидания истекло. Проверьте сеть и повторите.";
        }
        catch (Exception ex)
        {
            if (_visible) UpdateStatus.Text = "Не удалось обновить приложение: " + ex.Message + " Можно повторить.";
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
