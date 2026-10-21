using System.IO;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

// Synthetic data for tests and visual smoke only; never registered by the app.
internal sealed class MvpUiRepository : ISalesRepository, ICashRepository, IOrderRepository, IMvpOperationRepository, IInventorySetupRepository, ICommercialParametersRepository
{
    public List<object> Writes { get; } = [];
    public bool LoseResponse { get; set; }
    public string? RejectCode { get; set; }
    public MvpReceipt? FoundReceipt { get; set; }
    public Task<SaleQuote>? QuoteTask { get; set; }
    public Task<OrderSnapshot?>? OrderReadTask { get; set; }
    public Task<SaleSnapshot?>? ReceiptReadTask { get; set; }
    public Func<SaleCart, SaleQuote> QuoteFactory { get; set; } = cart => new(cart with { Items = cart.Items.ToArray() }, cart.Items.Select(i => new CalculatedPrice(i.ProductId, "UI-TEST", "Producto de prueba", i.Quantity, 100, 90, 90, 100, 10, 13.45m, 123.45m, i.DiscountPercent, 1, null, [new(null, i.Quantity)])).ToArray(), 100, 10, 13.45m, 123.45m, 7, 35, new string('A', 64));
    public List<CashBoxData> Boxes { get; } = [new() { Id = 1, WarehouseId = 1, Code = "PRUEBA", Name = "Caja de prueba", Active = true }];
    public CashShiftData? Shift { get; set; } = new() { Id = 1, CashBoxId = 1, State = "ABIERTO", Opening = 50, Expected = 75, RowVersion = [1,2,3,4,5,6,7,8] };
    public List<CashMovementData> Movements { get; } = [new() { Id = 1, Date = DateTimeOffset.UtcNow, Type = "INGRESO", Amount = 25, Reason = "Movimiento de prueba" }];
    public List<PaymentMethodData> Methods { get; } = [new() { Id = 1, Code = "EFECTIVO", Name = "Efectivo", AffectsCash = true }, new() { Id = 2, Code = "TRANSFERENCIA", Name = "Transferencia", RequiresReference = true }, new() { Id = 3, Code = "CREDITO", Name = "Crédito" }, new() { Id = 4, Code = "TARJETA", Name = "Tarjeta", RequiresReference = true }];
    public List<PosProductData> Products { get; } = [new() { Id = 1, Sku = "UI-TEST", Name = "Producto de prueba", Price = 100, Available = 10 }];
    public SaleSnapshot Sale { get; } = new(new() { Id = 1, Number = "PRUEBA-001", State = "CONFIRMADA", Total = 123.45m, CashTendered = 150, Change = 26.55m }, [new() { ProductId = 1, Sku = "UI-TEST", Name = "Producto de prueba", Quantity = 1, UnitPrice = 100, Total = 123.45m }], [new() { MethodId = 1, Method = "Efectivo", Amount = 123.45m }]);
    public List<QuotationData> Quotations { get; } = [];
    public CreditAccountData Account { get; } = new() { Id = 1, CustomerId = 1, Customer = "Cliente de prueba", Sale = "PRUEBA-001", Due = DateTime.Today, Original = 50, Balance = 25, State = "PENDIENTE", RowVersion = [8,7,6,5,4,3,2,1] };
    public OrderSnapshot Order { get; set; } = new(new() { Id = 1, CustomerId = 1, Customer = "Cliente de prueba", Contact = "Contacto de prueba", Phone = "0000", Address = "Dirección ficticia", State = "RECIBIDO", RowVersion = [2,2,2,2,2,2,2,2] }, [new() { ProductId = 1, Sku = "UI-TEST", Name = "Producto de prueba", Quantity = 1 }], [new() { Date = DateTimeOffset.UtcNow, State = "RECIBIDO", UserId = 1, Reason = "Pedido de prueba" }], 1);
    private Task<Result<MvpReceipt>> Write(object request, Guid id, long entity)
    {
        Writes.Add(request);
        if (LoseResponse) throw new IOException("Synthetic lost response");
        return Task.FromResult(RejectCode is null ? Result<MvpReceipt>.Success(new(entity, id)) : Result<MvpReceipt>.Failure(RejectCode, "Operación rechazada por prueba"));
    }
    public Task<IReadOnlyList<PaymentMethodData>> PaymentMethodsAsync(int userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PaymentMethodData>>(Methods);
    public Task<MvpPage<PosProductData>> SearchProductsAsync(int boxId, string search, MvpFilter filter, int userId, CancellationToken ct) => Task.FromResult(new MvpPage<PosProductData>(Products, Products.Count));
    public Task<SaleQuote> QuoteAsync(SaleCart cart, int userId, CancellationToken ct) => QuoteTask ?? Task.FromResult(QuoteFactory(cart));
    public Task<Result<MvpReceipt>> ConfirmAsync(ConfirmSale request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, Sale.Sale.Id);
    public Task<Result<MvpReceipt>> SaveQuotationAsync(SaveQuotation request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 10);
    public Task<Result<MvpReceipt>> AuthorizeDiscountAsync(DiscountApproval request, int administratorId, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 20);
    public Task<QuotationData?> ReadQuotationAsync(long id, int userId, CancellationToken ct) => Task.FromResult<QuotationData?>(Quotations.FirstOrDefault(q => q.Id == id));
    public Task<MvpPage<QuotationData>> QuotationsAsync(MvpFilter filter, int userId, CancellationToken ct) => Task.FromResult(new MvpPage<QuotationData>(Quotations, Quotations.Count));
    public Task<SaleSnapshot?> ReadReceiptAsync(long id, int userId, CancellationToken ct) => ReceiptReadTask ?? Task.FromResult<SaleSnapshot?>(Sale);
    public Task<SaleSnapshot?> ReadSaleAsync(long id, int userId, CancellationToken ct) => Task.FromResult<SaleSnapshot?>(Sale);
    public Task<MvpPage<SaleData>> SalesAsync(MvpFilter filter, int userId, CancellationToken ct) => Task.FromResult(new MvpPage<SaleData>([Sale.Sale], 1));
    public Task<IReadOnlyList<CashBoxData>> CashBoxesAsync(int userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<CashBoxData>>(Boxes);
    public Task<CashShiftData?> ReadShiftAsync(int boxId, int userId, CancellationToken ct) => Task.FromResult(Shift?.State == "ABIERTO" ? Shift : null);
    public Task<CashShiftData?> ReadShiftByIdAsync(long id, int userId, CancellationToken ct) => Task.FromResult(Shift);
    public Task<MvpPage<CashMovementData>> CashMovementsAsync(long shiftId, MvpFilter filter, int userId, CancellationToken ct) => Task.FromResult(new MvpPage<CashMovementData>(Movements, Movements.Count));
    public Task<Result<MvpReceipt>> SaveBoxAsync(CashBoxChange request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 1);
    public Task<Result<MvpReceipt>> OpenShiftAsync(OpenCashShift request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 1);
    public Task<Result<MvpReceipt>> MoveCashAsync(CashMovement request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 1);
    public Task<Result<MvpReceipt>> CloseShiftAsync(CloseCashShift request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 1);
    public Task<MvpPage<CreditAccountData>> AccountsAsync(bool overdueOnly, MvpFilter filter, int userId, CancellationToken ct) => Task.FromResult(new MvpPage<CreditAccountData>([Account], 1));
    public Task<Result<MvpReceipt>> PayAccountAsync(AccountPayment request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 30);
    public Task<Result<MvpReceipt>> PlaceOrderAsync(PlaceOrder request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, Order.Order.Id);
    public Task<Result<MvpReceipt>> TransitionOrderAsync(OrderTransition request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, Order.Order.Id);
    public Task<Result<MvpReceipt>> LinkSaleAsync(LinkOrderSale request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, Order.Order.Id);
    public Task<OrderSnapshot?> ReadOrderAsync(long id, int userId, CancellationToken ct) => OrderReadTask ?? Task.FromResult<OrderSnapshot?>(Order);
    public Task<MvpPage<OrderData>> OrdersAsync(string? state, MvpFilter filter, int userId, CancellationToken ct) => Task.FromResult(new MvpPage<OrderData>([Order.Order], 1));
    public Task<MvpPage<OrderEventData>> OrderEventsAsync(long id, MvpFilter filter, int userId, CancellationToken ct) => Task.FromResult(new MvpPage<OrderEventData>(Order.Events, Order.TotalEvents));
    public Task<Result<MvpReceipt>> InitialInventoryAsync(InitialInventory request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 1);
    public Task<Result<MvpReceipt>> SaveBarcodeAsync(BarcodeChange request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 40);
    public CommercialParameters Parameters { get; set; } = new(7, 35, [1,2,3,4,5,6,7,8], [8,7,6,5,4,3,2,1]);
    public Task<CommercialParameters>? ParametersTask { get; set; }
    public Task<CommercialParameters> ReadParametersAsync(int userId, CancellationToken ct) => ParametersTask ?? Task.FromResult(Parameters);
    public Task<Result<MvpReceipt>> ChangeParametersAsync(ChangeCommercialParameters request, int userId, int? workstationId, CancellationToken ct) => Write(request, request.OperationId, 50);
    public List<BarcodeData> Barcodes { get; } = [];
    public Task<ProductBarcodes> ReadBarcodesAsync(int productId, int userId, CancellationToken ct) => Task.FromResult(new ProductBarcodes([1,2,3,4,5,6,7,8], Barcodes));
    public Task<OperationResolution> ResolveOperationAsync(OperationTicket ticket, bool rejectIfAbsent, int userId, int? workstationId, CancellationToken ct) => Task.FromResult(FoundReceipt is not null && FoundReceipt.OperationId == ticket.OperationId ? new OperationResolution(OperationResolutionState.Confirmed, FoundReceipt) : new(rejectIfAbsent ? OperationResolutionState.RejectedDefinitively : OperationResolutionState.Unconfirmed));
    public Task<MvpReceipt?> FindOperationAsync(Guid id, int userId, CancellationToken ct) => Task.FromResult(FoundReceipt);
}
