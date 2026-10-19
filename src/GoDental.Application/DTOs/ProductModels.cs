namespace GoDental.Application.DTOs;

public sealed class ProductData
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public short UnitId { get; set; }
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Price { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal? MaximumStock { get; set; }
    public decimal AvailableStock { get; set; }
    public bool TracksLot { get; set; }
    public bool TracksExpiry { get; set; }
    public short? ExpiryAlertDays { get; set; }
    public bool TracksSerial { get; set; }
    public bool Active { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CatalogOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}
public sealed record ProductCatalogs(IReadOnlyList<CatalogOption> Categories, IReadOnlyList<CatalogOption> Brands, IReadOnlyList<CatalogOption> Units);
public sealed record ProductSearch(string Search = "", bool IncludeInactive = false, int Page = 1, int PageSize = 30, int? CategoryId = null, int? BrandId = null, bool LowStockOnly = false);
public sealed record ProductPage(IReadOnlyList<ProductData> Items, int Total);

