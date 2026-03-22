using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TV_Player.AvaloniaApp.ViewModels;

namespace TV_Player.AvaloniaApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = TVPlayerViewModel.Instance.MainWindowViewModel
            };
            // Initialize screens after the Lazy singleton is fully constructed
            TVPlayerViewModel.Instance.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
