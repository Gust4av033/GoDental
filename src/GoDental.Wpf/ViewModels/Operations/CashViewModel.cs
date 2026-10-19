using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class CashViewModel : MvpWorkspaceViewModel
{
    private readonly CashService cash;
    private readonly InventoryService inventory;
    private readonly IMvpDialogService dialogs;
    public ObservableCollection<CashBoxData> Boxes { get; } = [];
    public ObservableCollection<CashMovementData> Movements { get; } = [];
    [ObservableProperty] private CashBoxData? box;
    [ObservableProperty] private CashShiftData? shift;
    [ObservableProperty] private int page = 1;
    [ObservableProperty] private int total;
    [ObservableProperty] private string shiftLookup = "";
    public override bool CanRead => cash.CanRead;
    public bool CanOpen => CanEdit && cash.CanOpen && Box?.Active == true && Shift?.State != "ABIERTO";
    public bool CanMove => CanEdit && cash.CanMove && Shift?.State == "ABIERTO";
    public bool CanClose => CanEdit && cash.CanClose && Shift?.State == "ABIERTO";
    public bool CanManageBoxes => CanEdit && inventory.CanRead && Has("AJUSTES.EDITAR");
    public string ShiftLabel => Shift is null ? "No hay turno abierto. Selecciona una caja y consulta." : $"Turno {Shift.Id} · {Shift.State}";
    protected override void PermissionsChanged() { foreach (var n in new[] { nameof(CanOpen), nameof(CanMove), nameof(CanClose), nameof(CanManageBoxes), nameof(ShiftLabel) }) OnPropertyChanged(n); }
    partial void OnBoxChanged(CashBoxData? value) { Shift = null; Movements.Clear(); PermissionsChanged(); }
    partial void OnShiftChanged(CashShiftData? value) => PermissionsChanged();
    public CashViewModel(CashService cash, InventoryService inventory, IMvpDialogService dialogs, ISessionController session, MvpOperationService operations, ILogger<CashViewModel> logger) : base(session, operations, logger) { this.cash = cash; this.inventory = inventory; this.dialogs = dialogs; }
    protected override void Reset() { Boxes.Clear(); Movements.Clear(); Shift = null; Box = null; Total = 0; Page = 1; ShiftLookup = ""; }
    [RelayCommand] private Task ReloadAsync() => ReadAsync(async r =>
    {
        var boxes = await cash.BoxesAsync(); if (!Current(r)) return;
        var id = Box?.Id; Boxes.Clear(); foreach (var b in boxes) Boxes.Add(b); Box = Boxes.FirstOrDefault(b => b.Id == id) ?? Boxes.FirstOrDefault(b => b.Active);
        await CurrentCoreAsync(r);
    });
    [RelayCommand] private Task LoadShiftAsync() => ReadAsync(CurrentCoreAsync);
    private async Task CurrentCoreAsync(long r)
    {
        if (Box is null) { StatusMessage = "No hay cajas disponibles. Registra una caja con permiso de Ajustes."; return; }
        var shift = await cash.CurrentAsync(Box.Id); if (!Current(r)) return;
        Shift = shift; Page = 1; await MovementsCoreAsync(r);
    }
    [RelayCommand] private Task FindShiftAsync() => ReadAsync(async r =>
    {
        if (!long.TryParse(ShiftLookup, out var id) || id <= 0) throw new ArgumentException("Escribe un número de turno válido.");
        var shift = await cash.ReadAsync(id); if (!Current(r)) return;
        Shift = shift; Page = 1; await MovementsCoreAsync(r);
    });
    private async Task MovementsCoreAsync(long r)
    {
        Movements.Clear(); Total = 0;
        if (Shift is null) { StatusMessage = "La caja no tiene un turno abierto. Puedes abrirlo con el fondo inicial."; return; }
        var result = await cash.MovementsAsync(Shift.Id, new(Page)); if (!Current(r)) return;
        foreach (var m in result.Items) Movements.Add(m); Total = result.Total;
        StatusMessage = Total == 0 ? "El turno no tiene movimientos." : $"{Total} movimientos · Página {Page}";
    }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await ReadAsync(MovementsCoreAsync); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await ReadAsync(MovementsCoreAsync); }
    private async Task ShowAsync(MvpFormModel form) { using (form) { IsEditing = true; try { await dialogs.ShowAsync(form); } finally { IsEditing = false; } } }
    [RelayCommand] private async Task OpenAsync()
    {
        if (!CanOpen || Box is null) return; var box = Box;
        await ShowAsync(new MvpFormModel("Abrir caja · " + box.Name, "Abrir caja", Operation, async f =>
        {
            var id = Guid.NewGuid(); var request = new OpenCashShift(id, box.Id, f.Number("amount"), f.Text("reason"));
            await Operation.StartAsync(request, id, q => cash.OpenAsync(q), receipt => ReadAsync(CurrentCoreAsync));
        }).Input("amount", "Fondo inicial", "0").Input("reason", "Motivo de apertura"));
    }
    [RelayCommand] private async Task MoveAsync()
    {
        if (!CanMove || Shift is null) return; var shift = Shift;
        await ShowAsync(new MvpFormModel($"Movimiento · Turno {shift.Id}", "Registrar", Operation, async f =>
        {
            var id = Guid.NewGuid(); var request = new CashMovement(id, shift.Id, shift.RowVersion.ToArray(), f.Choice<string>("type"), f.Number("amount"), f.Text("reason"));
            await Operation.StartAsync(request, id, q => cash.MoveAsync(q), _ => ReadAsync(CurrentCoreAsync));
        }).Select("type", "Tipo de movimiento", new[] { "INGRESO", "RETIRO" }, "INGRESO", "").Input("amount", "Importe", "0").Input("reason", "Motivo"));
    }
    [RelayCommand] private async Task CloseAsync()
    {
        if (!CanClose || Shift is null) return; var shift = Shift;
        await ShowAsync(new MvpFormModel($"Cerrar turno {shift.Id}", "Cerrar caja", Operation, async f =>
        {
            var id = Guid.NewGuid(); var request = new CloseCashShift(id, shift.Id, shift.RowVersion.ToArray(), f.Number("amount"), f.Text("reason"));
            await Operation.StartAsync(request, id, q => cash.CloseAsync(q), receipt => ReadAsync(async r =>
            {
                var closed = await cash.ReadAsync(receipt.Id); if (!Current(r)) return; Shift = closed; await MovementsCoreAsync(r);
            }));
        }) { Description = $"Efectivo esperado: {shift.Expected:N2}. Cuenta el efectivo físico. El servicio determinará la diferencia del arqueo." }.Input("amount", "Efectivo contado", "0").Input("reason", "Motivo / explicación de la diferencia"));
    }
    [RelayCommand] private Task NewBoxAsync() => EditBoxAsync(false);
    [RelayCommand] private Task UpdateBoxAsync() => EditBoxAsync(true);
    private async Task EditBoxAsync(bool editing)
    {
        if (!CanManageBoxes || editing && Box is null) return;
        await ReadAsync(async r =>
        {
            var warehouses = await inventory.WarehousesAsync(); if (!Current(r)) return;
            var b = editing ? Box : null;
            await ShowAsync(new MvpFormModel(editing ? "Editar caja" : "Nueva caja", "Guardar", Operation, async f =>
            {
                var id = Guid.NewGuid(); var request = new CashBoxChange(id, b?.Id ?? 0, f.Text("code"), f.Text("name"), f.Choice<CatalogOption>("warehouse").Id, f.Choice<string>("active") == "ACTIVA", f.Text("reason"));
                await Operation.StartAsync(request, id, q => cash.SaveBoxAsync(q), _ => Task.CompletedTask);
            }).Input("code", "Código", b?.Code ?? "").Input("name", "Nombre", b?.Name ?? "").Select("warehouse", "Almacén", warehouses, warehouses.FirstOrDefault(w => w.Id == b?.WarehouseId)).Select("active", "Estado", new[] { "ACTIVA", "INACTIVA" }, b?.Active == false ? "INACTIVA" : "ACTIVA", "").Input("reason", "Motivo"));
        });
        if (!Operation.HasPending) await ReloadAsync();
    }
}
