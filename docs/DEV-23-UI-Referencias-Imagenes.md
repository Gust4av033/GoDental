# DEV-23 · Refinamiento a partir de Imagenes

20/10/2026 · Trabajo sobre `feature/desarrollo` (`5044b9b`), sin incorporar `feature/mejora-ui` ni crear ramas remotas adicionales. La base funcional es la integrada en `main` (`27c7080`); desarrollo añade las ocho composiciones y la guía de UX-01. Consultados AGENTS, DM-01, DB-01, ARQ-01, UX-01, DEV-15, DEV-17, DEV-20, DEV-21 y DEV-22.

## Revisión de conexiones

Caja, POS, cotizaciones, crédito/abonos, pedidos/entregas y PDF interno tienen vistas, ViewModels, registro DI y navegación por permisos. Los comandos siguen usando CashService, SalesService, OrderService y SaleReceiptService. Se conservan OperationId, solicitudes congeladas, versiones de concurrencia y cálculos devueltos por los servicios. No se introducen servicios simulados ni reglas en WPF.

El contrato de resolución negativa definitiva de operaciones inciertas sigue pendiente en DEV-15. La edición de parámetros comerciales sigue pendiente en DEV-22. Saldo inicial/códigos de barras y fiscal pendiente conservan su lógica sin nueva pantalla en este incremento.

## Decisiones visuales

Se inspeccionaron las ocho imágenes de `Imagenes/`, especialmente `221111` (clientes), `221124` (proveedores), `221138` (caja/crédito) y `221146` (POS/cobro). Las otras composiciones aportan el lenguaje de tablas/formularios; no autorizan crear módulos fiscales, clínicos, comerciales ni reportes sin servicios.

1. Tokens y componentes compartidos: controles con radio 8, tarjetas 12, badges en píldora, borde de campos más definido, filas de 40 DIP con hover/selección independientes, cabeceras muted y fuente Segoe UI Variable con fallback. Acciones principales/secundarias de 152×36 DIP. Foco de teclado visible sin variar el tamaño. Espacio de página aplicado una sola vez en el shell.
2. POS/cobro: catálogo en tarjetas seleccionables (SKU, nombre, precio público y disponibilidad reales), selección de cliente con F2 y búsqueda con F3/Enter. Cobro con mosaicos derivados del catálogo de métodos permitido; seleccionarlos no añade ni confirma pagos. El total y el cambio siguen viniendo del servicio, sin mostrar un cambio estimado. Venta confirmada con resumen visual basado en la venta leída.
3. Caja/crédito: métricas de apertura/esperado/diferencia con iconos, estado real de turno, ingreso/retiro con badges y cierre con contorno de advertencia. Cuenta seleccionada con importe original, saldo y vencimiento del DTO; no se inventan indicadores agregados.
4. Consultas: importes USD `$ 1,250.00`, alineados a la derecha en tablas, sin alterar los campos editables ni los cálculos. Estados adicionales corresponden a códigos reales de los contratos/SQL.
5. Distribuciones existentes: se conservará la navegación, el contenido real y los diálogos. Los estilos nuevos permanecen en Themes, sin colores/radios nuevos dentro de las vistas. La guía recomienda limitar cambios a recursos; para reproducir el catálogo y el cobro solicitados también se adapta su composición XAML, reutilizando los mismos enlaces y comandos.

La comprobación física con F5/DPI requiere Windows; no se presenta una compilación cruzada como revisión visual. CI se vuelve a activar para `feature/desarrollo`, de acuerdo con DEV-08.

## Validación

Base y primer pase de tokens: compilación Release cruzada con `EnableWindowsTargeting=true`, sin errores ni advertencias. 144 pruebas xUnit correctas en Linux. El smoke se amplía para verificar selección real de producto y método, ausencia de pagos al elegir método, radio de controles, geometría compartida, importes USD y tamaño uniforme de acciones. Pendiente registrar el resultado Windows de este incremento.

Primer pase publicado (`7da44d5`): [CI Windows](https://github.com/Gust4av033/GoDental/actions/runs/39448944164) correcto en compilación, xUnit, todas las suites SQL y smoke WPF. Se completa la uniformidad de acciones de alta/edición en Productos, Clientes, Proveedores, Usuarios y Ajustes; Acceso conserva su botón de ancho completo. Los badges sin estado no muestran una píldora vacía.
