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
using GoDental.Wpf.ViewModels.Operations;
using GoDental.Wpf.Views.Operations;
using GoDental.Infrastructure.Operations;
using Microsoft.Extensions.Options;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.Views.Inventory;
using GoDental.Wpf.Views.Auth;
using GoDental.Wpf.Views.Settings;
using GoDental.Wpf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using GoDental.Application.Abstractions.Security;

internal static class Program
{
    private static int passed;
    private static string? shutdownProbe;
    private static bool shutdownVerified;

    [STAThread]
    private static void Main(string[] args)
    {
        shutdownProbe = args.FirstOrDefault(a => a is "--shutdown-login" or "--shutdown-workspace");
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var name in new[] { "Colors", "LightTheme", "Typography", "Spacing", "Controls" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/GoDental;component/Themes/{name}.xaml", UriKind.Relative)
            });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/GoDental;component/Resources/Icons.xaml", UriKind.Relative) });
        if (app.TryFindResource("BoolToVisibility") is not BooleanToVisibilityConverter)
            throw new InvalidOperationException("El convertidor de visibilidad debe estar disponible desde Controls.xaml.");
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
        {
            try { await RunAsync(args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { if (shutdownProbe is null || Environment.ExitCode != 0) Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        // La prueba de salida necesita el mismo dueño del dispatcher que App.Main.
        if (shutdownProbe is not null) app.Run();
        else Dispatcher.Run();
        if (shutdownProbe is not null)
        {
            if (!shutdownVerified) Environment.ExitCode = 1;
            else Console.WriteLine("SHUTDOWN_OK");
        }
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
            var mvpRepository = new MvpUiRepository();
            var mvpDialogs = new MvpVisualDialogs();
            var operationService = new MvpOperationService(mvpRepository, session);
            var cashService = new CashService(mvpRepository, session);
            var salesService = new SalesService(mvpRepository, session, new FakeIdentityGateway());
            var customerService = new CustomerService(new CustomerWorkflowRepository(), session);
            var cashMvp = new CashViewModel(cashService, new InventoryService(new VisualInventoryRepository(), session), mvpDialogs, session, operationService, NullLogger<CashViewModel>.Instance);
            var posMvp = new PosViewModel(salesService, cashService, customerService, mvpDialogs, session, operationService, NullLogger<PosViewModel>.Instance);
            var creditMvp = new CreditViewModel(cashService, salesService, customerService, mvpDialogs, session, operationService, NullLogger<CreditViewModel>.Instance);
            var ordersMvp = new OrdersViewModel(new OrderService(mvpRepository, session), customerService, productService, mvpDialogs, session, operationService, NullLogger<OrdersViewModel>.Instance);
            var historyMvp = new CommercialHistoryViewModel(salesService, new SaleReceiptService(mvpRepository, new ReceiptPdfRenderer(Options.Create(new ReceiptPdfOptions())), session), new VisualPdfFiles(), posMvp, nav, session, operationService, NullLogger<CommercialHistoryViewModel>.Instance);
            var shell = new ShellViewModel(nav, vm, access, products, users, session, settingsHarness,
                new InventoryCountsViewModel(new InventoryCountService(new UiCountRepository(), session), new InventoryService(new VisualInventoryRepository(), session), productService, session, dialogHarness, NullLogger<InventoryCountsViewModel>.Instance),
                new SuppliersViewModel(new SupplierService(new UiSupplierRepository(), session), productService, session, dialogHarness, NullLogger<SuppliersViewModel>.Instance),
                new CustomersViewModel(new CustomerService(new CustomerWorkflowRepository(), session), session, new CustomerTestDialogs(), NullLogger<CustomersViewModel>.Instance), cashMvp, posMvp, creditMvp, ordersMvp, historyMvp);
            if (shutdownProbe is not null)
            {
                await StartShutdownProbeAsync(shell, session);
                return;
            }
            shell.NavigateCommand.Execute(SetupPage.Connection);
            Check(shell.IsConnection && !shell.IsHome && shell.PageTitle == "Ajustes", "La navegación muestra ajustes y actualiza el título");
            shell.OpenSettingsCommand.Execute(null);
            Check(settingsHarness.OpenCount == 1, "El acceso puede abrir los ajustes antes de iniciar sesión");
            shell.ToggleSidebarCommand.Execute(null);
            Check(shell.SidebarWidth == 64 && !shell.IsSidebarExpanded, "El menú compacto conserva navegación");

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
                await RenderWindowAsync(new LoginWindow(shell) { Width = 760, Height = 540 }, Path.Combine(output, "acceso-compacto-claro.png"));
                vm.IsDark = true;
                nav.Navigate(SetupPage.Connection);
                await RenderAsync(shell, 1280, 720, Path.Combine(output, "conexion-oscuro.png"));
                await RenderWindowAsync(new LoginWindow(shell) { Width = 920, Height = 660 }, Path.Combine(output, "acceso-oscuro.png"));
                await RenderWindowAsync(new LoginWindow(shell) { Width = 760, Height = 540 }, Path.Combine(output, "acceso-compacto-oscuro.png"));
                await RenderWindowAsync(new SettingsWindow(vm) { Width = 760, Height = 680 }, Path.Combine(output, "ajustes-ventana-oscuro.png"));
                shell.IsSidebarCompact = true;
                await RenderAsync(shell, 910, 510, Path.Combine(output, "conexion-compacta.png"));
                shell.IsSidebarCompact = false;
                nav.Navigate(SetupPage.Access); await access.PrepareCommand.ExecuteAsync(null);
                await RenderAsync(shell, 1280, 900, Path.Combine(output, "primer-administrador.png"));
                session.Begin(new(1, "prueba_visual", "Usuario de prueba visual", 1, ["Administrador"], [ProductService.ReadPermission, ProductService.ManagePermission,"INVENTARIO.AJUSTAR","INVENTARIO.COSTOS_VER","USUARIOS.VER","USUARIOS.GESTIONAR","SEGURIDAD.ROLES_PERMISOS","INVENTARIO.CONTEO","COMPRAS.VER","PROVEEDORES.GESTIONAR","CLIENTES.VER","CLIENTES.CREAR","CLIENTES.EDITAR","CLIENTES.CREDITO_CONFIGURAR","VENTAS.VER"], false));
                await products.ReloadCommand.ExecuteAsync(null);
                Check(shell.IsHome && shell.CanOpenProducts, "Una sesión autorizada abre el inicio con acceso al catálogo");
                await RenderAsync(shell, 1366, 768, Path.Combine(output, "inicio-sesion-claro.png"));
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
                    for (var section = 1; section <= 2; section++)
                    {
                        var sectionEditor = editorFactory.Create(null, await productRepository.GetCatalogsAsync());
                        var sectionView = new ProductEditorView { DataContext = sectionEditor };
                        ((TabControl)sectionView.FindName("EditorSections")!).SelectedIndex = section;
                        await RenderDialogAsync(sectionEditor, sectionView, Path.Combine(output, $"producto-seccion-{section}-{theme}.png"));
                    }
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
                productRepository.Rows = [
                    new() { Id = 1, Sku = "UI-001", Name = "Resina para prueba de interfaz", CategoryId = 1, Category = "Restauración", UnitId = 1, Unit = "Unidad", Price = 125.50m, AvailableStock = 12, MinimumStock = 5, TracksLot = true, TracksExpiry = true, ExpiryAlertDays = 90, RowVersion = new byte[8] },
                    new() { Id = 2, Sku = "UI-002", Name = "Adhesivo para prueba de interfaz", CategoryId = 1, Category = "Adhesivos", UnitId = 1, Unit = "Unidad", Price = 32.25m, AvailableStock = 3, MinimumStock = 5, RowVersion = new byte[8] },
                    new() { Id = 3, Sku = "UI-003", Name = "Guantes para prueba de interfaz", CategoryId = 1, Category = "Protección", UnitId = 1, Unit = "Caja", Price = 8.50m, AvailableStock = 0, MinimumStock = 2, RowVersion = new byte[8] },
                    new() { Id = 4, Sku = "UI-004", Name = "Producto inactivo de prueba", CategoryId = 1, Category = "General", UnitId = 1, Unit = "Unidad", Price = 10m, AvailableStock = 5, Active = false, RowVersion = new byte[8] }
                ];
                await products.ReloadCommand.ExecuteAsync(null);
                var stockCaption = new GoDental.Wpf.Converters.ProductStockCaptionConverter();
                Check(stockCaption.Convert(productRepository.Rows[0], typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture).Equals("Con stock")
                    && stockCaption.Convert(productRepository.Rows[1], typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture).Equals("Stock bajo")
                    && stockCaption.Convert(productRepository.Rows[2], typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture).Equals("Agotado"), "El catálogo distingue niveles de disponible físico sin confundirlos con stock vendible");
                Check(new GoDental.Wpf.Converters.InventoryCostConverter().Convert(2.123456m, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture).Equals("$ 2.123456"), "Los costos conservan seis decimales en USD");
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    themes.Apply(theme);
                    await RenderAsync(shell, 1366, 768, Path.Combine(output, $"productos-lista-{theme}.png"));
                    products.ToggleFiltersCommand.Execute(null);
                    await RenderAsync(shell, 1093, 614, Path.Combine(output, $"productos-filtros-{theme}.png"));
                    products.ToggleFiltersCommand.Execute(null);
                }
                var linkedProducts = new ProductsView { DataContext = products };
                linkedProducts.Measure(new Size(1000, 740)); linkedProducts.Arrange(new Rect(0, 0, 1000, 740)); linkedProducts.UpdateLayout();
                var rowLink = FindChildren<Button>(linkedProducts).First(b => b.CommandParameter is ProductData);
                var linkedProduct = (ProductData)rowLink.CommandParameter;
                await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)rowLink.Command).ExecuteAsync(linkedProduct);
                linkedProducts.UpdateLayout();
                Check(ReferenceEquals(inventory.Product, linkedProduct) && inventory.IsOpen
                    && FindChildren<InventoryView>(linkedProducts).Single().Visibility == Visibility.Visible,
                    "El enlace de producto abre la ficha y muestra el inventario dentro del catálogo");
                var detailEdit = FindChildren<Button>(FindChildren<InventoryView>(linkedProducts).Single()).Single(b => b.Content is string text && text == "Editar producto" && b.Command == products.EditSelectedCommand);
                Check(detailEdit.IsEnabled && products.CanEditSelected, "La ficha conecta la edición del producto con su comando existente");
                linkedProducts.DataContext = null;
                await inventory.OpenAsync(new(){Id=1,Name="Producto para prueba visual",Sku="VISUAL",Category="General",Unit="Unidad",TracksLot=true,TracksExpiry=true});
                Check(inventory.CanWrite && inventory.Stocks.Count==1,"Carga existencias y habilita operaciones con sesión autorizada");
                await RenderAsync(shell, 1366, 768, Path.Combine(output, "productos-detalle.png"));
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    themes.Apply(theme);
                    using var movementModel = new InventoryMovementDialogModel(inventory);
                    await RenderDialogAsync(movementModel, new InventoryMovementDialogView { DataContext = inventory }, Path.Combine(output, $"movimiento-dialogo-{theme}.png"));
                    var lotMovement = new InventoryMovementDialogView { DataContext = inventory };
                    lotMovement.Measure(new Size(900, 700)); lotMovement.Arrange(new Rect(0, 0, 900, 700)); lotMovement.UpdateLayout();
                    FindChildren<TabControl>(lotMovement).Single().SelectedIndex = 1;
                    await RenderDialogAsync(movementModel, lotMovement, Path.Combine(output, $"movimiento-lote-{theme}.png"));
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
                    for(var tab=0;tab<4;tab++) await RenderInventoryAsync(inventory,tab,theme,output);
                }
                inventory.CloseCommand.Execute(null);
                await RenderAsync(shell, 1366, 768, Path.Combine(output, "productos-regreso.png"));
                await users.ReloadCommand.ExecuteAsync(null); nav.Navigate(SetupPage.Users);
                await RenderAsync(shell, 1366, 1000, Path.Combine(output, "usuarios.png"));
                await shell.Counts.ReloadCommand.ExecuteAsync(null);
                shell.Counts.Selected = shell.Counts.Items.First(); await shell.Counts.OpenCommand.ExecuteAsync(null);
                await shell.Suppliers.ReloadCommand.ExecuteAsync(null);
                shell.Suppliers.Selected = shell.Suppliers.Items.First(); await shell.Suppliers.OpenCommand.ExecuteAsync(null);
                Check(shell.CanOpenCounts && shell.CanOpenSuppliers && shell.Counts.CanEdit && shell.Suppliers.CanManage,
                    "Conteos y proveedores se habilitan según permisos");
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    themes.Apply(theme);
                    nav.Navigate(SetupPage.Counts);
                    await RenderAsync(shell, 1366, 768, Path.Combine(output, $"conteos-{theme}.png"));
                    Check(shell.Counts.IsDetailOpen, "Abrir conteo separa la captura del listado");
                    shell.Counts.ReturnToListCommand.Execute(null);
                    await RenderAsync(shell, 1366, 768, Path.Combine(output, $"conteos-lista-{theme}.png"));
                    await RenderAsync(shell, 1093, 614, Path.Combine(output, $"conteos-lista-compacta-{theme}.png"));
                    await shell.Counts.OpenCommand.ExecuteAsync(null);
                    await RenderAsync(shell, 1093, 614, Path.Combine(output, $"conteos-detalle-compacto-{theme}.png"));
                    var countView = new GoDental.Wpf.Views.Inventory.InventoryCountsView { DataContext = shell.Counts };
                    countView.Measure(new Size(1000, 740)); countView.Arrange(new Rect(0, 0, 1000, 740)); countView.UpdateLayout();
                    FindChildren<TabControl>(countView).Single().SelectedIndex = 1;
                    await RenderWindowAsync(new Window { Content = countView, DataContext = shell.Counts, Width = 1000, Height = 740 }, Path.Combine(output, $"conteos-finalizacion-{theme}.png"));
                    nav.Navigate(SetupPage.Suppliers);
                    await RenderAsync(shell, 1366, 768, Path.Combine(output, $"proveedores-{theme}.png"));
                    Check(shell.Suppliers.IsDetailOpen, "Abrir proveedor muestra su ficha separada del listado");
                    shell.Suppliers.ReturnToListCommand.Execute(null);
                    await RenderAsync(shell, 1366, 768, Path.Combine(output, $"proveedores-lista-{theme}.png"));
                    await shell.Suppliers.OpenCommand.ExecuteAsync(null);
                    // Tamaño lógico equivalente al área de 1366×768 al 125 %.
                    await RenderAsync(shell, 1093, 614, Path.Combine(output, $"proveedores-125-{theme}.png"));
                    var suppliersView = new GoDental.Wpf.Views.Suppliers.SuppliersView { DataContext = shell.Suppliers };
                    suppliersView.Measure(new Size(1000, 700)); suppliersView.Arrange(new Rect(0, 0, 1000, 700)); suppliersView.UpdateLayout();
                    var tabs = FindChildren<TabControl>(suppliersView).First();
                    for (var tab = 0; tab < 3; tab++)
                    {
                        tabs.SelectedIndex = tab; suppliersView.UpdateLayout();
                        Check(FindChildren<DataGrid>(suppliersView).All(g => !g.AutoGenerateColumns), $"Tablas explícitas de proveedores: pestaña {tab}, {theme}");
                    }
                    var start = new CountStartDialogModel([new() { Id = 1, Name = "Almacén de prueba" }], new(productService), _ => Task.FromResult(false));
                    await RenderDialogAsync(start, new CountStartDialogView { DataContext = start }, Path.Combine(output, $"abrir-conteo-{theme}.png"));
                    foreach (var kind in Enum.GetValues<SupplierEditorKind>())
                    {
                        var editor = new SupplierEditorDialogModel(kind, shell.Suppliers.Detail!, shell.Suppliers.Contacts.First(), shell.Suppliers.References.First(), new(productService), [new() { Id = 1, Name = "USD" }], _ => Task.FromResult(false));
                        await RenderDialogAsync(editor, new GoDental.Wpf.Views.Suppliers.SupplierEditorDialogView { DataContext = editor }, Path.Combine(output, $"proveedor-{kind}-{theme}.png"));
                    }
                }
                await shell.Customers.ReloadCommand.ExecuteAsync(null);
                shell.Customers.Selected = shell.Customers.Items.First(); await shell.Customers.OpenCommand.ExecuteAsync(null);
                await shell.Customers.LoadSalesCommand.ExecuteAsync(null); await shell.Customers.LoadAccountsCommand.ExecuteAsync(null); await shell.Customers.LoadPaymentsCommand.ExecuteAsync(null);
                Check(shell.CanOpenCustomers && shell.Customers.CanConfigureCredit && shell.Customers.Addresses.Count == 1,
                    "Clientes conecta permisos, contactos, direcciones e historial");
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    themes.Apply(theme); nav.Navigate(SetupPage.Customers);
                    await RenderAsync(shell, 1366, 768, Path.Combine(output, $"clientes-{theme}.png"));
                    Check(shell.Customers.IsDetailOpen, "Abrir cliente muestra su ficha separada del listado");
                    shell.Customers.ReturnToListCommand.Execute(null);
                    await RenderAsync(shell, 1366, 768, Path.Combine(output, $"clientes-lista-{theme}.png"));
                    shell.Customers.ToggleFiltersCommand.Execute(null);
                    await RenderAsync(shell, 1093, 614, Path.Combine(output, $"clientes-filtros-{theme}.png"));
                    shell.Customers.ToggleFiltersCommand.Execute(null);
                    await shell.Customers.OpenCustomerCommand.ExecuteAsync(shell.Customers.Items.First());
                    await RenderAsync(shell, 1093, 614, Path.Combine(output, $"clientes-125-{theme}.png"));
                    var view = new GoDental.Wpf.Views.Customers.CustomersView { DataContext = shell.Customers };
                    view.Measure(new Size(1000, 700)); view.Arrange(new Rect(0, 0, 1000, 700)); view.UpdateLayout();
                    var tabs = FindChildren<TabControl>(view).First();
                    Check(tabs.Items.Count == 8, "Ficha de cliente organizada en ocho pestañas");
                    for (var i = 0; i < tabs.Items.Count; i++)
                    {
                        if (i == 5) await shell.Customers.LoadAccountsCommand.ExecuteAsync(null);
                        if (i == 6) await shell.Customers.LoadSalesCommand.ExecuteAsync(null);
                        if (i == 7) await shell.Customers.LoadPaymentsCommand.ExecuteAsync(null);
                        tabs.SelectedIndex = i; view.UpdateLayout();
                        Check(FindChildren<DataGrid>(view).All(g => g.IsReadOnly && !g.AutoGenerateColumns), $"Consulta de cliente explícita: {theme}, pestaña {i}");
                        var scroller = new ScrollViewer { Content = view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                        var frame = new Border { Padding = new Thickness(24), Background = (System.Windows.Media.Brush)appResource("SurfaceBrush"), Child = scroller };
                        await RenderWindowAsync(new Window { Content = frame, DataContext = shell.Customers, Width = 1000, Height = 740 }, Path.Combine(output, $"clientes-pestana-{i}-{theme}.png"));
                        scroller.Content = null;
                        frame.Child = null;
                        Check(FindChildren<Button>(view).Where(b => b.Content is string text && text is "Editar cliente" or "Nuevo contacto" or "Nueva dirección" or "Configurar crédito").All(b => b.ActualWidth == 152 && b.ActualHeight == 36), "Las acciones de Clientes conservan un tamaño uniforme");
                    }
                    foreach (var kind in Enum.GetValues<CustomerEditorKind>())
                    {
                        var form = new CustomerEditorDialogModel(kind, shell.Customers.Detail!, shell.Customers.Contacts.First(), shell.Customers.Addresses.First(), shell.Customers.Credit, new([new() { Id = 1, Name = "Clínica" }], []), _ => Task.FromResult(false));
                        await RenderDialogAsync(form, new GoDental.Wpf.Views.Customers.CustomerEditorDialogView { DataContext = form }, Path.Combine(output, $"cliente-{kind}-{theme}.png"));
                    }
                }
                session.Begin(new(4, "lector_clientes", "Lector clientes", 1, [], ["CLIENTES.VER"], false));
                await shell.Customers.ReloadCommand.ExecuteAsync(null); shell.Customers.Selected = shell.Customers.Items.First(); await shell.Customers.OpenCommand.ExecuteAsync(null);
                Check(shell.CanOpenCustomers && !shell.Customers.CanCreate && !shell.Customers.CanEdit && !shell.Customers.CanReadHistory && shell.Customers.Sales.Count == 0,
                    "El lector de clientes no obtiene edición ni historial financiero");
                session.Begin(new(3, "lector_compras", "Lector de compras", 1, [], ["COMPRAS.VER"], false));
                await shell.Suppliers.ReloadCommand.ExecuteAsync(null);
                shell.Suppliers.Selected = shell.Suppliers.Items.First(); await shell.Suppliers.OpenCommand.ExecuteAsync(null);
                Check(shell.CanOpenSuppliers && !shell.Suppliers.CanManage && !shell.Suppliers.CanEditChildren,
                    "El lector de compras puede consultar sin acciones de escritura");
                nav.Navigate(SetupPage.Suppliers);
                await RenderAsync(shell, 1366, 768, Path.Combine(output, "proveedores-solo-lectura.png"));
                session.Begin(new(2,"vendedor_visual","Vendedor de prueba visual",1,["Vendedor"],[ProductService.ReadPermission],false));
                await products.ReloadCommand.ExecuteAsync(null); nav.Navigate(SetupPage.Products);
                Check(products.CanView && !products.CanManage && !shell.CanOpenUsers && !shell.CanOpenCounts && !shell.CanOpenSuppliers && shell.Suppliers.References.Count == 0 && !shell.CanOpenCustomers && shell.Customers.Addresses.Count == 0, "El vendedor sólo ve acciones autorizadas");
                await RenderAsync(shell, 1366, 768, Path.Combine(output, "productos-vendedor.png"));
                await access.SignOutCommand.ExecuteAsync(null);
                Check(products.Products.Count == 0 && !shell.CanOpenProducts, "Cerrar sesión limpia datos y oculta el catálogo");
                session.Begin(new(1, "mvp_visual", "Prueba MVP", 1, ["Administrador"], ["CAJA.VER", "CAJA.ABRIR", "CAJA.CERRAR", "CAJA.MOVIMIENTO", "AJUSTES.EDITAR", "VENTAS.VER", "VENTAS.CREAR", "VENTAS.COTIZAR", "VENTAS.REIMPRIMIR", "VENTAS.USAR_CREDITO", "CLIENTES.VER", "INVENTARIO.VER"], false));
                await cashMvp.ReloadCommand.ExecuteAsync(null);
                await posMvp.ReloadCommand.ExecuteAsync(null);
                posMvp.Items.Add(new(1, "UI-TEST", "Producto de prueba", 1, 0));
                await posMvp.CalculateCommand.ExecuteAsync(null);
                await creditMvp.ReloadCommand.ExecuteAsync(null); creditMvp.Selected = creditMvp.Items.First();
                await creditMvp.HistoryCommand.ExecuteAsync(null);
                await ordersMvp.ReloadCommand.ExecuteAsync(null); ordersMvp.Selected = ordersMvp.Items.First();
                await ordersMvp.DetailCommand.ExecuteAsync(null);
                var emitted = mvpRepository.QuoteFactory(new(1, 1, [new(1, 1)]));
                mvpRepository.Quotations.Add(new() { Id = 10, Number = "COT-PRUEBA", State = "EMITIDA", Total = emitted.Total, SnapshotJson = System.Text.Json.JsonSerializer.Serialize(emitted) });
                await historyMvp.ReloadSalesCommand.ExecuteAsync(null); historyMvp.SelectedSale = historyMvp.Sales.First(); await historyMvp.ReadSaleCommand.ExecuteAsync(null);
                await historyMvp.ReloadQuotationsCommand.ExecuteAsync(null); historyMvp.SelectedQuotation = historyMvp.Quotations.First(); await historyMvp.ReadQuotationCommand.ExecuteAsync(null);
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    themes.Apply(theme);
                    foreach (var page in new[] { SetupPage.Cash, SetupPage.Pos, SetupPage.Credit, SetupPage.Orders, SetupPage.Sales, SetupPage.Quotations })
                    {
                        nav.Navigate(page);
                        await RenderAsync(shell, 1366, 768, Path.Combine(output, $"mvp-{page}-{theme}.png"));
                        await RenderAsync(shell, 1093, 614, Path.Combine(output, $"mvp-{page}-compacto-{theme}.png"));
                        if (page == SetupPage.Pos)
                        {
                            var cartWindow = new MainWindow(shell) { Width = 1366, Height = 768, ShowActivated = false };
                            var cartRoot = (FrameworkElement)cartWindow.Content;
                            cartRoot.Measure(new Size(1366, 768)); cartRoot.Arrange(new Rect(0, 0, 1366, 768)); cartRoot.UpdateLayout();
                            FindChildren<TabControl>(FindChildren<PosView>(cartRoot).Single()).First().SelectedIndex = 1;
                            await RenderWindowAsync(cartWindow, Path.Combine(output, $"mvp-Carrito-{theme}.png"));
                        }
                    }
                    mvpDialogs.Render = async form =>
                    {
                        var view = new MvpFormView(); view.Connect(form);
                        await RenderDialogAsync(form, view, Path.Combine(output, $"mvp-dialogo-{form.PrimaryActionLabel.Replace(' ', '-')}-{theme}.png"));
                    };
                    await cashMvp.MoveCommand.ExecuteAsync(null);
                    await cashMvp.CloseCommand.ExecuteAsync(null);
                    await posMvp.CheckoutCommand.ExecuteAsync(null);
                    await posMvp.SaveQuotationCommand.ExecuteAsync(null);
                    await posMvp.SelectCustomerCommand.ExecuteAsync(null);
                    await creditMvp.PayCommand.ExecuteAsync(null);
                    await ordersMvp.CreateCommand.ExecuteAsync(null);
                    await ordersMvp.TransitionCommand.ExecuteAsync(null);
                    await ordersMvp.LinkCommand.ExecuteAsync(null);
                    mvpDialogs.Render = null;
                }
                Check(posMvp.Quote?.Total == emitted.Total && ordersMvp.Detail is not null && creditMvp.Payments.Count > 0, "MVP muestra cálculos, pedido y abonos de sus servicios");
                var controlHost = new StackPanel();
                var input = new TextBox { Text = "Prueba de edición", Width = 220 };
                var password = new PasswordBox { Password = "Sólo-prueba", Width = 220 };
                var selector = new ComboBox { ItemsSource = new[] { new CatalogOption { Id = 1, Name = "Caja de prueba" } }, DisplayMemberPath = "Name", SelectedIndex = 0, Width = 220 };
                var action = new Button { Style = (Style)appResource("ActionButton"), Content = "Registrar", HorizontalAlignment = HorizontalAlignment.Left };
                controlHost.Children.Add(input); controlHost.Children.Add(password); controlHost.Children.Add(selector); controlHost.Children.Add(action);
                controlHost.Measure(new Size(500, 300)); controlHost.Arrange(new Rect(0, 0, 500, 300)); controlHost.UpdateLayout();
                Check(input.Template.FindName("PART_ContentHost", input) is ScrollViewer && password.Template.FindName("PART_ContentHost", password) is ScrollViewer, "Los campos modernos conservan el host de texto y contraseña");
                Check(selector.Template.FindName("PART_Popup", selector) is System.Windows.Controls.Primitives.Popup && selector.SelectedIndex == 0, "El desplegable moderno conserva popup y selección");
                Check(FindChildren<TextBlock>(selector).Any(t => t.Text == "Caja de prueba"), "El desplegable muestra el nombre del objeto seleccionado, sin nombres internos de clases");
                Check(action.ActualWidth == 152 && action.ActualHeight == 36, "Las acciones comparten tamaño de 152 por 36 DIP");
                Check(((Border)input.Template.FindName("InputSurface", input)).CornerRadius.TopLeft == 8, "Los campos siguen el radio de control de las referencias");
                Check(((Style)appResource("StateBadge")).TargetType == typeof(Border) && ((CornerRadius)appResource("CardRadius")).TopLeft == 12, "Tarjetas y estados utilizan geometría compartida");
                Check(new GoDental.Wpf.Converters.UsdMoneyConverter().Convert(1250m, typeof(string), null!, System.Globalization.CultureInfo.GetCultureInfo("es-SV")).Equals("$ 1,250.00"), "Los importes operativos se presentan en USD sin depender de la configuración regional");
                password.Clear();
                var customerForm = new CustomerEditorDialogModel(CustomerEditorKind.General, new(), null, null, null,
                    new([new() { Id = 1, Name = "Paciente" }, new() { Id = 2, Name = "Clínica" }], []), _ => Task.FromResult(false));
                var customerEditor = new GoDental.Wpf.Views.Customers.CustomerEditorDialogView { DataContext = customerForm };
                customerEditor.Measure(new Size(1000, 700)); customerEditor.Arrange(new Rect(0, 0, 1000, 700)); customerEditor.UpdateLayout();
                var customerEditorTabs = FindChildren<TabControl>(customerEditor).Single();
                customerEditorTabs.SelectedIndex = 2; customerEditor.UpdateLayout();
                var typeSelector = FindChildren<ComboBox>(customerEditor).Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Tipo de cliente");
                typeSelector.SelectedValue = 2;
                typeSelector.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)!.UpdateSource();
                Check(!Validation.GetHasError(typeSelector) && customerForm.BuildCustomer().TypeId == 2,
                    "Cambiar el tipo de cliente no produce error de conversión ni borde rojo");
                session.Clear();
                var loginWindow = new LoginWindow(shell) { ShowActivated = false };
                var workspaceWindow = new MainWindow(shell) { ShowActivated = false };
                var coordinator = new WindowCoordinator(System.Windows.Application.Current, loginWindow, workspaceWindow, session, access);
                coordinator.Start();
                Check(workspaceWindow.Width <= SystemParameters.WorkArea.Width && workspaceWindow.Height <= SystemParameters.WorkArea.Height,
                    "El espacio de trabajo cabe en el área útil incluso con escala de Windows");
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
            await VerifyShutdownProcessesAsync();
            Console.WriteLine($"{passed} comprobaciones correctas.");
        }
        finally { Directory.Delete(temporaryFolder, recursive: true); }
    }

    private static async Task StartShutdownProbeAsync(ShellViewModel shell, ISessionController session)
    {
        var app = System.Windows.Application.Current;
        var login = new LoginWindow(shell);
        var workspace = new MainWindow(shell);
        var service = new ShutdownProbeService(session);
        var host = Host.CreateDefaultBuilder().ConfigureServices(s => s.AddSingleton<IHostedService>(_ => service)).Build();
        await host.StartAsync();
        var coordinator = new WindowCoordinator(app, login, workspace, session, shell.Access, () => HostShutdown.StopAsync(host));
        app.Exit += (_, _) =>
        {
            shutdownVerified = service.Stopped && service.Disposed && !login.IsVisible && !workspace.IsVisible;
            coordinator.Dispose();
        };
        coordinator.Start();
        if (shutdownProbe == "--shutdown-workspace")
            session.Begin(new(1, "probe", "Cuenta de prueba", 1, [], [ProductService.ReadPermission], false));
        await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var active = shutdownProbe == "--shutdown-workspace" ? (Window)workspace : login;
        Check(active.IsVisible, "La ventana de la prueba de cierre está visible");
        // El seguimiento del coordinador debe resistir un MainWindow obsoleto.
        app.MainWindow = ReferenceEquals(active, workspace) ? login : workspace;
        active.Close();
    }

    private sealed class ShutdownProbeService(ISessionController session) : IHostedService, IDisposable
    {
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public async Task StopAsync(CancellationToken ct)
        {
            await Task.Delay(30, ct);
            session.Clear(); // No debe reaparecer otra ventana durante la salida.
            Stopped = true;
        }
        public void Dispose() => Disposed = true;
    }

    private static async Task VerifyShutdownProcessesAsync()
    {
        foreach (var mode in new[] { "--shutdown-login", "--shutdown-workspace" })
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
            start.ArgumentList.Add(mode);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("No se inició la prueba de cierre.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException("El proceso siguió vivo después de cerrar: " + mode);
            }
            Check(process.ExitCode == 0 && (await output).Contains("SHUTDOWN_OK"), "Cerrar con X termina proceso, host y ventanas: " + mode + " " + await error);
        }
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
            if (model is MvpFormModel { Body: CheckoutModel checkout })
            {
                var methods = FindChildren<ListBox>(body).Single(box => ReferenceEquals(box.ItemsSource, checkout.Methods));
                var previous = checkout.Method;
                methods.SelectedItem = checkout.Methods.LastOrDefault();
                Check(ReferenceEquals(checkout.Method, methods.SelectedItem), "Los mosaicos de cobro seleccionan el método real sin registrar pagos");
                Check(checkout.Payments.Count == 0, "Seleccionar un método de cobro no agrega ni confirma un pago");
                methods.SelectedItem = previous;
            }
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            await using var stream = File.Create(path); encoder.Save(stream);
        }
        finally
        {
            // Los formularios de operaciones piden confirmar el descarte incluso en
            // capturas sin guardar. Responder a esa ventana evita bloquear el runner.
            if (model.HasUnsavedChanges)
            {
                var discard = window.Dispatcher.InvokeAsync(() =>
                {
                    var confirmation = System.Windows.Application.Current.Windows.OfType<DialogWindow>()
                        .Single(w => w.Owner == window && w.IsVisible);
                    ((IDialogModel)confirmation.DataContext).SaveCommand.Execute(null);
                }, DispatcherPriority.Loaded);
                window.Close();
                await discard;
                Check(!window.IsVisible, "La captura descarta su formulario y cierra sin guardar: " + model.Title);
            }
            else window.Close();
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Func<ProductEditorViewModel, Task<bool>> Action { get; set; } = _ => Task.FromResult(false);
        public Task<bool> ShowProductEditorAsync(ProductEditorViewModel editor) => Action(editor);
        public Task<bool> ShowCategoryManagerAsync(CategoriesViewModel manager) => Task.FromResult(false);
        public Task<bool> ShowCreateUserAsync(UsersViewModel manager) => Task.FromResult(false);
        public Task<bool> ShowManageUserAsync(UsersViewModel manager) => Task.FromResult(false);
        public Task<bool> ShowCountStartAsync(CountStartDialogModel model) => Task.FromResult(false);
        public Task<bool> ShowSupplierEditorAsync(SupplierEditorDialogModel model) => Task.FromResult(false);
        public Task<bool> ShowInventoryMovementAsync(InventoryViewModel manager) => Task.FromResult(false);
    }

    private sealed class MvpVisualDialogs : IMvpDialogService
    {
        public Func<MvpFormModel, Task>? Render { get; set; }
        public async Task<bool> ShowAsync(MvpFormModel form) { if (Render is not null) await Render(form); return false; }
    }
    private sealed class VisualPdfFiles : IPdfFileService
    {
        public Task<bool> SaveAsync(byte[] pdf, string suggestedName) => Task.FromResult(false);
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

    private static IEnumerable<T> FindChildren<T>(DependencyObject node) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is T match) yield return match;
            foreach (var descendant in FindChildren<T>(child)) yield return descendant;
        }
    }

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
            root.InvalidateMeasure();
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();
            if (root.DataContext is ShellViewModel { IsPos: true } && width >= 1280 && height >= 768)
            {
                var pos = ((ShellViewModel)root.DataContext).Pos;
                var catalog = FindChildren<ListBox>(root).FirstOrDefault(box => ReferenceEquals(box.ItemsSource, pos.Products));
                if (catalog is { Items.Count: > 0 })
                {
                    catalog.SelectedIndex = 0;
                    Check(ReferenceEquals(pos.Product, catalog.SelectedItem) && pos.CanAddProduct, "Las tarjetas del catálogo conservan selección y habilitan la acción de carrito");
                }
                var checkout = FindChildren<Button>(root).Single(button => button.Content is string caption && caption == "Cobrar");
                var bounds = checkout.TransformToAncestor(root).TransformBounds(new Rect(checkout.RenderSize));
                Check(bounds.Bottom <= height - 56 && bounds.Right <= width,
                    "El botón Cobrar permanece visible junto al catálogo y al carrito a 1366×768");
            }
            if (root.DataContext is ShellViewModel { IsProducts: true } productShell && productShell.Products.CanManage)
            {
                var productsView = FindChildren<ProductsView>(root).Single();
                var caption = productShell.Products.Inventory?.IsOpen == true ? "Registrar movimiento" : "Nuevo producto";
                var action = FindChildren<Button>(productsView).Single(b => b.Content is string text && text == caption);
                var bounds = action.TransformToAncestor(root).TransformBounds(new Rect(action.RenderSize));
                Check(action.ActualWidth == 152 && action.ActualHeight == 36 && bounds.Left >= 0 && bounds.Right <= width && bounds.Bottom <= height,
                    $"La acción de Productos/Inventario cabe en {width}×{height} con tamaño uniforme");
            }
            if (root.DataContext is ShellViewModel { IsCounts: true } countShell)
            {
                var countView = FindChildren<GoDental.Wpf.Views.Inventory.InventoryCountsView>(root).Single();
                var caption = countShell.Counts.IsDetailOpen ? "Actualizar detalle" : "Abrir conteo";
                var action = FindChildren<Button>(countView).Single(b => b.Content is string text && text == caption);
                var bounds = action.TransformToAncestor(root).TransformBounds(new Rect(action.RenderSize));
                Check(action.ActualWidth == 152 && action.ActualHeight == 36 && bounds.Left >= 0 && bounds.Right <= width && bounds.Bottom <= height,
                    $"La acción principal de Conteos cabe en {width}×{height}");
            }
            if (root.DataContext is ShellViewModel { IsCustomers: true } customerShell)
            {
                var customersView = FindChildren<GoDental.Wpf.Views.Customers.CustomersView>(root).Single();
                var caption = customerShell.Customers.IsDetailOpen ? "Editar cliente" : "Nuevo cliente";
                var action = FindChildren<Button>(customersView).Single(b => b.Content is string text && text == caption);
                var bounds = action.TransformToAncestor(root).TransformBounds(new Rect(action.RenderSize));
                Check(action.ActualWidth == 152 && action.ActualHeight == 36 && bounds.Right <= width && bounds.Bottom <= height,
                    $"La acción principal de Clientes cabe en {width}×{height} con tamaño uniforme");
            }
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

