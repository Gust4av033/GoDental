using GoDental.Application.DTOs;
using GoDental.Application.Localization;
using GoDental.Application.Validators;
using GoDental.Domain.Rules;
using GoDental.Wpf.Dialogs;
namespace GoDental.Tests.Common;

public sealed class DirectoryFieldsTests
{
    [Fact] public void CatalogContainsCurrentDivisionAndVerifiedNames()
    {
        Assert.Equal(14, DirectoryCatalog.Departments.Count);
        Assert.Equal(44, DirectoryCatalog.Departments.Sum(d => d.Municipalities.Length));
        Assert.Equal(262, DirectoryCatalog.Departments.Sum(d => d.Municipalities.Sum(m => m.Districts.Length)));
        Assert.Null(DirectoryCatalog.LocationError("Cuscatlán", "Cuscatlán Norte", "San Bartolomé Perulapía"));
        Assert.Null(DirectoryCatalog.LocationError("La Unión", "La Unión Norte", "San José"));
        Assert.NotNull(DirectoryCatalog.LocationError("San Salvador", "Santa Ana Centro", null));
        Assert.NotNull(DirectoryCatalog.LocationError("San Salvador", "San Salvador Centro", "Santa Tecla"));
        Assert.Equal("CN", DirectoryCatalog.Country("China")?.Code);
        Assert.Equal("SV", DirectoryCatalog.Country("el salvador")?.Code);
        Assert.True(DirectoryCatalog.Countries.Count >= 249);
    }
    [Theory]
    [InlineData("70000001", "SV", false)]
    [InlineData("+50370000001", "CN", true)]
    [InlineData("13800138000", "CN", false)]
    [InlineData("+8613800138000", "SV", true)]
    [InlineData("+14155552671", null, true)]
    public void TelephoneKeepsOptionalPrefixAndDetectsInternationalRegion(string value, string? region, bool prefix)
    {
        Assert.True(PhoneFormat.TryFormat(value, region, out var result, out var hint));
        Assert.Equal(prefix, result!.StartsWith('+')); Assert.Contains("Número de", hint);
    }
    [Theory]
    [InlineData("123", "SV")]
    [InlineData("700000011111111111111", "SV")]
    [InlineData("abc70000001", "SV")]
    [InlineData("503+70000001", "SV")]
    [InlineData("++50370000001", "SV")]
    [InlineData("13800138000", null)]
    public void BadPhoneRequiresCorrection(string value, string? region) => Assert.False(PhoneFormat.TryFormat(value, region, out _, out _));
    [Theory]
    [InlineData("123456789", false, "12345678-9")]
    [InlineData("06140101001010", true, "0614-010100-101-0")]
    [InlineData("12345678-9", true, "12345678-9")]
    public void IdentityCanonicalFormat(string value, bool nit, string expected)
    { Assert.True(IdentityFormat.IsValid(value, nit)); Assert.Equal(expected, IdentityFormat.Normalize(value, nit)); }
    [Theory]
    [InlineData("1234567890", false)]
    [InlineData("061401010010100", true)]
    [InlineData("1234567", false)]
    [InlineData("A123456789", true)]
    public void RejectsExtraMissingOrNonAsciiIdentityDigits(string value, bool nit) => Assert.False(IdentityFormat.IsValid(value, nit));
    [Fact] public void UnchangedHistoricalFieldsArePreservedButEditedInvalidValuesFail()
    {
        var old = new CustomerData { Id = 1, Dui = "DUI viejo", Nit = "NIT viejo", Phone = " 123 ", WhatsApp = "456" };
        Assert.Null(DirectoryFields.CustomerError(old with { Name = "Nuevo nombre" }, old));
        Assert.Equal(old, DirectoryFields.Normalize(old, old));
        Assert.NotNull(DirectoryFields.CustomerError(old with { Phone = "12" }, old));
        Assert.NotNull(DirectoryFields.CustomerError(old with { Dui = "incompleto" }, old));
        var address = new CustomerAddressData { Id = 1, Department = "Antiguo", Municipality = "Antiguo municipio", Address = "Calle" };
        Assert.Null(DirectoryFields.AddressError(address with { Reference = "Nueva referencia" }, address));
        Assert.NotNull(DirectoryFields.AddressError(address with { Municipality = "Otro" }, address));
        var supplier = new SupplierData { Id = 1, Country = "País antiguo", Phone = "123" };
        Assert.Null(DirectoryFields.SupplierError(supplier with { Name = "Cambio" }, supplier));
        Assert.NotNull(DirectoryFields.SupplierError(supplier with { Country = "China" }, supplier));
    }
    [Fact] public void LocationSelectionCascadesAndPreservesLegacyUntilChanged()
    {
        var address = new CustomerAddressData { Id = 1, Department = "San Salvador", Municipality = "San Salvador", Address = "Calle" };
        var model = new AddressSelectionModel(address);
        Assert.True(model.HasLegacyLocation); Assert.Contains("San Salvador", model.Municipalities); Assert.Equal(address, model.Apply(address));
        model.Municipality = "San Salvador Centro"; model.District = "San Salvador";
        Assert.Null(DirectoryFields.AddressError(model.Apply(address), address));
        model.Department = "La Libertad";
        Assert.Equal(AddressSelectionModel.Unspecified, model.Municipality); Assert.Equal(AddressSelectionModel.Unspecified, model.District);
        Assert.DoesNotContain("San Salvador", model.Municipalities);
    }
    [Fact] public void ContactDefaultsToRegisteredAndOtherCreatesWithoutOverwriting()
    {
        var customer = new CustomerData { Id = 1, Name = "Cliente", Phone = "70000001", Email = "test@example.test" };
        var contact = new CustomerContactData { Id = 7, Name = "Compras", Phone = "70000002", Primary = true };
        var model = new CustomerEditorDialogModel(CustomerEditorKind.Contact, customer, null, null, null, new([], []), _ => Task.FromResult(false), [contact]);
        Assert.Equal(7, model.Contact.Id);
        model.Contact.Name = "Editar"; Assert.Equal("Compras", contact.Name);
        model.SelectedContactSource = model.ContactSources.Last();
        Assert.Equal(0, model.Contact.Id); Assert.Empty(model.Contact.Name); Assert.Null(model.Contact.Phone);
        model.SelectedContactSource = model.ContactSources[^2];
        Assert.Equal(0, model.Contact.Id); Assert.Equal(customer.Phone, model.Contact.Phone); Assert.Equal(customer.Name, model.Contact.Name);
        Assert.True(model.PrepareFields());
        model.SelectedContactSource = model.ContactSources.First(); Assert.Equal(7, model.Contact.Id); Assert.Equal("Compras", model.Contact.Name);
    }
    [Fact] public void ValidHistoricalIdentityIsFormattedForDisplayWithoutRewritingStoredValue()
    {
        var customer = new CustomerData { Id = 1, Dui = "123456789", Nit = "06140101001010" };
        var model = new CustomerEditorDialogModel(CustomerEditorKind.General, customer, null, null, null, new([], []), _ => Task.FromResult(false));
        Assert.Equal("12345678-9", model.DuiText); Assert.Equal("0614-010100-101-0", model.NitText);
        Assert.Equal(customer.Dui, model.BuildCustomer().Dui); Assert.Equal(customer.Nit, model.BuildCustomer().Nit);
    }
    [Fact] public void NitChoicePreventsAcceptingIncompleteFourteenDigitNitAsDui()
    {
        var model = new CustomerEditorDialogModel(CustomerEditorKind.General, new(), null, null, null, new([], []), _ => Task.FromResult(false));
        model.Customer.Nit = "123456789"; Assert.False(model.PrepareFields());
        model.NitDigits = 9; Assert.True(model.PrepareFields());
    }
}
