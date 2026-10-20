using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

internal static class AutomaticCodeIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory, string root, string database)
    {
        var checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; Console.WriteLine("OK " + message); }
        await using var c = await factory.OpenConnectionAsync();
        var hasher = new Pbkdf2PasswordHasher();
        var gateway = new SqlIdentityGateway(factory, hasher);
        var identity = (await gateway.CreateAdministratorAsync("codes_admin", "Administrador de códigos", Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), "CODES-TEST")).Value!;
        var session = new UserSession(); session.Begin(identity);
        var users = new UserManagementService(new SqlUserRepository(factory), session, hasher);
        Check((await users.ListAsync()).Value!.Single().EmployeeCode == $"EMP-{identity.Id:D10}", "Primer administrador recibe código de personal sin cambiar acceso");
        var role = (await users.RolesAsync()).First();
        Check((await users.CreateAsync("codes_staff", "Personal de prueba", role.Id, Convert.ToHexString(RandomNumberGenerator.GetBytes(24)))).IsSuccess, "Alta de personal asigna código automáticamente");
        var staff = (await users.ListAsync()).Value!.Single(u => u.UserName == "codes_staff");
        Check(staff.EmployeeCode == $"EMP-{staff.Id:D10}" && staff.EmployeeCode != (await users.ListAsync()).Value!.Single(u => u.Id == identity.Id).EmployeeCode, "Códigos de personal son distintos y estables");
        var repo = new SqlProductRepository(factory); var products = new ProductService(repo, session);
        var catalogs = (await products.GetCatalogsAsync()).Value!;
        ProductData Product(string sku = "") => new() { Sku = sku, Name = "Producto automático", CategoryId = catalogs.Categories[0].Id, UnitId = (short)catalogs.Units[0].Id, Price = 10 };
        Check((await products.SaveAsync(Product("PRD-0000000001"))).IsSuccess, "Conserva código explícito histórico para probar colisión");
        var requests = Enumerable.Range(0, 6).Select(_ => Product()).ToArray();
        var created = await Task.WhenAll(requests.Select(p => products.SaveAsync(p)));
        Check(created.All(r => r.IsSuccess), "Seis altas concurrentes de productos sin SKU se confirman");
        var codes = (await products.SearchAsync(new())).Value!.Items.Where(p => created.Select(r => r.Value).Contains(p.Id)).Select(p => p.Sku).ToArray();
        Check(codes.Length == 6 && codes.Distinct().Count() == 6 && codes.All(s => s.StartsWith("PRD-") && s.Length == 14) && !codes.Contains("PRD-0000000001"), "SKU único y salta colisión histórica");
        Check(requests.All(p => p.Sku == ""), "Asignar código no muta la solicitud del llamador");
        var beforeSequence = await c.ExecuteScalarAsync<long>("SELECT UltimoNumero FROM sis.SecuenciasDocumento WHERE TipoDocumento=N'MAESTRO_PRODUCTO';");
        var count = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.Productos;");
        var failed = false;
        try { await repo.SaveAsync(Product(), identity.Id, int.MaxValue); } catch (SqlException ex) when (ex.Number == 547) { failed = true; }
        Check(failed && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.Productos;") == count && await c.ExecuteScalarAsync<long>("SELECT UltimoNumero FROM sis.SecuenciasDocumento WHERE TipoDocumento=N'MAESTRO_PRODUCTO';") == beforeSequence, "Fallo de auditoría revierte producto y correlativo completo");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE Entidad=N'Productos' AND JSON_VALUE(DatosDespuesJson,'$.Sku') LIKE N'PRD-%';") == 7, "Auditoría conserva SKU realmente asignado");
        var customers = new CustomerService(new SqlCustomerRepository(factory), session);
        var type = (short)(await customers.CatalogsAsync()).Types.First().Id;
        var customerRequests = Enumerable.Range(0, 4).Select(_ => new CustomerChange(new() { Name = "Cliente automático", TypeId = type }, "Alta sin código")).ToArray();
        var customerResults = await Task.WhenAll(customerRequests.Select(r => customers.SaveAsync(r)));
        Check(customerResults.All(r => r.IsSuccess), "Altas simultáneas de clientes no requieren escribir código");
        var customerRows = (await customers.SearchAsync(new())).Items;
        Check(customerRows.Count == 4 && customerRows.Select(r => r.Code).Distinct().Count() == 4 && customerRows.All(r => r.Code.StartsWith("CLI-")), "Clientes conservan códigos únicos");
        var edited = customerRows[0];
        Check((await customers.SaveAsync(new(edited with { Name = "Cliente editado" }, "Editar nombre"))).IsSuccess && (await customers.ReadAsync(edited.Id))!.Customer.Code == edited.Code, "Editar nombre conserva código del cliente");
        var suppliers = new SupplierService(new SqlSupplierRepository(factory), session);
        var supplierResults = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => suppliers.SaveAsync(new(new() { Name = "Proveedor automático" }, "Alta sin código"))));
        Check(supplierResults.All(r => r.IsSuccess) && (await suppliers.SearchAsync(new())).Items.Select(s => s.Code).Distinct().Count() == 4, "Proveedores reciben códigos únicos en altas simultáneas");
        var mvp = new SqlMvpRepository(factory); var cash = new CashService(mvp, session);
        var warehouse = (await new InventoryService(new SqlInventoryRepository(factory), session).WarehousesAsync()).First().Id;
        var boxRequest = new CashBoxChange(Guid.NewGuid(), 0, "", "Caja automática", warehouse, true, "Alta automática");
        var boxes = await Task.WhenAll(cash.SaveBoxAsync(boxRequest), cash.SaveBoxAsync(boxRequest));
        Check(boxes.All(b => b.IsSuccess) && boxes[0].Value!.Id == boxes[1].Value!.Id && (await cash.BoxesAsync()).Single(b => b.Id == boxes[0].Value!.Id).Code.StartsWith("CAJ-"), "Código automático de caja respeta reenvío idempotente");
        var sequenceRows = await c.QueryAsync<long>("SELECT UltimoNumero FROM sis.SecuenciasDocumento WHERE TipoDocumento LIKE N'MAESTRO_%' ORDER BY TipoDocumento;");
        await DevelopmentDatabase.InstallAsync(database, Path.Combine(root, "database"));
        Check(sequenceRows.SequenceEqual(await c.QueryAsync<long>("SELECT UltimoNumero FROM sis.SecuenciasDocumento WHERE TipoDocumento LIKE N'MAESTRO_%' ORDER BY TipoDocumento;")), "Reaplicar migración no reinicia códigos");
        await c.ExecuteAsync("UPDATE sis.SecuenciasDocumento SET UltimoNumero=9999999999 WHERE TipoDocumento=N'MAESTRO_PRODUCTO';");
        Check((await products.SaveAsync(Product())).ErrorCode == "CODE_UNAVAILABLE", "Agotamiento no trunca ni reutiliza SKU");
        Check((await products.SaveAsync(Product("CODIGO-EXTERNO"))).IsSuccess, "Conserva compatibilidad con referencias explícitas");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario;") == 0, "Asignar códigos nunca crea stock ni movimientos");
        Console.WriteLine($"{checks} comprobaciones de códigos automáticos correctas.");
    }
}
