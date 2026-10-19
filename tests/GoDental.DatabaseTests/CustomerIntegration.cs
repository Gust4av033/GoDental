using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

internal static class CustomerIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory)
    {
        var count = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); count++; Console.WriteLine("OK " + message); }
        await using var c = await factory.OpenConnectionAsync();
        var gateway = new SqlIdentityGateway(factory, new Pbkdf2PasswordHasher());
        var identity = (await gateway.CreateAdministratorAsync("clientes_test", "Administrador clientes", Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), "CUSTOMER-TEST")).Value!;
        var session = new UserSession(); session.Begin(identity);
        var repo = new SqlCustomerRepository(factory); var service = new CustomerService(repo, session);
        var catalogs = await service.CatalogsAsync(); var type = checked((short)catalogs.Types.First().Id);
        var result = await service.SaveAsync(new(new() { Code = " CLI-001 ", Name = " Cliente aislado ", TypeId = type, CommercialName = " ", Nit = "NIT-TEST", Nrc = "NRC-TEST" }, "Alta aislada"));
        Check(result.IsSuccess, "Crea cliente con auditoría usando esquema aprobado"); var id = result.Value;
        async Task<CustomerSnapshot> Read() => (await service.ReadAsync(id))!;
        var snapshot = await Read();
        Check(snapshot.Customer.Code == "CLI-001" && snapshot.Customer.CommercialName is null && !snapshot.Credit.Enabled && snapshot.Customer.RowVersion.Length == 8, "Normaliza ficha y conserva crédito deshabilitado en alta");
        Check((await service.SearchAsync(new("aislado"))).Total == 1 && (await service.SearchAsync(new("%"))).Total == 0 && (await service.SearchAsync(new("' OR 1=1 --"))).Total == 0, "Busca literalmente y con parámetros");
        Check((await service.SaveAsync(new(new() { Code = "CLI-001", Name = "Duplicado", TypeId = type }, "Duplicado"))).ErrorCode == "DUPLICATE_IDENTITY", "Rechaza código duplicado");
        Check((await service.SaveAsync(new(new() { Code = "CLI-002", Name = "Duplicado fiscal", TypeId = type, Nit = "NIT-TEST" }, "Duplicado"))).ErrorCode == "DUPLICATE_IDENTITY", "Rechaza NIT duplicado");
        var original = snapshot.Customer;
        Check((await service.SaveContactAsync(new(id, original.RowVersion, new() { Name = "Contacto", Phone = "00000000", WhatsApp = "11111111", Primary = true }, "Alta contacto"))).IsSuccess, "Crea contacto individual");
        snapshot = await Read();
        Check(snapshot.Contacts.Count == 1 && !snapshot.Customer.RowVersion.SequenceEqual(original.RowVersion), "Contacto avanza versión del cliente");
        Check((await service.SaveAsync(new(original with { Name = "Obsoleto" }, "Concurrencia"))).ErrorCode == "CONCURRENCY_CONFLICT", "Ficha obsoleta no sobrescribe subregistros");
        var contact = snapshot.Contacts.Single(); var request = new CustomerContactChange(id, snapshot.Customer.RowVersion, contact with { Phone = "123" }, "Dos estaciones");
        var concurrent = await Task.WhenAll(service.SaveContactAsync(request), service.SaveContactAsync(request with { Contact = contact with { Phone = "456" } }));
        Check(concurrent.Count(r => r.IsSuccess) == 1 && concurrent.Count(r => r.ErrorCode == "CONCURRENCY_CONFLICT") == 1, "Dos estaciones editan sin pérdida de cambios");
        foreach (var addressType in new[] { "FISCAL", "ENTREGA", "OTRA" })
        {
            snapshot = await Read();
            Check((await service.SaveAddressAsync(new(id, snapshot.Customer.RowVersion, new() { Type = addressType, Address = "Calle de prueba", Department = "Departamento", Municipality = "Municipio", Reference = "Referencia", Primary = true }, "Alta dirección"))).IsSuccess, "Conserva dirección " + addressType);
        }
        snapshot = await Read();
        Check(snapshot.Addresses.Count == 3 && snapshot.Contacts.Count == 1, "No reemplaza contactos ni otras direcciones");
        Check((await service.SaveAddressAsync(new(id, snapshot.Customer.RowVersion, snapshot.Addresses.First() with { Id = long.MaxValue }, "Ajena"))).ErrorCode == "VALIDATION", "Rechaza dirección ajena o inexistente");
        Check((await service.SaveContactAsync(new(id, snapshot.Customer.RowVersion, contact with { Id = long.MaxValue }, "Ajeno"))).ErrorCode == "VALIDATION", "Rechaza contacto ajeno o inexistente");
        var version = snapshot.Customer.RowVersion;
        Check((await service.SaveCreditAsync(new(id, version, new() { Enabled = true, Limit = 250.15m, Days = 20, NoticeDays = 5, Blocked = true, BlockReason = "Prueba manual" }, "Configurar crédito"))).IsSuccess, "Crédito se configura mediante contrato separado");
        snapshot = await Read();
        Check(snapshot.Credit.Limit == 250.15m && snapshot.Credit.Blocked && !snapshot.Customer.RowVersion.SequenceEqual(version), "Crédito conserva precisión y avanza versión");
        Check((await service.SaveAsync(new(snapshot.Customer with { Phone = "789" }, "Cambiar teléfono"))).IsSuccess && (await Read()).Credit.Limit == 250.15m, "Editar ficha no sobrescribe configuración de crédito");
        snapshot = await Read();
        var failed = false;
        try { await repo.SaveAddressAsync(new(id, snapshot.Customer.RowVersion, new() { Address = "No persistir" }, "Fallo auditoría"), identity.Id, int.MaxValue); }
        catch (SqlException ex) when (ex.Number == 547) { failed = true; }
        var after = await Read();
        Check(failed && after.Addresses.Count == 3 && after.Customer.RowVersion.SequenceEqual(snapshot.Customer.RowVersion), "Fallo de auditoría revierte dirección y versión");
        Check((await service.SaveAsync(new(after.Customer with { Active = false }, "Baja lógica"))).IsSuccess, "Desactiva cliente sin eliminarlo");
        snapshot = await Read();
        Check(snapshot.Addresses.Count == 3 && snapshot.Contacts.Count == 1 && (await service.SearchAsync(new())).Total == 0 && (await service.SearchAsync(new(IncludeInactive: true))).Total == 1, "Baja conserva historial y filtros de inactivos");
        Check((await service.SaveContactAsync(new(id, snapshot.Customer.RowVersion, contact, "Cliente inactivo"))).ErrorCode == "INVALID_STATE", "Inactivo no admite cambios de subregistros");
        Check((await service.SaveAsync(new(snapshot.Customer with { Active = true }, "Reactivar"))).IsSuccess, "Reactiva cliente");
        // Fixtures exclusivamente en la base aislada; no son operaciones del producto.
        var warehouse = await c.ExecuteScalarAsync<int>("SELECT TOP(1) IdAlmacen FROM inv.Almacenes ORDER BY IdAlmacen");
        var caja = await c.ExecuteScalarAsync<int>("INSERT INTO ven.Cajas(Codigo,Nombre,IdAlmacen) OUTPUT INSERTED.IdCaja VALUES(N'CLI-CAJA',N'Caja de prueba',@warehouse);", new { warehouse });
        var sale = await c.ExecuteScalarAsync<long>("INSERT INTO ven.Ventas(NumeroVenta,IdCliente,IdCaja,IdVendedor,IdEquipo,Estado,Total,SaldoPendiente) OUTPUT INSERTED.IdVenta VALUES(N'CLI-VENTA',@id,@caja,@u,@w,N'CONFIRMADA',100,10);", new { id, caja, u = identity.Id, w = identity.WorkstationId });
        var account = await c.ExecuteScalarAsync<long>("INSERT INTO ven.CuentasPorCobrar(IdVenta,IdCliente,FechaEmision,FechaVencimiento,MontoOriginal,Saldo,Estado) OUTPUT INSERTED.IdCuenta VALUES(@sale,@id,DATEADD(DAY,-10,CONVERT(date,SYSDATETIMEOFFSET())),DATEADD(DAY,-1,CONVERT(date,SYSDATETIMEOFFSET())),100,10,N'PARCIAL');", new { sale, id });
        await c.ExecuteAsync("INSERT INTO ven.AbonosCliente(IdCuenta,IdMetodoPago,Monto,RegistradoPor,NumeroRecibo) SELECT @account,IdMetodoPago,90,@u,N'CLI-RECIBO' FROM cat.MetodosPago WHERE Codigo=N'EFECTIVO';", new { account, u = identity.Id });
        Check((await service.SalesAsync(new(id))).Items.Single().Total == 100 && (await service.AccountsAsync(new(id))).Items.Single().Balance == 10 && (await service.PaymentsAsync(new(id))).Items.Single().Amount == 90, "Historial y estado de cuenta consultan registros reales de prueba");
        var balance = await service.BalanceAsync(id);
        Check(balance!.Outstanding == 10 && balance.Overdue == 10 && balance.Available == 240.15m, "Resumen sigue estados y límite de sp_ConfirmarVenta");
        snapshot = await Read();
        Check((await service.SaveAsync(new(snapshot.Customer with { Code = "CAMBIADO" }, "Código con historial"))).ErrorCode == "IN_USE", "Conserva código con historial comercial");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'CLIENTES.CREDITO_CONFIGURAR')");
        Check((await service.SaveCreditAsync(new(id, snapshot.Customer.RowVersion, new(), "Sin permiso real"))).ErrorCode == "FORBIDDEN", "Servidor revalida permiso de crédito pese a sesión cacheada");
        Check((await service.SaveAsync(new(snapshot.Customer with { Phone = "456" }, "Ficha independiente"))).IsSuccess && (await Read()).Credit.Limit == 250.15m, "Permiso de ficha no concede crédito ni lo sobrescribe");
        snapshot = await Read();
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'CLIENTES.EDITAR')");
        Check((await service.SaveContactAsync(new(id, snapshot.Customer.RowVersion, contact, "Sin permiso"))).ErrorCode == "FORBIDDEN" && (await service.SaveAddressAsync(new(id, snapshot.Customer.RowVersion, snapshot.Addresses.First(), "Sin permiso"))).ErrorCode == "FORBIDDEN", "SQL revalida edición de subregistros");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'VENTAS.VER')");
        var denied = false; try { await service.SalesAsync(new(id)); } catch (UnauthorizedAccessException) { denied = true; }
        Check(denied && await service.ReadAsync(id) is not null, "Revocar ventas impide historial sin impedir la ficha");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'CLIENTES.VER')");
        denied = false; try { await Read(); } catch (UnauthorizedAccessException) { denied = true; }
        Check(denied, "SQL revalida lectura de clientes");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE Entidad=N'Clientes' AND EntidadId=CONVERT(nvarchar(100),@id) AND Accion=N'CREAR_CLIENTE'", new { id }) == 1, "Auditoría registra alta una sola vez");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables") == 72 && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.procedures") == 17, "No añade tablas ni procedimientos ni modifica baseline");
        Console.WriteLine($"{count} comprobaciones de clientes correctas.");
    }
}
