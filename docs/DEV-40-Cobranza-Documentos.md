# DEV-40 — 2.2B: cobranza y documentos de crédito

**Fecha local:** 22/10/2026 (El Salvador). **Estado:** implementado y validado automáticamente;
aceptación en estaciones/impresora y aplicación de 021 a la BD personal pendientes.
Continúa DEV-38/39; fuentes DM-01 §12.9–12.10, DB-01 CxC/abonos, VEN-01, SEC-01 y ARQ-01.

## Alcance y reglas

- Crédito muestra avisos por atender y permite registrar una gestión interna o contacto realizado
  externamente, con medio, resultado, observación, saldo/vencimiento leídos, usuario y estación.
  La aplicación no contacta ni envía mensajes a terceros, ni opera con WPF cerrado.
- Un aviso se registra sólo si hay saldo cobrable y `ProximoAviso` ya llegó. Para históricos sin fecha,
  se usa emisión + intervalo del cliente/configuración (5 días por defecto), sin rellenar historia.
  SQL usa fecha de El Salvador, también al reprogramar por abonos nuevos. Al registrar avanza último/próximo aviso desde hoy y la versión.
  No duplica por cuenta/día ni por reenvío de GUID; dos estaciones deben recargar tras un cambio.
- Nuevo permiso `CLIENTES.COBRANZA_GESTIONAR`, concedido inicialmente sólo al Administrador;
  conservar denegaciones/concesiones existentes. Consultas exigen CLIENTES.VER + VENTAS.VER;
  documentos además VENTAS.REIMPRIMIR, revalidados en SQL y tras renderizar/cambio de sesión.
- Recibo independiente de abono, incluyendo los ya registrados. Los nuevos congelan nombre del
  cliente/método/registrador y saldo antes/después dentro de la transacción del abono existente.
  Los antiguos conservan sus datos; se indica cuando no existe saldo histórico congelado.
  Una devolución posterior no altera importe ni saldo histórico del recibo; se distingue del saldo actual.
- Estado de cuenta del cliente al momento consultado: cargos financiados originales, abonos no
  anulados, reducciones de deuda por posventa aplicada y reintegros de efectivo informativos (sin
  restarlos de nuevo de CxC). Cada movimiento enlaza venta/documento y muestra saldo acumulado.
  No incluye como deuda el efectivo inicial de una venta mixta, ni solicitudes/rechazos de devolución.
  Saldo registrado y calculado se comparan; diferencias heredadas se muestran para revisión, sin
  fabricar ajustes. Cuentas CASTIGADA se identifican aparte del saldo cobrable; no se castigan aquí.
- Consultas/UI paginadas. Documento completo hasta 5000 movimientos/cuentas; si supera el límite,
  se rechaza la generación antes de emitir totales parciales. No se ofrecen cierres históricos por fecha.
- PDF/vista previa/impresión del mismo documento; medidas Carta/A4/tickets 58/80/configurables,
  con paginación real. Documentos internos, no DTE. Se mantiene la recuperación durable de abonos.
- Mora/excepciones automáticas, anulación de abonos, saldo a favor, DTE, envío automático,
  entrega fallida/COD y despliegue productivo siguen pendientes de definición/otros incrementos.

## Migración 021 (aditiva e idempotente)

- `ven.AvisosCobranza`: IdAviso bigint identity PK, IdCuenta FK, IdOperacion uniqueidentifier UNIQUE,
  Fecha datetimeoffset(0), FechaLocal date, IdUsuario/IdEquipo FK, Saldo decimal(18,2)>0,
  Vencimiento date, ProximoAviso date, Medio nvarchar(20), Resultado nvarchar(30),
  Observacion nvarchar(500); UNIQUE(IdCuenta,FechaLocal). Índice por cuenta/fecha/ID.
  Valores válidos definidos por el validador; registros inmutables y bitácora/ledger en la misma transacción.
- `ven.AbonosCliente`: ClienteNombreSnapshot nvarchar(200), MetodoSnapshot nvarchar(80),
  UsuarioSnapshot nvarchar(150), SaldoAntes/SaldoDespues decimal(18,2), todos NULL para históricos.
  CHECK cuando hay saldos: antes - monto = después, ambos no negativos; no rellenar antiguos.
- Permiso anterior y concesión Administrador sólo si falta. Total **77 tablas / 8 vistas / 18 procedimientos**.
  No se alteran formas JSON de solicitudes anteriores, ni se reescriben importes, pagos o retornos.
- Abonos/posventa conservan su orden de bloqueo; registrar aviso sólo bloquea cliente compartido
  para leer intervalo y luego cuenta (no necesita bloqueo de venta/caja), y no modifica dinero.

## Continuación

Después: **2.2C**, decidir si se necesita cobro contra entrega y cerrar su diseño antes de
implementar. Luego **2.3**, reportes operativos según DEV-30. DEV-36 (instalación,
compatibilidad/actualización y respaldos/restauración de BD + evidencias) sigue pendiente antes del piloto.

