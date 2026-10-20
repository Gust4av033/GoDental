using GoDental.Application.DTOs;
using GoDental.Application.Validators;

namespace GoDental.Tests.Common;

public sealed class AutomaticCodeTests
{
    [Fact]
    public void NewMastersAllowAutomaticCodesButEditsNeedExistingIdentity()
    {
        var product = new ProductData { Name = "Producto", CategoryId = 1, UnitId = 1 };
        Assert.Null(ProductValidator.Validate(product));
        product.Id = 1; product.RowVersion = new byte[8];
        Assert.NotNull(ProductValidator.Validate(product));
        var customer = new CustomerChange(new() { Name = "Cliente", TypeId = 1 }, "Alta");
        Assert.Null(CustomerValidator.Validate(customer));
        Assert.NotNull(CustomerValidator.Validate(customer with { Customer = customer.Customer with { Id = 1, RowVersion = new byte[8] } }));
        var supplier = new SupplierChange(new() { Name = "Proveedor" }, "Alta");
        Assert.Null(SupplierValidator.Validate(supplier));
        Assert.NotNull(SupplierValidator.Validate(supplier with { Supplier = supplier.Supplier with { Id = 1, RowVersion = new byte[8] } }));
        var box = new CashBoxChange(Guid.NewGuid(), 0, "", "Caja", 1, true, "Alta");
        Assert.Null(MvpValidator.Validate(box));
        Assert.NotNull(MvpValidator.Validate(box with { Id = 1 }));
    }
}
