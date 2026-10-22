# DEV-37 · Importaciones y costeo · 2.1B

22/10/2026 · Diseño previo a implementación. Fuentes: DM-01, DB-01, ARQ-01, IMP-01, INV-01, SEC-01 y DEV-33. El usuario confirmó repartir por valor neto y cerrar costos antes de aplicar la primera recepción. La siguiente fase funcional será 2.2A · posventa; DEV-36 conserva el frente de despliegue.

## Reglas aprobadas

Una importación por OC confirmada, sin recepciones vigentes ni unidades recibidas previamente. La compra, logística y recepción conservan entidades separadas. Courier/guía opcionales; seguimiento manual con eventos inmutables y sin integración DHL. Los estados logísticos avanzan hasta ADUANA; RECIBIDA_PARCIAL/RECIBIDA se derivan únicamente al aplicar cantidades físicas. Eventos y correcciones de guía/estimación no reescriben el historial. Una OC asociada no se cancela ni cambia su estado manualmente por el flujo de compra directa.

Gastos en moneda origen, tipo de cambio histórico USD por unidad (USD=1) y conversión monetaria a dos decimales. Corregir un gasto consiste en anularlo con motivo y registrar otro, antes de cerrar costos. No se elimina historial. El cierre requiere confirmación explícita y cambio real de la mercadería; congela costo unitario de compra USD y presupuesto de gastos por línea de OC. No se reabre ni se agregan/anulan gastos después del cierre; costos posteriores y recepciones con costo incompleto no son soportados por esta política, y deben resolverse antes de confirmar el cierre. La UI debe mostrar esa consecuencia.

Prorrateo: peso por valor neto de línea de OC. El cambio real común positivo conserva las proporciones; no se redondean los pesos individualmente al convertir, evitando perder bases pequeñas. Repartir centavos por restos mayores, desempate por Id de línea, suma exacta al total de gastos activos. Líneas gratuitas tienen peso cero; si toda la compra es gratuita sólo se cierra con cero gastos. No se usa cantidad como alternativa silenciosa. Cada parcial consume la diferencia entre el presupuesto acumulado redondeado de la cantidad recibida y el gasto ya aplicado; dividir entre lotes con el mismo reparto determinista. La última recepción consume el remanente exacto. Los borradores no reservan presupuesto: aplicar revalida las cantidades/gastos contra lo ya aplicado; un borrador desactualizado se anula y prepara de nuevo.

Se conserva el total monetario asignado a cada línea recibida y el costo adicional unitario redondeado a seis decimales. La diferencia `total asignado − cantidad × costo adicional unitario` queda consultable como residuo de conversión unitario, sin ocultarlo ni modificar gastos/ventas históricos; el promedio/Kardex mantienen la precisión de seis decimales vigente. El cierre y la recepción validan rangos antes de escribir.

## Migración 018 y contratos

Ampliaciones aditivas sobre tablas existentes: Importaciones agrega CostosCerrados, TipoCambioReal, TotalGastosUSD, FechaCierre y CerradoPor; GastosImportacion agrega Anulado; OrdenCompraDetalle agrega CostoImportacionUSD/GastoImportacionUSD congelados; RecepcionCompraDetalle agrega GastoImportacionTotalUSD. No cambia datos históricos ni crea tablas/procedimientos (76/8/18). Importaciones antiguas sin cierre continúan sin costos congelados; no se les inventa un cierre ni se reaplican recepciones históricas. Antes del cierre nuevo se rechaza cualquier recepción preexistente vigente. Definición incorporada como ampliación de DB-01 en este documento antes del DDL.

Operaciones nuevas con GUID, identidad normalizada, diario durable y auditoría atómica: guardar importación, evento/logística, agregar/anular gasto, cerrar costos y preparar recepción de importación. Consultas COMPRAS.VER; escrituras además COMPRAS.GESTIONAR y estación activa, revalidados en SQL. Bloqueo siempre desde OC, importación, recepción, productos y existencias. Misma recuperación de DEV-31; no se modifica la identidad serializada de solicitudes antiguas de compra directa.

