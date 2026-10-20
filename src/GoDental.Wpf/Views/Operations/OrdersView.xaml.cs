using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
namespace GoDental.Wpf.Views.Operations;
public partial class OrdersView : UserControl
{
    public static readonly DependencyProperty PreviewReceiptCommandProperty = DependencyProperty.Register(nameof(PreviewReceiptCommand), typeof(ICommand), typeof(OrdersView));
    public ICommand? PreviewReceiptCommand { get => (ICommand?)GetValue(PreviewReceiptCommandProperty); set => SetValue(PreviewReceiptCommandProperty, value); }
    public OrdersView() { InitializeComponent(); }
}
