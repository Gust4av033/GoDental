using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GoDental.Wpf;
using GoDental.Wpf.Navigation;
using GoDental.Wpf.Themes;
using GoDental.Wpf.ViewModels;

internal static partial class Program
{
    private static async Task VerifyWorkspaceScrollingAsync(ShellViewModel shell, INavigationService navigation, IThemeService themes)
    {
        foreach (var theme in new[] { "Light", "Dark" })
        foreach (var size in new[] { new Size(1366, 768), new Size(1093, 614) })
        {
            themes.Apply(theme);
            var window = new MainWindow(shell) { Width = size.Width, Height = size.Height, ShowActivated = false };
            try
            {
                window.Show();
                await ScrollLayoutAsync(window);
                var page = (ScrollViewer)window.FindName("PageScroll");
                var sidebar = (ScrollViewer)window.FindName("SidebarScroll");
                var brand = (FrameworkElement)window.FindName("SidebarBrand");
                var host = (Grid)window.FindName("PageHost");
                var links = (StackPanel)window.FindName("SidebarLinks");
                var root = (FrameworkElement)window.Content;
                // Extensión controlada para probar el espacio de trabajo sin depender de datos comerciales.
                var list = new ListBox { Height = 160, Width = 280, ItemsSource = Enumerable.Range(1, 80).Select(i => $"Fila {i}") };
                ScrollViewer.SetCanContentScroll(list, false);
                var fixture = new StackPanel { Height = 1800, VerticalAlignment = VerticalAlignment.Top };
                fixture.Children.Add(list);
                var table = new DataGrid { Height = 150, Width = 280, IsReadOnly = true, CanUserAddRows = false,
                    ItemsSource = Enumerable.Range(1, 80).Select(i => new { Nombre = $"Producto {i}" }) };
                fixture.Children.Add(table);
                var notes = new TextBox { Height = 60, Width = 280, AcceptsReturn = true,
                    Text = string.Join(Environment.NewLine, Enumerable.Range(1, 30).Select(i => $"Nota {i}")),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                fixture.Children.Add(notes);
                host.Children.Add(fixture);
                navigation.Navigate(SetupPage.Connection);
                await ScrollLayoutAsync(window);
                Check(page.ScrollableHeight > 400 && sidebar.ScrollableHeight > 0, "Hay recorrido independiente en página y menú");
                var brandTop = brand.TranslatePoint(new Point(), root).Y;
                var brandHeight = brand.ActualHeight;
                sidebar.ScrollToEnd();
                await ScrollLayoutAsync(window);
                var sidebarOffset = sidebar.VerticalOffset;
                page.ScrollToVerticalOffset(280);
                await ScrollLayoutAsync(window);
                Check(Math.Abs(sidebar.VerticalOffset - sidebarOffset) < 1
                    && Math.Abs(brand.TranslatePoint(new Point(), root).Y - brandTop) < 1
                    && Math.Abs(brand.ActualHeight - brandHeight) < 1, "Bajar la página mantiene el menú y la marca en su sitio");
                var pageOffset = page.VerticalOffset;
                sidebar.ScrollToHome();
                await ScrollLayoutAsync(window);
                Check(Math.Abs(page.VerticalOffset - pageOffset) < 1, "Mover el menú no desplaza el contenido");
                navigation.Navigate(SetupPage.Online);
                await ScrollLayoutAsync(window);
                Check(page.VerticalOffset < 1, "Cambiar de módulo vuelve al encabezado aunque la pantalla nueva también sea larga");
                CheckSelectedLinkVisible(links, sidebar);
                page.ScrollToEnd();
                await ScrollLayoutAsync(window);
                navigation.Navigate(SetupPage.Home);
                await ScrollLayoutAsync(window);
                Check(page.VerticalOffset < 1, "Volver a Inicio elimina el desplazamiento de la sección anterior");
                CheckSelectedLinkVisible(links, sidebar);

                var inner = FindChildren<ScrollViewer>(list).Single();
                SendWheel(inner, -120);
                await ScrollLayoutAsync(window);
                Check(inner.VerticalOffset > 0 && page.VerticalOffset < 1, "Una lista con recorrido recibe primero la rueda");
                inner.ScrollToEnd();
                await ScrollLayoutAsync(window);
                sidebarOffset = sidebar.VerticalOffset;
                SendWheel(inner, -120);
                await ScrollLayoutAsync(window);
                Check(page.VerticalOffset > 0 && Math.Abs(sidebar.VerticalOffset - sidebarOffset) < 1,
                    "Al terminar la lista, la rueda continúa en la página y no en el menú");
                page.ScrollToVerticalOffset(200);
                inner.ScrollToHome();
                await ScrollLayoutAsync(window);
                SendWheel(inner, 120);
                await ScrollLayoutAsync(window);
                Check(page.VerticalOffset < 200, "La rueda también continúa hacia arriba desde el comienzo de la lista");

                var tableScroll = FindChildren<ScrollViewer>(table).First();
                tableScroll.ScrollToEnd();
                page.ScrollToHome();
                await ScrollLayoutAsync(window);
                SendWheel(tableScroll, -120);
                await ScrollLayoutAsync(window);
                Check(page.VerticalOffset > 0, "Las tablas con scroll lógico también continúan en la página al llegar al final");
                var notesScroll = FindChildren<ScrollViewer>(notes).Single();
                notesScroll.ScrollToEnd();
                page.ScrollToHome();
                await ScrollLayoutAsync(window);
                SendWheel(notesScroll, -120);
                await ScrollLayoutAsync(window);
                Check(page.VerticalOffset < 1, "La rueda dentro de un campo de notas conserva su comportamiento de edición");

                navigation.Navigate(SetupPage.Connection);
                await ScrollLayoutAsync(window);
                window.Height = 450;
                await ScrollLayoutAsync(window);
                CheckSelectedLinkVisible(links, sidebar);
                Check(sidebar.ViewportHeight > 0 && sidebar.TranslatePoint(new Point(0, sidebar.ActualHeight), root).Y <= root.ActualHeight,
                    "El menú permanece acotado al reducir la altura de la ventana");
                shell.ToggleSidebarCommand.Execute(null);
                await ScrollLayoutAsync(window);
                CheckSelectedLinkVisible(links, sidebar);
                shell.ToggleSidebarCommand.Execute(null);
                await ScrollLayoutAsync(window);
                CheckSelectedLinkVisible(links, sidebar);
                host.Children.Remove(fixture);
            }
            finally { window.Close(); }
        }
        themes.Apply("Light");
        navigation.Navigate(SetupPage.Home);
    }

    private static void CheckSelectedLinkVisible(StackPanel links, ScrollViewer viewer)
    {
        var selected = links.Children.OfType<RadioButton>().Single(button => button.IsChecked == true && button.IsVisible);
        var top = selected.TranslatePoint(new Point(), viewer).Y;
        Check(top >= -1 && top + selected.ActualHeight <= viewer.ViewportHeight + 1,
            "La opción activa permanece visible al navegar, redimensionar y contraer el lateral");
    }

    private static async Task ScrollLayoutAsync(Window window)
    {
        await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
        await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
    }

    private static void SendWheel(UIElement source, int delta)
    {
        var e = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
        source.RaiseEvent(e);
        if (e.Handled) return;
        e.RoutedEvent = Mouse.MouseWheelEvent;
        source.RaiseEvent(e);
    }
}
