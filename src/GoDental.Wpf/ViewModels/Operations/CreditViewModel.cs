using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class CreditViewModel : MvpWorkspaceViewModel
{
    private readonly CashService cash;
    private readonly SalesService sales;
    private readonly CustomerService customers;
    private readonly IMvpDialogService dialogs;
    public ObservableCollection<CreditAccountData> Items { get; } = [];
    public ObservableCollection<CustomerPaymentData> Payments { get; } = [];
    [ObservableProperty] private CreditAccountData? selected;
    [ObservableProperty] private bool overdueOnly;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int page = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int total;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HistoryPageLabel))] private int historyPage = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HistoryPageLabel))] private int historyTotal;
    [ObservableProperty] private CustomerBalanceData? balance;
    private long? historyCustomer;
    [ObservableProperty] private bool isDetailOpen;
    [ObservableProperty] private bool hasLoadedHistory;
    public string PageLabel => $"{Total} cuentas · Página {Page}";
    public string HistoryPageLabel => $"{HistoryTotal} abonos del cliente · Página {HistoryPage}";
    [RelayCommand] private void ReturnToList() { if (!IsBusy) IsDetailOpen = false; }
    [RelayCommand] private async Task OpenAccountAsync(CreditAccountData? account)
    {
        if (!CanRead || IsBusy || account is null) return;
        Selected = account; IsDetailOpen = true;
        await HistoryCommand.ExecuteAsync(null);
    }
    [RelayCommand] private Task OpenSelectedAsync() => OpenAccountAsync(Selected);
    [RelayCommand] private Task RefreshAccountAsync() => ReadAsync(async revision =>
    {
        await LoadCoreAsync(revision);
        if (!Current(revision)) return;
        historyCustomer = Selected?.CustomerId; HistoryPage = 1;
        await HistoryCoreAsync(revision);
    });
    public override bool CanRead => Has("CLIENTES.VER", "VENTAS.VER");
    public bool CanPay => CanEdit && Has("CAJA.VER", "CAJA.MOVIMIENTO") && Selected?.Balance > 0;
    partial void OnSelectedChanged(CreditAccountData? value) { Payments.Clear(); Balance = null; historyCustomer = null; HasLoadedHistory = false; if (value is null) IsDetailOpen = false; PermissionsChanged(); }
    protected override void PermissionsChanged() => OnPropertyChanged(nameof(CanPay));
    public CreditViewModel(CashService cash, SalesService sales, CustomerService customers, IMvpDialogService dialogs, ISessionController session, MvpOperationService operations, ILogger<CreditViewModel> logger) : base(session, operations, logger) { this.cash = cash; this.sales = sales; this.customers = customers; this.dialogs = dialogs; }
    protected override void Reset() { IsDetailOpen = false; HasLoadedHistory = false; Items.Clear(); Payments.Clear(); Selected = null; Page = HistoryPage = 1; Total = HistoryTotal = 0; Balance = null; }
    [RelayCommand] private Task ReloadAsync() { Page = 1; return ReadAsync(LoadCoreAsync); }
    private async Task LoadCoreAsync(long r)
    {
        var result = await cash.AccountsAsync(OverdueOnly, new(Page)); if (!Current(r)) return;
        var wasDetailOpen = IsDetailOpen; var id = Selected?.Id; Items.Clear(); foreach (var a in result.Items) Items.Add(a); Selected = Items.FirstOrDefault(a => a.Id == id); Total = result.Total; IsDetailOpen = wasDetailOpen && Selected is not null;
        StatusMessage = Total == 0 ? "No hay cuentas para este filtro." : $"{Total} cuentas · Página {Page}. Selecciona una cuenta para registrar un abono.";
    }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await ReadAsync(LoadCoreAsync); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await ReadAsync(LoadCoreAsync); }
    [RelayCommand] private Task HistoryAsync() { HistoryPage = 1; historyCustomer = Selected?.CustomerId; return ReadAsync(HistoryCoreAsync); }
    private async Task HistoryCoreAsync(long r)
    {
        if (historyCustomer is not { } id) return;
        var result = await customers.PaymentsAsync(new(id, HistoryPage)); var balance = await customers.BalanceAsync(id); if (!Current(r) || Selected?.CustomerId != id) return;
        Payments.Clear(); foreach (var p in result.Items) Payments.Add(p); HistoryTotal = result.Total; Balance = balance; HasLoadedHistory = true;
        StatusMessage = result.Total == 0 ? "Este cliente no tiene abonos registrados." : $"{HistoryTotal} abonos del cliente · Página {HistoryPage}";
    }
    [RelayCommand] private async Task NextHistoryAsync() { if (IsBusy || HistoryPage * 30 >= HistoryTotal) return; HistoryPage++; await ReadAsync(HistoryCoreAsync); }
    [RelayCommand] private async Task PreviousHistoryAsync() { if (IsBusy || HistoryPage <= 1) return; HistoryPage--; await ReadAsync(HistoryCoreAsync); }
    [RelayCommand] private async Task PayAsync()
    {
        if (!CanPay || Selected is null) return; var account = Selected;
        await ReadAsync(async r =>
        {
            var methods = (await sales.PaymentMethodsAsync()).Where(m => m.Code != "CREDITO").ToArray();
            var boxes = await cash.BoxesAsync(); if (!Current(r)) return;
            using var form = new MvpFormModel($"Abonar · {account.Customer}", "Registrar abono", Operation, async f =>
            {
                var method = f.Choice<PaymentMethodData>("method");
                CashShiftData? shift = null; var revision = Session.Revision;
                if (method.AffectsCash) shift = await cash.CurrentAsync(f.Choice<CashBoxData>("box").Id);
                if (!Current(revision)) return;
                var id = Guid.NewGuid(); var request = new AccountPayment(id, account.Id, account.RowVersion.ToArray(), method.Id, f.Number("amount"), f.Text("reference"), shift?.Id, f.Text("reason"));
                await Operation.StartAsync(request, id, q => cash.PayAsync(q), async _ =>
                {
                    // The opening read has already finished by the time a recovery command runs.
                    var current = Session.Revision; await LoadCoreAsync(current); historyCustomer = account.CustomerId; HistoryPage = 1; await HistoryCoreAsync(current);
                    if (shift is not null) await cash.ReadAsync(shift.Id);
                });
            }) { Description = $"Venta {account.Sale} · Saldo leído {account.Balance:N2}. La cuenta se vuelve a validar al registrar." };
            form.Select("method", "Forma de pago · Tarjeta registra un cobro externo ya aprobado", methods, methods.FirstOrDefault()).Input("amount", "Importe del abono", account.Balance.ToString("0.00")).Input("reference", "Referencia del comprobante (sin datos de tarjeta)").Select("box", "Caja para efectivo", boxes.Where(b => b.Active), boxes.FirstOrDefault(b => b.Active)).Input("reason", "Motivo");
            IsEditing = true; try { await dialogs.ShowAsync(form); } finally { IsEditing = false; }
        });
    }
}
