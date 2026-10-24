using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GoDental.Wpf.Views.Inventory;

public partial class InventoryView : UserControl
{
    public static readonly DependencyProperty EditProductCommandProperty = DependencyProperty.Register(
        nameof(EditProductCommand), typeof(ICommand), typeof(InventoryView));
    public static readonly DependencyProperty CanEditProductProperty = DependencyProperty.Register(
        nameof(CanEditProduct), typeof(bool), typeof(InventoryView), new PropertyMetadata(false));
    public ICommand? EditProductCommand { get => (ICommand?)GetValue(EditProductCommandProperty); set => SetValue(EditProductCommandProperty, value); }
    public bool CanEditProduct { get => (bool)GetValue(CanEditProductProperty); set => SetValue(CanEditProductProperty, value); }
    public InventoryView() => InitializeComponent();
}
