using System.Windows;
using GoDental.Wpf.ViewModels;
using GoDental.Wpf.Views.Settings;

namespace GoDental.Wpf.Navigation;

public sealed class SettingsWindowService(System.Windows.Application application, ConnectionViewModel connection) : ISettingsWindowService
{
    public void Show()
    {
        var window = new SettingsWindow(connection);
        if (application.MainWindow is { IsVisible: true } owner) window.Owner = owner;
        window.ShowDialog();
    }
}
