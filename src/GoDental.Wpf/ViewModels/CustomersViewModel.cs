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

public sealed partial class CustomersViewModel : WorkflowViewModel
{
    private readonly CustomerService service;
    private readonly ICustomerDialogService dialogs;
    private CustomerCatalogs catalogs = new([], []);
    public ObservableCollection<CustomerData> Items { get; } = [];
    public ObservableCollection<CatalogOption> Types { get; } = [];
    public ObservableCollection<CustomerContactData> Contacts { get; } = [];
    public ObservableCollection<CustomerAddressData> Addresses { get; } = [];
    public ObservableCollection<CustomerSaleData> Sales { get; } = [];
    public ObservableCollection<CustomerAccountData> Accounts { get; } = [];
    public ObservableCollection<CustomerPaymentData> Payments { get; } = [];
    [ObservableProperty] private CustomerData? selected;
    [ObservableProperty] private CustomerData? detail;
    [ObservableProperty] private CustomerCreditData? credit;
    [ObservableProperty] private CustomerBalanceData? balance;
    [ObservableProperty] private CustomerContactData? selectedContact;
    [ObservableProperty] private CustomerAddressData? selectedAddress;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private CatalogOption? type;
    [ObservableProperty] private bool includeInactive;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool isDetailOpen;
    [ObservableProperty] private bool areFiltersOpen;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SalesEmptyMessage))] private bool hasLoadedSales;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AccountsEmptyMessage))] private bool hasLoadedAccounts;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PaymentsEmptyMessage))] private bool hasLoadedPayments;
    public string SalesEmptyMessage => HasLoadedSales ? "Este cliente no tiene ventas registradas." : "Pulsa Consultar ventas para cargar el historial.";
    public string AccountsEmptyMessage => HasLoadedAccounts ? "Este cliente no tiene cuentas por cobrar." : "Pulsa Consultar cuentas para cargar el estado de cuenta.";
    public string PaymentsEmptyMessage => HasLoadedPayments ? "Este cliente no tiene abonos registrados." : "Pulsa Consultar abonos para cargar los pagos.";
    [RelayCommand] private void ToggleFilters() => AreFiltersOpen = !AreFiltersOpen;
    [RelayCommand] private void ReturnToList() => IsDetailOpen = false;
    [RelayCommand] private async Task RefreshDetailAsync()
    {
        if (Detail is null) return;
        var id = Detail.Id;
        await RunAsync((revision, ct) => ReadCoreAsync(id, revision, ct));
    }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int page = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel)), NotifyPropertyChangedFor(nameof(IsEmpty))] private int total;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SalesLabel))] private int salesPage = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AccountsLabel))] private int accountsPage = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PaymentsLabel))] private int paymentsPage = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SalesLabel))] private int salesTotal;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AccountsLabel))] private int accountsTotal;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PaymentsLabel))] private int paymentsTotal;
    public bool CanRead => service.CanRead;
    public bool CanCreate => service.CanCreate && IsNotBusy && !IsEditing;
    public bool CanEdit => service.CanEdit && Detail is not null && IsNotBusy && !IsEditing;
    public bool CanEditChildren => CanEdit && Detail?.Active == true;
    public bool CanConfigureCredit => service.CanConfigureCredit && Detail is not null && IsNotBusy && !IsEditing;
    public bool CanReadHistory => service.CanReadHistory;
    public bool IsEmpty => Total == 0;
    public string PageLabel => Total == 0 ? "0 clientes" : $"Mostrando {(Page - 1) * 30 + 1}–{Math.Min(Page * 30, Total)} de {Total} clientes · Página {Page}";
    public string SalesLabel => $"{SalesTotal} ventas · Página {SalesPage}";
    public string AccountsLabel => $"{AccountsTotal} cuentas · Página {AccountsPage}";
    public string PaymentsLabel => $"{PaymentsTotal} abonos · Página {PaymentsPage}";
    protected override bool CanAccess => CanRead;
    public CustomersViewModel(CustomerService service, ISessionController session, ICustomerDialogService dialogs, ILogger<CustomersViewModel> logger) : base(session, logger) { this.service = service; this.dialogs = dialogs; }
    partial void OnDetailChanged(CustomerData? value) { IsDetailOpen = value is not null; RefreshPermissions(); }
    partial void OnIsEditingChanged(bool value) => RefreshPermissions();
    protected override void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanRead)); OnPropertyChanged(nameof(CanCreate)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditChildren)); OnPropertyChanged(nameof(CanConfigureCredit)); OnPropertyChanged(nameof(CanReadHistory));
    }
    private void ClearDetail() { HasLoadedSales = HasLoadedAccounts = HasLoadedPayments = false; Detail = null; Credit = null; Balance = null; Contacts.Clear(); Addresses.Clear(); Sales.Clear(); Accounts.Clear(); Payments.Clear(); SelectedContact = null; SelectedAddress = null; SalesPage = AccountsPage = PaymentsPage = 1; SalesTotal = AccountsTotal = PaymentsTotal = 0; }
    protected override void Reset() { AreFiltersOpen = false; ClearDetail(); Items.Clear(); Types.Clear(); Type = null; Selected = null; Total = 0; catalogs = new([], []); }
    [RelayCommand] private async Task ReloadAsync()
    {
        Page = 1;
        await RunAsync(async (r, ct) =>
        {
            var values = await service.CatalogsAsync(ct); if (!Current(r)) return; catalogs = values;
            var id = Type?.Id; Types.Clear(); Types.Add(new() { Id = 0, Name = "Todos los tipos" }); foreach (var t in values.Types) Types.Add(t); Type = Types.FirstOrDefault(t => t.Id == id) ?? Types.First();
            await SearchCoreAsync(r, ct);
        });
    }
    [RelayCommand] private async Task SearchAsync() { Page = 1; await RunAsync(SearchCoreAsync); }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await RunAsync(SearchCoreAsync); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await RunAsync(SearchCoreAsync); }
    private async Task SearchCoreAsync(long r, CancellationToken ct)
    {
        var result = await service.SearchAsync(new(Search, IncludeInactive, Type?.Id is > 0 ? checked((short)Type.Id) : null, Page), ct);
        if (!Current(r)) return; Items.Clear(); foreach (var c in result.Items) Items.Add(c); Total = result.Total;
        StatusMessage = "";
    }
    [RelayCommand] private async Task OpenAsync() { if (Selected is null) return; var id = Selected.Id; await RunAsync((r, ct) => ReadCoreAsync(id, r, ct)); }
    // El enlace de fila recibe el cliente concreto y no depende de cuándo cambie SelectedItem.
    [RelayCommand] private async Task OpenCustomerAsync(CustomerData? customer)
    {
        if (customer is null || IsBusy || !CanRead) return;
        Selected = customer;
        await RunAsync((r, ct) => ReadCoreAsync(customer.Id, r, ct));
    }
    private async Task ReadCoreAsync(long id, long r, CancellationToken ct)
    {
        ClearDetail(); var value = await service.ReadAsync(id, ct); if (!Current(r)) return;
        Detail = value?.Customer; Credit = value?.Credit;
        if (value is not null) { foreach (var c in value.Contacts) Contacts.Add(c); foreach (var a in value.Addresses) Addresses.Add(a); }
        StatusMessage = value is null ? "El cliente ya no está disponible." : "";
    }
    [RelayCommand] private Task NewAsync() => EditAsync(CustomerEditorKind.General, true);
    [RelayCommand] private Task EditCustomerAsync() => EditAsync(CustomerEditorKind.General, false);
    [RelayCommand] private Task NewContactAsync() => EditAsync(CustomerEditorKind.Contact, true);
    [RelayCommand] private Task EditContactAsync() => EditAsync(CustomerEditorKind.Contact, false);
    [RelayCommand] private Task NewAddressAsync() => EditAsync(CustomerEditorKind.Address, true);
    [RelayCommand] private Task EditAddressAsync() => EditAsync(CustomerEditorKind.Address, false);
    [RelayCommand] private Task EditCreditAsync() => EditAsync(CustomerEditorKind.Credit, false);
    private async Task EditAsync(CustomerEditorKind kind, bool create)
    {
        if (IsBusy || IsEditing || (kind == CustomerEditorKind.General && !(create ? CanCreate : CanEdit))
            || (kind is CustomerEditorKind.Contact or CustomerEditorKind.Address && !CanEditChildren)
            || (kind == CustomerEditorKind.Credit && !CanConfigureCredit)
            || (!create && kind == CustomerEditorKind.Contact && SelectedContact is null)
            || (!create && kind == CustomerEditorKind.Address && SelectedAddress is null)) return;
        var revision = Session.Revision;
        var customer = kind == CustomerEditorKind.General && create ? new CustomerData() : Detail!;
        var model = new CustomerEditorDialogModel(kind, customer, create ? null : SelectedContact, create ? null : SelectedAddress, Credit, catalogs, async form =>
        {
            if (!Current(revision)) return false;
            var editor = (CustomerEditorDialogModel)form; if (!editor.PrepareFields()) return false; var credit = kind == CustomerEditorKind.Credit ? editor.BuildCredit() : null;
            if (kind == CustomerEditorKind.Credit && credit is null) return false;
            bool saved = false, answered = false;
            await RunAsync(async (r, ct) =>
            {
                Result<long> result = kind switch
                {
                    CustomerEditorKind.General => await service.SaveAsync(new(editor.BuildCustomer(), editor.Reason)),
                    CustomerEditorKind.Contact => await service.SaveContactAsync(new(customer.Id, customer.RowVersion.ToArray(), editor.Contact, editor.Reason)),
                    CustomerEditorKind.Address => await service.SaveAddressAsync(new(customer.Id, customer.RowVersion.ToArray(), editor.Address, editor.Reason)),
                    _ => await service.SaveCreditAsync(new(customer.Id, customer.RowVersion.ToArray(), credit!, editor.Reason))
                };
                if (!Current(r)) return; answered = true;
                if (!result.IsSuccess)
                {
                    form.StatusMessage = result.Message ?? "No se pudo guardar.";
                    if (result.ErrorCode is "CONCURRENCY_CONFLICT" or "INVALID_STATE") { form.CanSubmit = false; await ReadCoreAsync(customer.Id, r, ct); form.StatusMessage += " Cierra y abre de nuevo el formulario desde la ficha actualizada."; }
                    if (result.ErrorCode == "FORBIDDEN") { form.CanSubmit = false; Session.Clear(); }
                    return;
                }
                saved = true; await ReadCoreAsync(result.Value, r, ct); await SearchCoreAsync(r, ct); StatusMessage = "Cambio guardado; ficha y versión actualizadas.";
            });
            if (saved && Detail is null) StatusMessage = "El cambio se guardó, pero falló la recarga. Consulta de nuevo antes de editar.";
            if (!answered) { form.CanSubmit = false; form.StatusMessage = "No se pudo confirmar el resultado. Cierra y consulta el cliente antes de repetir el guardado."; }
            return saved;
        }, Contacts.ToArray());
        IsEditing = true; try { await dialogs.ShowCustomerEditorAsync(model); } finally { IsEditing = false; }
    }
    [RelayCommand] private async Task LoadSalesAsync() { SalesPage = 1; await RunAsync(SalesCoreAsync); }
    [RelayCommand] private async Task NextSalesAsync() { if (IsBusy || SalesPage * 30 >= SalesTotal) return; SalesPage++; await RunAsync(SalesCoreAsync); }
    [RelayCommand] private async Task PreviousSalesAsync() { if (IsBusy || SalesPage <= 1) return; SalesPage--; await RunAsync(SalesCoreAsync); }
    private async Task SalesCoreAsync(long r, CancellationToken ct)
    {
        if (!CanReadHistory || Detail is null) return; var result = await service.SalesAsync(new(Detail.Id, SalesPage), ct);
        if (!Current(r)) return; Sales.Clear(); foreach (var s in result.Items) Sales.Add(s); SalesTotal = result.Total; HasLoadedSales = true; StatusMessage = result.Total == 0 ? "Este cliente no tiene ventas registradas." : "Ventas consultadas.";
    }
    [RelayCommand] private async Task LoadAccountsAsync() { AccountsPage = 1; await RunAsync(AccountsCoreAsync); }
    [RelayCommand] private async Task NextAccountsAsync() { if (IsBusy || AccountsPage * 30 >= AccountsTotal) return; AccountsPage++; await RunAsync(AccountsCoreAsync); }
    [RelayCommand] private async Task PreviousAccountsAsync() { if (IsBusy || AccountsPage <= 1) return; AccountsPage--; await RunAsync(AccountsCoreAsync); }
    private async Task AccountsCoreAsync(long r, CancellationToken ct)
    {
        if (!CanReadHistory || Detail is null) return; var id = Detail.Id; var b = await service.BalanceAsync(id, ct); var result = await service.AccountsAsync(new(id, AccountsPage), ct);
        if (!Current(r)) return; Balance = b; Accounts.Clear(); foreach (var a in result.Items) Accounts.Add(a); AccountsTotal = result.Total; HasLoadedAccounts = true; StatusMessage = result.Total == 0 ? "Este cliente no tiene cuentas por cobrar registradas." : "Estado de cuenta consultado.";
    }
    [RelayCommand] private async Task LoadPaymentsAsync() { PaymentsPage = 1; await RunAsync(PaymentsCoreAsync); }
    [RelayCommand] private async Task NextPaymentsAsync() { if (IsBusy || PaymentsPage * 30 >= PaymentsTotal) return; PaymentsPage++; await RunAsync(PaymentsCoreAsync); }
    [RelayCommand] private async Task PreviousPaymentsAsync() { if (IsBusy || PaymentsPage <= 1) return; PaymentsPage--; await RunAsync(PaymentsCoreAsync); }
    private async Task PaymentsCoreAsync(long r, CancellationToken ct)
    {
        if (!CanReadHistory || Detail is null) return; var result = await service.PaymentsAsync(new(Detail.Id, PaymentsPage), ct);
        if (!Current(r)) return; Payments.Clear(); foreach (var p in result.Items) Payments.Add(p); PaymentsTotal = result.Total; HasLoadedPayments = true; StatusMessage = result.Total == 0 ? "Este cliente no tiene abonos registrados." : "Historial de abonos consultado.";
    }
}
