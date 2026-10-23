# DEV-30 · Revisión funcional y apertura de la segunda etapa

21/10/2026 · Revisión solicitada por el usuario. Código revisado: `feature/desarrollo`, `28ffc5f134f3504435b182429330616bd9817364`. Se incorpora después `df63c8b`, que sólo registra la validación de DEV-29 y no cambia el código revisado. Referencia integrada en `main`: `27c7080`; los refinamientos posteriores de DEV-23 a DEV-29 todavía están en desarrollo. No se revisan como actuales las antiguas puntas de `feature/ui`/`feature/logica`.

## Dictamen

Existe un MVP conectado para carga inicial → cliente → caja → cotización/POS → venta/cobro/crédito → pedido/entrega → comprobante → arqueo. No están terminados todos los módulos de DM-01. Conteos y las fichas de clientes/proveedores tienen su recorrido previsto en los incrementos implementado; eso no completa Compras, seguimiento de cobranza ni todo Inventario.

Se abre la planificación de la **segunda etapa: completar la operación y cerrar las excepciones del MVP**. No se declara aprobado un despliegue productivo. El primer bloque de implementación recomendado es la recuperación de operaciones inciertas; después, los complementos de los módulos actuales y compras/recepciones. Esta revisión no implementa esos pendientes ni cambia políticas de negocio.

## Fuentes y método

- DM-01 v0.2 y DB-01 v0.1 como fuentes de alcance y datos, ARQ-01, SEC-01, INV-01, IMP-01, VEN-01, VEN-01A, FIS-01 y ONL-01. VEN-01A prevalece para la precedencia comercial ya resuelta.
- Contratos, servicios, repositorios, scripts 001–014, navegación, ViewModels, vistas, registro de DI y suites de pruebas. DEV-09 a DEV-29 explican decisiones posteriores y el alcance de cada entrega.
- Comparación entre una función accesible desde WPF, una función con lógica sin interfaz y una estructura SQL sin caso de uso de aplicación. Una tabla o procedimiento existente no prueba que el módulo esté terminado.
- Revisión funcional y técnica transversal con comprobación de rutas y pruebas; no constituye certificación exhaustiva de cada campo/regla ni una auditoría integral de seguridad.
- Compilación/pruebas locales en Linux y consulta de CI Windows. No se conectó a la base del usuario, no se aplicó 014 en su PC ni se verificó su impresora o su LAN.

## Estado por módulo

«Implementado en su alcance» significa recorrido conectado del incremento documentado; no equivale a certificación en las estaciones del negocio. «Parcial» identifica complementos pendientes del mismo módulo.

