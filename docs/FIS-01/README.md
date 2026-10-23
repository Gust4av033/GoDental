# FIS-01 — Fiscal, Facturación, DTE y Crédito Fiscal

**Versión:** v0.1 · **Estado:** arquitectura base; integración real pendiente

Principio principal: **Venta != DTE**. La operación comercial y el documento fiscal son entidades/procesos relacionados pero desacoplados.

## Reglas centrales

- DocumentoFiscal/DocumentoDte conserva identificadores, estado, snapshots y eventos fiscales sin reemplazar Venta.
- WPF no llama endpoints fiscales ni maneja certificados; Application define contratos e Infrastructure implementa transporte/firma/configuración.
- Reintentar una transmisión no crea una segunda venta ni un documento duplicado.
- Fallas técnicas pueden dejar el documento pendiente/reintento según política; nunca marcar aceptado sin respuesta válida.
- Factura Electrónica y CCFE son los tipos iniciales previstos; notas/invalidaciones se incorporan conforme normativa vigente.
- Correlativo comercial y Código de Generación/Número de Control fiscal son identificadores distintos.
- Secretos fiscales nunca viven en Git ni logs; Pruebas/Producción deben estar claramente separados.
- PDF/representación gráfica no sustituye el DTE/JSON aceptado y reimprimir no genera otro DTE.

La integración real con Hacienda no se activa hasta revisar normativa/esquemas/endpoints/credenciales vigentes y cerrar FIS-01 v0.2.

## Avance interno · 23/10/2026

[DEV-44](../DEV-44-Fiscal-Interno.md) implementa bandeja/preparación/revisión interna e historial
(migración 024), separados del estado fiscal y con datos actuales frente al comprobante original.
No equivale a FIS-01 v0.2 ni habilita emisión/firma/transmisión: el usuario aún no tiene acceso
a pruebas. [DEV-45](../DEV-45-Seguridad-Hallazgos.md) define controles de seguridad bloqueantes
para integrar Hacienda. Pendientes locales desde 021, incluida, hasta 024.
