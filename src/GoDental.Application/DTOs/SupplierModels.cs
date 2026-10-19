namespace GoDental.Application.DTOs;

public sealed record SupplierData
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Country { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public bool Active { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}

public sealed record SupplierContactData
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Position { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool Primary { get; set; }
    public bool Active { get; set; } = true;
}

public sealed record SupplierProductData
{
    public int ProductId { get; set; }
    public string LocalSku { get; set; } = "";
    public string Product { get; set; } = "";
    public string? SupplierSku { get; set; }
    public decimal? LastCost { get; set; }
    public short? CurrencyId { get; set; }
    public string? Currency { get; set; }
    public decimal? MinimumQuantity { get; set; }
    public short? LeadTimeDays { get; set; }
    public bool Preferred { get; set; }
    public bool Active { get; set; } = true;
}

public sealed record SupplierSnapshot(SupplierData Supplier, IReadOnlyList<SupplierContactData> Contacts, IReadOnlyList<SupplierProductData> Products);
public sealed record SupplierSearch(string Search = "", bool IncludeInactive = false, int Page = 1, int PageSize = 30);
public sealed record SupplierPage(IReadOnlyList<SupplierData> Items, int Total);
public sealed record SupplierChange(SupplierData Supplier, string Reason);
public sealed record SupplierContactChange(int SupplierId, byte[] ExpectedSupplierVersion, SupplierContactData Contact, string Reason);
public sealed record SupplierProductChange(int SupplierId, byte[] ExpectedSupplierVersion, SupplierProductData Product, string Reason);
