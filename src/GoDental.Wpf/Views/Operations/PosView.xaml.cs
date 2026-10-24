using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace GoDental.Wpf.Views.Operations;
public partial class PosView : UserControl
{
    public static readonly DependencyProperty OpenReceiptCommandProperty = DependencyProperty.Register(nameof(OpenReceiptCommand), typeof(ICommand), typeof(PosView));
    public ICommand? OpenReceiptCommand { get => (ICommand?)GetValue(OpenReceiptCommandProperty); set => SetValue(OpenReceiptCommandProperty, value); }
    public PosView() => InitializeComponent();
}
