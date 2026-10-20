# Base de datos GoDental — baseline SQL v0.1

Esta carpeta contiene la baseline ejecutable de SQL Server alineada con **DB-01 v0.1** y con las decisiones funcionales vigentes del proyecto.

## Orden obligatorio

Ejecutar los scripts en una base nueva exactamente en este orden:

1. `001_Core.sql`
2. `002_Inventario.sql`
3. `003_Compras.sql`
4. `004_ClientesVentas.sql`
5. `005_Fiscal_Base.sql`
6. `006_Comercial_Base.sql`
7. `007_Seed.sql`
8. `008_ViewsProcedures.sql`
9. `009_Seguridad_Parametros.sql`
10. `010_Inventario_Operativo.sql`
11. `011_Conteos_Inventario.sql`

Los scripts incluyen validaciones de dependencias y finalización para detectar instalaciones parciales.

## Contenido de la baseline

- `001`–`006`: **72 tablas**, coincidentes con el diccionario DB-01 v0.1.
- `007`: roles, permisos, catálogos, configuración, almacén/caja inicial y secuencias. Es idempotente y **no crea un Administrador con contraseña por defecto**.
- Al repetir `007`, los valores de `sis.Configuracion` marcados `EditableUI=1` conservan su valor, usuario y fecha de modificación. Los parámetros no editables siguen la semilla normativa. Esto evita perder cambios comerciales al resembrar.
- `008`: **8 vistas** y **12 procedimientos almacenados** para consultas y operaciones transaccionales/concurrentes.
- No se utilizan triggers.
- Se emplean PK/FK/CHECK, índices, `ROWVERSION` y bloqueos/transacciones explícitos donde corresponde.

## Precedencia comercial

VEN-01A cerró la regla que anteriormente estaba pendiente. La configuración `Precios.PrecedenciaDefinida` se siembra en `true` y la regla normativa es:

**lista base → condición/tipo de cliente → comparación con promoción → mejor precio válido → descuento manual autorizado**.

Las promociones no son acumulables por defecto (`Acumulable = 0`).

## Instalación local

Puede ejecutarse la secuencia con `scripts/install-database.ps1` cuando `sqlcmd` esté disponible, o manualmente desde SQL Server Management Studio respetando el orden 001–011.

La baseline 001→008 se ejecutó el 17/10/2026 en SQL Server 2022 local, dentro de una base separada de desarrollo: 72 tablas, 8 vistas y 12 procedimientos. Se añadieron pruebas reales de autenticación, catálogo, permisos, concurrencia y rollback; consultar `docs/DEV-02-Seguridad-Catalogo.md`. Esto no certifica todavía ventas, recepciones, fiscalidad ni operación multi-PC completa.

## Migración 009

`009_Seguridad_Parametros.sql` se ejecuta después de la baseline y agrega los parámetros iniciales de 5 intentos fallidos y 15 minutos de bloqueo. No reemplaza valores ya configurados y no crea usuarios ni claves por defecto.

La herramienta `tests/GoDental.DatabaseTests --install-development` crea sólo `localhost/GoDental_Development`, con marcador de desarrollo. Conserva la baseline y reemplaza su destino en memoria, incluyendo el `USE GoDental` sin corchetes de 005. Sobre una base existente marcada y completa ejecuta sólo migraciones posteriores a 008; no vuelve a sembrar ni resetear reglas.

## Migración 010

`010_Inventario_Operativo.sql` agrega `inv.sp_RegistrarAjuste` e `inv.sp_CambiarEstadoLote`: ajustes, bajas y transferencias, más bloqueo/reactivación de lotes. Incluye `USE [GoDental]`, igual que la baseline; al desplegar manualmente sobre otro nombre debe ajustarse el destino. El instalador de desarrollo adapta ese destino en memoria. No modifica datos existentes al instalarse y eleva el total a 14 procedimientos. Consultar decisiones y validaciones en [DEV-03](../docs/DEV-03-Inventario-operativo.md).

## Evolución futura

`011_Conteos_Inventario.sql` añade versión de cabecera, indicador de captura y fotografías de versiones a las tablas de conteos existentes. Agrega tres procedimientos para apertura, captura y cierre/cancelación transaccionales (17 procedimientos en total; se conservan 72 tablas y 8 vistas). No modifica saldos al instalarse. La lógica C# está lista para conectar desde WPF, según [DEV-09](../docs/DEV-09-Conteos.md).

Una vez que esta baseline haya sido aplicada sobre una base compartida o productiva, no se deben reescribir scripts históricos para introducir cambios de esquema. Las modificaciones posteriores deberán versionarse mediante nuevos scripts/migraciones incrementales y quedar documentadas.
