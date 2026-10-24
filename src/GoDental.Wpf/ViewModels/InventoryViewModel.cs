using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed partial class InventoryViewModel : ObservableObject
{
    private readonly InventoryService service;
    private readonly ISessionController session;
    private readonly ILogger<InventoryViewModel> logger;
    private readonly IDialogService dialogs;
    public event EventHandler? InventoryChanged;
    public BarcodesViewModel? Barcodes { get; }
    public InitialInventoryViewModel? InitialInventory { get; }
    private bool snapshotLoaded, initializedRefresh;
    public bool CanOfferInitial => InitialInventory is not null && CanAdjust && snapshotLoaded && product?.Active == true && SelectedStock is null;
    public bool CanInitialize => CanOfferInitial && IsNotBusy && InitialInventory!.CanInitialize;
    public string InitialInventoryHint => !CanAdjust ? "Consulta de inventario. Necesitas permiso de ajuste para registrar unidades."
        : product?.Active != true ? "Activa el producto antes de registrar sus existencias iniciales."
        : Warehouse is null ? "Selecciona un almacén para consultar sus existencias."
        : !snapshotLoaded ? "Pulsa Actualizar para consultar el almacén y habilitar la acción correspondiente."
        : SelectedStock is null ? "Sin saldo registrado en este almacén. Usa Existencias iniciales para ingresar cantidad, costo y lote si corresponde."
        : "Este almacén ya tiene un saldo registrado, aunque sea cero. Para agregar unidades usa Registrar movimiento → AJUSTE.";
    private ProductData? product;
    public ProductData? Product => product;
    public StockData? SelectedStock => Stocks.FirstOrDefault(stock => stock.WarehouseId == Warehouse?.Id);
    private byte[]? version;
    public ObservableCollection<CatalogOption> Warehouses { get; } = [];
    public ObservableCollection<StockData> Stocks { get; } = [];
    public ObservableCollection<LotData> Lots { get; } = [];
    public ObservableCollection<MovementData> Movements { get; } = [];
    public IReadOnlyList<string> MovementTypes { get; } = ["AJUSTE", "DAÑO", "VENCIMIENTO", "TRANSFERENCIA"];
    public IReadOnlyList<string> HistoryTypes { get; } = ["Todos", "COMPRA", "VENTA", "DEVOLUCION", "AJUSTE", "DAÑO", "VENCIMIENTO", "TRANSFERENCIA"];
    [ObservableProperty] private string title = "Existencias y trazabilidad";
    [ObservableProperty] private bool isOpen;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNotBusy)), NotifyPropertyChangedFor(nameof(CanWrite))] private bool isBusy;
    [ObservableProperty] private string statusMessage = "";
    [ObservableProperty] private CatalogOption? warehouse;
    [ObservableProperty] private CatalogOption? destination;
    [ObservableProperty] private LotData? selectedLot;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsTransfer))] private string movementType = "AJUSTE";
    [ObservableProperty] private string quantity = "";
    [ObservableProperty] private string unitCost = "";
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private bool createLot;
    [ObservableProperty] private string lotNumber = "";
    [ObservableProperty] private DateTime? manufactured;
    [ObservableProperty] private DateTime? expires;
    [ObservableProperty] private bool confirmed;
    [ObservableProperty] private string historyType = "Todos";
    [ObservableProperty] private bool filterSelectedLot;
    [ObservableProperty] private DateTime? from;
    [ObservableProperty] private DateTime? until;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int page = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel)), NotifyPropertyChangedFor(nameof(IsHistoryEmpty))] private int total;
    public bool IsNotBusy => Barcodes?.IsBusy != true && Barcodes?.IsEditing != true && Barcodes?.Operation.CanStart != false && !IsBusy && (InitialInventory is null || (!InitialInventory.IsBusy && !InitialInventory.IsEditing && InitialInventory.Operation.CanStart));
    public bool CanAdjust => service.CanAdjust;
    public bool CanSeeCosts => service.CanSeeCosts;
    public bool CanWrite => CanAdjust && IsNotBusy && version is not null;
    public bool IsTransfer => MovementType == "TRANSFERENCIA";
    public bool TracksLot => product?.TracksLot == true;
    public bool IsHistoryEmpty => Total == 0;
    public bool IsStocksEmpty => Stocks.Count == 0;
    public bool IsLotsEmpty => Lots.Count == 0;
    public string PageLabel => $"{Total} movimientos · Página {Page} de {Math.Max(1, (int)Math.Ceiling(Total / 30d))}";

    public InventoryViewModel(InventoryService service, ISessionController session, ILogger<InventoryViewModel> logger, IDialogService dialogs, InitialInventoryViewModel? initialInventory = null, BarcodesViewModel? barcodes = null)
    {
        this.service = service; this.session = session; this.logger = logger; this.dialogs = dialogs;
        Barcodes = barcodes;
        if (barcodes is not null) barcodes.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(IsNotBusy)); OnPropertyChanged(nameof(CanWrite)); OnPropertyChanged(nameof(CanInitialize)); };
        InitialInventory = initialInventory;
        if (initialInventory is not null) initialInventory.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsNotBusy)); OnPropertyChanged(nameof(CanWrite)); OnPropertyChanged(nameof(CanInitialize));
            if (initializedRefresh && IsNotBusy) { initializedRefresh = false; InventoryChanged?.Invoke(this, EventArgs.Empty); }
        };
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(CanInitialize));
            if (e.PropertyName is nameof(CanOfferInitial) or nameof(CanAdjust) or nameof(Product)) OnPropertyChanged(nameof(InitialInventoryHint));
        };
        Stocks.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(SelectedStock)); OnPropertyChanged(nameof(CanOfferInitial)); OnPropertyChanged(nameof(CanInitialize)); };
        session.Changed += (_, _) =>
        {
            snapshotLoaded = initializedRefresh = false;
            product = null; OnPropertyChanged(nameof(Product)); version = null; IsOpen = false; Stocks.Clear(); Lots.Clear(); Movements.Clear(); Warehouses.Clear();
            Warehouse = null; SelectedLot = null; Total = 0; ResetForm();
            OnPropertyChanged(nameof(IsStocksEmpty)); OnPropertyChanged(nameof(IsLotsEmpty));
            OnPropertyChanged(nameof(CanAdjust)); OnPropertyChanged(nameof(CanSeeCosts)); OnPropertyChanged(nameof(CanWrite));
        };
    }
    partial void OnWarehouseChanged(CatalogOption? value) { snapshotLoaded = false; version = null; Confirmed = false; Stocks.Clear(); Lots.Clear(); Movements.Clear(); Total = 0; SelectedLot = null; OnPropertyChanged(nameof(CanWrite)); OnPropertyChanged(nameof(IsStocksEmpty)); OnPropertyChanged(nameof(IsLotsEmpty)); }
    partial void OnCreateLotChanged(bool value) { Confirmed = false; if (value) SelectedLot = null; }
    partial void OnSelectedLotChanged(LotData? value) { Confirmed = false; }
    partial void OnQuantityChanged(string value) => Confirmed = false;
    partial void OnUnitCostChanged(string value) => Confirmed = false;
    partial void OnMovementTypeChanged(string value) => Confirmed = false;
    partial void OnDestinationChanged(CatalogOption? value) => Confirmed = false;
    partial void OnLotNumberChanged(string value) => Confirmed = false;
    partial void OnManufacturedChanged(DateTime? value) => Confirmed = false;
    partial void OnExpiresChanged(DateTime? value) => Confirmed = false;
    partial void OnReasonChanged(string value) => Confirmed = false;

    public async Task OpenAsync(ProductData selected)
    {
        if (!IsNotBusy) return;
        snapshotLoaded = false;
        product = selected; OnPropertyChanged(nameof(Product)); Title = $"{selected.Name} · {selected.Sku}"; IsOpen = true; Page = 1;
        From = null; Until = null; HistoryType = "Todos"; FilterSelectedLot = false;
        ResetForm(); OnPropertyChanged(nameof(TracksLot));
        var openedRevision = session.Revision;
        await ExecuteAsync(async revision =>
        {
            var values = await service.WarehousesAsync();
            if (revision != session.Revision) return;
            Warehouses.Clear(); foreach (var w in values) Warehouses.Add(w);
            Warehouse = Warehouses.FirstOrDefault();
            await ReadCoreAsync(revision);
        });
        if (openedRevision == session.Revision && Barcodes is not null) await Barcodes.OpenAsync(selected);
    }
    [RelayCommand] private void Close() { if (IsNotBusy) IsOpen = false; }
    [RelayCommand] private async Task OpenInitialInventoryAsync()
    {
        if (!CanInitialize || product is null || Warehouse is null || InitialInventory is null) return;
        await InitialInventory.OpenAsync(product, Warehouse, async () =>
        {
            initializedRefresh = true;
            await ReloadCommand.ExecuteAsync(null);
        });
    }
    [RelayCommand] private async Task OpenMovementDialogAsync()
    {
        if (!CanWrite || !IsOpen) return;
        ResetForm();
        await dialogs.ShowInventoryMovementAsync(this);
    }
    [RelayCommand] private async Task ReloadAsync() { Page = 1; await ExecuteAsync(ReadCoreAsync); }
    [RelayCommand] private async Task SearchHistoryAsync() { Page = 1; await ExecuteAsync(ReadHistoryAsync); }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await ExecuteAsync(ReadHistoryAsync); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await ExecuteAsync(ReadHistoryAsync); }
    private async Task ReadCoreAsync(long revision)
    {
        snapshotLoaded = false; version = null; OnPropertyChanged(nameof(CanWrite)); OnPropertyChanged(nameof(CanOfferInitial)); OnPropertyChanged(nameof(CanInitialize));
        if (product is null || Warehouse is null) { StatusMessage = "No hay almacenes activos para operar."; return; }
        var selectedId = SelectedLot?.Id;
        Stocks.Clear(); Lots.Clear(); Movements.Clear(); Total = 0;
        var snapshot = await service.ReadAsync(product.Id, Warehouse.Id);
        if (revision != session.Revision) return;
        Stocks.Clear(); foreach (var row in snapshot.Stocks) Stocks.Add(row);
        Lots.Clear(); foreach (var lot in snapshot.Lots) Lots.Add(lot);
        OnPropertyChanged(nameof(IsStocksEmpty)); OnPropertyChanged(nameof(IsLotsEmpty));
        SelectedLot = Lots.FirstOrDefault(l => l.Id == selectedId);
        version = Stocks.FirstOrDefault(s => s.WarehouseId == Warehouse.Id)?.RowVersion.ToArray();
        Confirmed = false; OnPropertyChanged(nameof(CanWrite));
        await ReadHistoryAsync(revision);
        if (revision != session.Revision) return;
        snapshotLoaded = true; OnPropertyChanged(nameof(CanOfferInitial)); OnPropertyChanged(nameof(CanInitialize));
        StatusMessage = "";
    }
    private async Task ReadHistoryAsync(long revision)
    {
        if (product is null || Warehouse is null) return;
        if (FilterSelectedLot && SelectedLot is null) { StatusMessage = "Selecciona un lote para filtrar el Kardex."; return; }
        Movements.Clear(); Total = 0;
        var result = await service.MovementsAsync(new(product.Id, Warehouse.Id, FilterSelectedLot ? SelectedLot?.Id : null,
            HistoryType == "Todos" ? null : HistoryType, From, Until, Page));
        if (revision != session.Revision) return;
        Movements.Clear(); foreach (var row in result.Items) Movements.Add(row); Total = result.Total;
    }
    [RelayCommand] private async Task SaveAsync()
    {
        if (!CanWrite || product is null || Warehouse is null || version is null) return;
        if (!Confirmed) { StatusMessage = "Revisa los datos y confirma que verificaste físicamente la operación."; return; }
        if (!Number(Quantity, out var parsedQuantity) || (!string.IsNullOrWhiteSpace(UnitCost) && !Number(UnitCost, out _)))
        { StatusMessage = "Escribe cantidades con punto decimal, sin separadores de miles."; return; }
        bool saved = false;
        var request = new InventoryAdjustment(product.Id, Warehouse.Id, parsedQuantity,
            string.IsNullOrWhiteSpace(UnitCost) ? null : decimal.Parse(UnitCost, CultureInfo.InvariantCulture), MovementType, Reason,
            version.ToArray(), CreateLot ? null : SelectedLot?.Id, CreateLot ? LotNumber : null,
            CreateLot ? Manufactured : null, CreateLot ? Expires : null, MovementType == "TRANSFERENCIA" ? Destination?.Id : null);
        await ExecuteAsync(async revision =>
        {
            var result = await service.AdjustAsync(request);
            if (revision != session.Revision) return;
            Confirmed = false;
            if (!result.IsSuccess)
            {
                if (result.ErrorCode == "CONCURRENCY_CONFLICT") { version = null; OnPropertyChanged(nameof(CanWrite)); }
                StatusMessage = result.Message ?? "No se pudo registrar."; return;
            }
            saved = true; var id = result.Value; version = null; ResetForm(); Page = 1;
            StatusMessage = $"Movimiento #{id} confirmado. Actualiza para consultar el saldo.";
            try { await ReadCoreAsync(revision); StatusMessage = $"Movimiento #{id} registrado con su auditoría y Kardex."; }
            catch (Exception ex) { logger.LogWarning("Falló recarga tras movimiento confirmado: {ErrorType}.", ex.GetType().Name); StatusMessage = $"Movimiento #{id} confirmado; falló la actualización de pantalla. Actualiza antes de otra operación."; }
        });
        if (saved) InventoryChanged?.Invoke(this, EventArgs.Empty);
    }
    [RelayCommand] private async Task ToggleLotAsync()
    {
        if (!CanWrite || SelectedLot is null) return;
        if (!Confirmed) { StatusMessage = "Escribe el motivo y marca la confirmación antes de cambiar el estado del lote."; return; }
        var change = new LotStateChange(SelectedLot.Id, SelectedLot.Active, !SelectedLot.Active, Reason);
        var changed = false;
        await ExecuteAsync(async revision =>
        {
            var result = await service.SetLotStateAsync(change);
            if (revision != session.Revision) return;
            Confirmed = false;
            if (!result.IsSuccess) { StatusMessage = result.Message ?? "No se pudo cambiar el estado."; return; }
            await ReadCoreAsync(revision); StatusMessage = "Estado del lote actualizado con motivo y auditoría."; changed = true;
        });
        if (changed) InventoryChanged?.Invoke(this, EventArgs.Empty);
    }
    private async Task ExecuteAsync(Func<long, Task> action)
    {
        if (IsBusy || !service.CanRead) return;
        IsBusy = true; var revision = session.Revision;
        try { await action(revision); }
        catch (Exception ex)
        {
            logger.LogWarning("Falló inventario: {ErrorType}.", ex.GetType().Name);
            if (revision != session.Revision) return;
            snapshotLoaded = false; OnPropertyChanged(nameof(CanOfferInitial)); OnPropertyChanged(nameof(CanInitialize));
            version = null; Confirmed = false;
            if (ex is UnauthorizedAccessException) { session.Clear(); return; }
            StatusMessage = ex is ArgumentException ? ex.Message : "No se pudo completar la operación. Revisa la conexión y la migración de inventario. Actualiza y revisa el Kardex antes de repetir un guardado.";
        }
        finally { IsBusy = false; }
    }
    private void ResetForm() { Quantity = ""; UnitCost = ""; Reason = ""; LotNumber = ""; Manufactured = null; Expires = null; CreateLot = false; Confirmed = false; Destination = null; MovementType = "AJUSTE"; }
    private static bool Number(string value, out decimal number) => decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
}
