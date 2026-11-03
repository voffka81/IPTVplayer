using Avalonia.Threading;
using TV_Player.Providers;
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
    private readonly StreamResolutionService _streamResolver;
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
        _streamResolver = new StreamResolutionService(TVPlayerViewModel.Instance.CurrentProgramsData?.Provider,
            ProviderResolutionMode.PreferProvider);
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
        _streamResolver.Cancel();
        StreamUrl = string.Empty;
        FallbackStreamUrls = Array.Empty<string>();
        SetPlaybackStatus(string.Empty);
        IsBuffering = true;
        _ = ResolveCurrentStreamAsync(_currentProgram);
        ProgramBar.SetChannel(_currentProgram);
    }

    private async Task ResolveCurrentStreamAsync(M3UInfo channel)
    {
        var result = await _streamResolver.ResolveAsync(channel);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_disposed || !_streamResolver.IsCurrent(result)) return;
            if (result.Status == StreamResolutionStatus.Resolved)
            {
                FallbackStreamUrls = result.Stream!.FallbackUrls;
                IsBuffering = true;
                SetPlaybackStatus(string.Empty);
                StreamUrl = result.Stream.Url;
            }
            else
            {
                IsBuffering = false;
                if (result.Status == StreamResolutionStatus.NoStream)
                    SetPlaybackStatus("No stream URL available for this channel.");
                else
                {
                    Debug.WriteLine($"Stream resolution failed: {result.Error}");
                    SetPlaybackStatus($"Cannot resolve stream: {result.Error?.Message}");
                }
            }
        });
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
        _streamResolver.Dispose();
        _programSubscription.Dispose();
        ChannelPicker.ChannelChanged -= OnChannelChanged;
        ProgramBar.Dispose();
    }
}

