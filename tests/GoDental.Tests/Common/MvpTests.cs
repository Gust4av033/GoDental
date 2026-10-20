using System.Reflection;
using System.Text;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Domain.Rules;
using GoDental.Infrastructure.Operations;
using Microsoft.Extensions.Options;

namespace GoDental.Tests.Common;

public sealed class CommercialPricingTests
{
    private static PricingInput Input() => new(1, "PRUEBA", "Producto", 2, 100, 0, 0, [], [], 0, false, [new(null, 2)]);

    [Fact]
    public void ListUsesPriorityThenEligibleQuantityAndStableId()
    {
        var input = Input() with
        {
            Quantity = 10,
            Prices = [new(3, 80, 1, 1), new(2, 85, 2, 5), new(1, 90, 2, 5), new(4, 50, 3, 11)]
        };
        var result = CommercialPricing.Calculate(input);
        Assert.Equal(1, result.PriceListId);
        Assert.Equal(90, result.UnitPrice);
        Assert.Equal(900, result.Total);
    }

    [Theory]
    [InlineData(false, 88)]
    [InlineData(true, 79.2)]
    public void PromotionAccumulatesOnlyWhenExplicit(bool accumulable, decimal expected)
    {
        var result = CommercialPricing.Calculate(Input() with
        {
            Prices = [new(1, 88, 1, 1)],
            Promotions = [new(1, "PORCENTAJE", null, 10, accumulable)]
        });
        Assert.Equal(expected, result.UnitPrice);
        Assert.Equal(accumulable ? 1L : (long?)null, result.PromotionId);
    }

    [Fact]
    public void BestSinglePromotionWinsBeforeManualDiscount()
    {
        var result = CommercialPricing.Calculate(Input() with
        {
            Prices = [new(1, 88, 1, 1)],
            Promotions = [new(2, "PRECIO_FIJO", 82, null, false), new(1, "PORCENTAJE", null, 15, false)],
            LineDiscount = 10,
            GlobalDiscount = 10
        });
        Assert.Equal(82, result.UnitPrice);
        Assert.Equal(2, result.PromotionId);
        Assert.Equal(19, result.EffectiveDiscountPercent);
        Assert.Equal(132.84m, result.Total);
    }

    [Fact]
    public void ThresholdComparisonDoesNotRoundAwayRequiredAuthorization()
    {
        var rate = CommercialPricing.EffectiveDiscount(30, 7.1429m);
        Assert.True(rate > 35);
        Assert.Equal(35, decimal.Round(rate, 4));
        Assert.Equal(rate, CommercialPricing.Calculate(Input() with { LineDiscount = 30, GlobalDiscount = 7.1429m }).EffectiveDiscountPercent);
    }

    [Theory]
    [InlineData(true, 113, 200, 20, 23.4, 203.4)]
    [InlineData(false, 100, 200, 20, 23.4, 203.4)]
    public void IncludedAndAddedTaxProduceConsistentNetTotals(bool included, decimal price, decimal subtotal, decimal discount, decimal tax, decimal total)
    {
        var result = CommercialPricing.Calculate(Input() with { PublicPrice = price, TaxRate = 13, TaxIncluded = included, LineDiscount = 10 });
        Assert.Equal(subtotal, result.Subtotal);
        Assert.Equal(discount, result.Discount);
        Assert.Equal(tax, result.Tax);
        Assert.Equal(total, result.Total);
        Assert.Equal(result.Subtotal - result.Discount + result.Tax, result.Total);
    }

    [Fact]
    public void LotSplittingRetainsWholeProductQuantityPriceAndRoundsEachLine()
    {
        var input = Input() with { Quantity = 3, PublicPrice = .05m, TaxRate = 13, TaxIncluded = true, Prices = [new(1, .04m, 1, 3)] };
        var selected = CommercialPricing.Calculate(input);
        var lines = new[] { new LotAllocation(1, 1), new LotAllocation(2, 2) }.Select(l => CommercialPricing.ForAllocation(input, selected, l)).ToArray();
        Assert.All(lines, l => Assert.Equal(.04m, l.UnitPrice));
        Assert.All(lines, l => Assert.Equal(1, l.PriceListId));
        Assert.All(lines, l => Assert.Equal(l.Subtotal - l.Discount + l.Tax, l.Total));
        Assert.Equal(.12m, lines.Sum(l => l.Total));
        Assert.Equal(.01m, CommercialPricing.Money(.005m));
    }

