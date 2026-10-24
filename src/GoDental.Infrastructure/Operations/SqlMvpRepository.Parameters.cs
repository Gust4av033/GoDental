using System.Globalization;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Infrastructure.Operations;

public sealed partial class SqlMvpRepository
{
    private const string JustificationKey = "Descuento.RequiereJustificacionSobre";
    private const string AuthorizationKey = "Descuento.RequiereAutorizacionSobre";
    private sealed class ParameterRow
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public byte[] Version { get; set; } = [];
        public bool Editable { get; set; }
        public string Type { get; set; } = "";
    }
    private static async Task<CommercialParameters> Parameters(Work w, bool editing)
    {
        var rows = await w.List<ParameterRow>("SELECT Clave [Key],Valor Value,RowVersion Version,EditableUI Editable,TipoDato Type FROM sis.Configuracion WITH(" + (editing ? "UPDLOCK,HOLDLOCK" : "HOLDLOCK") + ") WHERE Clave IN (@justificationKey,@authorizationKey) ORDER BY Clave;", new { justificationKey = JustificationKey, authorizationKey = AuthorizationKey });
        var j = rows.SingleOrDefault(r => r.Key == JustificationKey);
        var a = rows.SingleOrDefault(r => r.Key == AuthorizationKey);
        Require(j is { Editable: true, Type: "DECIMAL" } && a is { Editable: true, Type: "DECIMAL" }, "INVALID_STATE", "Faltan los parámetros comerciales editables. Revisa la instalación.");
        Require(decimal.TryParse(j!.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var justification)
            && decimal.TryParse(a!.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _), "INVALID_STATE", "Los umbrales guardados no son válidos.");
        var authorization = decimal.Parse(a!.Value, CultureInfo.InvariantCulture);
        Require(justification >= 0 && justification <= authorization && authorization <= 100, "INVALID_STATE", "Revisa el orden de los umbrales guardados.");
        return new(justification, authorization, j.Version, a.Version);
    }
    public Task<CommercialParameters> ReadParametersAsync(int userId, CancellationToken ct) => Read(userId, ["AJUSTES.EDITAR"], w => Parameters(w, false), ct);
    public Task<Result<MvpReceipt>> ChangeParametersAsync(ChangeCommercialParameters r, int userId, int? workstationId, CancellationToken ct)
        => Write(r.OperationId, "PARAMETROS_COMERCIALES", r, userId, workstationId, ["AJUSTES.EDITAR"], async w =>
        {
            Check(r);
            var before = await Parameters(w, true);
            Version(before.JustificationVersion, r.JustificationVersion); Version(before.AuthorizationVersion, r.AuthorizationVersion);
            await w.Execute("UPDATE sis.Configuracion SET Valor=@value,ModificadoPor=@userId,FechaModificacion=SYSDATETIMEOFFSET() WHERE Clave=@key;",
                new { value = r.Authorization.ToString("0.####", CultureInfo.InvariantCulture), userId, key = AuthorizationKey });
            await w.Execute("UPDATE sis.Configuracion SET Valor=@value,ModificadoPor=@userId,FechaModificacion=SYSDATETIMEOFFSET() WHERE Clave=@key;",
                new { value = r.Justification.ToString("0.####", CultureInfo.InvariantCulture), userId, key = JustificationKey });
            return await w.Scalar<long>("""
                INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,Descripcion,DatosAntesJson,DatosDespuesJson)
                OUTPUT INSERTED.IdBitacora VALUES(@userId,@workstationId,N'AJUSTES',N'EDITAR_PARAMETROS',N'sis.Configuracion',@Reason,@before,@after);
                """, new { userId, workstationId, r.Reason, before = Json(new { before.Justification, before.Authorization }), after = Json(new { r.Justification, r.Authorization }) });
        }, ct);
}
