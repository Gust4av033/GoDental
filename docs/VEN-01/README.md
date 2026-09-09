# VEN-01 — Ventas, POS, Precios, Descuentos, Caja, Pagos, Crédito y Devoluciones

**Versión:** v0.1

Ventas es el módulo operativo de mayor frecuencia. Debe ser rápido en UI y estricto en Application/SQL para soportar dos vendedores simultáneos sin sobreventa ni inconsistencias financieras.

## Reglas centrales

- Confirmar venta es una operación transaccional; SQL vuelve a validar stock, lote, pagos y crédito.
- El carrito nunca es fuente final de disponibilidad.
- Precios/listas/promociones y descuentos se preservan como snapshot histórico.
- Descuento ≤7% permitido al vendedor; >7% y ≤35% exige justificación; >35% exige reautenticación/autorización administrativa.
- Pagos: efectivo, transferencia, QR, depósito, tarjeta futura, crédito y pagos mixtos.
- Caja usa apertura, movimientos y cierre trazables; no se edita saldo directamente.
- Crédito valida habilitación, límite y mora; abonos no modifican la venta original.
- Devolución vigente: máximo 1 día, comprobante, motivo/evidencia cuando aplique y autorización del Administrador. Nunca borra la venta original.
- Anular no significa DELETE; se registran reversos/movimientos explícitos.
- Error de impresión después de confirmar no revierte la venta; se permite reimpresión.

La prioridad definitiva de precios/promociones, venta bajo costo, mora, crédito a favor y tarjeta se cierra en v0.2.
