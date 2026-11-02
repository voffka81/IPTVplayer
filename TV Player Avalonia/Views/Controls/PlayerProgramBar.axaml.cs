using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace TV_Player.AvaloniaApp.Views.Controls;

public partial class PlayerProgramBar : UserControl
{
    public PlayerProgramBar() => InitializeComponent();

    public void SetAvailableWidth(double centerColumnWidth)
    {
        GuideProgress.Width = Math.Clamp(centerColumnWidth * 0.42, 40, 360);
        GuidePanel.Width = double.NaN;
    }

    public void ScrollGuide(int direction)
    {
        var viewer = ProgramGuideScroll;
        var maxOffset = Math.Max(0, viewer.Extent.Width - viewer.Viewport.Width);
        viewer.Offset = new Vector(Math.Clamp(viewer.Offset.X + direction * 228, 0, maxOffset), viewer.Offset.Y);
    }

    private void OnProgramGuidePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;
        var maxOffset = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        if (maxOffset <= 0) return;
        var delta = Math.Abs(e.Delta.X) > 0 ? e.Delta.X : e.Delta.Y;
        if (delta == 0) return;
        scrollViewer.Offset = new Vector(Math.Clamp(scrollViewer.Offset.X - delta * 110, 0, maxOffset), scrollViewer.Offset.Y);
        e.Handled = true;
    }
}
