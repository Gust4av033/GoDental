using System.Windows.Controls;
using GoDental.Wpf.Dialogs;
namespace GoDental.Wpf.Views.Operations;
public partial class MvpFormView : UserControl
{
    public MvpFormView() { InitializeComponent(); }
    public void Connect(MvpFormModel form) { DataContext = form; form.ReadSecret = () => Secret.Password; form.ClearSecret = () => Secret.Clear(); }
}
