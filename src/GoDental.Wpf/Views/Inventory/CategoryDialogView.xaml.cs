using System.Windows.Controls;
using System.Windows.Threading;

namespace GoDental.Wpf.Views.Inventory;

public partial class CategoryDialogView : UserControl
{
    public CategoryDialogView()
    {
        InitializeComponent();
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => CategoryInput.Focus()));
    }
}
