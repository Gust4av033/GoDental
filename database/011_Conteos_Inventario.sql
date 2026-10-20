/* DEV-09: extensión aditiva de DB-01; ejecutar después de 010.
   No cambia la baseline ni crea datos de operación. Reejecutable. */
USE [GoDental];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'inv.sp_RegistrarAjuste',N'P') IS NULL
    THROW 53102,N'Instala la migración 010 antes de los conteos.',1;
IF COL_LENGTH(N'inv.ConteosInventario',N'RowVersion') IS NULL
    ALTER TABLE inv.ConteosInventario ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'inv.ConteoInventarioDetalle',N'Contado') IS NULL
    ALTER TABLE inv.ConteoInventarioDetalle ADD Contado bit NOT NULL CONSTRAINT DF_ConteoDetalle_Contado DEFAULT(1) WITH VALUES;
IF COL_LENGTH(N'inv.ConteoInventarioDetalle',N'VersionExistencia') IS NULL
    ALTER TABLE inv.ConteoInventarioDetalle ADD VersionExistencia varbinary(8) NULL;
IF COL_LENGTH(N'inv.ConteoInventarioDetalle',N'VersionLote') IS NULL
    ALTER TABLE inv.ConteoInventarioDetalle ADD VersionLote varbinary(8) NULL;
