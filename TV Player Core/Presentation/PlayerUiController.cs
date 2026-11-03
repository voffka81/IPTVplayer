namespace TV_Player.Presentation;

public readonly record struct PlayerUiState(bool ControlsVisible, bool ChannelPickerVisible, bool ProgramGuideVisible);

/// <summary>Single-threaded UI policy. The platform supplies time and drives Tick on its UI thread.</summary>
public sealed class PlayerUiController : IDisposable
{
    public static readonly TimeSpan AutoHideDelay = TimeSpan.FromSeconds(4);
    public PlayerUiState State { get; private set; }
    public event Action<PlayerUiState>? StateChanged;
    private DateTimeOffset? _hideAt;
    private bool _active;
    private bool _disposed;

    public void Resume(DateTimeOffset now)
    {
        if (_disposed) return;
        _active = true;
        Show(now);
    }

    public void Suspend()
    {
        _active = false;
        _hideAt = null;
    }

    public void Show(DateTimeOffset now)
    {
        if (!CanUpdate) return;
        Set(State with { ControlsVisible = true }, now);
    }

    public void ToggleControls(DateTimeOffset now)
    {
        if (!CanUpdate) return;
        Set(State.ControlsVisible ? default : new(true, false, false), now);
    }

    public void TogglePicker(DateTimeOffset now)
    {
        if (!CanUpdate) return;
        Set(new(true, !State.ChannelPickerVisible, false), now);
    }

    public void ToggleGuide(DateTimeOffset now)
    {
        if (!CanUpdate) return;
        Set(new(true, false, !State.ProgramGuideVisible), now);
    }

    public bool CloseOverlay(DateTimeOffset now)
    {
        if (!CanUpdate || (!State.ChannelPickerVisible && !State.ProgramGuideVisible)) return false;
        Set(new(true, false, false), now);
        return true;
    }

    public void CloseGuide(DateTimeOffset now)
    {
        if (CanUpdate && State.ProgramGuideVisible) Set(new(true, false, false), now);
    }

    public void Tick(DateTimeOffset now)
    {
        if (CanUpdate && _hideAt is { } deadline && now >= deadline)
            Set(default, now);
    }

    public void Dispose()
    {
        Suspend();
        _disposed = true;
        StateChanged = null;
    }

    private bool CanUpdate => _active && !_disposed;

    private void Set(PlayerUiState state, DateTimeOffset now)
    {
        _hideAt = state.ControlsVisible && !state.ChannelPickerVisible && !state.ProgramGuideVisible
            ? now + AutoHideDelay : null;
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(state);
    }
}
