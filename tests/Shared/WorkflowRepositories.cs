using System.IO;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

// Dobles exclusivos de pruebas. Nunca registrados en la aplicación.
internal sealed class UiCountRepository : IInventoryCountRepository
{
    public InventoryCountData Header { get; } = new() { Id = 1, WarehouseId = 1, Warehouse = "Almacén de prueba", State = "ABIERTO", TotalLines = 2, CreatedBy = "Prueba", Opened = DateTimeOffset.UtcNow, RowVersion = new byte[8] };
    public List<InventoryCountLineData> Lines { get; } = [new() { Id = 1, ProductId = 1, Sku = "PRUEBA", Product = "Producto de prueba", SystemQuantity = 5 }, new() { Id = 2, ProductId = 2, Sku = "LOTE", Product = "Producto con lote", LotId = 1, Lot = "PRUEBA", SystemQuantity = 2, Counted = true, CountedQuantity = 2 }];
    public List<CaptureInventoryCount> Captures { get; } = [];
    public int Finalizations { get; private set; }
    public StartInventoryCount? LastStart { get; private set; }
    public bool LoseStartResponse { get; set; }
    public bool LoseResponse { get; set; }
    public bool RejectCapture { get; set; }
    public bool DenyRead { get; set; }
    public TaskCompletionSource<InventoryCountPage>? PendingSearch { get; set; }
    public Task<InventoryCountPage> SearchAsync(InventoryCountFilter f, int u, CancellationToken ct = default) => PendingSearch?.Task ?? Task.FromResult(new InventoryCountPage([Header], 1));
    public Task<InventoryCountSnapshot?> ReadAsync(long id, int u, CancellationToken ct = default)
    {
        if (DenyRead) throw new UnauthorizedAccessException();
        Header.CountedLines = Lines.Count(l => l.Counted); Header.TotalLines = Lines.Count;
        return Task.FromResult<InventoryCountSnapshot?>(new(Header, Lines));
    }
    public Task<Result<long>> StartAsync(StartInventoryCount r, int u, int? w, CancellationToken ct = default)
    {
        LastStart = r;
        if (LoseStartResponse) throw new IOException("Respuesta perdida de apertura");
        return Task.FromResult(Result<long>.Success(1));
    }
    public Task<Result<long>> CaptureAsync(CaptureInventoryCount r, int u, int? w, CancellationToken ct = default)
    {
        Captures.Add(r);
        if (RejectCapture) return Task.FromResult(Result<long>.Failure("CONCURRENCY_CONFLICT", "Otro equipo editó el conteo."));
        if (!r.ExpectedVersion.SequenceEqual(Header.RowVersion)) throw new InvalidOperationException("Versión obsoleta en prueba");
        foreach (var q in r.Lines) { var l = Lines.Single(l => l.Id == q.LineId); l.Counted = true; l.CountedQuantity = q.Quantity; }
        Header.RowVersion = BitConverter.GetBytes(BitConverter.ToInt64(Header.RowVersion) + 1);
        return Task.FromResult(Result<long>.Success(1));
    }
    public Task<Result<long>> FinalizeAsync(FinalizeInventoryCount r, bool cancel, int u, int? w, CancellationToken ct = default)
    {
        Finalizations++; Header.State = cancel ? "CANCELADO" : "CERRADO";
        if (LoseResponse) throw new IOException("Respuesta perdida de prueba");
        return Task.FromResult(Result<long>.Success(1));
    }
}

internal sealed class UiSupplierRepository : ISupplierRepository
{
    public SupplierData Supplier { get; private set; } = new() { Id = 1, Code = "PRUEBA", Name = "Proveedor de prueba", Country = "País de prueba", RowVersion = new byte[8] };
    public List<SupplierContactData> Contacts { get; } = [new() { Id = 1, Name = "Contacto de prueba", Primary = true }];
    public List<SupplierProductData> Products { get; } = [new() { ProductId = 1, LocalSku = "PRUEBA", Product = "Producto de prueba", SupplierSku = "EXT-PRUEBA", LastCost = 1.123456m, CurrencyId = 1, Currency = "USD", MinimumQuantity = 2.0001m, LeadTimeDays = 5 }];
    public int Writes { get; private set; }
    public SupplierContactChange? LastContact { get; private set; }
    public SupplierProductChange? LastProduct { get; private set; }
    public bool Conflict { get; set; }
    public bool LoseResponse { get; set; }
    public bool DenyRead { get; set; }
    public Task<SupplierPage> SearchAsync(SupplierSearch r, int u, CancellationToken ct = default) => Task.FromResult(new SupplierPage([Supplier], 1));
    public Task<SupplierSnapshot?> ReadAsync(int id, int u, CancellationToken ct = default)
    {
        if (DenyRead) throw new UnauthorizedAccessException();
        return Task.FromResult<SupplierSnapshot?>(new(Supplier, Contacts, Products));
    }
    public Task<IReadOnlyList<CatalogOption>> CurrenciesAsync(int u, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CatalogOption>>([new() { Id = 1, Name = "USD" }]);
    private Result<int> Changed()
    {
        Writes++;
        if (Conflict) return Result<int>.Failure("CONCURRENCY_CONFLICT", "Otro equipo editó el proveedor.");
        Supplier = Supplier with { RowVersion = BitConverter.GetBytes(BitConverter.ToInt64(Supplier.RowVersion) + 1) };
        if (LoseResponse) throw new IOException("Respuesta perdida de prueba");
        return Result<int>.Success(1);
    }
    public Task<Result<int>> SaveAsync(SupplierChange r, int u, int? w, CancellationToken ct = default)
    {
        var result = Changed(); if (result.IsSuccess) Supplier = r.Supplier with { Id = 1, RowVersion = Supplier.RowVersion };
        return Task.FromResult(result);
    }
    public Task<Result<int>> SaveContactAsync(SupplierContactChange r, int u, int? w, CancellationToken ct = default)
    {
        LastContact = r; return Task.FromResult(Changed());
    }
    public Task<Result<int>> SaveProductAsync(SupplierProductChange r, int u, int? w, CancellationToken ct = default)
    {
        LastProduct = r; return Task.FromResult(Changed());
    }
}
