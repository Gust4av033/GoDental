# DEV-38 — 2.2A · Devoluciones y anulaciones

**Fecha:** 23/10/2026 · **Estado:** implementado y validado automáticamente.

Fuentes: DM-01 §12.7, DB-01 devoluciones/costeo, VEN-01/VEN-01A, INV-01,
SEC-01 y ARQ-01. Continúa DEV-37; no cambia costos de importaciones cerradas.

## Reglas y decisiones

- Solicitud dentro del plazo `Devolucion.PlazoHoras` (24 por defecto), usando hora SQL;
  comprobante verificable, motivo, venta y detalle original obligatorios. No se recalculan precios.
- Sólo administrador vigente autoriza/rechaza con reautenticación puntual. La solicitud es
  inmutable; una corrección requiere rechazarla y presentar otra. Solicitar/rechazar no mueve dinero ni stock.
- Confirmado por el usuario el 22/10/2026: reducir primero la deuda pendiente **de esa venta**;
  reintegrar en efectivo sólo el excedente. El saldo a favor reutilizable se difiere.
- Cantidad acumulada aplicada por detalle <= cantidad vendida. Las solicitudes pendientes no
  reservan cantidades; se vuelve a validar al aplicar. Importe por diferencia entre el objetivo
  acumulado redondeado a centavos y lo ya reintegrado; última parcial consume el remanente exacto.
- Mercadería sellada vendible puede reingresar al almacén/lote original, al costo histórico de
  VentaDetalle y promedio ponderado. Productos abiertos/dañados/no vendibles se identifican
  en el detalle de devolución con cantidad, condición y motivo; nunca incrementan stock disponible.
- Productos abiertos/defectuosos requieren justificación y foto. Almacén inactivo, lote inactivo,
  bloqueado o vencido no admite reingreso vendible. No se ocultan los casos no vendibles.
- Aplicación indivisible: autorización + devolución/detalle + movimiento de caja del turno actual
  + reducción CxC/saldo + inventario/lote/Kardex + bitácora + operación recuperable.
  No se modifican importes, precios, pagos ni abonos originales, ni se borra historial.
- La devolución de mercadería no reintegra automáticamente el cargo de entrega. Una anulación
  completa revierte todo el importe, incluido el cargo de entrega (también cuando el pedido ya se entregó, si se autoriza anulación completa), sin borrar la venta.
- Nunca simular una devolución como venta negativa. No se revierte un DTE emitido mediante
  este flujo interno: documentos fiscales que ya salieron de PENDIENTE bloquean la operación.

## Decisiones confirmadas y alcance

- Confirmado por el usuario: carpeta central configurable, misma ruta UNC en las dos laptops.
  `Archivos.RaizEvidencias` en sis.Configuracion, inicialmente vacío. Administrador + AJUSTES.EDITAR
  configura desde Ventas → Posventa; se comprueba escritura en la carpeta. Después de registrar fotos,
  el cambio de raíz queda bloqueado hasta mantenimiento/copias verificadas para evitar perder vínculos.
  Guardado inmutable por usuario/hash, PNG normalizado por el selector, <=10 MB y 8192 píxeles;
  hasta 5 fotos/línea y 50/solicitud. Rutas gestionadas, sin traversal/vínculos y SHA256 al leer/aplicar.
  Cargas previas sin solicitud se conservan; no se borran automáticamente ante respuestas inciertas.
  **Respaldar BD + carpeta**. Un .bak por sí solo no incluye fotos. DEV-36 aún debe automatizar ambos.
- Confirmado por el usuario: también se permite anular pedidos EN_RUTA/ENTREGADO.
  Exige recuperar físicamente **toda** la mercadería sellada/vendible y autorización administrativa
  del total, incluido el cargo de entrega visible al confirmar. No se cancelan por una entrega fallida
  automáticamente. El pedido pasa a CANCELADO y conserva fecha/usuario de entrega si ya se entregó;
  evento de posventa identifica el estado anterior y devolución. Una devolución ordinaria durante
  reparto/preparación pasa el pedido a INCIDENCIA; si ya estaba ENTREGADO se conserva ese hecho.
  El historial no se elimina ni permite volver a despachar una venta devuelta/anulada.
- Entregas fallidas/cobro contra entrega continúan fuera del alcance y necesitan diseño específico.

## Diseño técnico

Contratos y reglas nuevas por capas; no cambia el JSON de solicitudes antiguas ni se
invalidan solicitudes durables anteriores. Los nuevos tipos entran al mismo diario/ledger.
Los secretos de reautenticación se consumen en memoria y no pertenecen al request, hash ni auditoría.

### Migración 019 (aditiva, sin reescribir historia)

