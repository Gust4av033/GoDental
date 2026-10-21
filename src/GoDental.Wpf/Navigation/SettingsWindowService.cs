using System.Windows;
using GoDental.Wpf.ViewModels;
using GoDental.Wpf.ViewModels.Operations;
using GoDental.Wpf.Views.Settings;

namespace GoDental.Wpf.Navigation;

public sealed class SettingsWindowService(System.Windows.Application application, ConnectionViewModel connection, CommercialParametersViewModel? parameters = null) : ISettingsWindowService
{
    public void Show()
    {
        var window = new SettingsWindow(connection, parameters);
        if (application.MainWindow is { IsVisible: true } owner) window.Owner = owner;
        if (parameters?.CanRead == true) parameters.ReloadCommand.Execute(null);
        window.ShowDialog();
    }
}
