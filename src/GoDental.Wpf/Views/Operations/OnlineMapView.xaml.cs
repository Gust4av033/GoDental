using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using GoDental.Application.Online;
using GoDental.Wpf.ViewModels.Operations;
namespace GoDental.Wpf.Views.Operations;
public partial class OnlineMapView:System.Windows.Controls.UserControl
{
    public OnlineMapView()=>InitializeComponent();
    private void RegionClicked(object sender,MouseButtonEventArgs e){Select(sender);e.Handled=true;}
    private void RegionKeyDown(object sender,KeyEventArgs e){if(e.Key is Key.Enter or Key.Space){Select(sender);e.Handled=true;}}
    private void Select(object sender){if(DataContext is OnlineViewModel vm&&sender is FrameworkElement{DataContext:OnlineMapRegion region})vm.SelectMapDepartmentCommand.Execute(region.Name);}
    private void LicenseRequested(object sender,RequestNavigateEventArgs e)
    {
        e.Handled=true;if(e.Uri.AbsoluteUri!=OnlineMapCatalog.LicenseUrl&&e.Uri.AbsoluteUri!=OnlineMapCatalog.GeometryLicenseUrl&&e.Uri.AbsoluteUri!=OnlineMapCatalog.OpenStreetMapLicenseUrl)return;
        try{Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true});}
        catch(System.ComponentModel.Win32Exception){if(DataContext is OnlineViewModel vm)vm.MapMessage="Licencia disponible en ThirdPartyNotices/WhosOnFirst-LICENSE.md del proyecto.";}
    }
}
