using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows.Input;

namespace TV_Player.AvaloniaApp.ViewModels;

public class PlayerViewModel : TV_Player.ObservableViewModelBase, IDisposable
{
    private readonly IDisposable _programSubscription;
    private IDisposable? _programGuideDisposable;
    private IDisposable? _timer;
    private M3UInfo _currentProgram;
    private List<M3UInfo> _programs = new();
    private ProgramGuide? _currentGuide;
    private ProgramInfo? _currentProgramInfo;
    private int _currentProgramIndex;

    public ICommand BackCommand { get; }
    public ICommand FullscreenCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand ShowProgramListCommand { get; }
    public ICommand CloseAppCommand { get; }
    public ICommand OpenStreamCommand { get; }

    private string _topPanelTitle = string.Empty;
    public string TopPanelTitle
    {
        get => _topPanelTitle;
        set => SetProperty(ref _topPanelTitle, value);
    }

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
        set => SetProperty(ref _isProgramInfoVisible, value);
    }

    private bool _programGuideVisible;
    public bool ProgramGuideVisible
    {
        get => _programGuideVisible;
        set => SetProperty(ref _programGuideVisible, value);
    }

    private string _streamUrl = string.Empty;
    public string StreamUrl
    {
        get => _streamUrl;
        set => SetProperty(ref _streamUrl, value);
    }

    private string _playbackStatus = "Buffering stream...";
    public string PlaybackStatus
    {
        get => _playbackStatus;
        set => SetProperty(ref _playbackStatus, value);
    }

    private bool _hasPlaybackStatus = true;
    public bool HasPlaybackStatus
    {
        get => _hasPlaybackStatus;
        set => SetProperty(ref _hasPlaybackStatus, value);
    }

    private List<ProgramInfo> _programItems = new();
    public List<ProgramInfo> Programs
    {
        get => _programItems;
        set => SetProperty(ref _programItems, value);
    }

    public PlayerViewModel(M3UInfo selectedProgram)
    {
        _currentProgram = selectedProgram;
        BackCommand = new RelayCommand(OnButtonBackClick);
        NextCommand = new RelayCommand(NextProgram);
        PreviousCommand = new RelayCommand(PreviousProgram);
        FullscreenCommand = new RelayCommand(TVPlayerViewModel.Instance.FullScreenToggle);
        CloseAppCommand = new RelayCommand(TVPlayerViewModel.Instance.CloseAppCommand);
        ShowProgramListCommand = new RelayCommand(ShowProgramList);
        OpenStreamCommand = new RelayCommand(OpenStreamExternally);

        _programSubscription = TVPlayerViewModel.Instance.CurrentProgramsData!
            .AllPrograms
            .Subscribe(programs =>
            {
                _programs = programs.Where(p => p.GroupTitle == _currentProgram.GroupTitle).ToList();
                _currentProgramIndex = _programs.FindIndex(p => p.Name == _currentProgram.Name);
                if (_currentProgramIndex < 0)
                    _currentProgramIndex = 0;
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
        TVPlayerViewModel.Instance.TopPanelVisible(false, _currentProgram.Name);
        TopPanelTitle = _currentProgram.Name;
        StreamUrl = _currentProgram.Url;
        SetPlaybackStatus(string.IsNullOrWhiteSpace(StreamUrl)
            ? "No stream URL available for this channel."
            : "Buffering stream...");
        ProgramGuideVisible = false;

        _programGuideDisposable?.Dispose();
        _timer?.Dispose();

        _programGuideDisposable = TVPlayerViewModel.Instance.CurrentProgramsData!
            .ProgramGuideInfo
            .Subscribe(async _ =>
            {
                try
                {
                    _currentGuide = await TVPlayerViewModel.Instance.CurrentProgramsData.GetGuideByProgram(_currentProgram.TvgID);
                    UpdateScreenInfo();
                    _timer = Observable.Interval(TimeSpan.FromMinutes(1)).Subscribe(_ => UpdateScreenInfo());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load program guide: {ex.Message}");
                }
            });
    }

    private void PreviousProgram()
    {
        if (_programs.Count == 0)
            return;

        _currentProgramIndex = (_currentProgramIndex - 1 + _programs.Count) % _programs.Count;
        _currentProgram = _programs[_currentProgramIndex];
        UpdateUi();
    }

    private void NextProgram()
    {
        if (_programs.Count == 0)
            return;

        _currentProgramIndex = (_currentProgramIndex + 1) % _programs.Count;
        _currentProgram = _programs[_currentProgramIndex];
        UpdateUi();
    }

    private void ShowProgramList()
    {
        ProgramGuideVisible = !ProgramGuideVisible;
    }

    private void OpenStreamExternally()
    {
        if (string.IsNullOrWhiteSpace(StreamUrl))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = StreamUrl,
            UseShellExecute = true
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
        _programSubscription.Dispose();
        _programGuideDisposable?.Dispose();
        _timer?.Dispose();
    }
}
