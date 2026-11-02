using Avalonia.Threading;
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
    private ProgramInfo? _currentProgramInfo;
    private long _channelVersion;
    private bool _disposed;
    public ICommand ShowProgramListCommand { get; }
    private List<ProgramInfo> _programItems = new();
    public List<ProgramInfo> Programs { get => _programItems; set => SetProperty(ref _programItems, value); }
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
        Programs = new List<ProgramInfo>();
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
        try
        {
            if (_currentGuide?.Programs == null || _currentGuide.Programs.Count == 0)
            {
                IsProgramInfoVisible = false;
                Programs = new List<ProgramInfo>();
                return;
            }

            _currentProgramInfo = _currentGuide.Programs
                .FirstOrDefault(item => item.StartTime <= DateTime.Now && item.EndTime >= DateTime.Now);

            if (_currentProgramInfo == null)
            {
                IsProgramInfoVisible = false;
                Programs = _currentGuide.Programs.Take(7).ToList();
                return;
            }

            var currentIndex = _currentGuide.Programs.FindIndex(x => x.Title == _currentProgramInfo.Title);
            Programs = _currentGuide.Programs.Skip(Math.Max(currentIndex, 0)).Take(7).ToList();

            IsProgramInfoVisible = true;
            ProgramGuideText = _currentProgramInfo.Title;
            StartProgram = _currentProgramInfo.StartTime.ToShortTimeString();
            EndProgram = _currentProgramInfo.EndTime.ToShortTimeString();

            var totalMinutes = (_currentProgramInfo.EndTime - _currentProgramInfo.StartTime).TotalMinutes;
            DurationValue = totalMinutes <= 0
                ? 0
                : (int)((DateTime.Now - _currentProgramInfo.StartTime).TotalMinutes / totalMinutes * 100);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update screen info: {ex.Message}");
        }
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
