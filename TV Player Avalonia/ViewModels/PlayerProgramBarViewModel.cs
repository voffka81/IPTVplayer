using Avalonia.Threading;
using TV_Player.Presentation;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace TV_Player.AvaloniaApp.ViewModels;

public sealed class PlayerProgramBarViewModel : TV_Player.ObservableViewModelBase, IDisposable
{
    private readonly PlayerPresentationModel _presentation;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
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


    public PlayerProgramBarViewModel(PlayerPresentationModel presentation)
    {
        _presentation = presentation;
        ShowProgramListCommand = new RelayCommand(() => ProgramGuideVisible = !ProgramGuideVisible);
        _presentation.Changed += Render;
        _timer.Tick += OnTick;
        _timer.Start();
        Render(presentation.Snapshot);
    }

    private void OnTick(object? sender, EventArgs e) => _presentation.RefreshGuide();

    private void Render(PlayerPresentationSnapshot state)
    {
        if (_disposed) return;
        if (_channelVersion != state.ChannelVersion)
        {
            _channelVersion = state.ChannelVersion;
            ProgramGuideVisible = false;
        }
        Programs = state.Guide.Window;
        IsBuffering = state.Playback.IsLoading;
        var program = state.Program;
        IsProgramInfoVisible = program != null;
        ProgramGuideText = program?.Title ?? string.Empty;
        StartProgram = program?.ShortTimeLabels.Start ?? string.Empty;
        EndProgram = program?.ShortTimeLabels.End ?? string.Empty;
        DurationValue = program == null ? 0 : (int)(program.Progress * 100);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _presentation.Changed -= Render;
    }
}