La vista previa se limita a 40 páginas para acotar memoria; rechaza documentos superiores sin
entregar PDF parcial. Raster a 144 dpi, geometría en mm. No comparte el selector Venta/Entrega
con documentos de crédito. Los cambios de medida invalidan guardar/imprimir hasta regenerar.

## Uso y actualización

1. Cerrar GoDental en todas las estaciones, respaldar BD y evidencias, actualizar `feature/desarrollo`.
2. Aplicar `database/021_Cobranza_Documentos.sql` después de 020 sobre la base/instancia guardadas
   en Ajustes. Actualizar ambas aplicaciones y volver a iniciar sesión para obtener el permiso nuevo.
3. Crédito → Consultar cuentas. `Avisos por atender` filtra en SQL las cuentas que ya corresponden.
   Abrir cuenta → Registrar aviso; indicar sólo una gestión realmente realizada o revisión interna.
   La pestaña Avisos muestra el historial y su observación; no registrar significa no adelantar la fecha.
4. Estado de cuenta → Consultar estado permite revisar movimientos/saldos. `Estado de cuenta`
   genera el PDF completo y abre Impresión, con el tamaño elegido y todos sus movimientos.
5. En Abonos del cliente seleccionar un registro → Recibo de abono. Se abre Impresión para revisar,
   guardar PDF o imprimir. Cambiar medidas invalida la vista; volver a generarla antes de entregar.
   Guardar e imprimir consumen exactamente el documento revisado, sin registrar un segundo pago.

Para la base **de desarrollo marcada** (la herramienta no es un migrador productivo):

```powershell
git switch feature/desarrollo
git pull --ff-only origin feature/desarrollo
$servidor = Read-Host 'Instancia SQL guardada en Ajustes'
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --install-development --server "$servidor" .
dotnet run --project src/GoDental.Wpf -c Release
```

Debe mostrar `SQL 021_Cobranza_Documentos.sql`. Para una base existente fuera de la herramienta,
ejecutar sólo migraciones faltantes en orden, por ejemplo con `sqlcmd -S <instancia> -d <base> -E -b -i database/021_Cobranza_Documentos.sql`, sustituyendo los marcadores.
No se ha aplicado la migración a la BD personal desde cloud. No forzar el marcador de desarrollo
sobre una base productiva; DEV-36 sigue siendo necesario para despliegue/actualización productivos.

## Aceptación en las estaciones

Después de actualizar la BD y ambas aplicaciones, iniciar sesión otra vez y comprobar:

- En una cuenta con aviso pendiente, registrar una revisión interna y consultar su historial
  desde la segunda estación. La cuenta debe dejar de aparecer en avisos por atender hasta
  la fecha programada; una pantalla con versión anterior debe pedir actualizar.
- Registrar un abono de prueba, seleccionar su recibo y comparar sus saldos antes/después con
  el estado de cuenta. Si después se aplica una devolución, el recibo original debe conservar
  esos saldos y el estado de cuenta debe mostrar la reducción de deuda correspondiente.
- Revisar Carta y un tamaño de ticket, guardar el PDF y abrirlo. Cambiar las medidas exige
  regenerar antes de guardar/imprimir. La impresora física y su escala se comprueban cuando
  esté disponible el equipo; el smoke automatizado no certifica el hardware.

## Verificación

- Código validado: `987b98bd798f68113be214d1e2654bcc7a73f853` en `feature/desarrollo`.
  [CI Windows #155](https://github.com/Gust4av033/GoDental/actions/runs/39904041587)
  completado correctamente; registro del job `120251791163` consultado.
- Release con **0 advertencias / 0 errores** y **315 xUnit**, sin fallos ni omitidas.
  Build Release y las mismas 315 pruebas también pasan localmente con Windows targeting;
  Linux no ejecuta WPF ni SQL Server.
- **627 comprobaciones SQL** en bases aisladas: inventario 40, cobranza 31, posventa 56,
  seguridad/catálogo 51, conteos 67, proveedores 43, clientes 48, MVP 128, códigos 18,
  recuperación 24, complementos 39, compras 39 e importaciones 43.
  Incluye avisos concurrentes/idempotentes, fallo de auditoría con rollback, permisos
  revocados, reaplicación de 001–021 sin cambiar historia y regresión real
  devolución → reducción de deuda/efectivo → estado de cuenta/recibo original.
- **897 comprobaciones WPF**: pestañas de Crédito en Light/Dark/compacto, formulario de
  avisos, PDF real/vista previa de estado y recibo, mismo documento al guardar/imprimir,
  y bloqueo de documento anterior tras cambiar medidas; sin errores de binding.
  La prueba de formulario comprueba etiquetas legibles y reintento con el mismo GUID
  después de perder la respuesta. Se conservan las comprobaciones de módulos anteriores.
- No se aplicó 021 a la BD personal ni se ejecutó aceptación manual entre las dos PCs.
  Capturas/impresión simulada no certifican el monitor, DPI o impresora física.
