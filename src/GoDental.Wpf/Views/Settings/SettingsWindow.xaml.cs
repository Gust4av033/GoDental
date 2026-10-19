using System.Windows;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Views.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow(ConnectionViewModel connection)
    {
        InitializeComponent();
        DataContext = connection;
        MinWidth = Math.Min(MinWidth, SystemParameters.WorkArea.Width * 0.92);
        MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height * 0.92);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width * 0.92);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height * 0.92);
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
