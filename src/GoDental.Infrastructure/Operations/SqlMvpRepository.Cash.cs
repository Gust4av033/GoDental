using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;

namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    private const string ShiftColumns = "t.IdTurno Id,t.IdCaja CashBoxId,t.FechaApertura Opened,t.FechaCierre Closed,t.Estado State,t.MontoInicial Opening,t.EfectivoContado Counted,t.Diferencia Difference,t.RowVersion,t.MontoInicial+ISNULL((SELECT SUM(CASE WHEN m.Tipo IN(N'VENTA_EFECTIVO',N'INGRESO',N'AJUSTE') THEN m.Monto ELSE -m.Monto END) FROM ven.MovimientosCaja m WHERE m.IdTurno=t.IdTurno),0) Expected";
    public Task<IReadOnlyList<CashBoxData>> CashBoxesAsync(int userId, CancellationToken ct) => Read(userId, ["CAJA.VER"], w => w.List<CashBoxData>("SELECT IdCaja Id,IdAlmacen WarehouseId,Codigo Code,Nombre Name,Activo Active FROM ven.Cajas ORDER BY Nombre,IdCaja;"), ct);
    public Task<CashShiftData?> ReadShiftAsync(int boxId, int userId, CancellationToken ct) => Read(userId, ["CAJA.VER"], w => w.Maybe<CashShiftData>($"SELECT {ShiftColumns} FROM ven.TurnosCaja t WHERE t.IdCaja=@boxId AND t.Estado=N'ABIERTO';", new { boxId }), ct);
    public Task<CashShiftData?> ReadShiftByIdAsync(long id, int userId, CancellationToken ct) => Read(userId, ["CAJA.VER"], w => w.Maybe<CashShiftData>($"SELECT {ShiftColumns} FROM ven.TurnosCaja t WHERE t.IdTurno=@id;", new { id }), ct);
    public Task<MvpPage<CashMovementData>> CashMovementsAsync(long shiftId, MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["CAJA.VER"], w => Page<CashMovementData>(w, "ven.MovimientosCaja", "IdMovimientoCaja Id,Fecha Date,Tipo Type,Monto Amount,Motivo Reason", "IdTurno=@shiftId", "IdMovimientoCaja DESC", new { shiftId, Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }), ct);
    public Task<Result<MvpReceipt>> SaveBoxAsync(CashBoxChange r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "GUARDAR_CAJA", r, userId, workstationId, ["CAJA.VER", "AJUSTES.EDITAR"], async w =>
    {
        Check(r);
        Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM inv.Almacenes WITH(HOLDLOCK) WHERE IdAlmacen=@WarehouseId AND Activo=1) THEN 1 ELSE 0 END);", r), "VALIDATION", "Almacén no disponible.");
        var code = r.Id == 0 && string.IsNullOrWhiteSpace(r.Code)
            ? await GoDental.Infrastructure.Data.MasterCode.NextAsync(w.Connection, w.Transaction, "MAESTRO_CAJA", ct) : r.Code;
        if (r.Id == 0)
            return await w.Scalar<long>("INSERT INTO ven.Cajas(Codigo,Nombre,IdAlmacen,Activo) OUTPUT INSERTED.IdCaja VALUES(@Code,@Name,@WarehouseId,@Active);", new { Code=code, r.Name, r.WarehouseId, r.Active });
        var previous = await w.Maybe<CashBoxData>("SELECT IdCaja Id,Codigo Code,Nombre Name,IdAlmacen WarehouseId,Activo Active FROM ven.Cajas WITH(UPDLOCK,HOLDLOCK) WHERE IdCaja=@Id;", r);
        Require(previous is not null, "INVALID_STATE", "La caja no existe.");
        var inUse = await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM ven.TurnosCaja WITH(HOLDLOCK) WHERE IdCaja=@Id) OR EXISTS(SELECT 1 FROM ven.Ventas WITH(HOLDLOCK) WHERE IdCaja=@Id) OR EXISTS(SELECT 1 FROM ven.RepartosMvp WITH(HOLDLOCK) WHERE IdCaja=@Id) THEN 1 ELSE 0 END);", r);
        Require(!inUse || previous!.Code == r.Code && previous.WarehouseId == r.WarehouseId, "INVALID_STATE", "Una caja con historial conserva código y almacén.");
        if (!r.Active)
            Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM ven.TurnosCaja WITH(HOLDLOCK) WHERE IdCaja=@Id AND Estado=N'ABIERTO') THEN 1 ELSE 0 END);", r), "INVALID_STATE", "Cierra el turno antes de desactivar la caja.");
        await w.Execute("UPDATE ven.Cajas SET Nombre=@Name,Codigo=@Code,IdAlmacen=@WarehouseId,Activo=@Active WHERE IdCaja=@Id;", r);
        return r.Id;
    }, ct);
    public Task<Result<MvpReceipt>> OpenShiftAsync(OpenCashShift r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "ABRIR_CAJA", r, userId, workstationId, ["CAJA.VER", "CAJA.ABRIR"], async w =>
    {
        Check(r);
        return await w.Scalar<long>("EXEC ven.sp_AbrirTurnoCaja @IdCaja=@CashBoxId,@IdUsuario=@userId,@MontoInicial=@OpeningAmount,@Observaciones=@Reason;", new { r.CashBoxId, userId, r.OpeningAmount, r.Reason });
    }, ct);
    private static async Task<CashShiftData> LockShift(Work w, long id, int? boxId = null, byte[]? expected = null)
    {
        var s = await w.Maybe<CashShiftData>($"SELECT {ShiftColumns} FROM ven.TurnosCaja t WITH(UPDLOCK,HOLDLOCK) WHERE t.IdTurno=@id;", new { id });
        Require(s is not null && s.State == "ABIERTO" && (boxId is null || s.CashBoxId == boxId), "INVALID_STATE", "La caja no tiene un turno abierto compatible.");
        if (expected is not null)
            Version(s!.RowVersion, expected);
        return s!;
    }

    private static Task TouchShift(Work w, long id) => w.Execute("UPDATE ven.TurnosCaja SET MontoInicial=MontoInicial WHERE IdTurno=@id;", new { id });
    public Task<Result<MvpReceipt>> MoveCashAsync(CashMovement r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "MOVIMIENTO_CAJA", r, userId, workstationId, ["CAJA.VER", "CAJA.MOVIMIENTO"], async w =>
    {
        Check(r);
        var shift = await LockShift(w, r.ShiftId, expected: r.ExpectedVersion);
        Require(r.Type != "RETIRO" || shift.Expected >= r.Amount, "INVALID_STATE", "El retiro supera el efectivo esperado.");
        var id = await w.Scalar<long>("INSERT INTO ven.MovimientosCaja(IdTurno,Tipo,Monto,IdUsuario,Motivo) OUTPUT INSERTED.IdMovimientoCaja VALUES(@ShiftId,@Type,@Amount,@userId,@Reason);", new { r.ShiftId, r.Type, r.Amount, userId, r.Reason });
        await TouchShift(w, r.ShiftId);
        return id;
    }, ct);
    public Task<Result<MvpReceipt>> CloseShiftAsync(CloseCashShift r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "CERRAR_CAJA", r, userId, workstationId, ["CAJA.VER", "CAJA.CERRAR"], async w =>
    {
        Check(r);
        await LockShift(w, r.ShiftId, expected: r.ExpectedVersion);
        await w.Execute("EXEC ven.sp_CerrarTurnoCaja @IdTurno=@ShiftId,@IdUsuario=@userId,@EfectivoContado=@Counted,@Observaciones=@Reason;", new { r.ShiftId, userId, r.Counted, r.Reason });
        return r.ShiftId;
    }, ct);
    public Task<MvpPage<CreditAccountData>> AccountsAsync(bool overdueOnly, MvpFilter f, int userId, CancellationToken ct) => CreditAccountsAsync(overdueOnly, false, f, userId, ct);
    public Task<MvpPage<CreditAccountData>> DueAccountsAsync(bool overdueOnly, MvpFilter f, int userId, CancellationToken ct) => CreditAccountsAsync(overdueOnly, true, f, userId, ct);
    private Task<MvpPage<CreditAccountData>> CreditAccountsAsync(bool overdueOnly, bool noticesOnly, MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["CLIENTES.VER", "VENTAS.VER"], w => Page<CreditAccountData>(w,
        """
        ven.CuentasPorCobrar a JOIN cli.Clientes c ON c.IdCliente=a.IdCliente JOIN ven.Ventas v ON v.IdVenta=a.IdVenta
        OUTER APPLY(SELECT COALESCE(a.ProximoAviso,DATEADD(day,COALESCE(c.IntervaloAvisoDias,
         (SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WHERE Clave=N'Credito.IntervaloAvisoDias'),5),a.FechaEmision)) NextDate) n
        OUTER APPLY(SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time') Today) d
        """,
        """
        a.IdCuenta Id,a.IdCliente CustomerId,c.NombreRazonSocial Customer,v.NumeroVenta Sale,a.FechaVencimiento Due,n.NextDate NextNotice,
        a.MontoOriginal Original,a.Saldo Balance,a.Estado State,a.RowVersion,
        CONVERT(bit,CASE WHEN a.Saldo>0 AND a.FechaVencimiento<d.Today AND a.Estado<>N'CASTIGADA' THEN 1 ELSE 0 END) Overdue,
        CONVERT(bit,CASE WHEN a.Saldo>0 AND a.Estado IN(N'PENDIENTE',N'PARCIAL',N'VENCIDA') AND n.NextDate<=d.Today THEN 1 ELSE 0 END) NoticeDue
        """,
        "(@overdueOnly=0 OR (a.Saldo>0 AND a.FechaVencimiento<d.Today AND a.Estado<>N'CASTIGADA')) AND (@noticesOnly=0 OR (a.Saldo>0 AND a.Estado IN(N'PENDIENTE',N'PARCIAL',N'VENCIDA') AND n.NextDate<=d.Today))",
        "a.FechaVencimiento,a.IdCuenta", new { overdueOnly, noticesOnly, Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }), ct);
    public Task<Result<MvpReceipt>> PayAccountAsync(AccountPayment r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "ABONO_CLIENTE", r, userId, workstationId, ["CAJA.VER", "CLIENTES.VER", "VENTAS.VER", "CAJA.MOVIMIENTO"], async w =>
    {
        Check(r);
        var method = await Method(w, r.MethodId);
        Require(method.Code != "CREDITO", "VALIDATION", "El abono requiere un pago real distinto de crédito.");
        Require(!method.RequiresReference || !string.IsNullOrWhiteSpace(r.Reference), "VALIDATION", "El método requiere referencia.");
        if (method.AffectsCash)
        {
            Require(r.ShiftId is not null, "VALIDATION", "Selecciona el turno de caja.");
            await LockShift(w, r.ShiftId!.Value);
        }

        // Posventa y abonos toman venta antes de CxC para evitar invertir el orden de bloqueos.
        var saleId = await w.Scalar<long?>("SELECT IdVenta FROM ven.CuentasPorCobrar WHERE IdCuenta=@AccountId;", r);
        Require(saleId is not null,"INVALID_STATE","La cuenta por cobrar no existe.");
        await w.Execute("SELECT IdVenta FROM ven.Ventas WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@saleId;",new{saleId});
        var version = await w.Scalar<byte[]?>("SELECT RowVersion FROM ven.CuentasPorCobrar WITH(UPDLOCK,HOLDLOCK) WHERE IdCuenta=@AccountId;", r);
        Version(version, r.ExpectedVersion);
        var balanceBefore = await w.Scalar<decimal>("SELECT Saldo FROM ven.CuentasPorCobrar WHERE IdCuenta=@AccountId;", r);
        var id = await w.Scalar<long>("EXEC ven.sp_RegistrarAbono @IdCuenta=@AccountId,@IdMetodoPago=@MethodId,@Monto=@Amount,@IdUsuario=@userId,@Referencia=@Reference,@IdTurno=@ShiftId;", new { r.AccountId, r.MethodId, r.Amount, userId, r.Reference, r.ShiftId });
        await w.Execute("""
            UPDATE a SET ProximoAviso=CASE WHEN a.Saldo=0 THEN NULL ELSE
              DATEADD(day,COALESCE(c.IntervaloAvisoDias,(SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WHERE Clave=N'Credito.IntervaloAvisoDias'),5),
              CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time')) END
            FROM ven.CuentasPorCobrar a JOIN cli.Clientes c ON c.IdCliente=a.IdCliente WHERE a.IdCuenta=@AccountId;
            """, r);
        await w.Execute("""
            UPDATE p SET ClienteNombreSnapshot=c.NombreRazonSocial,MetodoSnapshot=@methodName,UsuarioSnapshot=u.NombreCompleto,
             SaldoAntes=@balanceBefore,SaldoDespues=a.Saldo
            FROM ven.AbonosCliente p JOIN ven.CuentasPorCobrar a ON a.IdCuenta=p.IdCuenta JOIN cli.Clientes c ON c.IdCliente=a.IdCliente
            JOIN seg.Usuarios u ON u.IdUsuario=p.RegistradoPor WHERE p.IdAbono=@id;
            """, new { id, methodName = method.Name, balanceBefore });
        if (method.AffectsCash)
            await TouchShift(w, r.ShiftId!.Value);
        return id;
    }, ct);
    private static void Check(object r)
    {
        if (MvpValidator.Validate(r) is { } error)
            throw new RuleFailure("VALIDATION", error);
    }

    private static async Task<PaymentMethodData> Method(Work w, short id)
    {
        var m = await w.Maybe<PaymentMethodData>("SELECT IdMetodoPago Id,Codigo Code,Nombre Name,AfectaCajaEfectivo AffectsCash,RequiereReferencia RequiresReference FROM cat.MetodosPago WITH(HOLDLOCK) WHERE IdMetodoPago=@id AND Activo=1;", new { id });
        Require(m is not null, "VALIDATION", "El método de pago no está disponible.");
        Require(m!.Code != "EFECTIVO_REPARTO", "VALIDATION", "El efectivo de reparto sólo se registra desde Pedidos y entregas.");
        Require(m.Code != "TARJETA" || (!m.AffectsCash && m.RequiresReference), "INVALID_STATE", "Revisa el catálogo: tarjeta manual exige referencia y no afecta efectivo.");
        return m;
    }
}
