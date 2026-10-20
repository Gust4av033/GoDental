using System.Windows.Controls;
using System.Windows.Threading;

namespace GoDental.Wpf.Views.Inventory;

public partial class ProductEditorView : UserControl
{
    public ProductEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => ProductNameInput.Focus()));
    }
}
