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
12. `012_MVP_Operativo.sql`
13. `013_Pedidos_MVP.sql`
14. `014_Codigos_Automaticos.sql`
15. `015_Resolucion_Operaciones.sql`
16. `016_Parametros_Comerciales.sql`
17. `017_Compras_Recepciones.sql`

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

Puede ejecutarse la secuencia con `scripts/install-database.ps1` cuando `sqlcmd` esté disponible, o manualmente desde SQL Server Management Studio respetando el orden 001–014.

La baseline 001→008 se ejecutó el 17/10/2026 en SQL Server 2022 local, dentro de una base separada de desarrollo: 72 tablas, 8 vistas y 12 procedimientos. Se añadieron pruebas reales de autenticación, catálogo, permisos, concurrencia y rollback; consultar `docs/DEV-02-Seguridad-Catalogo.md`. Esto no certifica todavía ventas, recepciones, fiscalidad ni operación multi-PC completa.

## Migración 009

`009_Seguridad_Parametros.sql` se ejecuta después de la baseline y agrega los parámetros iniciales de 5 intentos fallidos y 15 minutos de bloqueo. No reemplaza valores ya configurados y no crea usuarios ni claves por defecto.

La herramienta `tests/GoDental.DatabaseTests --install-development` crea sólo `localhost/GoDental_Development`, con marcador de desarrollo. Conserva la baseline y reemplaza su destino en memoria, incluyendo el `USE GoDental` sin corchetes de 005. Sobre una base existente marcada y completa ejecuta sólo migraciones posteriores a 008; no vuelve a sembrar ni resetear reglas.

## Migración 010

`010_Inventario_Operativo.sql` agrega `inv.sp_RegistrarAjuste` e `inv.sp_CambiarEstadoLote`: ajustes, bajas y transferencias, más bloqueo/reactivación de lotes. Incluye `USE [GoDental]`, igual que la baseline; al desplegar manualmente sobre otro nombre debe ajustarse el destino. El instalador de desarrollo adapta ese destino en memoria. No modifica datos existentes al instalarse y eleva el total a 14 procedimientos. Consultar decisiones y validaciones en [DEV-03](../docs/DEV-03-Inventario-operativo.md).

## Migraciones 011–013

`011_Conteos_Inventario.sql` añade versión de cabecera, indicador de captura y fotografías de versiones a las tablas de conteos existentes. Agrega tres procedimientos para apertura, captura y cierre/cancelación transaccionales (17 procedimientos en total; se conservan 72 tablas y 8 vistas). No modifica saldos al instalarse. La lógica C# está lista para conectar desde WPF, según [DEV-09](../docs/DEV-09-Conteos.md).

`012_MVP_Operativo.sql` agrega registro transaccional de operaciones por GUID, versiones de turnos/CxC/cotizaciones y snapshots de precio/cliente, entrega, efectivo/cambio y autorización de un uso. Actualiza `sp_ConfirmarVenta` para cargo de entrega, pagos exactos y exclusión de lotes vencidos. Si ya existen varios turnos abiertos por caja o ventas duplicadas por cotización se detiene con error; requiere revisar el historial antes de continuar.

`013_Pedidos_MVP.sql` agrega pedidos con dirección fotografiada, líneas y eventos. Total con ambas extensiones: **76 tablas, 8 vistas y 17 procedimientos**. Modelo/decisiones de estas cuatro tablas adicionales: [DEV-17](../docs/DEV-17-MVP-Logica.md). No cambia las 72 tablas documentadas en la baseline histórica de DB-01.

Las dos nuevas migraciones se verificaron en CI Windows/LocalDB: 102 comprobaciones de `--run-mvp`, incluida reaplicación conservando historial; ver DEV-17. La suite está incorporada a CI y a la verificación local. La base del usuario y el recorrido con las nuevas pantallas/dos estaciones mantienen su validación separada.

Una vez que esta baseline haya sido aplicada sobre una base compartida o productiva, no se deben reescribir scripts históricos para introducir cambios de esquema. Las modificaciones posteriores deberán versionarse mediante nuevos scripts/migraciones incrementales y quedar documentadas.

## Migración 014 · Códigos automáticos

`014_Codigos_Automaticos.sql` agrega códigos automáticos de clientes, productos, proveedores y cajas, y `CodigoEmpleado` derivado del ID de las cuentas del personal. El procedimiento específico usa los correlativos existentes dentro de la transacción del alta y evita colisiones históricas. No renumera registros previos ni modifica accesos/contraseñas. Total vigente: **76 tablas, 8 vistas y 18 procedimientos**. Contratos, formato y validación: [DEV-20](../docs/DEV-20-Codigos-Cierre.md).

