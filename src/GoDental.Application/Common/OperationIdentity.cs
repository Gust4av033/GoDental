using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;

namespace GoDental.Application.Common;

public static class OperationIdentity
{
    public static OperationTicket Describe(object request)
    {
        var (id, type, normalized) = Normalize(request);
        if (id == Guid.Empty) throw new ArgumentException("Identificador vacío.");
        return new(id, type, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalized)))));
    }

    public static bool IsValid(OperationTicket t) => t.OperationId != Guid.Empty && Types.Contains(t.Type)
        && t.RequestHash is { Length: 64 } && t.RequestHash.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static readonly HashSet<string> Types = ["ONLINE_PROSPECTO","ONLINE_FUENTE","ONLINE_ESTADO","ONLINE_BLOQUEAR","ONLINE_EXCLUSION","ONLINE_CONVERTIR","REVISAR_FISCAL_INTERNO", "DESPACHAR_REPARTO", "RESOLVER_REPARTO", "RENDIR_REPARTO", "AVISO_COBRANZA", "CARPETA_EVIDENCIAS", "SOLICITAR_DEVOLUCION", "APLICAR_DEVOLUCION", "RECHAZAR_DEVOLUCION", "CREAR_IMPORTACION", "EVENTO_IMPORTACION", "GASTO_IMPORTACION", "ANULAR_GASTO_IMPORTACION", "CERRAR_COSTOS_IMPORTACION", "RECEPCION_IMPORTACION", "GUARDAR_OC", "ESTADO_OC", "PREPARAR_RECEPCION", "APLICAR_RECEPCION", "ANULAR_RECEPCION", "PARAMETROS_COMERCIALES", "CONFIRMAR_VENTA", "EMITIR_COTIZACION", "AUTORIZAR_DESCUENTO", "SALDO_INICIAL", "CODIGO_BARRAS", "GUARDAR_CAJA", "ABRIR_CAJA", "MOVIMIENTO_CAJA", "CERRAR_CAJA", "ABONO_CLIENTE", "CREAR_PEDIDO", "ESTADO_PEDIDO", "VINCULAR_PEDIDO_VENTA", "FISCAL_PENDIENTE"];

    private static (Guid, string, object) Normalize(object request) => request switch
    {
        SaveOnlineProspect or AddOnlineSource or ChangeOnlineState or BlockOnlineProspect or ExcludeOnlineContact or ConvertOnlineProspect => (OnlineValidator.Identity(request).Id,OnlineValidator.Identity(request).Type,OnlineValidator.Freeze(request)),
        ReviewFiscalInternal r => (r.OperationId,"REVISAR_FISCAL_INTERNO",Services.FiscalWorkspaceService.Freeze(r)),
        DispatchDelivery r => (r.OperationId,"DESPACHAR_REPARTO",DeliveryValidator.Freeze(r)),
        CompleteDelivery r => (r.OperationId,"RESOLVER_REPARTO",DeliveryValidator.Freeze(r)),
        SettleDelivery r => (r.OperationId,"RENDIR_REPARTO",DeliveryValidator.Freeze(r)),
        RegisterCollectionNotice r => (r.OperationId, "AVISO_COBRANZA", CollectionValidator.Freeze(r)),
        ConfigureReturnEvidence r => (r.OperationId,"CARPETA_EVIDENCIAS",r with{Root=r.Root.Trim(),ExpectedVersion=r.ExpectedVersion.ToArray(),Reason=r.Reason.Trim()}),
        RequestSaleReturn r => (r.OperationId,"SOLICITAR_DEVOLUCION",SaleReturnValidator.Freeze(r)),
        ResolveSaleReturn r => (r.OperationId,r.Apply?"APLICAR_DEVOLUCION":"RECHAZAR_DEVOLUCION",SaleReturnValidator.Freeze(r)),
        SaveImportation or ImportEvent or ImportExpense or VoidImportExpense or CloseImportCosts or PrepareImportReceipt => (ImportValidator.Id(request), ImportValidator.Type(request), ImportValidator.Freeze(request)),
        SavePurchaseOrder r => (r.OperationId, "GUARDAR_OC", PurchaseValidator.Freeze(r)),
        PurchaseTransition r => (r.OperationId, "ESTADO_OC", r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason = r.Reason.Trim() }),
        PreparePurchaseReceipt r => (r.OperationId, "PREPARAR_RECEPCION", PurchaseValidator.Freeze(r)),
        PurchaseReceiptAction r => (r.OperationId, r.Apply ? "APLICAR_RECEPCION" : "ANULAR_RECEPCION", r with { ExpectedVersion = r.ExpectedVersion.ToArray(), ExpectedOrderVersion = r.ExpectedOrderVersion.ToArray(), Reason = r.Reason.Trim() }),
        ChangeCommercialParameters r => (r.OperationId, "PARAMETROS_COMERCIALES", r with { JustificationVersion = r.JustificationVersion.ToArray(), AuthorizationVersion = r.AuthorizationVersion.ToArray(), Reason = r.Reason.Trim() }),
        ConfirmSale r => (r.OperationId, "CONFIRMAR_VENTA", r with { Cart = MvpValidator.Freeze(r.Cart), Payments = r.Payments.Select(p => p with { Reference = p.Reference?.Trim() }).ToArray(), Reason = r.Reason.Trim(), Order = r.Order is { } o ? o with { ExpectedVersion = o.ExpectedVersion.ToArray() } : null }),
        SaveQuotation r => (r.OperationId, "EMITIR_COTIZACION", r with { Cart = MvpValidator.Freeze(r.Cart), Reason = r.Reason.Trim() }),
        DiscountApproval r => (r.OperationId, "AUTORIZAR_DESCUENTO", r with { Cart = MvpValidator.Freeze(r.Cart), Reason = r.Reason.Trim() }),
        InitialInventory r => (r.OperationId, "SALDO_INICIAL", r with { Reason = r.Reason.Trim(), LotNumber = r.LotNumber?.Trim() }),
        BarcodeChange r => (r.OperationId, "CODIGO_BARRAS", r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Code = r.Code?.Trim(), Reason = r.Reason.Trim() }),
        CashBoxChange r => (r.OperationId, "GUARDAR_CAJA", r with { Code = r.Code?.Trim() ?? "", Name = r.Name.Trim(), Reason = r.Reason.Trim() }),
        OpenCashShift r => (r.OperationId, "ABRIR_CAJA", r with { Reason = r.Reason.Trim() }),
        CashMovement r => (r.OperationId, "MOVIMIENTO_CAJA", r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason = r.Reason.Trim() }),
        CloseCashShift r => (r.OperationId, "CERRAR_CAJA", r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason = r.Reason.Trim() }),
        AccountPayment r => (r.OperationId, "ABONO_CLIENTE", r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason = r.Reason.Trim(), Reference = r.Reference?.Trim() }),
        PlaceOrder r => (r.OperationId, "CREAR_PEDIDO", r with { Items = r.Items.Select(i => i with { }).OrderBy(i => i.ProductId).ToArray(), Reason = r.Reason.Trim() }),
        OrderTransition r => (r.OperationId, "ESTADO_PEDIDO", r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason = r.Reason.Trim() }),
        LinkOrderSale r => (r.OperationId, "VINCULAR_PEDIDO_VENTA", r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason = r.Reason.Trim() }),
        PendingFiscal r => (r.OperationId, "FISCAL_PENDIENTE", r with { Reason = r.Reason.Trim() }),
        _ => throw new ArgumentException("Tipo de operación desconocido.")
    };
}
