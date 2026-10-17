# DEV-03 · Inventario operativo

Incremento de INV-01 v0.1 / DB-01 v0.1. Conserva las tablas y procedimientos 001–009 y los contratos públicos existentes. Los nuevos contratos se agregan en Application y los nuevos procedimientos en la migración 010.

## Decisiones para este incremento

- Ajustes manuales: `AJUSTE` con cantidad firmada; daño y vencimiento son salidas. Motivo obligatorio, permiso `INVENTARIO.AJUSTAR`, auditoría y Kardex en la misma transacción. No se presentan como compras ni como ventas.
- Ajuste positivo: costo unitario explícito y promedio ponderado móvil por almacén. Ajuste negativo/daño/vencimiento: costo promedio vigente, sin recalcularlo. Redondeo de costo a seis decimales. No se reescribe el costo histórico del lote.
- Inventario inicial sigue pendiente de política formal; este flujo corrige diferencias físicas documentadas y no se etiqueta como migración de inventario inicial.
- Lotes nuevos se crean al confirmar una entrada positiva. Se exige número, vencimiento cuando corresponde y fechas coherentes. El mismo número ya registrado se selecciona como lote existente; no se crea un duplicado físico desde ajustes.
- Un lote bloqueado (`Activo=0`) conserva saldo físico e historial. No admite entradas ni transferencias; sí admite bajas justificadas. La baja por vencimiento exige fecha anterior al día operativo en El Salvador. El día se calcula en SQL, no depende del reloj de la laptop.
- Stock físico, reservado, disponible físico y vendible se muestran por separado. Vendible excluye lotes bloqueados/vencidos y descuenta reservas agregadas conservadoramente; no implica reservas por lote.
- Transferencias: dos movimientos enlazados, salida a costo promedio del origen y entrada que recalcula promedio del destino; misma transacción. Lote conserva identidad/costo histórico. Ambas existencias se bloquean en orden de almacén.
- Concurrencia: lectura protegida de producto y saldos, rowversion esperado del saldo de origen (vacío significa que aún no existe), bloqueo de rango para saldos nuevos. Después de guardar, debe recargarse el saldo. Un reintento con versión vieja no duplica movimiento.
- Bloqueo/reactivación de lote usa `INVENTARIO.AJUSTAR`, motivo y comparación de estado anterior. No cambia stock ni borra historia.
- Costos sólo se devuelven con `INVENTARIO.COSTOS_VER`, revalidado en SQL. Las demás consultas y mutaciones también revalidan permisos SQL.
- Kardex paginado con filtros de producto, almacén, lote, tipo y fechas; conserva usuario, referencia y observación. Las referencias se muestran aunque el módulo de origen aún no tenga navegación.
- Series, conteos físicos, etiquetas, compras/ventas/devoluciones y reportes analíticos no se declaran terminados por este incremento.

## Despliegue

Ejecutar 010 después de 009, sobre la misma base de datos, antes de abrir las nuevas operaciones. No ejecutar scripts de prueba en producción. El instalador de desarrollo incluye 010 automáticamente. Sin migración, la UI informa el fallo y no simula guardado.

## Validación

Se documentarán aquí los resultados efectivamente ejecutados al cerrar el PR. La revisión visual WPF y las pruebas SQL requieren Windows/SQL Server; la compilación cruzada en Linux no las sustituye.

## Catálogo y categorías

Se añaden filtros por categoría, marca y stock bajo, y días de alerta configurables por producto. El SKU con movimientos no se cambia. Las categorías permiten renombrado, desactivación y reactivación con auditoría; se compara nombre/estado anterior porque DB-01 no define rowversion en esta tabla. Se rechaza desactivar categorías con productos o subcategorías activos. No se edita la jerarquía en este incremento.

## Pruebas de este incremento

- Unitarias: validaciones de cantidades/costos, motivo, permisos, transferencia y lotes.
- SQL: `dotnet run --project tests/GoDental.DatabaseTests -c Release -- --run-inventory .` crea una base `GoDental_Tests_<GUID>` aislada. En CI se añade `--localdb`, exclusivamente en el ejecutable de pruebas; LocalDB continúa prohibido como base central de la aplicación.
- SQL cubre costo promedio, compensaciones, bloqueo/vencimiento, transferencias, auditoría/rollback, última unidad desde dos conexiones, reservas, permisos revocados y paginación.
- Smoke WPF renderiza Light/Dark, navegación y las cuatro secciones de Inventario. Las clases de datos controlados sólo existen en tests, sin registro en App.
- La compilación cruzada Release mediante MSBuild pasó en los bloques de persistencia y UI. El CLI y VSTest no funcionan en el entorno Linux de edición; los resultados canónicos de `dotnet build/test` y SQL/WPF se comprobarán en Windows CI antes de integrar.
