using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    public Task<Result<MvpReceipt>> RegisterNoticeAsync(RegisterCollectionNotice request, int userId, int? workstationId, CancellationToken ct)
    {
        if (CollectionValidator.Validate(request) is { } error) return Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION", error));
        var r = CollectionValidator.Freeze(request);
        return Write(r.OperationId, "AVISO_COBRANZA", r, userId, workstationId, ["CLIENTES.VER", "VENTAS.VER", "CLIENTES.COBRANZA_GESTIONAR"], async w =>
        {
            var client = await w.Maybe<NoticeClient>("SELECT c.IntervaloAvisoDias Days FROM cli.Clientes c WITH(HOLDLOCK) WHERE c.IdCliente=(SELECT IdCliente FROM ven.CuentasPorCobrar WHERE IdCuenta=@AccountId);", r);
            Require(client is not null, "INVALID_STATE", "La cuenta ya no existe.");
            var interval = client!.Days ?? await w.Scalar<int>("SELECT ISNULL((SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WHERE Clave=N'Credito.IntervaloAvisoDias'),5);");
            Require(interval is > 0 and <= 32767, "INVALID_STATE", "Revisa el intervalo de avisos del cliente/configuración.");
            var account = await w.Maybe<NoticeAccount>("SELECT RowVersion,Saldo Balance,Estado State,FechaVencimiento Due,FechaEmision Issued,ProximoAviso NextNotice,UltimoAviso LastNotice FROM ven.CuentasPorCobrar WITH(UPDLOCK,HOLDLOCK) WHERE IdCuenta=@AccountId;", r);
            Version(account?.RowVersion, r.ExpectedVersion);
            Require(account!.Balance > 0 && account.State is "PENDIENTE" or "PARCIAL" or "VENCIDA", "INVALID_STATE", "La cuenta no tiene saldo cobrable.");
            var today = await w.Scalar<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
            Require((account.NextNotice ?? account.Issued.AddDays(interval)) <= today && account.LastNotice != today, "INVALID_STATE", "El aviso aún no corresponde o ya se registró hoy.");
            var next = today.AddDays(interval);
            var id = await w.Scalar<long>("""
                INSERT INTO ven.AvisosCobranza(IdCuenta,IdOperacion,Fecha,FechaLocal,IdUsuario,IdEquipo,Saldo,Vencimiento,ProximoAviso,Medio,Resultado,Observacion)
                OUTPUT INSERTED.IdAviso VALUES(@AccountId,@OperationId,SYSDATETIMEOFFSET(),@today,@userId,@workstationId,@Balance,@Due,@next,@Medium,@Outcome,@Reason);
                """, new { r.AccountId, r.OperationId, today, userId, workstationId, account.Balance, account.Due, next, r.Medium, r.Outcome, r.Reason });
            await w.Execute("UPDATE ven.CuentasPorCobrar SET UltimoAviso=@today,ProximoAviso=@next WHERE IdCuenta=@AccountId;", new { today, next, r.AccountId });
            return id;
        }, ct);
    }
    private sealed class NoticeClient { public int? Days { get; set; } }
    private sealed class NoticeAccount
    {
        public byte[] RowVersion { get; set; } = []; public decimal Balance { get; set; } public string State { get; set; } = "";
        public DateTime Due { get; set; } public DateTime Issued { get; set; } public DateTime? NextNotice { get; set; } public DateTime? LastNotice { get; set; }
    }
    public Task<MvpPage<CollectionNoticeData>> NoticesAsync(long accountId, MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["CLIENTES.VER", "VENTAS.VER"], w =>
        Page<CollectionNoticeData>(w, "ven.AvisosCobranza n JOIN seg.Usuarios u ON u.IdUsuario=n.IdUsuario", "n.IdAviso Id,n.IdCuenta AccountId,n.Fecha Date,u.NombreCompleto [User],n.Saldo Balance,n.Vencimiento Due,n.ProximoAviso NextNotice,n.Medio Medium,n.Resultado Outcome,n.Observacion Reason", "n.IdCuenta=@accountId", "n.Fecha DESC,n.IdAviso DESC", new { accountId, Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }), ct);
    public Task<CustomerStatement?> StatementAsync(long customerId, bool forPrint, int userId, CancellationToken ct) => Read<CustomerStatement?>(userId, forPrint ? ["CLIENTES.VER", "VENTAS.VER", "VENTAS.REIMPRIMIR"] : ["CLIENTES.VER", "VENTAS.VER"], async w =>
    {
        var customer = await w.Maybe<CustomerData>("SELECT Codigo Code,NombreRazonSocial Name FROM cli.Clientes WITH(HOLDLOCK) WHERE IdCliente=@customerId;", new { customerId });
        if (customer is null) return null;
        // Venta antes de CxC, igual que abonos/posventa. Lectura consistente sin mezclar versiones.
        await w.List<long>("SELECT IdVenta FROM ven.Ventas WITH(HOLDLOCK) WHERE IdCliente=@customerId;", new { customerId });
        var accounts = await w.List<CreditAccountData>("""
            SELECT TOP(5001) a.IdCuenta Id,a.IdCliente CustomerId,v.NumeroVenta Sale,a.FechaVencimiento Due,
             a.MontoOriginal Original,a.Saldo Balance,a.Estado State,a.RowVersion,a.ProximoAviso NextNotice
            FROM ven.CuentasPorCobrar a JOIN ven.Ventas v ON v.IdVenta=a.IdVenta WHERE a.IdCliente=@customerId ORDER BY a.IdCuenta;
            """, new { customerId });
        Require(accounts.Count <= 5000, "VALIDATION", "El estado supera 5000 cuentas; requiere un reporte de mayor volumen.");
        var movements = await w.List<CreditMovementData>("""
            SELECT TOP(5001) Id,AccountId,Date,Type,Document,Sale,Delta,CashRefund FROM (
             SELECT a.IdCuenta Id,a.IdCuenta AccountId,v.Fecha Date,N'CARGO' Type,v.NumeroVenta Document,v.NumeroVenta Sale,a.MontoOriginal Delta,CONVERT(decimal(18,2),0) CashRefund,0 Sort
             FROM ven.CuentasPorCobrar a JOIN ven.Ventas v ON v.IdVenta=a.IdVenta WHERE a.IdCliente=@customerId
             UNION ALL
             SELECT p.IdAbono,p.IdCuenta,p.Fecha,N'ABONO',p.NumeroRecibo,v.NumeroVenta,-p.Monto,0,1
             FROM ven.AbonosCliente p JOIN ven.CuentasPorCobrar a ON a.IdCuenta=p.IdCuenta JOIN ven.Ventas v ON v.IdVenta=a.IdVenta WHERE a.IdCliente=@customerId AND p.Anulado=0
             UNION ALL
             SELECT d.IdDevolucion,a.IdCuenta,d.FechaAplicacion,CASE WHEN d.EsAnulacion=1 THEN N'ANULACION' ELSE N'DEVOLUCION' END,
              CONCAT(N'DEV-',d.IdDevolucion),v.NumeroVenta,-d.ReduccionDeuda,d.ReintegroEfectivo,2
             FROM ven.DevolucionesVenta d JOIN ven.CuentasPorCobrar a ON a.IdVenta=d.IdVenta JOIN ven.Ventas v ON v.IdVenta=a.IdVenta
             WHERE a.IdCliente=@customerId AND d.Estado=N'APLICADA' AND d.FechaAplicacion IS NOT NULL
            ) m ORDER BY Date,Sort,Id;
            """, new { customerId });
        Require(movements.Count <= 5000, "VALIDATION", "El estado supera 5000 movimientos; no se emitirá un documento incompleto.");
        decimal running = 0; foreach (var row in movements) { running += row.Delta; row.RunningBalance = running; }
        var now = await w.Scalar<DateTimeOffset>("SELECT SYSDATETIMEOFFSET();");
        return new(customerId, customer.Code, customer.Name, now, accounts, movements);
    }, ct);
    public Task<PaymentReceiptData?> PaymentReceiptAsync(long paymentId, int userId, CancellationToken ct) => Read(userId, ["CLIENTES.VER", "VENTAS.VER", "VENTAS.REIMPRIMIR"], w => w.Maybe<PaymentReceiptData>("""
        SELECT p.IdAbono Id,p.IdCuenta AccountId,a.IdCliente CustomerId,p.NumeroRecibo Number,p.Fecha Date,p.Monto Amount,p.Referencia Reference,p.Anulado Voided,
         COALESCE(p.ClienteNombreSnapshot,c.NombreRazonSocial) Customer,COALESCE(p.MetodoSnapshot,m.Nombre) Method,COALESCE(p.UsuarioSnapshot,u.NombreCompleto) [User],
         SYSDATETIMEOFFSET() AsOf,p.SaldoAntes BalanceBefore,p.SaldoDespues BalanceAfter,a.Saldo CurrentBalance,v.NumeroVenta Sale,
         CONVERT(bit,CASE WHEN p.SaldoAntes IS NULL OR p.SaldoDespues IS NULL OR p.ClienteNombreSnapshot IS NULL OR p.MetodoSnapshot IS NULL OR p.UsuarioSnapshot IS NULL THEN 1 ELSE 0 END) HistoricalDetailsMissing
        FROM ven.AbonosCliente p JOIN ven.CuentasPorCobrar a ON a.IdCuenta=p.IdCuenta JOIN cli.Clientes c ON c.IdCliente=a.IdCliente
        JOIN ven.Ventas v ON v.IdVenta=a.IdVenta JOIN cat.MetodosPago m ON m.IdMetodoPago=p.IdMetodoPago JOIN seg.Usuarios u ON u.IdUsuario=p.RegistradoPor WHERE p.IdAbono=@paymentId;
        """, new { paymentId }), ct);
}
