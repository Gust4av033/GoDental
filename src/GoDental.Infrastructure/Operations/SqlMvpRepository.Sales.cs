using System.Text.Json;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;

namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    private const string SaleColumns = "IdVenta Id,NumeroVenta Number,IdCliente CustomerId,ISNULL(ClienteSnapshotMvp,N'') CustomerSnapshot,IdCaja CashBoxId,IdTurno ShiftId,Fecha Date,Estado State,Subtotal,DescuentoTotal Discount,ImpuestoTotal Tax,Total,SaldoPendiente Pending,EfectivoRecibido CashTendered,Cambio [Change],CargoEntrega DeliveryCharge,RowVersion";
    private sealed class PricingProduct
    {
        public int Id { get; set; }
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Available { get; set; }
        public bool TracksLot { get; set; }
        public bool TracksExpiry { get; set; }
        public bool Serial { get; set; }
        public bool Decimals { get; set; }
        public byte[] Version { get; set; } = [];
        public byte[]? StockVersion { get; set; }
    }

    private sealed class PricingCustomer
    {
        public long Id { get; set; }
        public short TypeId { get; set; }
        public int? PriceListId { get; set; }
    }

    private sealed class PriceRow
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public int Priority { get; set; }
        public decimal MinimumQuantity { get; set; }
    }

    private sealed class TaxRow
    {
        public decimal Rate { get; set; }
        public bool Included { get; set; }
    }

    private sealed class LotRow
    {
        public long Id { get; set; }
        public decimal Available { get; set; }
    }

    private sealed class Limits
    {
        public decimal Justification { get; set; }
        public decimal Authorization { get; set; }
    }

    public Task<IReadOnlyList<PaymentMethodData>> PaymentMethodsAsync(int userId, CancellationToken ct) => Read(userId, ["VENTAS.VER"], w => w.List<PaymentMethodData>("SELECT IdMetodoPago Id,Codigo Code,Nombre Name,AfectaCajaEfectivo AffectsCash,RequiereReferencia RequiresReference FROM cat.MetodosPago WHERE Activo=1 AND Codigo<>N'EFECTIVO_REPARTO' ORDER BY Nombre;"), ct);
    public Task<MvpPage<PosProductData>> SearchProductsAsync(int boxId, string search, MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["VENTAS.VER"], async w =>
    {
        var warehouse = await BoxWarehouse(w, boxId);
        return await Page<PosProductData>(w, "inv.Productos p LEFT JOIN inv.ExistenciasProducto e ON e.IdProducto=p.IdProducto AND e.IdAlmacen=@warehouse", "p.IdProducto Id,p.SKU,p.Nombre Name,p.PrecioVentaReferencia Price,ISNULL(e.StockActual-e.StockReservado,0) Available,p.ControlaLote TracksLot", "p.Activo=1 AND p.ControlaSerie=0 AND (@search=N'' OR CHARINDEX(@search,p.SKU)>0 OR CHARINDEX(@search,p.Nombre)>0 OR EXISTS(SELECT 1 FROM inv.ProductoCodigosBarra b WHERE b.IdProducto=p.IdProducto AND b.Activo=1 AND b.Codigo=@search))", "CASE WHEN p.SKU=@search OR EXISTS(SELECT 1 FROM inv.ProductoCodigosBarra b WHERE b.IdProducto=p.IdProducto AND b.Activo=1 AND b.Codigo=@search) THEN 0 ELSE 1 END,p.Nombre,p.IdProducto", new { warehouse, search, Offset = checked((f.Page - 1) * f.PageSize), f.PageSize });
    }, ct);
    private static async Task<int> BoxWarehouse(Work w, int id)
    {
        var warehouse = await w.Scalar<int?>("SELECT c.IdAlmacen FROM ven.Cajas c WITH(UPDLOCK,HOLDLOCK) JOIN inv.Almacenes a WITH(HOLDLOCK) ON a.IdAlmacen=c.IdAlmacen AND a.Activo=1 WHERE c.IdCaja=@id AND c.Activo=1;", new { id });
        Require(warehouse is not null, "INVALID_STATE", "Selecciona una caja y un almacén activos.");
        return warehouse!.Value;
    }

    public Task<SaleQuote> QuoteAsync(SaleCart cart, int userId, CancellationToken ct) => Read(userId, ["VENTAS.VER"], w => Calculate(w, cart), ct);
    private static async Task<SaleQuote> Calculate(Work w, SaleCart cart)
    {
        if (MvpValidator.Cart(cart) is { } error)
            throw new RuleFailure("VALIDATION", error);
        cart = MvpValidator.Freeze(cart);
        var warehouse = await BoxWarehouse(w, cart.CashBoxId);
        var today = await w.Scalar<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
        PricingCustomer? customer = null;
        if (cart.CustomerId is { } customerId)
        {
            customer = await w.Maybe<PricingCustomer>("SELECT IdCliente Id,IdTipoCliente TypeId,IdListaPrecio PriceListId FROM cli.Clientes WITH(UPDLOCK,HOLDLOCK) WHERE IdCliente=@customerId AND Activo=1;", new { customerId });
            Require(customer is not null, "INVALID_STATE", "El cliente está inactivo o no existe.");
        }

        var listId = cart.PriceListId ?? customer?.PriceListId;
        if (listId is not null)
            Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM ven.ListasPrecio WITH(HOLDLOCK) WHERE IdListaPrecio=@listId AND Activo=1 AND (IdTipoCliente IS NULL OR IdTipoCliente=@typeId) AND (FechaInicio IS NULL OR FechaInicio<=@today) AND (FechaFin IS NULL OR FechaFin>=@today)) THEN 1 ELSE 0 END);", new { listId, typeId = customer?.TypeId, today }), "INVALID_STATE", "La lista asignada no está vigente o no corresponde al cliente.");
        var products = new List<(SaleItem Item, PricingProduct Product)>();
        foreach (var item in cart.Items)
        {
            var p = await w.Maybe<PricingProduct>("""
                SELECT p.IdProducto Id,p.SKU,p.Nombre Name,p.PrecioVentaReferencia Price,p.ControlaLote TracksLot,p.ControlaVencimiento TracksExpiry,p.ControlaSerie Serial,
                  u.PermiteDecimales Decimals,p.RowVersion Version,e.RowVersion StockVersion,ISNULL(e.StockActual-e.StockReservado,0) Available
                FROM inv.Productos p WITH(UPDLOCK,HOLDLOCK) JOIN cat.UnidadesMedida u WITH(HOLDLOCK) ON u.IdUnidadMedida=p.IdUnidadMedida
                LEFT JOIN inv.ExistenciasProducto e WITH(UPDLOCK,HOLDLOCK) ON e.IdProducto=p.IdProducto AND e.IdAlmacen=@warehouse WHERE p.IdProducto=@ProductId AND p.Activo=1;
                """, new { item.ProductId, warehouse });
            Require(p is not null && !p.Serial, "INVALID_STATE", "El producto no está activo o requiere un control de series no soportado.");
            Require(p!.Decimals || decimal.Truncate(item.Quantity) == item.Quantity, "VALIDATION", "La unidad del producto no admite cantidades fraccionarias.");
            Require(p.Available >= item.Quantity, "INSUFFICIENT_STOCK", "No hay stock disponible suficiente.");
            Require(p.Price == 0 || item.Quantity <= 9999999999999999.99m / p.Price, "VALIDATION", "La línea supera el rango monetario.");
            products.Add((item, p));
        }

        var publicSubtotal = products.Sum(x => CommercialPricing.Money(x.Product.Price * x.Item.Quantity));
        Require(MvpValidator.Money(publicSubtotal), "VALIDATION", "El subtotal público supera el rango monetario.");
        var lines = new List<CalculatedPrice>();
        var versions = new List<string>();
        foreach (var (item, p) in products)
        {
            var prices = await w.List<PriceRow>("""
                SELECT l.IdListaPrecio Id,d.Precio Price,l.Prioridad Priority,d.CantidadMinima MinimumQuantity
                FROM ven.ListasPrecio l WITH(HOLDLOCK) JOIN ven.ListaPrecioProductos d WITH(HOLDLOCK) ON d.IdListaPrecio=l.IdListaPrecio AND d.Activo=1
                WHERE d.IdProducto=@ProductId AND l.Activo=1 AND (@listId IS NULL OR l.IdListaPrecio=@listId)
                  AND (@listId IS NOT NULL OR (l.IdTipoCliente=@typeId AND @typeId IS NOT NULL))
                  AND (l.FechaInicio IS NULL OR l.FechaInicio<=@today) AND (l.FechaFin IS NULL OR l.FechaFin>=@today)
                  AND (l.MontoMinimoVenta IS NULL OR l.MontoMinimoVenta<=@publicSubtotal);
                """, new { item.ProductId, listId, typeId = customer?.TypeId, today, publicSubtotal });
            var promos = await w.List<PromotionChoice>("""
                SELECT p.IdPromocion Id,p.Tipo Type,COALESCE(d.PrecioPromocional,CASE WHEN p.Tipo=N'PRECIO_FIJO' THEN p.Valor END) FixedPrice,
                  COALESCE(d.DescuentoPorcentaje,CASE WHEN p.Tipo=N'PORCENTAJE' THEN p.Valor END) [Percent],p.Acumulable Accumulable
                FROM ven.Promociones p WITH(HOLDLOCK) JOIN ven.PromocionDetalle d WITH(HOLDLOCK) ON d.IdPromocion=p.IdPromocion
                WHERE d.IdProducto=@ProductId AND p.Activo=1 AND p.FechaInicio<=SYSDATETIMEOFFSET() AND p.FechaFin>=SYSDATETIMEOFFSET()
                  AND (d.CantidadMinima IS NULL OR d.CantidadMinima<=@Quantity) ORDER BY p.IdPromocion;
                """, item);
            var taxes = await w.List<TaxRow>("SELECT i.Tasa Rate,i.IncluidoEnPrecio Included FROM inv.ProductoImpuestos p WITH(HOLDLOCK) JOIN cat.Impuestos i WITH(HOLDLOCK) ON i.IdImpuesto=p.IdImpuesto AND i.Activo=1 WHERE p.IdProducto=@ProductId AND p.Activo=1 ORDER BY i.IdImpuesto;", item);
            Require(taxes.Count <= 1, "INVALID_STATE", "Este producto tiene varios impuestos. Se requiere ampliar su cálculo antes de venderlo.");
            IReadOnlyList<LotAllocation> lots;
            if (p.TracksLot)
            {
                var available = await w.List<LotRow>("""
                    SELECT TOP(1000) l.IdLote Id,e.StockActual Available FROM inv.LotesProducto l WITH(UPDLOCK,HOLDLOCK)
                    JOIN inv.ExistenciasLote e WITH(UPDLOCK,HOLDLOCK) ON e.IdLote=l.IdLote AND e.IdAlmacen=@warehouse
                    WHERE l.IdProducto=@ProductId AND l.Activo=1 AND e.StockActual>0 AND (@LotId IS NULL OR l.IdLote=@LotId)
                      AND (l.FechaVencimiento IS NULL OR l.FechaVencimiento>=@today) AND (@TracksExpiry=0 OR l.FechaVencimiento IS NOT NULL)
                    ORDER BY CASE WHEN l.FechaVencimiento IS NULL THEN 1 ELSE 0 END,l.FechaVencimiento,l.IdLote;
                    """, new { item.ProductId, item.LotId, warehouse, today, p.TracksExpiry });
                lots = CommercialPricing.Allocate(item.Quantity, available.Select(l => (l.Id, l.Available)).ToArray());
            }
            else
            {
                Require(item.LotId is null, "VALIDATION", "El producto no controla lotes.");
                lots = [new(null, item.Quantity)];
            }

            var input = new PricingInput(p.Id, p.Sku, p.Name, item.Quantity, p.Price, item.DiscountPercent, cart.GlobalDiscountPercent, prices.Select(x => new PriceChoice(x.Id, x.Price, x.Priority, x.MinimumQuantity)).ToArray(), promos, taxes.FirstOrDefault()?.Rate ?? 0, taxes.FirstOrDefault()?.Included ?? false, lots);
            var selected = CommercialPricing.Calculate(input);
            foreach (var allocation in lots)
                lines.Add(CommercialPricing.ForAllocation(input, selected, allocation));
            versions.Add(Convert.ToHexString(p.Version) + ":" + Convert.ToHexString(p.StockVersion ?? []));
        }

        Require(lines.Count <= 1000, "VALIDATION", "El carrito supera 1.000 líneas de lote. Divide la operación.");
        var limits = await w.One<Limits>("SELECT ISNULL((SELECT TRY_CONVERT(decimal(9,4),Valor) FROM sis.Configuracion WITH(HOLDLOCK) WHERE Clave=N'Descuento.RequiereJustificacionSobre'),7) Justification,ISNULL((SELECT TRY_CONVERT(decimal(9,4),Valor) FROM sis.Configuracion WITH(HOLDLOCK) WHERE Clave=N'Descuento.RequiereAutorizacionSobre'),35) [Authorization];");
        Require(limits.Justification >= 0 && limits.Authorization >= limits.Justification && limits.Authorization <= 100, "INVALID_STATE", "Revisa los umbrales de descuento configurados.");
        Require(MvpValidator.Money(lines.Sum(l => l.Subtotal)) && MvpValidator.Money(lines.Sum(l => l.Discount)) && MvpValidator.Money(lines.Sum(l => l.Tax)), "VALIDATION", "Los importes calculados superan el rango monetario.");
        var total = lines.Sum(l => l.Total) + cart.DeliveryCharge;
        Require(total > 0 && MvpValidator.Money(total), "VALIDATION", "El total debe ser positivo y estar dentro del rango monetario.");
        var token = HashToken(new { cart, lines, versions, today, limits });
        return new(cart, lines, lines.Sum(l => l.Subtotal), lines.Sum(l => l.Discount), lines.Sum(l => l.Tax), total, limits.Justification, limits.Authorization, token);
    }

    private static void QuoteToken(SaleQuote q, string token) => Require(q.Token == token, "CONCURRENCY_CONFLICT", "Cambió el cálculo, precio o disponibilidad. Consulta y acepta el precio de nuevo.");
    private static async Task DiscountPermission(Work w, SaleQuote quote, int userId, string reason)
    {
        var max = quote.Lines.Max(l => l.EffectiveDiscountPercent);
        if (max > 0)
            await w.Authorize(userId, max > quote.JustificationThreshold ? ["VENTAS.DESCUENTO_MAYOR_7"] : ["VENTAS.DESCUENTO_7"]);
        if (max > quote.JustificationThreshold)
            Require(MvpValidator.Reason(reason), "VALIDATION", "El descuento requiere motivo.");
    }

    public Task<Result<MvpReceipt>> AuthorizeDiscountAsync(DiscountApproval r, int administratorId, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "AUTORIZAR_DESCUENTO", new { r, administratorId }, userId, workstationId, ["VENTAS.VER", "VENTAS.CREAR"], async w =>
    {
        Check(r);
        await w.Authorize(administratorId, "VENTAS.DESCUENTO_MAYOR_35");
        Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM seg.UsuarioRoles u JOIN seg.Roles r ON r.IdRol=u.IdRol AND r.Activo=1 WHERE u.IdUsuario=@administratorId AND r.Nombre=N'Administrador') THEN 1 ELSE 0 END);", new { administratorId }), "FORBIDDEN", "La cuenta no es administradora.");
        var quote = await Calculate(w, r.Cart);
        QuoteToken(quote, r.QuoteToken);
        await DiscountPermission(w, quote, userId, r.Reason);
        var percent = quote.Lines.Max(l => l.EffectiveDiscountPercent);
        Require(percent > quote.AuthorizationThreshold, "VALIDATION", "Ese descuento no necesita autorización administrativa.");
        return await w.Scalar<long>("INSERT INTO sis.AutorizacionesOperacion(TipoOperacion,EntidadTipo,SolicitadoPor,AutorizadoPor,PorcentajeValor,Motivo,FechaAutorizacion,Resultado,TokenCalculoMvp) OUTPUT INSERTED.IdAutorizacion VALUES(N'DESCUENTO_MAYOR_35',N'CARRITO_MVP',@userId,@administratorId,@percent,@Reason,SYSDATETIMEOFFSET(),N'APROBADA',@QuoteToken);", new { userId, administratorId, percent = decimal.Ceiling(percent * 10000) / 10000, r.Reason, r.QuoteToken });
    }, ct, GoDental.Application.Common.OperationIdentity.Describe(r));
    private static async Task ConsumeDiscount(Work w, SaleQuote quote, long? authorizationId, int userId, string entity, long id)
    {
        if (quote.Lines.Max(l => l.EffectiveDiscountPercent) <= quote.AuthorizationThreshold) return;
        Require(authorizationId is not null, "FORBIDDEN", "El descuento requiere reautenticación administrativa.");
        var admin = await w.Scalar<int?>("SELECT AutorizadoPor FROM sis.AutorizacionesOperacion WITH(UPDLOCK,HOLDLOCK) WHERE IdAutorizacion=@authorizationId AND TipoOperacion=N'DESCUENTO_MAYOR_35' AND EntidadTipo=N'CARRITO_MVP' AND SolicitadoPor=@userId AND Resultado=N'APROBADA' AND TokenCalculoMvp=@Token AND UtilizadaMvp=0 AND FechaAutorizacion>=DATEADD(MINUTE,-10,SYSDATETIMEOFFSET());", new { authorizationId, userId, quote.Token });
        Require(admin is not null, "FORBIDDEN", "La autorización no corresponde al cálculo, expiró o ya fue utilizada.");
        await w.Authorize(admin!.Value, "VENTAS.DESCUENTO_MAYOR_35");
        Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM seg.UsuarioRoles u JOIN seg.Roles r ON r.IdRol=u.IdRol AND r.Activo=1 WHERE u.IdUsuario=@administratorId AND r.Nombre=N'Administrador') THEN 1 ELSE 0 END);", new { administratorId = admin.Value }), "FORBIDDEN", "La autorización requiere una cuenta administradora vigente.");
        await w.Execute("UPDATE sis.AutorizacionesOperacion SET EntidadTipo=@entity,EntidadId=@id,UtilizadaMvp=1 WHERE IdAutorizacion=@authorizationId;", new { entity, id, authorizationId });
    }

    public Task<Result<MvpReceipt>> SaveQuotationAsync(SaveQuotation r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "EMITIR_COTIZACION", r, userId, workstationId, ["VENTAS.VER", "VENTAS.COTIZAR"], async w =>
    {
        Check(r);
        var quote = await Calculate(w, r.Cart);
        QuoteToken(quote, r.QuoteToken);
        await DiscountPermission(w, quote, userId, r.Reason);
        Require(r.ValidUntil is null || r.ValidUntil >= await w.Scalar<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');"), "VALIDATION", "La cotización ya está vencida.");
        var number = await Number(w, "COTIZACION");
        var id = await w.Scalar<long>("INSERT INTO ven.Cotizaciones(NumeroCotizacion,IdCliente,ValidaHasta,Estado,Subtotal,Descuento,Impuesto,Total,CreadaPor,Observaciones,SnapshotMvp) OUTPUT INSERTED.IdCotizacion VALUES(@number,@CustomerId,@ValidUntil,N'EMITIDA',@Subtotal,@Discount,@Tax,@Total,@userId,@Reason,@snapshot);", new { number, r.Cart.CustomerId, r.ValidUntil, quote.Subtotal, quote.Discount, quote.Tax, quote.Total, userId, r.Reason, snapshot = Json(quote) });
        foreach (var l in quote.Lines)
            await w.Execute("INSERT INTO ven.CotizacionDetalle(IdCotizacion,IdProducto,DescripcionSnapshot,Cantidad,PrecioUnitario,DescuentoPorcentaje,DescuentoMonto,ImpuestoMonto,TotalLinea) VALUES(@id,@ProductId,@Name,@Quantity,@NetUnitPrice,@percent,@Discount,@Tax,@Total);", new { id, l.ProductId, l.Name, l.Quantity, l.NetUnitPrice, percent = decimal.Ceiling(l.EffectiveDiscountPercent * 10000) / 10000, l.Discount, l.Tax, l.Total });
        await ConsumeDiscount(w, quote, r.DiscountAuthorizationId, userId, "COTIZACION", id);
        return id;
    }, ct);
    private static async Task<string> Number(Work w, string type) => await w.Scalar<string>("DECLARE @n nvarchar(60);EXEC sis.sp_SiguienteCorrelativo @TipoDocumento=@type,@NumeroDocumento=@n OUTPUT;SELECT @n;", new { type });
    public Task<Result<MvpReceipt>> ConfirmAsync(ConfirmSale r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "CONFIRMAR_VENTA", r, userId, workstationId, ["VENTAS.VER", "VENTAS.CREAR"], async w =>
    {
        Check(r);
        // Bloquear el pedido antes de cualquier efecto comercial impide cobrarlo en dos equipos.
        if (r.Order is { } origin)
            { LinkableOrder(await LockOrder(w, origin.OrderId, origin.ExpectedVersion)); await NoDelivery(w, origin.OrderId); }
        Require(workstationId is not null, "FORBIDDEN", "La sesión no tiene equipo de trabajo.");
        var methods = new List<(SalePayment Payment, PaymentMethodData Method)>();
        foreach (var p in r.Payments)
        {
            var method = await Method(w, p.MethodId);
            Require(!method.RequiresReference || !string.IsNullOrWhiteSpace(p.Reference), "VALIDATION", "El método requiere referencia.");
            methods.Add((p, method));
        }

        var cash = methods.Where(p => p.Method.AffectsCash).Sum(p => p.Payment.Amount);
        if (cash > 0)
        {
            Require(r.ShiftId is not null, "VALIDATION", "El efectivo requiere un turno abierto.");
            await LockShift(w, r.ShiftId!.Value, r.Cart.CashBoxId);
        }

        Require(cash > 0 ? r.CashTendered >= cash : r.CashTendered == 0, "VALIDATION", "El efectivo recibido no cubre el importe aplicado o no corresponde al pago.");
        if (methods.Any(m => m.Method.Code == "CREDITO"))
            await w.Authorize(userId, "VENTAS.USAR_CREDITO");
        var quote = await Calculate(w, r.Cart);
        QuoteToken(quote, r.QuoteToken);
        Require(r.Payments.Sum(p => p.Amount) == quote.Total, "VALIDATION", "Los pagos deben sumar exactamente el total.");
        await DiscountPermission(w, quote, userId, r.Reason);
        if (r.QuotationId is { } quotationId)
        {
            var quotation = await w.Maybe<QuotationData>("SELECT IdCotizacion Id,Estado State,ValidaHasta ValidUntil,ISNULL(SnapshotMvp,N'') SnapshotJson FROM ven.Cotizaciones WITH(UPDLOCK,HOLDLOCK) WHERE IdCotizacion=@quotationId;", new { quotationId });
            Require(quotation is not null && quotation.State == "EMITIDA" && (quotation.ValidUntil is null || quotation.ValidUntil >= await w.Scalar<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');")), "INVALID_STATE", "La cotización no está vigente o ya fue convertida.");
            var original = JsonSerializer.Deserialize<SaleQuote>(quotation!.SnapshotJson);
            Require(original is not null && Json(original.Cart) == Json(r.Cart), "CONCURRENCY_CONFLICT", "La venta no corresponde al carrito cotizado.");
        }

        var number = await Number(w, "VENTA");
        var snapshot = r.Cart.CustomerId is { } customerId ? await w.Scalar<string>("SELECT NombreRazonSocial Name,NombreComercial CommercialName,NIT,NRC,DUI,Giro,Telefono Phone,Email,IdTipoCliente CustomerTypeId,IdListaPrecio AssignedPriceListId FROM cli.Clientes WHERE IdCliente=@customerId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;", new { customerId }) : "{\"Name\":\"Consumidor final\"}";
        var id = await w.Scalar<long>("INSERT INTO ven.Ventas(NumeroVenta,IdCliente,IdCotizacion,IdCaja,IdTurno,IdVendedor,IdEquipo,Observaciones,CargoEntrega,EfectivoRecibido,Cambio,ClienteSnapshotMvp) OUTPUT INSERTED.IdVenta VALUES(@number,@CustomerId,@QuotationId,@CashBoxId,@ShiftId,@userId,@workstationId,@Reason,@DeliveryCharge,@CashTendered,@change,@snapshot);", new { number, r.Cart.CustomerId, r.QuotationId, r.Cart.CashBoxId, ShiftId = cash > 0 ? r.ShiftId : null, userId, workstationId, r.Reason, r.Cart.DeliveryCharge, r.CashTendered, change = r.CashTendered - cash, snapshot });
        foreach (var l in quote.Lines)
            await w.Execute("INSERT INTO ven.VentaDetalle(IdVenta,IdProducto,IdLote,SkuSnapshot,DescripcionSnapshot,Cantidad,PrecioLista,PrecioUnitario,DescuentoPorcentaje,DescuentoMonto,CostoUnitario,ImpuestoMonto,TotalLinea,CalculoPrecioMvp) VALUES(@id,@ProductId,@lotId,@Sku,@Name,@Quantity,@PublicPrice,@NetUnitPrice,@percent,@Discount,0,@Tax,@Total,@trace);", new { id, l.ProductId, lotId = l.Lots.Single().LotId, l.Sku, l.Name, l.Quantity, l.PublicPrice, l.NetUnitPrice, percent = decimal.Ceiling(l.EffectiveDiscountPercent * 10000) / 10000, l.Discount, l.Tax, l.Total, trace = Json(l) });
        foreach (var p in r.Payments)
            await w.Execute("INSERT INTO ven.VentaPagos(IdVenta,IdMetodoPago,Monto,Referencia,IdUsuario) VALUES(@id,@MethodId,@Amount,@Reference,@userId);", new { id, p.MethodId, p.Amount, p.Reference, userId });
        await ConsumeDiscount(w, quote, r.DiscountAuthorizationId, userId, "VENTA", id);

        await w.Execute("EXEC ven.sp_ConfirmarVenta @IdVenta=@id,@IdUsuario=@userId,@MotivoDescuento=@Reason,@IdAutorizacionDescuento=@DiscountAuthorizationId;", new { id, userId, r.Reason, r.DiscountAuthorizationId });
        // El snapshot de cálculo conserva los redondeos exactos por línea/lote.
        await w.Execute("UPDATE ven.Ventas SET Subtotal=@Subtotal,DescuentoTotal=@Discount,ImpuestoTotal=@Tax WHERE IdVenta=@id;", new { id, quote.Subtotal, quote.Discount, quote.Tax });
        if (cash > 0)
            await TouchShift(w, r.ShiftId!.Value);
        if (r.QuotationId is not null)
            await w.Execute("UPDATE ven.Cotizaciones SET Estado=N'CONVERTIDA' WHERE IdCotizacion=@QuotationId;", r);
        if (r.Order is { } order)
            await LinkOrderSaleCore(w, new(r.OperationId, order.OrderId, order.ExpectedVersion, id, r.Reason), userId);
        return id;
    }, ct);
    public Task<QuotationData?> ReadQuotationAsync(long id, int userId, CancellationToken ct) => Read(userId, ["VENTAS.VER"], w => w.Maybe<QuotationData>("SELECT IdCotizacion Id,NumeroCotizacion Number,IdCliente CustomerId,Fecha Date,Total,ValidaHasta ValidUntil,Estado State,ISNULL(SnapshotMvp,N'') SnapshotJson,RowVersion FROM ven.Cotizaciones WHERE IdCotizacion=@id;", new { id }), ct);
    public Task<MvpPage<QuotationData>> QuotationsAsync(MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["VENTAS.VER"], w => Page<QuotationData>(w, "ven.Cotizaciones", "IdCotizacion Id,NumeroCotizacion Number,IdCliente CustomerId,Fecha Date,Total,ValidaHasta ValidUntil,Estado State,RowVersion", "1=1", "IdCotizacion DESC", new { Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }), ct);
    public Task<MvpPage<SaleData>> SalesAsync(MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["VENTAS.VER"], w => Page<SaleData>(w, "ven.Ventas", SaleColumns, "1=1", "IdVenta DESC", new { Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }), ct);
    public Task<SaleSnapshot?> ReadReceiptAsync(long id, int userId, CancellationToken ct) => ReadSaleCore(id, userId, ["VENTAS.VER", "VENTAS.REIMPRIMIR"], ct);
    public Task<SaleSnapshot?> ReadSaleAsync(long id, int userId, CancellationToken ct) => ReadSaleCore(id, userId, ["VENTAS.VER"], ct);
    private Task<SaleSnapshot?> ReadSaleCore(long id, int userId, string[] permissions, CancellationToken ct) => Read(userId, permissions, async w =>
    {
        var sale = await w.Maybe<SaleData>($"SELECT {SaleColumns} FROM ven.Ventas WHERE IdVenta=@id;", new { id });
        if (sale is null)
            return null;
        var lines = await w.List<SaleLineData>("SELECT IdDetalle Id,IdProducto ProductId,IdLote LotId,SkuSnapshot Sku,DescripcionSnapshot Name,Cantidad Quantity,PrecioUnitario UnitPrice,DescuentoMonto Discount,ImpuestoMonto Tax,TotalLinea Total,CalculoPrecioMvp PricingTrace FROM ven.VentaDetalle WHERE IdVenta=@id ORDER BY IdDetalle;", new { id });
        var payments = await w.List<SalePaymentData>("SELECT p.IdMetodoPago MethodId,m.Nombre Method,p.Monto Amount,p.Referencia Reference FROM ven.VentaPagos p JOIN cat.MetodosPago m ON m.IdMetodoPago=p.IdMetodoPago WHERE p.IdVenta=@id AND p.Estado=N'CONFIRMADO' ORDER BY p.IdPago;", new { id });
        var delivery = await w.Maybe<OrderData>($"SELECT {OrderColumns} FROM ven.PedidosMvp p LEFT JOIN ven.Ventas v ON v.IdVenta=p.IdVenta WHERE p.IdVenta=@id;", new { id });
        return new SaleSnapshot(sale, lines, payments, delivery);
    }, ct);
}
