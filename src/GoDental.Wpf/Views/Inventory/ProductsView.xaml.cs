using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Views.Inventory;

public partial class ProductsView : UserControl
{
    public ProductsView() => InitializeComponent();

    private void OnProductDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ProductsViewModel model || !model.CanEdit || e.OriginalSource is not DependencyObject source) return;
        while (source is not null && source is not DataGridRow)
            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        if (source is DataGridRow) model.EditSelectedCommand.Execute(null);
    }
}