    [Fact]
    public void AllocationConsumesOnlyRequiredStockInProvidedFefoOrder()
    {
        Assert.Equal(new[] { new LotAllocation(7, 1.5m), new LotAllocation(3, .25m) }, CommercialPricing.Allocate(1.75m, [(7, 1.5m), (3, 5), (8, 2)]));
        Assert.Throws<ArgumentException>(() => CommercialPricing.Allocate(9, [(1, 1)]));
        Assert.Throws<ArgumentException>(() => CommercialPricing.Allocate(0, [(1, 1)]));
        Assert.Throws<ArgumentException>(() => CommercialPricing.Allocate(1, [(1, -1)]));
        Assert.Throws<ArgumentException>(() => CommercialPricing.Allocate(2, [(1, 1), (1, 1)]));
    }

    [Fact]
    public void UnsupportedPromotionAndExcessiveAmountsFailExplicitly()
    {
        Assert.Throws<ArgumentException>(() => CommercialPricing.Calculate(Input() with { Promotions = [new(1, "COMBO", null, null, false)] }));
        Assert.Throws<ArgumentException>(() => CommercialPricing.Calculate(Input() with { Quantity = 99999999999999m, PublicPrice = 9999999999999999m }));
        Assert.Throws<ArgumentException>(() => CommercialPricing.Calculate(Input() with { Prices = [new(1, -1, 1, 1)] }));
    }
}

public sealed class MvpValidationAndAccessTests
{
    private static readonly string Token = new('A', 64);
    private static SaleCart Cart() => new(1, null, [new(1, 1)]);
    private static ConfirmSale Sale() => new(Guid.NewGuid(), Cart(), Token, [new(1, 100)], 100, 1, "Venta de prueba");

    [Fact]
    public void SaleRequiresStableOperationValidTokenUniqueProductsAndSqlPrecision()
    {
        Assert.Null(MvpValidator.Validate(Sale()));
        Assert.NotNull(MvpValidator.Validate(Sale() with { OperationId = Guid.Empty }));
        Assert.NotNull(MvpValidator.Validate(Sale() with { QuoteToken = new('Z', 64) }));
        Assert.NotNull(MvpValidator.Validate(Sale() with { Cart = Cart() with { Items = [new(1, 1), new(1, 2)] } }));
        Assert.NotNull(MvpValidator.Validate(Sale() with { Cart = Cart() with { Items = [new(1, .00001m)] } }));
        Assert.NotNull(MvpValidator.Validate(Sale() with { Payments = [new(1, .001m)] }));
        Assert.NotNull(MvpValidator.Validate(Sale() with { Cart = Cart() with { GlobalDiscountPercent = .00001m } }));
        Assert.NotNull(MvpValidator.Validate(new CloseCashShift(Guid.NewGuid(), 1, [], 100, "Arqueo")));
        Assert.NotNull(MvpValidator.Validate(new BarcodeChange(Guid.NewGuid(), 1, new byte[8], new('A', 81), "CODE128", true, "Código")));
        Assert.False(MvpValidator.Page(new(int.MaxValue, 100)));
    }

    [Fact]
    public void FreezeCopiesAndSortsCartToKeepQuoteIdentityStable()
    {
        var mutable = new List<SaleItem> { new(2, 1), new(1, 1) };
        var frozen = MvpValidator.Freeze(Cart() with { Items = mutable });
        mutable.Clear();
        Assert.Equal(new[] { 1, 2 }, frozen.Items.Select(i => i.ProductId));
    }

    [Fact]
    public async Task BackendChecksPermissionsAndValidationBeforePersistence()
    {
        var session = new UserSession();
        var sales = new SalesService(Port<ISalesRepository>(), session, Port<IIdentityGateway>());
        var cash = new CashService(Port<ICashRepository>(), session);
        var orders = new OrderService(Port<IOrderRepository>(), session);
        Assert.Equal("FORBIDDEN", (await sales.ConfirmAsync(Sale())).ErrorCode);
        Assert.Equal("FORBIDDEN", (await cash.OpenAsync(new(Guid.NewGuid(), 1, 0, "Apertura"))).ErrorCode);
        Assert.Equal("FORBIDDEN", (await orders.TransitionAsync(new(Guid.NewGuid(), 1, new byte[8], "ENTREGADO", "Entrega"))).ErrorCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sales.QuoteAsync(Cart()));
        session.Begin(new(1, "prueba", "Prueba", 1, ["Vendedor"], ["VENTAS.VER", "VENTAS.CREAR", "CAJA.VER", "CAJA.ABRIR"], false));
        Assert.Equal("VALIDATION", (await sales.ConfirmAsync(Sale() with { CashTendered = -1 })).ErrorCode);
        Assert.Equal("VALIDATION", (await cash.OpenAsync(new(Guid.NewGuid(), 1, .001m, "Apertura"))).ErrorCode);
        Assert.Equal("FORBIDDEN", (await cash.PayAsync(new(Guid.NewGuid(), 1, new byte[8], 1, 10, null, 1, "Abono"))).ErrorCode);
        Assert.Equal("FORBIDDEN", (await new FiscalPendingService(Port<IFiscalPendingRepository>(), session).RegisterAsync(new(Guid.NewGuid(), 1, "FE", "Preparación"))).ErrorCode);
        Assert.Equal("FORBIDDEN", (await new InventorySetupService(Port<IInventorySetupRepository>(), session).InitializeAsync(new(Guid.NewGuid(), 1, 1, 1, 1, "Saldo"))).ErrorCode);
    }