La suite aislada `--run-codes` comprueba concurrencia, colisiones, auditoría, rollback, agotamiento y reaplicación. El instalador de desarrollo incorpora automáticamente 014 al ejecutar `--install-development` sobre una base local marcada; no actualiza bases sin ese marcador.

## Migración 015 · resolución de operaciones

Aplicar después de 014 y actualizar todas las estaciones antes de volver a operar. Agrega EstadoResolucion y HashRecuperacion al ledger sis.OperacionesMvp, conservando recibos confirmados anteriores. Los cierres RECHAZADA_DEFINITIVA usan EntidadId=0 y nunca se devuelven como recibos. Las escrituras actuales consultan el cierre bajo el mismo applock; una versión anterior de la app no conoce ese estado y no debe seguir operando tras la actualización. No agrega tablas/vistas/procedimientos: siguen **76/8/18**. Diseño y pruebas en DEV-31. La herramienta de desarrollo incluye 015; no instala sobre una base no marcada.

## Parámetros comerciales · 016

DEV-32 agrega `RowVersion` a `sis.Configuracion`; conserva las 76 tablas, 8 vistas y 18 procedimientos. Cambia únicamente etiquetas de los tres permisos de descuento para describir sus tramos configurables, conservando códigos y concesiones. Aplicar después de 015 y actualizar todas las estaciones. La pestaña Comercial requiere `AJUSTES.EDITAR`; no ejecutar de nuevo 001–008 sobre una base con datos para actualizarla.

## Compras y recepciones · 017

DEV-33 amplía el estado de OC con RECIBIDA_PARCIAL y agrega snapshots SKU/nombre al detalle de OC, versión/cambio real/costo completo a la recepción y fabricación a sus líneas. La migración conserva datos y deriva el estado de OC históricas con cantidades recibidas; una reejecución no cambia versiones ya coherentes. Recepciones históricas aplicadas permanecen aplicadas y no se presume confirmado un costo sin evidencia. Total: 76 tablas, 8 vistas, 18 procedimientos. La ruta nueva usa transacciones Dapper y el ledger; no invoca el procedimiento histórico de recepción sin sus validaciones adicionales.

## Importaciones · 018

DEV-37 añade cierre de costos, presupuestos y gastos congelados por valor neto antes de recibir;
parciales consumen su proporción y remanente exacto. Conserva historia y 76/8/18 objetos.

## Posventa · 019

`019_Posventa.sql`, definido en DEV-38, agrega trazabilidad/versionado de devoluciones y su
compensación de deuda/efectivo, detalle→movimiento, raíz central de fotos y conservación de la
entrega histórica cuando un pedido termina cancelado por posventa. Es aditivo e idempotente;
no calcula reversos para filas antiguas ni mueve stock/caja al instalarse. Conserva **76/8/18**.
Aplicar después de 018, con respaldo y todas las estaciones cerradas/actualizadas. El instalador
de desarrollo incluye 019 para bases marcadas; no sustituye el migrador productivo de DEV-36.
El respaldo SQL **no incluye fotos**: respaldar también la carpeta central y probar restauración conjunta.

## Localización de formularios · 020

`020_Distritos_Formularios.sql` (DEV-39) añade `Distrito NVARCHAR(100) NULL` a
`cli.ClienteDirecciones` y `ven.PedidosMvp`. El pedido conserva el distrito como snapshot de
la dirección seleccionada. No convierte municipios históricos, no rellena datos ni cambia
contratos JSON recuperables; 76 tablas / 8 vistas / 18 procedimientos. Reaplicable.
Aplicar después de 019, con respaldo y ambas estaciones cerradas/actualizadas.
El catálogo territorial se distribuye con la aplicación, sin tablas fiscales nuevas.

## Cobranza y documentos · 021

`021_Cobranza_Documentos.sql` (DEV-40) añade historial inmutable de avisos `ven.AvisosCobranza`,
con cuenta/fecha/usuario/estación/saldo/gestión y unicidad por cuenta/día, y snapshots opcionales de
nombres y saldo antes/después en abonos nuevos. Históricos siguen NULL, sin reconstruir datos.
Añade CLIENTES.COBRANZA_GESTIONAR y concesión Administrador sólo cuando falta. **77/8/18**.
Aplicar después de 020 y actualizar/reiniciar ambas estaciones. Se conserva el JSON de solicitudes
recuperables anteriores; la nueva solicitud AVISO_COBRANZA usa el mismo diario/ledger transaccional.
