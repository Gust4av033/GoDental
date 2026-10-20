using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Navigation;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public interface IPdfFileService { Task<bool> SaveAsync(byte[] pdf, string suggestedName); }

public sealed partial class CommercialHistoryViewModel : MvpWorkspaceViewModel
{
    private readonly SalesService sales;
    private readonly SaleReceiptService receipts;
    private readonly IPdfFileService files;
    private readonly PosViewModel pos;
    private readonly INavigationService navigation;
    public ObservableCollection<SaleData> Sales { get; } = [];
    public ObservableCollection<QuotationData> Quotations { get; } = [];
    [ObservableProperty] private SaleData? selectedSale;
    [ObservableProperty] private QuotationData? selectedQuotation;
    [ObservableProperty] private SaleSnapshot? saleDetail;
    [ObservableProperty] private SaleQuote? quotationDetail;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SalesPageLabel))] private int salesPage = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SalesPageLabel))] private int salesTotal;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(QuotationsPageLabel))] private int quotationsPage = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(QuotationsPageLabel))] private int quotationsTotal;
    [ObservableProperty] private bool isSaleDetailOpen;
    [ObservableProperty] private bool isQuotationDetailOpen;
    [ObservableProperty] private QuotationData? quotationRecord;
    public string SalesPageLabel => $"{SalesTotal} ventas · Página {SalesPage}";
    public string QuotationsPageLabel => $"{QuotationsTotal} cotizaciones · Página {QuotationsPage}";
    public bool CanOpenSelectedSale => CanRead && IsNotBusy && SelectedSale is not null;
    public bool CanOpenSelectedQuotation => CanRead && IsNotBusy && SelectedQuotation is not null;
    [RelayCommand] private void ReturnToSales() => IsSaleDetailOpen = false;
    [RelayCommand] private void ReturnToQuotations() => IsQuotationDetailOpen = false;
    [RelayCommand] private async Task OpenSaleAsync(SaleData? sale)
    {
        if (!CanRead || IsBusy || sale is null) return;
        SelectedSale = sale;
        await ReadSaleCommand.ExecuteAsync(null);
        if (SaleDetail?.Sale.Id == sale.Id) navigation.Navigate(SetupPage.Sales);
    }
    [RelayCommand] private async Task OpenQuotationAsync(QuotationData? quotation)
    {
        if (!CanRead || IsBusy || quotation is null) return;
        SelectedQuotation = quotation;
        await ReadQuotationCommand.ExecuteAsync(null);
    }
    public override bool CanRead => sales.CanRead;
    public bool CanExport => CanEdit && Has("VENTAS.REIMPRIMIR") && SaleDetail?.Sale.State == "CONFIRMADA";
    public bool CanConvert => CanEdit && sales.CanSell && (QuotationRecord ?? SelectedQuotation)?.State == "EMITIDA" && pos.Operation.CanStart;
    protected override void PermissionsChanged() { OnPropertyChanged(nameof(CanExport)); OnPropertyChanged(nameof(CanConvert)); OnPropertyChanged(nameof(CanOpenSelectedSale)); OnPropertyChanged(nameof(CanOpenSelectedQuotation)); }
    partial void OnSelectedSaleChanged(SaleData? value) { SaleDetail = null; PermissionsChanged(); }
    partial void OnSelectedQuotationChanged(QuotationData? value) { QuotationDetail = null; QuotationRecord = null; IsQuotationDetailOpen = false; PermissionsChanged(); }
    partial void OnQuotationRecordChanged(QuotationData? value) => PermissionsChanged();
    partial void OnSaleDetailChanged(SaleSnapshot? value) { IsSaleDetailOpen = value is not null; PermissionsChanged(); }
    public CommercialHistoryViewModel(SalesService sales, SaleReceiptService receipts, IPdfFileService files, PosViewModel pos, INavigationService navigation, ISessionController session, MvpOperationService operations, ILogger<CommercialHistoryViewModel> logger) : base(session, operations, logger) { this.sales = sales; this.receipts = receipts; this.files = files; this.pos = pos; this.navigation = navigation; }
    protected override void Reset() { IsSaleDetailOpen = IsQuotationDetailOpen = false; QuotationRecord = null; Sales.Clear(); Quotations.Clear(); SelectedSale = null; SelectedQuotation = null; SaleDetail = null; QuotationDetail = null; SalesPage = QuotationsPage = 1; SalesTotal = QuotationsTotal = 0; }
    [RelayCommand] private Task ReloadSalesAsync() { SalesPage = 1; return ReadAsync(SalesCoreAsync); }
    private async Task SalesCoreAsync(long r)
    {
        var result = await sales.SearchAsync(new(SalesPage)); if (!Current(r)) return;
        Sales.Clear(); SelectedSale = null; foreach (var s in result.Items) Sales.Add(s); SalesTotal = result.Total;
        StatusMessage = SalesTotal == 0 ? "No hay ventas registradas." : $"{SalesTotal} ventas · Página {SalesPage}";
    }
    [RelayCommand] private Task ReloadQuotationsAsync() { QuotationsPage = 1; return ReadAsync(QuotationsCoreAsync); }
    private async Task QuotationsCoreAsync(long r)
    {
        var result = await sales.QuotationsAsync(new(QuotationsPage)); if (!Current(r)) return;
        Quotations.Clear(); SelectedQuotation = null; foreach (var q in result.Items) Quotations.Add(q); QuotationsTotal = result.Total;
        StatusMessage = QuotationsTotal == 0 ? "No hay cotizaciones emitidas." : $"{QuotationsTotal} cotizaciones · Página {QuotationsPage}";
    }
    [RelayCommand] private async Task NextSalesAsync() { if (IsBusy || SalesPage * 30 >= SalesTotal) return; SalesPage++; await ReadAsync(SalesCoreAsync); }
    [RelayCommand] private async Task PreviousSalesAsync() { if (IsBusy || SalesPage <= 1) return; SalesPage--; await ReadAsync(SalesCoreAsync); }
    [RelayCommand] private async Task NextQuotationsAsync() { if (IsBusy || QuotationsPage * 30 >= QuotationsTotal) return; QuotationsPage++; await ReadAsync(QuotationsCoreAsync); }
    [RelayCommand] private async Task PreviousQuotationsAsync() { if (IsBusy || QuotationsPage <= 1) return; QuotationsPage--; await ReadAsync(QuotationsCoreAsync); }
    [RelayCommand] private Task ReadSaleAsync() => ReadAsync(async r =>
    {
        if (SelectedSale is null) return; var id = SelectedSale.Id;
        var result = await sales.ReadAsync(id); if (!Current(r) || SelectedSale?.Id != id) return; SaleDetail = result; IsSaleDetailOpen = result is not null;
        StatusMessage = result is null ? "La venta no está disponible." : "Detalle actualizado. El PDF es un comprobante interno, sin autorización fiscal DTE.";
    });
    [RelayCommand] private Task ReadQuotationAsync() => ReadAsync(async r =>
    {
        if (SelectedQuotation is null) return; var id = SelectedQuotation.Id;
        var result = await sales.ReadQuotationAsync(id); if (!Current(r) || SelectedQuotation?.Id != id) return;
        QuotationRecord = result;
        QuotationDetail = result is null ? null : JsonSerializer.Deserialize<SaleQuote>(result.SnapshotJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        IsQuotationDetailOpen = QuotationDetail is not null;
        StatusMessage = QuotationDetail is null ? "La cotización no contiene un detalle disponible." : "Este es el cálculo emitido. Al llevarla al POS se recalculan precios y disponibilidad.";
    });
    [RelayCommand] private async Task ConvertAsync()
    {
        if (!CanConvert || SelectedQuotation is null) return;
        var id = SelectedQuotation.Id;
        await pos.ReloadCommand.ExecuteAsync(null);
        await pos.LoadQuotationAsync(id);
        if (pos.QuotationId == id) navigation.Navigate(SetupPage.Pos);
        else StatusMessage = pos.StatusMessage;
    }
    [RelayCommand] private Task ExportAsync() => ReadAsync(async r =>
    {
        if (!Has("VENTAS.REIMPRIMIR") || SaleDetail?.Sale.State != "CONFIRMADA") return; var id = SaleDetail.Sale.Id;
        var pdf = await receipts.ExportAsync(id); if (!Current(r)) return;
        var saved = await files.SaveAsync(pdf, $"GoDental-comprobante-interno-{id}.pdf"); if (!Current(r)) return;
        StatusMessage = saved ? "Comprobante interno PDF guardado." : "No se guardó el archivo.";
    });
}
