# DEV-41 — 2.2C: reparto y cobro contra entrega

Fecha local: 22/10/2026 (El Salvador). Diseño previo a implementación.
Fuentes: DM-01 §12, DB-01 inventario/ventas/caja, VEN-01, SEC-01, ARQ-01 y DEV-30/38/40.
El usuario confirmó entrega completa o retorno completo, efectivo o transferencia y rendición.

## Recorrido y consistencia

- Pedido sin venta, LISTO, responsable identificado → consultar precio → despachar completo.
  El precio y sus redondeos quedan congelados; no hay edición comercial durante el reparto.
- Despachar transfiere físicamente las unidades desde el almacén a custodia del repartidor:
  descuenta existencias/lotes mediante Kardex TRANSFERENCIA y conserva cantidad/costo histórico
  en líneas de custodia. No aumenta StockReservado ni inventa crédito comercial. Ningún POS,
  ajuste o conteo puede consumir esas unidades porque ya no están en existencia de bodega.
  Inventario empresarial = bodega + custodia pendiente; los reportes muestran ambos por separado.
- EN_RUTA → ENTREGADO: exige confirmación de pago total exacto, efectivo o transferencia con
  referencia. Registra una sola venta confirmada al precio congelado y costo de despacho,
  sin descontar otra vez inventario, con pago completo y sin CxC. Queda PENDIENTE_RENDICION.
- El efectivo cobrado por repartidor se registra como EFECTIVO_REPARTO, exclusivo de este flujo,
  sin afectar caja. Rendir exige turno abierto de la caja de origen y monto exacto; genera un
  único VENTA_EFECTIVO y actualiza la versión del turno. Transferencia exige comprobación de
  su referencia y rendición exacta, pero no genera movimiento de efectivo ni exige turno.
  Ambos registran quién recibió/verificó, estación, fecha y motivo; no habilita pago parcial/mixto.
- Fallo de entrega → INCIDENCIA: conserva custodia. Retornar exige recuperación completa
  sellada/vendible, devuelve al almacén/lotes originales al costo histórico, recalcula promedio,
  registra Kardex y cierra CANCELADO/RETORNADO. No vende ni cobra cargo de entrega fallida.
  Producto faltante/dañado no se declara retornado: queda INCIDENCIA/custodia hasta resolución
  específica posterior, sin crear existencias ficticias. No se permite segundo despacho del mismo pedido.
- Antes de rendir, posventa de la venta queda bloqueada para impedir reintegrar dinero aún no
  rendido o pendiente de verificación. Tras rendir usa DEV-38 sin cambiar originales. El plazo
  de devolución cuenta desde la confirmación de entrega/venta, no desde despacho.
- Estados/vinculación/POS genéricos no pueden saltarse un reparto existente. Pedidos previos
  mantienen su recorrido. Recibos de venta identifican el método de efectivo por rendir.
- Nuevas escrituras usan el ledger, GUID estable, transacción, versiones, permisos SQL,
  auditoría y diario durable existentes. No se modifican contratos de solicitudes antiguas.
  Orden de bloqueo: pedido → reparto → método/turno cuando aplique → productos ordenados/lotes.

## Migración 022

Diseño aditivo e idempotente, sin reconstruir históricos:

- ven.RepartosMvp: IdReparto bigint identity PK, IdPedido bigint UNIQUE FK, IdCaja/IdAlmacen int FK,
  Responsable nvarchar150, Estado nvarchar30, Total decimal18,2 >0, SnapshotCalculo nvarchar(max)
  JSON válido, ClienteSnapshot nvarchar(max), IdVenta bigint NULL FK UNIQUE filtrado,
  Medio nvarchar20 NULL EFECTIVO/TRANSFERENCIA, Referencia nvarchar150 NULL,
  FechaSalida datetimeoffset0, IdUsuarioSalida/IdEquipoSalida FK, FechaEntrega/FechaRetorno/
  FechaRendicion datetimeoffset0 NULL, IdUsuarioRendicion/IdEquipoRendicion/IdTurno NULL FK,
  RowVersion. Estados EN_RUTA/INCIDENCIA/PENDIENTE_RENDICION/RENDIDO/RETORNADO.
  CHECK coherencia de venta, medio, fechas y rendición. Índice Estado/IdReparto.
