using Avalonia.Controls;
using TV_Player.AvaloniaApp.ViewModels;
using Xunit;

namespace TV_Player.Tests;

public class NavigationTests
{
    [Fact]
    public void BackCommand_InvokesConfiguredNavigation()
    {
        var vm = new MainWindowViewModel();
        var calls = 0;
        vm.ButtonBackAction = () => calls++;

        vm.BackCommand.Execute(null);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void FullscreenCommand_TogglesWindowState()
    {
        var vm = new MainWindowViewModel();
        Assert.Equal(WindowState.Normal, vm.CurrentWindowState);

        vm.FullscreenCommand.Execute(null);
        Assert.Equal(WindowState.FullScreen, vm.CurrentWindowState);

        vm.FullscreenCommand.Execute(null);
        Assert.Equal(WindowState.Normal, vm.CurrentWindowState);
    }

    [Fact]
    public void TopBar_PropertiesNotifyOnChange()
    {
        var vm = new MainWindowViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        vm.IsTopPanelVisible = true;
        vm.TopPanelTitle = "Playlists";

        Assert.Contains(nameof(vm.IsTopPanelVisible), changed);
        Assert.Contains(nameof(vm.TopPanelTitle), changed);
    }
}
