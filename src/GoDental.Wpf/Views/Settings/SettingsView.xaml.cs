using System.Windows;
using System.Windows.Controls;
using GoDental.Wpf.ViewModels.Operations;

namespace GoDental.Wpf.Views.Settings;

public partial class SettingsView : UserControl
{
    public static readonly DependencyProperty CommercialParametersProperty = DependencyProperty.Register(nameof(CommercialParameters), typeof(CommercialParametersViewModel), typeof(SettingsView));
    public CommercialParametersViewModel? CommercialParameters
    {
        get => (CommercialParametersViewModel?)GetValue(CommercialParametersProperty);
        set => SetValue(CommercialParametersProperty, value);
    }
    public SettingsView() => InitializeComponent();
}
