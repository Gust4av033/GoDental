using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
namespace GoDental.Tests.Common;

public sealed class InventoryTests
{
    private static InventoryAdjustment Entry() => new(1, 1, 10, 2.50m, "AJUSTE", "Diferencia física verificada", []);
    [Fact] public void EntryRequiresCostReasonAndSqlPrecision()
    {
        Assert.Null(InventoryValidator.Validate(Entry()));
        Assert.NotNull(InventoryValidator.Validate(Entry() with { UnitCost = null }));
        Assert.NotNull(InventoryValidator.Validate(Entry() with { Reason = "  " }));
        Assert.NotNull(InventoryValidator.Validate(Entry() with { Quantity = .00001m }));
        Assert.NotNull(InventoryValidator.Validate(Entry() with { UnitCost = .0000001m }));
        Assert.NotNull(InventoryValidator.Validate(Entry() with { ExpectedVersion = new byte[3] }));
    }
    [Fact] public void DisposalCannotBecomeAnEntryOrCreateALot()
    {
        Assert.NotNull(InventoryValidator.Validate(Entry() with { Type = "DAÑO" }));
        Assert.Null(InventoryValidator.Validate(Entry() with { Type = "DAÑO", Quantity = -1, UnitCost = null, LotId = 7 }));
        Assert.NotNull(InventoryValidator.Validate(Entry() with { Quantity = -1, NewLotNumber = "NUEVO" }));
        Assert.NotNull(InventoryValidator.Validate(Entry() with { LotId = 7, NewLotNumber = "NUEVO" }));
    }
    [Fact] public void TransferRequiresDifferentDestinationAndOutgoingQuantity()
    {
        var transfer = Entry() with { Type = "TRANSFERENCIA", Quantity = -1, DestinationWarehouseId = 2 };
        Assert.Null(InventoryValidator.Validate(transfer));
        Assert.NotNull(InventoryValidator.Validate(transfer with { DestinationWarehouseId = 1 }));
        Assert.NotNull(InventoryValidator.Validate(transfer with { DestinationWarehouseId = null }));
        Assert.NotNull(InventoryValidator.Validate(transfer with { Quantity = 1 }));
    }
    [Fact] public async Task SellerAndSignedOutUsersCannotWriteEvenIfCalledOutsideUi()
    {
        var session = new UserSession(); var repository = new ForbiddenRepository();
        var service = new InventoryService(repository, session);
        Assert.Equal("FORBIDDEN", (await service.AdjustAsync(Entry())).ErrorCode);
        session.Begin(new(1, "vendedor", "Vendedor", 1, ["Vendedor"], ["INVENTARIO.VER"], false));
        Assert.True(service.CanRead); Assert.False(service.CanSeeCosts);
        Assert.Equal("FORBIDDEN", (await service.AdjustAsync(Entry())).ErrorCode);
        Assert.Equal("FORBIDDEN", (await service.SetLotStateAsync(new(1, true, false, "Retiro"))).ErrorCode);
        session.Begin(new(1, "admin", "Admin", 1, ["Administrador"], ["INVENTARIO.VER", "INVENTARIO.AJUSTAR"], false));
        Assert.Equal("VALIDATION", (await service.AdjustAsync(Entry() with { Quantity = 0 })).ErrorCode);
    }
    private sealed class ForbiddenRepository : IInventoryRepository
    {
        public Task<IReadOnlyList<CatalogOption>> WarehousesAsync(int userId, CancellationToken ct = default) => throw new InvalidOperationException("No debe alcanzar persistencia.");
        public Task<InventorySnapshot> ReadAsync(int p, int w, int u, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<MovementPage> MovementsAsync(MovementFilter f, int u, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<Result<long>> AdjustAsync(InventoryAdjustment r, int u, int? w, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<Result<long>> SetLotStateAsync(LotStateChange r, int u, int? w, CancellationToken ct = default) => throw new InvalidOperationException();
    }
}
