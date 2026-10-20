# DEV-09 · Conteos físicos: lógica y persistencia

Decisión técnica del 19 de octubre de 2026. Continúa la fase 3 de DM-01 y las tablas de conteos de DB-01 antes de pasar a proveedores/compras. Esta edición entrega servicios C# y SQL, sin cambiar las vistas WPF; el siguiente incremento de UI consume estos contratos.

## Flujo y límites

1. Abrir un conteo parcial por almacén seleccionando hasta 500 productos con saldo previamente establecido. Para productos con lotes se incluyen todos los saldos de lote del almacén, incluidos los bloqueados/vencidos y los de cantidad cero. No crea productos, lotes ni inventario inicial; series siguen pendientes.
2. SQL toma una fotografía de cantidades y versiones de existencias. No se mantiene una transacción abierta durante el trabajo físico. Un producto no participa en dos conteos abiertos del mismo almacén.
3. Capturar cantidades físicas no negativas, con hasta cuatro decimales. Se permite guardar por grupos y revisar antes del cierre. Una línea pendiente es distinta de una cantidad contada de cero. Toda edición exige la versión actual de la cabecera.
4. Cerrar exige todas las líneas contadas, motivo y permisos `INVENTARIO.VER`, `INVENTARIO.CONTEO` e `INVENTARIO.AJUSTAR`. SQL vuelve a comprobar permisos, estado, productos, almacén, saldos, reservas y versiones. Si hubo movimientos o reservas desde la apertura, incluso si el saldo volvió al mismo valor, se rechaza el cierre: cancelar y abrir un conteo nuevo tras revisar el movimiento, sin rebasar automáticamente la fotografía.
5. El cierre aplica diferencias como `AJUSTE`, con referencia `CONTEO` + identificador. Conserva el costo promedio establecido y el costo histórico del lote; no genera un movimiento de cantidad cero. Existencia, lotes, Kardex, líneas, cabecera y auditoría se confirman en una sola transacción. El saldo agregado final no puede quedar por debajo de reservas, aunque se cuenten varios lotes con diferencias opuestas.
6. Los lotes bloqueados/vencidos se pueden contar. Una diferencia positiva en ellos se rechaza, manteniendo la política de entradas de DEV-03; una diferencia negativa corrige el físico sin reactivar ni vender el lote.
7. Cancelar exige motivo y permisos de lectura/conteo. Conserva líneas e historial; no genera movimientos. Un conteo cerrado/cancelado no puede editarse ni aplicarse otra vez. Tras pérdida de respuesta se consulta el conteo antes de repetir.

## Extensión documentada de DB-01

La migración aditiva `011_Conteos_Inventario.sql`, posterior a 010, conserva las 72 tablas. Agrega a `inv.ConteosInventario` una `RowVersion`; a `inv.ConteoInventarioDetalle`, `Contado` (bit), `VersionExistencia` y `VersionLote` (varbinary(8), nullable para filas históricas). Las filas existentes se marcan contadas para conservar su significado; un conteo histórico abierto sin fotografía de versión requiere cancelarse y abrirse de nuevo.

Se agregan tres procedimientos: `inv.sp_AbrirConteo`, `inv.sp_CapturarConteo` e `inv.sp_FinalizarConteo`. Reciben JSON acotado para listas de productos/cantidades, validado también en servidor, sin SQL dinámico. Las consultas de cabecera/detalle no devuelven costos. Los bloqueos de productos y saldos se adquieren en orden de producto, compatibles con el bloqueo de ajustes de 010. Los errores de concurrencia se devuelven como resultados recuperables; no hay reintentos silenciosos de escrituras.

## Contratos para UI

`InventoryCountService` expone `CanRead`, `CanClose`, `SearchAsync`, `ReadAsync`, `StartAsync`, `CaptureAsync`, `CloseAsync` y `CancelAsync`. `InventoryCountData.RowVersion` se devuelve al leer y se envía en cada edición/finalización. Tras guardar se recarga el detalle. `InventoryCountLineData.Difference` es null mientras no se haya contado, y `RequiresRecount` señala una fotografía obsoleta al consultar un conteo abierto. No equivale a bloquear movimientos posteriores a la lectura: el servidor decide definitivamente al cerrar.

La UI debe mostrar claramente almacén, estado, capturadas/total, pendientes, diferencias y conflictos; pedir confirmación al cerrar/cancelar. Las escrituras devuelven `Result<long>` con códigos `FORBIDDEN`, `VALIDATION`, `CONCURRENCY_CONFLICT`, `INSUFFICIENT_STOCK` o `INVALID_STATE`; las lecturas denegadas producen `UnauthorizedAccessException`. No ejecutar SQL desde ViewModels.

## Despliegue y validación

Aplicar 011 después de 010 sobre la base destino revisada. La herramienta de desarrollo adapta el destino en memoria y comprueba el marcador; no instala automáticamente sobre la base del usuario al ejecutar pruebas. Los tests usan bases `GoDental_Tests_<GUID>` aisladas. Ejecutar `scripts/verify.ps1 -SqlIntegration` antes de integrar.

La suite nueva pasó 67 comprobaciones SQL reales en Windows/SQL Server local: captura y cierre concurrentes, cantidades cero, reservas hasta 0.0001, costo histórico, lotes bloqueados/vencidos, versiones de producto y lote, movimiento que vuelve al mismo saldo, permisos revocados, rollback completo y reaplicación de migraciones. Los validadores/reglas/servicio añaden 11 casos xUnit (31 en total con los anteriores). Los procedimientos establecen explícitamente READ COMMITTED para no heredar aislamiento de conexiones reutilizadas antes de adquirir el bloqueo de conteo.

No se certifican inventario inicial, series, etiquetas, compras, ventas ni operación productiva multi-PC por entregar estos contratos.
