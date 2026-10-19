using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Domain.Rules;

namespace GoDental.Tests.Common;

public sealed class InventoryCountTests
{
    [Theory]
    [InlineData("0", true)]
    [InlineData("1.2345", true)]
    [InlineData("99999999999999.9999", true)]
    [InlineData("-1", false)]
    [InlineData("0.00001", false)]
    [InlineData("100000000000000", false)]
    public void PhysicalQuantityRespectsSqlPrecision(string value, bool valid) => Assert.Equal(valid,
        InventoryCountRules.IsValidQuantity(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void PendingLineDiffersFromAnExplicitZero()
    {
        var line = new InventoryCountLineData { SystemQuantity = 7, CountedQuantity = 0 };
        Assert.Null(line.Difference);
        line.Counted = true;
        Assert.Equal(-7m, line.Difference);
        Assert.Equal(2.5001m, InventoryCountRules.Difference(1.2345m, 3.7346m));
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryCountRules.Difference(0, -1));
    }

    [Fact]
    public void StartRequiresDefinedScopeWithoutDuplicateProducts()
    {
        var start = new StartInventoryCount(1, [1, 2], "Conteo semanal");
        Assert.Null(InventoryCountValidator.Validate(start));
        Assert.NotNull(InventoryCountValidator.Validate(start with { ProductIds = [1, 1] }));
        Assert.NotNull(InventoryCountValidator.Validate(start with { ProductIds = [] }));
        Assert.NotNull(InventoryCountValidator.Validate(start with { ProductIds = [-1] }));
        Assert.NotNull(InventoryCountValidator.Validate(start with { ProductIds = Enumerable.Range(1, 501).ToArray() }));
        Assert.NotNull(InventoryCountValidator.Validate(start with { WarehouseId = 0 }));
        Assert.NotNull(InventoryCountValidator.Validate(start with { Reason = " " }));
        Assert.NotNull(InventoryCountValidator.Validate(start with { Reason = new('a', 501) }));
    }

    [Fact]
    public void CaptureRequiresVersionAndUniqueBoundedLines()
    {
        var capture = new CaptureInventoryCount(1, new byte[8], [new(1, 0)]);
        Assert.Null(InventoryCountValidator.Validate(capture));
        Assert.NotNull(InventoryCountValidator.Validate(capture with { ExpectedVersion = [] }));
        Assert.NotNull(InventoryCountValidator.Validate(capture with { ExpectedVersion = new byte[9] }));
        Assert.NotNull(InventoryCountValidator.Validate(capture with { Lines = [new(1, 2), new(1, 3)] }));
        Assert.NotNull(InventoryCountValidator.Validate(capture with { Lines = [new(1, -.1m)] }));
        Assert.NotNull(InventoryCountValidator.Validate(capture with { Lines = [new(1, .00001m)] }));
        Assert.NotNull(InventoryCountValidator.Validate(capture with { Lines = [new(0, 1)] }));
        Assert.NotNull(InventoryCountValidator.Validate(capture with { Lines = [] }));
    }

    [Fact]
    public async Task PermissionsAndValidationStopWritesBeforePersistence()
    {
        var repository = new RecordingRepository();
        var session = new UserSession();
        var service = new InventoryCountService(repository, session);
        var start = new StartInventoryCount(1, [1], " Conteo semanal ");
        var finish = new FinalizeInventoryCount(1, new byte[8], " Revisión física ");
        Assert.Equal("FORBIDDEN", (await service.StartAsync(start)).ErrorCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(1));
        session.Begin(new(1, "vendedor", "Vendedor", 1, ["Vendedor"], ["INVENTARIO.VER"], false));
        Assert.False(service.CanRead);
        Assert.Equal("FORBIDDEN", (await service.CloseAsync(finish)).ErrorCode);
        session.Begin(new(1, "contador", "Contador", 1, [], ["INVENTARIO.VER", "INVENTARIO.CONTEO"], false));
        Assert.True(service.CanRead); Assert.False(service.CanClose);
        Assert.Equal("FORBIDDEN", (await service.CloseAsync(finish)).ErrorCode);
        Assert.Equal("VALIDATION", (await service.StartAsync(start with { ProductIds = [] })).ErrorCode);
        Assert.Equal(0, repository.Writes);
        Assert.True((await service.StartAsync(start)).IsSuccess);
        Assert.Equal("Conteo semanal", repository.Reason);
        Assert.True((await service.CancelAsync(finish)).IsSuccess);
        Assert.True(repository.Cancel);
        session.Begin(new(1, "admin", "Administrador", 1, [], ["INVENTARIO.VER", "INVENTARIO.CONTEO", "INVENTARIO.AJUSTAR"], false));
        Assert.True(service.CanClose);
        Assert.True((await service.CloseAsync(finish)).IsSuccess);
        Assert.False(repository.Cancel);
        Assert.Equal("Revisión física", repository.Reason);
        session.Clear();
        Assert.False(service.CanRead);
        Assert.Equal("FORBIDDEN", (await service.CaptureAsync(new(1, new byte[8], [new(1, 0)]))).ErrorCode);
    }

    [Fact]
    public async Task PaginationCannotOverflowOrRequestUnknownStates()
    {
        var session = new UserSession();
        session.Begin(new(1, "admin", "Admin", 1, [], ["INVENTARIO.VER", "INVENTARIO.CONTEO"], false));
        var service = new InventoryCountService(new RecordingRepository(), session);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new(Page: int.MaxValue)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new(State: "OTRO")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new(WarehouseId: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ReadAsync(0));
    }

    private sealed class RecordingRepository : IInventoryCountRepository
    {
        public int Writes { get; private set; }
        public string? Reason { get; private set; }
        public bool Cancel { get; private set; }
        public Task<InventoryCountPage> SearchAsync(InventoryCountFilter filter, int userId, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<InventoryCountSnapshot?> ReadAsync(long countId, int userId, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<Result<long>> StartAsync(StartInventoryCount request, int userId, int? workstationId, CancellationToken ct = default)
        { Writes++; Reason = request.Reason; return Task.FromResult(Result<long>.Success(1)); }
        public Task<Result<long>> CaptureAsync(CaptureInventoryCount request, int userId, int? workstationId, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<Result<long>> FinalizeAsync(FinalizeInventoryCount request, bool cancel, int userId, int? workstationId, CancellationToken ct = default)
        { Writes++; Reason = request.Reason; Cancel = cancel; return Task.FromResult(Result<long>.Success(1)); }
    }
}
