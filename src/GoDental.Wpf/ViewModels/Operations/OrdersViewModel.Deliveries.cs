using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class OrdersViewModel
{
    private readonly DeliveryService? deliveries;
    private readonly CashService? deliveryCash;
    public ObservableCollection<CashBoxData> DeliveryBoxes { get; }=[];
    [ObservableProperty] private DeliveryData? delivery;
    [ObservableProperty] private CashBoxData? deliveryBox;
    [ObservableProperty] private SaleQuote? deliveryQuote;
    [ObservableProperty] private string courier="";
    public string DeliverySummary=>Delivery is {} d?$"{d.State} · {d.Courier} · USD {d.Total:N2} · {d.Medium??"Sin cobro"} · Venta {d.SaleId?.ToString()??"sin emitir"}":"Sin reparto contra entrega. Cotiza y revisa antes de despachar.";
    public string DeliveryQuoteLabel=>DeliveryQuote is {} q?$"Importe a cobrar: USD {q.Total:N2} (incluye entrega {q.Cart.DeliveryCharge:N2}). Precio congelado al despachar.":"Precio de reparto sin consultar.";
    public bool CanPrepareDelivery=>CanEdit&&deliveries?.CanManage==true&&Detail?.Order is {State:"LISTO",SaleId:null}&&Delivery is null;
    public bool CanDispatchDelivery=>CanPrepareDelivery&&DeliveryQuote is not null&&DeliveryBox is not null;
    public bool CanResolveDelivery=>CanEdit&&deliveries?.CanManage==true&&Delivery?.State is "EN_RUTA" or "INCIDENCIA";
    public bool CanSettleDelivery=>CanEdit&&deliveries?.CanSettle==true&&Delivery?.State=="PENDIENTE_RENDICION";
    partial void OnDeliveryChanged(DeliveryData? value){OnPropertyChanged(nameof(DeliverySummary));DeliveryPermissions();}
    partial void OnDeliveryBoxChanged(CashBoxData? value)=>DeliveryQuote=null;
    partial void OnDeliveryQuoteChanged(SaleQuote? value){OnPropertyChanged(nameof(DeliveryQuoteLabel));DeliveryPermissions();}
    private void DeliveryPermissions(){foreach(var n in new[]{nameof(CanPrepareDelivery),nameof(CanDispatchDelivery),nameof(CanResolveDelivery),nameof(CanSettleDelivery),nameof(CanTransition),nameof(CanLink),nameof(CanPassToPos)})OnPropertyChanged(n);}
    private void ResetDelivery(){Delivery=null;DeliveryQuote=null;Courier="";}
    private async Task LoadDeliveryCoreAsync(long id,long revision)
    {
        if(deliveries is null)return;
        DeliveryQuote=null;
        var result=await deliveries.ReadAsync(id);if(!Current(revision)||Selected?.Id!=id)return;Delivery=result;
        if(deliveryCash?.CanRead==true&&DeliveryBoxes.Count==0)
        {var boxes=await deliveryCash.BoxesAsync();if(!Current(revision)||Selected?.Id!=id)return;foreach(var box in boxes.Where(x=>x.Active))DeliveryBoxes.Add(box);DeliveryBox=DeliveryBoxes.FirstOrDefault();}
        Courier=Detail?.Order.Responsible??"";
    }
    [RelayCommand] private Task QuoteDeliveryAsync()=>ReadAsync(async revision=>
    {
        if(deliveries is null||Detail?.Order is not {} order||DeliveryBox is not {} box||Delivery is not null)return;
        DeliveryQuote=null;var quote=await deliveries.QuoteAsync(order.Id,box.Id);
        if(Current(revision)&&Detail?.Order.Id==order.Id&&DeliveryBox?.Id==box.Id)DeliveryQuote=quote;
    });
    [RelayCommand] private async Task DispatchCodAsync()
    {
        if(!CanDispatchDelivery||deliveries is null||Detail is null||DeliveryBox is null||DeliveryQuote is null)return;
        var order=Detail.Order;var quote=DeliveryQuote;var box=DeliveryBox;
        await ShowAsync(new MvpFormModel("Despachar contra entrega","Despachar",Operation,async f=>
        {
            var id=Guid.NewGuid();var request=new DispatchDelivery(id,order.Id,order.RowVersion.ToArray(),box.Id,quote.Token,f.Text("courier"),f.Text("reason"));
            await Operation.StartAsync(request,id,q=>deliveries.DispatchAsync(q),_=>ReadAsync(r=>RefreshOrderAsync(order.Id,r)));
        }){Description=$"Se retiran las unidades de bodega a custodia. Cobro completo USD {quote.Total:N2}. No se registra venta ni deuda hasta confirmar entrega."}.Input("courier","Responsable del reparto",Courier).Input("reason","Motivo / constancia de salida"));
    }
    public sealed record DeliveryChoice(string Code,string Name);
    [RelayCommand] private async Task ResolveCodAsync()
    {
        if(!CanResolveDelivery||deliveries is null||Delivery is not {} d)return;
        DeliveryChoice[] outcomes=[new("ENTREGADO","Entregado y pago completo"),new("INCIDENCIA","Entrega fallida: sigue en custodia"),new("RETORNADO","Retorno completo sellado y recuperado")];
        DeliveryChoice[] media=[new("EFECTIVO","Efectivo en poder del repartidor"),new("TRANSFERENCIA","Transferencia con referencia")];
        await ShowAsync(new MvpFormModel("Resolver reparto","Registrar resultado",Operation,async f=>
        {
            var outcome=f.Choice<DeliveryChoice>("outcome").Code;var delivered=outcome=="ENTREGADO";
            var id=Guid.NewGuid();var request=new CompleteDelivery(id,d.OrderId,d.RowVersion.ToArray(),outcome,delivered?f.Choice<DeliveryChoice>("medium").Code:null,delivered?f.Text("reference"):null,delivered?f.Number("amount"):0,f.Choice<string>("recovered")=="Sí, todo recuperado sellado",f.Text("reason"));
            await Operation.StartAsync(request,id,q=>deliveries.CompleteAsync(q),_=>ReadAsync(r=>RefreshOrderAsync(d.OrderId,r)));
        }){Description=$"Importe congelado USD {d.Total:N2}. Entrega exige pago completo y queda pendiente de rendición. Para retorno confirma toda la mercadería sellada; faltantes/daño permanecen en incidencia."}
        .Select("outcome","Resultado",outcomes,outcomes[0]).Select("medium","Medio (sólo entrega)",media,media[0]).Input("amount","Monto completo (sólo entrega)",d.Total.ToString(CultureInfo.CurrentCulture)).Input("reference","Referencia de transferencia")
        .Select("recovered","Recuperación (sólo retorno)",new[]{"Sin confirmar","Sí, todo recuperado sellado"},"Sin confirmar","").Input("reason","Motivo / constancia de entrega o retorno"));
    }
    [RelayCommand] private Task SettleCodAsync()=>ReadAsync(async revision=>
    {
        if(deliveries?.CanSettle!=true||Delivery is not {State:"PENDIENTE_RENDICION"} d||deliveryCash is null)return;
        CashShiftData? shift=d.Medium=="EFECTIVO"?await deliveryCash.CurrentAsync(d.CashBoxId):null;
        if(!Current(revision)||Delivery?.Id!=d.Id)return;
        if(d.Medium=="EFECTIVO"&&shift is null){StatusMessage="Abre un turno de la caja de origen antes de recibir el efectivo.";return;}
        await ShowAsync(new MvpFormModel("Rendir cobro del reparto","Confirmar rendición",Operation,async f=>
        {
            var id=Guid.NewGuid();var request=new SettleDelivery(id,d.OrderId,d.RowVersion.ToArray(),shift?.Id,f.Number("amount"),f.Text("reference"),f.Text("reason"));
            await Operation.StartAsync(request,id,q=>deliveries.SettleAsync(q),async _=>{await RefreshOrderAsync(d.OrderId,Session.Revision);});
        }){Description=d.Medium=="EFECTIVO"?$"Recibe físicamente USD {d.Total:N2} en turno {shift!.Id}. Sólo ahora aumenta el efectivo disponible.":$"Verifica la transferencia por USD {d.Total:N2} y confirma su referencia exacta. No agrega efectivo a caja."}
        .Input("amount","Monto recibido / verificado",d.Total.ToString(CultureInfo.CurrentCulture)).Input("reference","Referencia verificada",d.Reference??"").Input("reason","Constancia de recepción / verificación"));
    });
}
