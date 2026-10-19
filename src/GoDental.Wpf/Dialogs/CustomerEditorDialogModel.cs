using System.Globalization;
using GoDental.Application.DTOs;
namespace GoDental.Wpf.Dialogs;

public enum CustomerEditorKind { General, Contact, Address, Credit }
public sealed class CustomerEditorDialogModel : OperationDialogModel
{
    public CustomerEditorKind Kind { get; }
    public bool IsGeneral => Kind == CustomerEditorKind.General;
    public bool IsContact => Kind == CustomerEditorKind.Contact;
    public bool IsAddress => Kind == CustomerEditorKind.Address;
    public bool IsCredit => Kind == CustomerEditorKind.Credit;
    public override string Title => Kind switch { CustomerEditorKind.General => "Ficha del cliente", CustomerEditorKind.Contact => "Contacto del cliente", CustomerEditorKind.Address => "Dirección del cliente", _ => "Configurar crédito del cliente" };
    public CustomerData Customer { get; }
    public CustomerContactData Contact { get; }
    public CustomerAddressData Address { get; }
    public CustomerCreditData Credit { get; }
    public IReadOnlyList<CatalogOption> Types { get; }
    public IReadOnlyList<CatalogOption> PriceLists { get; }
    public IReadOnlyList<string> AddressTypes { get; } = ["FISCAL", "ENTREGA", "OTRA"];
    public int PriceListId { get; set; }
    public string Limit { get; set; }
    public string Days { get; set; }
    public string NoticeDays { get; set; }
    public CustomerEditorDialogModel(CustomerEditorKind kind, CustomerData customer, CustomerContactData? contact,
        CustomerAddressData? address, CustomerCreditData? credit, CustomerCatalogs catalogs,
        Func<OperationDialogModel, Task<bool>> save) : base(save)
    {
        Kind = kind; Customer = customer with { RowVersion = customer.RowVersion.ToArray() };
        Contact = (contact ?? new()) with { }; Address = (address ?? new()) with { }; Credit = (credit ?? new()) with { };
        var types = catalogs.Types.ToList();
        if (Customer.TypeId > 0 && types.All(t => t.Id != Customer.TypeId)) types.Add(new() { Id = Customer.TypeId, Name = Customer.Type });
        Types = types;
        if (Customer.Id == 0 && Customer.TypeId == 0 && types.Count != 0) Customer.TypeId = checked((short)types[0].Id);
        var lists = catalogs.PriceLists.ToList();
        if (Customer.PriceListId is { } id && lists.All(l => l.Id != id)) lists.Add(new() { Id = id, Name = Customer.PriceList ?? "Lista histórica" });
        lists.Insert(0, new() { Id = 0, Name = "Sin lista asignada" }); PriceLists = lists; PriceListId = Customer.PriceListId ?? 0;
        Limit = Credit.Limit?.ToString(CultureInfo.InvariantCulture) ?? "";
        Days = Credit.Days?.ToString(CultureInfo.InvariantCulture) ?? "";
        NoticeDays = Credit.NoticeDays?.ToString(CultureInfo.InvariantCulture) ?? "";
    }
    public CustomerData BuildCustomer() => Customer with { PriceListId = PriceListId == 0 ? null : PriceListId };
    public CustomerCreditData? BuildCredit()
    {
        decimal? limit = null; short? days = null; short? notice = null;
        if ((!string.IsNullOrWhiteSpace(Limit) && !decimal.TryParse(Limit, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
          || (!string.IsNullOrWhiteSpace(Days) && !short.TryParse(Days, NumberStyles.None, CultureInfo.InvariantCulture, out _))
          || (!string.IsNullOrWhiteSpace(NoticeDays) && !short.TryParse(NoticeDays, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        { StatusMessage = "Revisa el límite con punto decimal y los plazos en días enteros."; return null; }
        if (!string.IsNullOrWhiteSpace(Limit)) limit = decimal.Parse(Limit, CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(Days)) days = short.Parse(Days, CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(NoticeDays)) notice = short.Parse(NoticeDays, CultureInfo.InvariantCulture);
        return Credit with { Limit = limit, Days = days, NoticeDays = notice };
    }
}