`ven.DevolucionesVenta`: EsAnulacion BIT NOT NULL DEFAULT 0; Comprobante NVARCHAR(250)
NULL (históricos); ReduccionDeuda/ReintegroEfectivo DECIMAL(18,2) NOT NULL DEFAULT 0;
FechaAplicacion DATETIMEOFFSET(0) NULL; MotivoResolucion NVARCHAR(500) NULL;
IdTurno BIGINT NULL FK TurnosCaja; IdEquipo INT NULL FK EquiposTrabajo;
RowVersion ROWVERSION. Los metadatos financieros nuevos cuadran cuando FechaAplicacion
está informada; no se inventan compensaciones para devoluciones históricas.

`ven.DevolucionDetalle`: IdMovimiento BIGINT NULL FK MovimientosInventario para la
entrada vendible. La condición/cantidad/motivo existentes identifican lo no vendible,
sin fabricar una entrada seguida de daño que altere el promedio o la existencia disponible.
Índice detalle por venta/devolución para controlar acumulados. No crea tablas ni procedimientos.

Las evidencias mantienen el contrato lógico de DB-01 (ruta relativa y hash); su adaptación
física usa la carpeta central confirmada, separada mediante un puerto de Application.

`Archivos.RaizEvidencias` se agrega sólo si falta; nunca reemplaza rutas existentes al reaplicar 019.

`ven.PedidosMvp.CK_PedidosMvp_Entrega` se amplía en 019 para conservar fecha/usuario
cuando un pedido previamente entregado termina CANCELADO/INCIDENCIA por posventa. Ambos
siguen siendo obligatorios en ENTREGADO y deben ser ambos nulos o ambos informados.

## Uso y actualización

1. Cerrar GoDental en todas las estaciones y respaldar la base y carpeta de evidencias.
2. Actualizar `feature/desarrollo` y aplicar 019 después de 018; actualizar ambas estaciones.
3. Ventas → ficha de venta → **Devoluciones / anulaciones**. El administrador configura
   **Carpeta de evidencias** antes de adjuntar fotos; con varias laptops usar la **misma ruta UNC**
   (por ejemplo `\\SERVIDOR\GoDentalArchivos\Evidencias`) accesible por sus cuentas Windows.
4. Seleccionar cantidades del detalle original, condición y reingreso/no reingreso. Adjuntar fotos,
   comprobante y motivo. Una solicitud no compensa dinero ni mueve inventario.
5. Consultar solicitud/fotos, autorizar o rechazar con credenciales administrativas. Se muestra
   importe, deuda a reducir y efectivo a entregar. El efectivo exige turno abierto de la caja original,
   permiso de caja y fondo suficiente. Puede ser un turno posterior al de la venta.
6. Ante pérdida de respuesta consultar Pendientes; conservar el mismo identificador. No volver a
   capturar la operación ni borrar el diario local para intentar repetirla.

Sólo para la base **de desarrollo marcada** (no es migrador productivo):

```powershell
git switch feature/desarrollo
git pull --ff-only origin feature/desarrollo
$servidor = Read-Host 'Servidor SQL guardado en Ajustes de conexión'
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --install-development --server "$servidor" .
dotnet run --project src/GoDental.Wpf -c Release
```

Debe mostrar `SQL 019_Posventa.sql`. No se ha aplicado la migración a la BD personal desde este entorno.
No cambiar marcadores para forzar la herramienta sobre una base productiva: DEV-36 sigue pendiente.
La raíz central se conserva al reaplicar y su traslado con evidencias registradas requiere mantenimiento.

## Validación y continuación

Se agregan reglas puras, validadores, archivos compartidos por hash, recuperación y sesión,
SQL aislado `--run-returns` y smoke de vistas/formularios Light/Dark y compacto. La suite SQL
cubre reintegros de contado/crédito/abonos, parciales y centavos, costo histórico/promedio,
no vendible, foto alterada, lote bloqueado, autorización, concurrencia, rollback, turnos,
anulación antes/en ruta/después de entrega y reaplicación conservando datos.
[CI Windows #142 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39878696320),
código **`08507a8715da834debfcbba7f243f885b43eadda`**, job `120170550581`:
Release **0 advertencias/errores**, **272 xUnit**, **574 comprobaciones SQL** (54 de posventa)
y **817 WPF**, sin fallos de binding. Localmente también pasó Release con Windows targeting
sin advertencias/errores y las 271 pruebas anteriores; el nuevo caso de carpeta inaccesible
quedó cubierto por las 272 de CI. No certifica hardware/SMB entre las PCs ni la BD personal.

Sigue **2.2B · cobranza y documentos de crédito**: avisos/seguimiento consultables, recibo de abono
 y estado de cuenta, con saldo y efectos de posventa trazables. Sin envío automático a terceros.
Saldo a favor reutilizable, reversión fiscal/DTE, disposición posterior de cuarentena/daño,
reapertura/corrección de devoluciones aplicadas y entregas fallidas/COD requieren complementos
explícitos; no se implementan como atajos en este bloque. DEV-36 mantiene pendiente instalación,
migrador/compatibilidad y respaldo automático de BD **y archivos**, con prueba de restauración.
