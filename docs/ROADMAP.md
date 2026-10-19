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
- Fase 4: proveedores, contactos y referencias comerciales de productos tienen lógica, persistencia y pruebas (DEV-10); UI pendiente. El siguiente bloque es órdenes y recepciones, con sus estados y costeo, antes de ventas. Prorrateo definitivo de importaciones y recepciones con costo incompleto conservan sus decisiones pendientes de IMP-01.

## Primer MVP operativo · incremento de lógica

El ciclo priorizado es existencias iniciales/ajustes → clientes → cotización → venta → cobro/crédito → pedido/entrega → arqueo. DEV-17 documenta su implementación en `feature/logica`, los contratos para `feature/ui`, las pruebas y sus límites. Esto adelanta un recorrido mínimo de operación; no declara completas todas las fases del roadmap. Las interfaces de conteos, proveedores y clientes ya existen en la base común.

Para cerrar el MVP faltan ejecución SQL de la suite nueva, conexión de estas nuevas funciones desde UI y validación del recorrido en Windows/dos estaciones. Compras/recepciones/importaciones, reportes avanzados, devoluciones y conectores se desarrollarán después con sus decisiones de negocio explícitas. La fase dos solicitada por el usuario es ese segundo ciclo de refinamiento/ampliación; no sustituye la numeración histórica de DM-01.

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
