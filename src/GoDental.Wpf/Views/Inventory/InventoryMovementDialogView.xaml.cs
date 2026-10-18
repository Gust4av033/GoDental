using System.Windows.Controls;
using System.Windows.Threading;

namespace GoDental.Wpf.Views.Inventory;

public partial class InventoryMovementDialogView : UserControl
{
    public InventoryMovementDialogView()
    {
        InitializeComponent();
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => MovementTypeInput.Focus()));
    }
}
