using TV_Player.Presentation;
using Xunit;

namespace TV_Player.Tests;

public class PlayerUiControllerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-03T12:00:00Z");

    [Fact]
    public void ControlsHideAtFourSecondsAndInteractionResetsDeadline()
    {
        using var ui = new PlayerUiController();
        ui.Resume(Now);
        ui.Tick(Now.AddSeconds(3.999));
        Assert.True(ui.State.ControlsVisible);
        ui.Show(Now.AddSeconds(3));
        ui.Tick(Now.AddSeconds(4));
        Assert.True(ui.State.ControlsVisible);
        ui.Tick(Now.AddSeconds(7));
        Assert.Equal(default, ui.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OverlayPinsControlsAndClosingStartsFreshDeadline(bool picker)
    {
        using var ui = new PlayerUiController();
        ui.Resume(Now);
        if (picker) ui.TogglePicker(Now); else ui.ToggleGuide(Now);
        ui.Tick(Now.AddHours(1));
        Assert.True(ui.State.ControlsVisible);
        Assert.True(ui.CloseOverlay(Now.AddHours(1)));
        ui.Tick(Now.AddHours(1).AddSeconds(3));
        Assert.True(ui.State.ControlsVisible);
        ui.Tick(Now.AddHours(1).AddSeconds(4));
        Assert.False(ui.State.ControlsVisible);
    }

    [Fact]
    public void OpeningOneOverlayClosesOtherAndTouchHidesEverything()
    {
        using var ui = new PlayerUiController();
        ui.Resume(Now);
        ui.TogglePicker(Now);
        Assert.Equal(new(true, true, false), ui.State);
        ui.ToggleGuide(Now);
        Assert.Equal(new(true, false, true), ui.State);
        ui.ToggleControls(Now);
        Assert.Equal(default, ui.State);
        ui.ToggleControls(Now);
        Assert.Equal(new(true, false, false), ui.State);
        Assert.False(ui.CloseOverlay(Now));
    }

    [Fact]
    public void SuspendedControllerRejectsLateTicksAndRequestsThenResumesWithNewDeadline()
    {
        using var ui = new PlayerUiController();
        ui.Resume(Now);
        var updates = 0;
        ui.StateChanged += _ => updates++;
        ui.Suspend();
        ui.Tick(Now.AddHours(1));
        ui.TogglePicker(Now);
        ui.ToggleControls(Now);
        Assert.Equal(0, updates);
        ui.Resume(Now.AddHours(1));
        ui.Tick(Now.AddHours(1).AddSeconds(3));
        Assert.True(ui.State.ControlsVisible);
        ui.Tick(Now.AddHours(1).AddSeconds(4));
        Assert.False(ui.State.ControlsVisible);
    }

    [Fact]
    public void DisposalRejectsAllCallbacksIncludingQueuedTimerTicks()
    {
        var ui = new PlayerUiController();
        ui.Resume(Now);
        var updates = 0;
        ui.StateChanged += _ => updates++;
        ui.Dispose();
        ui.Resume(Now);
        ui.TogglePicker(Now);
        ui.ToggleGuide(Now);
        ui.ToggleControls(Now);
        ui.Show(Now);
        ui.CloseGuide(Now);
        ui.Tick(Now.AddDays(1));
        Assert.False(ui.CloseOverlay(Now));
        Assert.Equal(0, updates);
        ui.Dispose();
    }
}
