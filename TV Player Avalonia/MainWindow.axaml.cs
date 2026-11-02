using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TV_Player.AvaloniaApp.ViewModels;

namespace TV_Player.AvaloniaApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (viewModel.CurrentViewModel is PlayerViewModel playerViewModel && e.Key is Key.Left or Key.Right)
        {
            playerViewModel.SwitchChannel(e.Key == Key.Right ? 1 : -1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.OnCloseAppButtonClick();
        }
        else if (e.Key == Key.Back)
        {
            viewModel.TriggerBack();
        }
    }
}
