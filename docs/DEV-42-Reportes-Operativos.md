# DEV-42 — 2.3: reportes operativos e Inicio

Fecha local: 22/10/2026 (El Salvador). **Estado: implementado y validado automáticamente.**
Aceptación entre estaciones/impresora y migración local pendientes. Diseño registrado antes del SQL.
Fuentes: DM-01 §14.1, DB-01 snapshots de venta/costo y recepciones, ARQ-01, SEC-01, DEV-30/38/41.

## Indicadores definidos

- Fechas inclusivas de El Salvador, convertidas a intervalo UTC [inicio, día posterior al fin).
  Máximo 366 días por consulta. Inventario es una fotografía actual, no un cierre histórico.
- Ventas: documentos confirmados o con posventa, conservando su cargo original y registrando
  devoluciones/anulaciones APLICADAS como compensaciones en la fecha de aplicación. No
  excluir la venta original por su estado actual ni retrotraer la devolución a la fecha de venta.
  Entrega se muestra separada de mercadería. No mezclar cobros con ingresos por ventas.
- Margen bruto de mercadería: total de líneas sin impuesto menos costo histórico vendido,
  menos devolución proporcional de base y más reversión de costo sólo si vuelve inventario.
  El impuesto devuelto usa diferencias de proporción acumulada: el último parcial consume
  el remanente exacto de centavos, incluyendo parciales de períodos anteriores.
  Producto dañado/no reingresado conserva costo consumido. No se llama utilidad neta:
  no descuenta gastos generales, sueldos, impuestos de renta ni costos de reparto no registrados.
- Agrupación por documento/día, producto, categoría y cliente; categoría actual se identifica
  como referencia actual (no existe snapshot histórico). Todos los grupos concilian con el total.
- Inventario por almacén/producto: físico, reservado, disponible vendible, mínimo, costo promedio,
  valor de bodega y custodia de reparto al costo histórico. Incluye productos sin saldo; no
  inventa stock negativo. Próximos a vencer (30 días) y vencidos muestran lote/almacén/unidades.
  Recomendación determinista: reponer si vendible <= mínimo, revisar salida si próximo a vencer;
  sin inventar demanda futura. Rotación inicial = unidades netas vendidas en período; no se
  presenta como índice contable ni días de cobertura sin denominador histórico fiable.
- Compras: sólo recepciones APLICADAS en fecha real de recepción, unidades, mercadería USD,
  gasto asignado USD y valor puesto en bodega por proveedor/documento. Órdenes no recibidas
  no son stock ni gasto realizado. No suma gasto total de importación otra vez a sus recepciones.
- Permisos existentes REPORTES.VENTAS/INVENTARIO/COSTOS y COMPRAS.VER. Costo/margen/valor
  exige además INVENTARIO.COSTOS_VER; SQL omite importes protegidos cuando no se conceden.
  Compras/costos no se ofrecen por tener sólo permiso de ventas. Session revision protege
  respuestas tardías; consultas SQL consistentes y parametrizadas, paginación de resultados.
- UI con filtros y totales completos; PDF interno con medidas configurables, vista previa y
  guardado/impresión del mismo documento. Límite explícito de 5000 filas y 40 páginas; rechazar
  antes de entregar documentos parciales. No exportar costos ocultos por una ruta alternativa.
- Inicio agrega un resumen diario permitido y acceso a reportes; conserva consultas operativas.
  No agrega DTE, IA, recomendaciones predictivas, Online ni contabilidad general.

Migración 023: añade `cmp.RecepcionesCompra.FechaAplicacionMvp datetimeoffset(0) NULL`;
las nuevas aplicaciones congelan esa fecha. Recepciones anteriores usan FechaRecepcion y se
identifican como históricas sin fecha de aplicación disponible, sin rellenarlas.
No requiere cambiar hechos históricos ni nuevas tablas. Continúa con **2.4**: aceptación real
entre estaciones, respaldo/restauración de BD y evidencias, despliegue/actualizaciones de DEV-36,
identidad SQL de mínimo privilegio y prueba de impresión cuando haya equipo.

## Uso y aceptación

Reportes en el menú o desde Inicio. Seleccionar tipo, período (máximo 366 días) y agrupación;
Consultar reporte. Las filas se paginan de 30 en 30 y los totales abarcan la consulta completa.
Cambiar filtros limpia el resultado anterior. Seleccionar papel/medidas → Generar PDF → revisar
páginas → Guardar PDF o Imprimir. Cambiar medidas o filtros invalida el documento anterior.
Para PDF se requiere además VENTAS.VER + VENTAS.REIMPRIMIR, mediante los servicios de impresión
internos ya existentes; consultar un reporte no concede permiso de imprimir.

En ventas, los grupos se distinguen por ID del producto/categoría/cliente, aunque compartan
nombre; la etiqueta conserva la referencia legible. Rotación inicial significa unidades netas
vendidas; no es el índice financiero de rotación ni una predicción. Seleccionar un período con
venta y devolución y comparar total, entrega, impuesto y costo histórico. En inventario, comparar
bodega y custodia con los módulos y comprobar que un usuario sin costos no vea valores/márgenes.
En compras, las recepciones en borrador no cuentan; las aplicadas incluyen gastos asignados una
sola vez. La fecha real de aplicación sólo existe para aplicaciones posteriores a 023; los
históricos se identifican y no se rellenan.

## Verificación y continuación

- Código validado: `287557d7919eb8dad7e28f4de3a40cbafb8778c1` en `feature/desarrollo`.
  [CI Windows #163](https://github.com/Gust4av033/GoDental/actions/runs/39908151107)
  completado correctamente; registro del job `120264859605` consultado.
- Release con **0 advertencias / 0 errores**, **335 xUnit**, sin fallos ni omitidas.
  Build Release y las mismas 335 pruebas pasan también localmente con Windows targeting.
- **687 comprobaciones SQL** en bases aisladas: 60 nuevas de reparto/reportes y las 627
  de regresión existentes. Incluye doble despacho/rendición, caja sin turno previo con custodia,
  turno cerrado/nuevo turno, transferencia sin efectivo, retorno completo y lote bloqueado en ruta,
  fallo de auditoría con rollback/recuperación, permisos revocados, fechas en medianoche local,
  devoluciones reales con impuesto por centavos, recepción aplicada/histórica y reaplicación 022/023.
- **949 comprobaciones WPF**, sin errores de binding. Inicio vuelve a caber en 1366×768;
  reportes de los cuatro tipos, formularios de reparto y estados de rendición/custodia en
  Light/Dark/compacto. PDF real, guardar/imprimir el mismo documento y bloqueo tras cambiar medidas.
- No se aplicaron 022/023 a la BD personal ni se ejecutó aceptación manual entre las dos PCs.
  El smoke automatizado y su impresión simulada no certifican impresora, monitor/DPI o LAN físicos.


Sigue **2.4: piloto y preparación de despliegue**: ejecutar aceptación entre las dos PCs sobre
una base de prueba, implementar DEV-36 (versiones/migrador/compatibilidad, instalación del motor
SQL sin SSMS, respaldo/restauración de BD y evidencias), definir identidad SQL y verificar LAN/TLS.
Fiscal/DTE y Online conservan etapas posteriores; no se habilitan por terminar estos reportes.
