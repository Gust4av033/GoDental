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
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.Views.Inventory;
using GoDental.Wpf.Views.Auth;
using GoDental.Wpf.Views.Settings;
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
        if (app.TryFindResource("BoolToVisibility") is not BooleanToVisibilityConverter)
            throw new InvalidOperationException("El convertidor de visibilidad debe estar disponible desde Controls.xaml.");
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
            vm.IsDark = false;
            await vm.SaveThemePreferenceCommand.ExecuteAsync(null);
            var themeOnly = await store.LoadAsync();
            Check(themeOnly.Theme == "Light" && themeOnly.Server == profile.Server && themeOnly.Database == profile.Database
                && vm.ThemePreferenceStatus.Contains("guardado"),
                "El interruptor conserva el tema sin guardar cambios pendientes de conexión");

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
            var inventory = new InventoryViewModel(new InventoryService(new VisualInventoryRepository(),session),session,NullLogger<InventoryViewModel>.Instance, new FakeDialogService());
            var categories = new CategoriesViewModel(new CategoryService(new VisualCategoryRepository(),session),session,NullLogger<CategoriesViewModel>.Instance);
            var productRepository = new FakeProductRepository();
            var productService = new ProductService(productRepository, session);
            var dialogHarness = new FakeDialogService();
            var editorFactory = new ProductEditorFactory(productService, session, NullLogger<ProductEditorViewModel>.Instance);
            var products = new ProductsViewModel(productService, session, NullLogger<ProductsViewModel>.Instance,dialogHarness,editorFactory,inventory,categories);
            var users = new UsersViewModel(new UserManagementService(new FakeUserRepository(), session, new Pbkdf2PasswordHasher()), session, NullLogger<UsersViewModel>.Instance, dialogHarness);
            var settingsHarness = new FakeSettingsWindowService();
            var shell = new ShellViewModel(nav, vm, access, products, users, session, settingsHarness);
            shell.NavigateCommand.Execute(SetupPage.Connection);
            Check(shell.IsConnection && !shell.IsHome && shell.PageTitle == "Ajustes", "La navegación muestra ajustes y actualiza el título");
            shell.OpenSettingsCommand.Execute(null);
            Check(settingsHarness.OpenCount == 1, "El acceso puede abrir los ajustes antes de iniciar sesión");
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
                await RenderWindowAsync(new LoginWindow(shell) { Width = 920, Height = 660 }, Path.Combine(output, "acceso-claro.png"));
                vm.IsDark = true;
                nav.Navigate(SetupPage.Connection);
                await RenderAsync(shell, 1280, 720, Path.Combine(output, "conexion-oscuro.png"));
                await RenderWindowAsync(new LoginWindow(shell) { Width = 920, Height = 660 }, Path.Combine(output, "acceso-oscuro.png"));
                await RenderWindowAsync(new SettingsWindow(vm) { Width = 760, Height = 680 }, Path.Combine(output, "ajustes-ventana-oscuro.png"));
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
                var beforeCancel = productRepository.SearchCount;
                dialogHarness.Action = editor => { editor.Name = "Cancelado"; return Task.FromResult(false); };
                await products.NewProductCommand.ExecuteAsync(null);
                Check(productRepository.SaveCount == 0 && productRepository.SearchCount == beforeCancel,
                    "Cancelar descarta la edición sin guardar ni refrescar el catálogo");
                dialogHarness.Action = async editor =>
                {
                    editor.Sku = "PRUEBA-UI"; editor.Name = "Producto de prueba";
                    await editor.SaveCommand.ExecuteAsync(null);
                    return true;
                };
                await products.NewProductCommand.ExecuteAsync(null);
                Check(productRepository.SaveCount == 1 && productRepository.SearchCount == beforeCancel + 1
                    && products.StatusMessage == "Producto guardado correctamente.",
                    "Guardar cierra la edición, refresca el listado y confirma en la página");
                var invalidEditor = editorFactory.Create(null, await productRepository.GetCatalogsAsync());
                invalidEditor.Price = "incorrecto";
                await invalidEditor.SaveCommand.ExecuteAsync(null);
                Check(invalidEditor.StatusMessage.Contains("valores numéricos") && productRepository.SaveCount == 1,
                    "Un error de validación permanece dentro del editor y no guarda");
                var rejectedEditor = editorFactory.Create(null, await productRepository.GetCatalogsAsync());
                rejectedEditor.Sku = "RECHAZADO"; rejectedEditor.Name = "Producto rechazado";
                var rejectionClosed = false; rejectedEditor.Saved += (_, _) => rejectionClosed = true;
                productRepository.RejectSave = true;
                await rejectedEditor.SaveCommand.ExecuteAsync(null);
                Check(!rejectionClosed && rejectedEditor.StatusMessage == "Guardado rechazado para prueba.",
                    "Un fallo al guardar deja abierto el editor y muestra el mensaje");
                productRepository.RejectSave = false;
                var busyEditor = editorFactory.Create(null, await productRepository.GetCatalogsAsync());
                busyEditor.Sku = "OCUPADO"; busyEditor.Name = "Producto ocupado";
                var gate = new TaskCompletionSource<GoDental.Application.Common.Result<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
                productRepository.PendingSave = gate;
                var firstSave = busyEditor.SaveCommand.ExecuteAsync(null);
                var writesDuringSave = productRepository.SaveCount;
                Check(busyEditor.IsBusy && !busyEditor.CanEdit, "El editor bloquea acciones mientras guarda");
                busyEditor.SaveCommand.Execute(null);
                Check(productRepository.SaveCount == writesDuringSave, "Guardar dos veces no duplica la escritura");
                gate.SetResult(GoDental.Application.Common.Result<int>.Success(1));
                await firstSave; productRepository.PendingSave = null;
                Check(!busyEditor.IsBusy, "El editor libera el estado ocupado al terminar");
                Check(editorFactory.Create(null, await productRepository.GetCatalogsAsync()).Sku == "",
                    "Cada apertura usa un editor con estado limpio");
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    themes.Apply(theme);
                    await RenderDialogAsync(editorFactory.Create(null, await productRepository.GetCatalogsAsync()),
                        Path.Combine(output, $"producto-dialogo-{theme}.png"));
                    await categories.ReloadAsync();
                    using (var categoryModel = new CategoryDialogModel(categories))
                        await RenderDialogAsync(categoryModel, new CategoryDialogView { DataContext = categories }, Path.Combine(output, $"categorias-dialogo-{theme}.png"));
                    await users.ReloadCommand.ExecuteAsync(null);
                    var createView = new UserCreateDialogView { DataContext = users };
                    using (var createModel = new UserDialogModel(users, true, createView.ReadPasswords, createView.ClearPasswords, createView.HasPasswords))
                        await RenderDialogAsync(createModel, createView, Path.Combine(output, $"crear-usuario-dialogo-{theme}.png"));
                    users.SelectedUser = users.Users.First();
                    var manageView = new UserManageDialogView { DataContext = users };
                    using (var manageModel = new UserDialogModel(users, false, manageView.ReadPasswords, manageView.ClearPasswords, manageView.HasPasswords))
                        await RenderDialogAsync(manageModel, manageView, Path.Combine(output, $"administrar-usuario-dialogo-{theme}.png"));
                }
                categories.Selected = categories.Items.First();
                categories.Name = "General actualizada";
                using (var categoryModel = new CategoryDialogModel(categories))
                {
                    var categoryClosed = false; categoryModel.Saved += (_, _) => categoryClosed = true;
                    Check(categoryModel.HasUnsavedChanges, "El diálogo de categorías detecta cambios sin guardar");
                    await categories.SaveCommand.ExecuteAsync(null);
                    Check(!categoryClosed && categories.StatusMessage.Contains("Prueba visual sin escritura"),
                        "Un error de categoría permanece en el diálogo");
                }
                users.UserName = "visual_nuevo"; users.DisplayName = "Cuenta de prueba";
                using (var createModel = new UserDialogModel(users, true,
                    () => ("ClaveTemporalSegura2026", "distinta"), () => { }, () => true))
                {
                    var closed = false; createModel.Saved += (_, _) => closed = true;
                    await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)createModel.SaveCommand).ExecuteAsync(null);
                    Check(!closed && users.StatusMessage.Contains("no coinciden"), "Contraseñas distintas mantienen abierto el diálogo de cuenta");
                }
                using (var createModel = new UserDialogModel(users, true,
                    () => ("ClaveTemporalSegura2026", "ClaveTemporalSegura2026"), () => { }, () => true))
                {
                    var closed = false; createModel.Saved += (_, _) => closed = true;
                    await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)createModel.SaveCommand).ExecuteAsync(null);
                    Check(closed && users.StatusMessage.Contains("Usuario creado"), "Crear cuenta confirma y cierra el diálogo");
                }
                users.SelectedUser = users.Users.First(); users.Reason = "Prueba visual";
                using (var manageModel = new UserDialogModel(users, false, () => ("", ""), () => { }, () => false))
                {
                    var closed = false; manageModel.Saved += (_, _) => closed = true;
                    await users.ToggleActiveCommand.ExecuteAsync(null);
                    Check(closed && users.StatusMessage.Contains("Usuario desactivado"), "Administrar cuenta confirma y cierra al cambiar estado");
                }
                var cancelEditor = editorFactory.Create(null, await productRepository.GetCatalogsAsync());
                _ = Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => cancelEditor.CancelCommand.Execute(null)), DispatcherPriority.Loaded);
                Check(!await new DialogService(System.Windows.Application.Current).ShowProductEditorAsync(cancelEditor), "Cancelar cierra el diálogo modal sin guardar");
                var saveEditor = editorFactory.Create(null, await productRepository.GetCatalogsAsync());
                saveEditor.Sku = "PRUEBA-MODAL"; saveEditor.Name = "Producto modal";
                _ = Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => saveEditor.SaveCommand.Execute(null)), DispatcherPriority.Loaded);
                Check(await new DialogService(System.Windows.Application.Current).ShowProductEditorAsync(saveEditor), "Guardar cierra el diálogo modal con resultado correcto");
                var dirtyEditor = editorFactory.Create(null, await productRepository.GetCatalogsAsync());
                dirtyEditor.Sku = "CAMBIO";
                Check(dirtyEditor.HasUnsavedChanges, "El editor detecta cambios sin guardar");
                var dirtyWindow = new DialogWindow(dirtyEditor, new ProductEditorView { DataContext = dirtyEditor });
                dirtyWindow.Show();
                void AnswerConfirmation(bool discard)
                {
                    var confirmation = System.Windows.Application.Current.Windows.OfType<DialogWindow>()
                        .Single(w => w.Owner == dirtyWindow && w.IsVisible);
                    var answer = (IDialogModel)confirmation.DataContext;
                    if (discard) answer.SaveCommand.Execute(null);
                    else answer.CancelCommand.Execute(null);
                }
                _ = Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => AnswerConfirmation(false)), DispatcherPriority.Loaded);
                dirtyWindow.Close();
                Check(dirtyWindow.IsVisible, "Volver conserva el diálogo y los cambios sin guardar");
                _ = Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => AnswerConfirmation(true)), DispatcherPriority.Loaded);
                dirtyWindow.Close();
                Check(!dirtyWindow.IsVisible, "Descartar confirma el cierre con cambios sin guardar");
                themes.Apply("Light");
                filterView.DataContext = null;
                nav.Navigate(SetupPage.Products);
                await RenderAsync(shell, 1366, 1000, Path.Combine(output, "productos.png"));
                Check(products.ShowEmptyState && !products.ShowCatalogTable,
                    "El catálogo vacío muestra un estado visible en lugar de una tabla en blanco");
                await inventory.OpenAsync(new(){Id=1,Name="Producto para prueba visual",Sku="VISUAL",TracksLot=true,TracksExpiry=true});
                Check(inventory.CanWrite && inventory.Stocks.Count==1,"Carga existencias y habilita operaciones con sesión autorizada");
                await RenderAsync(shell, 1366, 768, Path.Combine(output, "productos-detalle.png"));
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    themes.Apply(theme);
                    using var movementModel = new InventoryMovementDialogModel(inventory);
                    await RenderDialogAsync(movementModel, new InventoryMovementDialogView { DataContext = inventory }, Path.Combine(output, $"movimiento-dialogo-{theme}.png"));
                }
                themes.Apply("Light");
                inventory.Quantity = "5";
                await inventory.OpenMovementDialogCommand.ExecuteAsync(null);
                Check(inventory.Quantity == "", "Cada apertura del diálogo de movimiento limpia el formulario");
                using (var movementModel = new InventoryMovementDialogModel(inventory))
                {
                    var closed = false; movementModel.Saved += (_, _) => closed = true;
                    inventory.SelectedLot = inventory.Lots.First(); inventory.Quantity = "-1"; inventory.Reason = "Prueba visual"; inventory.Confirmed = true;
                    await inventory.SaveCommand.ExecuteAsync(null);
                    Check(!closed && inventory.StatusMessage.Contains("Prueba visual sin escritura"),
                        "Un movimiento rechazado permanece en el diálogo con su error");
                }
                inventory.Quantity="1"; inventory.Reason="Prueba visual"; await inventory.SaveCommand.ExecuteAsync(null);
                Check(inventory.StatusMessage.Contains("confirma"),"Exige confirmación antes de escribir");
                foreach(var theme in new[]{"Light","Dark"})
                {
                    themes.Apply(theme);
                    for(var tab=0;tab<3;tab++) await RenderInventoryAsync(inventory,tab,theme,output);
                }
                inventory.CloseCommand.Execute(null);
                await RenderAsync(shell, 1366, 768, Path.Combine(output, "productos-regreso.png"));
                await users.ReloadCommand.ExecuteAsync(null); nav.Navigate(SetupPage.Users);
                await RenderAsync(shell, 1366, 1000, Path.Combine(output, "usuarios.png"));
                session.Begin(new(2,"vendedor_visual","Vendedor de prueba visual",1,["Vendedor"],[ProductService.ReadPermission],false));
                await products.ReloadCommand.ExecuteAsync(null); nav.Navigate(SetupPage.Products);
                Check(products.CanView && !products.CanManage && !shell.CanOpenUsers, "El vendedor sólo ve acciones autorizadas");
                await RenderAsync(shell, 1366, 768, Path.Combine(output, "productos-vendedor.png"));
                await access.SignOutCommand.ExecuteAsync(null);
                Check(products.Products.Count == 0 && !shell.CanOpenProducts, "Cerrar sesión limpia datos y oculta el catálogo");
                var loginWindow = new LoginWindow(shell) { ShowActivated = false };
                var workspaceWindow = new MainWindow(shell) { ShowActivated = false };
                var coordinator = new WindowCoordinator(System.Windows.Application.Current, loginWindow, workspaceWindow, session, access);
                coordinator.Start();
                Check(loginWindow.IsVisible && !workspaceWindow.IsVisible && ReferenceEquals(System.Windows.Application.Current.MainWindow, loginWindow),
                    "Sin sesión se muestra la ventana de acceso");
                Window? settingsOwner = null;
                _ = Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    var settingsWindow = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().Single(w => w.IsVisible);
                    settingsOwner = settingsWindow.Owner;
                    settingsWindow.Close();
                }), DispatcherPriority.Loaded);
                new SettingsWindowService(System.Windows.Application.Current, vm).Show();
                Check(ReferenceEquals(settingsOwner, loginWindow), "Ajustes se abre desde acceso como ventana modal con propietario");
                session.Begin(new(2, "vendedor_visual", "Vendedor de prueba visual", 1, ["Vendedor"], [ProductService.ReadPermission], false));
                Check(workspaceWindow.IsVisible && !loginWindow.IsVisible && ReferenceEquals(System.Windows.Application.Current.MainWindow, workspaceWindow),
                    "Al iniciar sesión se muestra el espacio de trabajo");
                session.Clear();
                Check(loginWindow.IsVisible && !workspaceWindow.IsVisible && ReferenceEquals(System.Windows.Application.Current.MainWindow, loginWindow),
                    "Cerrar sesión vuelve a la ventana de acceso");
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Check(bindingErrors.Errors.Count == 0, "Ventana, temas y navegación sin errores de enlaces WPF: " + string.Join(";", bindingErrors.Errors));
                Check(((SolidColorBrush)appResource("TextBrush")).Color != ((SolidColorBrush)appResource("BackgroundBrush")).Color, "El tema mantiene recursos distintos para texto y fondo");
                coordinator.Dispose(); loginWindow.Close(); workspaceWindow.Close();
                Console.WriteLine("Vistas renderizadas: " + output);
            }
            finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingErrors); }
            Console.WriteLine($"{passed} comprobaciones correctas.");
        }
        finally { Directory.Delete(temporaryFolder, recursive: true); }
    }

    private static Task RenderDialogAsync(ProductEditorViewModel editor, string path) =>
        RenderDialogAsync(editor, new ProductEditorView { DataContext = editor }, path);

    private static async Task RenderDialogAsync(IDialogModel model, UIElement body, string path)
    {
        var window = new DialogWindow(model, body) { ShowActivated = false };
        try
        {
            window.Show();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            await using var stream = File.Create(path); encoder.Save(stream);
        }
        finally { window.Close(); }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Func<ProductEditorViewModel, Task<bool>> Action { get; set; } = _ => Task.FromResult(false);
        public Task<bool> ShowProductEditorAsync(ProductEditorViewModel editor) => Action(editor);
        public Task<bool> ShowCategoryManagerAsync(CategoriesViewModel manager) => Task.FromResult(false);
        public Task<bool> ShowCreateUserAsync(UsersViewModel manager) => Task.FromResult(false);
        public Task<bool> ShowManageUserAsync(UsersViewModel manager) => Task.FromResult(false);
        public Task<bool> ShowInventoryMovementAsync(InventoryViewModel manager) => Task.FromResult(false);
    }

    private sealed class FakeSettingsWindowService : ISettingsWindowService
    {
        public int OpenCount { get; private set; }
        public void Show() => OpenCount++;
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
        if (theme == "Dark" && tab == 2)
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

    private static Task RenderAsync(ShellViewModel shell, double width, double height, string path) =>
        RenderWindowAsync(new MainWindow(shell) { Width = width, Height = height, ShowActivated = false }, path);

    private static async Task RenderWindowAsync(Window window, string path)
    {
        var width = window.Width;
        var height = window.Height;
        try
        {
            // Renderizar fuera del compositor evita capturas parciales al cerrar/cambiar ventanas.
            var root = (FrameworkElement)window.Content;
            window.Content = null;
            root.DataContext = window.DataContext;
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

