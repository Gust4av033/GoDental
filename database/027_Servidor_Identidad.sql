/* DEV-51 / DB-01: identidad del servidor. Sin emisión ni secretos fiscales. */
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'com.SeguimientosProspecto',N'U') IS NULL
    THROW 53701,N'Falta aplicar 026_Online_Planificacion.sql.',1;
GO
IF OBJECT_ID(N'seg.SesionesServidor',N'U') IS NULL
BEGIN
    CREATE TABLE seg.SesionesServidor(
        IdSesion bigint IDENTITY NOT NULL CONSTRAINT PK_SesionesServidor PRIMARY KEY,
        TokenHash binary(32) NOT NULL CONSTRAINT UQ_SesionesServidor_Token UNIQUE,
        IdUsuario int NOT NULL, IdEquipo int NOT NULL, CredencialHash binary(32) NOT NULL,
        CreadaEn datetimeoffset(7) NOT NULL, UltimoUso datetimeoffset(7) NOT NULL,
        ExpiraEn datetimeoffset(7) NOT NULL, RevocadaEn datetimeoffset(7) NULL,
        CONSTRAINT FK_SesionesServidor_Usuario FOREIGN KEY(IdUsuario) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT FK_SesionesServidor_Equipo FOREIGN KEY(IdEquipo) REFERENCES sis.EquiposTrabajo(IdEquipo),
        CONSTRAINT CK_SesionesServidor_Fechas CHECK(ExpiraEn>CreadaEn AND UltimoUso>=CreadaEn
            AND (RevocadaEn IS NULL OR RevocadaEn>=CreadaEn)));
    CREATE INDEX IX_SesionesServidor_UsuarioEquipo ON seg.SesionesServidor(IdUsuario,IdEquipo,RevocadaEn);
