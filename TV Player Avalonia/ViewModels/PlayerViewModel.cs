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
    private List<M3UInfo> _programs = new();
    private IReadOnlyList<string> _fallbackStreamUrls = Array.Empty<string>();
    private int _currentProgramIndex;
    private CancellationTokenSource? _resolveCts;
    private bool _disposed;

    public ICommand BackCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand FullscreenCommand { get; }
    public ICommand ToggleChannelPickerCommand { get; }
    public ICommand CloseAppCommand { get; }
    public ICommand OpenStreamCommand { get; }

    public ObservableCollection<ChannelPickerItem> ChannelPickerItems { get; } = new();

    private bool _channelPickerVisible;
    public bool ChannelPickerVisible
    {
        get => _channelPickerVisible;
        set => SetProperty(ref _channelPickerVisible, value);
    }

    private ChannelPickerItem? _selectedChannelPickerItem;
    private bool _updatingChannelPickerSelection;
    public ChannelPickerItem? SelectedChannelPickerItem
    {
        get => _selectedChannelPickerItem;
        set
        {
            if (SetProperty(ref _selectedChannelPickerItem, value) && value != null && !_updatingChannelPickerSelection)
                SelectChannel(value);
        }
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
                    _programs = groupPrograms;
                    _currentProgramIndex = _programs.FindIndex(IsCurrentChannel);
                    if (_currentProgramIndex < 0)
                        _currentProgramIndex = 0;
                    RefreshChannelPickerItems();
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
        foreach (var item in ChannelPickerItems)
            item.IsCurrent = IsCurrentChannel(item.Channel);
        _updatingChannelPickerSelection = true;
        SelectedChannelPickerItem = ChannelPickerItems.FirstOrDefault(item => item.IsCurrent);
        _updatingChannelPickerSelection = false;
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

    private bool IsCurrentChannel(M3UInfo program)
    {
        if (!string.IsNullOrWhiteSpace(_currentProgram.ProviderId) && !string.IsNullOrWhiteSpace(_currentProgram.ChannelId))
            return program.ProviderId == _currentProgram.ProviderId && program.ChannelId == _currentProgram.ChannelId;

        return ReferenceEquals(program, _currentProgram) ||
               (program.Name == _currentProgram.Name && program.Url == _currentProgram.Url);
    }

    private void RefreshChannelPickerItems()
    {
        _updatingChannelPickerSelection = true;
        ChannelPickerItems.Clear();
        foreach (var program in _programs)
            ChannelPickerItems.Add(new ChannelPickerItem(program, IsCurrentChannel(program)));
        SelectedChannelPickerItem = ChannelPickerItems.FirstOrDefault(item => item.IsCurrent);
        _updatingChannelPickerSelection = false;
    }

    private void SelectChannel(ChannelPickerItem? item)
    {
        if (item == null)
            return;

        var index = _programs.FindIndex(program =>
            !string.IsNullOrWhiteSpace(item.Channel.ProviderId)
                ? program.ProviderId == item.Channel.ProviderId && program.ChannelId == item.Channel.ChannelId
                : ReferenceEquals(program, item.Channel) || (program.Name == item.Channel.Name && program.Url == item.Channel.Url));
        if (index < 0)
            return;

        if (index == _currentProgramIndex)
            return;

        _currentProgramIndex = index;
        _currentProgram = _programs[index];
        UpdateUi();
    }

    public void SwitchChannel(int direction)
    {
        if (_programs.Count < 2 || direction == 0)
            return;

        var nextIndex = (_currentProgramIndex + Math.Sign(direction) + _programs.Count) % _programs.Count;
        _currentProgramIndex = nextIndex;
        _currentProgram = _programs[nextIndex];
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
        ProgramBar.Dispose();
    }
}

public sealed class ChannelPickerItem : TV_Player.ObservableViewModelBase
{
    private bool _isCurrent;

    public M3UInfo Channel { get; }
    public string Name => Channel.Name;

    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    public ChannelPickerItem(M3UInfo channel, bool isCurrent)
    {
        Channel = channel;
        _isCurrent = isCurrent;
    }
}
