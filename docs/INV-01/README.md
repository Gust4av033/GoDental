# INV-01 — Inventario, Productos, Lotes, Costeo y Trazabilidad

**Versión:** v0.1

Inventario es transaccional y trazable. Ningún ViewModel modifica stock; toda entrada/salida genera movimiento con origen, usuario, equipo y lote cuando corresponda.

## Reglas centrales

- Existencia por producto + almacén; stock disponible = actual - reservado.
- Validación definitiva de stock dentro de SQL Server al confirmar operaciones concurrentes.
- Productos con lote/vencimiento usan FEFO; lotes vencidos o bloqueados no se venden normalmente.
- Valuación por promedio ponderado móvil y costo puesto en bodega proveniente de compras/importaciones.
- Kardex es historial: no se edita/borrar para corregir; se usan movimientos compensatorios.
- Recepciones, ventas, devoluciones, ajustes y conteos deben mantener existencia + lote + costo + movimiento en transacciones coherentes.
- Productos con historial se desactivan, no se eliminan.
- Stock inicial entra mediante una operación trazable, nunca escribiendo `StockActual` manualmente.

Servicios previstos: Producto, Inventario, Lotes, Conteos, Barcode y Reposición. Repositorios específicos; no GenericRepository.
