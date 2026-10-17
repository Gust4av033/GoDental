namespace GoDental.Application.DTOs;

public sealed class StockData
{
    public int WarehouseId { get; set; }
    public string Warehouse { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available => Quantity - Reserved;
    public decimal Sellable { get; set; }
    public decimal? AverageCost { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class LotData
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime? Manufactured { get; set; }
    public DateTime? Expires { get; set; }
    public decimal Quantity { get; set; }
    public decimal? EntryCost { get; set; }
    public bool Active { get; set; }
    public string Status { get; set; } = "";
    public override string ToString() => $"{Number} · {Status} · {Quantity:0.####}";
}
public sealed class MovementData
{
    public long Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Warehouse { get; set; } = "";
    public string? Lot { get; set; }
    public string Type { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Before { get; set; }
    public decimal After { get; set; }
    public decimal? Cost { get; set; }
    public string User { get; set; } = "";
    public string? Reference { get; set; }
    public string? Reason { get; set; }
}
public sealed record InventorySnapshot(IReadOnlyList<StockData> Stocks, IReadOnlyList<LotData> Lots);
public sealed record MovementFilter(int ProductId, int? WarehouseId = null, long? LotId = null,
    string? Type = null, DateTime? From = null, DateTime? Until = null, int Page = 1, int PageSize = 30);
public sealed record MovementPage(IReadOnlyList<MovementData> Items, int Total);
public sealed record InventoryAdjustment(int ProductId, int WarehouseId, decimal Quantity, decimal? UnitCost,
    string Type, string Reason, byte[] ExpectedVersion, long? LotId = null, string? NewLotNumber = null,
    DateTime? Manufactured = null, DateTime? Expires = null, int? DestinationWarehouseId = null);
public sealed record LotStateChange(long LotId, bool ExpectedActive, bool Active, string Reason);
