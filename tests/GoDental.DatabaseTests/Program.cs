using System.Data.Common;
using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Data;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

if (args is ["--recovery-journal-probe", var journalFolder, var serializedPending])
{
    var pending = System.Text.Json.JsonSerializer.Deserialize<PendingOperation>(Convert.FromBase64String(serializedPending))!;
    if (!await new FilePendingOperationStore(journalFolder).TryAddAsync(pending)) throw new InvalidOperationException("El diario ya tenía una solicitud.");
    Console.WriteLine("JOURNAL_READY");
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return;
}
var root = args.LastOrDefault(a => Directory.Exists(Path.Combine(a, "database"))) ?? "GoDental";
try { DevelopmentDatabase.TestServer = DevelopmentServerOptions.Parse(args); }
catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 2; return; }
if (args.Contains("--run-purchases"))
{
    var purchaseDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
    await DevelopmentDatabase.InstallAsync(purchaseDatabase, Path.Combine(root, "database"));
    await PurchaseIntegration.RunAsync(new TestConnectionFactory(purchaseDatabase), root, purchaseDatabase);
    return;
}
if (args.Contains("--run-complements"))
{
    var complementDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
    await DevelopmentDatabase.InstallAsync(complementDatabase, Path.Combine(root, "database"));
    await ComplementsIntegration.RunAsync(new TestConnectionFactory(complementDatabase), root, complementDatabase);
    return;
}
if (args.Contains("--run-recovery"))
{
    var recoveryDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
    await DevelopmentDatabase.InstallAsync(recoveryDatabase, Path.Combine(root, "database"));
    await OperationRecoveryIntegration.RunAsync(new TestConnectionFactory(recoveryDatabase), root, recoveryDatabase);
    return;
}
if (args.Contains("--run-codes"))
{
    var codesDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
    await DevelopmentDatabase.InstallAsync(codesDatabase, Path.Combine(root, "database"));
    await AutomaticCodeIntegration.RunAsync(new TestConnectionFactory(codesDatabase), root, codesDatabase);
    return;
}
if (args.Contains("--run-mvp"))
{
    var mvpDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
    await DevelopmentDatabase.InstallAsync(mvpDatabase, Path.Combine(root, "database"));
    await MvpIntegration.RunAsync(new TestConnectionFactory(mvpDatabase), root, mvpDatabase);
    return;
}
if (args.Contains("--run-customers"))
{
    var customerDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
    await DevelopmentDatabase.InstallAsync(customerDatabase, Path.Combine(root, "database"));
    await CustomerIntegration.RunAsync(new TestConnectionFactory(customerDatabase));
    Console.WriteLine("Base aislada retenida: " + customerDatabase);
    return;
}
if (args.Contains("--run-suppliers"))
{
    var supplierDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
    await DevelopmentDatabase.InstallAsync(supplierDatabase, Path.Combine(root, "database"));
    await SupplierIntegration.RunAsync(new TestConnectionFactory(supplierDatabase));
    Console.WriteLine("Base aislada retenida: " + supplierDatabase);
    return;
}
if (args.Contains("--run-counts"))
{
    var countDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
    await DevelopmentDatabase.InstallAsync(countDatabase, Path.Combine(root, "database"));
    await InventoryCountIntegration.RunAsync(new TestConnectionFactory(countDatabase), root);
    Console.WriteLine("Base aislada retenida: " + countDatabase);
    return;
}
if (args.Contains("--run-inventory"))
{
    var inventoryDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
    await DevelopmentDatabase.InstallAsync(inventoryDatabase, Path.Combine(root, "database"));
    await InventoryIntegration.RunAsync(new TestConnectionFactory(inventoryDatabase));
    Console.WriteLine("Base aislada retenida: " + inventoryDatabase);
    return;
}
if (args.Contains("--install-development"))
{
    await DevelopmentDatabase.InstallAsync("GoDental_Development", Path.Combine(root, "database"));
    Console.WriteLine("GoDental_Development lista. No se crearon contraseñas ni usuarios de aplicación.");
    return;
}
if (!args.Contains("--run-integration"))
{
    Console.WriteLine(@"Uso: --install-development | --run-integration | --run-inventory | --run-counts | --run-suppliers | --run-customers | --run-mvp | --run-codes | --run-recovery | --run-complements | --run-purchases [--server .\SQLEXPRESS | --localdb] [ruta del repositorio]. SQL local y autenticación Windows.");
    return;
}

