namespace GoDental.Application.DTOs;

public sealed record PurchaseLine(int ProductId, decimal Quantity, decimal UnitCost, decimal Discount = 0);
public sealed record SavePurchaseOrder(Guid OperationId, long Id, byte[] ExpectedVersion, int SupplierId, short CurrencyId,
    decimal EstimatedRate, DateTime Date, IReadOnlyList<PurchaseLine> Lines, string Reason);
public sealed record PurchaseTransition(Guid OperationId, long OrderId, byte[] ExpectedVersion, string State, string Reason);
public sealed record ReceivePurchaseLine(long OrderLineId, decimal Quantity, decimal ExtraUnitCostUsd = 0,
    string? LotNumber = null, DateTime? Expires = null, DateTime? Manufactured = null);
public sealed record PreparePurchaseReceipt(Guid OperationId, long OrderId, byte[] ExpectedOrderVersion, int WarehouseId,
    decimal ExchangeRate, bool CostsComplete, IReadOnlyList<ReceivePurchaseLine> Lines, string Reason);
public sealed record PurchaseReceiptAction(Guid OperationId, long ReceiptId, byte[] ExpectedVersion,
    byte[] ExpectedOrderVersion, bool Apply, string Reason);
public sealed record PurchaseCatalogs(IReadOnlyList<CatalogOption> Suppliers, IReadOnlyList<PurchaseCurrency> Currencies,
    IReadOnlyList<CatalogOption> Warehouses);
public sealed class PurchaseCurrency
{
    public short Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
public sealed class PurchaseOrderData
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public int SupplierId { get; set; }
    public string Supplier { get; set; } = "";
    public short CurrencyId { get; set; }
    public string Currency { get; set; } = "";
    public decimal EstimatedRate { get; set; }
    public DateTime Date { get; set; }
    public string State { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class PurchaseOrderLineData
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Received { get; set; }
    public decimal Pending => Quantity - Received;
    public decimal UnitCost { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public bool TracksLot { get; set; }
    public bool TracksExpiry { get; set; }
}
public sealed class PurchaseReceiptData
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public long OrderId { get; set; }
    public int WarehouseId { get; set; }
    public string Warehouse { get; set; } = "";
    public DateTimeOffset Date { get; set; }
    public string State { get; set; } = "";
    public decimal? ExchangeRate { get; set; }
    public bool CostsComplete { get; set; }
    public string User { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
}
public sealed class PurchaseReceiptLineData
{
    public long Id { get; set; }
    public long OrderLineId { get; set; }
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal PurchaseCostUsd { get; set; }
    public decimal ExtraUnitCostUsd { get; set; }
    public decimal LandedUnitCost { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? Expires { get; set; }
    public DateTime? Manufactured { get; set; }
}
public sealed record PurchaseOrderSnapshot(PurchaseOrderData Order, IReadOnlyList<PurchaseOrderLineData> Lines, IReadOnlyList<PurchaseReceiptData> Receipts);
public sealed record PurchaseReceiptSnapshot(PurchaseReceiptData Receipt, IReadOnlyList<PurchaseReceiptLineData> Lines);
