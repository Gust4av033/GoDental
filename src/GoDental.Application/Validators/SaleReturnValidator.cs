using GoDental.Application.DTOs;
namespace GoDental.Application.Validators;
public static class SaleReturnValidator
{
    public static string? Validate(object request) => request switch
    {
        ConfigureReturnEvidence r => r.OperationId==Guid.Empty||r.ExpectedVersion is not {Length:8}||!Text(r.Root,500)||!Text(r.Reason,500)?"Indica una carpeta central absoluta y motivo; actualiza su configuración.":null,
        RequestSaleReturn r => r.OperationId == Guid.Empty || r.SaleId <= 0 || r.ExpectedSaleVersion is not {Length:8} ? "Actualiza la venta antes de solicitar."
            : !Text(r.Proof,250) || !Text(r.Reason,1000) ? "Indica comprobante verificable y motivo de devolución."
            : r.Items is not {Count:>0 and <=100} || r.Items.Select(x=>x.SaleLineId).Distinct().Count()!=r.Items.Count ? "Selecciona entre 1 y 100 líneas sin repetir."
            : r.Items.Any(x=>x.SaleLineId<=0 || x.Quantity<=0 || x.Quantity>999999999 || decimal.Round(x.Quantity,4)!=x.Quantity
                || x.Condition is not ("SELLADO" or "ABIERTO_DEFECTUOSO" or "DAÑADO" or "OTRO")
                || x.Restock && x.Condition!="SELLADO" || !x.Restock && !Text(x.NoRestockReason,500)
                || x.Evidence is null || x.Evidence.Count>5 || x.Evidence.Any(e=>!Evidence(e))
                || x.Condition=="ABIERTO_DEFECTUOSO" && x.Evidence.Count==0) ? "Revisa cantidad, condición, motivo de no reingreso y fotos (hasta 5 por línea; abierto requiere foto)."
            : r.Items.Sum(x=>x.Evidence.Count)>50 ? "Adjunta como máximo 50 fotos por solicitud."
            : r.CancelSale && r.Items.Any(x=>!x.Restock || x.Condition!="SELLADO") ? "La anulación completa requiere devolver toda la mercadería sellada y vendible." : null,
        ResolveSaleReturn r => r.OperationId==Guid.Empty || r.SaleId<=0 || r.ReturnId<=0 || r.ExpectedVersion is not {Length:8} || !Text(r.Reason,500)
            || r.Apply && (r.QuoteToken is not {Length:64} || r.ShiftId is <=0) ? "Actualiza la solicitud e indica motivo y resolución." : null,
        _ => "Operación de posventa desconocida."
    };
    private static bool Text(string? value,int max)=>!string.IsNullOrWhiteSpace(value)&&value.Trim().Length<=max;
    private static bool Evidence(ReturnEvidence e)=>Text(e.Name,255)&&e.MimeType is "image/png" or "image/jpeg"
        &&Text(e.RelativePath,500)&&e.Sha256 is {Length:64}&&e.Sha256.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
    public static RequestSaleReturn Freeze(RequestSaleReturn r)=>r with{ExpectedSaleVersion=r.ExpectedSaleVersion.ToArray(),Proof=r.Proof.Trim(),Reason=r.Reason.Trim(),Items=r.Items.OrderBy(x=>x.SaleLineId).Select(x=>x with{NoRestockReason=x.NoRestockReason?.Trim(),Evidence=x.Evidence.Select(e=>e with{Name=e.Name.Trim()}).ToArray()}).ToArray()};
    public static ResolveSaleReturn Freeze(ResolveSaleReturn r)=>r with{ExpectedVersion=r.ExpectedVersion.ToArray(),Reason=r.Reason.Trim()};
}