| Módulo | Estado actual comprobado | Complementos respecto de la documentación |
| --- | --- | --- |
| Acceso, cuentas y usuarios | Login, primer administrador, contraseñas con hash, bloqueo, cambio/restablecimiento, roles de cuentas, código EMP y cierre normal de ventanas/host implementados. | Administración granular de roles/permisos, consulta de bitácora/estaciones y políticas finales de sesión. Identidad SQL de mínimo privilegio y despliegue LAN pendientes. |
| Productos y categorías | Altas/edición, desactivación, SKU automático, búsqueda, filtros y acceso al inventario conectados. | Mantenimiento de códigos de barras y etiquetas; gestión de otros catálogos que hoy sólo se consultan. Control operativo de series pendiente; POS rechaza productos serializados. |
| Inventario, lotes y Kardex | Saldo inicial **ya conectado**, ajustes, bajas, transferencias, bloqueo/reactivación de lotes, vencimientos, stock físico/vendible, costos por permiso y Kardex trazable. | Reposición comercial mediante compras/recepciones, etiquetas y reposición/alertas globales. Los ajustes no sustituyen la compra ni su costeo. |
| Conteos | Apertura, captura —vacío distinto de cero—, cierre con ajustes, cancelación, versiones y auditoría conectados. | No se identifica una pantalla central faltante del alcance DEV-09/12. Validar el uso con inventario real de prueba y movimientos simultáneos en las dos estaciones. |
| Proveedores | Ficha, contactos, referencias de productos, moneda/costo comercial y versiones conectados; UI refinada en DEV-29. | Órdenes, recepciones, importaciones y documentos de compra son casos de uso adicionales pendientes; no están implementados por existir la ficha del proveedor. |
| Clientes | Ficha, contactos, direcciones, datos fiscales/comerciales, configuración administrativa del crédito e historiales conectados. | Seguimiento de cobranza/avisos y estados de cuenta imprimibles; la política final de mora y excepciones sigue abierta. |
| Precios y promociones | Cálculo compartido de listas/segmentos/cantidades/montos/promociones/descuentos, autorización puntual y snapshots. | Pantallas/servicios de mantenimiento de listas, escalas y promociones. COMBO/OTRO y más de un impuesto aplicable se rechazan explícitamente en el MVP. La precedencia básica **ya está resuelta** por VEN-01A. |
| POS y cotizaciones | Buscar, calcular, cotizar, convertir una vez, cobrar, FEFO y protección de stock/precio concurrentes. Pedido → POS confirmado/enlazado atómicamente. | Recuperación terminal de solicitudes inciertas; registro manual de tarjeta; anulaciones/devoluciones; documento de cotización si se incorpora su emisión impresa. No se considera terminado todo VEN-01. |
| Caja y pagos | Catálogo de cajas con código automático, apertura, ingresos/retiros, pagos mixtos, cambio y cierre con versión/motivo conectados. | Tarjeta manual y conciliación, recibo independiente de abono/arqueo y política definitiva de diferencias que requieren administrador. |
| Crédito y abonos | Habilitación/límite/plazo/bloqueo por cliente, CxC, vencidos, abonos parciales y versiones. | Registrar/gestionar avisos periódicos y su seguimiento; estado de cuenta/recibo. Tener `ProximoAviso` no equivale a emitir o registrar recordatorios. No se inventa bloqueo automático por mora. |
| Pedidos y entregas | Alta con contacto/dirección precargados y snapshot, eventos, estados, enlace a venta y Pasar al POS; venta obligatoria para EN_RUTA/ENTREGADO. | Cobro contra entrega aún sin diseño. LISTO no exige venta en el código vigente y no significa reserva. No se presenta crédito comercial como sustituto del cobro contra entrega. |
| Comprobantes | PDF interno Carta, logo, snapshot de cliente/entrega, vista previa y guardado/diálogo de impresión del mismo documento. | Recibos de abono, estados de cuenta, documentos de compra y formatos posteriores. La cola/impresora física no está certificada por el smoke. No es un DTE. |
| Ajustes | Conexión local y apariencia conectadas; semilla conserva valores editables al repetirse. | Edición operativa de parámetros comerciales con permisos, validación conjunta, concurrencia y auditoría (DEV-22); no basta `EditableUI=1`. |
| Compras/recepciones/importaciones | Tablas y procedimiento base de recepción; ficha de proveedor disponible. | Faltan contratos/casos de uso, repositorios y pantallas para OC, recepciones parciales, logística, gastos y costo puesto en bodega. No hay un módulo completo de compras. |
| Devoluciones/anulaciones | Estructura SQL base. | Flujo autorizado desde venta, plazo/motivos/evidencia, evaluación de reingreso, reversos de inventario/caja/CxC y política de crédito a favor. No borrar ni simular una nueva venta negativa. |
| Dashboard/reportes | Vistas/procedimientos SQL de base y consultas operativas en módulos. | Dashboard con datos reales, reportes por período, margen/utilidad, rotación, inventario valorizado y compras. Inicio no es el dashboard previsto en DM-01. |
| Fiscal/DTE | Tablas base y servicio de registro fiscal pendiente, sin pantalla operativa de esa cola. | Contratos fiscales definitivos, UI de estados, firma/transporte/reintentos y requisitos vigentes. No hay emisión ni aceptación DTE real. |
| Online | Estructura SQL de cimientos. | Prospección, fuentes/deduplicación, conversión, supresión, campañas y proveedores autorizados. No hay módulo operativo ni Worker. |
| Respaldo y despliegue | Scripts de instalación/verificación para desarrollo y CI. | Backup SQL diario consistente, copia secundaria del .bak, retención y restauración comprobada; cuenta SQL de mínimo privilegio, red/TLS y actualización de estaciones. Un instalador de desarrollo no cubre estas funciones. |

## Hallazgos que condicionan el cierre

