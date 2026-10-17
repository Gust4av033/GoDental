using System.Windows.Controls;
using System.Windows;
namespace GoDental.Wpf.Views.Inventory;
public partial class ProductsView : UserControl
{
    public ProductsView() => InitializeComponent();
    private void FocusEditor(object sender, RoutedEventArgs e) { EditorPanel.BringIntoView(); SkuInput.Focus(); }
}
