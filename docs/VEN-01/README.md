# VEN-01 — Ventas, POS, Precios, Descuentos, Caja, Pagos, Crédito y Devoluciones

**Versión:** v0.2 · Posventa según DEV-38, 23/10/2026

Ventas es el módulo operativo de mayor frecuencia. Debe ser rápido en UI y estricto en Application/SQL para soportar dos vendedores simultáneos sin sobreventa ni inconsistencias financieras.

## Reglas centrales

- Confirmar venta es una operación transaccional; SQL vuelve a validar stock, lote, pagos y crédito.
- El carrito nunca es fuente final de disponibilidad.
- Precios/listas/promociones y descuentos se preservan como snapshot histórico.
- Descuento ≤7% permitido al vendedor; >7% y ≤35% exige justificación; >35% exige reautenticación/autorización administrativa.
- Pagos: efectivo, transferencia, QR, depósito, tarjeta manual con referencia (DEV-32), crédito y pagos mixtos.
- Caja usa apertura, movimientos y cierre trazables; no se edita saldo directamente.
- Crédito valida habilitación y límite; el bloqueo definitivo por mora sigue pendiente. Los abonos conservan monto original y reducen saldo.
- Devolución vigente: máximo 1 día, comprobante, motivo/evidencia cuando aplique y autorización del Administrador. Nunca borra la venta original.
- Anular no significa DELETE; se registran reversos/movimientos explícitos.
- Error de impresión después de confirmar no revierte la venta; se permite reimpresión.

Precedencia de precios/promociones: VEN-01A. Tarjeta manual y parámetros comerciales: DEV-32.
Venta bajo costo, bloqueo definitivo por mora y saldo a favor reutilizable siguen diferidos.

## Posventa · decisiones confirmadas

DEV-38 implementa solicitud dentro de `Devolucion.PlazoHoras` (24), comprobante/motivo,
fotos para abierto/defectuoso y reautenticación administrativa. Solicitar/rechazar no mueve saldos.
Aplicar reduce deuda pendiente de esa venta y reintegra excedente en efectivo desde un turno
abierto compatible con fondo suficiente. Preserva importes, detalles, pagos y abonos originales.
Reingreso sellado vendible usa almacén/lote y costo histórico; lo no vendible se identifica en la
devolución sin sumarse a disponible. La carpeta central de fotos se respalda además de la BD.

El usuario confirmó anular también pedidos en ruta/entregados: recuperar toda la mercadería
sellada/vendible y autorizar compensación total (incluye entrega). Pedido conserva el hecho histórico
de entrega y eventos; no se borra ni se vuelve a cobrar. Devolución ordinaria no reintegra entrega.
La reversión de DTE emitidos requiere el módulo fiscal. DEV-38 detalla permisos, migración 019,
concurrencia, recuperación y limitaciones; sigue 2.2B, cobranza/documentos de crédito.
