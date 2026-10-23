# Roadmap y decisiones pendientes

## Estado vigente · revisión del 21/10/2026

[DEV-30 · Revisión funcional y segunda etapa](DEV-30-Revision-Fase-2.md) compara el código `28ffc5f` con DM-01 y los documentos funcionales. Incluye matriz por módulo, hallazgos, pruebas y criterios de salida. La rama activa es **`feature/desarrollo`**; `feature/ui` y `feature/logica` son históricas (DEV-08).

El MVP tiene UI conectada para productos/inventario **con existencias iniciales**, conteos, proveedores, clientes, caja, POS/cotizaciones, crédito/abonos, Pedido → POS/entrega y PDF con vista previa/impresión. El cierre normal de acceso y ventana principal está probado. Esto no completa todos los módulos del Documento Maestro ni certifica el uso productivo.

La **segunda etapa** comienza por resolver operaciones inciertas y recuperar pendientes después de un reinicio; después completa códigos de barras, parámetros y tarjeta manual. Continúa con compras/recepciones/importaciones y su costeo, posventa/cobranza, reportes y piloto entre estaciones. Los criterios y dependencias están en DEV-30. Fiscal/DTE y Online conservan su etapa posterior y sus requisitos externos. Preparar respaldo/restauración y despliegue desde el inicio, con validación antes del piloto.

CI Windows #98 del código revisado: **173 xUnit, 374 comprobaciones SQL y 570 WPF** correctas, compilación Release sin advertencias/errores. No confirma instalación/migraciones en la PC del usuario ni prueba manual entre dos estaciones.

El primer bloque se implementa en [DEV-31](DEV-31-Recuperacion-Operaciones.md), con migración 015: recuperación terminal y diario previo al envío. Los complementos de **2.0B** se implementan en [DEV-32](DEV-32-Complementos-MVP.md), con migración 016. 2.0B pasó CI Windows #110 sobre `f34b1a3` (202 xUnit, 438 SQL y 593 WPF). El bloque **2.1A: compras y recepción** se implementa en [DEV-33](DEV-33-Compras-Recepciones.md), con migración 017. [DEV-37](DEV-37-Importaciones-Costeo.md) implementa **2.1B: importaciones y costeo**, con reparto por valor neto y costos cerrados antes de recibir, aprobados por el usuario. [DEV-38](DEV-38-Posventa.md) implementa **2.2A: posventa** (019), con reintegro en efectivo tras reducir deuda y evidencias en carpeta central. Sigue **2.2B: cobranza y documentos de crédito**, implementado en [DEV-40](DEV-40-Cobranza-Documentos.md) (021). [DEV-34](DEV-34-Impresion-Formatos.md) implementa el complemento de impresión configurable; CI Windows #128 pasó con 241 xUnit, 477 SQL y 725 WPF. 2.0A pasó CI Windows #106 sobre `c3d3445` (187 xUnit, 399 SQL y 575 WPF), con detalle en DEV-31; no completa los bloques de compras/posventa/reportes ni el piloto.

## Secuencia de entrega

| Bloque | Estado | Referencia |
| --- | --- | --- |
| 2.0A · Recuperación | Implementado y validado automáticamente | DEV-31, migración 015, CI #106 |
| 2.0B · Complementos MVP | Implementado y validado automáticamente | DEV-32, migración 016, CI #110 |
| 2.1A · Compras y recepción directa | Implementado y validado automáticamente | DEV-33, migración 017, CI #116: 218 xUnit / 477 SQL / 625 WPF |
| 2.1B · Importaciones y costeo | Implementado y validado automáticamente | DEV-37, migración 018, CI #136: 255 xUnit / 520 SQL / 767 WPF |
| 2.2A · Posventa | Implementado y validado automáticamente | DEV-38, migración 019, CI #142: 272 xUnit / 574 SQL / 817 WPF |
| 2.2B · Cobranza y documentos | Implementado; CI #155 correcto, detalle en DEV-40 | Avisos, recibo de abono y estado de cuenta con posventa trazable (021) |
| Impresión configurable | Implementado y validado automáticamente; hardware pendiente | DEV-34, medidas en mm, CI #128 |
| 2.2C · Reparto contra entrega | Implementado; validación en DEV-41 | Custodia, entrega completa, incidencia/retorno y rendición de efectivo/transferencia (022) |
| 2.3 · Reportes operativos | Implementado; validación en DEV-42 | Ventas/posventa, margen histórico, inventario/custodia, vencimientos y compras aplicadas (023) |
| Instalador y actualizaciones | Diseño propuesto; implementación pendiente antes del piloto productivo | DEV-36: motor SQL, historial, compatibilidad y backups |

DEV-36 define la preparación del despliegue antes de empezar a conservar registros productivos: una base central, instalador principal/cliente, migraciones con historial, respaldo y compatibilidad. No está implementado aún y no reemplaza la secuencia funcional de DEV-30.

