# DEV-11 · Conectar la siguiente UI a los servicios de lógica

Entrega del 19/10/2026 en `feature/desarrollo`. Mantener únicamente esta rama y `main`; actualizar referencias y avanzar por fast-forward antes de editar. No crear otra rama permanente de UI ni duplicar reglas en WPF. Esta entrega no cambia archivos dentro de `src/GoDental.Wpf/`.

## Preparación

Consultar DEV-09/DEV-10 y respetar UX-01, SEC-01 y ARQ-01. `InventoryCountService` y `SupplierService`, junto a sus repositorios, ya están registrados en DI. Inyectarlos en ViewModels mediante constructor, como los servicios existentes. Se utiliza la misma sesión y conexión elegida en Ajustes; no añadir credenciales ni conexión alternativa.

La migración 011 debe estar aplicada en la base de desarrollo antes de consultar conteos. Las pruebas de esta entrega sólo actualizan bases aisladas. Para preparar la base local marcada del equipo, ejecutar desde el repositorio:

```powershell
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --install-development .
```

Ese comando verifica el marcador de `GoDental_Development` antes de aplicar migraciones, conserva cuentas/datos y no vuelve a ejecutar el seed sobre una base completa. No usarlo como instalador de producción. Proveedores usa las tablas existentes, sin otra migración.

## Conteos

- Listado con almacén, estado, fecha, capturadas/total y autor: `SearchAsync(InventoryCountFilter)`.
- Nuevo: seleccionar almacén con `InventoryService.WarehousesAsync` y productos del catálogo; `StartAsync(StartInventoryCount)`. Los productos deben tener saldo establecido; SQL incluye todos sus lotes del almacén.
- Detalle: `ReadAsync(id)` devuelve cabecera y líneas. Capturar cantidades por línea con `CaptureAsync(CaptureInventoryCount)`, enviando la versión de cabecera. Guardar por grupos de hasta 1000 líneas, recargando entre grupos.
- Mostrar pendiente cuando `Counted=false`, nunca interpretarlo como cero. La diferencia nullable ya viene calculada en el DTO. `RequiresRecount=true` avisa de movimiento posterior: cancelar y abrir un conteo nuevo después de revisar, sin actualizar el snapshot silenciosamente.
- Cierre y cancelación requieren motivo/confirmación y versión de cabecera. `CanClose` controla visibilidad de cerrar; `CanRead` habilita acceso al flujo. Mostrar estado de guardado y bloquear envíos repetidos. Recargar después de toda escritura y ante conflicto; si se pierde la respuesta de cierre, consultar el estado antes de repetir.
- Un conteo finalizado es de consulta. Lotes bloqueados/vencidos se cuentan, pero no se admiten diferencias positivas en ellos. El cierre protege reservas y no modifica costo histórico.

## Proveedores

- Listado: `SupplierService.SearchAsync(SupplierSearch)`; detalle: `ReadAsync(id)`; catálogo de monedas activas: `CurrenciesAsync()`.
- Ficha de alta usa Id=0 y `RowVersion=[]`; edición usa la ficha leída, incluyendo su versión. `SaveAsync(SupplierChange)` sirve para alta, edición y cambio de estado con motivo. Código obligatorio y protegido si existen órdenes.
- Contactos: `SaveContactAsync(SupplierContactChange)` con `ExpectedSupplierVersion`. Id=0 agrega contacto; Id existente lo edita sin reemplazar otros. Un contacto inactivo no puede ser principal; para cambiar de principal desmarcar el actual, guardar/recargar y luego seleccionar el nuevo.
- Referencias: `SaveProductAsync(SupplierProductChange)` con `ExpectedSupplierVersion`. El par proveedor+producto se actualiza/reactiva, no duplica. Mostrar SKU externo, costo/moneda, MOQ, plazo y preferencia. Costo conocido exige moneda; seis decimales para costo y cuatro para MOQ.
- `CanManage` habilita escrituras; `CanRead` acceso al listado. Costo comercial null puede significar ausencia o falta de permiso: no sustituirlo por cero ni reconstruirlo en UI. Un proveedor inactivo conserva su detalle y requiere reactivación antes de editar subregistros.
- Tras editar contacto o producto, recargar también la ficha: su versión cambió. Los códigos de error están en DEV-10.

## Estados y validación

Respetar loading/empty/error, cancelación de lecturas y Light/Dark. Tratar `FORBIDDEN`, `CONCURRENCY_CONFLICT` y `INVALID_STATE` como estados recuperables, sin reintentos automáticos de escrituras. Las lecturas pueden producir `UnauthorizedAccessException` por revocación de permisos aun si la sesión de UI los conserva. Mostrar fallos de conexión/migración sin simular datos o guardados.

Después de conectar las vistas, ampliar smoke WPF para nuevos estados y ejecutar `scripts/verify.ps1 -SqlIntegration`. No presentar como implementadas órdenes, recepciones, importaciones, etiquetas, series ni inventario inicial: esta entrega cubre conteos y proveedores en lógica/persistencia.
