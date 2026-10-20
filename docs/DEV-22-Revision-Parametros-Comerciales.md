# DEV-22 · Revisión de parámetros comerciales

Revisión de `feature/desarrollo` antes de integrar `feature/ui` el 19/10/2026. Se consultaron DM-01, DB-01, ARQ-01, VEN-01 y SEC-01. No cambia la política vigente de 7% y 35%.

## Estado encontrado

- `sis.Configuracion` ya contiene `Descuento.VendedorMaximo` (7), `Descuento.RequiereJustificacionSobre` (7) y `Descuento.RequiereAutorizacionSobre` (35), todos con `EditableUI=1`. El procedimiento `ven.sp_ConfirmarVenta` lee los dos umbrales `Requiere...`; usa 7 y 35 sólo como respaldo si faltan valores válidos. `Descuento.VendedorMaximo` todavía no se consulta en ese flujo.
- No existe todavía un servicio ni una pantalla para modificar esos parámetros. Ajustes permite conexión local y tema. `AJUSTES.EDITAR` existe en el catálogo de permisos, pero no autoriza una operación de edición de `sis.Configuracion` implementada en la aplicación.
- Crédito (`Credito.DiasDefault`, `Credito.IntervaloAvisoDias`) y alerta de vencimiento (`Inventario.AlertaVencimientoDias`) tienen lectura SQL con valores de respaldo. `Devolucion.PlazoHoras` y `Precios.MontoReferenciaEspecial` están sembrados, pero no hay un flujo operativo que los consulte en esta rama.
- La semilla `007` reemplazaba todos los valores configurables y borraba `ModificadoPor` al repetirse. Ahora conserva valor, usuario y fecha de los parámetros `EditableUI=1`; sigue restaurando los no editables. La validación final acepta valores configurados válidos y comprueba el orden de los dos umbrales.
- El MVP de ventas integrado desde `feature/ui` lee los umbrales en la cotización y vuelve a calcular al confirmar. Sus permisos y tipos de autorización conservan nombres con 7/35 (`VENTAS.DESCUENTO_MAYOR_7`, `VENTAS.DESCUENTO_MAYOR_35`, `DESCUENTO_MAYOR_35`). Antes de exponer edición en Ajustes hay que verificar la semántica de esos identificadores.

## Siguiente incremento acotado

Crear un caso de uso de parámetros comerciales en Application y repositorio SQL en Infrastructure. Validar `0 <= justificación <= autorización <= 100`, permisos `AJUSTES.EDITAR` en servicio y SQL, concurrencia entre estaciones y escritura de `sis.Bitacora` dentro de la misma transacción. Exponer únicamente claves realmente consumidas en Ventas; no presentar `Descuento.VendedorMaximo` como límite efectivo mientras siga sin uso. Añadir la pantalla de Ajustes y pruebas de lectura, edición, rechazo sin permiso, conflicto y auditoría.
