using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

internal static class MvpIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory, string root, string database)
    {
        var checks = 0;
        void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; Console.WriteLine("OK " + label); }
        long Saved(Result<MvpReceipt> result)
        {
            Check(result.IsSuccess, "Operación confirmada: " + (result.ErrorCode ?? "OK") + " " + result.Message);
            return result.Value!.Id;
        }
        await using var c = await factory.OpenConnectionAsync();
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var gateway = new SqlIdentityGateway(factory, new Pbkdf2PasswordHasher());
        var identity = (await gateway.CreateAdministratorAsync("mvp_test", "Administrador MVP aislado", password, "MVP-TEST")).Value!;
        var session = new UserSession(); session.Begin(identity);
        var repository = new SqlMvpRepository(factory);
        var sales = new SalesService(repository, session, gateway);
        var cash = new CashService(repository, session);
        var orders = new OrderService(repository, session);
        var setup = new InventorySetupService(repository, session);
        var customers = new CustomerService(new SqlCustomerRepository(factory), session);
        var inventory = new InventoryService(new SqlInventoryRepository(factory), session);
        var products = new ProductService(new SqlProductRepository(factory), session);
        var catalogs = (await products.GetCatalogsAsync()).Value!;
        var warehouse = (await inventory.WarehousesAsync()).First().Id;
        var today = await c.ExecuteScalarAsync<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
        async Task<int> Product(string sku, decimal price, bool lots = false)
        {
            var result = await products.SaveAsync(new() { Sku = sku, Name = "Producto " + sku, CategoryId = catalogs.Categories[0].Id, UnitId = (short)catalogs.Units[0].Id, Price = price, TracksLot = lots, TracksExpiry = lots });
            Check(result.IsSuccess, "Crea producto de escenario " + sku); return checked((int)result.Value);
        }
        var p = await Product("MVP-001", 100);
        var request = new InitialInventory(Guid.NewGuid(), p, warehouse, 50, 40, "Carga inicial aislada");
        var duplicateInitial = await Task.WhenAll(setup.InitializeAsync(request), setup.InitializeAsync(request));
        Check(duplicateInitial.All(r => r.IsSuccess) && duplicateInitial.Select(r => r.Value!.Id).Distinct().Count() == 1, "Dos reenvíos crean un solo saldo inicial y movimiento");
        Check((await setup.InitializeAsync(request with { OperationId = Guid.NewGuid() })).ErrorCode == "CONCURRENCY_CONFLICT", "Otro GUID no permite duplicar saldo inicial");
        Check((await setup.InitializeAsync(request with { Quantity = 51 })).ErrorCode == "CONCURRENCY_CONFLICT", "GUID utilizado rechaza contenido distinto");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse;", new { p, warehouse }) == 50, "Reenvíos no duplican stock");

        var product = (await products.SearchAsync(new("MVP-001"))).Value!.Items.Single();
        Saved(await setup.SaveBarcodeAsync(new(Guid.NewGuid(), p, product.RowVersion, null, "CODE128", true, "Código interno")));
        var box = checked((int)Saved(await cash.SaveBoxAsync(new(Guid.NewGuid(), 0, "MVP-CAJA", "Caja MVP", warehouse, true, "Alta de caja"))));
        Check((await sales.SearchProductsAsync(box, $"GD{p:D10}", new())).Items.Single().Id == p, "POS encuentra producto por código interno exacto sin exponer costos");
        var open = new OpenCashShift(Guid.NewGuid(), box, 20, "Fondo inicial");
        var shiftId = Saved(await cash.OpenAsync(open));
        Check(Saved(await cash.OpenAsync(open)) == shiftId && !(await cash.OpenAsync(open with { OperationId = Guid.NewGuid() })).IsSuccess, "Apertura idempotente y un solo turno abierto");
        var methods = await sales.PaymentMethodsAsync();
        var effective = methods.Single(m => m.Code == "EFECTIVO").Id;
        var credit = methods.Single(m => m.Code == "CREDITO").Id;
        var transfer = methods.Single(m => m.Code == "TRANSFERENCIA").Id;
        Check(methods.All(m => m.Code != "TARJETA"), "No anuncia cobro con tarjeta sin integración");
        var type = checked((short)(await customers.CatalogsAsync()).Types.First().Id);
        var createdCustomer = await customers.SaveAsync(new(new() { Code = "MVP-CLIENTE", Name = "Cliente aislado", TypeId = type, Nit = "MVP-NIT", Nrc = "MVP-NRC" }, "Alta cliente"));
        Check(createdCustomer.IsSuccess, "Crea cliente para ventas y entregas"); var customerId = createdCustomer.Value;
        var customer = (await customers.ReadAsync(customerId))!;
        Check((await customers.SaveCreditAsync(new(customerId, customer.Customer.RowVersion, new() { Enabled = true, Limit = 1000, Days = 15, NoticeDays = 5 }, "Crédito de prueba"))).IsSuccess, "Habilita crédito mediante contrato administrativo");
        customer = (await customers.ReadAsync(customerId))!;
        Check((await customers.SaveAddressAsync(new(customerId, customer.Customer.RowVersion, new() { Type = "ENTREGA", Address = "Dirección original", Reference = "Portón" }, "Dirección de entrega"))).IsSuccess, "Registra dirección de entrega");
        customer = (await customers.ReadAsync(customerId))!;
        var address = customer.Addresses.Single();

        SaleCart Cart(decimal quantity = 1, decimal delivery = 0) => new(box, customerId, [new(p, quantity)], DeliveryCharge: delivery);
        async Task<ConfirmSale> CashSale(SaleCart cart, decimal extra = 0)
        {
            var q = await sales.QuoteAsync(cart);
            return new(Guid.NewGuid(), q.Cart, q.Token, [new(effective, q.Total)], q.Total + extra, shiftId, "Venta aislada");
        }
        var cart = Cart(delivery: 5);
        var quote = await sales.QuoteAsync(cart);
        Check(quote.Total == 105 && quote.Subtotal == 100, "Cotizador separa cargo de entrega del neto");
        var quotationId = Saved(await sales.SaveQuotationAsync(new(Guid.NewGuid(), quote.Cart, quote.Token, today.AddDays(1), "Oferta aislada")));
        Check((await sales.QuotationsAsync(new())).Total == 1 && await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse;", new { p, warehouse }) == 50, "Cotizar conserva stock y permite listar y recuperar");
        var oldShift = (await cash.CurrentAsync(box))!;
        var saleRequest = (await CashSale(cart, 15)) with { QuotationId = quotationId };
        var confirmations = await Task.WhenAll(sales.ConfirmAsync(saleRequest), sales.ConfirmAsync(saleRequest));
        Check(confirmations.All(r => r.IsSuccess) && confirmations.Select(r => r.Value!.Id).Distinct().Count() == 1, "Confirmaciones concurrentes con mismo GUID devuelven una venta");
        var saleId = confirmations[0].Value!.Id; var sale = (await sales.ReadAsync(saleId))!;
        Check(sale.Sale.State == "CONFIRMADA" && sale.Sale.Total == 105 && sale.Sale.CashTendered == 120 && sale.Sale.Change == 15 && sale.Lines.Single().PricingTrace is not null, "Venta conserva importes, cambio y explicación del cálculo");
        Check((await cash.CurrentAsync(box))!.Expected == 125, "Caja recibe neto aplicado de 105, sin sumar el cambio");
        Check((await cash.CloseAsync(new(Guid.NewGuid(), shiftId, oldShift.RowVersion, 125, "Cierre obsoleto"))).ErrorCode == "CONCURRENCY_CONFLICT", "Cobro invalida arqueo leído antes de vender");
        Check((await sales.ReadQuotationAsync(quotationId))!.State == "CONVERTIDA", "Conversión marca la cotización una sola vez");
        var secondConversion = (await CashSale(cart)) with { QuotationId = quotationId };
        Check(!(await sales.ConfirmAsync(secondConversion)).IsSuccess, "Cotización convertida no vuelve a facturar");
        Check((await new MvpOperationService(repository, session).FindAsync(saleRequest.OperationId))?.Id == saleId, "Consulta de resultado resuelve confirmación incierta");
        Check((await sales.ConfirmAsync(saleRequest with { Reason = "Otra solicitud" })).ErrorCode == "CONCURRENCY_CONFLICT", "Reutilizar GUID no altera venta confirmada");

        var stale = await CashSale(Cart());
        product = (await products.SearchAsync(new("MVP-001"))).Value!.Items.Single(); product.Price = 101;
        Check((await products.SaveAsync(product)).IsSuccess, "Actualiza precio por contrato de catálogo");
        Check((await sales.ConfirmAsync(stale)).ErrorCode == "CONCURRENCY_CONFLICT", "Un precio cambiado exige aceptar un cálculo nuevo");
        var invalidPayment = (await CashSale(Cart())) with { Payments = [new(effective, 100)] };
        Check((await sales.ConfirmAsync(invalidPayment)).ErrorCode == "VALIDATION", "No admite diferencia de un dólar entre pago y total");
        var noReference = await sales.QuoteAsync(Cart());
        Check(!(await sales.ConfirmAsync(new(Guid.NewGuid(), noReference.Cart, noReference.Token, [new(transfer, noReference.Total)], 0, null, "Transferencia sin referencia"))).IsSuccess, "Transferencia requiere referencia real");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.Ventas;") == 1, "Errores previos no dejan ventas borrador ni pagos parciales");

        var listId = await c.ExecuteScalarAsync<int>("INSERT INTO ven.ListasPrecio(Codigo,Nombre,IdTipoCliente,Prioridad,MontoMinimoVenta) OUTPUT INSERTED.IdListaPrecio VALUES(N'MVP-LISTA',N'Lista prueba',@type,99,0);", new { type });
        await c.ExecuteAsync("INSERT INTO ven.ListaPrecioProductos(IdListaPrecio,IdProducto,CantidadMinima,Precio) VALUES(@listId,@p,1,90),(@listId,@p,3,80);", new { listId, p });
        customer = (await customers.ReadAsync(customerId))!;
        Check((await customers.SaveAsync(new(customer.Customer with { PriceListId = listId }, "Asignar lista"))).IsSuccess, "Cliente conserva lista comercial explícita");
        Check((await sales.QuoteAsync(Cart())).Total == 90 && (await sales.QuoteAsync(Cart(3))).Total == 240, "Lista SQL aplica escalón de cantidad sobre producto completo");
        await c.ExecuteAsync("UPDATE ven.ListasPrecio SET MontoMinimoVenta=250 WHERE IdListaPrecio=@listId;", new { listId });
        Check((await sales.QuoteAsync(Cart())).Total == 101 && (await sales.QuoteAsync(Cart(3))).Total == 240, "Mínimo de compra compara subtotal público antes de beneficios");
        await c.ExecuteAsync("UPDATE ven.ListasPrecio SET MontoMinimoVenta=0 WHERE IdListaPrecio=@listId;", new { listId });
        var promotionId = await c.ExecuteScalarAsync<long>("INSERT INTO ven.Promociones(Nombre,FechaInicio,FechaFin,Tipo,Valor,Prioridad) OUTPUT INSERTED.IdPromocion VALUES(N'Promoción prueba',DATEADD(day,-1,SYSDATETIMEOFFSET()),DATEADD(day,1,SYSDATETIMEOFFSET()),N'PORCENTAJE',10,1);");
        await c.ExecuteAsync("INSERT INTO ven.PromocionDetalle(IdPromocion,IdProducto) VALUES(@promotionId,@p);", new { promotionId, p });
        Check((await sales.QuoteAsync(Cart())).Total == 90, "Promoción no acumulable compara contra precio público y conserva mejor lista");
        await c.ExecuteAsync("UPDATE ven.Promociones SET Acumulable=1 WHERE IdPromocion=@promotionId;", new { promotionId });
        Check((await sales.QuoteAsync(Cart())).Total == 81, "Promoción acumulable se aplica al precio de cliente");
        await c.ExecuteAsync("UPDATE ven.Promociones SET Tipo=N'COMBO' WHERE IdPromocion=@promotionId;", new { promotionId });
        var comboDenied = false; try { await sales.QuoteAsync(Cart()); } catch (ArgumentException) { comboDenied = true; }
        Check(comboDenied, "Promoción no soportada falla sin cobrar un precio alternativo silenciosamente");
        await c.ExecuteAsync("UPDATE ven.Promociones SET Activo=0 WHERE IdPromocion=@promotionId;UPDATE ven.ListasPrecio SET Activo=0 WHERE IdListaPrecio=@listId;", new { promotionId, listId });
        customer = (await customers.ReadAsync(customerId))!;
        Check((await customers.SaveAsync(new(customer.Customer with { PriceListId = null }, "Retirar lista de fixture"))).IsSuccess, "Restaura precio público para los escenarios siguientes");

        // Dos solicitudes distintas compiten por la última unidad, aceptada por ambas previamente.
        var scarce = await Product("MVP-ULTIMO", 10);
        Saved(await setup.InitializeAsync(new(Guid.NewGuid(), scarce, warehouse, 1, 3, "Última unidad")));
        var lastCart = new SaleCart(box, customerId, [new(scarce, 1)]);
        var lastQuote = await sales.QuoteAsync(lastCart);
        ConfirmSale Last() => new(Guid.NewGuid(), lastQuote.Cart, lastQuote.Token, [new(transfer, 10, "REF-PRUEBA")], 0, null, "Última unidad concurrente");
        var lastResults = await Task.WhenAll(sales.ConfirmAsync(Last()), sales.ConfirmAsync(Last()));
        Check(lastResults.Count(r => r.IsSuccess) == 1 && lastResults.Count(r => !r.IsSuccess) == 1, "Dos estaciones no venden dos veces la última unidad");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@scarce AND IdAlmacen=@warehouse;", new { scarce, warehouse }) == 0, "Stock final no es negativo");

        var creditQuote = await sales.QuoteAsync(Cart());
        var creditSale = Saved(await sales.ConfirmAsync(new(Guid.NewGuid(), creditQuote.Cart, creditQuote.Token, [new(effective, 21), new(credit, 80)], 30, shiftId, "Venta mixta crédito")));
        var creditNumber = (await sales.ReadAsync(creditSale))!.Sale.Number;
        var account = (await cash.AccountsAsync(false, new())).Items.Single(a => a.Sale == creditNumber);
        Check(account.Balance == 80 && account.RowVersion.Length == 8, "Venta mixta genera CxC por saldo financiado");
        var payment = new AccountPayment(Guid.NewGuid(), account.Id, account.RowVersion, effective, 30, null, shiftId, "Abono parcial");
        var payments = await Task.WhenAll(cash.PayAsync(payment), cash.PayAsync(payment));
        Check(payments.All(r => r.IsSuccess) && payments[0].Value!.Id == payments[1].Value!.Id, "Reenvíos de abono registran un único ingreso");
        Check((await cash.PayAsync(payment with { OperationId = Guid.NewGuid() })).ErrorCode == "CONCURRENCY_CONFLICT", "Abono obsoleto no pierde el saldo actualizado");
        account = (await cash.AccountsAsync(false, new())).Items.Single(a => a.Id == account.Id);
        Check(account.Balance == 50 && (await sales.ReadAsync(creditSale))!.Sale.Pending == 50, "Abono actualiza CxC y venta juntos");
        Check(!(await cash.PayAsync(payment with { OperationId = Guid.NewGuid(), ExpectedVersion = account.RowVersion, Amount = 51 })).IsSuccess, "No admite sobrepago");
        Saved(await cash.PayAsync(payment with { OperationId = Guid.NewGuid(), ExpectedVersion = account.RowVersion, Amount = 50, MethodId = transfer, Reference = "ABONO-TRANSFERENCIA", ShiftId = null }));
        Check((await sales.ReadAsync(creditSale))!.Sale.Pending == 0, "Abono final liquida venta sin agregar transferencia a efectivo");
        await c.ExecuteAsync("UPDATE cli.Clientes SET BloqueadoCredito=1,MotivoBloqueoCredito=N'Bloqueo de prueba' WHERE IdCliente=@customerId;", new { customerId });
        creditQuote = await sales.QuoteAsync(Cart());
        Check(!(await sales.ConfirmAsync(new(Guid.NewGuid(), creditQuote.Cart, creditQuote.Token, [new(credit, creditQuote.Total)], 0, null, "Crédito bloqueado"))).IsSuccess, "Servidor rechaza crédito bloqueado aunque cliente esté activo");
        await c.ExecuteAsync("UPDATE cli.Clientes SET BloqueadoCredito=0,MotivoBloqueoCredito=NULL WHERE IdCliente=@customerId;", new { customerId });

        customer = (await customers.ReadAsync(customerId))!;
        Check((await customers.SaveCreditAsync(new(customerId, customer.Customer.RowVersion, customer.Credit with { Limit = 150 }, "Límite para concurrencia"))).IsSuccess, "Fija crédito disponible para dos estaciones");
        var creditRequests = new List<ConfirmSale>();
        foreach (var sku in new[] { "MVP-LIMITE-A", "MVP-LIMITE-B" })
        {
            var creditProduct = await Product(sku, 100);
            Saved(await setup.InitializeAsync(new(Guid.NewGuid(), creditProduct, warehouse, 1, 20, "Stock crédito concurrente")));
            var creditCartQuote = await sales.QuoteAsync(new(box, customerId, [new(creditProduct, 1)]));
            creditRequests.Add(new(Guid.NewGuid(), creditCartQuote.Cart, creditCartQuote.Token, [new(credit, 100)], 0, null, "Crédito concurrente"));
        }
        var competingCredit = await Task.WhenAll(creditRequests.Select(r => sales.ConfirmAsync(r)));
        Check(competingCredit.Count(r => r.IsSuccess) == 1 && competingCredit.Count(r => !r.IsSuccess) == 1 && (await customers.BalanceAsync(customerId))!.Outstanding == 100, "Dos productos distintos no permiten exceder el límite de crédito concurrentemente");
        customer = (await customers.ReadAsync(customerId))!;
        Check((await customers.SaveCreditAsync(new(customerId, customer.Customer.RowVersion, customer.Credit with { Limit = 1000 }, "Restaurar límite"))).IsSuccess, "Restaura configuración para escenarios siguientes");

        // Descuento efectivo apenas mayor a 35: no se redondea para eludir reautenticación.
        var discounted = Cart() with { Items = [new(p, 1, 30)], GlobalDiscountPercent = 7.1429m };
        var discountQuote = await sales.QuoteAsync(discounted);
        var highSale = new ConfirmSale(Guid.NewGuid(), discountQuote.Cart, discountQuote.Token, [new(effective, discountQuote.Total)], discountQuote.Total, shiftId, "Descuento autorizado");
        Check((await sales.ConfirmAsync(highSale)).ErrorCode == "FORBIDDEN", "Descuento efectivo mayor a 35 exige autorización aun junto al umbral");
        Check((await sales.SaveQuotationAsync(new(Guid.NewGuid(), discountQuote.Cart, discountQuote.Token, today.AddDays(1), "Cotización sin autorización"))).ErrorCode == "FORBIDDEN", "Cotización emitida tampoco puede eludir autorización administrativa");
        var authorizationId = Saved(await sales.AuthorizeDiscountAsync(new(Guid.NewGuid(), discountQuote.Cart, discountQuote.Token, "Autorización de prueba"), identity.UserName, password));
        Saved(await sales.ConfirmAsync(highSale with { DiscountAuthorizationId = authorizationId }));
        Check(await c.ExecuteScalarAsync<bool>("SELECT UtilizadaMvp FROM sis.AutorizacionesOperacion WHERE IdAutorizacion=@authorizationId;", new { authorizationId }), "Autorización administrativa se consume en la transacción de venta");

        // Lotes FEFO: dos líneas, impuesto incluido, fechas controladas por SQL.
        var tracked = await Product("MVP-LOTES", 113, true);
        Saved(await setup.InitializeAsync(new(Guid.NewGuid(), tracked, warehouse, 1, 30, "Lote próximo", "PRIMERO", today.AddDays(1))));
        var stock = (await inventory.ReadAsync(tracked, warehouse)).Stocks.Single(s => s.WarehouseId == warehouse);
        Check((await inventory.AdjustAsync(new(tracked, warehouse, 2, 30, "AJUSTE", "Segundo lote", stock.RowVersion, NewLotNumber: "SEGUNDO", Expires: today.AddDays(20)))).IsSuccess, "Agrega segundo lote mediante ajuste trazable");
        await c.ExecuteAsync("INSERT INTO cat.Impuestos(Codigo,Nombre,Tasa,IncluidoEnPrecio) VALUES(N'MVP-IVA',N'Impuesto prueba',13,1);INSERT INTO inv.ProductoImpuestos(IdProducto,IdImpuesto) SELECT @tracked,IdImpuesto FROM cat.Impuestos WHERE Codigo=N'MVP-IVA';", new { tracked });
        var lotCart = new SaleCart(box, customerId, [new(tracked, 2)]);
        var lotQuote = await sales.QuoteAsync(lotCart);
        Check(lotQuote.Lines.Count == 2 && lotQuote.Subtotal == 200 && lotQuote.Tax == 26 && lotQuote.Total == 226 && lotQuote.Lines[0].Lots.Single().Quantity == 1, "FEFO reparte líneas y conserva impuesto incluido exacto");
        var lotSale = Saved(await sales.ConfirmAsync(new(Guid.NewGuid(), lotQuote.Cart, lotQuote.Token, [new(transfer, lotQuote.Total, "LOTES")], 0, null, "Venta FEFO")));
        Check((await sales.ReadAsync(lotSale))!.Sale.Subtotal == 200, "Encabezado conserva neto exacto recalculado por lotes");
        var remainingLot = (await inventory.ReadAsync(tracked, warehouse)).Lots.Single(l => l.Number == "SEGUNDO");
        await c.ExecuteAsync("UPDATE inv.LotesProducto SET FechaVencimiento=DATEADD(day,-1,@today) WHERE IdLote=@id;", new { today, id = remainingLot.Id });
        var expiredDenied = false; try { await sales.QuoteAsync(new(box, customerId, [new(tracked, 1)])); } catch (ArgumentException) { expiredDenied = true; }
        Check(expiredDenied, "Un lote vencido no se ofrece aunque haya saldo agregado");

        var placed = new PlaceOrder(Guid.NewGuid(), customerId, "Contacto", "00000000", "Ignorada por selección de dirección", null, null, null, today.AddDays(1), "Repartidor", null, 5, [new(p, 1)], "Pedido aislado", address.Id);
        var orderId = Saved(await orders.PlaceAsync(placed));
        var order = (await orders.ReadAsync(orderId))!;
        customer = (await customers.ReadAsync(customerId))!;
        Check((await customers.SaveAddressAsync(new(customerId, customer.Customer.RowVersion, address with { Address = "Dirección nueva" }, "Cambio de domicilio"))).IsSuccess, "Edita dirección de cliente después de crear pedido");
        Check((await orders.ReadAsync(orderId))!.Order.Address == "Dirección original", "Pedido conserva snapshot de entrega");
        Check(!(await orders.TransitionAsync(new(Guid.NewGuid(), orderId, order.Order.RowVersion, "ENTREGADO", "Salto inválido"))).IsSuccess, "Pedido recibido no pasa directamente a entregado");
        Saved(await orders.LinkSaleAsync(new(Guid.NewGuid(), orderId, order.Order.RowVersion, saleId, "Vincular venta correspondiente")));
        Check((await sales.ReadAsync(saleId))!.Delivery is { Id: var linkedOrder, Address: "Dirección original" } && linkedOrder == orderId,
            "Comprobante lee la entrega del pedido sin sustituirla por la dirección actual del cliente");
        Check((await orders.TransitionAsync(new(Guid.NewGuid(), orderId, order.Order.RowVersion, "PREPARANDO", "Versión obsoleta"))).ErrorCode == "CONCURRENCY_CONFLICT", "Vincular venta invalida versión anterior del pedido");
        foreach (var state in new[] { "PREPARANDO", "LISTO", "EN_RUTA", "ENTREGADO" })
        {
            order = (await orders.ReadAsync(orderId))!;
            Saved(await orders.TransitionAsync(new(Guid.NewGuid(), orderId, order.Order.RowVersion, state, "Avance operativo " + state)));
        }
        order = (await orders.ReadAsync(orderId))!;
        Check(order.Order.Delivered is not null && order.Order.DeliveredBy == identity.Id && order.Order.PaymentState == "PAGADO" && order.TotalEvents == 6, "Entrega conserva usuario/fecha, pagos derivados y eventos completos");
        Check((await orders.EventsAsync(orderId, new(1, 2))).Items.Count == 2, "Historial de pedido se pagina");
        Check(!(await orders.TransitionAsync(new(Guid.NewGuid(), orderId, order.Order.RowVersion, "PREPARANDO", "Reabrir"))).IsSuccess, "Entregado es un estado terminal");

        var unbilledOrderId = Saved(await orders.PlaceAsync(placed with { OperationId = Guid.NewGuid() }));
        foreach (var state in new[] { "PREPARANDO", "LISTO" })
        {
            var pendingOrder = (await orders.ReadAsync(unbilledOrderId))!;
            Saved(await orders.TransitionAsync(new(Guid.NewGuid(), unbilledOrderId, pendingOrder.Order.RowVersion, state, "Preparación sin venta")));
        }
        var unbilled = (await orders.ReadAsync(unbilledOrderId))!;
        Check(!(await orders.TransitionAsync(new(Guid.NewGuid(), unbilledOrderId, unbilled.Order.RowVersion, "EN_RUTA", "Despachar sin venta"))).IsSuccess && !(await orders.TransitionAsync(new(Guid.NewGuid(), unbilledOrderId, unbilled.Order.RowVersion, "ENTREGADO", "Entregar sin venta"))).IsSuccess, "No despacha ni entrega sin venta e inventario registrados");
        Saved(await orders.TransitionAsync(new(Guid.NewGuid(), unbilledOrderId, unbilled.Order.RowVersion, "CANCELADO", "Cancelar pedido no facturado")));

        // Pedido → POS: una sola transacción, incluso con pérdida de respuesta o dos equipos.
        var fromOrderId = Saved(await orders.PlaceAsync(placed with { OperationId = Guid.NewGuid() }));
        var fromOrder = (await orders.ReadAsync(fromOrderId))!;
        var beforeOrderStock = await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse;", new { p, warehouse });
        var origin = new SaleOrderOrigin(fromOrderId, fromOrder.Order.RowVersion);
        var fromOrderRequest = (await CashSale(Cart(delivery: 5))) with { Order = origin };
        Saved(await orders.TransitionAsync(new(Guid.NewGuid(), fromOrderId, origin.ExpectedVersion, "PREPARANDO", "Preparar antes de cobrar")));
        Check((await sales.ConfirmAsync(fromOrderRequest)).ErrorCode == "CONCURRENCY_CONFLICT", "Pedido cambiado exige revisar versión antes de cobrar");
        fromOrder = (await orders.ReadAsync(fromOrderId))!; origin = new(fromOrderId, fromOrder.Order.RowVersion);
        Check(await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse;", new { p, warehouse }) == beforeOrderStock, "Recibir y preparar pedido no reservan ni descuentan inventario");

        Task<string?> CommercialState() => c.ExecuteScalarAsync<string>("""
            SELECT (SELECT (SELECT COUNT(*) FROM ven.Ventas) Sales,
              (SELECT COUNT(*) FROM ven.VentaDetalle) Lines,(SELECT COUNT(*) FROM ven.VentaPagos) Payments,
              (SELECT COUNT(*) FROM ven.CuentasPorCobrar) Accounts,(SELECT COUNT(*) FROM ven.MovimientosCaja) CashMovements,
              (SELECT COUNT(*) FROM inv.MovimientosInventario) InventoryMovements,
              (SELECT SUM(StockActual) FROM inv.ExistenciasProducto) Stock,
              (SELECT EfectivoEsperado FROM ven.TurnosCaja WHERE IdTurno=@shiftId) Cash,
              (SELECT COUNT(*) FROM sis.OperacionesMvp) Operations,(SELECT COUNT(*) FROM sis.Bitacora) Audit,
              (SELECT COUNT(*) FROM ven.PedidoEventosMvp) Events,
              (SELECT IdVenta FROM ven.PedidosMvp WHERE IdPedido=@fromOrderId) LinkedSale
              FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
            """, new { shiftId, fromOrderId });
        foreach (var incompatible in new[] { Cart(delivery: 6), Cart(2, 5), Cart(delivery: 5) with { CustomerId = null } })
        {
            var incompatibleRequest = (await CashSale(incompatible)) with { Order = origin };
            var originalState = await CommercialState();
            Check((await sales.ConfirmAsync(incompatibleRequest)).ErrorCode == "VALIDATION", "Venta desde pedido conserva validación de cargo/cantidad/cliente");
            Check(await CommercialState() == originalState && await repository.FindOperationAsync(incompatibleRequest.OperationId, identity.Id, default) is null,
                "Enlace incompatible revierte venta, pagos, caja, Kardex, stock, evento y GUID");
        }
        var otherProduct = await Product("MVP-PEDIDO-OTRO", 101);
        Saved(await setup.InitializeAsync(new(Guid.NewGuid(), otherProduct, warehouse, 4, 30, "Stock para incompatibilidad")));
        var wrongProduct = (await CashSale(new(box, customerId, [new(otherProduct, 1)], DeliveryCharge: 5))) with { Order = origin };
        var beforeWrongProduct = await CommercialState();
        Check((await sales.ConfirmAsync(wrongProduct)).ErrorCode == "VALIDATION" && await CommercialState() == beforeWrongProduct, "Producto distinto también revierte toda la operación");

        fromOrderRequest = (await CashSale(Cart(delivery: 5))) with { Order = origin };
        var beforeAuditFailure = await CommercialState();
        await c.ExecuteAsync("""
            CREATE TRIGGER sis.tr_Test_OrderSaleRollback ON sis.Bitacora AFTER INSERT AS
            BEGIN
              IF EXISTS(SELECT 1 FROM inserted WHERE Modulo=N'MVP' AND Accion=N'CONFIRMAR_VENTA')
                THROW 51099,N'Fallo sintético después del enlace.',1;
            END;
            """);
        var orderSaleRolledBack = false;
        try { await sales.ConfirmAsync(fromOrderRequest); }
        catch (SqlException ex) when (ex.Number == 51099) { orderSaleRolledBack = true; }
        finally { await c.ExecuteAsync("DROP TRIGGER sis.tr_Test_OrderSaleRollback;"); }
        Check(orderSaleRolledBack && await CommercialState() == beforeAuditFailure && (await orders.ReadAsync(fromOrderId))!.Order.RowVersion.SequenceEqual(origin.ExpectedVersion),
            "Fallo posterior al enlace revierte venta y pedido incluidos versión, evento, stock, caja y ledger");
        var automaticSaleId = Saved(await sales.ConfirmAsync(fromOrderRequest));
        fromOrder = (await orders.ReadAsync(fromOrderId))!;
        Check(fromOrder.Order.SaleId == automaticSaleId && fromOrder.Order.State == "PREPARANDO" && fromOrder.Order.PaymentState == "PAGADO" && fromOrder.TotalEvents == 3,
            "Cobrar pedido vincula venta automáticamente sin cambiar su estado de preparación");
        Check((await repository.ReadReceiptAsync(automaticSaleId, identity.Id, default))!.Delivery?.Id == fromOrderId,
            "Comprobante con permiso de reimpresión incluye el pedido asociado automáticamente");
        var afterAutomaticSale = await CommercialState();
        Check(Saved(await sales.ConfirmAsync(fromOrderRequest)) == automaticSaleId && await CommercialState() == afterAutomaticSale,
            "Recuperar respuesta perdida devuelve venta enlazada original aunque su versión avanzó");
        Check((await repository.FindOperationAsync(fromOrderRequest.OperationId, identity.Id, default))?.Id == automaticSaleId,
            "Consulta por GUID identifica venta con pedido ya enlazado");
        Check(!(await sales.ConfirmAsync(fromOrderRequest with { OperationId = Guid.NewGuid() })).IsSuccess && await CommercialState() == afterAutomaticSale,
            "Otro GUID no vuelve a cobrar un pedido vinculado");

        var concurrentOrderId = Saved(await orders.PlaceAsync(placed with { OperationId = Guid.NewGuid() }));
        var concurrentOrder = (await orders.ReadAsync(concurrentOrderId))!;
        var competingRequest = (await CashSale(Cart(delivery: 5))) with { Order = new(concurrentOrderId, concurrentOrder.Order.RowVersion) };
        var salesBeforeCompetition = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.Ventas;");
        var orderCompetition = await Task.WhenAll(sales.ConfirmAsync(competingRequest), sales.ConfirmAsync(competingRequest with { OperationId = Guid.NewGuid() }));
        Check(orderCompetition.Count(r => r.IsSuccess) == 1 && orderCompetition.Count(r => !r.IsSuccess) == 1
            && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.Ventas;") == salesBeforeCompetition + 1
            && (await orders.ReadAsync(concurrentOrderId))!.Order.SaleId == orderCompetition.Single(r => r.IsSuccess).Value!.Id,
            "Dos equipos con GUID distintos confirman y vinculan una única venta por pedido");

        var cancelledOrderRequest = (await CashSale(Cart(delivery: 5))) with { Order = new(unbilledOrderId, (await orders.ReadAsync(unbilledOrderId))!.Order.RowVersion) };
        Check((await sales.ConfirmAsync(cancelledOrderRequest)).ErrorCode == "INVALID_STATE", "Pedido cancelado no admite venta automática");

        var fiscal = new FiscalPendingService(repository, session);
        customer = (await customers.ReadAsync(customerId))!;
        Check((await customers.SaveAddressAsync(new(customerId, customer.Customer.RowVersion, new() { Type = "FISCAL", Address = "Dirección fiscal aislada" }, "Preparación fiscal"))).IsSuccess, "Completa datos fiscales mediante contrato de clientes");
        Saved(await fiscal.RegisterAsync(new(Guid.NewGuid(), saleId, "FE", "Registro pendiente")));
        Check((await fiscal.SearchAsync(new())).Items.Single().State == "PENDIENTE" && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM fis.DocumentosDte;") == 0, "Preparación fiscal no inventa DTE aceptado");
        Check((await fiscal.RegisterAsync(new(Guid.NewGuid(), saleId, "CCFE", "Segundo tipo"))).ErrorCode == "INVALID_STATE", "No agrega otro tipo fiscal activo a la misma venta aun con datos completos");

        // Error de auditoría provoca rollback del saldo, movimiento y registro de reenvío.
        var before = (await cash.CurrentAsync(box))!;
        var rollbackRequest = new CashMovement(Guid.NewGuid(), shiftId, before.RowVersion, "INGRESO", 7, "Fallo de auditoría");
        var rolledBack = false;
        try { await repository.MoveCashAsync(rollbackRequest, identity.Id, int.MaxValue, default); } catch (SqlException ex) when (ex.Number == 547) { rolledBack = true; }
        Check(rolledBack && (await cash.CurrentAsync(box))!.Expected == before.Expected && await repository.FindOperationAsync(rollbackRequest.OperationId, identity.Id, default) is null, "Fallo de auditoría revierte movimiento, versión y ledger");
        var move = new CashMovement(Guid.NewGuid(), shiftId, before.RowVersion, "RETIRO", before.Expected + 1, "Retiro excesivo");
        Check(!(await cash.MoveAsync(move)).IsSuccess, "Retiro manual no deja efectivo esperado negativo");
        Saved(await cash.MoveAsync(move with { OperationId = Guid.NewGuid(), Type = "INGRESO", Amount = 5 }));
        before = (await cash.CurrentAsync(box))!;
        Saved(await cash.CloseAsync(new(Guid.NewGuid(), shiftId, before.RowVersion, before.Expected, "Arqueo completo")));
        var closed = (await cash.ReadAsync(shiftId))!;
        Check(closed.State == "CERRADO" && closed.Difference == 0 && await cash.CurrentAsync(box) is null, "Cierre registra efectivo y diferencia y libera caja");
        Check(!(await cash.MoveAsync(new(Guid.NewGuid(), shiftId, closed.RowVersion, "INGRESO", 1, "Turno cerrado"))).IsSuccess, "Caja cerrada rechaza nuevos movimientos");
        var afterClosing = await CashSale(Cart());
        Check(!(await sales.ConfirmAsync(afterClosing)).IsSuccess, "No vende en efectivo después del cierre");

        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'VENTAS.CREAR');");
        var deniedQuote = await sales.QuoteAsync(Cart());
        Check((await sales.ConfirmAsync(new(Guid.NewGuid(), deniedQuote.Cart, deniedQuote.Token, [new(transfer, deniedQuote.Total, "SIN-PERMISO")], 0, null, "Permiso revocado"))).ErrorCode == "FORBIDDEN", "SQL revalida permisos aunque la sesión aún los conserve");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE DatosDespuesJson LIKE '%'+@password+'%';", new { password }) == 0, "Reautenticación no persiste contraseña en auditoría");
        var salesBeforeInstall = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.Ventas;");
        await DevelopmentDatabase.InstallAsync(database, Path.Combine(root, "database"));
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.Ventas;") == salesBeforeInstall && (await orders.ReadAsync(orderId))!.Order.State == "ENTREGADO", "Reaplicar migraciones conserva ventas y entregas");
        Console.WriteLine($"{checks} comprobaciones MVP SQL correctas. Base aislada retenida: {database}");
    }
}
