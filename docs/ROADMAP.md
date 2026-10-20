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

La suite SQL nueva ya pasó en CI Windows, junto con regresiones y smoke WPF (DEV-17). Caja, POS/cotizaciones, crédito/abonos, pedidos/entregas y PDF interno ya están conectados en páginas propias (DEV-19), con CI Windows #50 correcto. Para cerrar el MVP queda la validación manual del recorrido en dos estaciones y la resolución negativa definitiva de operaciones inciertas indicada en DEV-15. Saldo inicial/códigos de barras y fiscal pendiente conservan UI pendiente fuera del bloque solicitado. Compras/recepciones/importaciones, reportes avanzados, devoluciones y conectores se desarrollarán después con sus decisiones de negocio explícitas. La fase dos solicitada por el usuario es ese segundo ciclo de refinamiento/ampliación; no sustituye la numeración histórica de DM-01.

## Prioridad vigente · Pedidos → Ventas → Entregas

Decisiones de `feature/desarrollo`, definidas en [DEV-17](DEV-17-MVP-Logica.md), con recorrido de interfaz en [DEV-19](DEV-19-UI-MVP.md) y validación en [DEV-26](DEV-26-Ventas-Caja-Refinamiento.md). El paso al POS ya se implementa con autorización posterior; reservas y cobro contra entrega siguen pendientes.

1. **Implementado: Pasar al POS / Crear venta desde pedido.** Ficha de pedido → Pasar al POS → revisar/cobrar. Precarga cliente, productos, cantidades y cargo de entrega, conserva referencia y recalcula precios/promociones/descuentos por servicio. Confirmación y enlace compatible se ejecutan en una transacción con bloqueo/versionado del pedido y un solo `OperationId`, impidiendo dos ventas para el mismo pedido desde equipos distintos. El ID manual queda como recuperación/compatibilidad. No cambia el estado de preparación ni requiere migración nueva. Validación en DEV-26; recorrido manual en las dos estaciones del negocio aún pendiente.
2. **Pedido y stock:** **un pedido no reserva ni descuenta inventario**. Para la operación pequeña actual se recomienda **RECIBIDO → PREPARANDO → CONFIRMAR VENTA → LISTO → EN_RUTA → ENTREGADO**. `RECIBIDO` y `PREPARANDO` no reservan; **LISTO tampoco significa reserva**. Antes de considerarlo listo para salir debe existir una venta confirmada asociada, que descuenta inventario, asigna lotes, genera Kardex y registra pago/crédito. Confirmar venta es un paso comercial, no un estado nuevo. Hoy la validación sólo exige esa venta para `EN_RUTA` y `ENTREGADO`; no se cambia `OrderRules`. No implementar reservas complejas; una reserva ligera queda como evolución futura si el negocio la necesita.
3. **Cobro contra entrega: decisión pendiente de diseño.** No convertirlo automáticamente en crédito comercial: no debe consumir límite, generar mora, alterar indicadores de crédito ni crear CxC comercial ordinaria por unas horas de reparto. Definir protección de unidades en `EN_RUTA`, descuento definitivo del inventario, momento de confirmar venta, rechazo/fallo de entrega, registro de efectivo/efecto en Caja y devolución de unidades disponibles. No añadir todavía método de pago ni comportamiento transaccional; el flujo actual no lo habilita por sí solo.

**Pedido = solicitud/preparación · Venta = operación comercial · Entrega = cumplimiento físico · Cobro contra entrega = condición operativa pendiente de diseño.**

Correcciones de usabilidad autorizadas por separado e implementadas sobre servicios existentes (DEV-19/DEV-26):

- **Datos del cliente al crear pedido:** precarga de contacto, teléfono y dirección, selección cuando hay varias opciones, revisión/corrección específica del pedido y protección ante respuestas antiguas. Se conserva su snapshot sin editar la ficha del cliente.
- **Existencias iniciales:** UI conectada a `InventorySetupService.InitializeAsync`, con producto/almacén consultado sin saldo, permiso de ajuste, costo, motivo y trazabilidad; SQL valida también la ausencia de historial. Conserva la recuperación con GUID y refresca existencias. No equivale a editar stock mínimo ni a completar compras/recepciones.

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