END;
GO
CREATE OR ALTER PROCEDURE seg.ServidorLoginLeer @userName nvarchar(60)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PasswordHash,RowVersion FROM seg.Usuarios WHERE Usuario=@userName;
END;
GO
CREATE OR ALTER PROCEDURE seg.ServidorLoginFinalizar
    @userName nvarchar(60), @expectedVersion binary(8), @passwordVerified bit,
    @workstation nvarchar(120), @tokenHash binary(32)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @now datetimeoffset(7)=CONVERT(datetimeoffset(7),SYSUTCDATETIME());
    DECLARE @id int,@equipment int,@active bit,@change bit,@locked datetimeoffset(0),
        @version binary(8),@credential binary(32),@outcome int=0;
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @id=IdUsuario,@active=Activo,@change=DebeCambiarClave,@locked=BloqueadoHasta,
            @version=RowVersion,@credential=HASHBYTES('SHA2_256',CONVERT(varbinary(1024),PasswordHash))
        FROM seg.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE Usuario=@userName;

        IF @id IS NOT NULL AND @active=1 AND (@locked IS NULL OR @locked<=@now)
        BEGIN
            IF @passwordVerified=0
            BEGIN
                DECLARE @max int=ISNULL((SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WHERE Clave=N'Seguridad.MaxIntentos'),5);
                DECLARE @minutes int=ISNULL((SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WHERE Clave=N'Seguridad.BloqueoMinutos'),15);
                IF @max NOT BETWEEN 1 AND 100 SET @max=5;
                IF @minutes NOT BETWEEN 1 AND 1440 SET @minutes=15;
                UPDATE seg.Usuarios SET
                    IntentosFallidos=CASE WHEN BloqueadoHasta IS NOT NULL THEN 1 ELSE IntentosFallidos+1 END,
                    BloqueadoHasta=CASE WHEN (CASE WHEN BloqueadoHasta IS NOT NULL THEN 1 ELSE IntentosFallidos+1 END)>=@max
                        THEN DATEADD(minute,@minutes,@now) ELSE NULL END
                WHERE IdUsuario=@id;
            END
            ELSE IF @passwordVerified=1 AND @expectedVersion IS NOT NULL AND @expectedVersion=@version
            BEGIN
                SELECT @equipment=IdEquipo FROM sis.EquiposTrabajo WITH(UPDLOCK,HOLDLOCK)
                WHERE IdentificadorEquipo=@workstation AND Activo=1;
                IF @equipment IS NOT NULL
                BEGIN
                    IF @change=1 SET @outcome=2;
                    ELSE
                    BEGIN
                        UPDATE seg.SesionesServidor SET RevocadaEn=@now
                        WHERE IdUsuario=@id AND IdEquipo=@equipment AND RevocadaEn IS NULL;
                        INSERT INTO seg.SesionesServidor(TokenHash,IdUsuario,IdEquipo,CredencialHash,CreadaEn,UltimoUso,ExpiraEn)
                        VALUES(@tokenHash,@id,@equipment,@credential,@now,@now,DATEADD(hour,8,@now));
                        UPDATE seg.Usuarios SET IntentosFallidos=0,BloqueadoHasta=NULL,UltimoAcceso=@now WHERE IdUsuario=@id;
                        UPDATE sis.EquiposTrabajo SET UltimaConexion=@now WHERE IdEquipo=@equipment;
                        SET @outcome=1;
                    END;
                END;
            END;
        END;
        INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion)
        VALUES(@id,@equipment,N'Seguridad',CASE @outcome WHEN 1 THEN N'SERVIDOR_LOGIN_CORRECTO'
            WHEN 2 THEN N'SERVIDOR_CAMBIO_CLAVE_REQUERIDO' ELSE N'SERVIDOR_LOGIN_FALLIDO' END,
            N'Usuarios',@id,N'Autenticación del servidor; sin contenido de credenciales.');
        COMMIT;
        SELECT @outcome Outcome,DATEADD(hour,8,@now) ExpiresAt;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE seg.ServidorSesionValidar @tokenHash binary(32)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @now datetimeoffset(7)=CONVERT(datetimeoffset(7),SYSUTCDATETIME());
    DECLARE @id int,@equipment int,@credential binary(32),@valid bit=0,@session bigint;
    SELECT @id=IdUsuario,@equipment=IdEquipo FROM seg.SesionesServidor WHERE TokenHash=@tokenHash;
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @credential=CASE WHEN Activo=1 AND DebeCambiarClave=0
            AND (BloqueadoHasta IS NULL OR BloqueadoHasta<=@now)
            THEN HASHBYTES('SHA2_256',CONVERT(varbinary(1024),PasswordHash)) END
        FROM seg.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE IdUsuario=@id;
        IF @credential IS NOT NULL AND EXISTS(SELECT 1 FROM sis.EquiposTrabajo WITH(UPDLOCK,HOLDLOCK)
            WHERE IdEquipo=@equipment AND Activo=1)
        BEGIN
            SELECT @session=IdSesion FROM seg.SesionesServidor WITH(UPDLOCK,HOLDLOCK)
            WHERE TokenHash=@tokenHash AND IdUsuario=@id AND IdEquipo=@equipment
                AND CredencialHash=@credential AND RevocadaEn IS NULL AND ExpiraEn>@now
                AND DATEADD(minute,20,UltimoUso)>@now;
            IF @session IS NOT NULL
            BEGIN
                UPDATE seg.SesionesServidor SET UltimoUso=CASE WHEN UltimoUso<@now THEN @now ELSE UltimoUso END WHERE IdSesion=@session;
                SET @valid=1;
            END;
        END;
        IF @id IS NOT NULL AND @valid=0
        BEGIN
            UPDATE seg.SesionesServidor SET RevocadaEn=@now
            WHERE TokenHash=@tokenHash AND IdUsuario=@id AND RevocadaEn IS NULL;
            IF @@ROWCOUNT>0
                INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,Descripcion)
                VALUES(@id,@equipment,N'Seguridad',N'SERVIDOR_SESION_INVALIDADA',N'SesionesServidor',N'Sesión invalidada al comprobar estado o vencimiento.');
        END;
        SELECT u.IdUsuario UserId,u.Usuario UserName,u.NombreCompleto DisplayName,s.IdEquipo WorkstationId,s.ExpiraEn ExpiresAt
        FROM seg.Usuarios u JOIN seg.SesionesServidor s ON s.IdUsuario=u.IdUsuario
        WHERE @valid=1 AND s.IdSesion=@session;
        IF @valid=1
            SELECT DISTINCT p.Codigo FROM seg.UsuarioRoles ur
            JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1
            JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
            JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1
            WHERE ur.IdUsuario=@id ORDER BY p.Codigo;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE seg.ServidorSesionRevocar @tokenHash binary(32)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @id int,@equipment int,@locked int,@session bigint;
    DECLARE @now datetimeoffset(7)=CONVERT(datetimeoffset(7),SYSUTCDATETIME());
    SELECT @id=IdUsuario,@equipment=IdEquipo FROM seg.SesionesServidor WHERE TokenHash=@tokenHash;
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @locked=IdUsuario FROM seg.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE IdUsuario=@id;
        SELECT @session=IdSesion FROM seg.SesionesServidor WITH(UPDLOCK,HOLDLOCK)
        WHERE TokenHash=@tokenHash AND IdUsuario=@id AND RevocadaEn IS NULL;
        IF @session IS NOT NULL
        BEGIN
            UPDATE seg.SesionesServidor SET RevocadaEn=@now WHERE IdSesion=@session;
            INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion)
            VALUES(@id,@equipment,N'Seguridad',N'SERVIDOR_LOGOUT',N'SesionesServidor',@session,N'Sesión revocada por su titular.');
        END;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
IF DATABASE_PRINCIPAL_ID(N'GoDentalServerRuntime') IS NULL
    CREATE ROLE GoDentalServerRuntime AUTHORIZATION dbo;
GRANT EXECUTE ON OBJECT::seg.ServidorLoginLeer TO GoDentalServerRuntime;
GRANT EXECUTE ON OBJECT::seg.ServidorLoginFinalizar TO GoDentalServerRuntime;
GRANT EXECUTE ON OBJECT::seg.ServidorSesionValidar TO GoDentalServerRuntime;
GRANT EXECUTE ON OBJECT::seg.ServidorSesionRevocar TO GoDentalServerRuntime;
GO
IF OBJECT_ID(N'seg.SesionesServidor',N'U') IS NULL
    OR OBJECT_ID(N'seg.ServidorLoginLeer',N'P') IS NULL
    OR OBJECT_ID(N'seg.ServidorLoginFinalizar',N'P') IS NULL
    OR OBJECT_ID(N'seg.ServidorSesionValidar',N'P') IS NULL
    OR OBJECT_ID(N'seg.ServidorSesionRevocar',N'P') IS NULL
    THROW 53702,N'La migración 027 de identidad del servidor está incompleta.',1;
GO
