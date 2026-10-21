using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Domain.Rules;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class PurchasesViewModel : MvpWorkspaceViewModel
{
    private readonly PurchaseService service;
    private readonly IMvpDialogService dialogs;
    private PurchaseCatalogs? catalogs;
    public ObservableCollection<PurchaseOrderData> Items { get; } = [];
    public IReadOnlyList<string> States { get; } = ["Todos", "BORRADOR", "ENVIADA", "CONFIRMADA", "EN_PRODUCCION", "DESPACHADA", "RECIBIDA_PARCIAL", "RECIBIDA", "CANCELADA"];
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string state = "Todos";
    [ObservableProperty] private PurchaseOrderData? selected;
    [ObservableProperty] private PurchaseReceiptData? selectedReceipt;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsListOpen))] private bool isDetailOpen;
    public bool IsListOpen => !IsDetailOpen;
    [ObservableProperty] private PurchaseOrderSnapshot? detail;
    [ObservableProperty] private PurchaseReceiptSnapshot? receiptDetail;
    [ObservableProperty] private int page = 1;
    [ObservableProperty] private int total;
    public string PageLabel => $"{Total} órdenes · Página {Page}";
    public override bool CanRead => service.CanRead;
    public bool CanManage => CanEdit && service.CanManage;
    public bool CanCreate => CanManage && catalogs is not null;
    public bool CanEditOrder => CanManage && Detail?.Order.State == "BORRADOR";
    public bool CanTransition => CanManage && Detail is not null && States.Any(s => PurchaseRules.CanTransition(Detail.Order.State, s, Detail.Lines.Any(l => l.Received > 0)));
    public bool CanPrepare => CanManage && Detail is not null && PurchaseRules.CanReceive(Detail.Order.State) && catalogs is not null;
    public bool CanActOnReceipt => CanManage && ReceiptDetail?.Receipt.State == "BORRADOR";
    protected override void PermissionsChanged() { foreach (var property in new[] { nameof(CanManage), nameof(CanCreate), nameof(CanEditOrder), nameof(CanTransition), nameof(CanPrepare), nameof(CanActOnReceipt) }) OnPropertyChanged(property); }
    protected override void Reset() { catalogs = null; Items.Clear(); Selected = null; Detail = null; ReceiptDetail = null; SelectedReceipt = null; IsDetailOpen = false; Page = 1; Total = 0; PermissionsChanged(); }
    partial void OnDetailChanged(PurchaseOrderSnapshot? value) => PermissionsChanged();
    partial void OnReceiptDetailChanged(PurchaseReceiptSnapshot? value) => PermissionsChanged();
    partial void OnSelectedChanged(PurchaseOrderData? value) { Detail = null; ReceiptDetail = null; SelectedReceipt = null; }
    partial void OnSelectedReceiptChanged(PurchaseReceiptData? value) => ReceiptDetail = null;
    partial void OnPageChanged(int value) => OnPropertyChanged(nameof(PageLabel));
    partial void OnTotalChanged(int value) => OnPropertyChanged(nameof(PageLabel));
    public PurchasesViewModel(PurchaseService service, IMvpDialogService dialogs, ISessionController session, MvpOperationService operations,
        ILogger<PurchasesViewModel> logger) : base(session, operations, logger) { this.service = service; this.dialogs = dialogs; }
    [RelayCommand] private Task ReloadAsync() { if (IsBusy) return Task.CompletedTask; Page = 1; return LoadAsync(); }
    [RelayCommand] private Task NextAsync() { if (IsBusy || Page * 30 >= Total) return Task.CompletedTask; Page++; return LoadAsync(); }
    [RelayCommand] private Task PreviousAsync() { if (IsBusy || Page <= 1) return Task.CompletedTask; Page--; return LoadAsync(); }
    private Task LoadAsync() => ReadAsync(async revision =>
    {
        Items.Clear(); Selected = null; Detail = null; ReceiptDetail = null; IsDetailOpen = false; catalogs = null; PermissionsChanged();
        var choices = await service.CatalogsAsync(); var rows = await service.SearchAsync(Search, State == "Todos" ? null : State, new(Page));
        if (!Current(revision)) return;
        catalogs = choices; foreach (var item in rows.Items) Items.Add(item); Total = rows.Total; PermissionsChanged();
        StatusMessage = rows.Total == 0 ? "No hay órdenes para estos filtros. Crea una orden para comenzar." : "Abre una orden para consultar líneas, pendientes y recepciones.";
    });
    [RelayCommand] private Task OpenAsync() => Selected is { } selected ? ReadAsync(async revision =>
    {
        var result = await service.ReadAsync(selected.Id);
        if (!Current(revision) || Selected?.Id != selected.Id) return;
        Detail = result; ReceiptDetail = null; SelectedReceipt = null; IsDetailOpen = result is not null;
        StatusMessage = result is null ? "La orden ya no está disponible." : "El stock cambia únicamente al aplicar una recepción con costo completo.";
    }) : Task.CompletedTask;
    [RelayCommand] private Task RefreshDetailAsync() => !IsBusy && !IsEditing && Detail is { } d ? RefreshAsync(d.Order.Id, SelectedReceipt?.Id) : Task.CompletedTask;
    [RelayCommand] private void Back() { if (!IsBusy && !IsEditing) { IsDetailOpen = false; ReceiptDetail = null; } }
    [RelayCommand] private Task ReadReceiptAsync() => SelectedReceipt is { } receipt ? ReadAsync(async revision =>
    {
        var result = await service.ReceiptAsync(receipt.Id);
        if (Current(revision) && SelectedReceipt?.Id == receipt.Id) ReceiptDetail = result;
    }) : Task.CompletedTask;
    private Task RefreshAsync(long orderId, long? receiptId = null) => ReadAsync(async revision =>
    {
        var result = await service.ReadAsync(orderId); var receipt = receiptId is { } id ? await service.ReceiptAsync(id) : null;
        if (!Current(revision)) return;
        Detail = result; IsDetailOpen = result is not null; SelectedReceipt = result?.Receipts.FirstOrDefault(r => r.Id == receiptId); ReceiptDetail = receipt;
    });
    [RelayCommand] private Task NewAsync() => EditAsync(null);
    [RelayCommand] private Task EditOrderAsync() => CanEditOrder ? EditAsync(Detail) : Task.CompletedTask;
    private async Task EditAsync(PurchaseOrderSnapshot? original)
    {
        if (!CanCreate || catalogs is null) return;
        var draft = new PurchaseOrderDraftModel(service, () => Session.Revision, catalogs, original);
        using var form = new MvpFormModel(original is null ? "Nueva orden de compra" : "Editar orden en borrador", "Guardar orden", Operation, async f =>
        {
            if (draft.IsBusy || draft.Supplier is null || draft.Currency is null || draft.Date is null) throw new ArgumentException("Completa proveedor, moneda y fecha; espera la búsqueda de productos.");
            var id = Guid.NewGuid(); var request = new SavePurchaseOrder(id, original?.Order.Id ?? 0, original?.Order.RowVersion.ToArray() ?? [], draft.Supplier.Id, draft.Currency.Id, CheckoutModel.Parse(draft.Rate, "tipo de cambio estimado"), draft.Date.Value.Date, draft.Snapshot(), f.Text("reason"));
            await Operation.StartAsync(request, id, r => service.SaveAsync(r), receipt => RefreshAsync(receipt.Id));
        }) { Body = draft, Description = "Importes en moneda origen. Tipo de cambio = USD por una unidad de esa moneda; USD usa 1. Guardar no ingresa unidades al inventario." };
        form.Input("reason", "Motivo / observaciones"); await DialogAsync(form);
    }
    [RelayCommand] private async Task TransitionAsync()
    {
        if (!CanTransition || Detail is not { } source) return;
        var choices = States.Where(s => PurchaseRules.CanTransition(source.Order.State, s, source.Lines.Any(l => l.Received > 0))).ToArray();
        using var form = new MvpFormModel("Cambiar estado de orden", "Guardar estado", Operation, async f =>
        {
            var id = Guid.NewGuid(); var request = new PurchaseTransition(id, source.Order.Id, source.Order.RowVersion.ToArray(), f.Choice<string>("state"), f.Text("reason"));
            await Operation.StartAsync(request, id, r => service.TransitionAsync(r), _ => RefreshAsync(source.Order.Id));
        }) { Description = $"{source.Order.Number} · {source.Order.State}. Cancelar no revierte recepciones aplicadas. Los estados de recepción se calculan al ingresar unidades." };
        form.Select("state", "Nuevo estado", choices, choices.First(), "").Input("reason", "Motivo"); await DialogAsync(form);
    }
    [RelayCommand] private async Task PrepareAsync()
    {
        if (!CanPrepare || Detail is not { } source || catalogs is null) return;
        var draft = new PurchaseReceiptDraftModel(source, catalogs.Warehouses);
        using var form = new MvpFormModel("Preparar recepción de compra", "Guardar borrador", Operation, async f =>
        {
            if (draft.Warehouse is null) throw new ArgumentException("Selecciona el almacén de recepción.");
            var id = Guid.NewGuid(); var request = new PreparePurchaseReceipt(id, source.Order.Id, source.Order.RowVersion.ToArray(), draft.Warehouse.Id, CheckoutModel.Parse(draft.Rate, "cambio real"), draft.CostsComplete, draft.Snapshot(), f.Text("reason"));
            await Operation.StartAsync(request, id, r => service.PrepareAsync(r), receipt => RefreshAsync(source.Order.Id, receipt.Id));
        }) { Body = draft, Description = $"{source.Order.Number} · {source.Order.Currency}. Compra directa con costos conocidos. El gasto adicional es USD por unidad; no distribuye gastos de importación. El borrador no crea stock; revísalo y aplica después." };
        form.Input("reason", "Motivo de la recepción"); await DialogAsync(form);
    }
    [RelayCommand] private Task ApplyAsync() => ActAsync(true);
    [RelayCommand] private Task CancelReceiptAsync() => ActAsync(false);
    private async Task ActAsync(bool apply)
    {
        if (!CanActOnReceipt || ReceiptDetail is not { } receipt || Detail is not { } order) return;
        using var form = new MvpFormModel(apply ? "Aplicar recepción" : "Anular borrador", apply ? "Aplicar recepción" : "Anular borrador", Operation, async f =>
        {
            var id = Guid.NewGuid(); var request = new PurchaseReceiptAction(id, receipt.Receipt.Id, receipt.Receipt.RowVersion.ToArray(), order.Order.RowVersion.ToArray(), apply, f.Text("reason"));
            await Operation.StartAsync(request, id, r => service.ApplyAsync(r), _ => RefreshAsync(order.Order.Id, receipt.Receipt.Id));
        }) { Description = apply ? $"{receipt.Receipt.Number} · {receipt.Receipt.Warehouse}. Se ingresarán {receipt.Lines.Sum(l => l.Quantity):0.####} unidades físicas, con costo en USD, lotes y Kardex. Costo completo: {(receipt.Receipt.CostsComplete ? "Sí" : "No; será rechazado hasta corregir el borrador")}. La operación no se revierte desde esta pantalla."
            : $"{receipt.Receipt.Number}. Se conserva el borrador como ANULADA sin ingresar stock. Para corregir cantidades o costos crea otro borrador desde la orden actualizada." };
        form.Input("reason", "Motivo"); await DialogAsync(form);
    }
    private async Task DialogAsync(MvpFormModel form) { IsEditing = true; try { await dialogs.ShowAsync(form); } finally { IsEditing = false; } }
}
