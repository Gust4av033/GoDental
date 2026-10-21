using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class BarcodesViewModel : MvpWorkspaceViewModel
{
    private readonly InventorySetupService service;
    private readonly IMvpDialogService dialogs;
    private readonly BarcodeLabelService? labelService;
    private readonly IPdfFileService? files;
    private readonly IPdfPrintService? printer;
    private readonly IPrintPreferencesStore? printStore;
    private bool printLoaded;
    private string printWarning = "";
    private ProductData? product;
    private LabelPrintOptions? previewOptions;
    private long previewCodeId;
    public LabelPrintSettingsModel PrintSettings { get; } = new();
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsReceiptPreviewOpen)), NotifyPropertyChangedFor(nameof(PreviewPage)), NotifyPropertyChangedFor(nameof(PreviewPageLabel)), NotifyPropertyChangedFor(nameof(PreviewWidth)), NotifyPropertyChangedFor(nameof(PreviewHeight)), NotifyPropertyChangedFor(nameof(PreviewCaption)), NotifyPropertyChangedFor(nameof(CanUsePreview))]
    private ReceiptDocument? receiptPreview;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PreviewPage)), NotifyPropertyChangedFor(nameof(PreviewPageLabel))] private int previewPageNumber = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PreviewWidth)), NotifyPropertyChangedFor(nameof(PreviewHeight)), NotifyPropertyChangedFor(nameof(ZoomLabel))] private decimal previewZoom = 1;
    public bool IsReceiptPreviewOpen => ReceiptPreview is not null;
    public byte[]? PreviewPage => ReceiptPreview?.Pages.ElementAtOrDefault(PreviewPageNumber - 1);
    public string PreviewPageLabel => $"Página {PreviewPageNumber} de {ReceiptPreview?.Pages.Count ?? 0}";
    public string PreviewCaption => ReceiptPreview is null ? "" : $"{ReceiptPreview.WidthMm:0.##} × {ReceiptPreview.HeightMm:0.##} mm · {productLabel}";
    public double PreviewWidth => (double)PreviewZoom * (double)(ReceiptPreview?.WidthMm ?? 50) * 96 / 25.4;
    public double PreviewHeight => (double)PreviewZoom * (double)(ReceiptPreview?.HeightMm ?? 30) * 96 / 25.4;
    public string ZoomLabel => $"{PreviewZoom:P0}";
    public bool CanPreview => CanRead && IsNotBusy && Selected is { Active: true } && labelService is not null;
    public bool CanUsePreview
    {
        get { try { return CanRead && IsNotBusy && ReceiptPreview is not null && Selected is { Active: true } && Selected.Id == previewCodeId && previewOptions == PrintSettings.Options(); } catch (ArgumentException) { return false; } }
    }
    [RelayCommand] private void CloseReceiptPreview() { if (!IsBusy) ReceiptPreview = null; }
    [RelayCommand] private void NextPreviewPage() { if (PreviewPageNumber < ReceiptPreview?.Pages.Count) PreviewPageNumber++; }
    [RelayCommand] private void PreviousPreviewPage() { if (PreviewPageNumber > 1) PreviewPageNumber--; }
    [RelayCommand] private void ZoomIn() { if (PreviewZoom < 3) PreviewZoom += .25m; }
    [RelayCommand] private void ZoomOut() { if (PreviewZoom > .5m) PreviewZoom -= .25m; }
    partial void OnSelectedChanged(BarcodeData? value) { ReceiptPreview = null; PermissionsChanged(); }
    private int productId;
    private string productLabel = "";
    private byte[]? version;
    public ObservableCollection<BarcodeData> Items { get; } = [];
    public IReadOnlyList<string> Types { get; } = ["CODE128", "EAN13", "UPC", "OTRO"];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanToggle))] private BarcodeData? selected;
    [ObservableProperty] private bool isEmpty = true;
    public override bool CanRead => service.CanReadBarcodes;
    public bool CanManage => CanEdit && service.CanManageBarcodes && version is not null;
    public bool CanToggle => CanManage && Selected is not null;
    protected override void PermissionsChanged() { OnPropertyChanged(nameof(CanManage)); OnPropertyChanged(nameof(CanToggle)); OnPropertyChanged(nameof(CanPreview)); OnPropertyChanged(nameof(CanUsePreview)); }
    protected override void Reset() { ReceiptPreview = null; product = null; productId = 0; productLabel = ""; version = null; Items.Clear(); Selected = null; IsEmpty = true; }
    public BarcodesViewModel(InventorySetupService service, IMvpDialogService dialogs, ISessionController session,
        MvpOperationService operations, ILogger<BarcodesViewModel> logger, BarcodeLabelService? labelService = null, IPdfFileService? files = null, IPdfPrintService? printer = null, IPrintPreferencesStore? printStore = null) : base(session, operations, logger)
    { this.service = service; this.dialogs = dialogs; this.labelService = labelService; this.files = files; this.printer = printer; this.printStore = printStore;
        PrintSettings.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanUsePreview));
    }
    public async Task OpenAsync(ProductData product)
    {
        if (IsBusy || IsEditing || !Operation.CanStart) return;
        this.product = product; ReceiptPreview = null; productId = product.Id; productLabel = $"{product.Name} · {product.Sku}";
        await ReloadAsync();
    }
    [RelayCommand] private Task ReloadAsync() => ReadAsync(async revision =>
    {
        if (productId <= 0) return;
        if (!printLoaded && printStore is not null)
        {
            printLoaded = true;
            try { var saved = await printStore.LoadAsync(); if (!Current(revision)) return; PrintSettings.Load(saved.Labels); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            { printWarning = " No se pudo cargar el formato local de impresión; puedes elegir las medidas aquí."; }
        }
        if (!Current(revision)) return;
        version = null; PermissionsChanged(); Items.Clear(); Selected = null; IsEmpty = true;
        var result = await service.BarcodesAsync(productId);
        if (!Current(revision)) return;
        version = result.ProductVersion.ToArray();
        foreach (var item in result.Items) Items.Add(item);
        IsEmpty = Items.Count == 0; PermissionsChanged();
        StatusMessage = IsEmpty ? "Este producto no tiene códigos. Puedes registrar uno externo o generar un código interno." : "Los códigos activos permiten buscar este producto en el POS. Desactivar conserva el historial.";
        StatusMessage += printWarning;
    });
    [RelayCommand] private Task PreviewReceiptAsync() => ReadAsync(async revision =>
    {
        if (labelService is null || product is null || Selected is not { Active: true } selected) return;
        ReceiptPreview = null;
        var options = PrintSettings.Options(); var target = product.Id; var codeId = selected.Id;
        var document = await labelService.PreviewAsync(product, codeId, options);
        if (!Current(revision) || product?.Id != target || Selected?.Id != codeId || options != PrintSettings.Options()) return;
        previewCodeId = codeId; previewOptions = options; PreviewPageNumber = 1; PreviewZoom = 1; ReceiptPreview = document;
        StatusMessage = "Etiqueta generada. Las barras corresponden al código activo seleccionado. Imprime a tamaño real (100%).";
        if (printStore is not null)
        {
            try { var previous = await printStore.LoadAsync(); await printStore.SaveAsync(previous with { Labels = options with { Copies = 1 } }); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) { StatusMessage += " No se pudo conservar el formato en este equipo."; }
        }
    });
    [RelayCommand] private Task ExportAsync() => ReadAsync(async revision =>
    {
        if (!Has("INVENTARIO.VER") || files is null || ReceiptPreview is null || Selected?.Id != previewCodeId || PrintSettings.Options() != previewOptions) return;
        var saved = await files.SaveLabelsAsync(ReceiptPreview.Pdf, $"GoDental-etiquetas-{productId}.pdf");
        if (Current(revision)) StatusMessage = saved ? "Etiquetas PDF guardadas." : "No se guardó el archivo.";
    });
    [RelayCommand] private Task PrintReceiptAsync() => ReadAsync(async revision =>
    {
        if (!Has("INVENTARIO.VER") || printer is null || ReceiptPreview is null || Selected?.Id != previewCodeId || PrintSettings.Options() != previewOptions) return;
        var printed = await printer.PrintAsync(ReceiptPreview, "GoDental · Etiquetas");
        if (Current(revision)) StatusMessage = printed ? "Etiquetas enviadas a impresión." : "Se canceló la impresión.";
    });
    [RelayCommand] private Task AddAsync() => EditAsync(null);
    [RelayCommand] private Task ToggleAsync() => Selected is { } item ? EditAsync(item) : Task.CompletedTask;
    private async Task EditAsync(BarcodeData? existing)
    {
        if (!CanManage || version is null) return;
        var expected = version.ToArray(); var target = productId;
        using var form = new MvpFormModel(existing is null ? "Agregar código de barras" : existing.Active ? "Desactivar código" : "Activar código", "Guardar código", Operation, async f =>
        {
            var id = Guid.NewGuid();
            var request = new BarcodeChange(id, target, expected, existing?.Code ?? f.Text("code"), existing?.Type ?? f.Choice<string>("type"), existing is null || !existing.Active, f.Text("reason"));
            await Operation.StartAsync(request, id, r => service.SaveBarcodeAsync(r), _ => ReloadAsync());
        }) { Description = existing is null ? $"{productLabel}. Deja el código vacío y elige CODE128 para generar el interno. Para EAN13/UPC incluye el dígito de control. No se imprime una etiqueta en este paso."
            : $"{productLabel} · {existing.Code} · {existing.Type}. {(existing.Active ? "Dejará de encontrarse por este código en el POS." : "Volverá a encontrarse por este código en el POS.")} No se elimina ni se reasigna a otro producto." };
        if (existing is null) form.Input("code", "Código externo (vacío para generar interno)").Select("type", "Tipo", Types, "CODE128", display: "");
        form.Input("reason", "Motivo");
        IsEditing = true;
        try { await dialogs.ShowAsync(form); }
        finally { IsEditing = false; }
    }
}
