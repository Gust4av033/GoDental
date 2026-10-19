using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed partial class CountLineViewModel : ObservableObject
{
    public InventoryCountLineData Data { get; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Difference))] private string quantity;
    public bool Dirty => Quantity != (Data.Counted ? Data.CountedQuantity.ToString(CultureInfo.InvariantCulture) : "");
    public string Difference => decimal.TryParse(Quantity, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
        CultureInfo.InvariantCulture, out var q) ? (q - Data.SystemQuantity).ToString("0.####", CultureInfo.InvariantCulture) : "Pendiente";
    public CountLineViewModel(InventoryCountLineData data) { Data = data; quantity = data.Counted ? data.CountedQuantity.ToString(CultureInfo.InvariantCulture) : ""; }
}

public sealed partial class InventoryCountsViewModel : WorkflowViewModel
{
    private readonly InventoryCountService service;
    private readonly InventoryService inventory;
    private readonly ProductService products;
    private readonly IWorkflowDialogService dialogs;
    private InventoryCountData? header;
    public ObservableCollection<InventoryCountData> Items { get; } = [];
    public ObservableCollection<CountLineViewModel> Lines { get; } = [];
    public ObservableCollection<CatalogOption> Warehouses { get; } = [];
    public IReadOnlyList<string> States { get; } = ["Todos", "ABIERTO", "CERRADO", "CANCELADO"];
    [ObservableProperty] private InventoryCountData? selected;
    [ObservableProperty] private CatalogOption? warehouse;
    [ObservableProperty] private string state = "Todos";
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private bool confirmed;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int page = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel)), NotifyPropertyChangedFor(nameof(IsEmpty))] private int total;
    public bool CanRead => service.CanRead;
    public bool CanEdit => CanRead && IsNotBusy && header?.State == "ABIERTO";
    public bool CanClose => CanEdit && service.CanClose;
    public bool IsEmpty => Total == 0;
    public string PageLabel => $"{Total} conteos · Página {Page}";
    public string DetailLabel => header is null ? "Selecciona un conteo y pulsa Ver detalle." : $"Conteo #{header.Id} · {header.Warehouse} · {header.State} · {header.CountedLines}/{header.TotalLines} capturadas · {header.TotalLines - header.CountedLines} pendientes";
    public bool RequiresRecount => Lines.Any(l => l.Data.RequiresRecount);
    protected override bool CanAccess => CanRead;
    public InventoryCountsViewModel(InventoryCountService service, InventoryService inventory, ProductService products,
        ISessionController session, IWorkflowDialogService dialogs, ILogger<InventoryCountsViewModel> logger) : base(session, logger)
    { this.service = service; this.inventory = inventory; this.products = products; this.dialogs = dialogs; }
    protected override void Reset() { header = null; Lines.Clear(); Items.Clear(); Warehouses.Clear(); Selected = null; Total = 0; Confirmed = false; Reason = ""; RefreshPermissions(); }
    protected override void RefreshPermissions() { OnPropertyChanged(nameof(CanRead)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanClose)); OnPropertyChanged(nameof(DetailLabel)); OnPropertyChanged(nameof(RequiresRecount)); }
    [RelayCommand] private async Task ReloadAsync() { Page = 1; await RunAsync(async (r, ct) => { var ws = await inventory.WarehousesAsync(ct); if (!Current(r)) return; var id = Warehouse?.Id; Warehouses.Clear(); Warehouses.Add(new() { Id = 0, Name = "Todos los almacenes" }); foreach (var w in ws) Warehouses.Add(w); Warehouse = Warehouses.FirstOrDefault(w => w.Id == id) ?? Warehouses.First(); await SearchCoreAsync(r, ct); }); }
    [RelayCommand] private async Task SearchAsync() { Page = 1; await RunAsync(SearchCoreAsync); }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await RunAsync(SearchCoreAsync); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await RunAsync(SearchCoreAsync); }
    private async Task SearchCoreAsync(long r, CancellationToken ct)
    {
        var result = await service.SearchAsync(new(Warehouse?.Id is > 0 ? Warehouse.Id : null, State == "Todos" ? null : State, Page), ct);
        if (!Current(r)) return; Items.Clear(); foreach (var row in result.Items) Items.Add(row); Total = result.Total;
        StatusMessage = Total == 0 ? "No hay conteos con estos filtros." : "Listado actualizado.";
    }
    [RelayCommand] private async Task OpenAsync() { if (Selected is null) return; var id = Selected.Id; await RunAsync((r, ct) => ReadCoreAsync(id, r, ct)); }
    private async Task ReadCoreAsync(long id, long r, CancellationToken ct)
    {
        header = null; Lines.Clear(); Confirmed = false; Reason = ""; RefreshPermissions();
        var snapshot = await service.ReadAsync(id, ct); if (!Current(r)) return;
        header = snapshot?.Header; if (snapshot is not null) foreach (var line in snapshot.Lines) Lines.Add(new(line));
        RefreshPermissions(); StatusMessage = snapshot is null ? "El conteo ya no está disponible." : "Detalle actualizado. Una celda vacía sigue pendiente; cero es una cantidad contada.";
    }
    [RelayCommand] private async Task StartAsync()
    {
        if (!CanRead || IsBusy || IsEditing) return;
        var revision = Session.Revision;
        var model = new CountStartDialogModel(Warehouses.Where(w => w.Id > 0).ToArray(), new(products), async form =>
        {
            if (!Current(revision)) return false;
            var start = (CountStartDialogModel)form; bool saved = false; bool answered = false;
            await RunAsync(async (r, ct) =>
            {
                var result = await service.StartAsync(new(start.Warehouse?.Id ?? 0, start.Picker.SelectedIds, start.Reason));
                if (!Current(r)) return;
                answered = true;
                form.StatusMessage = result.Message ?? "No se pudo abrir el conteo.";
                if (!result.IsSuccess) { if (result.ErrorCode == "FORBIDDEN") Session.Clear(); return; }
                saved = true; await SearchCoreAsync(r, ct); await ReadCoreAsync(result.Value, r, ct);
            });
            if (saved && header is null) StatusMessage = "El conteo se abrió, pero no se pudo actualizar el detalle. Busca el conteo antes de abrir otro.";
            if (!answered) { form.CanSubmit = false; form.StatusMessage = "No se pudo confirmar la apertura. Cierra y consulta el listado antes de repetirla."; }
            if (!saved && string.IsNullOrEmpty(form.StatusMessage)) form.StatusMessage = StatusMessage;
            return saved;
        });
        IsEditing = true; try { await dialogs.ShowCountStartAsync(model); } finally { IsEditing = false; }
    }
    [RelayCommand] private async Task CaptureAsync()
    {
        if (!CanEdit || header is null) return;
        var changed = Lines.Where(l => l.Dirty).ToArray();
        if (changed.Length == 0) { StatusMessage = "No hay cantidades nuevas para guardar."; return; }
        var quantities = new List<InventoryCountQuantity>();
        foreach (var line in changed)
        {
            if (!decimal.TryParse(line.Quantity, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var q)) { StatusMessage = "Escribe cantidades con punto decimal. Una cantidad guardada no puede volver a pendiente."; return; }
            quantities.Add(new(line.Data.Id, q));
        }
        var id = header.Id; var version = header.RowVersion.ToArray();
        await RunAsync(async (r, ct) =>
        {
            // Cada lote usa la versión obtenida tras guardar el anterior.
            foreach (var group in quantities.Chunk(1000))
            {
                var result = await service.CaptureAsync(new(id, version, group));
                if (!Current(r)) return;
                if (!result.IsSuccess && result.ErrorCode == "FORBIDDEN") { Session.Clear(); return; }
                if (!result.IsSuccess) { await ReadCoreAsync(id, r, ct); StatusMessage = result.Message ?? "No se pudo guardar; revisa el detalle recargado."; return; }
                await ReadCoreAsync(id, r, ct); if (header is null || !Current(r)) return; version = header.RowVersion.ToArray();
            }
            await SearchCoreAsync(r, ct); StatusMessage = "Cantidades guardadas. Revisa las diferencias antes de cerrar.";
        });
    }
    [RelayCommand] private Task CloseAsync() => FinalizeAsync(false);
    [RelayCommand] private Task CancelAsync() => FinalizeAsync(true);
    private async Task FinalizeAsync(bool cancel)
    {
        if (!CanEdit || header is null || (!cancel && !CanClose)) return;
        if (!Confirmed) { StatusMessage = "Escribe un motivo y marca la confirmación antes de cerrar o cancelar."; return; }
        if (!cancel && Lines.Any(l => l.Dirty)) { StatusMessage = "Guarda las cantidades pendientes antes de cerrar."; return; }
        var request = new FinalizeInventoryCount(header.Id, header.RowVersion.ToArray(), Reason);
        Confirmed = false;
        await RunAsync(async (r, ct) =>
        {
            var result = cancel ? await service.CancelAsync(request) : await service.CloseAsync(request);
            if (!Current(r)) return;
            if (result.ErrorCode == "FORBIDDEN") { Session.Clear(); return; }
            await ReadCoreAsync(request.CountId, r, ct); await SearchCoreAsync(r, ct);
            StatusMessage = result.IsSuccess ? (cancel ? "Conteo cancelado; historial conservado." : "Conteo cerrado y diferencias aplicadas.") : result.Message ?? "No se pudo finalizar. Revisa el estado actualizado.";
        });
    }
}
