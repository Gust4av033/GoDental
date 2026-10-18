using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
namespace GoDental.Wpf.Views.Inventory;
public partial class ProductsView : UserControl
{
    public ProductsView() => InitializeComponent();
    private void FocusEditor(object sender, RoutedEventArgs e)
    {
        // Click precede al comando: esperar a que el editor sea visible y esté dispuesto.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!EditorPanel.IsVisible) return;
            SkuInput.BringIntoView();
            SkuInput.Focus();
        }));
    }
}
