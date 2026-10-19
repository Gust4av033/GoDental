# Roadmap y decisiones pendientes

Orden heredado de DM-01 v0.2:

1. Revisar dominio y DB-01; cerrar relaciones, restricciones, índices y reglas.
2. Implementar esqueleto funcional: DI, Dapper, conexión, MVVM, navegación, login y temas.
3. Productos, lotes, existencias, kardex y conteos.
4. Proveedores, compras, importaciones, recepciones y costo puesto en bodega.
5. Clientes, cotizaciones y ventas concurrentes.
6. Caja, pagos mixtos, crédito y documentos PDF.
7. Dashboard, reportes e inteligencia comercial.
8. Online y prospección con revisión humana.
9. Integración fiscal/DTE cuando estén disponibles requisitos y credenciales vigentes.

## Avance de lógica · 19/10/2026

- Fases 1–2: baseline, arquitectura, acceso y configuración implementados para desarrollo.
- Fase 3: catálogo, lotes, saldos, movimientos y seguridad operativos (DEV-03). Los conteos físicos agregan contratos C#/SQL y pruebas en DEV-09; su pantalla queda para el incremento de UI. Inventario inicial definitivo, series, etiquetas y reposición avanzada conservan sus pendientes.
- El siguiente bloque funcional es proveedores como base para órdenes y recepciones de compras. No se salta directamente a ventas ni se presenta la fase 3 como certificada para producción.

## Reglas comerciales ya documentadas

- Descuento efectivo combinado: hasta 7% según permiso; más de 7% hasta 35% con motivo y bitácora; más de 35% con autenticación de administrador y motivo.
- Devoluciones: plazo inicial de 24 horas, comprobante y autorización de administrador; productos abiertos requieren justificación/evidencia y evaluación de reingreso.
- Costeo promedio ponderado móvil con gastos de importación; conservar costo histórico de la venta.
- Listas de precio configurables por segmento, cantidad y monto; umbral comercial inicial de $50 configurable.
- Crédito habilitado por cliente; plazo inicial de 15 días y avisos cada 5 días, con límite editable.
- Efectivo, transferencia, QR, depósito, tarjeta, crédito y pagos mixtos; integración real de tarjeta futura.

## Pendientes que no deben darse por resueltos

Prioridad y acumulación de precios/promociones/descuentos; bloqueo por mora y excepciones; prorrateo final de importaciones (por valor es la propuesta inicial); series de productos; terminal de tarjeta; ubicación compartida de evidencias; datos fiscales; DTE; canales Online autorizados; instancia SQL y red; retención y restauración de backups; identidad y plantillas definitivas.

DB-01 sigue en versión 0.1 como borrador. Su revisión precede a la generación del DDL. La creación del repositorio no implica aprobar todas esas decisiones.
