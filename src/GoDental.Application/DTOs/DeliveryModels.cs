namespace GoDental.Application.DTOs;
public sealed record DispatchDelivery(Guid OperationId,long OrderId,byte[] ExpectedVersion,int CashBoxId,string QuoteToken,string Courier,string Reason);
public sealed record CompleteDelivery(Guid OperationId,long OrderId,byte[] ExpectedVersion,string Outcome,string? Medium,string? Reference,decimal Amount,bool AllRecovered,string Reason);
public sealed record SettleDelivery(Guid OperationId,long OrderId,byte[] ExpectedVersion,long? ShiftId,decimal Amount,string? Reference,string Reason);
public sealed class DeliveryData
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public int CashBoxId { get; set; }
    public int WarehouseId { get; set; }
    public string Courier { get; set; } = "";
    public string State { get; set; } = "";
    public decimal Total { get; set; }
    public string Snapshot { get; set; } = "";
    public string CustomerSnapshot { get; set; } = "";
    public long? SaleId { get; set; }
    public string? Medium { get; set; }
    public string? Reference { get; set; }
    public DateTimeOffset Dispatched { get; set; }
    public DateTimeOffset? Delivered { get; set; }
    public DateTimeOffset? Settled { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
