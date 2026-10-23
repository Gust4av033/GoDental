using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;

namespace GoDental.Application.Services;

public sealed class SupplierService(ISupplierRepository repository, IUserSession session)
{
    public bool CanRead => session.IsAuthenticated && session.Permissions.Contains("COMPRAS.VER");
    public bool CanManage => CanRead && session.Permissions.Contains("PROVEEDORES.GESTIONAR");
    private int UserId => checked((int)session.UserId!.Value);
    private int? WorkstationId => checked((int?)session.WorkstationId);

    public Task<SupplierPage> SearchAsync(SupplierSearch search, CancellationToken ct = default)
    {
        RequireRead();
        if (search.Search is null || search.Search.Trim().Length > 200 || search.Page < 1 || search.PageSize is < 1 or > 100
            || search.Page > int.MaxValue / search.PageSize) throw new ArgumentException("Revisa la búsqueda y la página de proveedores.", nameof(search));
        return repository.SearchAsync(search with { Search = search.Search.Trim() }, UserId, ct);
    }
    public Task<SupplierSnapshot?> ReadAsync(int supplierId, CancellationToken ct = default)
    {
        RequireRead();
        if (supplierId <= 0) throw new ArgumentOutOfRangeException(nameof(supplierId));
        return repository.ReadAsync(supplierId, UserId, ct);
    }
    public Task<IReadOnlyList<CatalogOption>> CurrenciesAsync(CancellationToken ct = default)
    {
        RequireRead(); return repository.CurrenciesAsync(UserId, ct);
    }
    public Task<Result<int>> SaveAsync(SupplierChange request, CancellationToken ct = default)
    {
        if (!CanManage) return Denied();
        if (SupplierValidator.Validate(request) is { } error) return Invalid(error);
        var s = request.Supplier;
        return repository.SaveAsync(request with { Reason = request.Reason.Trim(), Supplier = s with
            { Code = s.Code?.Trim() ?? "", Name = s.Name.Trim(), Country = s.Id == 0 ? Optional(s.Country) : s.Country, Address = Optional(s.Address),
                Email = Optional(s.Email), Phone = s.Id == 0 ? Optional(s.Phone) : s.Phone, Website = Optional(s.Website) } }, UserId, WorkstationId, ct);
    }
    public Task<Result<int>> SaveContactAsync(SupplierContactChange request, CancellationToken ct = default)
    {
        if (!CanManage) return Denied();
        if (SupplierValidator.Validate(request) is { } error) return Invalid(error);
        var c = request.Contact;
        return repository.SaveContactAsync(request with { Reason = request.Reason.Trim(), Contact = c with
            { Name = c.Name.Trim(), Position = Optional(c.Position), Email = Optional(c.Email), Phone = c.Id == 0 ? Optional(c.Phone) : c.Phone } }, UserId, WorkstationId, ct);
    }
    public Task<Result<int>> SaveProductAsync(SupplierProductChange request, CancellationToken ct = default)
    {
        if (!CanManage) return Denied();
        if (SupplierValidator.Validate(request) is { } error) return Invalid(error);
        return repository.SaveProductAsync(request with { Reason = request.Reason.Trim(), Product = request.Product with
            { SupplierSku = Optional(request.Product.SupplierSku) } }, UserId, WorkstationId, ct);
    }
    private void RequireRead()
    {
        if (!CanRead) throw new UnauthorizedAccessException("No tienes permiso para consultar proveedores.");
    }
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Task<Result<int>> Invalid(string error) => Task.FromResult(Result<int>.Failure("VALIDATION", error));
    private static Task<Result<int>> Denied() => Task.FromResult(Result<int>.Failure("FORBIDDEN", "No tienes permiso para gestionar proveedores."));
}
