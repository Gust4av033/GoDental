namespace GoDental.Application.DTOs;

public sealed record ReturnEvidence(string Name, string MimeType, string RelativePath, string Sha256);
public sealed record ReturnItem(long SaleLineId, decimal Quantity, string Condition, bool Restock, string? NoRestockReason, IReadOnlyList<ReturnEvidence> Evidence);
public sealed record RequestSaleReturn(Guid OperationId, long SaleId, byte[] ExpectedSaleVersion, bool CancelSale, string Proof, string Reason, IReadOnlyList<ReturnItem> Items);
public sealed record ResolveSaleReturn(Guid OperationId, long SaleId, long ReturnId, byte[] ExpectedVersion, bool Apply, string QuoteToken, long? ShiftId, string Reason);
public sealed class SaleReturnData
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public long SaleId { get; set; }
    public DateTimeOffset Requested { get; set; }
    public string Requester { get; set; } = "";
    public string? Administrator { get; set; }
    public string State { get; set; } = "";
    public bool CancelSale { get; set; }
    public string Proof { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? Resolution { get; set; }
    public decimal Amount { get; set; }
    public decimal DebtReduction { get; set; }
    public decimal CashRefund { get; set; }
    public long? ShiftId { get; set; }
    public DateTimeOffset? Applied { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class ReturnableSaleLine
{
    public long Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Sold { get; set; }
    public decimal Total { get; set; }
    public decimal Returned { get; set; }
    public decimal Refunded { get; set; }
    public decimal Remaining => Sold - Returned;
}
public sealed class SaleReturnLineData
{
    public long Id { get; set; }
    public long SaleLineId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Condition { get; set; } = "";
    public bool Restock { get; set; }
    public string? NoRestockReason { get; set; }
    public decimal Amount { get; set; }
    public List<ReturnEvidence> Evidence { get; set; } = [];
}
public sealed record SaleReturnsSnapshot(IReadOnlyList<ReturnableSaleLine> Lines, IReadOnlyList<SaleReturnData> Returns, int Total, int Page, int WindowHours, DateTimeOffset ServerNow);
public sealed record SaleReturnSnapshot(SaleReturnData Return, IReadOnlyList<SaleReturnLineData> Lines, decimal CurrentAmount, decimal DebtReduction, decimal CashRefund, string QuoteToken, string? UnavailableReason);

public sealed record ReturnEvidenceLocation(string Root,byte[] RowVersion);
public sealed record ConfigureReturnEvidence(Guid OperationId,string Root,byte[] ExpectedVersion,string Reason);