La aceptación física entre dos estaciones y hardware no se sustituye por estas pruebas automáticas. Las fases posteriores conservan el orden de DEV-30.

## Fases históricas de DM-01

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
- Fase 3: catálogo, lotes, saldos, movimientos y seguridad operativos (DEV-03). Conteos agregan contratos/pruebas en DEV-09 y pantalla en DEV-12. Existencias iniciales ya están conectadas (DEV-26). Códigos ya tienen mantenimiento en DEV-32. Series, etiquetas y reposición avanzada conservan sus pendientes.
- Fase 4: proveedores, contactos y referencias comerciales tienen lógica/persistencia (DEV-10) e interfaz (DEV-12/29). Órdenes y recepciones directas se conectan después en DEV-33. Importaciones y su prorrateo siguen pendientes; el MVP de ventas se adelantó como recorrido mínimo. Prorrateo definitivo y recepciones con costo incompleto conservan sus decisiones de IMP-01.

## Primer MVP operativo · incremento de lógica

El ciclo priorizado es existencias iniciales/ajustes → clientes → cotización → venta → cobro/crédito → pedido/entrega → arqueo. DEV-17 documenta su implementación en `feature/logica`, los contratos para `feature/ui`, las pruebas y sus límites. Esto adelanta un recorrido mínimo de operación; no declara completas todas las fases del roadmap. Las interfaces de conteos, proveedores y clientes ya existen en la base común.

La suite SQL nueva pasó en CI Windows junto con regresiones y smoke WPF (DEV-17). Caja, POS/cotizaciones, crédito/abonos, pedidos/entregas y PDF interno están conectados (DEV-19); DEV-26 agrega existencias iniciales y Pedido → POS, y DEV-27 vista previa/impresión. La recuperación completa se implementa en DEV-31; códigos de barras, parámetros y tarjeta manual en DEV-32. Para cerrar la aceptación operativa queda la validación manual en dos estaciones. Fiscal pendiente conserva UI pendiente. Compras/recepciones/importaciones, reportes avanzados, devoluciones y conectores requieren incrementos con decisiones explícitas. La segunda etapa de DEV-30 no sustituye la numeración histórica de DM-01.

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

Bloqueo por mora y excepciones; costos posteriores al cierre y recepción con costos incompletos (no admitidos en 2.1B); series de productos; conciliación del registro manual y terminal de tarjeta; ubicación compartida de evidencias; datos fiscales; DTE; canales Online autorizados; instancia SQL y red; retención y restauración de backups; plantillas restantes. La precedencia y acumulación básica ya están resueltas en VEN-01A; combos y reglas no soportadas siguen como ampliación. La marca del usuario ya se incorporó en DEV-28.

DB-01 v0.1 documenta la baseline de 72 tablas, ya materializada en 001–008. Las ampliaciones aprobadas en DEV-17/20 y 012–014 dejan 76 tablas, 8 vistas y 18 procedimientos. Cada cambio posterior requiere diseño/migración aditiva; tener DDL no implica implementar un módulo ni aprobar decisiones comerciales pendientes.

## Complemento de captura · 23/10/2026

[DEV-39](DEV-39-Formularios-Datos.md) añade país de proveedor, teléfono nacional/internacional
con prefijo opcional, máscaras DUI/NIT (incluido DUI como NIT), direcciones de 14 departamentos /
44 municipios con distrito opcional, reutilización de contactos y alta directa de categorías.
Migración 020 aditiva para distrito y snapshot de pedido; conserva datos históricos sin convertirlos.
Paréntesis después de 2.2A; la continuación funcional es **2.2B: cobranza y documentos de crédito**.

DEV-40 continúa la secuencia después del paréntesis de formularios: cobranza, recibos y estados
de cuenta. 021 deja 77 tablas / 8 vistas / 18 procedimientos. Después corresponde **2.2C**
si se confirma cobro contra entrega (diseño pendiente), y luego **2.3**, reportes. No automatizar
bloqueos por mora ni enviar avisos externos por inferencia de esta fase.

2.2B validado sobre `987b98b`: [CI Windows #155](https://github.com/Gust4av033/GoDental/actions/runs/39904041587),
Release sin advertencias/errores, **315 xUnit / 627 SQL / 897 WPF**. DEV-40 deja los pasos
de actualización y aceptación local; 021 no se ha aplicado desde cloud a la BD personal.

Continuación autorizada 22/10/2026 (El Salvador): **DEV-41/42** implementan 2.2C y 2.3.
El usuario confirmó entrega/retorno completos y efectivo o transferencia con rendición.
022 añade custodia de reparto (79 tablas / 8 vistas / 18 procedimientos); 023 congela fecha
de aplicación de nuevas recepciones. Sigue **2.4: piloto entre estaciones y despliegue DEV-36**,
con respaldos/restauración, instalador/actualizaciones y compatibilidad antes de uso productivo.
