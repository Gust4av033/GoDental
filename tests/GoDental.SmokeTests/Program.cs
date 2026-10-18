using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GoDental.Application.DTOs;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Application.Validators;
using GoDental.Application.Services;
using GoDental.Infrastructure.Data;
using GoDental.Infrastructure.Security;
using GoDental.Wpf.Navigation;
using GoDental.Wpf.Themes;
using GoDental.Wpf.ViewModels;
using GoDental.Wpf;
using Microsoft.Extensions.Logging.Abstractions;

internal static class Program
{
    private static int passed;

    [STAThread]
    private static void Main(string[] args)
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var name in new[] { "Colors", "LightTheme", "Typography", "Spacing", "Controls" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/GoDental;component/Themes/{name}.xaml", UriKind.Relative)
            });
        app.Resources.Add("BoolToVisibility", new BooleanToVisibilityConverter());
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
        {
            try { await RunAsync(args.FirstOrDefault()); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        Dispatcher.Run();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("OK " + message);
        passed++;
    }

    private static async Task RunAsync(string? artifactDirectory)
    {
        var temporaryFolder = Path.Combine(Path.GetTempPath(), "GoDental.SmokeTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryFolder);
        try
        {
            var profilePath = Path.Combine(temporaryFolder, "workstation.json");
            var store = new LocalWorkstationSettingsStore(profilePath);
            Check(await store.LoadAsync() == new WorkstationSettings(), "Primer arranque sin archivo de configuración");
            Check(WorkstationSettingsValidator.Validate(new()) is not null, "Rechaza perfil incompleto");
            Check(WorkstationSettingsValidator.Validate(new("(localdb)\\MSSQLLocalDB", "GoDental")) is not null, "Rechaza LocalDB como servidor central");
            var profile = new WorkstationSettings("servidor\\instancia", "GoDentalPruebas", "Dark");
            await store.SaveAsync(profile);
            Check(await store.LoadAsync() == profile, "Guarda y recupera perfil y tema");
            Check(!Directory.EnumerateFiles(temporaryFolder, "*.tmp").Any(), "No deja archivos temporales tras guardar");

            var fake = new ControlledHealthCheck();
            var themes = new ThemeService();
            var vm = new ConnectionViewModel(store, fake, themes, NullLogger<ConnectionViewModel>.Instance);
            await vm.InitializeAsync();
            Check(vm.IsDark && themes.CurrentTheme == "Dark", "Restaura el tema al iniciar");
            var probe = vm.CheckConnectionCommand.ExecuteAsync(null);
            Check(vm.IsBusy && !vm.SaveCommand.CanExecute(null) && !vm.CheckConnectionCommand.CanExecute(null), "Bloquea operaciones solapadas durante la comprobación");
            fake.Complete(new(true, "Respuesta controlada para prueba"));
            await probe;
            Check(vm.ConnectionStatus == "Conexión comprobada" && !vm.IsBusy, "Refleja una comprobación exitosa sin crear sesión");
            vm.Server = "otro-servidor";
            Check(vm.ConnectionStatus == "Sin comprobar", "Editar el perfil invalida el resultado previo");

            fake.Reset();
            probe = vm.CheckConnectionCommand.ExecuteAsync(null);
            vm.CheckConnectionCancelCommand.Execute(null);
            await probe;
            Check(vm.ConnectionStatus == "Sin comprobar" && !vm.IsBusy, "Cancela la comprobación y recupera la interfaz");

            fake.Reset();
            probe = vm.CheckConnectionCommand.ExecuteAsync(null);
            fake.Complete(new(false, "Servidor no disponible"));
            await probe;
            Check(vm.ConnectionStatus == "Sin conexión" && vm.StatusMessage == "Servidor no disponible", "Muestra un fallo de conexión sin simular éxito");

            vm.Server = "";
            await vm.CheckConnectionCommand.ExecuteAsync(null);
            Check(fake.CallCount == 3 && !vm.IsBusy, "No consulta SQL con un perfil inválido");
            var actualHealthCheck = new SqlConnectionHealthCheck(new SqlConnectionFactory(Microsoft.Extensions.Options.Options.Create(new DatabaseOptions())), NullLogger<SqlConnectionHealthCheck>.Instance);
            Check(!(await actualHealthCheck.CheckAsync(new())).IsSuccess, "El servicio real valida antes de abrir conexiones");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var observedCancellation = false;
                try { await actualHealthCheck.CheckAsync(new("tcp:127.0.0.1,1", "GoDentalTests"), cancelled.Token); }
                catch (OperationCanceledException) { observedCancellation = true; }
                Check(observedCancellation, "Propaga cancelación al acceso SQL real");
            }
            await File.WriteAllTextAsync(profilePath, "{malformado");
            await vm.InitializeAsync();
            Check(vm.StatusMessage.Contains("No se pudo leer"), "Permite recuperar un perfil local dañado");
            await File.WriteAllTextAsync(profilePath, "{\"Server\":null,\"Database\":null,\"Theme\":null}");
            Check(await store.LoadAsync() == new WorkstationSettings(), "Normaliza campos nulos de configuración");

            var nav = new NavigationService();
            var session = new UserSession();
            var access = new AccessViewModel(new FakeIdentityGateway(), session, NullLogger<AccessViewModel>.Instance);
            var inventory = new InventoryViewModel(new InventoryService(new VisualInventoryRepository(),session),session,NullLogger<InventoryViewModel>.Instance);
            var categories = new CategoriesViewModel(new CategoryService(new VisualCategoryRepository(),session),session,NullLogger<CategoriesViewModel>.Instance);
            var productRepository = new FakeProductRepository();
            var products = new ProductsViewModel(new ProductService(productRepository, session), session, NullLogger<ProductsViewModel>.Instance,inventory,categories);
            var users = new UsersViewModel(new UserManagementService(new FakeUserRepository(), session, new Pbkdf2PasswordHasher()), session, NullLogger<UsersViewModel>.Instance);
            var shell = new ShellViewModel(nav, vm, access, products, users, session);
            shell.NavigateCommand.Execute(SetupPage.Connection);
            Check(shell.IsConnection && !shell.IsHome && shell.PageTitle.Contains("Conexión"), "La navegación actualiza página y título");
            shell.ToggleSidebarCommand.Execute(null);
            Check(shell.SidebarWidth == 72 && !shell.IsSidebarExpanded, "El menú compacto conserva navegación");

            var bindingErrors = new BindingErrorListener();
            PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            var output = artifactDirectory ?? Path.Combine(AppContext.BaseDirectory, "previews");
            Directory.CreateDirectory(output);
            try
            {
                vm.Server = "";
                vm.Database = "";
                vm.IsDark = false;
                shell.IsSidebarCompact = false;
                nav.Navigate(SetupPage.Home);
                await RenderAsync(shell, 1280, 720, Path.Combine(output, "inicio-claro.png"));
                vm.IsDark = true;
                nav.Navigate(SetupPage.Connection);
                await RenderAsync(shell, 1280, 720, Path.Combine(output, "conexion-oscuro.png"));
                shell.IsSidebarCompact = true;
                await RenderAsync(shell, 910, 510, Path.Combine(output, "conexion-compacta.png"));
                shell.IsSidebarCompact = false;
                nav.Navigate(SetupPage.Access); await access.PrepareCommand.ExecuteAsync(null);
                await RenderAsync(shell, 1280, 900, Path.Combine(output, "primer-administrador.png"));
                session.Begin(new(1, "prueba_visual", "Usuario de prueba visual", 1, ["Administrador"], [ProductService.ReadPermission, ProductService.ManagePermission,"INVENTARIO.AJUSTAR","INVENTARIO.COSTOS_VER","USUARIOS.VER","USUARIOS.GESTIONAR","SEGURIDAD.ROLES_PERMISOS"], false));
                await products.ReloadCommand.ExecuteAsync(null);
                var filterView = new GoDental.Wpf.Views.Inventory.ProductsView { DataContext = products };
                filterView.Measure(new Size(1080, 768));
                filterView.Arrange(new Rect(0, 0, 1080, 768));
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                products.FilterCategory = products.Categories.First();
                products.FilterBrand = products.Brands.First(); // Sin marca es un filtro válido con Id=0.
                await products.SearchCommand.ExecuteAsync(null);
                Check(productRepository.LastSearch is { CategoryId: 1, BrandId: 0 }
                    && products.FilterCategory?.Id == 1 && products.FilterBrand?.Id == 0,
                    "Buscar conserva filtros de categoría y Sin marca al renovar catálogos enlazados");
                await products.ClearFiltersCommand.ExecuteAsync(null);
                Check(productRepository.LastSearch is { CategoryId: null, BrandId: null }, "Limpiar filtros consulta todas las categorías y marcas");
                filterView.DataContext = null;
                nav.Navigate(SetupPage.Products);
                await RenderAsync(shell, 1366, 1000, Path.Combine(output, "productos.png"));
                await inventory.OpenAsync(new(){Id=1,Name="Producto para prueba visual",Sku="VISUAL",TracksLot=true,TracksExpiry=true});
                Check(inventory.CanWrite && inventory.Stocks.Count==1,"Carga existencias y habilita operaciones con sesión autorizada");
                inventory.Quantity="1"; inventory.Reason="Prueba visual"; await inventory.SaveCommand.ExecuteAsync(null);
                Check(inventory.StatusMessage.Contains("confirma"),"Exige confirmación antes de escribir");
                foreach(var theme in new[]{"Light","Dark"})
                {
                    themes.Apply(theme);
                    for(var tab=0;tab<4;tab++) await RenderInventoryAsync(inventory,tab,theme,output);
                }
                inventory.CloseCommand.Execute(null);
                await users.ReloadCommand.ExecuteAsync(null); nav.Navigate(SetupPage.Users);
                await RenderAsync(shell, 1366, 1000, Path.Combine(output, "usuarios.png"));
                session.Begin(new(2,"vendedor_visual","Vendedor de prueba visual",1,["Vendedor"],[ProductService.ReadPermission],false));
                await products.ReloadCommand.ExecuteAsync(null); nav.Navigate(SetupPage.Products);
                Check(products.CanView && !products.CanManage && !shell.CanOpenUsers, "El vendedor sólo ve acciones autorizadas");
                await RenderAsync(shell, 1366, 768, Path.Combine(output, "productos-vendedor.png"));
                await access.SignOutCommand.ExecuteAsync(null);
                Check(products.Products.Count == 0 && !shell.CanOpenProducts, "Cerrar sesión limpia datos y oculta el catálogo");
                Check(bindingErrors.Errors.Count == 0, "Ventana, temas y navegación sin errores de enlaces WPF: " + string.Join(";", bindingErrors.Errors));
                Check(((SolidColorBrush)appResource("TextBrush")).Color != ((SolidColorBrush)appResource("BackgroundBrush")).Color, "El tema mantiene recursos distintos para texto y fondo");
                Console.WriteLine("Vistas renderizadas: " + output);
            }
            finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingErrors); }
            Console.WriteLine($"{passed} comprobaciones correctas.");
        }
        finally { Directory.Delete(temporaryFolder, recursive: true); }
    }

    private static async Task RenderInventoryAsync(InventoryViewModel model,int tab,string theme,string output)
    {
        var view=new GoDental.Wpf.Views.Inventory.InventoryView {DataContext=model};
        view.Foreground = (Brush)appResource("TextBrush");
        view.FontFamily = new FontFamily("Segoe UI");
        view.FontSize = 14;
        view.Measure(new Size(1080,660));view.Arrange(new Rect(0,0,1080,660));view.UpdateLayout();
        TabControl? FindTabs(DependencyObject node)
        {
            if(node is TabControl found) return found;
            for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) if(FindTabs(VisualTreeHelper.GetChild(node,i)) is {} child) return child;
            return null;
        }
        FindTabs(view)!.SelectedIndex=tab;
        await Dispatcher.CurrentDispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        view.Measure(new Size(1080,760));view.Arrange(new Rect(0,0,1080,760));view.UpdateLayout();
        if (theme == "Dark" && tab == 3)
        {
            var picker = Descendant<DatePicker>(view)!;
            var popup = (System.Windows.Controls.Primitives.Popup)picker.Template.FindName("PART_Popup", picker);
            Check(popup.Child is Calendar, "El selector de fecha conserva el calendario nativo con el tema oscuro");
            ((Calendar)popup.Child).SelectedDate = new DateTime(2026, 10, 17);
            Check(model.From == new DateTime(2026, 10, 17), "Seleccionar una fecha de calendario actualiza el filtro de Kardex");
            model.From = null;
        }
        var bitmap=new RenderTargetBitmap(1080,760,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var stream=File.Create(Path.Combine(output,$"inventario-{theme}-{tab}.png"));encoder.Save(stream);
    }

    private static T? Descendant<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T found) return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (Descendant<T>(VisualTreeHelper.GetChild(node, i)) is { } child) return child;
        return null;
    }

    private static object appResource(string key) => System.Windows.Application.Current.FindResource(key);

    private static async Task RenderAsync(ShellViewModel shell, double width, double height, string path)
    {
        var window = new MainWindow(shell) { Width = width, Height = height, ShowActivated = false };
        try
        {
            // Renderizar fuera del compositor evita capturas parciales al cerrar/cambiar ventanas.
            var root = (FrameworkElement)window.Content;
            window.Content = null;
            root.DataContext = shell;
            TextElement.SetFontFamily(root, window.FontFamily);
            TextElement.SetFontSize(root, window.FontSize);
            TextElement.SetForeground(root, window.Foreground);
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            await using var stream = File.Create(path);
            encoder.Save(stream);
        }
        finally { window.Close(); }
    }

    private sealed class ControlledHealthCheck : IConnectionHealthCheck
    {
        private TaskCompletionSource<ConnectionCheckResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }
        public async Task<ConnectionCheckResult> CheckAsync(WorkstationSettings settings, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return await completion.Task.WaitAsync(cancellationToken);
        }
        public void Complete(ConnectionCheckResult result) => completion.SetResult(result);
        public void Reset() => completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if (message is not null) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}

