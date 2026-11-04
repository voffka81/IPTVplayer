using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows.Input;
using TV_Player.Playback;
using TV_Player.Presentation;
using TV_Player.Providers;

namespace TV_Player.AvaloniaApp.ViewModels;

/// <summary>Desktop commands and bindings over the shared player presentation.</summary>
public class PlayerViewModel : TV_Player.ObservableViewModelBase, IDisposable
{
    private readonly IDisposable _programSubscription;
    private readonly IDisposable _guideSubscription;
    private bool _disposed;
    private string _renderedStreamUrl = string.Empty;
    private long _renderedChannelVersion;

    public PlayerPresentationModel Presentation { get; }
    public PlayerPlaybackModel Playback => Presentation.Playback;
    public PlayerChannelPickerViewModel ChannelPicker { get; }
    public PlayerProgramBarViewModel ProgramBar { get; }
    public ICommand BackCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand FullscreenCommand { get; }
    public ICommand ToggleChannelPickerCommand { get; }
    public ICommand CloseAppCommand { get; }
    public ICommand OpenStreamCommand { get; }

    public bool ChannelPickerVisible
    {
        get => ChannelPicker.ChannelPickerVisible;
        set => ChannelPicker.ChannelPickerVisible = value;
    }

    public bool ProgramGuideVisible
    {
        get => ProgramBar.ProgramGuideVisible;
        set => ProgramBar.ProgramGuideVisible = value;
    }

    public string TopPanelTitle => Presentation.Snapshot.ChannelTitle;
    public bool IsBuffering => Presentation.Snapshot.Playback.IsLoading;
    public string StreamUrl => Presentation.Snapshot.Stream?.Url ?? string.Empty;
    public IReadOnlyList<string> FallbackStreamUrls => Presentation.Snapshot.Stream?.FallbackUrls ?? Array.Empty<string>();

    private string _playbackStatus = string.Empty;
    public string PlaybackStatus { get => _playbackStatus; private set => SetProperty(ref _playbackStatus, value); }
    private bool _hasPlaybackStatus;
    public bool HasPlaybackStatus { get => _hasPlaybackStatus; private set => SetProperty(ref _hasPlaybackStatus, value); }

    public PlayerViewModel(M3UInfo selectedProgram)
    {
        var source = TVPlayerViewModel.Instance.CurrentProgramsData!;
        Presentation = new PlayerPresentationModel(new StreamResolutionService(source.Provider, ProviderResolutionMode.PreferProvider),
            async channel => await source.GetGuideByProgram(channel),
            async action => await Dispatcher.UIThread.InvokeAsync(action),
            options: new(GuideWindowOrigin.CurrentOrFirst, IncludeNextProgram: false),
            formatProvider: System.Globalization.CultureInfo.CurrentCulture);
        ChannelPicker = new PlayerChannelPickerViewModel(Presentation.Selection);
        ProgramBar = new PlayerProgramBarViewModel(Presentation);
        Presentation.Changed += RenderPresentation;
        BackCommand = new RelayCommand(OnButtonBackClick);
        SettingsCommand = new RelayCommand(TVPlayerViewModel.Instance.ShowSettingsScreen);
        ToggleChannelPickerCommand = new RelayCommand(() => ChannelPickerVisible = !ChannelPickerVisible);
        FullscreenCommand = new RelayCommand(TVPlayerViewModel.Instance.FullScreenToggle);
        CloseAppCommand = new RelayCommand(TVPlayerViewModel.Instance.CloseAppCommand);
        OpenStreamCommand = new RelayCommand(OpenStreamExternally);
        _programSubscription = source.AllPrograms.Subscribe(programs => Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            var group = Presentation.Snapshot.Channel?.GroupTitle ?? selectedProgram.GroupTitle;
            Presentation.SetChannels(programs.Where(channel => channel.GroupTitle == group));
            ChannelPicker.RefreshChannels();
        }));
        _guideSubscription = source.ProgramGuideInfo.Subscribe(signal => Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed) _ = Presentation.NotifyGuideAvailableAsync();
        }));
        _ = Presentation.OpenAsync(selectedProgram);
    }

    private void RenderPresentation(PlayerPresentationSnapshot state)
    {
        if (_disposed) return;
        if (_renderedChannelVersion != state.ChannelVersion)
        {
            _renderedChannelVersion = state.ChannelVersion;
            if (state.Channel != null) ChannelPicker.SyncCurrentChannel(state.Channel);
            TVPlayerViewModel.Instance.TopPanelVisible(false, state.ChannelTitle);
        }
        RaisePropertyChanged(nameof(TopPanelTitle));
        RaisePropertyChanged(nameof(IsBuffering));
        RaisePropertyChanged(nameof(FallbackStreamUrls));
        SetPlaybackStatus(state.Status switch
        {
            PlayerStatus.NoStream => TV_Player.Localization.I18n.Get("Ta7ab307b018b"),
            PlayerStatus.ResolutionFailed => TV_Player.Localization.I18n.Get("Tac7e57a9520a"),
            PlayerStatus.PlaybackFailed => TV_Player.Localization.I18n.Get("Tbe8efa4990f8"),
            PlayerStatus.EngineUnavailable => TV_Player.Localization.I18n.Get("T3088b87433c6"),
            PlayerStatus.Ended => TV_Player.Localization.I18n.Get("T9e78d065d014"),
            _ => string.Empty
        });
        // Source changes start native playback. Loading/progress updates must not replay it.
        var url = state.Stream?.Url ?? string.Empty;
        if (_renderedStreamUrl != url)
        {
            _renderedStreamUrl = url;
            RaisePropertyChanged(nameof(StreamUrl));
        }
    }

    // Native adapter-specific diagnostics remain local; common failures come from Presentation.Status.
    public void SetPlaybackStatus(string status)
    {
        PlaybackStatus = status;
        HasPlaybackStatus = !string.IsNullOrWhiteSpace(status);
    }

    public void SwitchChannel(int direction) => Presentation.SwitchChannel(direction);

    private void OpenStreamExternally()
    {
        if (string.IsNullOrWhiteSpace(StreamUrl)) return;
        Process.Start(new ProcessStartInfo { FileName = StreamUrl.Split('|', 2)[0], UseShellExecute = true });
    }

    private void OnButtonBackClick() => TVPlayerViewModel.Instance.ShowProgramsListScreen(new GroupInfo
    {
        Name = Presentation.Snapshot.Channel?.GroupTitle ?? string.Empty,
        Count = 0
    });

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _programSubscription.Dispose();
        _guideSubscription.Dispose();
        Presentation.Changed -= RenderPresentation;
        ProgramBar.Dispose();
        ChannelPicker.Dispose();
        Presentation.Dispose();
    }
}
