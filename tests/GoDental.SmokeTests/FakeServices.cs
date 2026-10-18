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
    public ProductSearch? LastSearch { get; private set; }
    public Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken ct = default)
    {
        LastSearch = search;
        return Task.FromResult(new ProductPage([], 0));
    }
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


// Datos controlados sólo para pruebas de enlaces/layout; nunca registrados en App.
internal sealed class VisualInventoryRepository : IInventoryRepository
{
    public Task<IReadOnlyList<CatalogOption>> WarehousesAsync(int userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CatalogOption>>([new(){Id=1,Name="Almacén de prueba"}]);
    public Task<InventorySnapshot> ReadAsync(int p, int w, int u, CancellationToken ct = default) => Task.FromResult(new InventorySnapshot([new(){WarehouseId=1,Warehouse="Almacén de prueba",Quantity=10,Sellable=10,AverageCost=2,RowVersion=new byte[8]}],[new(){Id=1,Number="LOTE-PRUEBA",Expires=new DateTime(2027,1,1),Quantity=10,Active=true,Status="Vigente"}]));
    public Task<MovementPage> MovementsAsync(MovementFilter f, int u, CancellationToken ct = default) => Task.FromResult(new MovementPage([],0));
    public Task<Result<long>> AdjustAsync(InventoryAdjustment r,int u,int? w,CancellationToken ct=default) => Task.FromResult(Result<long>.Failure("TEST","Prueba visual sin escritura."));
    public Task<Result<long>> SetLotStateAsync(LotStateChange r,int u,int? w,CancellationToken ct=default) => Task.FromResult(Result<long>.Failure("TEST","Prueba visual sin escritura."));
}
internal sealed class VisualCategoryRepository : ICategoryRepository
{
    public Task<IReadOnlyList<CategoryData>> ListAsync(int u,CancellationToken ct=default) => Task.FromResult<IReadOnlyList<CategoryData>>([]);
    public Task<Result<int>> SaveAsync(CategoryChange c,int u,int? w,CancellationToken ct=default) => Task.FromResult(Result<int>.Failure("TEST","Prueba visual sin escritura."));
}
