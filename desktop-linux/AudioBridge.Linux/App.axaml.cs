using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AudioBridge.Desktop.Capture;
using AudioBridge.Desktop.Services;
using AudioBridge.Desktop.ViewModels;
using AudioBridge.Desktop.Views;

namespace AudioBridge.Desktop;

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
            var settingsService = new SettingsService();
            var capture = new LinuxPipeWireCapture();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(capture, settingsService),
            };
            desktop.MainWindow.Closed += (_, _) => capture.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}