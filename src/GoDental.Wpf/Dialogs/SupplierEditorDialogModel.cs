using System.Collections.ObjectModel;
using System.Globalization;
using GoDental.Application.DTOs;
using GoDental.Application.Localization;
using GoDental.Application.Validators;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Dialogs;

public enum SupplierEditorKind { Supplier, Contact, Product }
public sealed class SupplierEditorDialogModel : OperationDialogModel
{
    public SupplierEditorKind Kind { get; }
    public bool IsSupplier => Kind == SupplierEditorKind.Supplier;
    public string CodeDisplay => string.IsNullOrWhiteSpace(Supplier.Code) ? "Se asigna al guardar" : Supplier.Code;
    public bool IsContact => Kind == SupplierEditorKind.Contact;
    public bool IsProduct => Kind == SupplierEditorKind.Product;
    public override string Title => Kind switch { SupplierEditorKind.Supplier => "Ficha del proveedor", SupplierEditorKind.Contact => "Contacto del proveedor", _ => "Referencia comercial del producto" };
    public SupplierData Supplier { get; }
    public SupplierContactData Contact { get; }
    public SupplierProductData Product { get; }
    public ProductPickerViewModel Picker { get; }
    public IReadOnlyList<CatalogOption> Currencies { get; }
    public int? CurrencyId { get; set; }
    public string Cost { get; set; }
    public string Minimum { get; set; }
    public string LeadTime { get; set; }
    public bool IsNewProduct => IsProduct && Product.ProductId == 0;
    public SupplierEditorDialogModel(SupplierEditorKind kind, SupplierData supplier, SupplierContactData? contact,
        SupplierProductData? product, ProductPickerViewModel picker, IReadOnlyList<CatalogOption> currencies,
        Func<OperationDialogModel, Task<bool>> save) : base(save)
    {
        originalSupplier = supplier with { }; originalContact = contact is null ? null : contact with { };
        Kind = kind; Supplier = supplier with { RowVersion = supplier.RowVersion.ToArray() };
        Contact = (contact ?? new()) with { }; Product = (product ?? new()) with { };
        var countries = DirectoryCatalog.Countries.ToList();
        countries.Insert(0, new("", "Sin especificar", []));
        var existingCountry = DirectoryCatalog.Country(Supplier.Country);
        if (existingCountry is null && !string.IsNullOrWhiteSpace(Supplier.Country)) { existingCountry = new("ZZ", Supplier.Country, []); countries.Add(existingCountry); }
        Countries = countries; selectedCountry = existingCountry ?? countries[0];
        Picker = picker;
        var options = currencies.ToList();
        if (Product.CurrencyId is { } id && options.All(c => c.Id != id)) options.Add(new() { Id = id, Name = Product.Currency ?? "Moneda histórica" });
        options.Insert(0, new() { Id = 0, Name = "Sin moneda" });
        Currencies = options; CurrencyId = Product.CurrencyId ?? 0;
        Cost = Product.LastCost?.ToString(CultureInfo.InvariantCulture) ?? "";
        Minimum = Product.MinimumQuantity?.ToString(CultureInfo.InvariantCulture) ?? "";
        LeadTime = Product.LeadTimeDays?.ToString(CultureInfo.InvariantCulture) ?? "";
    }
    private readonly SupplierData originalSupplier;
    private readonly SupplierContactData? originalContact;
    public IReadOnlyList<CountryOption> Countries { get; }
    private CountryOption selectedCountry;
    public CountryOption SelectedCountry { get => selectedCountry; set { if (value is null || !SetProperty(ref selectedCountry, value)) return; Supplier.Country = value.Code == "" ? null : value.Name; OnPropertyChanged(nameof(PhoneRegion)); } }
    public string? PhoneRegion => DirectoryCatalog.Country(Supplier.Country)?.Code;
    public bool PrepareFields()
    {
        var error = IsSupplier ? DirectoryFields.SupplierError(Supplier, Supplier.Id == 0 ? null : originalSupplier) : IsContact ? PhoneFormat.Error(Contact.Phone, originalContact?.Phone, PhoneRegion, originalContact is null) : null;
        if (error is null) return true;
        StatusMessage = error; return false;
    }
    public SupplierProductData? BuildProduct()
    {
        if (!OptionalDecimal(Cost, out var cost) || !OptionalDecimal(Minimum, out var minimum)
            || (!string.IsNullOrWhiteSpace(LeadTime) && !short.TryParse(LeadTime, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        { StatusMessage = "Revisa costo, MOQ y plazo. Usa punto decimal sin separadores de miles."; return null; }
        var ids = Picker.SelectedIds;
        if (IsNewProduct && ids.Count != 1) { StatusMessage = "Selecciona exactamente un producto para la referencia."; return null; }
        if (CurrencyId is < 0 or > short.MaxValue) { StatusMessage = "Selecciona una moneda válida."; return null; }
        return Product with { ProductId = IsNewProduct ? ids[0] : Product.ProductId, LastCost = cost,
            MinimumQuantity = minimum, CurrencyId = CurrencyId is > 0 ? (short?)CurrencyId : null,
            LeadTimeDays = string.IsNullOrWhiteSpace(LeadTime) ? null : short.Parse(LeadTime, CultureInfo.InvariantCulture) };
    }
    private static bool OptionalDecimal(string value, out decimal? number)
    {
        number = null; if (string.IsNullOrWhiteSpace(value)) return true;
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)) return false;
        number = n; return true;
    }
}
