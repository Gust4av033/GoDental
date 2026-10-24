namespace GoDental.Application.DTOs;

public sealed record MvpReceipt(long Id, Guid OperationId);
public sealed record MvpPage<T>(IReadOnlyList<T> Items, int Total);
public sealed record MvpFilter(int Page = 1, int PageSize = 30);
public sealed record SaleItem(int ProductId, decimal Quantity, decimal DiscountPercent = 0, long? LotId = null);
public sealed record SaleCart(int CashBoxId, long? CustomerId, IReadOnlyList<SaleItem> Items, decimal GlobalDiscountPercent = 0,
    int? PriceListId = null, decimal DeliveryCharge = 0);
public sealed record SalePayment(short MethodId, decimal Amount, string? Reference = null);
public sealed record SaleOrderOrigin(long OrderId, byte[] ExpectedVersion);
public sealed record ConfirmSale(Guid OperationId, SaleCart Cart, string QuoteToken, IReadOnlyList<SalePayment> Payments,
    decimal CashTendered, long? ShiftId, string Reason, long? DiscountAuthorizationId = null, long? QuotationId = null, SaleOrderOrigin? Order = null);
public sealed record SaveQuotation(Guid OperationId, SaleCart Cart, string QuoteToken, DateTime? ValidUntil, string Reason, long? DiscountAuthorizationId = null);
public sealed record DiscountApproval(Guid OperationId, SaleCart Cart, string QuoteToken, string Reason);
public sealed record InitialInventory(Guid OperationId, int ProductId, int WarehouseId, decimal Quantity, decimal UnitCost,
    string Reason, string? LotNumber = null, DateTime? Expires = null, DateTime? Manufactured = null);
public sealed record BarcodeChange(Guid OperationId, int ProductId, byte[] ExpectedVersion, string? Code, string Type, bool Active, string Reason);
public sealed record CashBoxChange(Guid OperationId, int Id, string Code, string Name, int WarehouseId, bool Active, string Reason);
public sealed record OpenCashShift(Guid OperationId, int CashBoxId, decimal OpeningAmount, string Reason);
public sealed record CashMovement(Guid OperationId, long ShiftId, byte[] ExpectedVersion, string Type, decimal Amount, string Reason);
public sealed record CloseCashShift(Guid OperationId, long ShiftId, byte[] ExpectedVersion, decimal Counted, string Reason);
public sealed record AccountPayment(Guid OperationId, long AccountId, byte[] ExpectedVersion, short MethodId, decimal Amount,
    string? Reference, long? ShiftId, string Reason);
public sealed class CashBoxData
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; }
}
public sealed class CashShiftData
{
    public long Id { get; set; }
    public int CashBoxId { get; set; }
    public DateTimeOffset Opened { get; set; }
    public DateTimeOffset? Closed { get; set; }
    public string State { get; set; } = "";
    public decimal Opening { get; set; }
    public decimal Expected { get; set; }
    public decimal? Counted { get; set; }
    public decimal? Difference { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CashMovementData
{
    public long Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}
public sealed class PaymentMethodData
{
    public short Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool AffectsCash { get; set; }
    public bool RequiresReference { get; set; }
}
public sealed class PosProductData
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public decimal Available { get; set; }
    public bool TracksLot { get; set; }
}
public sealed record LotAllocation(long? LotId, decimal Quantity);
public sealed record CalculatedPrice(int ProductId, string Sku, string Name, decimal Quantity, decimal PublicPrice,
    decimal UnitPrice, decimal NetUnitPrice, decimal Subtotal, decimal Discount, decimal Tax, decimal Total,
    decimal EffectiveDiscountPercent, int? PriceListId, long? PromotionId, IReadOnlyList<LotAllocation> Lots);
public sealed record SaleQuote(SaleCart Cart, IReadOnlyList<CalculatedPrice> Lines, decimal Subtotal, decimal Discount,
    decimal Tax, decimal Total, decimal JustificationThreshold, decimal AuthorizationThreshold, string Token);
public sealed class SaleData
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public long? CustomerId { get; set; }
    public string CustomerSnapshot { get; set; } = "";
    public int CashBoxId { get; set; }
    public long? ShiftId { get; set; }
    public DateTimeOffset Date { get; set; }
    public string State { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal Pending { get; set; }
    public decimal CashTendered { get; set; }
    public decimal Change { get; set; }
    public decimal DeliveryCharge { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class SaleLineData
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public long? LotId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? PricingTrace { get; set; }
}
public sealed class SalePaymentData
{
    public short MethodId { get; set; }
    public string Method { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
}
public sealed record SaleSnapshot(SaleData Sale, IReadOnlyList<SaleLineData> Lines, IReadOnlyList<SalePaymentData> Payments, OrderData? Delivery = null);
public sealed record ReceiptDocument(byte[] Pdf, IReadOnlyList<byte[]> Pages,
    decimal WidthMm = 215.9m, decimal HeightMm = 279.4m, decimal MarginMm = 11.3m, bool IsLabel = false);
public sealed class QuotationData
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public long? CustomerId { get; set; }
    public DateTimeOffset Date { get; set; }
    public decimal Total { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string State { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CreditAccountData
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public string Sale { get; set; } = "";
    public DateTime Due { get; set; }
    public DateTime? NextNotice { get; set; }
    public bool NoticeDue { get; set; }
    public decimal Original { get; set; }
    public decimal Balance { get; set; }
    public bool Overdue { get; set; }
    public string State { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
}
public sealed record OrderItem(int ProductId, decimal Quantity);
public sealed record PlaceOrder(Guid OperationId, long CustomerId, string Contact, string Phone, string Address,
    string? Department, string? Municipality, string? Reference, DateTime? Scheduled, string? Responsible,
    int? ResponsibleUserId, decimal DeliveryCharge, IReadOnlyList<OrderItem> Items, string Reason, long? CustomerAddressId = null);
public sealed record OrderTransition(Guid OperationId, long OrderId, byte[] ExpectedVersion, string State, string Reason);
public sealed record LinkOrderSale(Guid OperationId, long OrderId, byte[] ExpectedVersion, long SaleId, string Reason);
public sealed class OrderData
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Department { get; set; }
    public string? Municipality { get; set; }
    public string? District { get; set; }
    public string? Reference { get; set; }
    public DateTime? Scheduled { get; set; }
    public string? Responsible { get; set; }
    public int? ResponsibleUserId { get; set; }
    public decimal DeliveryCharge { get; set; }
    public long? SaleId { get; set; }
    public string State { get; set; } = "";
    public DateTimeOffset? Delivered { get; set; }
    public int? DeliveredBy { get; set; }
    public decimal? Pending { get; set; }
    public string PaymentState { get; set; } = "SIN_VENTA";
    public byte[] RowVersion { get; set; } = [];
}
public sealed class OrderLineData
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
}
public sealed class OrderEventData
{
    public DateTimeOffset Date { get; set; }
    public int UserId { get; set; }
    public string State { get; set; } = "";
    public string Reason { get; set; } = "";
}
public sealed record OrderSnapshot(OrderData Order, IReadOnlyList<OrderLineData> Lines, IReadOnlyList<OrderEventData> Events, int TotalEvents);
public sealed record PendingFiscal(Guid OperationId, long SaleId, string Type, string Reason);
public sealed class PendingFiscalData
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public string Type { get; set; } = "";
    public string State { get; set; } = "";
    public DateTimeOffset Date { get; set; }
}
