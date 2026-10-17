using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace GoDental.Wpf.Views.Inventory;
public partial class InventoryView : UserControl
{
    public InventoryView() => InitializeComponent();
    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => BringIntoView()));
    }
}
