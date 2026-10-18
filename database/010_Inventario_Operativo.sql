/* DEV-03 / INV-01: extensión aditiva. Ejecutar después de 009 en la base destino.
   No modifica contratos 001–009. Sin triggers ni cambios destructivos. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
USE [GoDental];
GO
CREATE OR ALTER PROCEDURE inv.sp_RegistrarAjuste
    @ProductId int, @WarehouseId int, @Quantity decimal(18,4), @UnitCost decimal(18,6),
    @Type nvarchar(30), @Reason nvarchar(500), @ExpectedVersion varbinary(8),
    @LotId bigint = NULL, @NewLotNumber nvarchar(100) = NULL,
    @Manufactured date = NULL, @Expires date = NULL, @DestinationWarehouseId int = NULL,
    @UserId int, @WorkstationId int = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
            JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
            JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1 AND p.Codigo=N'INVENTARIO.AJUSTAR'
            WHERE u.IdUsuario=@UserId AND u.Activo=1 AND u.DebeCambiarClave=0)
            THROW 53001,N'El permiso para ajustar inventario ya no está disponible.',1;
        IF @Quantity IS NULL OR @Quantity=0 OR @Type IS NULL OR @Type NOT IN(N'AJUSTE',N'DAÑO',N'VENCIMIENTO',N'TRANSFERENCIA')
            OR (@Type<>N'AJUSTE' AND @Quantity>=0) OR NULLIF(LTRIM(RTRIM(@Reason)),N'') IS NULL
            OR (@Quantity>0 AND @UnitCost IS NULL) OR @UnitCost<0
            THROW 53002,N'Revisa cantidad, tipo, costo y motivo del movimiento.',1;
        IF (@Type=N'TRANSFERENCIA' AND (@DestinationWarehouseId IS NULL OR @DestinationWarehouseId=@WarehouseId))
            OR (@Type<>N'TRANSFERENCIA' AND @DestinationWarehouseId IS NOT NULL)
            THROW 53002,N'El destino sólo corresponde a una transferencia entre almacenes diferentes.',1;
        IF NOT EXISTS(SELECT 1 FROM inv.Almacenes WITH(HOLDLOCK) WHERE IdAlmacen=@WarehouseId AND Activo=1)
            OR (@DestinationWarehouseId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM inv.Almacenes WITH(HOLDLOCK) WHERE IdAlmacen=@DestinationWarehouseId AND Activo=1))
            THROW 53002,N'El almacén ya no está activo.',1;
        DECLARE @TracksLot bit, @TracksExpiry bit, @Serial bit, @Active bit;
        SELECT @TracksLot=ControlaLote,@TracksExpiry=ControlaVencimiento,@Serial=ControlaSerie,@Active=Activo
        FROM inv.Productos WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId;
        IF @Active IS NULL OR @Active=0 THROW 53002,N'El producto no está activo. Revisa su ficha antes de operar.',1;
        IF @Serial=1 THROW 53002,N'El control de series aún no admite movimientos desde este módulo.',1;
        SET @NewLotNumber=NULLIF(LTRIM(RTRIM(@NewLotNumber)),N'');
        IF @TracksLot=0 AND (@LotId IS NOT NULL OR @NewLotNumber IS NOT NULL OR @Expires IS NOT NULL OR @Manufactured IS NOT NULL)
            THROW 53002,N'Este producto no controla lotes.',1;
        IF @LotId IS NOT NULL AND (@NewLotNumber IS NOT NULL OR @Expires IS NOT NULL OR @Manufactured IS NOT NULL)
            THROW 53002,N'Un lote existente conserva sus fechas e identidad originales.',1;
        IF @TracksLot=1 AND @LotId IS NULL AND (@Quantity<=0 OR @NewLotNumber IS NULL)
            THROW 53002,N'Selecciona un lote o registra uno nuevo para la entrada.',1;
        DECLARE @Today date=CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');
        IF @TracksLot=1 AND @LotId IS NULL
        BEGIN
            IF (@TracksExpiry=1 AND @Expires IS NULL) OR @Manufactured>@Expires OR @Expires<@Today
                THROW 53002,N'Revisa fabricación y vencimiento. No se admite una entrada vencida.',1;
            IF EXISTS(SELECT 1 FROM inv.LotesProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId AND NumeroLote=@NewLotNumber)
                THROW 53002,N'El número de lote ya existe para este producto. Selecciona el lote registrado.',1;
            INSERT INTO inv.LotesProducto(IdProducto,NumeroLote,FechaFabricacion,FechaVencimiento,CostoUnitarioIngreso)
                VALUES(@ProductId,@NewLotNumber,@Manufactured,@Expires,@UnitCost);
            SET @LotId=SCOPE_IDENTITY();
        END;
        DECLARE @LotActive bit,@LotExpiry date;
        IF @LotId IS NOT NULL
        BEGIN
            SELECT @LotActive=Activo,@LotExpiry=FechaVencimiento FROM inv.LotesProducto WITH(UPDLOCK,HOLDLOCK)
                WHERE IdLote=@LotId AND IdProducto=@ProductId;
            IF @LotActive IS NULL THROW 53002,N'El lote no pertenece al producto.',1;
            IF @TracksExpiry=1 AND @LotExpiry IS NULL THROW 53002,N'El lote carece de vencimiento obligatorio.',1;
            IF (@Quantity>0 OR @Type=N'TRANSFERENCIA') AND (@LotActive=0 OR @LotExpiry<@Today)
                THROW 53002,N'El lote está bloqueado o vencido; sólo admite bajas justificadas.',1;
        END;
        IF @Type=N'VENCIMIENTO' AND (@LotExpiry IS NULL OR @LotExpiry>=@Today)
            THROW 53002,N'La baja por vencimiento requiere un lote ya vencido.',1;
        -- Bloqueo ordenado de ambos saldos. HOLDLOCK protege también la ausencia de filas.
        DECLARE @First int=CASE WHEN @DestinationWarehouseId<@WarehouseId THEN @DestinationWarehouseId ELSE @WarehouseId END;
        DECLARE @Second int=CASE WHEN @DestinationWarehouseId<@WarehouseId THEN @WarehouseId ELSE @DestinationWarehouseId END;
        DECLARE @LockValue decimal(18,4);
        SELECT @LockValue=StockActual FROM inv.ExistenciasProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdAlmacen=@First AND IdProducto=@ProductId;
        IF @Second IS NOT NULL SELECT @LockValue=StockActual FROM inv.ExistenciasProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdAlmacen=@Second AND IdProducto=@ProductId;
        DECLARE @Before decimal(18,4)=0,@Reserved decimal(18,4)=0,@Average decimal(18,6)=0,@Version varbinary(8);
        SELECT @Before=StockActual,@Reserved=StockReservado,@Average=CostoPromedio,@Version=RowVersion
            FROM inv.ExistenciasProducto WHERE IdAlmacen=@WarehouseId AND IdProducto=@ProductId;
        IF @ExpectedVersion IS NULL OR ISNULL(@Version,0x)<>@ExpectedVersion
            THROW 53003,N'Las existencias cambiaron. Actualiza y revisa antes de confirmar de nuevo.',1;
        IF @Before+@Quantity<@Reserved THROW 53004,N'No hay stock disponible suficiente; existen salidas o reservas.',1;
        IF @LotId IS NOT NULL
        BEGIN
            DECLARE @LotStock decimal(18,4)=0;
            SELECT @LotStock=StockActual FROM inv.ExistenciasLote WITH(UPDLOCK,HOLDLOCK) WHERE IdAlmacen=@WarehouseId AND IdLote=@LotId;
            IF @LotStock+@Quantity<0 THROW 53004,N'El lote no tiene stock suficiente en este almacén.',1;
            IF EXISTS(SELECT 1 FROM inv.ExistenciasLote WHERE IdAlmacen=@WarehouseId AND IdLote=@LotId)
                UPDATE inv.ExistenciasLote SET StockActual=StockActual+@Quantity WHERE IdAlmacen=@WarehouseId AND IdLote=@LotId;
            ELSE INSERT INTO inv.ExistenciasLote(IdAlmacen,IdLote,StockActual) VALUES(@WarehouseId,@LotId,@Quantity);
        END;
        IF @Quantity<0 SET @UnitCost=@Average;
        DECLARE @After decimal(18,4)=@Before+@Quantity;
        DECLARE @NewAverage decimal(18,6)=@Average;
        IF @Quantity>0 SET @NewAverage=CONVERT(decimal(18,6),
            (CONVERT(decimal(28,8),@Before)*@Average+CONVERT(decimal(28,8),@Quantity)*@UnitCost)/NULLIF(@After,0));
        IF @Version IS NULL
            INSERT INTO inv.ExistenciasProducto(IdAlmacen,IdProducto,StockActual,CostoPromedio,FechaUltimoMovimiento)
                VALUES(@WarehouseId,@ProductId,@After,@NewAverage,SYSDATETIMEOFFSET());
        ELSE UPDATE inv.ExistenciasProducto SET StockActual=@After,CostoPromedio=@NewAverage,FechaUltimoMovimiento=SYSDATETIMEOFFSET()
                WHERE IdAlmacen=@WarehouseId AND IdProducto=@ProductId;
        INSERT INTO inv.MovimientosInventario(IdAlmacen,IdProducto,IdLote,TipoMovimiento,Cantidad,CostoUnitario,ExistenciaAnterior,ExistenciaPosterior,IdUsuario,Observacion)
            VALUES(@WarehouseId,@ProductId,@LotId,@Type,@Quantity,@UnitCost,@Before,@After,@UserId,@Reason);
        DECLARE @MovementId bigint=SCOPE_IDENTITY();
        IF @DestinationWarehouseId IS NOT NULL
        BEGIN
            DECLARE @TargetStock decimal(18,4)=0,@TargetAverage decimal(18,6)=0;
            SELECT @TargetStock=StockActual,@TargetAverage=CostoPromedio FROM inv.ExistenciasProducto
                WHERE IdAlmacen=@DestinationWarehouseId AND IdProducto=@ProductId;
            DECLARE @TargetCost decimal(18,6)=CONVERT(decimal(18,6),
                (CONVERT(decimal(28,8),@TargetStock)*@TargetAverage-CONVERT(decimal(28,8),@Quantity)*@UnitCost)/NULLIF(@TargetStock-@Quantity,0));
            IF EXISTS(SELECT 1 FROM inv.ExistenciasProducto WHERE IdAlmacen=@DestinationWarehouseId AND IdProducto=@ProductId)
                UPDATE inv.ExistenciasProducto SET StockActual=StockActual-@Quantity,CostoPromedio=@TargetCost,FechaUltimoMovimiento=SYSDATETIMEOFFSET()
                WHERE IdAlmacen=@DestinationWarehouseId AND IdProducto=@ProductId;
            ELSE INSERT INTO inv.ExistenciasProducto(IdAlmacen,IdProducto,StockActual,CostoPromedio,FechaUltimoMovimiento)
                VALUES(@DestinationWarehouseId,@ProductId,-@Quantity,@TargetCost,SYSDATETIMEOFFSET());
            IF @LotId IS NOT NULL
            BEGIN
                IF EXISTS(SELECT 1 FROM inv.ExistenciasLote WITH(UPDLOCK,HOLDLOCK) WHERE IdAlmacen=@DestinationWarehouseId AND IdLote=@LotId)
                    UPDATE inv.ExistenciasLote SET StockActual=StockActual-@Quantity WHERE IdAlmacen=@DestinationWarehouseId AND IdLote=@LotId;
                ELSE INSERT INTO inv.ExistenciasLote(IdAlmacen,IdLote,StockActual) VALUES(@DestinationWarehouseId,@LotId,-@Quantity);
            END;
            INSERT INTO inv.MovimientosInventario(IdAlmacen,IdProducto,IdLote,TipoMovimiento,Cantidad,CostoUnitario,ExistenciaAnterior,ExistenciaPosterior,ReferenciaTipo,ReferenciaId,IdUsuario,Observacion)
                VALUES(@DestinationWarehouseId,@ProductId,@LotId,@Type,-@Quantity,@UnitCost,@TargetStock,@TargetStock-@Quantity,N'TRANSFERENCIA',@MovementId,@UserId,@Reason);
        END;
        UPDATE inv.MovimientosInventario SET ReferenciaTipo=N'MOVIMIENTO',ReferenciaId=@MovementId WHERE IdMovimiento=@MovementId;
        INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion)
            VALUES(@UserId,@WorkstationId,N'Inventario',@Type,N'MovimientosInventario',@MovementId,@Reason);
        COMMIT;
        SELECT @MovementId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE inv.sp_CambiarEstadoLote
    @LotId bigint,@ExpectedActive bit,@Active bit,@Reason nvarchar(500),@UserId int,@WorkstationId int=NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
            JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
            JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1 AND p.Codigo=N'INVENTARIO.AJUSTAR'
            WHERE u.IdUsuario=@UserId AND u.Activo=1 AND u.DebeCambiarClave=0)
            THROW 53001,N'No tienes permiso para cambiar el estado del lote.',1;
        IF NULLIF(LTRIM(RTRIM(@Reason)),N'') IS NULL THROW 53002,N'El motivo es obligatorio.',1;
        UPDATE inv.LotesProducto SET Activo=@Active WHERE IdLote=@LotId AND Activo=@ExpectedActive;
        IF @@ROWCOUNT<>1 THROW 53003,N'El estado del lote cambió. Actualiza antes de continuar.',1;
        INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion,DatosAntesJson,DatosDespuesJson)
            VALUES(@UserId,@WorkstationId,N'Inventario',N'ESTADO_LOTE',N'LotesProducto',@LotId,@Reason,
                CASE WHEN @ExpectedActive=1 THEN N'{"Activo":true}' ELSE N'{"Activo":false}' END,
                CASE WHEN @Active=1 THEN N'{"Activo":true}' ELSE N'{"Activo":false}' END);
        COMMIT;
        SELECT @LotId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
