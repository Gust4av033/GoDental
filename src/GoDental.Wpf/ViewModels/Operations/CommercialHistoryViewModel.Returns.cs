using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Domain.Rules;
using GoDental.Wpf.Dialogs;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class CommercialHistoryViewModel
{
    private readonly SaleReturnService? returnService;
    private readonly CashService? returnCash;
    private readonly IMvpDialogService? returnDialogs;
    private readonly IReturnPhotoPicker? returnPhotos;
    [ObservableProperty]private SaleReturnsSnapshot? saleReturns;
    [ObservableProperty]private SaleReturnData? selectedReturn;
    [ObservableProperty]private SaleReturnSnapshot? returnDetail;
    [ObservableProperty]private SaleReturnLineData? selectedReturnLine;
    [ObservableProperty]private ReturnEvidence? selectedReturnPhoto;
    [ObservableProperty]private byte[]? returnPhoto;
    public bool CanConfigureReturnEvidence=>CanEdit&&returnService?.CanConfigure==true&&returnDialogs is not null;
    [RelayCommand]private async Task ConfigureReturnEvidenceAsync()
    {
        if(!CanConfigureReturnEvidence||returnService is null)return;
        ReturnEvidenceLocation? location=null;var revision=Session.Revision;
        await ReadAsync(async r=>location=await returnService.EvidenceLocationAsync());if(!Current(revision)||location is null)return;
        using var form=new MvpFormModel("Carpeta central de evidencias","Guardar carpeta",Operation,async f=>
        {
            var id=Guid.NewGuid();var request=new ConfigureReturnEvidence(id,f.Text("root"),location.RowVersion.ToArray(),f.Text("reason"));
            await Operation.StartAsync(request,id,r=>returnService.ConfigureAsync(r),_=>Task.CompletedTask);
        }){Description="Para las dos laptops configura una misma ruta compartida UNC en el servidor. Ambas cuentas de Windows requieren acceso. Respaldar esta carpeta junto con la BD: el .bak no incluye las fotos. Tras registrar fotos, el traslado de raíz requiere mantenimiento y copia verificada; no se mueve automáticamente."};
        form.Input("root","Carpeta absoluta (compartida para varias laptops)",location.Root).Input("reason","Motivo");await ReturnDialogAsync(form);
    }
    public bool HasReturnDetail=>ReturnDetail is not null;
    public string ReturnPageLabel=>$"{SaleReturns?.Total??0} solicitudes · Página {SaleReturns?.Page??1}";
    public bool CanRequestReturn=>CanEdit&&returnService?.CanRequest==true&&returnDialogs is not null&&SaleDetail is {} sale&&sale.Sale.State is "CONFIRMADA" or "DEVUELTA_PARCIAL"&&SaleReturns is {} source&&ReturnRules.WithinWindow(sale.Sale.Date,source.ServerNow,source.WindowHours)&&source.Lines.Any(l=>l.Remaining>0);
    public bool CanRequestCancellation=>CanRequestReturn&&returnService?.CanCancel==true&&SaleDetail?.Sale.State=="CONFIRMADA";
    public bool CanResolveReturn=>CanEdit&&returnService?.CanResolve==true&&returnDialogs is not null&&ReturnDetail?.Return.State=="SOLICITADA";
    public bool CanApplyReturn=>CanResolveReturn&&ReturnDetail?.UnavailableReason is null;
    partial void OnSaleReturnsChanged(SaleReturnsSnapshot? value){OnPropertyChanged(nameof(ReturnPageLabel));ReturnPermissionsChanged();}
    partial void OnReturnDetailChanged(SaleReturnSnapshot? value){ReturnPhoto=null;SelectedReturnLine=value?.Lines.FirstOrDefault();ReturnPermissionsChanged();}
    partial void OnSelectedReturnChanged(SaleReturnData? value){ReturnDetail=null;}
    partial void OnSelectedReturnLineChanged(SaleReturnLineData? value){ReturnPhoto=null;SelectedReturnPhoto=null;}
    partial void OnSelectedReturnPhotoChanged(ReturnEvidence? value)=>ReturnPhoto=null;
    private void ReturnPermissionsChanged(){OnPropertyChanged(nameof(HasReturnDetail));OnPropertyChanged(nameof(CanConfigureReturnEvidence));OnPropertyChanged(nameof(CanRequestReturn));OnPropertyChanged(nameof(CanRequestCancellation));OnPropertyChanged(nameof(CanResolveReturn));OnPropertyChanged(nameof(CanApplyReturn));}
    private void ResetReturns(){SaleReturns=null;SelectedReturn=null;ReturnDetail=null;SelectedReturnLine=null;SelectedReturnPhoto=null;ReturnPhoto=null;}
    private async Task LoadReturnsAsync(long revision,long saleId,int page=1,long? returnId=null)
    {
        if(returnService is null)return;
        var result=await returnService.ReadAsync(saleId,page);if(!Current(revision)||SaleDetail?.Sale.Id!=saleId)return;
        SaleReturns=result;SelectedReturn=returnId is {} id?result.Returns.FirstOrDefault(x=>x.Id==id):null;
        if(SelectedReturn is {} selected){var detail=await returnService.DetailAsync(saleId,selected.Id);if(Current(revision)&&SaleDetail?.Sale.Id==saleId&&SelectedReturn?.Id==selected.Id)ReturnDetail=detail;}
    }
    [RelayCommand]private Task ReloadReturnsAsync()=>ReadAsync(r=>SaleDetail is {} sale?LoadReturnsAsync(r,sale.Sale.Id):Task.CompletedTask);
    [RelayCommand]private Task NextReturnsAsync()=>ReadAsync(r=>SaleDetail is {} sale&&SaleReturns is {} s&&s.Page*30<s.Total?LoadReturnsAsync(r,sale.Sale.Id,s.Page+1):Task.CompletedTask);
    [RelayCommand]private Task PreviousReturnsAsync()=>ReadAsync(r=>SaleDetail is {} sale&&SaleReturns is {Page:>1} s?LoadReturnsAsync(r,sale.Sale.Id,s.Page-1):Task.CompletedTask);
    [RelayCommand]private Task ReadReturnAsync()=>ReadAsync(async r=>
    {
        if(returnService is null||SaleDetail is not {} sale||SelectedReturn is not {} selected)return;
        var result=await returnService.DetailAsync(sale.Sale.Id,selected.Id);if(!Current(r)||SaleDetail?.Sale.Id!=sale.Sale.Id||SelectedReturn?.Id!=selected.Id)return;
        ReturnDetail=result;StatusMessage=result?.UnavailableReason??"Solicitud actualizada. Revisa comprobante, condición, fotos y reintegro antes de autorizar.";
    });
    [RelayCommand]private Task RequestReturnAsync()=>RequestPosventaAsync(false);
    [RelayCommand]private Task RequestCancellationAsync()=>RequestPosventaAsync(true);
    private async Task RequestPosventaAsync(bool cancel)
    {
        if(!(cancel?CanRequestCancellation:CanRequestReturn)||SaleDetail is not {} sale||SaleReturns is not {} source||returnService is null)return;
        var draft=new ReturnDraftModel(source,cancel,returnService,returnPhotos);
        using var form=new MvpFormModel(cancel?"Solicitar anulación completa":"Solicitar devolución","Guardar solicitud",Operation,async f=>
        {
            var id=Guid.NewGuid();var request=new RequestSaleReturn(id,sale.Sale.Id,sale.Sale.RowVersion.ToArray(),cancel,f.Text("proof"),f.Text("reason"),draft.Snapshot());
            await Operation.StartAsync(request,id,r=>returnService.RequestAsync(r),receipt=>RefreshReturnAsync(sale.Sale.Id,receipt.Id));
        }){Body=draft,Description=$"{sale.Sale.Number} · Plazo: {source.WindowHours} horas desde la compra. Se conserva la venta original. {(cancel?"La anulación requiere recuperar toda la mercadería sellada y vendible, incluso si ya se entregó el pedido; reintegra también el cargo de entrega.":"Abierto/defectuoso requiere foto y motivo; no vuelve al stock vendible.")} Sólo el administrador aplica los efectos después de revisar."};
        form.Input("proof","Factura/comprobante o constancia verificable (hasta 250 caracteres)",sale.Sale.Number).Input("reason","Motivo");await ReturnDialogAsync(form);
    }
    [RelayCommand]private Task ApplyReturnAsync()=>ResolvePosventaAsync(true);
    [RelayCommand]private Task RejectReturnAsync()=>ResolvePosventaAsync(false);
    private async Task ResolvePosventaAsync(bool apply)
    {
        if(!(apply?CanApplyReturn:CanResolveReturn)||returnService is null||ReturnDetail is not {} source||SaleDetail is not {} sale)return;
        CashShiftData? shift=null;var revision=Session.Revision;
        if(apply&&source.CashRefund>0)
        {
            await ReadAsync(async r=>{if(returnCash is null||!Has("CAJA.VER","CAJA.MOVIMIENTO"))throw new ArgumentException("Necesitas permiso de caja para reintegrar efectivo.");shift=await returnCash.CurrentAsync(sale.Sale.CashBoxId);});
            if(!Current(revision)||shift is null){StatusMessage="Abre un turno de la caja de esta venta antes de reintegrar efectivo.";return;}
        }
        if(!Current(revision)||ReturnDetail!=source)return;
        using var form=new MvpFormModel(apply?"Autorizar y aplicar":"Rechazar solicitud",apply?"Aplicar devolución":"Rechazar",Operation,async f=>
        {
            var id=Guid.NewGuid();var request=new ResolveSaleReturn(id,sale.Sale.Id,source.Return.Id,source.Return.RowVersion.ToArray(),apply,source.QuoteToken,apply&&source.CashRefund>0?shift?.Id:null,f.Text("reason"));
            await Operation.StartAsync(request,id,r=>returnService.ResolveAsync(r,f.Text("admin"),f.ReadSecret?.Invoke()??""),_=>RefreshReturnAsync(sale.Sale.Id,source.Return.Id));
        }){SecretRequired=true,Description=apply?$"{source.Return.Number} · Total a compensar USD {source.CurrentAmount:0.00}. Reducción de deuda: USD {source.DebtReduction:0.00}. Efectivo a entregar: USD {source.CashRefund:0.00}. Revisa las fotos y el destino de cada producto. Se aplican todos los efectos juntos; no se puede deshacer desde esta pantalla.":$"{source.Return.Number}. Se conserva rechazada sin modificar inventario, caja ni deuda."};
        form.Input("admin","Usuario administrador").Input("reason","Motivo de resolución");await ReturnDialogAsync(form);
    }
    private async Task RefreshReturnAsync(long saleId,long returnId)
    {
        await ReadAsync(async r=>
        {
            var sale=await sales.ReadAsync(saleId);if(!Current(r)||SelectedSale?.Id!=saleId)return;
            ClearPreview();if(PrintSettings.DocumentKind==ReceiptKind.Delivery&&sale?.Sale.State is "ANULADA" or "DEVUELTA_PARCIAL" or "DEVUELTA_TOTAL")PrintSettings.Kind="Venta";
            if(sale is not null)
            {
                var index=Sales.ToList().FindIndex(x=>x.Id==saleId);if(index>=0)Sales[index]=sale.Sale;
                SelectedSale=sale.Sale;
            }
            SaleDetail=sale;if(sale is not null)await LoadReturnsAsync(r,saleId,returnId:returnId);
        });
    }
    private async Task ReturnDialogAsync(MvpFormModel form){if(returnDialogs is null)return;IsEditing=true;try{await returnDialogs.ShowAsync(form);}finally{IsEditing=false;}}
    [RelayCommand]private Task ShowReturnPhotoAsync()=>ReadAsync(async r=>
    {
        if(returnService is null||SaleDetail is not {} sale||ReturnDetail is not {} request||SelectedReturnLine is not {} line||SelectedReturnPhoto is not {} photo)return;
        var index=line.Evidence.IndexOf(photo);var bytes=await returnService.PhotoAsync(sale.Sale.Id,request.Return.Id,line.Id,index);
        if(Current(r)&&ReturnDetail==request&&SelectedReturnLine==line&&SelectedReturnPhoto==photo)ReturnPhoto=bytes;
    });
}