### R1 · Alta prioridad: falta salida terminal para una operación incierta

`MvpOperationModel.CheckAsync` sólo recibe recibo o ausencia; después de una excepción de transporte, `SendAsync` conserva la solicitud incluso si el reenvío es rechazado. El repositorio autoriza antes de consultar el recibo bajo `sp_getapplock`. Con una respuesta perdida y un permiso revocado o una autorización ya no utilizable, se puede quedar sin una resolución negativa segura; el espacio de trabajo bloquea otra escritura y el cierre mientras siga pendiente. Es una protección deliberada contra duplicados, pero falta completar la recuperación.

Evidencia: [modelo de operación](../src/GoDental.Wpf/ViewModels/Operations/MvpOperationModel.cs), [WriteCore/FindOperationAsync](../src/GoDental.Infrastructure/Operations/SqlMvpRepository.cs), [contrato pendiente en DEV-15](DEV-15-Coordinacion-UI.md). Las pruebas `LostResponseRetainsExactRequestAndIdAndDoesNotAutomaticallyRetry` y `RejectionOfRetryDoesNotEraseAnEarlierUncertainWrite` cubren la conservación, no una salida terminal negativa.

Cierre esperado: estado CONFIRMADA/EN_CURSO/RECHAZADA_DEFINITIVA con propiedad de usuario, identidad/contenido de solicitud y serialización bajo el mismo bloqueo. Una solicitud declarada definitivamente rechazada no podrá confirmarse después. Consultar no debe generar efectos comerciales. Rechazo por falta de permiso y ausencia en el ledger no son por sí solos prueba de resultado terminal.

### R2 · Alta prioridad antes del piloto: recuperación después de un cierre anormal

GUID, solicitud y callback se conservan en memoria; no hay un almacén de operaciones pendientes que se recupere al reiniciar. El cierre normal está protegido, pero un apagón o terminación del proceso puede perder esa referencia mientras SQL sí confirmó. Registrar una operación equivalente con un GUID nuevo dejaría de estar protegido por la idempotencia del anterior. No se afirma que las pruebas actuales hayan reproducido un cobro duplicado; se identifica el límite del diseño y de su cobertura.

Cierre esperado: diseñar y versionar persistencia mínima segura antes del envío, restauración por base/equipo/usuario y reconciliación al entrar. No conservar contraseñas de autorización, PAN/CVV ni secretos. Una operación recuperada de otra sesión no debe poder ejecutarse como otro usuario. Probar proceso interrumpido antes/después del commit y antes/después de guardar el recibo local.

### R3 · Diferencia funcional con DM-01: tarjeta manual bloqueada

DM-01 §12.5 exige que Tarjeta exista para **registrar** un pago desde la primera versión, mientras difiere la integración con terminal/procesador. El alcance reducido DEV-17 excluye ambas cosas: `PaymentMethodsAsync` filtra TARJETA, el POS repite el filtro y venta/abono rechazan ese método. La prueba SQL actual exige que no se anuncie. Esto no es un fallo de binding ni se resuelve contratando primero un conector.

Evidencia: [repositorio de ventas](../src/GoDental.Infrastructure/Operations/SqlMvpRepository.Sales.cs), [repositorio de caja](../src/GoDental.Infrastructure/Operations/SqlMvpRepository.Cash.cs), [POS](../src/GoDental.Wpf/ViewModels/Operations/PosViewModel.cs), [suite MVP](../tests/GoDental.DatabaseTests/MvpIntegration.cs).

Cierre esperado: diseñar registro manual de una operación ya autorizada externamente, con referencia y conciliación definidas; nunca simular autorización ni capturar datos sensibles de tarjeta. Separar expresamente ese registro de la integración futura. Actualizar pruebas, DEV-17 y la UI en el mismo incremento.

### R4 · Prioridad media: funciones configurables sin mantenimiento operativo

El motor consume listas, promociones y umbrales SQL, pero no existe el mantenimiento completo por servicios/UI. Los códigos de barras tienen escritura en `InventorySetupService.SaveBarcodeAsync` y búsqueda, sin gestor WPF ni etiquetas. Crédito muestra `NextNotice`, sin caso de uso para registrar el aviso. Las condiciones configurables y los datos disponibles no deben presentarse como procesos ya completados.

