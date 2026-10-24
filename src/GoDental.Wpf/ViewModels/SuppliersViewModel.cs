using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed partial class SuppliersViewModel : WorkflowViewModel
{
    private readonly SupplierService service;
    private readonly ProductService products;
    private readonly IWorkflowDialogService dialogs;
    public ObservableCollection<SupplierData> Items { get; } = [];
    public ObservableCollection<SupplierContactData> Contacts { get; } = [];
    public ObservableCollection<SupplierProductData> References { get; } = [];
    [ObservableProperty] private SupplierData? selected;
    [ObservableProperty] private SupplierData? detail;
    [ObservableProperty] private SupplierContactData? selectedContact;
    [ObservableProperty] private SupplierProductData? selectedProduct;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool includeInactive;
    [ObservableProperty] private bool areFiltersOpen;
    [RelayCommand] private void ToggleFilters() => AreFiltersOpen = !AreFiltersOpen;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool isDetailOpen;
    [RelayCommand] private void ReturnToList() => IsDetailOpen = false;
    [RelayCommand] private async Task RefreshDetailAsync()
    {
        if (Detail is null) return;
        var id = Detail.Id;
        await RunAsync((revision, ct) => ReadCoreAsync(id, revision, ct));
    }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int page = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel)), NotifyPropertyChangedFor(nameof(IsEmpty))] private int total;
    public bool CanRead => service.CanRead;
    public bool CanManage => service.CanManage;
    public bool CanEdit => CanManage && IsNotBusy && !IsEditing;
    public bool CanEditChildren => CanEdit && Detail?.Active == true;
    public bool IsEmpty => Total == 0;
    public string PageLabel => Total == 0 ? "Sin resultados" : $"Mostrando {(Page - 1) * 30 + 1}–{Math.Min(Page * 30, Total)} de {Total} · Página {Page}";
    protected override bool CanAccess => CanRead;
    public SuppliersViewModel(SupplierService service, ProductService products, ISessionController session,
        IWorkflowDialogService dialogs, ILogger<SuppliersViewModel> logger) : base(session, logger)
    { this.service = service; this.products = products; this.dialogs = dialogs; }
    partial void OnDetailChanged(SupplierData? value) { IsDetailOpen = value is not null; RefreshPermissions(); }
    partial void OnIsEditingChanged(bool value) => RefreshPermissions();
    protected override void RefreshPermissions() { OnPropertyChanged(nameof(CanRead)); OnPropertyChanged(nameof(CanManage)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditChildren)); }
    protected override void Reset() { Items.Clear(); Contacts.Clear(); References.Clear(); Detail = null; Selected = null; SelectedContact = null; SelectedProduct = null; Total = 0; }
    [RelayCommand] private async Task ReloadAsync() { Page = 1; await RunAsync(SearchCoreAsync); }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await RunAsync(SearchCoreAsync); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await RunAsync(SearchCoreAsync); }
    private async Task SearchCoreAsync(long r, CancellationToken ct)
    {
        var result = await service.SearchAsync(new(Search, IncludeInactive, Page), ct);
        if (!Current(r)) return; Items.Clear(); foreach (var s in result.Items) Items.Add(s); Total = result.Total;
        StatusMessage = Total == 0 ? "No hay proveedores con estos filtros." : "Listado actualizado.";
    }
    [RelayCommand] private Task OpenAsync() => OpenSupplierAsync(Selected);
    [RelayCommand] private async Task OpenSupplierAsync(SupplierData? supplier)
    {
        if (supplier is null || IsBusy || !CanRead) return;
        Selected = supplier;
        await RunAsync((r, ct) => ReadCoreAsync(supplier.Id, r, ct));
    }
    private async Task ReadCoreAsync(int id, long r, CancellationToken ct)
    {
        Detail = null; Contacts.Clear(); References.Clear(); SelectedContact = null; SelectedProduct = null;
        var snapshot = await service.ReadAsync(id, ct); if (!Current(r)) return;
        Detail = snapshot?.Supplier;
        if (snapshot is not null)
        {
            foreach (var c in snapshot.Contacts) Contacts.Add(c);
            foreach (var p in snapshot.Products) References.Add(p);
        }
        StatusMessage = Detail is null ? "El proveedor ya no está disponible." : "Ficha actualizada.";
    }
    [RelayCommand] private Task NewAsync() => EditAsync(SupplierEditorKind.Supplier, true);
    [RelayCommand] private Task EditSupplierAsync() => EditAsync(SupplierEditorKind.Supplier, false);
    [RelayCommand] private Task NewContactAsync() => EditAsync(SupplierEditorKind.Contact, true);
    [RelayCommand] private Task EditContactAsync() => EditAsync(SupplierEditorKind.Contact, false);
    [RelayCommand] private Task NewProductAsync() => EditAsync(SupplierEditorKind.Product, true);
    [RelayCommand] private Task EditProductAsync() => EditAsync(SupplierEditorKind.Product, false);
    private async Task EditAsync(SupplierEditorKind kind, bool create)
    {
        if (!CanEdit || (!create && Detail is null) || (kind != SupplierEditorKind.Supplier && !CanEditChildren)
            || (kind == SupplierEditorKind.Contact && !create && SelectedContact is null)
            || (kind == SupplierEditorKind.Product && !create && SelectedProduct is null)) return;
        var revision = Session.Revision;
        IReadOnlyList<CatalogOption> currencies = [];
        if (kind == SupplierEditorKind.Product)
        {
            await RunAsync(async (r, ct) => { var values = await service.CurrenciesAsync(ct); if (Current(r)) currencies = values; });
            if (!Current(revision) || !CanEditChildren) return;
        }
        var supplier = kind == SupplierEditorKind.Supplier && create ? new SupplierData() : Detail!;
        var model = new SupplierEditorDialogModel(kind, supplier, create ? null : SelectedContact,
            create ? null : SelectedProduct, new(products), currencies, async form =>
        {
            if (!Current(revision)) return false;
            var editor = (SupplierEditorDialogModel)form;
            if (!editor.PrepareFields()) return false;
            var product = kind == SupplierEditorKind.Product ? editor.BuildProduct() : null;
            if (kind == SupplierEditorKind.Product && product is null) return false;
            bool saved = false; bool answered = false;
            await RunAsync(async (r, ct) =>
            {
                Result<int> result = kind switch
                {
                    SupplierEditorKind.Supplier => await service.SaveAsync(new(editor.Supplier, editor.Reason)),
                    SupplierEditorKind.Contact => await service.SaveContactAsync(new(supplier.Id, supplier.RowVersion.ToArray(), editor.Contact, editor.Reason)),
                    _ => await service.SaveProductAsync(new(supplier.Id, supplier.RowVersion.ToArray(), product!, editor.Reason))
                };
                if (!Current(r)) return;
                answered = true;
                form.StatusMessage = result.Message ?? "No se pudo guardar.";
                if (!result.IsSuccess)
                {
                    if (result.ErrorCode is "CONCURRENCY_CONFLICT" or "INVALID_STATE")
                    {
                        form.CanSubmit = false;
                        await ReadCoreAsync(supplier.Id, r, ct);
                        form.StatusMessage += " Cierra el formulario y vuelve a abrirlo desde la ficha actualizada.";
                    }
                    if (result.ErrorCode == "FORBIDDEN") { Reset(); Session.Clear(); }
                    return;
                }
                saved = true;
                await ReadCoreAsync(result.Value, r, ct); await SearchCoreAsync(r, ct);
                StatusMessage = "Cambio guardado; ficha y versión actualizadas.";
            });
            if (saved && Detail is null) StatusMessage = "El cambio se guardó, pero no se pudo actualizar la ficha. Consulta de nuevo antes de editar.";
            if (!answered) { form.CanSubmit = false; form.StatusMessage = "No se pudo confirmar el resultado. Cierra y consulta la ficha o el listado antes de repetir el cambio."; }
            if (!saved) form.StatusMessage = string.IsNullOrEmpty(form.StatusMessage) ? StatusMessage : form.StatusMessage;
            return saved;
        });
        IsEditing = true; try { await dialogs.ShowSupplierEditorAsync(model); } finally { IsEditing = false; }
    }
}
