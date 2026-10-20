using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;

namespace GoDental.Tests.Common;

public sealed class SupplierTests
{
    private static SupplierChange NewSupplier() => new(new() { Code = "SUP-001", Name = "Proveedor prueba" }, "Alta comercial");

    [Fact]
    public void SupplierAllowsAutomaticCodeAndRequiresNameReasonAndVersionForExistingRecords()
    {
        var request = NewSupplier();
        Assert.Null(SupplierValidator.Validate(request));
        Assert.Null(SupplierValidator.Validate(request with { Supplier = request.Supplier with { Code = " " } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Supplier = request.Supplier with { Id = 1, RowVersion = new byte[8], Code = " " } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Supplier = request.Supplier with { Name = new('a', 201) } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Reason = " " }));
        Assert.NotNull(SupplierValidator.Validate(request with { Supplier = request.Supplier with { Id = 1 } }));
        Assert.Null(SupplierValidator.Validate(request with { Supplier = request.Supplier with { Id = 1, RowVersion = new byte[8] } }));
    }

    [Theory]
    [InlineData("https://proveedor.example/catalogo", true)]
    [InlineData("http://proveedor.example/", true)]
    [InlineData("file:///C:/datos", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://usuario:clave@proveedor.example", false)]
    public void WebsiteAcceptsOnlyHttpWithoutCredentials(string website, bool valid)
    {
        var request = NewSupplier();
        Assert.Equal(valid, SupplierValidator.Validate(request with { Supplier = request.Supplier with { Website = website } }) is null);
    }

    [Fact]
    public void ContactsRequireValidAddressAndAnActivePrincipal()
    {
        var request = new SupplierContactChange(1, new byte[8], new() { Name = "Compras", Email = "compras@example.test", Primary = true }, "Contacto");
        Assert.Null(SupplierValidator.Validate(request));
        Assert.NotNull(SupplierValidator.Validate(request with { Contact = request.Contact with { Active = false } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Contact = request.Contact with { Email = "correo incorrecto" } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Contact = request.Contact with { Email = "Compras <compras@example.test>" } }));
        Assert.NotNull(SupplierValidator.Validate(request with { ExpectedSupplierVersion = [] }));
    }

    [Fact]
    public void CommercialCostRequiresCurrencyAndKeepsSqlPrecision()
    {
        var request = new SupplierProductChange(1, new byte[8], new() { ProductId = 1, LastCost = 1.234567m, CurrencyId = 1, MinimumQuantity = .0001m }, "Condiciones");
        Assert.Null(SupplierValidator.Validate(request));
        Assert.NotNull(SupplierValidator.Validate(request with { Product = request.Product with { LastCost = -.1m } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Product = request.Product with { CurrencyId = null } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Product = request.Product with { LastCost = .0000001m } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Product = request.Product with { MinimumQuantity = .00001m } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Product = request.Product with { MinimumQuantity = 0 } }));
        Assert.NotNull(SupplierValidator.Validate(request with { Product = request.Product with { LeadTimeDays = -1 } }));
        Assert.Null(SupplierValidator.Validate(request with { Product = request.Product with { LastCost = null, CurrencyId = null, MinimumQuantity = null } }));
    }

    [Fact]
    public async Task ServiceDeniesUnauthorizedWritesAndNormalizesOptionalFields()
    {
        var repository = new RecordingRepository();
        var session = new UserSession();
        var service = new SupplierService(repository, session);
        var request = NewSupplier();
        Assert.Equal("FORBIDDEN", (await service.SaveAsync(request)).ErrorCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(1));
        session.Begin(new(1, "consulta", "Consulta", 1, [], ["COMPRAS.VER"], false));
        Assert.True(service.CanRead); Assert.False(service.CanManage);
        Assert.Equal("FORBIDDEN", (await service.SaveContactAsync(new(1, new byte[8], new() { Name = "Compras" }, "Alta"))).ErrorCode);
        Assert.Equal("FORBIDDEN", (await service.SaveProductAsync(new(1, new byte[8], new() { ProductId = 1 }, "Alta"))).ErrorCode);
        Assert.Equal(0, repository.Writes);
        session.Begin(new(1, "admin", "Admin", 1, [], ["COMPRAS.VER", "PROVEEDORES.GESTIONAR"], false));
        Assert.Equal("VALIDATION", (await service.SaveAsync(request with { Reason = " " })).ErrorCode);
        Assert.True((await service.SaveAsync(request with { Supplier = request.Supplier with { Code = " SUP-001 ", Country = " ", Phone = " 123 " } })).IsSuccess);
        Assert.Equal("SUP-001", repository.Saved!.Supplier.Code); Assert.Null(repository.Saved.Supplier.Country);
        Assert.Equal("123", repository.Saved.Supplier.Phone); Assert.Equal("Alta comercial", repository.Saved.Reason);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new(Page: int.MaxValue)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new(Search: new('a', 201))));
        session.Clear(); Assert.False(service.CanRead);
    }

    private sealed class RecordingRepository : ISupplierRepository
    {
        public int Writes { get; private set; }
        public SupplierChange? Saved { get; private set; }
        public Task<SupplierPage> SearchAsync(SupplierSearch search, int userId, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<SupplierSnapshot?> ReadAsync(int supplierId, int userId, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<CatalogOption>> CurrenciesAsync(int userId, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<Result<int>> SaveAsync(SupplierChange request, int userId, int? workstationId, CancellationToken ct = default)
        { Writes++; Saved = request; return Task.FromResult(Result<int>.Success(1)); }
        public Task<Result<int>> SaveContactAsync(SupplierContactChange request, int userId, int? workstationId, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<Result<int>> SaveProductAsync(SupplierProductChange request, int userId, int? workstationId, CancellationToken ct = default) => throw new InvalidOperationException();
    }
}