UI integrada en la ficha de Compras, pestaña Importación: ficha/seguimiento, gastos e historial, cierre revisado y recepción por el formulario existente con costos calculados por servidor. Vista previa del reparto antes del cierre, cantidades/recibos consultables, Light/Dark y compacto; no SQL en WPF. La aplicación de recepción reutiliza el flujo transaccional de DEV-33, validando además cierre, presupuesto y origen de importación. Nada en tránsito se vende.

## Verificación prevista

Reglas puras de reparto/redondeo/parciales, validaciones/identidades; SQL real de creación, permisos, guía nullable, eventos inmutables, gastos/FX/anulación, cierre/reenvío/rollback, parciales multilote, dos estaciones compitiendo, saldo/gasto/costo/Kardex, rechazo de costos posteriores, conservación histórica/reaplicación; UI con selección/sesión tardía y solicitudes durables; WPF en ambos temas y tamaño compacto. No se opera sobre la base personal.

## Recorrido y actualización

Compras → abrir OC confirmada sin recepciones vigentes → **Importación** → **Crear importación**. Usa **Evento / logística** para agregar seguimiento y corregir courier/guía/fecha estimada. **Registrar gasto** guarda moneda/cambio histórico; **Anular gasto** conserva un error en historial sin sumarlo. **Cerrar costos** abre la revisión: indicar cambio real de mercadería, **Calcular reparto**, revisar importes, motivo y confirmar que no hay costos pendientes. El cierre no ingresa stock y es definitivo.

Después, **Preparar recepción** usa cambio/costos congelados (sin gasto manual), cantidades y varios lotes; **Recepciones → Consultar → Aplicar** ingresa stock/Kardex. Consulta en el detalle el gasto total asignado y su residuo frente al costo unitario. Un borrador anulado no consume gasto ni stock. Tras otra estación, actualizar y revisar antes de confirmar; si el presupuesto del borrador quedó obsoleto, anularlo y preparar otro. El historial de eventos tiene paginación.

Cerrar todas las estaciones. Desde PowerShell, en la raíz del repositorio:

```powershell
git fetch origin
git switch feature/desarrollo
git pull --ff-only origin feature/desarrollo
$servidor = Read-Host 'Servidor SQL guardado en Ajustes de conexión'
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --install-development --server "$servidor" .
dotnet run --project src/GoDental.Wpf -c Release
```

El instalador de desarrollo requiere `GoDental_Development` con marcador reconocido; conserva datos y debe mostrar `SQL 018_Importaciones_Costeo.sql`. No cambiar marcadores ni reutilizar esta herramienta sobre producción. Para otra base, revisar/aplicar en su contexto y con respaldo según DEV-36. Actualizar todas las estaciones. No se modificó la BD personal desde este entorno.

## Resultado verificado

**2.1B implementado y validado automáticamente** para importación manual, gastos por valor neto, costos cerrados y recepciones parciales. [CI Windows #136 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39805904810), código **`d17b23710133fa3d7c6acb0e585da5f9c523066e`**, job `119918912592`: Release **0 advertencias/errores**, **255 xUnit**, **520 comprobaciones SQL** (43 nuevas de importaciones) y **767 WPF**, sin errores de binding. Localmente también pasaron Release con Windows targeting y 255 xUnit. Los cambios posteriores a ese código sólo actualizan documentación.

SQL comprueba guía nullable, reenvíos, permisos/estación, gastos y cambio histórico, anulación conservando historial, vista de gastos activos, cierre congelado, rechazo de gastos posteriores, reparto exacto de centavos, parciales/multilote, concurrencia de aplicación, promedio/Kardex, rollback del cierre/auditoría, recuperación durable y reaplicación de 018. El smoke renderiza pestaña, formularios y cierre con revisión de costos en ambos temas y tamaño compacto, además de las regresiones existentes. No se modificó la base personal ni se probaron hardware o DHL real.

Sigue **2.2A · posventa** según DEV-30: devoluciones/anulaciones con autorización, referencia a venta original y reversos atómicos de inventario/caja/CxC. DEV-36 conserva antes del uso productivo el trabajo pendiente de respaldos, migrador e instalación. Costos incompletos/posteriores al cierre, cancelación/reversión de importaciones ya registradas, documentos de courier y cuentas por pagar requieren un diseño posterior; no se habilitan silenciosamente en este bloque.
