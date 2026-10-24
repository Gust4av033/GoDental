using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Data;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

internal static class ComplementsIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory, string root, string database)
    {
        var checks = 0;
        void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; Console.WriteLine("OK " + label); }
        await using var c = await factory.OpenConnectionAsync();
        var gateway = new SqlIdentityGateway(factory, new Pbkdf2PasswordHasher());
        var identity = (await gateway.CreateAdministratorAsync("complements_test", "Administrador de prueba aislada", Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), "COMPLEMENTS-TEST")).Value!;
        var user = identity.Id; var station = identity.WorkstationId;
        var session = new UserSession(); session.Begin(identity);
        var repo = new SqlMvpRepository(factory); var parameters = new CommercialParametersService(repo, session);
        ChangeCommercialParameters Change(CommercialParameters p, decimal j, decimal a) => new(Guid.NewGuid(), j, a, p.JustificationVersion, p.AuthorizationVersion, "Cambio comercial aislado");
        var initial = await parameters.ReadAsync();
        Check(initial.Justification == 7 && initial.Authorization == 35 && initial.JustificationVersion.Length == 8, "Lee umbrales sembrados con versiones");
        var invalid = Change(initial, 40, 35);
        Check((await repo.ChangeParametersAsync(invalid, user, station, default)).ErrorCode == "VALIDATION", "SQL rechaza el orden inválido sin depender de la UI");
        var first = Change(initial, 8, 40); var saved = await parameters.SaveAsync(first);
        Check(saved.IsSuccess, "Guarda ambos parámetros en una transacción");
        var changed = await parameters.ReadAsync();
        Check(changed.Justification == 8 && changed.Authorization == 40 && !changed.JustificationVersion.SequenceEqual(initial.JustificationVersion), "Valores y versión cambian juntos");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE IdBitacora=@Id AND Accion=N'EDITAR_PARAMETROS' AND DatosAntesJson LIKE '%7%' AND DatosDespuesJson LIKE '%40%' AND IdUsuario=@user;", new { saved.Value!.Id, user }) == 1, "Auditoría conserva motivo, autor y snapshots");
        Check((await parameters.SaveAsync(first)).Value == saved.Value && (await repo.ResolveOperationAsync(OperationIdentity.Describe(first), false, user, station, default)).Receipt == saved.Value, "Reenvío y recuperación devuelven mismo recibo sin volver a editar");
        Check((await parameters.SaveAsync(Change(initial, 9, 45))).ErrorCode == "CONCURRENCY_CONFLICT", "Versión antigua no sobrescribe valores");
        var a = Change(changed, 9, 45); var b = Change(changed, 10, 50);
        var racing = await Task.WhenAll(parameters.SaveAsync(a), parameters.SaveAsync(b));
        Check(racing.Count(r => r.IsSuccess) == 1 && racing.Count(r => r.ErrorCode == "CONCURRENCY_CONFLICT") == 1, "Dos estaciones editan: sólo una versión gana");
        var beforeRollback = await parameters.ReadAsync(); var rollback = Change(beforeRollback, 11, 55); var failed = false;
        try { await repo.ChangeParametersAsync(rollback, user, int.MaxValue, default); } catch (SqlException ex) when (ex.Number == 547) { failed = true; }
        var afterRollback = await parameters.ReadAsync();
        Check(failed && beforeRollback.JustificationVersion.SequenceEqual(afterRollback.JustificationVersion) && beforeRollback.AuthorizationVersion.SequenceEqual(afterRollback.AuthorizationVersion)
            && await repo.FindOperationAsync(rollback.OperationId, user, default) is null, "Fallo de auditoría revierte valores, versiones y ledger");
        var other = await c.ExecuteScalarAsync<int>("INSERT INTO seg.Usuarios(Usuario,NombreCompleto,PasswordHash,Activo,DebeCambiarClave) OUTPUT INSERTED.IdUsuario VALUES(N'complements_denied',N'Sin permisos',N'no-login',1,0);");
        Check((await repo.ChangeParametersAsync(Change(afterRollback, 11, 55), other, station, default)).ErrorCode == "FORBIDDEN", "SQL exige permiso a otra cuenta");
        var deniedRead = false; try { await repo.ReadParametersAsync(other, default); } catch (UnauthorizedAccessException) { deniedRead = true; }
        Check(deniedRead, "Consulta administrativa rechaza cuenta sin permiso");

        var products = new ProductService(new SqlProductRepository(factory), session);
        var catalogs = (await products.GetCatalogsAsync()).Value!;
        async Task<int> Product(string sku) => checked((int)(await products.SaveAsync(new() { Sku = sku, Name = "Producto " + sku, CategoryId = catalogs.Categories[0].Id, UnitId = (short)catalogs.Units[0].Id, Price = 100 })).Value);
        var product = await Product("COMPLEMENTO-01"); var secondProduct = await Product("COMPLEMENTO-02");
        var warehouse = await c.ExecuteScalarAsync<int>("SELECT TOP(1) IdAlmacen FROM inv.Almacenes WHERE Activo=1 ORDER BY IdAlmacen;");
        var setup = new InventorySetupService(repo, session); var sales = new SalesService(repo, session, gateway); var cash = new CashService(repo, session);
        Check((await setup.InitializeAsync(new(Guid.NewGuid(), product, warehouse, 20, 40, "Saldo inicial aislado"))).IsSuccess, "Prepara stock aislado");
        var box = checked((int)(await cash.SaveBoxAsync(new(Guid.NewGuid(), 0, "", "Caja complementos", warehouse, true, "Prueba aislada"))).Value!.Id);
        var shift = (await cash.OpenAsync(new(Guid.NewGuid(), box, 20, "Fondo aislado"))).Value!.Id;
        var codes = await setup.BarcodesAsync(product);
        var codeRequest = new BarcodeChange(Guid.NewGuid(), product, codes.ProductVersion, "4006381333931", "EAN13", true, "Código externo válido");
        Check((await setup.SaveBarcodeAsync(codeRequest)).IsSuccess, "Registra EAN13 válido");
        Check((await setup.SaveBarcodeAsync(codeRequest)).IsSuccess && (await setup.BarcodesAsync(product)).Items.Count == 1, "Reenviar código no duplica registro");
        Check((await setup.SaveBarcodeAsync(codeRequest with { OperationId = Guid.NewGuid(), Code = "OTRO", Type = "CODE128" })).ErrorCode == "CONCURRENCY_CONFLICT", "Versión de producto antigua protege códigos");
        Check((await setup.SaveBarcodeAsync(codeRequest with { OperationId = Guid.NewGuid(), ProductId = secondProduct, ExpectedVersion = (await setup.BarcodesAsync(secondProduct)).ProductVersion })).ErrorCode == "DUPLICATE", "Código global no se reasigna a otro producto");
        Check((await sales.SearchProductsAsync(box, codeRequest.Code!, new())).Items.Single().Id == product, "POS encuentra código externo activo");
        codes = await setup.BarcodesAsync(product);
        Check((await setup.SaveBarcodeAsync(codeRequest with { OperationId = Guid.NewGuid(), ExpectedVersion = codes.ProductVersion, Active = false })).IsSuccess, "Desactiva código sin eliminarlo");
        Check((await sales.SearchProductsAsync(box, codeRequest.Code!, new())).Total == 0 && !(await setup.BarcodesAsync(product)).Items.Single().Active, "Código inactivo conserva historial y no encuentra producto");
        codes = await setup.BarcodesAsync(product);
        Check((await setup.SaveBarcodeAsync(codeRequest with { OperationId = Guid.NewGuid(), ExpectedVersion = codes.ProductVersion })).IsSuccess, "Reactiva mismo código sin reasignar");
        codes = await setup.BarcodesAsync(product);
        Check((await setup.SaveBarcodeAsync(codeRequest with { OperationId = Guid.NewGuid(), ExpectedVersion = codes.ProductVersion, Type = "CODE128" })).ErrorCode == "INVALID_STATE", "No cambia el tipo de un código ya creado");
        Check((await setup.SaveBarcodeAsync(codeRequest with { OperationId = Guid.NewGuid(), ExpectedVersion = codes.ProductVersion, Code = "4006381333932" })).ErrorCode == "VALIDATION", "EAN13 con dígito inválido se rechaza");
        Check((await setup.SaveBarcodeAsync(new(Guid.NewGuid(), product, codes.ProductVersion, null, "CODE128", true, "Generar interno"))).IsSuccess
            && (await setup.BarcodesAsync(product)).Items.Any(x => x.Generated && x.Code == $"GD{product:D10}"), "Genera interno CODE128 desde la ficha");
        var forbiddenCode = new BarcodeChange(Guid.NewGuid(), product, (await setup.BarcodesAsync(product)).ProductVersion, "SIN-PERMISO", "CODE128", true, "Intento aislado");
        Check((await repo.SaveBarcodeAsync(forbiddenCode, other, station, default)).ErrorCode == "FORBIDDEN", "SQL exige permiso de mantenimiento de códigos");

        var cart = new SaleCart(box, null, [new(product, 1)]); var quote = await sales.QuoteAsync(cart);
        var currentParameters = await parameters.ReadAsync();
        Check((await parameters.SaveAsync(Change(currentParameters, 12, 60))).IsSuccess, "Cambia umbrales con carrito abierto");
        var methods = await sales.PaymentMethodsAsync(); var card = methods.Single(m => m.Code == "TARJETA"); var effective = methods.Single(m => m.Code == "EFECTIVO"); var credit = methods.Single(m => m.Code == "CREDITO");
        var oldQuote = new ConfirmSale(Guid.NewGuid(), cart, quote.Token, [new(card.Id, quote.Total, "EXTERNO")], 0, null, "Cálculo antiguo");
        Check((await sales.ConfirmAsync(oldQuote)).ErrorCode == "CONCURRENCY_CONFLICT", "Cambio comercial invalida token anterior");
        quote = await sales.QuoteAsync(cart);
        Check(quote.JustificationThreshold == 12 && quote.AuthorizationThreshold == 60, "Nuevo cálculo usa umbrales guardados");
        var missingReference = oldQuote with { OperationId = Guid.NewGuid(), QuoteToken = quote.Token, Payments = [new(card.Id, quote.Total)] };
        Check((await sales.ConfirmAsync(missingReference)).ErrorCode == "VALIDATION", "Tarjeta manual exige referencia al confirmar en SQL");
        var cashBefore = (await cash.CurrentAsync(box))!.Expected;
        var mixed = missingReference with { OperationId = Guid.NewGuid(), Payments = [new(card.Id, 30, "COMPROBANTE-EXTERNO"), new(effective.Id, quote.Total - 30)], CashTendered = quote.Total - 30 + 5, ShiftId = shift };
        var mixedSaved = await sales.ConfirmAsync(mixed);
        Check(mixedSaved.IsSuccess && (await cash.CurrentAsync(box))!.Expected == cashBefore + quote.Total - 30, "Mixto tarjeta/efectivo sólo aumenta efectivo aplicado");
        var receipt = await sales.ReadAsync(mixedSaved.Value!.Id);
        Check(receipt!.Payments.Any(x => x.MethodId == card.Id && x.Reference == "COMPROBANTE-EXTERNO") && receipt.Sale.Change == 5, "Comprobante conserva referencia manual y cambio sólo de efectivo");
        Check((await sales.ConfirmAsync(mixed)).Value == mixedSaved.Value && (await cash.CurrentAsync(box))!.Expected == cashBefore + quote.Total - 30, "Reenvío mixto no duplica cobro ni efectivo");
        quote = await sales.QuoteAsync(cart);
        var cardOnly = new ConfirmSale(Guid.NewGuid(), cart, quote.Token, [new(card.Id, quote.Total, "EXTERNO-SOLO")], 0, null, "Tarjeta manual");
        var beforeCard = (await cash.CurrentAsync(box))!.Expected;
        Check((await sales.ConfirmAsync(cardOnly)).IsSuccess && (await cash.CurrentAsync(box))!.Expected == beforeCard, "Tarjeta sola no requiere efectivo ni genera movimiento de efectivo");

        var customers = new CustomerService(new SqlCustomerRepository(factory), session);
        var type = checked((short)(await customers.CatalogsAsync()).Types.First().Id);
        var customerId = (await customers.SaveAsync(new(new() { Name = "Cliente de prueba aislada", TypeId = type }, "Alta aislada"))).Value;
        var customer = (await customers.ReadAsync(customerId))!;
        Check((await customers.SaveCreditAsync(new(customerId, customer.Customer.RowVersion, new() { Enabled = true, Limit = 1000, Days = 15, NoticeDays = 5 }, "Crédito de prueba"))).IsSuccess, "Prepara cuenta con crédito");
        var creditCart = cart with { CustomerId = customerId }; quote = await sales.QuoteAsync(creditCart);
        var creditSale = await sales.ConfirmAsync(new(Guid.NewGuid(), creditCart, quote.Token, [new(credit.Id, quote.Total)], 0, null, "Venta crédito aislada"));
        Check(creditSale.IsSuccess, "Confirma crédito para probar abono");
        var account = (await cash.AccountsAsync(false, new())).Items.Single();
        var pay = new AccountPayment(Guid.NewGuid(), account.Id, account.RowVersion, card.Id, 10, null, null, "Abono tarjeta manual");
        Check((await cash.PayAsync(pay)).ErrorCode == "VALIDATION", "Abono tarjeta también exige referencia");
        pay = pay with { OperationId = Guid.NewGuid(), Reference = "ABONO-EXTERNO" };
        var payResult = await cash.PayAsync(pay);
        Check(payResult.IsSuccess && (await cash.CurrentAsync(box))!.Expected == beforeCard && (await cash.AccountsAsync(false, new())).Items.Single().Balance == account.Balance - 10, "Abono tarjeta reduce deuda sin alterar efectivo");
        Check((await cash.PayAsync(pay)).Value == payResult.Value && (await cash.AccountsAsync(false, new())).Items.Single().Balance == account.Balance - 10, "Reenvío de abono no reduce deuda dos veces");

        var beforeRevocation = await parameters.ReadAsync();
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'AJUSTES.EDITAR');");
        var beforeRevoked = await repo.ResolveOperationAsync(OperationIdentity.Describe(first), false, user, station, default);
        Check(beforeRevoked.Receipt == saved.Value && (await parameters.SaveAsync(Change(beforeRevocation, 1, 2))).ErrorCode == "FORBIDDEN", "SQL revoca nuevas ediciones conservando recuperación");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=1 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'AJUSTES.EDITAR');");
        var beforeInstall = await parameters.ReadAsync();
        await DevelopmentDatabase.InstallAsync(database, Path.Combine(root, "database"));
        var reinstalled = await parameters.ReadAsync();
        Check(reinstalled.Justification == beforeInstall.Justification && reinstalled.Authorization == beforeInstall.Authorization && reinstalled.JustificationVersion.SequenceEqual(beforeInstall.JustificationVersion), "Reaplicar 016 conserva parámetros y versiones");
        Console.WriteLine($"{checks} comprobaciones de complementos SQL correctas.");
    }
}