Cierre esperado: completar esos flujos por capas con permisos, versión/auditoría y pruebas de los efectos que consumen; conservar las decisiones pendientes que no estén aprobadas. DEV-22 ya define el siguiente incremento acotado de parámetros.

### R5 · Condición de puesta en operación: faltan respaldo/restauración y prueba de dos estaciones

SEC-01 y DM-01 §18.4 exigen backup diario y restauración. No se identifica implementación operativa de ese recorrido en scripts/servicios. La aplicación usa Windows integrada y su documentación reconoce pendiente la identidad SQL de mínimo privilegio. CI LocalDB prueba conexiones concurrentes, pero no conectividad, permisos/TLS ni recuperación en las dos máquinas reales.

Cierre esperado: procedimiento de respaldo/restauración probado en otra base; política explícita de retención y copia secundaria; matriz de acceso de la identidad SQL y recorrido entre estaciones sobre una base de aceptación separada. No habilitar una instancia a Internet ni sincronizar MDF/LDF activos.

### R6 · Documentación con estados históricos que parecían actuales

README/ROADMAP/DEV-15 aún mezclaban «pantallas pendientes» de DEV-17 con entregas posteriores y el saldo inicial ya conectado. ROADMAP repetía como pendiente la precedencia que VEN-01A resolvió. DB-01 v0.1 y sus 72 tablas son la baseline, mientras 012–014 dejan 76 tablas, 8 vistas y 18 procedimientos. Se actualizan los puntos de entrada, conservando el historial y sus límites.

La instrucción anterior de actualizar `feature/ui` quedó superada por DEV-08: la rama vigente es `feature/desarrollo`. La revisión no confirma qué versión/migraciones tiene instalada el usuario en su PC.

## Segunda etapa · orden y criterios de salida

Los bloques siguientes son un plan de trabajo, todos pendientes de implementación en esta revisión. No renumeran las nueve fases históricas de DM-01. UI y lógica se entregan juntas en `feature/desarrollo`.

| Bloque | Entrega acotada | Criterios verificables para darla por terminada | Dependencias/decisiones |
| --- | --- | --- | --- |
| 2.0A · Recuperación | Resolver R1 y recuperar pendientes tras reinicio (R2). | Respuesta perdida, permiso revocado, petición aún en curso, rechazo definitivo y proceso interrumpido; mismo GUID/contenido, sin duplicación; SQL y UI permiten continuar sólo con resultado seguro. | Versionar primero contrato/estados, cambios SQL aditivos y almacenamiento local seguro. No implementar un botón que simplemente descarte el GUID. |
| 2.0B · Completar módulos actuales | Parámetros consumidos, mantenimiento de códigos de barras y registro manual de tarjeta. | Edición autorizada con conflicto entre estaciones y auditoría; búsqueda por código asignado; referencia de tarjeta y pagos mixtos sin alterar efectivo de caja. | Semántica de permisos con nombres 7/35 (DEV-22), formato de etiqueta y conciliación de tarjeta. No exponer claves sin uso. |
| 2.1A · Compras y recepción | Orden de compra → recepción parcial → confirmación → saldo pendiente/estado. | Cantidad pendiente calculada en servidor, rechazo de sobre-recepción concurrente, lotes/fechas, costo y promedio a seis decimales, Kardex/auditoría; fallo revierte todo; pantalla y pruebas SQL de principio a fin. | Revisar IMP-01 y el procedimiento base antes de reutilizarlo; definir estados/moneda/tipo de cambio y qué costos deben estar completos para confirmar. No convertir ajustes en compras. |
| 2.1B · Importaciones y costo | Eventos logísticos manuales, gastos y distribución a recepciones. | Conciliación de gastos/redondeo, tipo de cambio histórico, trazabilidad por lote y stock sólo al recibir. Guía nullable y flujo funcional sin DHL automático. | Cerrar prorrateo definitivo y recepción con costos incompletos; no inventar cuentas por pagar ni documentos sin diseño. |
| 2.2A · Posventa | Devoluciones y anulaciones con reversos explícitos. | Venta original inalterable, cantidades/plazo, autorización, evidencia, reingreso vendible o cuarentena; efectos atómicos en inventario, caja/CxC e historial. | Política de crédito a favor, evidencias compartidas y tratamiento de entregas fallidas. |
| 2.2B · Cobranza/documentos | Seguimiento de avisos, recibo de abono y estado de cuenta. | Avisos registrados sin duplicación, fecha/usuario, paginación e impresión coherente con saldos históricos; no se envían mensajes automáticamente. | Cerrar mora/excepciones antes de automatizar bloqueos. |
| 2.2C · Entregas | Cobro contra entrega si sigue siendo necesario. | Salida/unidades protegidas, entrega fallida/reingreso y cobro de reparto conciliados sin consumir crédito comercial por defecto. | Diseño de DEV-17/ROADMAP obligatorio antes de modificar estados, pagos o reservas. |
| 2.3 · Reportes e inicio útil | Ventas por período, margen con costo histórico, inventario valorizado, stock crítico/vencimientos y compras. | Datos SQL reales, filtros/fecha/permisos y totales conciliados con los módulos; sin costos para usuarios sin permiso. | Reportes de compra/margen confiables después de cerrar costeo; acordar indicadores, no inventar cifras. |
| 2.4 · Piloto operativo | Respaldo/restauración, acceso SQL, actualización, impresora/DPI y dos estaciones. | Restaurar .bak en otra base; verificar stock/caja/CxC tras ventas concurrentes y cortes; recuperación de pendientes, impresión y cierre; acta de aceptación con versión y migraciones. | Definir instancia/red/TLS/identidades, destino/retención y hardware. Preparar respaldo desde el inicio, no esperar al día del despliegue. |
| Después · Integraciones | Fiscal/DTE, Online y proveedores externos. | Contratos/versiones vigentes, entornos separados y pruebas de reintento/errores sin duplicar negocio. | Datos fiscales y proveedores oficiales autorizados. Worker sólo cuando exista tarea que deba correr con WPF cerrado. |