var database = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
await DevelopmentDatabase.InstallAsync(database, Path.Combine(root, "database"));
Console.WriteLine("Base aislada de pruebas: " + database);
var factory = new TestConnectionFactory(database);
var hasher = new Pbkdf2PasswordHasher();
var gateway = new SqlIdentityGateway(factory, hasher);
int checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    checks++; Console.WriteLine("OK " + label);
}
string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
var password = Secret();
Check(await gateway.NeedsAdministratorAsync(), "La base nueva requiere administrador");
var attempts = await Task.WhenAll(
    gateway.CreateAdministratorAsync("admin_a", "Administrador de prueba A", password, "INTEGRATION"),
    gateway.CreateAdministratorAsync("admin_b", "Administrador de prueba B", password, "INTEGRATION"));
Check(attempts.Count(a => a.IsSuccess) == 1, "Dos inicializaciones concurrentes crean un solo administrador");
var identity = attempts.Single(a => a.IsSuccess).Value!;
Check(identity.Permissions.Contains(ProductService.ManagePermission), "El administrador recibe permisos efectivos del seed");
await using var c = await factory.OpenConnectionAsync();
await c.ExecuteAsync("""
    UPDATE sis.Configuracion
    SET Valor = CASE Clave
        WHEN N'Descuento.VendedorMaximo' THEN N'9.00'
        WHEN N'Descuento.RequiereJustificacionSobre' THEN N'9.00'
        WHEN N'Descuento.RequiereAutorizacionSobre' THEN N'42.00'
        WHEN N'Credito.DiasDefault' THEN N'21'
        ELSE N'false' END,
        ModificadoPor = @id,
        FechaModificacion = '2020-01-01T00:00:00+00:00'
    WHERE Clave IN (N'Descuento.VendedorMaximo', N'Descuento.RequiereJustificacionSobre',
                    N'Descuento.RequiereAutorizacionSobre', N'Credito.DiasDefault', N'Precios.PrecedenciaDefinida');
    """, new { id = identity.Id });
await DevelopmentDatabase.ReapplySeedAsync(database, Path.Combine(root, "database"));
Check(await c.ExecuteScalarAsync<int>("""
    SELECT COUNT(*) FROM sis.Configuracion
    WHERE (Clave=N'Descuento.VendedorMaximo' AND Valor=N'9.00' OR
           Clave=N'Descuento.RequiereJustificacionSobre' AND Valor=N'9.00' OR
           Clave=N'Descuento.RequiereAutorizacionSobre' AND Valor=N'42.00' OR
           Clave=N'Credito.DiasDefault' AND Valor=N'21')
      AND ModificadoPor=@id AND FechaModificacion='2020-01-01T00:00:00+00:00';
    """, new { id = identity.Id }) == 4, "Reaplicar el seed conserva parámetros editables y su auditoría");
Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Configuracion WHERE Clave=N'Precios.PrecedenciaDefinida' AND Valor=N'true' AND ModificadoPor IS NULL") == 1,
    "Reaplicar el seed restaura parámetros no editables");
Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seg.Usuarios") == 1 && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seg.UsuarioRoles") == 1, "Inicialización atómica de usuario y rol");
var storedHash = await c.ExecuteScalarAsync<string>("SELECT PasswordHash FROM seg.Usuarios");
Check(storedHash != password && hasher.Verify(password, storedHash!), "SQL conserva sólo hash PBKDF2 verificable");
var missingAccount = await gateway.SignInAsync("cuenta_que_no_existe", password, "INTEGRATION");
var simultaneousFailures = await Task.WhenAll(gateway.SignInAsync(identity.UserName, Secret(), "INTEGRATION"), gateway.SignInAsync(identity.UserName, Secret(), "INTEGRATION"));
Check(!missingAccount.IsSuccess && simultaneousFailures.All(r => !r.IsSuccess && r.Message == missingAccount.Message), "No distingue usuario inexistente de credencial incorrecta");
Check(await c.ExecuteScalarAsync<int>("SELECT IntentosFallidos FROM seg.Usuarios") == 2, "Dos intentos simultáneos no pierden el contador de fallos");
await c.ExecuteAsync("UPDATE seg.Usuarios SET IntentosFallidos=0;");
for (var i = 0; i < 5; i++) Check(!(await gateway.SignInAsync(identity.UserName, Secret(), "INTEGRATION")).IsSuccess, "Rechaza credencial incorrecta " + (i + 1));
Check(!(await gateway.SignInAsync(identity.UserName, password, "INTEGRATION")).IsSuccess, "El bloqueo rechaza incluso una contraseña correcta");
await c.ExecuteAsync("UPDATE seg.Usuarios SET BloqueadoHasta=DATEADD(minute,-1,SYSDATETIMEOFFSET());");
var login = await gateway.SignInAsync(identity.UserName, password, "INTEGRATION");
Check(login.IsSuccess, "Permite acceso tras expirar el bloqueo");
Check(await c.ExecuteScalarAsync<int>("SELECT IntentosFallidos FROM seg.Usuarios") == 0, "El acceso correcto reinicia los intentos");
var newPassword = Secret();
Check((await gateway.ChangePasswordAsync(identity.Id, password, newPassword)).IsSuccess, "Cambia contraseña con auditoría");
Check(!(await gateway.SignInAsync(identity.UserName, password, "INTEGRATION")).IsSuccess && (await gateway.SignInAsync(identity.UserName, newPassword, "INTEGRATION")).IsSuccess, "Invalida la contraseña anterior");

