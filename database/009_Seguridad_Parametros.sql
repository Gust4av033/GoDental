/* GoDental 0.2: parámetros iniciales SEC-01. No reemplaza valores existentes. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
USE [GoDental];
GO
INSERT INTO sis.Configuracion(Clave,Valor,TipoDato,Descripcion,EditableUI)
SELECT v.Clave,v.Valor,N'INT',v.Descripcion,1
FROM (VALUES
 (N'Seguridad.MaxIntentos',N'5',N'Intentos consecutivos antes del bloqueo temporal.'),
 (N'Seguridad.BloqueoMinutos',N'15',N'Duración inicial del bloqueo temporal de una cuenta.')
) v(Clave,Valor,Descripcion)
WHERE NOT EXISTS(SELECT 1 FROM sis.Configuracion c WITH(UPDLOCK,HOLDLOCK) WHERE c.Clave=v.Clave);
GO
