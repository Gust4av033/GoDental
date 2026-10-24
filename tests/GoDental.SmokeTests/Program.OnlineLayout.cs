using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GoDental.Wpf;
using GoDental.Wpf.Navigation;
using GoDental.Wpf.Themes;
using GoDental.Wpf.ViewModels;
using GoDental.Wpf.Views.Operations;
using GoDental.Wpf.Views.Settings;

internal static partial class Program
{
    private static async Task VerifyOnlineWorkspaceAsync(ShellViewModel shell, INavigationService navigation, IThemeService themes, string output)
    {
        var online = shell.Online!;
        foreach (var theme in new[] { "Light", "Dark" })
        foreach (var size in new[] { new Size(1366, 768), new Size(1093, 614) })
        {
            themes.Apply(theme);
            navigation.Navigate(SetupPage.Online);
            online.SectionIndex = 0;
            var window = new MainWindow(shell) { Width = size.Width, Height = size.Height, ShowActivated = false };
            try
            {
                window.Show();
                await ScrollLayoutAsync(window);
                var root = (FrameworkElement)window.Content;
                var view = FindChildren<OnlineView>(root).Single();
                var tabs = FindChildren<TabControl>(view).Single();
                var content = (ScrollViewer)tabs.Template.FindName("SectionScroll", tabs);
                var menu = (ScrollViewer)tabs.Template.FindName("SectionNavigationScroll", tabs);
                var outer = (ScrollViewer)window.FindName("PageScroll");
                var loading = (ProgressBar)window.FindName("WorkspaceLoading");
                Check(outer.ScrollableHeight < 1 && content.ScrollableHeight > 0,
                    "Online ocupa el viewport y desplaza únicamente el contenido de la sección larga");
                var menuTop = menu.TranslatePoint(new Point(), root).Y;
                online.IsBusy = true;
                await ScrollLayoutAsync(window);
                var loadingTop = loading.TranslatePoint(new Point(), root).Y;
                content.ScrollToEnd();
                await ScrollLayoutAsync(window);
                Check(content.VerticalOffset > 0 && Math.Abs(menu.TranslatePoint(new Point(), root).Y - menuTop) < 1,
                    "Zonas de prospección y las otras opciones permanecen en su sitio al bajar");

                // Estado visual controlado; no se lanzan llamadas de red ni operaciones comerciales.
                online.IsBusy = true;
                await ScrollLayoutAsync(window);
                Check(shell.IsPageBusy && loading.IsVisible && Math.Abs(loading.TranslatePoint(new Point(), root).Y - loadingTop) < 1,
                    "La carga activa permanece visible fuera del scroll del módulo");
                Check(FindChildren<ProgressBar>(view).All(bar => bar.Visibility == Visibility.Collapsed),
                    "Online no duplica la barra de carga cuando el host ya muestra una fija");
                var segment = (Border)loading.Template.FindName("LoadingSegment", loading);
                Check(((SolidColorBrush)segment.Background).Color == ((SolidColorBrush)loading.FindResource("PrimaryBrush")).Color,
                    "El indicador animado usa el azul actual del tema, sin el verde nativo");
                SavePreview(root, Path.Combine(output, $"online-menu-carga-{theme}-{(int)size.Width}.png"));
                online.IsBusy = false;
                await ScrollLayoutAsync(window);
                Check(!loading.IsVisible && !shell.IsPageBusy, "La carga desaparece al terminar la operación");

                // La carga de otra página no se presenta como actividad de Online.
                shell.Cash.IsBusy = true;
                await ScrollLayoutAsync(window);
                Check(!loading.IsVisible, "Una consulta de otro módulo no enciende la barra de Online");
                shell.Cash.IsBusy = false;
                online.SectionIndex = tabs.Items.Count - 1;
                await ScrollLayoutAsync(window);
                for (var index = 0; index < tabs.Items.Count; index++)
                {
                    online.SectionIndex = index;
                    await ScrollLayoutAsync(window);
                    var selected = (TabItem)tabs.ItemContainerGenerator.ContainerFromIndex(index);
                    var top = selected.TranslatePoint(new Point(), menu).Y;
                    Check(content.VerticalOffset < 1 && top >= -1 && top + selected.ActualHeight <= menu.ViewportHeight + 1,
                        $"Sección {index + 1} accesible y abierta desde el inicio en Online");
                    content.ScrollToEnd();
                    await ScrollLayoutAsync(window);
                }
                window.Width = 860;
                window.Height = 480;
                await ScrollLayoutAsync(window);
                Check(menu.ScrollableHeight > 0 && menu.ViewportHeight > 0 && outer.ScrollableHeight < 1,
                    "En una ventana pequeña, el menú interno tiene su propio scroll y no desplaza toda la página");
                online.SectionIndex = 0;
                await ScrollLayoutAsync(window);
                var first = (TabItem)tabs.ItemContainerGenerator.ContainerFromIndex(0);
                first.Focus();
                first.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
                await ScrollLayoutAsync(window);
                Check(tabs.SelectedIndex == 1, "La navegación de teclado permite pasar a Prospectos desde el menú interno");
                online.IsBusy = true;
                navigation.Navigate(SetupPage.Cash);
                await ScrollLayoutAsync(window);
                Check(!shell.IsPageBusy && !loading.IsVisible, "Cambiar de página actualiza la carga aunque la anterior siga consultando");
                shell.Cash.IsBusy = true;
                outer.ScrollToEnd();
                await ScrollLayoutAsync(window);
                Check(shell.IsPageBusy && loading.IsVisible, "Caja también mantiene la carga en la cabecera al desplazar su página");
                shell.Cash.IsBusy = false;
                navigation.Navigate(SetupPage.Online);
                await ScrollLayoutAsync(window);
                Check(loading.IsVisible, "Volver al módulo recupera su indicador de carga real");

                // La misma plantilla mantiene valores determinados, usados por otros flujos.
                loading.IsIndeterminate = false;
                loading.Minimum = 0;
                loading.Maximum = 100;
                loading.Value = 50;
                await ScrollLayoutAsync(window);
                var track = (Border)loading.Template.FindName("PART_Track", loading);
                var indicator = (Border)loading.Template.FindName("PART_Indicator", loading);
                Check(Math.Abs(indicator.ActualWidth - track.ActualWidth / 2) < 1,
                    "El estilo azul también representa correctamente el progreso determinado");
            }
            finally { online.IsBusy = false; shell.Cash.IsBusy = false; window.Close(); }
        }
        online.SectionIndex = 1;
        foreach (var theme in new[] { "Light", "Dark" })
        {
            themes.Apply(theme);
            var settings = new SettingsWindow(shell.Connection, shell.CommercialParameters) { Width = 760, Height = 480, ShowActivated = false };
            try
            {
                settings.Show();
                shell.Connection.IsBusy = true;
                await ScrollLayoutAsync(settings);
                var root = (FrameworkElement)settings.Content;
                var loading = (ProgressBar)settings.FindName("SettingsLoading");
                var body = FindChildren<ScrollViewer>(root).Single(scroll => scroll.Content is SettingsView);
                var top = loading.TranslatePoint(new Point(), root).Y;
                body.ScrollToEnd();
                await ScrollLayoutAsync(settings);
                Check(body.VerticalOffset > 0 && loading.IsVisible && Math.Abs(loading.TranslatePoint(new Point(), root).Y - top) < 1,
                    "Ajustes independiente mantiene la carga visible al bajar en una ventana pequeña");
                shell.Connection.IsBusy = false;
                shell.CommercialParameters!.IsBusy = true;
                await ScrollLayoutAsync(settings);
                Check(loading.IsVisible, "Ajustes refleja también la carga de parámetros comerciales");
                shell.CommercialParameters.IsBusy = false;
                await ScrollLayoutAsync(settings);
                Check(!loading.IsVisible, "El indicador fijo de Ajustes vuelve a reposo al terminar ambas consultas");
            }
            finally { shell.Connection.IsBusy = false; shell.CommercialParameters!.IsBusy = false; settings.Close(); }
        }
        themes.Apply("Light");
        navigation.Navigate(SetupPage.Home);
    }
}
