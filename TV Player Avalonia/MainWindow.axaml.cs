using Avalonia.Controls;
using Avalonia.Input;
using TV_Player.AvaloniaApp.ViewModels;

namespace TV_Player.AvaloniaApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

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