    [Fact]
    public async Task AdministrativeReauthenticationCannotOutliveRequestingSession()
    {
        var session = new UserSession();
        session.Begin(new(1, "cajero", "Cajero", 1, ["Vendedor"], ["VENTAS.VER", "VENTAS.CREAR"], false));
        var gateway = DispatchProxy.Create<IIdentityGateway, CallbackPort>();
        ((CallbackPort)gateway).Callback = (_, _) =>
        {
            session.Clear();
            return Task.FromResult(Result<UserIdentity>.Success(new(2, "admin", "Administrador", 1, ["Administrador"], ["VENTAS.DESCUENTO_MAYOR_35"], false)));
        };
        var service = new SalesService(Port<ISalesRepository>(), session, gateway);
        var result = await service.AuthorizeDiscountAsync(new(Guid.NewGuid(), Cart(), Token, "Motivo"), "admin", "entrada-no-persistida");
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task ReceiptDoesNotReturnDataAfterSessionChanges()
    {
        var session = new UserSession();
        session.Begin(new(1, "cajero", "Cajero", 1, [], ["VENTAS.VER", "VENTAS.REIMPRIMIR"], false));
        var repository = DispatchProxy.Create<ISalesRepository, CallbackPort>();
        ((CallbackPort)repository).Callback = (_, _) =>
        {
            session.Clear();
            return Task.FromResult<SaleSnapshot?>(new(new() { State = "CONFIRMADA" }, [], []));
        };
        var service = new SaleReceiptService(repository, Port<IReceiptRenderer>(), session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ExportAsync(1));
    }

    [Theory]
    [InlineData("RECIBIDO", "PREPARANDO", true)]
    [InlineData("RECIBIDO", "ENTREGADO", false)]
    [InlineData("PREPARANDO", "LISTO", true)]
    [InlineData("LISTO", "ENTREGADO", true)]
    [InlineData("LISTO", "EN_RUTA", true)]
    [InlineData("EN_RUTA", "CANCELADO", false)]
    [InlineData("EN_RUTA", "INCIDENCIA", true)]
    [InlineData("INCIDENCIA", "PREPARANDO", true)]
    [InlineData("ENTREGADO", "RECIBIDO", false)]
    [InlineData("CANCELADO", "PREPARANDO", false)]
    public void OrderTransitionPreservesTerminalStates(string from, string to, bool allowed) => Assert.Equal(allowed, OrderRules.CanTransition(from, to));

    [Fact]
    public void RendererProducesRealPdfFromPersistedSale()
    {
        var renderer = new ReceiptPdfRenderer(Options.Create(new ReceiptPdfOptions()));
        var bytes = renderer.Render(new(new() { Number = "VENTA-PRUEBA", State = "CONFIRMADA", CustomerSnapshot = "{\"Name\":\"Cliente de prueba\"}", Total = 10, Subtotal = 10, CashTendered = 20, Change = 10 },
            [new() { Sku = "PRUEBA", Name = "Producto de prueba", Quantity = 1, UnitPrice = 10, Total = 10 }], [new() { Method = "Efectivo", Amount = 10 }]));
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 1000);
    }

    private static T Port<T>() where T : class => DispatchProxy.Create<T, UnreachablePort>();
    public class UnreachablePort : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException("La solicitud rechazada no debe alcanzar persistencia ni autenticación.");
    }
    public class CallbackPort : DispatchProxy
    {
        public Func<MethodInfo?, object?[]?, object?> Callback { get; set; } = (_, _) => throw new InvalidOperationException();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Callback(targetMethod, args);
    }
}
