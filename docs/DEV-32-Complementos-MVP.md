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

## Actualizar la PC de desarrollo

Cerrar GoDental en todas las estaciones. Desde PowerShell en la raíz del repositorio:

```powershell
git fetch origin
git switch feature/desarrollo
git pull --ff-only origin feature/desarrollo
$servidor = Read-Host 'Servidor SQL guardado en Ajustes de conexión'
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --install-development --server "$servidor" .
dotnet build GoDental.sln -c Release
dotnet run --project src/GoDental.Wpf -c Release
```

El instalador actualiza exclusivamente `GoDental_Development` con marcador reconocido de desarrollo, conservando datos; ejecuta las migraciones pendientes y debe mostrar `SQL 016_Parametros_Comerciales.sql` y `GoDental_Development lista.`. No usarlo para otra base ni cambiar marcadores para forzarlo. Para una base distinta, seleccionar explícitamente la base correcta en SSMS y aplicar únicamente 016 después de comprobar 015; este entorno no tiene acceso a la instancia de la PC del usuario.

Los parámetros requieren AJUSTES.EDITAR; códigos requieren INVENTARIO.VER y, para cambios, INVENTARIO.PRODUCTOS_GESTIONAR. El flujo conserva el centro de pendientes y nunca descarta un resultado incierto para permitir una segunda operación.

## Resultado verificado

[CI Windows #110 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39678804036), código **`f34b1a3d73b90cb97508b8ec5236b9d5f7a0abc9`**, job `119495564921`: Release con **0 advertencias/errores**, **202 xUnit**, **438 comprobaciones SQL** y **593 WPF**, sin errores de binding. Se consultó el resultado y los logs del job. SQL conserva 399 regresiones y agrega 39 comprobaciones de este bloque. Incluye conflictos, rollback de auditoría, reenvíos, invalidación de cálculo, códigos activos/inactivos y tarjeta/abonos sin duplicar efectivo o saldo.

WPF agrega Comercial en 760×680 y 540×500, códigos y formularios en ambos temas, junto con las regresiones de navegación/cierre real y comprobantes. La CI genera capturas de acceso, menú y Ajustes con el logo adaptable; esto no certifica monitor/DPI ni impresora física. El artefacto de capturas existe en CI, pero su descarga local fue rechazada con HTTP 403 por el entorno; no se declara una inspección visual manual de esas capturas. Se inspeccionó el PNG transparente original y la composición de la vista. Localmente pasaron build Release y `dotnet test GoDental.sln -c Release` (202), con Windows targeting para compilar desde Linux.

**2.0B queda implementado y validado automáticamente.** No se modificó la BD del usuario ni se fusionó `main`. Continúa **2.1A · Compras y recepción**, con diseño de estados/moneda/costos según IMP-01 antes de agregar contratos o pantallas. Etiquetas, series, conciliación bancaria, impresión/DPI físico y el piloto de dos estaciones continúan pendientes.

Continuación: [DEV-33](DEV-33-Compras-Recepciones.md) implementa 2.1A con migración 017. [DEV-34](DEV-34-Impresion-Formatos.md) registra la solicitud posterior de etiquetas y tamaños configurables, pendiente de implementación.
