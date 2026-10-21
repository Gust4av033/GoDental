using System.Windows;
using GoDental.Wpf.ViewModels;
using GoDental.Wpf.ViewModels.Operations;

namespace GoDental.Wpf.Views.Settings;

public partial class SettingsWindow : Window
{
    public CommercialParametersViewModel? Parameters { get; }
    public SettingsWindow(ConnectionViewModel connection, CommercialParametersViewModel? parameters = null)
    {
        Parameters = parameters;
        InitializeComponent();
        SettingsContent.CommercialParameters = parameters;
        DataContext = connection;
        MinWidth = Math.Min(MinWidth, SystemParameters.WorkArea.Width * 0.92);
        MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height * 0.92);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width * 0.92);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height * 0.92);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (Parameters is { } p && (p.IsBusy || p.IsEditing || !p.Operation.CanClose)) e.Cancel = true;
        base.OnClosing(e);
    }
    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
