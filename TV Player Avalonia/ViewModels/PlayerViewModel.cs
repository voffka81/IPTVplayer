using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows.Input;

namespace TV_Player.AvaloniaApp.ViewModels;

public class PlayerViewModel : TV_Player.ObservableViewModelBase, IDisposable
{
    private readonly IDisposable _programSubscription;
    private M3UInfo _currentProgram;
    private IReadOnlyList<string> _fallbackStreamUrls = Array.Empty<string>();
    private CancellationTokenSource? _resolveCts;
    private bool _disposed;

    public ICommand BackCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand FullscreenCommand { get; }
    public ICommand ToggleChannelPickerCommand { get; }
    public ICommand CloseAppCommand { get; }
    public ICommand OpenStreamCommand { get; }

    public PlayerChannelPickerViewModel ChannelPicker { get; }

    public bool ChannelPickerVisible
    {
        get => ChannelPicker.ChannelPickerVisible;
        set => ChannelPicker.ChannelPickerVisible = value;
    }

    private string _topPanelTitle = string.Empty;
    public string TopPanelTitle
    {
        get => _topPanelTitle;
        set => SetProperty(ref _topPanelTitle, value);
    }

    public PlayerProgramBarViewModel ProgramBar { get; } = new();

    private bool _isBuffering;
    public bool IsBuffering
    {
        get => _isBuffering;
        set
        {
            if (SetProperty(ref _isBuffering, value)) ProgramBar.IsBuffering = value;
        }
    }

    public bool ProgramGuideVisible
    {
        get => ProgramBar.ProgramGuideVisible;
        set => ProgramBar.ProgramGuideVisible = value;
    }

    private string _streamUrl = string.Empty;
    public string StreamUrl
    {
        get => _streamUrl;
        set => SetProperty(ref _streamUrl, value);
    }

    public IReadOnlyList<string> FallbackStreamUrls
    {
        get => _fallbackStreamUrls;
        private set => SetProperty(ref _fallbackStreamUrls, value);
    }

    private string _playbackStatus = string.Empty;
    public string PlaybackStatus
    {
        get => _playbackStatus;
        set => SetProperty(ref _playbackStatus, value);
    }

    private bool _hasPlaybackStatus;
    public bool HasPlaybackStatus
    {
        get => _hasPlaybackStatus;
        set => SetProperty(ref _hasPlaybackStatus, value);
    }

    public PlayerViewModel(M3UInfo selectedProgram)
    {
        _currentProgram = selectedProgram;
        BackCommand = new RelayCommand(OnButtonBackClick);
        SettingsCommand = new RelayCommand(TVPlayerViewModel.Instance.ShowSettingsScreen);
        ChannelPicker = new PlayerChannelPickerViewModel(selectedProgram);
        ChannelPicker.ChannelChanged += OnChannelChanged;
        ToggleChannelPickerCommand = new RelayCommand(() => ChannelPickerVisible = !ChannelPickerVisible);
        FullscreenCommand = new RelayCommand(TVPlayerViewModel.Instance.FullScreenToggle);
        CloseAppCommand = new RelayCommand(TVPlayerViewModel.Instance.CloseAppCommand);
        OpenStreamCommand = new RelayCommand(OpenStreamExternally);

        _programSubscription = TVPlayerViewModel.Instance.CurrentProgramsData!
            .AllPrograms
            .Subscribe(programs =>
            {
                var groupPrograms = programs.Where(p => p.GroupTitle == _currentProgram.GroupTitle).ToList();
                Dispatcher.UIThread.Post(() =>
                {
                    ChannelPicker.SetChannels(groupPrograms);
                });
            });

        UpdateUi();
    }

    public void SetPlaybackStatus(string status)
    {
        PlaybackStatus = status;
        HasPlaybackStatus = !string.IsNullOrWhiteSpace(status);
    }

    private void UpdateUi()
    {
        ChannelPicker.SyncCurrentChannel(_currentProgram);
        TVPlayerViewModel.Instance.TopPanelVisible(false, _currentProgram.Name);
        TopPanelTitle = _currentProgram.Name;
        _resolveCts?.Cancel();
        _resolveCts?.Dispose();
        _resolveCts = new CancellationTokenSource();
        StreamUrl = string.Empty;
        FallbackStreamUrls = Array.Empty<string>();
        SetPlaybackStatus(string.Empty);
        IsBuffering = true;
        _ = ResolveCurrentStreamAsync(_currentProgram, _resolveCts.Token);
        ProgramBar.SetChannel(_currentProgram);
    }

    private async Task ResolveCurrentStreamAsync(M3UInfo channel, CancellationToken token)
    {
        try
        {
            var provider = TVPlayerViewModel.Instance.CurrentProgramsData?.Provider;
            var url = channel.Url;
            IReadOnlyList<string> fallbackUrls = Array.Empty<string>();
            if (provider != null && channel.ProviderId == provider.Id && channel.ChannelId != null)
            {
                var stream = await provider.ResolveStreamAsync(channel.ChannelId, token);
                url = stream.PrimaryUrl;
                fallbackUrls = stream.FallbackUrls ?? Array.Empty<string>();
            }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || _disposed) return;
                FallbackStreamUrls = fallbackUrls;
                IsBuffering = !string.IsNullOrWhiteSpace(url);
                SetPlaybackStatus(string.IsNullOrWhiteSpace(url)
                    ? "No stream URL available for this channel."
                    : string.Empty);
                StreamUrl = url;
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"Stream resolution failed: {ex}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested && !_disposed)
                {
                    IsBuffering = false;
                    SetPlaybackStatus($"Cannot resolve stream: {ex.Message}");
                }
            });
        }
    }

    public void SwitchChannel(int direction) => ChannelPicker.SwitchChannel(direction);

    private void OnChannelChanged(M3UInfo channel)
    {
        _currentProgram = channel;
        UpdateUi();
    }

    private void OpenStreamExternally()
    {
        if (string.IsNullOrWhiteSpace(StreamUrl))
            return;

        var externalUrl = StreamUrl.Split('|', 2)[0];

        Process.Start(new ProcessStartInfo
        {
            FileName = externalUrl,
            UseShellExecute = true
        });
    }

    private void OnButtonBackClick()
    {
        TVPlayerViewModel.Instance.ShowProgramsListScreen(new GroupInfo
        {
            Name = _currentProgram.GroupTitle,
            Count = 0
        });
    }

    public void Dispose()
    {
        _disposed = true;
        _resolveCts?.Cancel();
        _resolveCts?.Dispose();
        _programSubscription.Dispose();
        ChannelPicker.ChannelChanged -= OnChannelChanged;
        ProgramBar.Dispose();
    }
}