var session = new UserSession();
var repository = new SqlProductRepository(factory);
var service = new ProductService(repository, session);
Check(!(await service.SearchAsync(new())).IsSuccess, "Deniega inventario sin sesión");
session.Begin(identity);
var catalogs = (await service.GetCatalogsAsync()).Value!;
ProductData MakeProduct(string sku) => new() { Sku = sku, Name = "Producto de prueba", CategoryId = catalogs.Categories[0].Id, UnitId = (short)catalogs.Units[0].Id, Price = 12.50m, MinimumStock = 2 };
var created = await service.SaveAsync(MakeProduct("TEST-001"));
Check(created.IsSuccess, "Crea producto real mediante servicio y Dapper");
Check(!(await service.SaveAsync(MakeProduct("TEST-001"))).IsSuccess, "Rechaza SKU duplicado");
var page = (await service.SearchAsync(new("TEST-001"))).Value!;
Check(page.Total == 1 && page.Items[0].AvailableStock == 0 && page.Items[0].RowVersion.Length == 8, "Consulta stock y rowversion reales sin crear existencias");
var copyA = (await repository.SearchAsync(new("TEST-001"))).Items[0];
var copyB = (await repository.SearchAsync(new("TEST-001"))).Items[0];
copyA.Name = "Cambio A"; copyB.Name = "Cambio B";
var concurrent = await Task.WhenAll(service.SaveAsync(copyA), service.SaveAsync(copyB));
Check(concurrent.Count(r => r.IsSuccess) == 1 && concurrent.Count(r => r.ErrorCode == "CONCURRENCY_CONFLICT") == 1, "Dos ediciones concurrentes: una guarda y la otra informa conflicto");
var after = (await repository.SearchAsync(new("TEST-001"))).Items[0]; after.Active = false;
Check((await service.SaveAsync(after)).IsSuccess && (await repository.SearchAsync(new("TEST-001"))).Total == 0 && (await repository.SearchAsync(new("TEST-001", true))).Total == 1, "Desactiva y conserva el producto sin borrarlo");
Check((await service.CreateCategoryAsync("Categoría de prueba")).IsSuccess, "Crea categoría con permiso y auditoría");
var auditCount = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE Modulo=N'Inventario'");
Check(auditCount == 4, "Audita sólo las cuatro mutaciones confirmadas de catálogo");
Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE Entidad=N'Productos' AND TRY_CONVERT(int,JSON_VALUE(DatosDespuesJson,'$.Id'))<>EntidadId") == 0, "La auditoría guarda el identificador persistido del producto");
Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario") == 0 && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.ExistenciasProducto") == 0, "El maestro no inventa movimientos ni existencias");

