using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;

public sealed class CustomerService(ICustomerRepository repository, IUserSession session)
{
    public bool CanRead => Has("CLIENTES.VER");
    public bool CanCreate => CanRead && Has("CLIENTES.CREAR");
    public bool CanEdit => CanRead && Has("CLIENTES.EDITAR");
    public bool CanConfigureCredit => CanRead && Has("CLIENTES.CREDITO_CONFIGURAR");
    public bool CanReadHistory => CanRead && Has("VENTAS.VER");
    private bool Has(string permission) => session.IsAuthenticated && session.Permissions.Contains(permission);
    private int UserId => checked((int)session.UserId!.Value);
    private int? WorkstationId => checked((int?)session.WorkstationId);
    private void RequireRead(bool history = false) { if (!CanRead || history && !CanReadHistory) throw new UnauthorizedAccessException("No tienes permiso para consultar estos datos del cliente."); }
    public Task<CustomerPage> SearchAsync(CustomerSearch search, CancellationToken ct = default)
    {
        RequireRead();
        if (search.Search is null || search.Search.Trim().Length > 200 || search.TypeId is <= 0 || !Page(search.Page, search.PageSize)) throw new ArgumentException("Revisa los filtros y la página de clientes.");
        return repository.SearchAsync(search with { Search = search.Search.Trim() }, UserId, ct);
    }
    public Task<CustomerCatalogs> CatalogsAsync(CancellationToken ct = default) { RequireRead(); return repository.CatalogsAsync(UserId, ct); }
    public Task<CustomerSnapshot?> ReadAsync(long id, CancellationToken ct = default) { RequireRead(); Positive(id); return repository.ReadAsync(id, UserId, ct); }
    public Task<Result<long>> SaveAsync(CustomerChange request, CancellationToken ct = default)
    {
        if (!(request.Customer?.Id == 0 ? CanCreate : CanEdit)) return Denied();
        if (CustomerValidator.Validate(request) is { } error) return Invalid(error);
        var c = request.Customer!;
        return repository.SaveAsync(request with { Reason = request.Reason.Trim(), Customer = c with { Code = c.Code?.Trim() ?? "", Name = c.Name.Trim(), CommercialName = Optional(c.CommercialName), Nit = Optional(c.Nit), Nrc = Optional(c.Nrc), Dui = Optional(c.Dui), BusinessActivity = Optional(c.BusinessActivity), Phone = Optional(c.Phone), WhatsApp = Optional(c.WhatsApp), Email = Optional(c.Email), RowVersion = c.RowVersion.ToArray() } }, UserId, WorkstationId, ct);
    }
    public Task<Result<long>> SaveContactAsync(CustomerContactChange request, CancellationToken ct = default)
    {
        if (!CanEdit) return Denied(); if (CustomerValidator.Validate(request) is { } error) return Invalid(error);
        var c = request.Contact;
        return repository.SaveContactAsync(request with { Reason = request.Reason.Trim(), ExpectedVersion = request.ExpectedVersion.ToArray(), Contact = c with { Name = c.Name.Trim(), Position = Optional(c.Position), Phone = Optional(c.Phone), WhatsApp = Optional(c.WhatsApp), Email = Optional(c.Email) } }, UserId, WorkstationId, ct);
    }
    public Task<Result<long>> SaveAddressAsync(CustomerAddressChange request, CancellationToken ct = default)
    {
        if (!CanEdit) return Denied(); if (CustomerValidator.Validate(request) is { } error) return Invalid(error);
        var a = request.Address;
        return repository.SaveAddressAsync(request with { Reason = request.Reason.Trim(), ExpectedVersion = request.ExpectedVersion.ToArray(), Address = a with { Address = a.Address.Trim(), Department = Optional(a.Department), Municipality = Optional(a.Municipality), Reference = Optional(a.Reference) } }, UserId, WorkstationId, ct);
    }
    public Task<Result<long>> SaveCreditAsync(CustomerCreditChange request, CancellationToken ct = default)
    {
        if (!CanConfigureCredit) return Denied(); if (CustomerValidator.Validate(request) is { } error) return Invalid(error);
        return repository.SaveCreditAsync(request with { Reason = request.Reason.Trim(), ExpectedVersion = request.ExpectedVersion.ToArray(), Credit = request.Credit with { BlockReason = Optional(request.Credit.BlockReason) } }, UserId, WorkstationId, ct);
    }
    public Task<CustomerHistoryPage<CustomerSaleData>> SalesAsync(CustomerHistoryFilter filter, CancellationToken ct = default) { History(filter); return repository.SalesAsync(filter, UserId, ct); }
    public Task<CustomerHistoryPage<CustomerAccountData>> AccountsAsync(CustomerHistoryFilter filter, CancellationToken ct = default) { History(filter); return repository.AccountsAsync(filter, UserId, ct); }
    public Task<CustomerHistoryPage<CustomerPaymentData>> PaymentsAsync(CustomerHistoryFilter filter, CancellationToken ct = default) { History(filter); return repository.PaymentsAsync(filter, UserId, ct); }
    public Task<CustomerBalanceData?> BalanceAsync(long id, CancellationToken ct = default) { RequireRead(true); Positive(id); return repository.BalanceAsync(id, UserId, ct); }
    private void History(CustomerHistoryFilter f) { RequireRead(true); Positive(f.CustomerId); if (!Page(f.Page, f.PageSize)) throw new ArgumentException("Revisa la página del historial."); }
    private static void Positive(long id) { if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id)); }
    private static bool Page(int page, int size) => size is >= 1 and <= 100 && page >= 1 && page <= int.MaxValue / size;
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Task<Result<long>> Denied() => Task.FromResult(Result<long>.Failure("FORBIDDEN", "No tienes permiso para este cambio de cliente."));
    private static Task<Result<long>> Invalid(string error) => Task.FromResult(Result<long>.Failure("VALIDATION", error));
}
