/* DEV-40: seguimiento de cobranza y snapshots de abonos. Sin rellenar históricos. */
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'ven.AvisosCobranza',N'U') IS NULL
BEGIN
 CREATE TABLE ven.AvisosCobranza(
  IdAviso bigint IDENTITY PRIMARY KEY,
  IdCuenta bigint NOT NULL REFERENCES ven.CuentasPorCobrar(IdCuenta),
  IdOperacion uniqueidentifier NOT NULL UNIQUE,
  Fecha datetimeoffset(0) NOT NULL,
  FechaLocal date NOT NULL,
  IdUsuario int NOT NULL REFERENCES seg.Usuarios(IdUsuario),
  IdEquipo int NULL REFERENCES sis.EquiposTrabajo(IdEquipo),
  Saldo decimal(18,2) NOT NULL CHECK(Saldo>0),
  Vencimiento date NOT NULL,
  ProximoAviso date NOT NULL,
  Medio nvarchar(20) NOT NULL CHECK(Medio IN(N'INTERNO',N'LLAMADA',N'WHATSAPP',N'EMAIL',N'PRESENCIAL')),
  Resultado nvarchar(30) NOT NULL CHECK(Resultado IN(N'GESTION_INTERNA',N'CONTACTADO',N'SIN_RESPUESTA')),
  Observacion nvarchar(500) NOT NULL CHECK(LEN(LTRIM(RTRIM(Observacion)))>0),
  CONSTRAINT UQ_AvisosCobranza_CuentaFecha UNIQUE(IdCuenta,FechaLocal)
 );
 CREATE INDEX IX_AvisosCobranza_Cuenta ON ven.AvisosCobranza(IdCuenta,Fecha DESC,IdAviso DESC);
END;
GO
IF COL_LENGTH(N'ven.AbonosCliente',N'SaldoAntes') IS NULL
 ALTER TABLE ven.AbonosCliente ADD ClienteNombreSnapshot nvarchar(200) NULL,MetodoSnapshot nvarchar(80) NULL,
  UsuarioSnapshot nvarchar(150) NULL,SaldoAntes decimal(18,2) NULL,SaldoDespues decimal(18,2) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_AbonosCliente_SnapshotSaldo')
 ALTER TABLE ven.AbonosCliente ADD CONSTRAINT CK_AbonosCliente_SnapshotSaldo CHECK(
  (SaldoAntes IS NULL AND SaldoDespues IS NULL) OR
  (SaldoAntes IS NOT NULL AND SaldoDespues IS NOT NULL AND SaldoAntes>=0 AND SaldoDespues>=0 AND SaldoAntes-Monto=SaldoDespues));
GO
IF NOT EXISTS(SELECT 1 FROM seg.Permisos WHERE Codigo=N'CLIENTES.COBRANZA_GESTIONAR')
 INSERT INTO seg.Permisos(Codigo,Nombre,Modulo,Descripcion)
 VALUES(N'CLIENTES.COBRANZA_GESTIONAR',N'Registrar avisos de cobranza',N'Clientes',N'Registrar seguimiento interno de avisos; no envía mensajes ni modifica saldos.');
INSERT INTO seg.RolPermisos(IdRol,IdPermiso,Concedido)
 SELECT r.IdRol,p.IdPermiso,1 FROM seg.Roles r CROSS JOIN seg.Permisos p
 WHERE r.Nombre=N'Administrador' AND p.Codigo=N'CLIENTES.COBRANZA_GESTIONAR'
 AND NOT EXISTS(SELECT 1 FROM seg.RolPermisos rp WHERE rp.IdRol=r.IdRol AND rp.IdPermiso=p.IdPermiso);
GO
