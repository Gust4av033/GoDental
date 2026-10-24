using System.Globalization;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using GoDental.Domain.Rules;
namespace GoDental.Wpf.Dialogs;

public enum CustomerEditorKind { General, Contact, Address, Credit }
public sealed class CustomerEditorDialogModel : OperationDialogModel
{
    public CustomerEditorKind Kind { get; }
    public bool IsGeneral => Kind == CustomerEditorKind.General;
    public string CodeDisplay => string.IsNullOrWhiteSpace(Customer.Code) ? "Se asigna al guardar" : Customer.Code;
    public bool IsContact => Kind == CustomerEditorKind.Contact;
    public bool IsAddress => Kind == CustomerEditorKind.Address;
    public bool IsCredit => Kind == CustomerEditorKind.Credit;
    public override string Title => Kind switch { CustomerEditorKind.General => "Ficha del cliente", CustomerEditorKind.Contact => "Contacto del cliente", CustomerEditorKind.Address => "Dirección del cliente", _ => "Configurar crédito del cliente" };
    public CustomerData Customer { get; }
    public CustomerContactData Contact { get; private set; }
    public CustomerAddressData Address { get; private set; }
    public CustomerCreditData Credit { get; }
    public IReadOnlyList<CatalogOption> Types { get; }
    public IReadOnlyList<CatalogOption> PriceLists { get; }
    public IReadOnlyList<string> AddressTypes { get; } = ["FISCAL", "ENTREGA", "OTRA"];
    public int PriceListId { get; set; }
    // SelectedValue entrega el Id del catálogo como int; el DTO utiliza smallint.
    public int SelectedTypeId { get => Customer.TypeId; set => Customer.TypeId = checked((short)value); }
    public string Limit { get; set; }
    public string Days { get; set; }
    public string NoticeDays { get; set; }
    public CustomerEditorDialogModel(CustomerEditorKind kind, CustomerData customer, CustomerContactData? contact,
        CustomerAddressData? address, CustomerCreditData? credit, CustomerCatalogs catalogs,
        Func<OperationDialogModel, Task<bool>> save, IReadOnlyList<CustomerContactData>? contacts = null) : base(save)
    {
        originalCustomer = customer with { };
        Kind = kind; Customer = customer with { RowVersion = customer.RowVersion.ToArray() };
        if (kind == CustomerEditorKind.General && customer.Id == 0) Reason = "Alta de cliente";
        Contact = (contact ?? new()) with { }; Address = (address ?? new()) with { }; Credit = (credit ?? new()) with { };
        Location = new(Address); originalAddress = Address with { };
        NitDigits = IdentityFormat.Digits(Customer.Nit).Length == 9 ? 9 : 14;
        var sources = (contacts ?? []).Select(c => new ContactSource($"Registrado: {c.Name}" + (c.Primary ? " · principal" : ""), c with { })).ToList();
        if (contact is not null && sources.All(x => x.Contact.Id != contact.Id)) sources.Add(new($"Registrado: {contact.Name}", contact with { }));
        var general = new ContactSource("Usar datos generales del cliente (nuevo contacto)", new() { Name = customer.Name, Phone = customer.Phone, WhatsApp = customer.WhatsApp, Email = customer.Email });
        sources.Add(general); sources.Add(new("Otro contacto (nuevo)", new())); ContactSources = sources;
        SelectedContactSource = contact is not null ? sources.First(x => x.Contact.Id == contact.Id) : sources.FirstOrDefault(x => x.Contact.Id > 0 && x.Contact.Active && x.Contact.Primary) ?? sources.FirstOrDefault(x => x.Contact.Id > 0 && x.Contact.Active) ?? general;
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
    private readonly CustomerData originalCustomer;
    private CustomerContactData? originalContact;
    private readonly CustomerAddressData originalAddress;
    public AddressSelectionModel Location { get; }
    public IReadOnlyList<int> NitFormats { get; } = [14, 9];
    private int nitDigits;
    public int NitDigits { get => nitDigits; set => SetProperty(ref nitDigits, value); }
    public IReadOnlyList<ContactSource> ContactSources { get; }
    private ContactSource? selectedContactSource;
    public ContactSource? SelectedContactSource
    {
        get => selectedContactSource;
        set
        {
            if (value is null || !SetProperty(ref selectedContactSource, value)) return;
            Contact = value.Contact with { }; originalContact = Contact.Id == 0 ? null : Contact with { };
            OnPropertyChanged(nameof(Contact)); OnPropertyChanged(nameof(ContactSourceHint));
        }
    }
    public string ContactSourceHint => Contact.Id > 0 ? "Se actualizará este contacto registrado; conserva su código. Elige Otro contacto para agregar uno." : "Se agregará otro contacto sin sustituir los registrados. Los datos opcionales pueden quedar vacíos.";
    public bool PrepareFields()
    {
        string? error = null;
        if (IsGeneral)
        {
            if (Customer.Nit != originalCustomer.Nit && !string.IsNullOrWhiteSpace(Customer.Nit) && IdentityFormat.Digits(Customer.Nit).Length != NitDigits) error = $"El formato de NIT seleccionado requiere {NitDigits} dígitos.";
            error ??= DirectoryFields.CustomerError(Customer, Customer.Id == 0 ? null : originalCustomer);
        }
        if (IsContact) error = DirectoryFields.ContactError(Contact, originalContact);
        if (IsAddress)
        {
            var localized = Location.Apply(Address);
            Address.Department = localized.Department; Address.Municipality = localized.Municipality; Address.District = localized.District;
            error = DirectoryFields.AddressError(Address, Address.Id == 0 ? null : originalAddress);
        }
        if (error is null) return true;
        StatusMessage = error; return false;
    }
    public sealed record ContactSource(string Name, CustomerContactData Contact);
    public string? DuiText { get => DisplayIdentity(Customer.Dui, false); set => Customer.Dui = value; }
    public string? NitText { get => DisplayIdentity(Customer.Nit, true); set => Customer.Nit = value; }
    private static string? DisplayIdentity(string? value, bool nit) => IdentityFormat.IsValid(value, nit) ? IdentityFormat.Normalize(value, nit) : value;
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