- ven.RepartoLineasMvp: IdLinea bigint identity PK, IdReparto FK, IdProducto/IdLote FK,
  Cantidad decimal18,4 >0, CostoUnitario decimal18,6 >=0, Calculo nvarchar(max) JSON.
  Índice IdReparto/IdProducto; snapshot de línea incluye lote y precio/redondeos.
- Permisos VENTAS.REPARTO_GESTIONAR y CAJA.REPARTO_RENDIR: Administrador por defecto;
  conservar concesiones/denegaciones. Método EFECTIVO_REPARTO exclusivo, no se publica en POS/abonos.
  Total tras 022: 79 tablas / 8 vistas / 18 procedimientos.

Después: DEV-42 (2.3), luego 2.4 piloto/DEV-36 instalación, actualizaciones y respaldos.

## Uso y límites del incremento

Pedidos y entregas → pedido LISTO sin venta → Caja de origen → Consultar precio → Despachar
contra entrega. Revisar responsable, total y motivo. Luego Entrega/incidencia/retorno y, cuando
corresponda, Rendir cobro. El total no admite diferencias ni pagos parciales en esta fase.
El precio usa las reglas vigentes sin descuentos manuales de reparto; un descuento que exige
reautenticación especial debe tramitarse por la venta previa ordinaria. Al entregar se revalida
producto/lote, bloqueo y vencimiento: si dejó de ser apto, registrar incidencia/recuperación.

Los permisos nuevos se conceden inicialmente al Administrador. Volver a iniciar sesión después
de actualizar todas las estaciones. El usuario que sólo consulta pedidos no puede gestionar ni rendir.
Una venta contra entrega no tiene CxC ni consume límite/plazo del cliente. La rendición no
reescribe la forma de pago ni el importe del recibo de venta; su estado se consulta en el pedido.

## Actualización y aceptación

Aplicar 022 después de 021 y 023 a continuación, sobre la instancia/base guardadas en Ajustes.
Cerrar las aplicaciones y respaldar BD/evidencias antes de actualizar ambas estaciones.
Esta sesión cloud no aplica migraciones a la BD personal.

Para la BD **de desarrollo marcada**, desde la raíz del repositorio:

```powershell
git switch feature/desarrollo
git pull --ff-only origin feature/desarrollo
$servidor = Read-Host 'Instancia SQL de Ajustes'
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --install-development --server "$servidor" .
dotnet run --project src/GoDental.Wpf -c Release
```

Para una base existente ajena a la herramienta de desarrollo, ejecutar únicamente las
migraciones faltantes en orden mediante sqlcmd/SSMS, sin cambiar su marcador:

```powershell
sqlcmd -S '<instancia>' -d '<base>' -E -b -i database/022_Reparto_ContraEntrega.sql
sqlcmd -S '<instancia>' -d '<base>' -E -b -i database/023_Reportes_Operativos.sql
```

Revisar entre las dos PCs: un despacho reduce disponible, el otro equipo no puede vender esas
unidades ni repetir despacho/cobro/rendición. Cerrar turno antes de rendir y abrir otro: sólo el
nuevo turno debe recibir el efectivo. Verificar transferencia sin sumar efectivo. Incidencia
conserva custodia; retorno completo repone una sola vez. Faltantes/daños permanecen identificados,
pendientes de su complemento de resolución; no declararlos recuperados.

## Verificación

Pruebas de contratos/recuperación, SQL real aislado y smoke WPF en ambos temas/compacto.
Resultado definitivo de CI pendiente de registrar.
