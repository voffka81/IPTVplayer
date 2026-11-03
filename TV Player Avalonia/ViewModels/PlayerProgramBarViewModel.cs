using Avalonia.Threading;
using TV_Player.Presentation;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows.Input;

namespace TV_Player.AvaloniaApp.ViewModels;

public sealed class PlayerProgramBarViewModel : TV_Player.ObservableViewModelBase, IDisposable
{
    private IDisposable? _programGuideDisposable;
    private IDisposable? _timer;
    private ProgramGuide? _currentGuide;
    private long _channelVersion;
    private bool _disposed;
    public ICommand ShowProgramListCommand { get; }
    private IReadOnlyList<EpgProgramItem> _programItems = Array.Empty<EpgProgramItem>();
    public IReadOnlyList<EpgProgramItem> Programs { get => _programItems; set => SetProperty(ref _programItems, value); }
    private string _programGuideText = string.Empty;
    public string ProgramGuideText
    {
        get => _programGuideText;
        set => SetProperty(ref _programGuideText, value);
    }

    private string _startProgram = string.Empty;
    public string StartProgram
    {
        get => _startProgram;
        set => SetProperty(ref _startProgram, value);
    }

    private string _endProgram = string.Empty;
    public string EndProgram
    {
        get => _endProgram;
        set => SetProperty(ref _endProgram, value);
    }

    private int _durationValue;
    public int DurationValue
    {
        get => _durationValue;
        set => SetProperty(ref _durationValue, value);
    }

    private bool _isProgramInfoVisible;
    public bool IsProgramInfoVisible
    {
        get => _isProgramInfoVisible;
        set
        {
            if (SetProperty(ref _isProgramInfoVisible, value))
                NotifyProgramBarVisibilityChanged();
        }
    }

    private bool _isBuffering;
    public bool IsBuffering
    {
        get => _isBuffering;
        set { if (SetProperty(ref _isBuffering, value)) NotifyProgramBarVisibilityChanged(); }
    }

    public bool ShowProgramInfo => IsProgramInfoVisible && !IsBuffering;
    public bool ShowCurrentProgramInBar => ShowProgramInfo && !ProgramGuideVisible;
    public bool ShowProgramGuideInBar => ProgramGuideVisible && !IsBuffering;

    private bool _programGuideVisible;
    public bool ProgramGuideVisible
    {
        get => _programGuideVisible;
        set
        {
            if (SetProperty(ref _programGuideVisible, value))
                NotifyProgramBarVisibilityChanged();
        }
    }

    private void NotifyProgramBarVisibilityChanged()
    {
        RaisePropertyChanged(nameof(ShowProgramInfo));
        RaisePropertyChanged(nameof(ShowCurrentProgramInBar));
        RaisePropertyChanged(nameof(ShowProgramGuideInBar));
    }


    public PlayerProgramBarViewModel() => ShowProgramListCommand = new RelayCommand(() => ProgramGuideVisible = !ProgramGuideVisible);
    public void SetChannel(M3UInfo channel)
    {
        var version = ++_channelVersion;
        _programGuideDisposable?.Dispose();
        _timer?.Dispose();
        _timer = null;
        _currentGuide = null;
        Programs = Array.Empty<EpgProgramItem>();
        IsProgramInfoVisible = false;
        ProgramGuideVisible = false;
        _programGuideDisposable = TVPlayerViewModel.Instance.CurrentProgramsData!
            .ProgramGuideInfo.Subscribe(async _ =>
            {
                try
                {
                    var guide = await TVPlayerViewModel.Instance.CurrentProgramsData.GetGuideByProgram(channel);
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_disposed || version != _channelVersion) return;
                        _currentGuide = guide;
                        UpdateScreenInfo();
                        _timer?.Dispose();
                        _timer = Observable.Interval(TimeSpan.FromMinutes(1))
                            .Subscribe(_ => Dispatcher.UIThread.Post(() =>
                            {
                                if (!_disposed && version == _channelVersion) UpdateScreenInfo();
                            }));
                    });
                }
                catch (Exception ex) { Debug.WriteLine($"Failed to load program guide: {ex.Message}"); }
            });
    }
    private void UpdateScreenInfo()
    {
        var state = ProgramGuideProjection.Project(_currentGuide, DateTime.Now,
            origin: GuideWindowOrigin.CurrentOrFirst, formatProvider: CultureInfo.CurrentCulture);
        Programs = state.Window;
        IsProgramInfoVisible = state.Current != null;
        ProgramGuideText = state.Current?.Title ?? string.Empty;
        StartProgram = state.Current?.ShortTimeLabels.Start ?? string.Empty;
        EndProgram = state.Current?.ShortTimeLabels.End ?? string.Empty;
        DurationValue = state.Current == null ? 0 : (int)(state.Current.Progress * 100);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ++_channelVersion;
        _programGuideDisposable?.Dispose();
        _timer?.Dispose();
    }
}
