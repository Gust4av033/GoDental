using GoDental.Application.DTOs;
namespace GoDental.Application.Validators;
public static class ImportValidator
{
    private static bool Text(string? value, int max) => value is null || value.Length <= max && !value.Any(char.IsControl);
    private static bool Basic(Guid id, long order, long import, byte[] version, string reason) => id != Guid.Empty && order > 0 && import > 0 && MvpValidator.Version(version) && MvpValidator.Reason(reason) && reason.Length <= 300;
    public static string? Validate(object request) => request switch
    {
        SaveImportation r => r.OperationId == Guid.Empty || r.OrderId <= 0 || !MvpValidator.Version(r.ExpectedOrderVersion) || (!MvpValidator.Reason(r.Reason) || r.Reason.Length > 300) || !Text(r.Courier,80) || !Text(r.Guide,120) || !Date(r.ExpectedDate) ? "Revisa orden, versión, courier, guía, fecha y motivo." : null,
        ImportEvent r => !Basic(r.OperationId,r.OrderId,r.ImportId,r.ExpectedVersion,r.Reason) || !Text(r.Courier,80) || !Text(r.Guide,120) || !Date(r.ExpectedDate) || r.State is not ("CONFIRMADA" or "EN_PRODUCCION" or "DESPACHADA" or "EN_TRANSITO" or "ADUANA" or "RECIBIDA_PARCIAL" or "RECIBIDA") ? "Revisa logística, versión y motivo." : null,
        ImportExpense r => !Basic(r.OperationId,r.OrderId,r.ImportId,r.ExpectedVersion,r.Reason) || r.Type is not ("FLETE" or "IMPUESTO" or "ADUANA" or "MANEJO" or "SEGURO" or "OTRO") || !MvpValidator.Money(r.Amount,true) || !PurchaseValidator.Rate(r.Rate) || r.CurrencyId <= 0 || !Date(r.Date) || !Text(r.Description,250) ? "Revisa gasto, importe, moneda, cambio, fecha y descripción." : null,
        VoidImportExpense r => !Basic(r.OperationId,r.OrderId,r.ImportId,r.ExpectedVersion,r.Reason) || r.ExpenseId <= 0 ? "Actualiza la importación y selecciona un gasto." : null,
        CloseImportCosts r => !Basic(r.OperationId,r.OrderId,r.ImportId,r.ExpectedVersion,r.Reason) || !PurchaseValidator.Rate(r.Rate) ? "Revisa importación, cambio real y motivo del cierre." : null,
        PrepareImportReceipt r => r.Receipt is null || r.OperationId != r.Receipt.OperationId || r.ImportId <= 0 || !MvpValidator.Version(r.ExpectedImportVersion) ? "Revisa importación y versión." : PurchaseValidator.Validate(r.Receipt),
        _ => "Solicitud de importación desconocida."
    };
    private static bool Date(DateTime? value) => value is null || value.Value.Year >= 1900 && value.Value.TimeOfDay == TimeSpan.Zero;
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    public static object Freeze(object request) => request switch
    {
        SaveImportation r => r with { ExpectedOrderVersion=r.ExpectedOrderVersion.ToArray(), Courier=Clean(r.Courier),Guide=Clean(r.Guide),Reason=r.Reason.Trim() },
        ImportEvent r => r with { ExpectedVersion=r.ExpectedVersion.ToArray(),Courier=Clean(r.Courier),Guide=Clean(r.Guide),Reason=r.Reason.Trim() },
        ImportExpense r => r with { ExpectedVersion=r.ExpectedVersion.ToArray(),Description=r.Description.Trim(),Reason=r.Reason.Trim() },
        VoidImportExpense r => r with { ExpectedVersion=r.ExpectedVersion.ToArray(),Reason=r.Reason.Trim() },
        CloseImportCosts r => r with { ExpectedVersion=r.ExpectedVersion.ToArray(),Reason=r.Reason.Trim() },
        PrepareImportReceipt r => r with { ExpectedImportVersion=r.ExpectedImportVersion.ToArray(),Receipt=PurchaseValidator.Freeze(r.Receipt) },
        _ => throw new ArgumentException("Solicitud desconocida.")
    };
    public static string Type(object r) => r switch { SaveImportation => "CREAR_IMPORTACION", ImportEvent => "EVENTO_IMPORTACION", ImportExpense => "GASTO_IMPORTACION", VoidImportExpense => "ANULAR_GASTO_IMPORTACION", CloseImportCosts => "CERRAR_COSTOS_IMPORTACION", PrepareImportReceipt => "RECEPCION_IMPORTACION", _ => throw new ArgumentException("Solicitud desconocida.") };
    public static Guid Id(object r) => r switch { SaveImportation x=>x.OperationId,ImportEvent x=>x.OperationId,ImportExpense x=>x.OperationId,VoidImportExpense x=>x.OperationId,CloseImportCosts x=>x.OperationId,PrepareImportReceipt x=>x.OperationId,_=>Guid.Empty };
}
