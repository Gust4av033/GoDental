using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class ReportsViewModel:MvpWorkspaceViewModel
{
    private readonly ReportService reports;
    private readonly IPdfFileService files;
    private readonly IPdfPrintService printer;
    public sealed record ReportChoice(OperationalReport Code,string Name);
    public sealed record GroupChoice(ReportGrouping Code,string Name);
    public ObservableCollection<ReportChoice> Kinds { get; }=[];
    public IReadOnlyList<GroupChoice> Groups { get; }=[new(ReportGrouping.Document,"Documento"),new(ReportGrouping.Day,"Día"),new(ReportGrouping.Product,"Producto"),new(ReportGrouping.Category,"Categoría actual"),new(ReportGrouping.Customer,"Cliente")];
    public ObservableCollection<ReportRow> Rows { get; }=[];
    [ObservableProperty] private ReportChoice? kind;
    [ObservableProperty] private GroupChoice? grouping;
    [ObservableProperty] private DateTime? from=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;
    [ObservableProperty] private DateTime? to=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;
    [ObservableProperty] private OperationalReportData? snapshot;
    [ObservableProperty] private int page=1;
    [ObservableProperty] private string summary="Consulta un reporte para ver los totales completos.";
    private IReadOnlyList<ReportRow> allRows=[];
    public bool IsSales=>Kind?.Code==OperationalReport.Sales;
    public bool IsInventory=>Kind?.Code==OperationalReport.Inventory;
    public bool IsExpiry=>Kind?.Code==OperationalReport.Expiry;
    public bool IsPurchases=>Kind?.Code==OperationalReport.Purchases;
    public string PageLabel=>$"{allRows.Count} filas · Página {Page}";
    public override bool CanRead=>reports.CanRead;
    public bool CanPreview=>CanRead&&reports.CanPrint&&!IsBusy&&Kind is not null;
    public ReportsViewModel(ReportService reports,IPdfFileService files,IPdfPrintService printer,ISessionController session,MvpOperationService operations,ILogger<ReportsViewModel> logger):base(session,operations,logger)
    {
        this.reports=reports;this.files=files;this.printer=printer;Reset();
        PrintSettings.PropertyChanged+=(_,_)=>OnPropertyChanged(nameof(CanUsePreview));
    }
    protected override void Reset()
    {
        Kinds.Clear();foreach(var choice in new[]{new ReportChoice(OperationalReport.Sales,"Ventas y margen"),new(OperationalReport.Inventory,"Inventario actual"),new(OperationalReport.Expiry,"Lotes y vencimientos"),new(OperationalReport.Purchases,"Compras recibidas")})if(reports.CanReadKind(choice.Code))Kinds.Add(choice);
        Kind=Kinds.FirstOrDefault();Grouping=Groups[0];Invalidate();
    }
    protected override void PermissionsChanged(){OnPropertyChanged(nameof(CanPreview));OnPropertyChanged(nameof(CanUsePreview));}
    partial void OnKindChanged(ReportChoice? value){Invalidate();foreach(var n in new[]{nameof(IsSales),nameof(IsInventory),nameof(IsExpiry),nameof(IsPurchases)})OnPropertyChanged(n);}
    partial void OnGroupingChanged(GroupChoice? value)=>Invalidate();
    partial void OnFromChanged(DateTime? value)=>Invalidate();
    partial void OnToChanged(DateTime? value)=>Invalidate();
    private void Invalidate(){Snapshot=null;Rows.Clear();allRows=[];Page=1;Summary="Consulta el reporte con los filtros actuales.";ClearPreview();OnPropertyChanged(nameof(PageLabel));}
    private ReportFilter Filter()=>new(Kind?.Code??throw new ArgumentException("Selecciona un reporte."),From??throw new ArgumentException("Selecciona fecha inicial."),To??throw new ArgumentException("Selecciona fecha final."),Grouping?.Code??ReportGrouping.Document);
    [RelayCommand] private Task ReloadAsync()=>ReadAsync(async revision=>
    {
        var filter=Filter();Invalidate();var result=await reports.ReadAsync(filter);
        if(!Current(revision)||Filter()!=filter)return;Snapshot=result;allRows=ReportService.Group(result);FillPage();
        string M(decimal? v)=>v?.ToString("C2",CultureInfo.GetCultureInfo("en-US"))??"Restringido";
        Summary=filter.Kind switch
        {
            OperationalReport.Sales=>$"Total {M(result.Total)} · Impuesto {M(result.Tax)} · Entrega {M(result.Delivery)} · Mercadería {M(result.Net)} · Costo {M(result.Cost)} · Margen bruto {M(result.Margin)}",
            OperationalReport.Purchases=>$"Valor recibido puesto en bodega {M(result.Total)} · {result.Rows.Count} líneas aplicadas",
            _=>$"Fotografía actual · Valor bodega + custodia {M(result.Value)} · {result.Rows.Count} filas"
        };
        StatusMessage=result.Rows.Count==0?"No hay datos para esta consulta.":$"Consulta {result.AsOf.ToOffset(TimeSpan.FromHours(-6)):dd/MM/yyyy HH:mm}. Totales completos. Margen bruto no equivale a utilidad neta.";
    });
    private void FillPage(){Rows.Clear();foreach(var row in allRows.Skip((Page-1)*30).Take(30))Rows.Add(row);OnPropertyChanged(nameof(PageLabel));}
    [RelayCommand] private void Next(){if(!IsBusy&&Page*30<allRows.Count){Page++;FillPage();}}
    [RelayCommand] private void Previous(){if(!IsBusy&&Page>1){Page--;FillPage();}}
    public ReceiptPrintSettingsModel PrintSettings { get; }=new();
    private PrintLayout? previewLayout;private ReportFilter? previewFilter;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(IsReceiptPreviewOpen)),NotifyPropertyChangedFor(nameof(PreviewPage)),NotifyPropertyChangedFor(nameof(PreviewWidth)),NotifyPropertyChangedFor(nameof(PreviewHeight)),NotifyPropertyChangedFor(nameof(PreviewPageLabel)),NotifyPropertyChangedFor(nameof(CanUsePreview))]private ReceiptDocument? receiptPreview;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(PreviewPage)),NotifyPropertyChangedFor(nameof(PreviewPageLabel))]private int previewPageNumber=1;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(PreviewWidth)),NotifyPropertyChangedFor(nameof(PreviewHeight)),NotifyPropertyChangedFor(nameof(ZoomLabel))]private decimal previewZoom=.75m;
    public bool IsReceiptPreviewOpen=>ReceiptPreview is not null;
    public string PreviewCaption=>"Reporte interno · USD · tamaño real 100% · no es DTE";
    public string PreviewPageLabel=>$"Página {PreviewPageNumber} de {ReceiptPreview?.Pages.Count??0}";
    public string ZoomLabel=>$"{PreviewZoom:P0}";
    public byte[]? PreviewPage=>ReceiptPreview?.Pages.ElementAtOrDefault(PreviewPageNumber-1);
    public double PreviewWidth=>(double)(ReceiptPreview?.WidthMm??215.9m)*(double)PreviewZoom*96/25.4;
    public double PreviewHeight=>(double)(ReceiptPreview?.HeightMm??279.4m)*(double)PreviewZoom*96/25.4;
    public bool CanUsePreview=>!IsBusy&&PreviewValid();
    private bool PreviewValid(){try{return reports.CanPrint&&reports.CanReadKind(Filter().Kind)&&ReceiptPreview is not null&&previewFilter==Filter()&&previewLayout==PrintSettings.Layout();}catch(ArgumentException){return false;}}
    private void ClearPreview(){ReceiptPreview=null;previewFilter=null;previewLayout=null;PreviewPageNumber=1;PreviewZoom=.75m;}
    [RelayCommand]private Task PreviewReceiptAsync()=>ReadAsync(async revision=>
    {
        var filter=Filter();var layout=PrintSettings.Layout();ClearPreview();var doc=await reports.PreviewAsync(filter,layout);
        if(!Current(revision)||Filter()!=filter||PrintSettings.Layout()!=layout)return;previewLayout=layout;previewFilter=filter;ReceiptPreview=doc;
        StatusMessage="Revisa el reporte completo antes de guardar o imprimir.";
    });
    [RelayCommand]private Task ExportAsync()=>ReadAsync(async revision=>{if(!PreviewValid()||ReceiptPreview is null)return;var ok=await files.SaveAsync(ReceiptPreview.Pdf,"GoDental-reporte.pdf");if(Current(revision))StatusMessage=ok?"PDF guardado.":"Guardado cancelado.";});
    [RelayCommand]private Task PrintReceiptAsync()=>ReadAsync(async revision=>{if(!PreviewValid()||ReceiptPreview is null)return;var ok=await printer.PrintAsync(ReceiptPreview,"GoDental-reporte");if(Current(revision))StatusMessage=ok?"Reporte enviado a impresión.":"Impresión cancelada.";});
    [RelayCommand]private void CloseReceiptPreview(){if(!IsBusy)ClearPreview();}
    [RelayCommand]private void ZoomIn(){if(PreviewZoom<2)PreviewZoom+=.25m;}
    [RelayCommand]private void ZoomOut(){if(PreviewZoom>.5m)PreviewZoom-=.25m;}
    [RelayCommand]private void NextPreviewPage(){if(PreviewPageNumber<ReceiptPreview?.Pages.Count)PreviewPageNumber++;}
    [RelayCommand]private void PreviousPreviewPage(){if(PreviewPageNumber>1)PreviewPageNumber--;}
}
