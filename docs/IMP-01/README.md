# IMP-01 — Compras, Proveedores, Importaciones, DHL, Recepciones y Costeo

**Versión:** v0.2 · complemento de 2.1B, 22/10/2026

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

## Política de costeo confirmada para 2.1B

El usuario aprobó repartir gastos por valor neto de mercadería y cerrar todos los costos antes de aplicar la primera recepción. DEV-37 define centavos por restos mayores, costo unitario a seis decimales y consumo acumulado del presupuesto en recepciones parciales. La conversión usa cambio real histórico común para la mercadería; cada gasto conserva su propio cambio histórico. No se reabre un cierre ni se agregan gastos posteriores; recibir con costo incompleto sigue sin habilitarse. Courier/guía son opcionales y los eventos se registran manualmente.

La integración real con DHL, costos posteriores al cierre y recepción con costos incompletos quedan como ampliaciones posteriores con diseño explícito. La siguiente fase funcional tras 2.1B es 2.2A, sin cerrar esos alcances por omisión.
