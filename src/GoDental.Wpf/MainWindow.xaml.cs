using System.Windows;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closing += (_, e) =>
        {
            if (viewModel.CanCloseWorkspace) return;
            e.Cancel = true;
            MessageBox.Show(this, "Hay una operación en curso o pendiente de confirmar. Vuelve a su página y consulta el estado antes de cerrar GoDental.", "GoDental", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        MinWidth = Math.Min(MinWidth, SystemParameters.WorkArea.Width);
        MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
    }
}
