using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

internal static class SupplierIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory)
    {
        var checks = 0;
        void Check(bool ok, string label)
        { if (!ok) throw new InvalidOperationException(label); checks++; Console.WriteLine("OK " + label); }
        await using var c = await factory.OpenConnectionAsync();
        var gateway = new SqlIdentityGateway(factory, new Pbkdf2PasswordHasher());
        var identity = (await gateway.CreateAdministratorAsync("proveedor_test", "Administrador proveedores",
            Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), "SUPPLIER-TEST")).Value!;
        var session = new UserSession(); session.Begin(identity);
        var repository = new SqlSupplierRepository(factory); var service = new SupplierService(repository, session);
        var result = await service.SaveAsync(new(new() { Code = " SUP-001 ", Name = " Proveedor de prueba ", Country = " China ", Address = " " }, "Alta aislada"));
        Check(result.IsSuccess, "Crea proveedor en SQL con auditoría");
        var id = result.Value;
        async Task<SupplierSnapshot> Read() => (await service.ReadAsync(id))!;
        var snapshot = await Read();
        Check(snapshot.Supplier.Code == "SUP-001" && snapshot.Supplier.Country == "China" && snapshot.Supplier.Address is null
            && snapshot.Supplier.RowVersion.Length == 8 && snapshot.Contacts.Count == 0, "Normaliza y persiste ficha sin datos ficticios");
        Check((await service.SaveAsync(new(new() { Code = "SUP-001", Name = "Duplicado" }, "Duplicado"))).ErrorCode == "DUPLICATE_CODE", "Rechaza código duplicado");
        Check((await service.SearchAsync(new("China"))).Total == 1 && (await service.SearchAsync(new("%"))).Total == 0, "Búsqueda literal por país sin comodines inesperados");
        Check((await service.SearchAsync(new("' OR 1=1 --"))).Total == 0, "La búsqueda se parametriza");
        Check((await service.SaveAsync(new(new() { Name = "País inválido", Country = "País inexistente" }, "Catálogo"))).ErrorCode == "VALIDATION", "Proveedor nuevo requiere país de catálogo si lo especifica");
        Check((await service.SaveAsync(new(snapshot.Supplier with { Phone = "13800138000" }, "Teléfono chino nacional"))).IsSuccess, "País China permite teléfono sin prefijo");
        snapshot = await Read();
        Check(!snapshot.Supplier.Phone!.StartsWith("+"), "No fuerza el código internacional del proveedor");
        Check((await service.SaveAsync(new(snapshot.Supplier with { Phone = "+50370000001" }, "Número de otro país"))).IsSuccess, "Prefijo explícito distingue región del teléfono de país comercial");
        snapshot = await Read();
        Check(snapshot.Supplier.Country == "China" && snapshot.Supplier.Phone!.StartsWith("+503"), "Detección telefónica no cambia país comercial");
        var original = snapshot.Supplier;
        Check((await service.SaveContactAsync(new(id, original.RowVersion, new() { Name = "Contacto principal", Email = "compras@example.test", Primary = true }, "Primer contacto"))).IsSuccess,
            "Crea contacto principal");
        snapshot = await Read();
        Check(snapshot.Contacts.Count == 1 && !snapshot.Supplier.RowVersion.SequenceEqual(original.RowVersion), "Contacto avanza versión del agregado");
        Check((await service.SaveAsync(new(original with { Name = "Edición obsoleta" }, "Concurrencia"))).ErrorCode == "CONCURRENCY_CONFLICT", "Ficha obsoleta no sobrescribe cambios de contactos");
        Check((await service.SaveContactAsync(new(id, snapshot.Supplier.RowVersion, new() { Name = "Segundo principal", Primary = true }, "Duplicado principal"))).ErrorCode == "PRIMARY_CONTACT_EXISTS",
            "Rechaza dos contactos principales activos");
        Check((await Read()).Supplier.RowVersion.SequenceEqual(snapshot.Supplier.RowVersion), "Solicitud rechazada no modifica versión ni datos");
        var contact = snapshot.Contacts.Single();
        var request = new SupplierContactChange(id, snapshot.Supplier.RowVersion, contact with { Phone = "13800138000" }, "Cambio concurrente");
        var concurrent = await Task.WhenAll(service.SaveContactAsync(request), service.SaveContactAsync(request with { Contact = contact with { Phone = "13900139000" } }));
        Check(concurrent.Count(r => r.IsSuccess) == 1 && concurrent.Count(r => r.ErrorCode == "CONCURRENCY_CONFLICT") == 1, "Dos estaciones editan contacto sin perder cambios");
        snapshot = await Read(); contact = snapshot.Contacts.Single();
        Check((await service.SaveContactAsync(new(id, snapshot.Supplier.RowVersion, contact with { Active = false, Primary = false }, "Baja de contacto"))).IsSuccess, "Desactiva contacto sin eliminarlo");
        snapshot = await Read();
        Check(snapshot.Contacts.Count == 1 && !snapshot.Contacts.Single().Active, "Conserva historial del contacto");
        Check((await service.SaveContactAsync(new(id, snapshot.Supplier.RowVersion, new() { Id = int.MaxValue, Name = "Ajeno" }, "Inválido"))).ErrorCode == "VALIDATION", "No edita contacto ajeno o inexistente");
        var productService = new ProductService(new SqlProductRepository(factory), session);
        var catalogs = (await productService.GetCatalogsAsync()).Value!;
        var product = (await productService.SaveAsync(new() { Sku = "SUP-PRODUCTO", Name = "Producto proveedor", CategoryId = catalogs.Categories.First().Id,
            UnitId = (short)catalogs.Units.First().Id, Price = 12 })).Value;
        var currency = (short)(await service.CurrenciesAsync()).First().Id;
        snapshot = await Read();
        var link = new SupplierProductData { ProductId = product, SupplierSku = " SKU-CHINA ", LastCost = 1.234567m, CurrencyId = currency,
            MinimumQuantity = .1250m, LeadTimeDays = 30, Preferred = true };
        Check((await service.SaveProductAsync(new(id, snapshot.Supplier.RowVersion, link, "Relación comercial"))).IsSuccess, "Relaciona producto con costo, moneda, MOQ y plazo");
        snapshot = await Read();
        var persisted = snapshot.Products.Single();
        Check(persisted.SupplierSku == "SKU-CHINA" && persisted.LastCost == 1.234567m && persisted.MinimumQuantity == .125m && persisted.LeadTimeDays == 30
            && persisted.CurrencyId == currency && persisted.LocalSku == "SUP-PRODUCTO", "Conserva precisión comercial y referencias locales");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.ExistenciasProducto WHERE IdProducto=@product", new { product }) == 0
            && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario WHERE IdProducto=@product", new { product }) == 0
            && await c.ExecuteScalarAsync<decimal>("SELECT PrecioVentaReferencia FROM inv.Productos WHERE IdProducto=@product", new { product }) == 12,
            "Referencia comercial no inventa stock, Kardex ni cambia precio de venta");
        Check((await service.SaveProductAsync(new(id, snapshot.Supplier.RowVersion, link with { CurrencyId = short.MaxValue }, "Moneda inválida"))).ErrorCode == "VALIDATION",
            "Revalida catálogo de moneda en SQL");
        var beforeRollback = snapshot.Supplier;
        var failed = false;
        try { await repository.SaveProductAsync(new(id, beforeRollback.RowVersion, link with { LastCost = 9 }, "Fallo controlado"), identity.Id, int.MaxValue); }
        catch (SqlException ex) when (ex.Number == 547) { failed = true; }
        snapshot = await Read();
        Check(failed && snapshot.Products.Single().LastCost == 1.234567m && snapshot.Supplier.RowVersion.SequenceEqual(beforeRollback.RowVersion),
            "Fallo de auditoría revierte relación y versión del proveedor");
        failed = false;
        try { await repository.SaveAsync(new(new() { Code = "SUP-ROLLBACK", Name = "No persistir" }, "Fallo alta"), identity.Id, int.MaxValue); }
        catch (SqlException ex) when (ex.Number == 547) { failed = true; }
        Check(failed && (await service.SearchAsync(new("SUP-ROLLBACK"))).Total == 0, "Fallo de auditoría revierte también alta de proveedor");
        Check((await service.SaveAsync(new(snapshot.Supplier with { Active = false }, "Baja lógica"))).IsSuccess, "Desactiva proveedor sin borrado");
        snapshot = await Read();
        Check(!snapshot.Supplier.Active && snapshot.Products.Count == 1 && snapshot.Contacts.Count == 1, "Baja conserva contactos y relaciones");
        Check((await service.SearchAsync(new())).Total == 0 && (await service.SearchAsync(new(IncludeInactive: true))).Total == 1, "Filtro de proveedores activos e históricos");
        Check((await service.SaveProductAsync(new(id, snapshot.Supplier.RowVersion, link, "No activo"))).ErrorCode == "INVALID_STATE", "No modifica relaciones de proveedor inactivo");
        Check((await service.SaveAsync(new(snapshot.Supplier with { Active = true }, "Reactivación"))).IsSuccess, "Reactiva proveedor");
        snapshot = await Read();
        await c.ExecuteAsync("UPDATE inv.Productos SET Activo=0 WHERE IdProducto=@product; UPDATE cat.Monedas SET Activo=0 WHERE IdMoneda=@currency;", new { product, currency });
        Check((await service.SaveProductAsync(new(id, snapshot.Supplier.RowVersion, persisted, "Catálogos inactivos"))).ErrorCode == "VALIDATION", "No activa relación con producto o moneda inactivos");
        Check((await service.SaveProductAsync(new(id, snapshot.Supplier.RowVersion, persisted with { Active = false }, "Baja relación"))).IsSuccess, "Permite baja de relación cuyos catálogos fueron desactivados");
        await c.ExecuteAsync("UPDATE inv.Productos SET Activo=1 WHERE IdProducto=@product; UPDATE cat.Monedas SET Activo=1 WHERE IdMoneda=@currency;", new { product, currency });
        snapshot = await Read();
        Check((await service.SaveProductAsync(new(id, snapshot.Supplier.RowVersion, persisted with { Active = true }, "Reactivación de relación"))).IsSuccess, "Reactiva relación sin duplicarla");
        snapshot = await Read();
        Check(snapshot.Products.Count == 1, "Una relación por proveedor/producto");
        await c.ExecuteAsync("INSERT INTO cmp.OrdenesCompra(NumeroOrden,IdProveedor,IdMoneda,FechaOrden,CreadoPor) VALUES(N'SUP-OC-TEST',@id,@currency,CONVERT(date,SYSDATETIMEOFFSET()),@userId);",
            new { id, currency, userId = identity.Id });
        Check((await service.SaveAsync(new(snapshot.Supplier with { Code = "SUP-CAMBIADO" }, "Código con historial"))).ErrorCode == "IN_USE", "Conserva código del proveedor con órdenes");
        Check((await service.SaveAsync(new(snapshot.Supplier with { Name = "Nombre corregido" }, "Corrección con historial"))).IsSuccess, "Permite corregir ficha manteniendo identidad histórica");
        snapshot = await Read();
        var staleSupplier = snapshot.Supplier;
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'PROVEEDORES.GESTIONAR')");
        Check((await Read()).Products.Single().LastCost is null, "SQL oculta costo comercial con permiso de gestión revocado");
        Check((await service.SaveAsync(new(staleSupplier with { Name = "Denegado" }, "Sin permiso"))).ErrorCode == "FORBIDDEN", "SQL deniega escritura pese a sesión C# cacheada");
        Check((await service.SaveContactAsync(new(id, staleSupplier.RowVersion, new() { Name = "Denegado" }, "Sin permiso"))).ErrorCode == "FORBIDDEN"
            && (await service.SaveProductAsync(new(id, staleSupplier.RowVersion, persisted, "Sin permiso"))).ErrorCode == "FORBIDDEN", "Permisos se revalidan también en subregistros");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'COMPRAS.VER')");
        var denied = false;
        try { await Read(); } catch (UnauthorizedAccessException) { denied = true; }
        Check(denied, "SQL deniega lectura tras revocar permiso de compras");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=1 WHERE IdRol=(SELECT IdRol FROM seg.Roles WHERE Nombre=N'Administrador') AND IdPermiso IN(SELECT IdPermiso FROM seg.Permisos WHERE Codigo IN(N'COMPRAS.VER',N'PROVEEDORES.GESTIONAR'))");
        Check(await service.ReadAsync(int.MaxValue) is null, "Proveedor inexistente devuelve ausencia explícita");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE Entidad=N'Proveedores' AND EntidadId=@id AND Accion=N'CREAR_PROVEEDOR'", new { id }) == 1,
            "Auditoría del alta se registra una sola vez");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables") == 77 && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.procedures") == 18,
            "Mantenimiento usa el esquema aprobado con las extensiones aditivas del MVP");
        Console.WriteLine($"{checks} comprobaciones de proveedores correctas.");
    }
}
