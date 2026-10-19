using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Views.Auth;

public partial class UsersView : UserControl
{
    public UsersView() => InitializeComponent();

    private void OnUserDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not UsersViewModel model || !model.CanManageSelected || e.OriginalSource is not DependencyObject source) return;
        while (source is not null && source is not DataGridRow)
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        if (source is DataGridRow) model.OpenManageCommand.Execute(null);
    }
}
