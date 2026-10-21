# DEV-33 · Compras y recepciones · etapa 2.1A

Diseño previo a la implementación. Continuación autorizada de DEV-30/32 en `feature/desarrollo`. Fuentes: DM-01 §§10–11, DB-01 compras, ARQ-01, IMP-01, INV-01, SEC-01 y UX-01.

## Alcance y estados

Orden nueva en BORRADOR, correlativo ORDEN_COMPRA automático, proveedor/moneda activos, fecha, tipo de cambio estimado (USD por unidad de moneda), hasta 100 productos distintos con cantidad a cuatro decimales, costo a seis y descuento monetario a dos. Subtotal/total se recalculan en servidor con redondeo comercial; no son datos confiados a WPF. Se edita sólo BORRADOR, con rowversion y motivo. Transiciones: BORRADOR → ENVIADA/CONFIRMADA/CANCELADA; ENVIADA → CONFIRMADA/CANCELADA; CONFIRMADA → EN_PRODUCCION/DESPACHADA/CANCELADA; EN_PRODUCCION → DESPACHADA/CANCELADA. No se cancela una orden con unidades recibidas. RECIBIDA_PARCIAL y RECIBIDA son derivados de recepciones aplicadas, nunca estados manuales. Se admite recepción desde CONFIRMADA/EN_PRODUCCION/DESPACHADA/RECIBIDA_PARCIAL.

## Recepción

Crear borrador de recepción con orden/version, almacén, tipo de cambio real histórico, líneas referidas al detalle de OC y motivo. USD exige tipo de cambio 1; otras monedas requieren valor positivo explícito. Una línea puede repetirse con lotes distintos; se suma por detalle para comprobar el pendiente. El servidor obtiene producto/costo/descuento de la OC; calcula costo de compra USD a seis decimales. Gasto adicional USD por unidad es explícito y conocido para compra directa; no hay un prorrateo automático. Costo puesto en bodega = compra USD + gasto unitario. Se conserva snapshot del nombre/SKU de OC para que la historia no cambie con el catálogo.

Confirmar costo completo es una declaración explícita del responsable: incluye mercadería y todos los gastos atribuibles conocidos. Un borrador con costo incompleto no se aplica ni genera stock. No se admite asociar importaciones ni recibir una OC que ya tenga importación: ese flujo espera 2.1B y el diseño del prorrateo; no se declara resuelto el costo incompleto. Se puede anular un borrador sin efectos para corregirlo y crear otro; una recepción APLICADA es inmutable y requiere el futuro flujo de reversos, no un DELETE.

Aplicar exige versiones vigentes de orden y recepción. Bajo bloqueo de orden, luego recepción, productos en orden creciente y existencias: revalidar estado, pendiente agregado por detalle, producto/unidad, costos, almacén, lotes y fechas; crear lotes con vínculo al detalle de recepción, stock agregado y por lote, promedio ponderado móvil con seis decimales, Kardex COMPRA con usuario/equipo/referencia y actualizar cantidades/estado/rowversion de OC y recepción. No admite productos serializados hasta diseñar su control. Vencidos se rechazan; fabricación no puede superar vencimiento. Cada recepción puede incorporar varios lotes del mismo producto; un número repetido en otra recepción conserva su origen/costo por detalle y no fusiona las entradas; si ese número está bloqueado, se rechaza la recepción. No cambia costo histórico de entradas ni ventas.

## Persistencia y recuperación

017 amplía el CHECK de estados de OC con RECIBIDA_PARCIAL; agrega SKU/Nombre snapshot al detalle de OC, y RowVersion/TipoCambioReal/CostosCompletos a la recepción, FechaFabricacion a su detalle. Backfill de snapshots existentes sin borrar historia; las recepciones históricas aplicadas no se reaplican. No crea tablas ni procedimientos: 76/8/18. La aplicación usa las tablas existentes y transacciones Dapper; no reutiliza `cmp.sp_AplicarRecepcionCompra` sin guardas, porque la base no valida permisos/equipo, series/fechas/costos completos y no actualiza versión/estado parcial de OC de esta entrega. El procedimiento histórico queda para compatibilidad, fuera de la ruta nueva.

Nuevos tipos del ledger: GUARDAR_OC, ESTADO_OC, PREPARAR_RECEPCION, APLICAR_RECEPCION, ANULAR_RECEPCION. Mismo contrato durable de DEV-31: reserva local antes del envío, GUID/huella normalizada, recibo único, rechazo terminal y recuperación al reiniciar. Resultado incierto no crea otra recepción ni movimiento. Todas las escrituras requieren COMPRAS.VER + COMPRAS.GESTIONAR y estación activa, revalidados en SQL; las consultas requieren COMPRAS.VER. Los importes de compra son información del módulo autorizado, sin ampliar el permiso de costos de Inventario. Cada operación guarda auditoría atómica de actor/equipo/motivo/datos; permisos revocados impiden nuevos efectos y permiten recuperar recibo propio ya confirmado.

## Interfaz y verificación

Compras y recepciones es una página del menú junto a Proveedores: lista paginada/filtrada, detalle con cantidades ordenadas/recibidas/pendientes, recepciones con líneas/costos/lotes, y diálogos de alta/edición, cambio de estado, preparación, aplicación y anulación de borrador. WPF no ejecuta SQL. Estados vacío/carga/error, Light/Dark y pantalla compacta. Edición ocurre en modales; consulta permanece en página. Las operaciones activas bloquean salida; pendientes durables siguen el centro global de recuperación.

Pruebas: estados, costos/precisión y permisos; SQL real para parciales/completa, dos estaciones compitiendo, exceso, multi-lote/FEFO, costo promedio con stock anterior y cero, FX, inventario sin fila previa, rollback de auditoría, reenvío/recuperación, permiso revocado, datos históricos, reaplicación de 017; ViewModels para selección/sesión tardía y solicitudes conservadas; smoke WPF de listas/detalle/formularios, ambos temas y tamaño compacto.

## Secuencia siguiente

Después de validar 2.1A: 2.1B · importaciones, eventos manuales, gastos y costeo/prorrateo. Antes deben definirse reparto/redondeo, tratamiento de costos posteriores y recepción con costos incompletos. Después continúan 2.2A posventa, 2.2B cobranza/documentos, 2.2C contra entrega, 2.3 reportes y 2.4 piloto según DEV-30. La solicitud de etiquetas y formatos de impresión se registra separadamente en DEV-34; no se confunde con cerrar compras.
