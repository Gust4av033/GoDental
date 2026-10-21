# DEV-32 · Complementos del MVP · etapa 2.0B

21/10/2026. Incremento posterior a DEV-31. Se consultan DM-01, DB-01, ARQ-01, VEN-01/VEN-01A, INV-01, SEC-01, UX-01 y DEV-22. Rama `feature/desarrollo`.

## Parámetros comerciales

Se exponen únicamente los umbrales consumidos por el cálculo y confirmación de ventas: `Descuento.RequiereJustificacionSobre` y `Descuento.RequiereAutorizacionSobre`. El valor inicial sigue siendo 7/35. Se exige `0 <= justificación <= autorización <= 100`, hasta cuatro decimales, motivo y `AJUSTES.EDITAR` tanto en Application como en SQL. Los permisos históricos `VENTAS.DESCUENTO_7`, `VENTAS.DESCUENTO_MAYOR_7` y `VENTAS.DESCUENTO_MAYOR_35` designan respectivamente los tramos básico, con motivo y administrativo definidos por los umbrales vigentes; no se renombran ni amplían concesiones. `DESCUENTO_MAYOR_35` conserva su identificador histórico para autorizaciones. Las etiquetas del POS se rigen por el cálculo recibido. Modificar umbrales invalida el token del cálculo anterior y sus autorizaciones; no modifica ventas históricas.

016 agrega `RowVersion ROWVERSION` a `sis.Configuracion`, sin nuevas tablas/procedimientos. Una única transacción bloquea ambas claves en orden estable, comprueba ambas versiones, cambia valores/autor/fecha y guarda snapshots antes/después con motivo en Bitácora. Un conflicto no sobrescribe otro equipo. La operación utiliza el diario y ledger de DEV-31 (`PARAMETROS_COMERCIALES`), con recibo referido al registro de auditoría. No se expone `Descuento.VendedorMaximo`, devoluciones ni reglas de precio sin consumo implementado. Ajustes mantiene conexión/apariencia y agrega la pestaña comercial sólo a cuentas con permiso.

## Códigos de barras

La ficha de producto permite listar activos/inactivos, agregar código externo o generar el interno CODE128 existente (`GD` + Id con diez dígitos), y activar/desactivar un código. La versión del producto protege el conjunto completo; cada escritura cambia esa versión. No se cambia el texto/tipo de un código ya creado, no se reasigna a otro producto y no se borra. Una clave global única conserva trazabilidad aun al desactivar. La búsqueda POS sólo usa códigos activos. EAN13/UPC exigen longitud y dígito de control; CODE128 usa ASCII imprimible. Etiquetas físicas y control de series siguen pendientes de su diseño específico.

## Tarjeta manual

Conforme a DM-01 §12.5 se habilita el método de catálogo TARJETA para registrar un cobro ya aprobado fuera de GoDental. Requiere referencia del comprobante externo, se conserva en venta/abono y sus documentos; no se registra PAN/CVV, no se solicita información de la tarjeta ni se simula autorización o conexión a terminal. No afecta efectivo ni genera vuelto. Puede combinarse con efectivo/crédito conforme a las reglas actuales. Los abonos admiten tarjeta con referencia. La conciliación bancaria y la integración con procesador continúan pendientes; registrar no acredita una verificación con el banco. La UI explica esta responsabilidad antes de confirmar.

## Validación prevista

Pruebas SQL aisladas: parámetros fuera de rango, falta/revocación de permiso, dos versiones concurrentes, rollback de auditoría, reenvío/recuperación, cambios de token; códigos duplicados/versión/activación/búsqueda; tarjeta sin referencia, venta mixta, reenvío y abono sin efecto en efectivo. Pruebas de ViewModels: permisos, sesión tardía y solicitud conservada. Smoke WPF: pestaña comercial y códigos en ambos temas/tamaño compacto, formulario de edición, tarjeta visible con aviso. Ejecutar Release, xUnit, SQL y WPF antes de integrar.

## Apariencia de marca

Se elimina la placa blanca fija en Dark: el fondo del logo sigue `SurfaceBrush` en acceso, menú y Ajustes. Un componente común usa el PNG transparente original; la rotulación se ilumina con una máscara de sus propios píxeles en Dark para conservar contraste, manteniendo el diente/degradado. El PNG compartido con PDF permanece intacto.
