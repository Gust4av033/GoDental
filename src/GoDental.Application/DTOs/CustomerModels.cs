namespace GoDental.Application.DTOs;

public sealed record CustomerData
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public short TypeId { get; set; }
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string? CommercialName { get; set; }
    public string? Nit { get; set; }
    public string? Nrc { get; set; }
    public string? Dui { get; set; }
    public string? BusinessActivity { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Email { get; set; }
    public int? PriceListId { get; set; }
    public string? PriceList { get; set; }
    public bool Active { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
public sealed record CustomerContactData
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Position { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Email { get; set; }
    public bool Primary { get; set; }
    public bool Active { get; set; } = true;
}
public sealed record CustomerAddressData
{
    public long Id { get; set; }
    public string Type { get; set; } = "ENTREGA";
    public string? Department { get; set; }
    public string? Municipality { get; set; }
    public string? District { get; set; }
    public string Address { get; set; } = "";
    public string? Reference { get; set; }
    public bool Primary { get; set; }
    public bool Active { get; set; } = true;
}
public sealed record CustomerCreditData
{
    public bool Enabled { get; set; }
    public decimal? Limit { get; set; }
    public short? Days { get; set; } = 15;
    public short? NoticeDays { get; set; } = 5;
    public bool Blocked { get; set; }
    public string? BlockReason { get; set; }
}
public sealed record CustomerSearch(string Search = "", bool IncludeInactive = false, short? TypeId = null, int Page = 1, int PageSize = 30);
public sealed record CustomerPage(IReadOnlyList<CustomerData> Items, int Total);
public sealed record CustomerCatalogs(IReadOnlyList<CatalogOption> Types, IReadOnlyList<CatalogOption> PriceLists);
public sealed record CustomerSnapshot(CustomerData Customer, CustomerCreditData Credit, IReadOnlyList<CustomerContactData> Contacts, IReadOnlyList<CustomerAddressData> Addresses);
public sealed record CustomerChange(CustomerData Customer, string Reason);
public sealed record CustomerContactChange(long CustomerId, byte[] ExpectedVersion, CustomerContactData Contact, string Reason);
public sealed record CustomerAddressChange(long CustomerId, byte[] ExpectedVersion, CustomerAddressData Address, string Reason);
public sealed record CustomerCreditChange(long CustomerId, byte[] ExpectedVersion, CustomerCreditData Credit, string Reason);
public sealed record CustomerHistoryFilter(long CustomerId, int Page = 1, int PageSize = 30);
public sealed record CustomerHistoryPage<T>(IReadOnlyList<T> Items, int Total);
public sealed class CustomerSaleData
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateTimeOffset Date { get; set; }
    public string State { get; set; } = "";
    public decimal Total { get; set; }
    public decimal Pending { get; set; }
}
public sealed class CustomerAccountData
{
    public long Id { get; set; }
    public string Sale { get; set; } = "";
    public DateTime Issued { get; set; }
    public DateTime Due { get; set; }
    public decimal Original { get; set; }
    public decimal Balance { get; set; }
    public string State { get; set; } = "";
}
public sealed class CustomerPaymentData
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string Receipt { get; set; } = "";
    public DateTimeOffset Date { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public bool Voided { get; set; }
}
public sealed class CustomerBalanceData
{
    public decimal Outstanding { get; set; }
    public decimal Overdue { get; set; }
    public decimal Available { get; set; }
}
