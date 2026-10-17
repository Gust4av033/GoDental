using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

internal sealed class FakeIdentityGateway : IIdentityGateway
{
    public Task<bool> NeedsAdministratorAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<Result<UserIdentity>> CreateAdministratorAsync(string u, string d, string p, string w, CancellationToken ct = default) => Task.FromResult(Result<UserIdentity>.Failure("TEST", "Sin acceso SQL en smoke visual."));
    public Task<Result<UserIdentity>> SignInAsync(string u, string p, string w, CancellationToken ct = default) => Task.FromResult(Result<UserIdentity>.Failure("TEST", "Sin acceso SQL en smoke visual."));
    public Task<Result> ChangePasswordAsync(int id, string p, string n, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task SignOutAsync(int id, int? w, CancellationToken ct = default) => Task.CompletedTask;
}
internal sealed class FakeProductRepository : IProductRepository
{
    public Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken ct = default) => Task.FromResult(new ProductPage([], 0));
    public Task<ProductCatalogs> GetCatalogsAsync(CancellationToken ct = default) => Task.FromResult(new ProductCatalogs([new() { Id = 1, Name = "General" }], [], [new() { Id = 1, Name = "Unidad" }]));
    public Task<Result<int>> SaveAsync(ProductData p, int id, int? w, CancellationToken ct = default) => Task.FromResult(Result<int>.Success(1));
    public Task<Result<int>> CreateCategoryAsync(string n, int id, int? w, CancellationToken ct = default) => Task.FromResult(Result<int>.Success(1));
}
internal sealed class FakeUserRepository : IUserRepository
{
    public Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UserSummary>>([]);
    public Task<IReadOnlyList<CatalogOption>> RolesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CatalogOption>>([new() { Id = 1, Name = "Vendedor" }]);
    public Task<Result> CreateAsync(string u, string n, int r, string h, int a, int? e, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result> SetActiveAsync(UserSummary u, bool v, string r, int a, int? e, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result> ResetPasswordAsync(UserSummary u, string h, string r, int a, int? e, CancellationToken ct = default) => Task.FromResult(Result.Success());
}
