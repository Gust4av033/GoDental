# DEV-16 · Refinamiento de componentes UI

19 de octubre de 2026 · `feature/ui`. Primer incremento acotado; mantiene colores y servicios existentes.

## Cambios

- Campos de texto y contraseña con superficies redondeadas y anillo de foco sin cambio de tamaño. Se conserva `PART_ContentHost` para edición, selección y contraseñas.
- Botones centrados con respuesta de pulsación; navegación con indicador lateral de selección; pestañas con hover y estado deshabilitado.
- Tablas con separadores discretos, filas de 42 DIP, selección lateral y hover. Combobox transmite correctamente el borde de foco al template interno.
- Tarjetas de inicio con mayor espacio interior y borde superior de acento.
- Acceso con monograma tipográfico provisional, mensaje de marca y formulario sin ancho mínimo rígido; mensajes largos ajustan línea. No sustituye un logo aprobado.
- Smoke ampliado con capturas de acceso de 760×540 DIP en Light/Dark.

## Acceso y coordinación

Se revisó Ajustes → guardar conexión → cerrar Ajustes → Reintentar acceso → crear administrador/iniciar sesión. Se reutilizan los comandos actuales; no hay reintentos automáticos de escrituras. El error observado previamente en Windows no se considera resuelto: falta reproducirlo y comprobar esquema/permisos además de conectividad. Ver DEV-15.

## Validación ejecutada

- `dotnet build GoDental.sln -c Release -p:EnableWindowsTargeting=true --no-restore`: correcto, 0 errores y 0 advertencias.
- `dotnet test GoDental.sln -c Release -p:EnableWindowsTargeting=true --no-build --no-restore`: 90 pruebas correctas.
- Smoke WPF intentado: bloqueado en Linux por ausencia de `Microsoft.WindowsDesktop.App`; no se generaron capturas ni se afirma revisión visual.
- `git diff --check`: correcto.

Pendiente en Windows: `scripts/verify.ps1`, revisión Light/Dark, teclado/edición/selección de campos y tablas, ventanas a 1366×768 y 125 % DPI, y `scripts/verify.ps1 -SqlIntegration -SqlServer '.\SQLEXPRESS'` sobre bases de pruebas. No integrar en main antes de completar las comprobaciones correspondientes.

No se inició Ventas/POS en este bloque.
