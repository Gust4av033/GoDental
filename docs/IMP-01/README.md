# IMP-01 — Compras, Proveedores, Importaciones, DHL, Recepciones y Costeo

**Versión:** v0.1

El módulo separa compra comercial, logística/importación y recepción física para alimentar Inventario con cantidades, lotes y costo real puesto en bodega.

## Reglas centrales

- Proveedores conservan historial; un producto puede tener referencias/costos distintos por proveedor.
- Órdenes soportan monedas distintas a USD y conservan tipo de cambio histórico.
- Importación admite courier y guía nullable; DHL es habitual, pero tracking no bloquea el flujo manual.
- Eventos logísticos forman una línea de tiempo y no se sobrescriben.
- Gastos atribuibles (flete, impuestos, aduana, manejo, etc.) se distribuyen explícita y auditablemente.
- Recepciones pueden ser parciales; el saldo pendiente se recalcula en servidor.
- Confirmar recepción es transaccional: validar → costear → lotes → existencias → promedio → Kardex → estado → commit; ante fallo, rollback completo.
- Mercadería en tránsito no forma parte del stock vendible.
- No improvisar documentos de importación, cuentas por pagar o reclamos sin diseño formal previo.

La integración real con DHL y el tratamiento definitivo de recepciones con costo incompleto quedan para v0.2.
