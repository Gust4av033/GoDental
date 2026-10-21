using System.Collections.ObjectModel;
using System.Text.Json;
using GoDental.Application.Abstractions.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Navigation;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public interface IPdfFileService { Task<bool> SaveAsync(byte[] pdf, string suggestedName); Task<bool> SaveLabelsAsync(byte[] pdf, string suggestedName) => SaveAsync(pdf, suggestedName); }
public interface IPdfPrintService { Task<bool> PrintAsync(ReceiptDocument document, string name); }

public sealed partial class CommercialHistoryViewModel : MvpWorkspaceViewModel
{
    private readonly SalesService sales;
    private readonly SaleReceiptService receipts;
    private readonly IPdfFileService files;
    private readonly IPdfPrintService printer;
    private readonly PosViewModel pos;
    private readonly INavigationService navigation;
    private readonly IPrintPreferencesStore? printStore;
    private bool printLoaded;
    private string printWarning = "";
    private PrintLayout? previewLayout;
    private ReceiptKind previewKind;
    public ReceiptPrintSettingsModel PrintSettings { get; } = new();
    public bool PreviewMatchesSettings
    {
        get { try { return ReceiptPreview is not null && previewLayout == PrintSettings.Layout() && previewKind == PrintSettings.DocumentKind; } catch (ArgumentException) { return false; } }
    }
    public bool CanUsePreview => CanExport && PreviewMatchesSettings;
    public string PreviewCaption => ReceiptPreview is null ? "" : $"{ReceiptPreview.WidthMm:0.##} × {ReceiptPreview.HeightMm:0.##} mm · {SaleDetail?.Sale.Number}";
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
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsReceiptPreviewOpen)), NotifyPropertyChangedFor(nameof(PreviewPage)), NotifyPropertyChangedFor(nameof(PreviewPageLabel)), NotifyPropertyChangedFor(nameof(PreviewWidth)), NotifyPropertyChangedFor(nameof(PreviewHeight)), NotifyPropertyChangedFor(nameof(PreviewCaption)), NotifyPropertyChangedFor(nameof(CanUsePreview))]
    private ReceiptDocument? receiptPreview;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PreviewPage)), NotifyPropertyChangedFor(nameof(PreviewPageLabel))] private int previewPageNumber = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PreviewWidth)), NotifyPropertyChangedFor(nameof(PreviewHeight)), NotifyPropertyChangedFor(nameof(ZoomLabel))] private decimal previewZoom = .75m;
    public bool IsReceiptPreviewOpen => ReceiptPreview is not null;
    public byte[]? PreviewPage => ReceiptPreview?.Pages.ElementAtOrDefault(PreviewPageNumber - 1);
    public string PreviewPageLabel => $"Página {PreviewPageNumber} de {ReceiptPreview?.Pages.Count ?? 0}";
    public double PreviewWidth => (double)PreviewZoom * (double)(ReceiptPreview?.WidthMm ?? 215.9m) * 96 / 25.4;
    public double PreviewHeight => (double)PreviewZoom * (double)(ReceiptPreview?.HeightMm ?? 279.4m) * 96 / 25.4;
    public string ZoomLabel => $"{PreviewZoom:P0}";
    private void ClearPreview() { ReceiptPreview = null; PreviewPageNumber = 1; PreviewZoom = .75m; }
    [RelayCommand] private void CloseReceiptPreview() { if (!IsBusy) ClearPreview(); }
    [RelayCommand] private void NextPreviewPage() { if (PreviewPageNumber < ReceiptPreview?.Pages.Count) PreviewPageNumber++; }
    [RelayCommand] private void PreviousPreviewPage() { if (PreviewPageNumber > 1) PreviewPageNumber--; }
    [RelayCommand] private void ZoomIn() { if (PreviewZoom < 2) PreviewZoom += .25m; }
    [RelayCommand] private void ZoomOut() { if (PreviewZoom > .5m) PreviewZoom -= .25m; }
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
    protected override void PermissionsChanged() { OnPropertyChanged(nameof(CanExport)); OnPropertyChanged(nameof(CanUsePreview)); OnPropertyChanged(nameof(CanConvert)); OnPropertyChanged(nameof(CanOpenSelectedSale)); OnPropertyChanged(nameof(CanOpenSelectedQuotation)); }
    partial void OnSelectedSaleChanged(SaleData? value) { ClearPreview(); SaleDetail = null; PermissionsChanged(); }
    partial void OnSelectedQuotationChanged(QuotationData? value) { QuotationDetail = null; QuotationRecord = null; IsQuotationDetailOpen = false; PermissionsChanged(); }
    partial void OnQuotationRecordChanged(QuotationData? value) => PermissionsChanged();
    partial void OnSaleDetailChanged(SaleSnapshot? value) { IsSaleDetailOpen = value is not null; PermissionsChanged(); }
    public CommercialHistoryViewModel(SalesService sales, SaleReceiptService receipts, IPdfFileService files, IPdfPrintService printer, PosViewModel pos, INavigationService navigation, ISessionController session, MvpOperationService operations, ILogger<CommercialHistoryViewModel> logger, IPrintPreferencesStore? printStore = null) : base(session, operations, logger) { this.sales = sales; this.receipts = receipts; this.files = files; this.printer = printer; this.pos = pos; this.navigation = navigation; this.printStore = printStore;
        PrintSettings.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanUsePreview)); };
    }
    protected override void Reset() { ClearPreview(); IsSaleDetailOpen = IsQuotationDetailOpen = false; QuotationRecord = null; Sales.Clear(); Quotations.Clear(); SelectedSale = null; SelectedQuotation = null; SaleDetail = null; QuotationDetail = null; SalesPage = QuotationsPage = 1; SalesTotal = QuotationsTotal = 0; }
    [RelayCommand] private Task ReloadSalesAsync() { SalesPage = 1; return ReadAsync(SalesCoreAsync); }
    private async Task SalesCoreAsync(long r)
    {
        ClearPreview();
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
        var result = await sales.ReadAsync(id); if (!Current(r) || SelectedSale?.Id != id) return; ClearPreview();
        if (!printLoaded && printStore is not null)
        {
            printLoaded = true;
            try { var saved = await printStore.LoadAsync(); if (!Current(r)) return; PrintSettings.Load(saved); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { printWarning = " No se pudo cargar el formato local de impresión; puedes elegir las medidas aquí."; }
        }
        if (!Current(r) || SelectedSale?.Id != id) return;
        if (result?.Delivery is null && PrintSettings.DocumentKind == ReceiptKind.Delivery) PrintSettings.Kind = "Venta";
        SaleDetail = result; IsSaleDetailOpen = result is not null;
        StatusMessage = result is null ? "La venta no está disponible." : "Detalle actualizado. El PDF es un comprobante interno, sin autorización fiscal DTE.";
        StatusMessage += printWarning;
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
        if (ReceiptPreview is not null && !PreviewMatchesSettings) throw new ArgumentException("Actualiza la vista previa con el formato elegido antes de guardar.");
        if (ReceiptPreview is null) await GeneratePreviewAsync(r);
        if (ReceiptPreview is null || !PreviewMatchesSettings || !Current(r) || SaleDetail?.Sale.Id != id) return;
        var pdf = ReceiptPreview.Pdf; if (!Current(r) || SaleDetail?.Sale.Id != id) return;
        var saved = await files.SaveAsync(pdf, $"GoDental-comprobante-interno-{id}.pdf"); if (!Current(r)) return;
        StatusMessage = saved ? "Comprobante interno PDF guardado." : "No se guardó el archivo.";
    });
    [RelayCommand] private Task PreviewReceiptAsync() => ReadAsync(GeneratePreviewAsync);
    private async Task GeneratePreviewAsync(long r)
    {
        if (!Has("VENTAS.REIMPRIMIR") || SaleDetail?.Sale.State != "CONFIRMADA") return;
        var id = SaleDetail.Sale.Id;
        var layout = PrintSettings.Layout(); var kind = PrintSettings.DocumentKind;
        var document = await receipts.PreviewAsync(id, layout, kind); if (!Current(r) || SaleDetail?.Sale.Id != id) return;
        if (document.Pages.Count == 0) throw new ArgumentException("El comprobante no contiene páginas para mostrar.");
        if (PrintSettings.Layout() != layout || PrintSettings.DocumentKind != kind) return;
        previewLayout = layout; previewKind = kind;
        PreviewPageNumber = 1; PreviewZoom = .75m; ReceiptPreview = document;
        if (printStore is not null)
        {
            try { var previous = await printStore.LoadAsync(); await printStore.SaveAsync(previous with { Receipt = layout, Kind = kind }); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) { StatusMessage = "Vista previa lista; no se pudo conservar el formato en este equipo."; return; }
        }
        StatusMessage = "Vista previa del comprobante. Revisa los datos y luego guarda el PDF o selecciona una impresora. Imprime a tamaño real (100%).";
    }
    [RelayCommand] private Task PrintReceiptAsync() => ReadAsync(async r =>
    {
        if (!Has("VENTAS.REIMPRIMIR") || !PreviewMatchesSettings || ReceiptPreview is null || SaleDetail?.Sale.State != "CONFIRMADA") return;
        var printed = await printer.PrintAsync(ReceiptPreview, $"GoDental · {SaleDetail.Sale.Number}"); if (!Current(r)) return;
        StatusMessage = printed ? "Comprobante enviado a la cola de impresión." : "Se canceló la impresión.";
    });
    [RelayCommand] private async Task PreviewSaleAsync(long? saleId)
    {
        if (!Has("VENTAS.REIMPRIMIR") || saleId is null or <= 0) return;
        await OpenSaleAsync(new() { Id = saleId.Value });
        if (SaleDetail?.Sale.Id == saleId) await PreviewReceiptCommand.ExecuteAsync(null);
    }
}
