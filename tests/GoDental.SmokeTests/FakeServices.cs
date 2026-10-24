using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

internal sealed class FakeIdentityGateway : IIdentityGateway
{
    public Task<GoDental.Application.Common.Result<UserIdentity>>? PendingAccess { get; set; }
    public Task<bool> NeedsAdministratorAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<Result<UserIdentity>> CreateAdministratorAsync(string u, string d, string p, string w, CancellationToken ct = default) => PendingAccess ?? Task.FromResult(Result<UserIdentity>.Failure("TEST", "Sin acceso SQL en smoke visual."));
    public Task<Result<UserIdentity>> SignInAsync(string u, string p, string w, CancellationToken ct = default) => Task.FromResult(Result<UserIdentity>.Failure("TEST", "Sin acceso SQL en smoke visual."));
    public Task<Result> ChangePasswordAsync(int id, string p, string n, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task SignOutAsync(int id, int? w, CancellationToken ct = default) => Task.CompletedTask;
}
internal sealed class FakeProductRepository : IProductRepository
{
    public IReadOnlyList<ProductData> Rows { get; set; } = [];
    public ProductSearch? LastSearch { get; private set; }
    public int SearchCount { get; private set; }
    public int SaveCount { get; private set; }
    public bool RejectSave { get; set; }
    public TaskCompletionSource<Result<int>>? PendingSave { get; set; }
    public Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken ct = default)
    {
        LastSearch = search;
        SearchCount++;
        return Task.FromResult(new ProductPage(Rows, Rows.Count));
    }
    public Task<ProductCatalogs> GetCatalogsAsync(CancellationToken ct = default) => Task.FromResult(new ProductCatalogs([new() { Id = 1, Name = "General" }], [], [new() { Id = 1, Name = "Unidad" }]));
    public Task<Result<int>> SaveAsync(ProductData p, int id, int? w, CancellationToken ct = default)
    {
        SaveCount++;
        if (PendingSave is { } pending) return pending.Task;
        return Task.FromResult(RejectSave ? Result<int>.Failure("TEST", "Guardado rechazado para prueba.") : Result<int>.Success(1));
    }
    public Task<Result<int>> CreateCategoryAsync(string n, int id, int? w, CancellationToken ct = default) => Task.FromResult(Result<int>.Success(1));
}
internal sealed class FakeUserRepository : IUserRepository
{
    public Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UserSummary>>([new() { Id = 3, UserName = "usuario_visual", DisplayName = "Usuario de prueba visual", Roles = "Vendedor", Active = true, RowVersion = new byte[8] }]);
    public Task<IReadOnlyList<CatalogOption>> RolesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CatalogOption>>([new() { Id = 1, Name = "Vendedor" }]);
    public Task<Result> CreateAsync(string u, string n, int r, string h, int a, int? e, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result> SetActiveAsync(UserSummary u, bool v, string r, int a, int? e, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result> ResetPasswordAsync(UserSummary u, string h, string r, int a, int? e, CancellationToken ct = default) => Task.FromResult(Result.Success());
}


// Datos controlados sólo para pruebas de enlaces/layout; nunca registrados en App.
internal sealed class VisualInventoryRepository : IInventoryRepository
{
    public bool HasStock { get; set; } = true;
    public Task<IReadOnlyList<CatalogOption>> WarehousesAsync(int userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CatalogOption>>([new(){Id=1,Name="Almacén de prueba"}]);
    public Task<InventorySnapshot> ReadAsync(int p, int w, int u, CancellationToken ct = default) => Task.FromResult(!HasStock ? new InventorySnapshot([], []) : new InventorySnapshot([new(){WarehouseId=1,Warehouse="Almacén de prueba",Quantity=10,Sellable=10,AverageCost=2,RowVersion=new byte[8]}],[new(){Id=1,Number="LOTE-PRUEBA",Expires=new DateTime(2027,1,1),Quantity=10,Active=true,Status="Vigente"}]));
    public Task<MovementPage> MovementsAsync(MovementFilter f, int u, CancellationToken ct = default) => Task.FromResult(!HasStock ? new MovementPage([], 0) : new MovementPage([new() { Id = 1, Date = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.FromHours(-6)), Warehouse = "Almacén de prueba", Lot = "LOTE-PRUEBA", Type = "AJUSTE", Quantity = 10, Before = 0, After = 10, User = "Cuenta de prueba", Reference = "CONTEO #1", Reason = "Movimiento sintético para comprobar la vista" }],1));
    public Task<Result<long>> AdjustAsync(InventoryAdjustment r,int u,int? w,CancellationToken ct=default) => Task.FromResult(Result<long>.Failure("TEST","Prueba visual sin escritura."));
    public Task<Result<long>> SetLotStateAsync(LotStateChange r,int u,int? w,CancellationToken ct=default) => Task.FromResult(Result<long>.Failure("TEST","Prueba visual sin escritura."));
}
internal sealed class VisualCategoryRepository : ICategoryRepository
{
    public Task<IReadOnlyList<CategoryData>> ListAsync(int u,CancellationToken ct=default) => Task.FromResult<IReadOnlyList<CategoryData>>([new(){Id=1,Name="General",Active=true}]);
    public Task<Result<int>> SaveAsync(CategoryChange c,int u,int? w,CancellationToken ct=default) => Task.FromResult(Result<int>.Failure("TEST","Prueba visual sin escritura."));
}