El primer incremento queda acotado a **2.0A**. La aceptación de módulos nuevos no sustituye la validación de los existentes. Cada incremento debe cerrar diseño, SQL/contratos cuando correspondan, servicio, UI, permisos, versiones, auditoría, errores y pruebas; no dejar nuevamente una interfaz sin conectar.

## Avance posterior

El usuario autorizó continuar el orden propuesto. [DEV-31](DEV-31-Recuperacion-Operaciones.md) implementa 2.0A para los hallazgos R1/R2; sus decisiones y validación se registran por separado. Los estados y resultados anteriores corresponden a la revisión original, no afirman que se implementaron los demás bloques.

## Validación de esta revisión

- `dotnet build GoDental.sln -c Release -p:EnableWindowsTargeting=true`: correcto, **0 advertencias y 0 errores**, SDK 8.0.426.
- `dotnet test tests/GoDental.Tests/GoDental.Tests.csproj -c Release --no-build --no-restore`: **173 aprobadas**, 0 fallidas/omitidas. Incluye PDF real; no ejecuta WPF ni SQL Server en Linux.
- `EnableWindowsTargeting=true dotnet list GoDental.sln package --vulnerable --include-transitive --format json`: NuGet no reportó vulnerabilidades conocidas en los siete proyectos. Es una consulta de dependencias, no una auditoría completa de seguridad.
- [CI Windows #98](https://github.com/Gust4av033/GoDental/actions/runs/39637090540), **correcto sobre el mismo `28ffc5f`**: Release con 0 advertencias/errores; **173 xUnit**, **374 comprobaciones SQL** (40 inventario, 51 seguridad/catálogo, 67 conteos, 38 proveedores, 33 clientes, 127 MVP y 18 códigos automáticos) y **570 comprobaciones WPF**, sin errores de binding. Se consultaron el resultado y el registro del job `119350436480`; no se atribuyen cifras de una revisión anterior al código actual. El smoke incluye salida de procesos de acceso y espacio de trabajo. Las capturas automáticas no sustituyen inspección del monitor/DPI físico.
- La instalación de 014 en la base personal y el recorrido manual en dos estaciones continúan **sin evidencia de ejecución en este chat**. Las pruebas SQL del runner utilizan bases aisladas.

[DEV-32](DEV-32-Complementos-MVP.md) implementa el bloque 2.0B: parámetros consumidos, mantenimiento de códigos y registro manual de tarjeta, junto con adaptación del logo al tema. El siguiente bloque es 2.1A, compras y recepciones. Etiquetas físicas, series, conciliación y terminal bancaria conservan sus decisiones pendientes. Consultar DEV-32 para resultados de validación del incremento.

El usuario autorizó 2.1A: [DEV-33](DEV-33-Compras-Recepciones.md) registra diseño, implementación, recorrido y validación de compras directas/recepciones con costo completo. La siguiente fase sigue siendo 2.1B; su prorrateo y tratamiento de costo incompleto no se dan por resueltos. La impresión configurable solicitada se implementa como complemento separado en DEV-34, después de compras directas y antes de continuar 2.1B.

Continuación 22/10/2026: [DEV-37](DEV-37-Importaciones-Costeo.md) implementa 2.1B (018), con reparto por valor neto y cierre antes de recibir confirmados por el usuario. CI #136: 255 xUnit, 520 SQL y 767 WPF. Sigue 2.2A; DEV-36 mantiene pendiente el despliegue/respaldo para uso productivo.

Continuación 23/10/2026: DEV-38 implementa **2.2A** con migración 019. El usuario confirmó deuda
primero/efectivo después, carpeta central y anulación también en ruta/entregado. Se conservan
originales y entrega histórica, con compensaciones atómicas y recuperación. CI #142 correcto: 272 xUnit, 574 SQL y 817 WPF.
La siguiente fase funcional es **2.2B: cobranza y documentos de crédito**. Saldo a favor reutilizable,
DTE/Online, entregas fallidas/COD y despliegue productivo de DEV-36 conservan su alcance pendiente.

Paréntesis solicitado 23/10/2026: **DEV-39**, captura/localización de datos y categorías, con
migración **020**. El usuario confirmó 14 departamentos, 44 municipios y distrito opcional.
Se atiende después de 2.2A y antes de 2.2B; no cambia la secuencia funcional pendiente.

DEV-39 cerrado: [CI Windows #149](https://github.com/Gust4av033/GoDental/actions/runs/39883444740)
valida `8c3a54e`: Release 0 advertencias/errores, **300 xUnit / 594 SQL / 872 WPF**.
Migración 020 preparada; aplicación en la BD personal y comprobación manual entre estaciones
siguen siendo pasos locales. Continuar por **2.2B**, sin repetir el paréntesis de formularios.

Continuación 22/10/2026 (fecha local El Salvador): **DEV-40**, 2.2B, seguimiento de avisos,
recibo independiente de abono y estado de cuenta con efectos de posventa. Migración 021:
77 tablas / 8 vistas / 18 procedimientos; nuevos snapshots no rellenan históricos.
Sigue 2.2C si se requiere entrega contra cobro, después de definir el flujo, y luego 2.3 reportes.

DEV-40 cerrado en su alcance implementado: [CI Windows #155](https://github.com/Gust4av033/GoDental/actions/runs/39904041587)
valida `987b98b`, con Release sin advertencias/errores y **315 xUnit / 627 SQL / 897 WPF**.
Incluye concurrencia/rollback de avisos, snapshots de abonos, estado con posventa y formulario
con reintento del mismo GUID; vista previa/guardado/impresión en ambos temas y compacto.
Aplicar 021 a la BD personal y comprobar el recorrido entre estaciones siguen siendo pasos
locales pendientes; el CI utiliza SQL aislado. La continuación conserva 2.2C (diseño COD)
y después 2.3; no se abre automáticamente otro módulo ni se declara listo el despliegue DEV-36.

Continuación autorizada 22/10/2026 (El Salvador): [DEV-41](DEV-41-Reparto-ContraEntrega.md)
implementa **2.2C**, entrega/retorno completos, efectivo o transferencia con rendición,
y [DEV-42](DEV-42-Reportes-Operativos.md) implementa **2.3**, reportes por período/permisos
con costo histórico y compensaciones de posventa. Migraciones **022–023**, 79 tablas / 8 vistas
/ 18 procedimientos. No se aplica SQL a la base personal desde cloud. La siguiente etapa es
**2.4** y la implementación de DEV-36 para instalación/actualizaciones/respaldos y piloto.