GO
CREATE OR ALTER PROCEDURE inv.sp_AbrirConteo
    @WarehouseId int,@ProductsJson nvarchar(max),@Reason nvarchar(max),@UserId int,@WorkstationId int=NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF (SELECT COUNT(DISTINCT p.Codigo) FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
            JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
            JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1
            WHERE u.IdUsuario=@UserId AND u.Activo=1 AND u.DebeCambiarClave=0 AND p.Codigo IN(N'INVENTARIO.VER',N'INVENTARIO.CONTEO'))<>2
            THROW 53101,N'No tienes permiso para abrir conteos.',1;
        SET @Reason=LTRIM(RTRIM(@Reason));
        IF @Reason IS NULL OR LEN(@Reason)=0 OR DATALENGTH(@Reason)>1000
            THROW 53102,N'Escribe un motivo de hasta 500 caracteres.',1;
        IF @ProductsJson IS NULL OR ISJSON(@ProductsJson)<>1 OR LEFT(LTRIM(@ProductsJson),1)<>N'[' OR DATALENGTH(@ProductsJson)>40000
            THROW 53102,N'La lista de productos no es válida.',1;
        IF (SELECT COUNT(*) FROM OPENJSON(@ProductsJson)) NOT BETWEEN 1 AND 500
            OR EXISTS(SELECT 1 FROM OPENJSON(@ProductsJson) WHERE type<>2 OR TRY_CONVERT(int,value) IS NULL OR TRY_CONVERT(int,value)<=0)
            OR EXISTS(SELECT value FROM OPENJSON(@ProductsJson) GROUP BY TRY_CONVERT(int,value),value HAVING COUNT(*)>1)
            THROW 53102,N'Selecciona entre 1 y 500 productos diferentes.',1;
        DECLARE @Products TABLE(ProductId int PRIMARY KEY);
        IF (SELECT COUNT(*) FROM OPENJSON(@ProductsJson))<>(SELECT COUNT(DISTINCT TRY_CONVERT(int,value)) FROM OPENJSON(@ProductsJson))
            THROW 53102,N'La lista contiene productos repetidos.',1;
        INSERT INTO @Products SELECT CONVERT(int,value) FROM OPENJSON(@ProductsJson);
        -- Serializa aperturas/finalizaciones del mismo almacén, dentro de SQL Server.
        DECLARE @LockResult int,@Resource nvarchar(255)=CONCAT(N'GoDental.InventoryCount.',@WarehouseId);
        EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=5000;
        IF @LockResult<0 THROW 53103,N'Hay otro conteo operando en este almacén. Recarga y revisa.',1;
        IF NOT EXISTS(SELECT 1 FROM inv.Almacenes WITH(HOLDLOCK) WHERE IdAlmacen=@WarehouseId AND Activo=1)
            THROW 53102,N'El almacén no está activo.',1;
        DECLARE @CountId bigint,@ProductId int,@TracksLot bit,@Active bit,@Serial bit,@Stock decimal(18,4),@StockVersion varbinary(8);
        INSERT INTO inv.ConteosInventario(IdAlmacen,CreadoPor,Observaciones) VALUES(@WarehouseId,@UserId,@Reason);
        SET @CountId=SCOPE_IDENTITY();
        DECLARE products CURSOR LOCAL FAST_FORWARD FOR SELECT ProductId FROM @Products ORDER BY ProductId;
        OPEN products; FETCH NEXT FROM products INTO @ProductId;
        WHILE @@FETCH_STATUS=0
        BEGIN
            SET @Active=NULL; SET @Stock=NULL;
            SELECT @TracksLot=ControlaLote,@Serial=ControlaSerie,@Active=Activo FROM inv.Productos WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId;
            IF @Active IS NULL OR @Active=0 OR @Serial=1
                THROW 53102,N'El producto no está activo o requiere el control de series pendiente.',1;
            IF EXISTS(SELECT 1 FROM inv.ConteosInventario h JOIN inv.ConteoInventarioDetalle d ON d.IdConteo=h.IdConteo
                WHERE h.IdAlmacen=@WarehouseId AND h.Estado=N'ABIERTO' AND d.IdProducto=@ProductId)
                THROW 53105,N'El producto ya participa en un conteo abierto de este almacén.',1;
            SELECT @Stock=StockActual,@StockVersion=RowVersion FROM inv.ExistenciasProducto WITH(UPDLOCK,HOLDLOCK)
                WHERE IdAlmacen=@WarehouseId AND IdProducto=@ProductId;
            IF @Stock IS NULL THROW 53102,N'El conteo requiere un saldo previamente establecido; no es una carga de inventario inicial.',1;
            IF @TracksLot=1
            BEGIN
                IF NOT EXISTS(SELECT 1 FROM inv.ExistenciasLote el JOIN inv.LotesProducto l ON l.IdLote=el.IdLote
                    WHERE el.IdAlmacen=@WarehouseId AND l.IdProducto=@ProductId)
                    THROW 53102,N'El producto no tiene saldos de lotes para contar en este almacén.',1;
                INSERT INTO inv.ConteoInventarioDetalle(IdConteo,IdProducto,IdLote,CantidadSistema,CantidadContada,Diferencia,Contado,VersionExistencia,VersionLote)
                    SELECT @CountId,@ProductId,l.IdLote,el.StockActual,0,-el.StockActual,0,@StockVersion,el.RowVersion
                    FROM inv.LotesProducto l JOIN inv.ExistenciasLote el WITH(UPDLOCK,HOLDLOCK) ON el.IdLote=l.IdLote
                    WHERE el.IdAlmacen=@WarehouseId AND l.IdProducto=@ProductId;
                IF @Stock<>(SELECT SUM(CantidadSistema) FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId AND IdProducto=@ProductId)
                    THROW 53102,N'Los saldos de lotes no cuadran con el agregado. Requieren revisión antes del conteo.',1;
            END
            ELSE
                INSERT INTO inv.ConteoInventarioDetalle(IdConteo,IdProducto,CantidadSistema,CantidadContada,Diferencia,Contado,VersionExistencia)
                    VALUES(@CountId,@ProductId,@Stock,0,-@Stock,0,@StockVersion);
            FETCH NEXT FROM products INTO @ProductId;
        END;
        CLOSE products; DEALLOCATE products;
        IF (SELECT COUNT(*) FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId)>5000
            THROW 53102,N'El conteo excede 5000 líneas. Divide los productos en grupos menores.',1;
        INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion)
            VALUES(@UserId,@WorkstationId,N'Inventario',N'ABRIR_CONTEO',N'ConteosInventario',@CountId,@Reason);
        COMMIT; SELECT @CountId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE inv.sp_CapturarConteo
    @CountId bigint,@ExpectedVersion varbinary(max),@LinesJson nvarchar(max),@UserId int,@WorkstationId int=NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF (SELECT COUNT(DISTINCT p.Codigo) FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
            JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
            JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1
            WHERE u.IdUsuario=@UserId AND u.Activo=1 AND u.DebeCambiarClave=0 AND p.Codigo IN(N'INVENTARIO.VER',N'INVENTARIO.CONTEO'))<>2
            THROW 53101,N'No tienes permiso para capturar conteos.',1;
        IF @LinesJson IS NULL OR ISJSON(@LinesJson)<>1 OR LEFT(LTRIM(@LinesJson),1)<>N'[' OR DATALENGTH(@LinesJson)>300000
            THROW 53102,N'Las líneas del conteo no son válidas.',1;
        IF (SELECT COUNT(*) FROM OPENJSON(@LinesJson)) NOT BETWEEN 1 AND 1000 OR EXISTS(SELECT 1 FROM OPENJSON(@LinesJson) WHERE type<>5)
            THROW 53102,N'Captura entre 1 y 1000 líneas.',1;
        DECLARE @Raw TABLE(LineId nvarchar(100),Quantity nvarchar(200));
        INSERT INTO @Raw SELECT LineId,Quantity FROM OPENJSON(@LinesJson) WITH(LineId nvarchar(100) '$.LineId',Quantity nvarchar(200) '$.Quantity');
        IF EXISTS(SELECT 1 FROM @Raw WHERE TRY_CONVERT(bigint,LineId) IS NULL OR TRY_CONVERT(bigint,LineId)<=0
            OR TRY_CONVERT(decimal(38,18),Quantity) IS NULL OR TRY_CONVERT(decimal(18,4),Quantity) IS NULL
            OR TRY_CONVERT(decimal(38,18),Quantity)<0 OR TRY_CONVERT(decimal(38,18),Quantity)<>TRY_CONVERT(decimal(18,4),Quantity)
            OR (CHARINDEX(N'.',Quantity)>0 AND SUBSTRING(Quantity,CHARINDEX(N'.',Quantity)+5,200) LIKE N'%[^0]%'))
            OR EXISTS(SELECT 1 FROM @Raw GROUP BY TRY_CONVERT(bigint,LineId) HAVING COUNT(*)>1)
            THROW 53102,N'Revisa las líneas y las cantidades: no negativas, sin duplicados y hasta cuatro decimales.',1;
        DECLARE @WarehouseId int,@LockResult int,@Resource nvarchar(255),@State nvarchar(20),@Version varbinary(8);
        SELECT @WarehouseId=IdAlmacen FROM inv.ConteosInventario WHERE IdConteo=@CountId;
        SET @Resource=CONCAT(N'GoDental.InventoryCount.',@WarehouseId);
        EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=5000;
        IF @LockResult<0 THROW 53103,N'Otra operación utiliza este conteo. Recarga y revisa.',1;
        SELECT @State=Estado,@Version=RowVersion FROM inv.ConteosInventario WITH(UPDLOCK,HOLDLOCK) WHERE IdConteo=@CountId;
        IF @State IS NULL OR @State<>N'ABIERTO' THROW 53105,N'El conteo no existe o ya no está abierto.',1;
        IF @ExpectedVersion IS NULL OR DATALENGTH(@ExpectedVersion)<>8 OR @Version<>@ExpectedVersion
            THROW 53103,N'El conteo cambió. Recarga antes de guardar.',1;
        IF EXISTS(SELECT 1 FROM @Raw r WHERE NOT EXISTS(SELECT 1 FROM inv.ConteoInventarioDetalle d WHERE d.IdConteo=@CountId AND d.IdDetalle=CONVERT(bigint,r.LineId)))
            THROW 53102,N'Una línea no pertenece a este conteo.',1;
        DECLARE @Before nvarchar(max)=(SELECT d.IdDetalle,d.CantidadContada,d.Contado FROM inv.ConteoInventarioDetalle d
            JOIN @Raw r ON CONVERT(bigint,r.LineId)=d.IdDetalle WHERE d.IdConteo=@CountId ORDER BY d.IdDetalle FOR JSON PATH);
        UPDATE d SET CantidadContada=CONVERT(decimal(18,4),r.Quantity),Diferencia=CONVERT(decimal(18,4),r.Quantity)-d.CantidadSistema,Contado=1
            FROM inv.ConteoInventarioDetalle d JOIN @Raw r ON CONVERT(bigint,r.LineId)=d.IdDetalle WHERE d.IdConteo=@CountId;
        UPDATE inv.ConteosInventario SET Observaciones=Observaciones WHERE IdConteo=@CountId; -- Avanza RowVersion de toda la captura.
        INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion,DatosAntesJson,DatosDespuesJson)
            VALUES(@UserId,@WorkstationId,N'Inventario',N'CAPTURAR_CONTEO',N'ConteosInventario',@CountId,N'Captura de cantidades físicas.',@Before,@LinesJson);
        COMMIT; SELECT @CountId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE inv.sp_FinalizarConteo
    @CountId bigint,@ExpectedVersion varbinary(max),@Reason nvarchar(max),@Cancel bit,@UserId int,@WorkstationId int=NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Permissions TABLE(Code nvarchar(100));
        INSERT INTO @Permissions SELECT DISTINCT p.Codigo FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
            JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
            JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1
            WHERE u.IdUsuario=@UserId AND u.Activo=1 AND u.DebeCambiarClave=0;
        IF NOT EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'INVENTARIO.VER') OR NOT EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'INVENTARIO.CONTEO')
            OR (@Cancel=0 AND NOT EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'INVENTARIO.AJUSTAR'))
            THROW 53101,N'No tienes permiso para finalizar este conteo.',1;
        SET @Reason=LTRIM(RTRIM(@Reason));
        IF @Cancel IS NULL OR @Reason IS NULL OR LEN(@Reason)=0 OR DATALENGTH(@Reason)>1000
            THROW 53102,N'Escribe un motivo de hasta 500 caracteres y selecciona la operación.',1;
        DECLARE @WarehouseId int,@State nvarchar(20),@Version varbinary(8),@LockResult int,@Resource nvarchar(255);
        SELECT @WarehouseId=IdAlmacen FROM inv.ConteosInventario WHERE IdConteo=@CountId;
        SET @Resource=CONCAT(N'GoDental.InventoryCount.',@WarehouseId);
        EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=5000;
        IF @LockResult<0 THROW 53103,N'Otra operación utiliza este conteo. Recarga y revisa.',1;
        SELECT @State=Estado,@Version=RowVersion FROM inv.ConteosInventario WITH(UPDLOCK,HOLDLOCK) WHERE IdConteo=@CountId;
        IF @State IS NULL OR @State<>N'ABIERTO' THROW 53105,N'El conteo no existe o ya está finalizado. Consulta su estado.',1;
        IF @ExpectedVersion IS NULL OR DATALENGTH(@ExpectedVersion)<>8 OR @Version<>@ExpectedVersion
            THROW 53103,N'El conteo cambió. Recarga antes de finalizar.',1;
        IF @Cancel=0
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM inv.Almacenes WITH(HOLDLOCK) WHERE IdAlmacen=@WarehouseId AND Activo=1)
                THROW 53102,N'El almacén ya no está activo.',1;
            IF NOT EXISTS(SELECT 1 FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId)
                OR EXISTS(SELECT 1 FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId AND (Contado=0 OR AjusteGenerado=1))
                THROW 53102,N'Completa todas las líneas antes de cerrar; no se permiten ajustes ya generados.',1;
            DECLARE @ProductId int,@Active bit,@Serial bit,@TracksLot bit,@Stock decimal(18,4),@Reserved decimal(18,4),@StockVersion varbinary(8),@TotalDifference decimal(28,4);
            DECLARE products CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT IdProducto FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId ORDER BY IdProducto;
            OPEN products; FETCH NEXT FROM products INTO @ProductId;
            WHILE @@FETCH_STATUS=0
            BEGIN
                SET @Active=NULL; SET @Stock=NULL;
                SELECT @Active=Activo,@Serial=ControlaSerie,@TracksLot=ControlaLote FROM inv.Productos WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId;
                IF @Active IS NULL OR @Active=0 OR @Serial=1 THROW 53102,N'Un producto cambió de estado o requiere series. Revisa el conteo.',1;
                SELECT @Stock=StockActual,@Reserved=StockReservado,@StockVersion=RowVersion FROM inv.ExistenciasProducto WITH(UPDLOCK,HOLDLOCK)
                    WHERE IdAlmacen=@WarehouseId AND IdProducto=@ProductId;
                IF @Stock IS NULL OR EXISTS(SELECT 1 FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId AND IdProducto=@ProductId
                    AND (VersionExistencia IS NULL OR VersionExistencia<>@StockVersion))
                    THROW 53103,N'Hubo movimientos o reservas desde la apertura. Cancela y abre un conteo nuevo tras revisar.',1;
                IF EXISTS(SELECT 1 FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId AND IdProducto=@ProductId
                    AND ((@TracksLot=1 AND IdLote IS NULL) OR (@TracksLot=0 AND IdLote IS NOT NULL)))
                    THROW 53102,N'Cambió el control de lotes del producto.',1;
                SELECT @TotalDifference=SUM(CONVERT(decimal(38,4),Diferencia)) FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId AND IdProducto=@ProductId;
                IF @Stock+@TotalDifference<@Reserved THROW 53104,N'El físico contado no puede quedar por debajo del stock reservado.',1;
                IF @Stock+@TotalDifference>99999999999999.9999 THROW 53102,N'El saldo contado excede la precisión admitida.',1;
                IF @Stock+(SELECT CONVERT(decimal(28,4),SUM(CASE WHEN Diferencia>0 THEN CONVERT(decimal(28,4),Diferencia) ELSE 0 END))
                    FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId AND IdProducto=@ProductId)>99999999999999.9999
                    THROW 53102,N'Las diferencias exceden el límite de saldo durante la compensación. Requieren revisión.',1;
                FETCH NEXT FROM products INTO @ProductId;
            END;
            CLOSE products; DEALLOCATE products;
            DECLARE @LotId bigint,@LineId bigint,@Difference decimal(18,4),@LotActive bit,@Expiry date,@LotStock decimal(18,4),@LotVersion varbinary(8),@ExpectedLotVersion varbinary(8),@SystemQuantity decimal(18,4);
            DECLARE @Today date=CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');
            DECLARE lots CURSOR LOCAL FAST_FORWARD FOR SELECT IdDetalle,IdProducto,IdLote,Diferencia,VersionLote,CantidadSistema
                FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId AND IdLote IS NOT NULL ORDER BY IdProducto,IdLote;
            OPEN lots; FETCH NEXT FROM lots INTO @LineId,@ProductId,@LotId,@Difference,@ExpectedLotVersion,@SystemQuantity;
            WHILE @@FETCH_STATUS=0
            BEGIN
                SET @LotActive=NULL; SET @LotStock=NULL;
                SELECT @LotActive=Activo,@Expiry=FechaVencimiento FROM inv.LotesProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdLote=@LotId AND IdProducto=@ProductId;
                SELECT @LotStock=StockActual,@LotVersion=RowVersion FROM inv.ExistenciasLote WITH(UPDLOCK,HOLDLOCK) WHERE IdAlmacen=@WarehouseId AND IdLote=@LotId;
                IF @LotActive IS NULL OR @LotStock IS NULL OR @ExpectedLotVersion IS NULL OR @LotVersion<>@ExpectedLotVersion OR @LotStock<>@SystemQuantity
                    THROW 53103,N'Un lote cambió desde la apertura. Revisa y abre un conteo nuevo.',1;
                IF @Difference>0 AND (@LotActive=0 OR @Expiry<@Today)
                    THROW 53102,N'No se permiten diferencias positivas sobre lotes bloqueados o vencidos.',1;
                FETCH NEXT FROM lots INTO @LineId,@ProductId,@LotId,@Difference,@ExpectedLotVersion,@SystemQuantity;
            END;
            CLOSE lots; DEALLOCATE lots;
            -- Positivos primero: el saldo intermedio nunca invade las reservas si el saldo final es válido.
            DECLARE @Before decimal(18,4),@Average decimal(18,6);
            DECLARE movements CURSOR LOCAL FAST_FORWARD FOR SELECT IdDetalle,IdProducto,IdLote,Diferencia
                FROM inv.ConteoInventarioDetalle WHERE IdConteo=@CountId AND Diferencia<>0 ORDER BY IdProducto,Diferencia DESC,IdDetalle;
            OPEN movements; FETCH NEXT FROM movements INTO @LineId,@ProductId,@LotId,@Difference;
            WHILE @@FETCH_STATUS=0
            BEGIN
                SELECT @Before=StockActual,@Average=CostoPromedio FROM inv.ExistenciasProducto WHERE IdAlmacen=@WarehouseId AND IdProducto=@ProductId;
                UPDATE inv.ExistenciasProducto SET StockActual=StockActual+@Difference,FechaUltimoMovimiento=SYSDATETIMEOFFSET()
                    WHERE IdAlmacen=@WarehouseId AND IdProducto=@ProductId;
                IF @LotId IS NOT NULL UPDATE inv.ExistenciasLote SET StockActual=StockActual+@Difference WHERE IdAlmacen=@WarehouseId AND IdLote=@LotId;
                INSERT INTO inv.MovimientosInventario(IdAlmacen,IdProducto,IdLote,TipoMovimiento,Cantidad,CostoUnitario,ExistenciaAnterior,ExistenciaPosterior,ReferenciaTipo,ReferenciaId,IdUsuario,Observacion)
                    VALUES(@WarehouseId,@ProductId,@LotId,N'AJUSTE',@Difference,@Average,@Before,@Before+@Difference,N'CONTEO',@CountId,@UserId,@Reason);
                UPDATE inv.ConteoInventarioDetalle SET AjusteGenerado=1 WHERE IdDetalle=@LineId;
                FETCH NEXT FROM movements INTO @LineId,@ProductId,@LotId,@Difference;
            END;
            CLOSE movements; DEALLOCATE movements;
        END;
        UPDATE inv.ConteosInventario SET Estado=CASE WHEN @Cancel=1 THEN N'CANCELADO' ELSE N'CERRADO' END,
            FechaCierre=SYSDATETIMEOFFSET(),CerradoPor=@UserId WHERE IdConteo=@CountId;
        INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion)
            VALUES(@UserId,@WorkstationId,N'Inventario',CASE WHEN @Cancel=1 THEN N'CANCELAR_CONTEO' ELSE N'CERRAR_CONTEO' END,N'ConteosInventario',@CountId,@Reason);
        COMMIT; SELECT @CountId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
