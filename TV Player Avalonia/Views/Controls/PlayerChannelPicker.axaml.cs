using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using TV_Player.AvaloniaApp.ViewModels;

namespace TV_Player.AvaloniaApp.Views.Controls;

public partial class PlayerChannelPicker : UserControl
{
    public PlayerChannelPicker() => InitializeComponent();

    public void ScrollIntoView(ChannelPickerItem item) => ChannelPickerList.ScrollIntoView(item);

    private void OnChannelPickerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var scrollViewer = ChannelPickerList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer == null)
            return;

        var maxOffset = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        if (maxOffset <= 0)
            return;

        var wheelDelta = Math.Abs(e.Delta.X) > 0 ? e.Delta.X : e.Delta.Y;
        if (wheelDelta == 0)
            return;

        var nextOffset = Math.Clamp(scrollViewer.Offset.X - wheelDelta * 110, 0, maxOffset);
        scrollViewer.Offset = new Vector(nextOffset, scrollViewer.Offset.Y);
        e.Handled = true;
    }


}
