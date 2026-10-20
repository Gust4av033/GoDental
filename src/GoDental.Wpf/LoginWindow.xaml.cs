using System.Windows;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf;

public partial class LoginWindow : Window
{
    public LoginWindow(ShellViewModel shell)
    {
        InitializeComponent();
        DataContext = shell;
        Closing += (_, e) =>
        {
            if (!shell.Access.IsSubmitting) return;
            e.Cancel = true;
            MessageBox.Show(this, "Espera a que termine la operación de acceso antes de cerrar GoDental.", "GoDental", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        MinWidth = Math.Min(MinWidth, SystemParameters.WorkArea.Width * 0.95);
        MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height * 0.94);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width * 0.95);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height * 0.94);
    }
}
