/* DEV-17: extensión aditiva del MVP. Aplicar después de 011. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
USE [GoDental];
GO
IF OBJECT_ID(N'sis.OperacionesMvp',N'U') IS NULL
CREATE TABLE sis.OperacionesMvp(
 IdOperacion uniqueidentifier NOT NULL CONSTRAINT PK_OperacionesMvp PRIMARY KEY,
 Tipo nvarchar(40) NOT NULL,IdUsuario int NOT NULL,HashSolicitud binary(32) NOT NULL,
 EntidadId bigint NOT NULL,Fecha datetimeoffset(0) NOT NULL CONSTRAINT DF_OperacionesMvp_Fecha DEFAULT SYSDATETIMEOFFSET(),
 CONSTRAINT FK_OperacionesMvp_Usuario FOREIGN KEY(IdUsuario) REFERENCES seg.Usuarios(IdUsuario));
GO
IF COL_LENGTH(N'ven.TurnosCaja',N'RowVersion') IS NULL ALTER TABLE ven.TurnosCaja ADD RowVersion rowversion NOT NULL;
GO
IF COL_LENGTH(N'ven.CuentasPorCobrar',N'RowVersion') IS NULL ALTER TABLE ven.CuentasPorCobrar ADD RowVersion rowversion NOT NULL;
GO
IF COL_LENGTH(N'ven.Ventas',N'EfectivoRecibido') IS NULL ALTER TABLE ven.Ventas ADD EfectivoRecibido decimal(18,2) NOT NULL CONSTRAINT DF_Ventas_EfectivoRecibido DEFAULT(0);
GO
IF COL_LENGTH(N'ven.Ventas',N'Cambio') IS NULL ALTER TABLE ven.Ventas ADD Cambio decimal(18,2) NOT NULL CONSTRAINT DF_Ventas_Cambio DEFAULT(0);
GO
IF COL_LENGTH(N'ven.Ventas',N'CargoEntrega') IS NULL ALTER TABLE ven.Ventas ADD CargoEntrega decimal(18,2) NOT NULL CONSTRAINT DF_Ventas_CargoEntrega DEFAULT(0);
GO
IF COL_LENGTH(N'ven.Ventas',N'ClienteSnapshotMvp') IS NULL ALTER TABLE ven.Ventas ADD ClienteSnapshotMvp nvarchar(max) NULL;
GO
IF COL_LENGTH(N'ven.VentaDetalle',N'CalculoPrecioMvp') IS NULL ALTER TABLE ven.VentaDetalle ADD CalculoPrecioMvp nvarchar(max) NULL;
GO
IF COL_LENGTH(N'ven.Cotizaciones',N'RowVersion') IS NULL ALTER TABLE ven.Cotizaciones ADD RowVersion rowversion NOT NULL;
GO
IF COL_LENGTH(N'ven.Cotizaciones',N'SnapshotMvp') IS NULL ALTER TABLE ven.Cotizaciones ADD SnapshotMvp nvarchar(max) NULL;
GO
IF COL_LENGTH(N'sis.AutorizacionesOperacion',N'TokenCalculoMvp') IS NULL ALTER TABLE sis.AutorizacionesOperacion ADD TokenCalculoMvp char(64) NULL;
GO
IF COL_LENGTH(N'sis.AutorizacionesOperacion',N'UtilizadaMvp') IS NULL ALTER TABLE sis.AutorizacionesOperacion ADD UtilizadaMvp bit NOT NULL CONSTRAINT DF_Autorizaciones_UtilizadaMvp DEFAULT(0);
GO
IF EXISTS(SELECT IdCaja FROM ven.TurnosCaja WHERE Estado=N'ABIERTO' GROUP BY IdCaja HAVING COUNT(*)>1)
 THROW 53402,N'Hay cajas con varios turnos abiertos. Revisar antes de aplicar 012.',1;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ven.TurnosCaja') AND name=N'UX_TurnosCaja_Abierto')
 CREATE UNIQUE INDEX UX_TurnosCaja_Abierto ON ven.TurnosCaja(IdCaja) WHERE Estado=N'ABIERTO';
GO
IF EXISTS(SELECT IdCotizacion FROM ven.Ventas WHERE IdCotizacion IS NOT NULL GROUP BY IdCotizacion HAVING COUNT(*)>1)
 THROW 53402,N'Hay cotizaciones vinculadas a varias ventas. Revisar antes de aplicar 012.',1;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ven.Ventas') AND name=N'UX_Ventas_Cotizacion')
 CREATE UNIQUE INDEX UX_Ventas_Cotizacion ON ven.Ventas(IdCotizacion) WHERE IdCotizacion IS NOT NULL;
GO
CREATE OR ALTER PROCEDURE ven.sp_ConfirmarVenta
    @IdVenta BIGINT,
    @IdUsuario INT,
    @MotivoDescuento NVARCHAR(1000) = NULL,
    @IdAutorizacionDescuento BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Estado NVARCHAR(20);
        DECLARE @IdCliente BIGINT;
        DECLARE @IdCaja INT;
        DECLARE @IdTurno BIGINT;
        DECLARE @FechaVenta DATETIMEOFFSET(0);
        DECLARE @Total DECIMAL(18,2);
        DECLARE @CargoEntrega DECIMAL(18,2);
        DECLARE @MaxDescuento DECIMAL(9,4);
        DECLARE @UmbralJustificacion DECIMAL(9,4) =
            ISNULL((SELECT TRY_CONVERT(DECIMAL(9,4), Valor) FROM sis.Configuracion WHERE Clave = N'Descuento.RequiereJustificacionSobre'), 7.00);
        DECLARE @UmbralAutorizacion DECIMAL(9,4) =
            ISNULL((SELECT TRY_CONVERT(DECIMAL(9,4), Valor) FROM sis.Configuracion WHERE Clave = N'Descuento.RequiereAutorizacionSobre'), 35.00);

        SELECT
            @Estado = Estado,
            @IdCliente = IdCliente,
            @IdCaja = IdCaja,
            @IdTurno = IdTurno,
            @FechaVenta = Fecha,
            @CargoEntrega = CargoEntrega
        FROM ven.Ventas WITH (UPDLOCK, HOLDLOCK)
        WHERE IdVenta = @IdVenta;

        IF @Estado IS NULL
            THROW 51030, N'La venta no existe.', 1;

        IF @Estado <> N'BORRADOR'
            THROW 51031, N'Sólo se puede confirmar una venta en estado BORRADOR.', 1;

        IF NOT EXISTS (SELECT 1 FROM ven.VentaDetalle WHERE IdVenta = @IdVenta)
            THROW 51032, N'La venta no contiene productos.', 1;

        SELECT @MaxDescuento = ISNULL(MAX(DescuentoPorcentaje), 0)
        FROM ven.VentaDetalle
        WHERE IdVenta = @IdVenta;

        IF @MaxDescuento > @UmbralJustificacion
           AND NULLIF(LTRIM(RTRIM(@MotivoDescuento)), N'') IS NULL
            THROW 51033, N'El descuento supera el límite ordinario y requiere justificación.', 1;

        IF @MaxDescuento > @UmbralAutorizacion
        BEGIN
            IF @IdAutorizacionDescuento IS NULL
                THROW 51034, N'El descuento requiere autorización administrativa.', 1;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sis.AutorizacionesOperacion ao WITH (UPDLOCK, HOLDLOCK)
                WHERE ao.IdAutorizacion = @IdAutorizacionDescuento
                  AND ao.TipoOperacion = N'DESCUENTO_MAYOR_35'
                  AND ao.EntidadTipo = N'VENTA'
                  AND ao.EntidadId = @IdVenta
                  AND ao.Resultado = N'APROBADA'
                  AND ao.FechaAutorizacion IS NOT NULL
                  AND ISNULL(ao.PorcentajeValor, 0) >= @MaxDescuento
            )
                THROW 51035, N'La autorización administrativa no es válida para este descuento.', 1;
        END;

        SELECT
            @Total = CAST(ISNULL(SUM(TotalLinea), 0) + @CargoEntrega AS DECIMAL(18,2))
        FROM ven.VentaDetalle
        WHERE IdVenta = @IdVenta;

        IF @Total <= 0
            THROW 51036, N'El total de la venta debe ser mayor que cero.', 1;

        DECLARE @TotalPagos DECIMAL(18,2);
        DECLARE @MontoCredito DECIMAL(18,2);
        DECLARE @MontoEfectivo DECIMAL(18,2);

        SELECT
            @TotalPagos = ISNULL(SUM(CASE WHEN vp.Estado <> N'ANULADO' THEN vp.Monto ELSE 0 END), 0),
            @MontoCredito = ISNULL(SUM(CASE WHEN vp.Estado <> N'ANULADO' AND mp.Codigo = N'CREDITO' THEN vp.Monto ELSE 0 END), 0),
            @MontoEfectivo = ISNULL(SUM(CASE WHEN vp.Estado <> N'ANULADO' AND mp.AfectaCajaEfectivo = 1 THEN vp.Monto ELSE 0 END), 0)
        FROM ven.VentaPagos vp
        INNER JOIN cat.MetodosPago mp ON mp.IdMetodoPago = vp.IdMetodoPago
        WHERE vp.IdVenta = @IdVenta;

        IF @TotalPagos <> @Total
            THROW 51037, N'La suma de pagos no coincide con el total de la venta.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM ven.VentaPagos vp
            INNER JOIN cat.MetodosPago mp ON mp.IdMetodoPago = vp.IdMetodoPago
            WHERE vp.IdVenta = @IdVenta
              AND vp.Estado <> N'ANULADO'
              AND mp.RequiereReferencia = 1
              AND NULLIF(LTRIM(RTRIM(vp.Referencia)), N'') IS NULL
        )
            THROW 51038, N'Existe un método de pago que requiere referencia y no la tiene.', 1;

        IF @MontoEfectivo > 0
        BEGIN
            IF @IdTurno IS NULL
                THROW 51039, N'Una venta con efectivo requiere un turno de caja abierto.', 1;

            IF NOT EXISTS
            (
                SELECT 1
                FROM ven.TurnosCaja WITH (UPDLOCK, HOLDLOCK)
                WHERE IdTurno = @IdTurno
                  AND IdCaja = @IdCaja
                  AND Estado = N'ABIERTO'
            )
                THROW 51040, N'El turno de caja no está abierto o no corresponde a la caja de la venta.', 1;
        END;

        IF @MontoCredito > 0
        BEGIN
            DECLARE @PermiteCredito BIT;
            DECLARE @BloqueadoCredito BIT;
            DECLARE @LimiteCredito DECIMAL(18,2);
            DECLARE @DiasCredito SMALLINT;
            DECLARE @SaldoActualCredito DECIMAL(18,2);
            DECLARE @DiasCreditoDefault INT =
                ISNULL((SELECT TRY_CONVERT(INT, Valor) FROM sis.Configuracion WHERE Clave = N'Credito.DiasDefault'), 15);

            IF @IdCliente IS NULL
                THROW 51041, N'No se puede vender a crédito sin cliente identificado.', 1;

            SELECT
                @PermiteCredito = PermiteCredito,
                @BloqueadoCredito = BloqueadoCredito,
                @LimiteCredito = ISNULL(LimiteCredito, 0),
                @DiasCredito = ISNULL(DiasCredito, @DiasCreditoDefault)
            FROM cli.Clientes WITH (UPDLOCK, HOLDLOCK)
            WHERE IdCliente = @IdCliente AND Activo = 1;

            IF @PermiteCredito IS NULL
                THROW 51042, N'El cliente no existe o está inactivo.', 1;

            IF @PermiteCredito = 0
                THROW 51043, N'El cliente no tiene crédito habilitado.', 1;

            IF @BloqueadoCredito = 1
                THROW 51044, N'El crédito del cliente está bloqueado.', 1;

            SELECT @SaldoActualCredito = ISNULL(SUM(Saldo), 0)
            FROM ven.CuentasPorCobrar WITH (UPDLOCK, HOLDLOCK)
            WHERE IdCliente = @IdCliente
              AND Saldo > 0
              AND Estado IN (N'PENDIENTE', N'PARCIAL', N'VENCIDA');

            IF @MontoCredito > (@LimiteCredito - @SaldoActualCredito)
                THROW 51045, N'La venta excede el crédito disponible del cliente.', 1;
        END;

        DECLARE
            @IdDetalle BIGINT,
            @IdProducto INT,
            @IdLote BIGINT,
            @Cantidad DECIMAL(18,4),
            @IdAlmacen INT,
            @ControlaLote BIT,
            @StockAnterior DECIMAL(18,4),
            @StockReservado DECIMAL(18,4),
            @StockPosterior DECIMAL(18,4),
            @CostoPromedio DECIMAL(18,6),
            @StockLote DECIMAL(18,4);

        SELECT @IdAlmacen = IdAlmacen
        FROM ven.Cajas
        WHERE IdCaja = @IdCaja;

        DECLARE curVenta CURSOR LOCAL FAST_FORWARD FOR
            SELECT vd.IdDetalle, vd.IdProducto, vd.IdLote, vd.Cantidad
            FROM ven.VentaDetalle vd
            WHERE vd.IdVenta = @IdVenta
            ORDER BY vd.IdProducto, ISNULL(vd.IdLote, 0), vd.IdDetalle;

        OPEN curVenta;
        FETCH NEXT FROM curVenta INTO @IdDetalle, @IdProducto, @IdLote, @Cantidad;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @ControlaLote = NULL;

            SELECT @ControlaLote = ControlaLote
            FROM inv.Productos
            WHERE IdProducto = @IdProducto AND Activo = 1;

            IF @ControlaLote IS NULL
                THROW 51046, N'La venta contiene un producto inexistente o inactivo.', 1;

            IF @ControlaLote = 1 AND @IdLote IS NULL
                THROW 51047, N'Un producto controlado por lote requiere lote en la venta.', 1;

            SELECT
                @StockAnterior = StockActual,
                @StockReservado = StockReservado,
                @CostoPromedio = CostoPromedio
            FROM inv.ExistenciasProducto WITH (UPDLOCK, HOLDLOCK)
            WHERE IdAlmacen = @IdAlmacen
              AND IdProducto = @IdProducto;

            IF @StockAnterior IS NULL
                THROW 51048, N'No existe saldo de inventario para uno de los productos.', 1;

            IF (@StockAnterior - @StockReservado) < @Cantidad
                THROW 51049, N'Stock disponible insuficiente para confirmar la venta.', 1;

            IF @IdLote IS NOT NULL
            BEGIN
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM inv.LotesProducto
                    WHERE IdLote = @IdLote
                      AND IdProducto = @IdProducto
                      AND Activo = 1
                      AND (FechaVencimiento IS NULL OR FechaVencimiento >= CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time'))
                )
                    THROW 51050, N'El lote no pertenece al producto de la venta.', 1;

                SELECT @StockLote = StockActual
                FROM inv.ExistenciasLote WITH (UPDLOCK, HOLDLOCK)
                WHERE IdAlmacen = @IdAlmacen
                  AND IdLote = @IdLote;

                IF @StockLote IS NULL OR @StockLote < @Cantidad
                    THROW 51051, N'Stock insuficiente en el lote seleccionado.', 1;

                UPDATE inv.ExistenciasLote
                SET StockActual = StockActual - @Cantidad
                WHERE IdAlmacen = @IdAlmacen
                  AND IdLote = @IdLote;
            END;

            SET @StockPosterior = @StockAnterior - @Cantidad;

            UPDATE inv.ExistenciasProducto
            SET StockActual = @StockPosterior,
                FechaUltimoMovimiento = SYSDATETIMEOFFSET()
            WHERE IdAlmacen = @IdAlmacen
              AND IdProducto = @IdProducto;

            UPDATE ven.VentaDetalle
            SET CostoUnitario = @CostoPromedio
            WHERE IdDetalle = @IdDetalle;

            INSERT INTO inv.MovimientosInventario
            (
                IdAlmacen, IdProducto, IdLote, TipoMovimiento, Cantidad,
                CostoUnitario, ExistenciaAnterior, ExistenciaPosterior,
                ReferenciaTipo, ReferenciaId, IdUsuario, Observacion
            )
            VALUES
            (
                @IdAlmacen, @IdProducto, @IdLote, N'VENTA', -@Cantidad,
                @CostoPromedio, @StockAnterior, @StockPosterior,
                N'VENTA', @IdVenta, @IdUsuario, N'Salida por venta confirmada.'
            );

            SET @StockAnterior = NULL;
            SET @StockReservado = NULL;
            SET @StockLote = NULL;

            FETCH NEXT FROM curVenta INTO @IdDetalle, @IdProducto, @IdLote, @Cantidad;
        END;

        CLOSE curVenta;
        DEALLOCATE curVenta;

        DECLARE @Subtotal DECIMAL(18,2);
        DECLARE @DescuentoTotal DECIMAL(18,2);
        DECLARE @ImpuestoTotal DECIMAL(18,2);
        DECLARE @CostoTotal DECIMAL(18,6);

        SELECT
            @Subtotal = CAST(ISNULL(SUM(Cantidad * PrecioUnitario), 0) AS DECIMAL(18,2)),
            @DescuentoTotal = CAST(ISNULL(SUM(DescuentoMonto), 0) AS DECIMAL(18,2)),
            @ImpuestoTotal = CAST(ISNULL(SUM(ImpuestoMonto), 0) AS DECIMAL(18,2)),
            @CostoTotal = CAST(ISNULL(SUM(Cantidad * CostoUnitario), 0) AS DECIMAL(18,6)),
            @Total = CAST(ISNULL(SUM(TotalLinea), 0) + @CargoEntrega AS DECIMAL(18,2))
        FROM ven.VentaDetalle
        WHERE IdVenta = @IdVenta;

        UPDATE ven.VentaPagos
        SET Estado = N'CONFIRMADO'
        WHERE IdVenta = @IdVenta
          AND Estado = N'REGISTRADO';

        IF @MontoEfectivo > 0
        BEGIN
            INSERT INTO ven.MovimientosCaja
            (
                IdTurno, Tipo, Monto, ReferenciaTipo, ReferenciaId, IdUsuario, Motivo
            )
            VALUES
            (
                @IdTurno, N'VENTA_EFECTIVO', @MontoEfectivo,
                N'VENTA', @IdVenta, @IdUsuario, N'Cobro en efectivo de venta.'
            );
        END;

        IF @MontoCredito > 0
        BEGIN
            DECLARE @DiasFinalCredito INT =
                ISNULL((SELECT DiasCredito FROM cli.Clientes WHERE IdCliente = @IdCliente),
                       ISNULL((SELECT TRY_CONVERT(INT, Valor) FROM sis.Configuracion WHERE Clave = N'Credito.DiasDefault'), 15));
            DECLARE @IntervaloAviso INT =
                ISNULL((SELECT IntervaloAvisoDias FROM cli.Clientes WHERE IdCliente = @IdCliente),
                       ISNULL((SELECT TRY_CONVERT(INT, Valor) FROM sis.Configuracion WHERE Clave = N'Credito.IntervaloAvisoDias'), 5));
            DECLARE @FechaEmision DATE = CONVERT(date, @FechaVenta);
            DECLARE @FechaVencimiento DATE = DATEADD(DAY, @DiasFinalCredito, CONVERT(date, @FechaVenta));

            INSERT INTO ven.CuentasPorCobrar
            (
                IdVenta, IdCliente, FechaEmision, FechaVencimiento,
                MontoOriginal, Saldo, Estado, ProximoAviso
            )
            VALUES
            (
                @IdVenta, @IdCliente, @FechaEmision, @FechaVencimiento,
                @MontoCredito, @MontoCredito, N'PENDIENTE',
                DATEADD(DAY, @IntervaloAviso, @FechaEmision)
            );
        END;

        UPDATE ven.Ventas
        SET Estado = N'CONFIRMADA',
            Subtotal = @Subtotal,
            DescuentoTotal = @DescuentoTotal,
            ImpuestoTotal = @ImpuestoTotal,
            Total = @Total,
            CostoTotal = @CostoTotal,
            SaldoPendiente = @MontoCredito
        WHERE IdVenta = @IdVenta;

        INSERT INTO sis.Bitacora
        (
            IdUsuario, Modulo, Accion, Entidad, EntidadId, Descripcion
        )
        VALUES
        (
            @IdUsuario, N'Ventas', N'CONFIRMAR_VENTA', N'Ventas', @IdVenta,
            CONCAT(N'Venta confirmada. Total: $', CONVERT(NVARCHAR(40), @Total))
        );

        IF @MaxDescuento > @UmbralJustificacion
        BEGIN
            INSERT INTO sis.Bitacora
            (
                IdUsuario, Modulo, Accion, Entidad, EntidadId, Descripcion
            )
            VALUES
            (
                @IdUsuario, N'Ventas', N'DESCUENTO_JUSTIFICADO', N'Ventas', @IdVenta,
                CONCAT(N'Descuento máximo: ', CONVERT(NVARCHAR(40), @MaxDescuento), N'%. Motivo: ', @MotivoDescuento)
            );
        END;

        COMMIT TRANSACTION;

        SELECT
            @IdVenta AS IdVenta,
            @Total AS Total,
            @MontoCredito AS SaldoPendiente,
            N'CONFIRMADA' AS Estado;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'curVenta') >= -1
        BEGIN
            IF CURSOR_STATUS('local', 'curVenta') > -1 CLOSE curVenta;
            DEALLOCATE curVenta;
        END;
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

