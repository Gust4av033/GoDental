using GoDental.Domain.Rules;

namespace GoDental.Application.DTOs;

public sealed class InventoryCountData
{
    public long Id { get; set; }
    public int WarehouseId { get; set; }
    public string Warehouse { get; set; } = "";
    public string State { get; set; } = "";
    public DateTimeOffset Opened { get; set; }
    public DateTimeOffset? Closed { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? ClosedBy { get; set; }
    public string? Notes { get; set; }
    public int TotalLines { get; set; }
    public int CountedLines { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class InventoryCountLineData
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Product { get; set; } = "";
    public long? LotId { get; set; }
    public string? Lot { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public bool Counted { get; set; }
    public decimal? Difference => Counted ? InventoryCountRules.Difference(SystemQuantity, CountedQuantity) : null;
    public bool AdjustmentGenerated { get; set; }
    public bool RequiresRecount { get; set; }
}

public sealed record InventoryCountSnapshot(InventoryCountData Header, IReadOnlyList<InventoryCountLineData> Lines);
public sealed record InventoryCountFilter(int? WarehouseId = null, string? State = null, int Page = 1, int PageSize = 30);
public sealed record InventoryCountPage(IReadOnlyList<InventoryCountData> Items, int Total);
public sealed record StartInventoryCount(int WarehouseId, IReadOnlyList<int> ProductIds, string Reason);
public sealed record InventoryCountQuantity(long LineId, decimal Quantity);
public sealed record CaptureInventoryCount(long CountId, byte[] ExpectedVersion, IReadOnlyList<InventoryCountQuantity> Lines);
public sealed record FinalizeInventoryCount(long CountId, byte[] ExpectedVersion, string Reason);