// Fallo deliberado de FK en auditoría, únicamente en esta base de pruebas.
var before = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.Productos");
var auditFailed = false;
try { await repository.SaveAsync(MakeProduct("ROLLBACK-001"), identity.Id, int.MaxValue); }
catch (SqlException) { auditFailed = true; }
Check(auditFailed && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.Productos") == before, "Revierte producto completo si falla la auditoría");
var userRepository = new SqlUserRepository(factory);
var userService = new UserManagementService(userRepository, session, hasher);
var sellerRole = (await userService.RolesAsync()).Single(r => r.Name == "Vendedor");
var temporaryPassword = Secret();
Check((await userService.CreateAsync("vendedor_prueba", "Vendedor de prueba", sellerRole.Id, temporaryPassword)).IsSuccess, "Administrador crea vendedor con contraseña temporal");
var sellerLogin = (await gateway.SignInAsync("vendedor_prueba", temporaryPassword, "INTEGRATION")).Value!;
Check(sellerLogin.MustChangePassword, "El vendedor nuevo debe cambiar su contraseña");
var permanentSellerPassword = Secret();
Check((await gateway.ChangePasswordAsync(sellerLogin.Id, temporaryPassword, permanentSellerPassword)).IsSuccess, "El vendedor elige su contraseña propia");
var sellerIdentity = (await gateway.SignInAsync("vendedor_prueba", permanentSellerPassword, "INTEGRATION")).Value!;
session.Begin(sellerIdentity);
Check((await service.SearchAsync(new())).IsSuccess && !(await service.SaveAsync(MakeProduct("DENIED"))).IsSuccess, "El vendedor consulta inventario y no puede modificar productos");
Check(!(await userService.ListAsync()).IsSuccess && !(await userService.CreateAsync("intruso", "Otra cuenta", sellerRole.Id, Secret())).IsSuccess, "El vendedor no consulta ni crea cuentas");
session.Begin(identity);
var sellerRow = (await userService.ListAsync()).Value!.Single(u => u.Id == sellerIdentity.Id);
Check((await userService.SetActiveAsync(sellerRow, false, "Baja de prueba controlada")).IsSuccess, "Desactiva una cuenta conservando su historial");
Check(!(await gateway.SignInAsync("vendedor_prueba", permanentSellerPassword, "INTEGRATION")).IsSuccess, "La cuenta desactivada no inicia sesión");
sellerRow = (await userService.ListAsync()).Value!.Single(u => u.Id == sellerIdentity.Id);
Check((await userService.SetActiveAsync(sellerRow, true, "Reactivación de prueba")).IsSuccess, "Reactiva una cuenta");
Check((await userService.ResetPasswordAsync(sellerRow, Secret(), "Restablecimiento con versión antigua")).ErrorCode == "CONCURRENCY_CONFLICT", "Rechaza restablecer con una versión obsoleta");
sellerRow = (await userService.ListAsync()).Value!.Single(u => u.Id == sellerIdentity.Id);
var resetPassword = Secret();
Check((await userService.ResetPasswordAsync(sellerRow, resetPassword, "Restablecimiento de prueba")).IsSuccess, "Restablece contraseña con auditoría y cambio obligatorio");
Check((await gateway.SignInAsync("vendedor_prueba", resetPassword, "INTEGRATION")).Value!.MustChangePassword, "La clave restablecida no omite el cambio obligatorio");
var ownRow = (await userService.ListAsync()).Value!.Single(u => u.Id == identity.Id);
Check(!(await userService.SetActiveAsync(ownRow, false, "Intento de auto desactivación")).IsSuccess, "Impide desactivar la cuenta propia");
var userCount = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seg.Usuarios");
var failedUserAudit = false;
try { await userRepository.CreateAsync("rollback_user", "Prueba rollback", sellerRole.Id, hasher.Hash(Secret()), identity.Id, int.MaxValue); }
catch (SqlException) { failedUserAudit = true; }
Check(failedUserAudit && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seg.Usuarios") == userCount, "Revierte usuario, rol y preferencias cuando falla su auditoría");
var profileStore = new TestProfileStore(new("localhost", database, "Light", true));
IWorkstationConnectionOpener profileOpener = args.Contains("--localdb") ? new IsolatedTestProfileOpener()
    : new SqlConnectionFactory(Microsoft.Extensions.Options.Options.Create(new DatabaseOptions()));
var boundFactory = new WorkstationConnectionFactory(profileOpener, profileStore, session);
await using (var boundConnection = await boundFactory.OpenConnectionAsync())
    Check(await boundConnection.ExecuteScalarAsync<int>("SELECT TRY_CONVERT(int,SESSION_CONTEXT(N'IdUsuario'))") == identity.Id, "Cada conexión SQL recibe el contexto de usuario");
var otherDatabase = "GoDental_Tests_" + Guid.NewGuid().ToString("N");
await DevelopmentDatabase.InstallAsync(otherDatabase, Path.Combine(root, "database"));
profileStore.Profile = new("localhost", otherDatabase, "Light", true);
var otherDatabaseDenied = false;
try { await using var other = await boundFactory.OpenConnectionAsync(); } catch (InvalidOperationException) { otherDatabaseDenied = true; }
Check(otherDatabaseDenied, "La sesión no puede reutilizarse al cambiar de base de datos");
profileStore.Profile = new("localhost", database, "Light", true); session.Begin(sellerIdentity);
var resetSessionDenied = false;
try { await using var obsolete = await boundFactory.OpenConnectionAsync(); } catch (UnauthorizedAccessException) { resetSessionDenied = true; }
Check(resetSessionDenied, "Restablecer la contraseña impide reutilizar una sesión anterior");
session.Begin(identity);
await c.ExecuteAsync("INSERT INTO inv.ExistenciasProducto(IdAlmacen,IdProducto,StockActual) SELECT TOP(1) IdAlmacen,@id,1 FROM inv.Almacenes WHERE Activo=1;", new { id = created.Value });
var traceable = (await repository.SearchAsync(new("TEST-001", true))).Items[0]; traceable.TracksLot = true;
Check((await service.SaveAsync(traceable)).ErrorCode == "TRACEABILITY", "Impide cambiar el control de lotes cuando ya existe stock");
await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'INVENTARIO.PRODUCTOS_GESTIONAR');");
Check((await service.SaveAsync(MakeProduct("REVOKED"))).ErrorCode == "FORBIDDEN", "SQL rechaza una escritura cuando se revoca el permiso durante la sesión");
await c.ExecuteAsync("UPDATE seg.Usuarios SET DebeCambiarClave=1 WHERE IdUsuario=@id", new { id = identity.Id });
var temporaryIdentity = (await gateway.SignInAsync(identity.UserName, newPassword, "INTEGRATION")).Value!;
session.Begin(temporaryIdentity);
Check(!session.IsAuthenticated && session.Permissions.Count == 0, "La contraseña temporal no concede acceso a inventario");
session.Clear();
Check(session.UserId is null && session.Permissions.Count == 0, "Cerrar sesión borra identidad y permisos");
Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE DatosAntesJson LIKE '%PBKDF2%' OR DatosDespuesJson LIKE '%PBKDF2%'") == 0, "La auditoría no expone hashes de contraseña");
Console.WriteLine($"{checks} comprobaciones SQL correctas. Base aislada retenida: {database}");

internal sealed class TestConnectionFactory(string database) : IDbConnectionFactory
{
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var c = new SqlConnection(DevelopmentDatabase.ConnectionString(database));
        try { await c.OpenAsync(ct); return c; } catch { await c.DisposeAsync(); throw; }
    }
}
internal sealed class TestProfileStore(WorkstationSettings profile) : IWorkstationSettingsStore
{
    public WorkstationSettings Profile { get; set; } = profile;
    public Task<WorkstationSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(Profile);
    public Task SaveAsync(WorkstationSettings value, CancellationToken ct = default) { Profile = value; return Task.CompletedTask; }
}

// Sólo en el ejecutable de pruebas: cambia el transporte al LocalDB aislado del runner.
// La fábrica de sesión, el contexto SQL y sus comprobaciones siguen siendo los de producción.
internal sealed class IsolatedTestProfileOpener : IWorkstationConnectionOpener
{
    public Task<DbConnection> OpenAsync(WorkstationSettings settings, CancellationToken ct = default)
    {
        if (GoDental.Application.Validators.WorkstationSettingsValidator.Validate(settings) is { } error)
            throw new ArgumentException(error, nameof(settings));
        if (!System.Text.RegularExpressions.Regex.IsMatch(settings.Database, @"^GoDental_Tests_[A-Za-z0-9]+$"))
            throw new ArgumentException("El adaptador sólo abre bases aisladas de pruebas.", nameof(settings));
        return new TestConnectionFactory(settings.Database).OpenConnectionAsync(ct);
    }
}

