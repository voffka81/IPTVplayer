using Microsoft.Maui.Dispatching;
using TV_Player.Presentation;

namespace TV_Player.Maui;

/// <summary>Drives the shared UI policy on the MAUI dispatcher; owns no views.</summary>
internal sealed class MauiPlayerUiController : IDisposable
{
    private readonly PlayerUiController _policy = new();
    private readonly IDispatcherTimer _timer;
    private bool _disposed;
    private readonly TimeProvider _clock = TimeProvider.System;
    public PlayerUiState State => _policy.State;

    public MauiPlayerUiController(IDispatcher dispatcher, Action<PlayerUiState> render)
    {
        _policy.StateChanged += render;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += OnTick;
    }

    public void Resume()
    {
        if (_disposed) return;
        _policy.Resume(_clock.GetUtcNow());
        _timer.Start();
    }
    public void Suspend() { _timer.Stop(); _policy.Suspend(); }
    public void Show() => _policy.Show(_clock.GetUtcNow());
    public void ToggleControls() => _policy.ToggleControls(_clock.GetUtcNow());
    public void TogglePicker() => _policy.TogglePicker(_clock.GetUtcNow());
    public void ToggleGuide() => _policy.ToggleGuide(_clock.GetUtcNow());
    public bool CloseOverlay() => _policy.CloseOverlay(_clock.GetUtcNow());
    public void CloseGuide() => _policy.CloseGuide(_clock.GetUtcNow());
    private void OnTick(object? sender, EventArgs e) => _policy.Tick(_clock.GetUtcNow());

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _policy.Dispose();
    }
}
