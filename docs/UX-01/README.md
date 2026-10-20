# UX-01 — Arquitectura UI/UX y Design System

**Versión:** v0.1 · **Plataforma:** WPF · .NET 8 · Windows 11

GoDental debe sentirse como un ERP/POS dental moderno, limpio y rápido, con identidad celeste/azul/verde y soporte Light/Dark desde el inicio.

## Reglas base

- Diseñar para 1366×768 y escalar correctamente a 1920×1080 y 125/150% de Windows.
- Tokens compartidos en `Themes/Colors.xaml`, `Typography.xaml`, `Spacing.xaml`, `Controls.xaml`, `LightTheme.xaml` y `DarkTheme.xaml`; ningún View inventa colores o estilos repetidos.
- Navegación principal en Shell con sidebar y contenido central. Los diálogos modales se usan para crear o editar un registro acotado y para confirmar acciones críticas, aunque el formulario necesite desplazamiento interno. Consulta, listados y detalle de solo lectura permanecen en páginas o paneles. El encabezado y las acciones del diálogo quedan visibles mientras se desplaza el formulario.
- Componentes reutilizables para botones, inputs, tablas, estados, diálogos, PageHeader, FilterBar y feedback cuando su uso se estabilice.
- Tablas legibles, filtros claros, estados empty/loading/error, teclado accesible y permisos reflejados en UI.
- Code-behind sólo para comportamiento estrictamente visual; reglas en ViewModel/Application.

## Orden inicial de UI

1. Tokens/temas.
2. Shell + Sidebar + TopBar.
3. Login.
4. Componentes base.
5. Inventario/productos.
6. Clientes.
7. Ventas/POS.
8. Caja/crédito.
9. Compras/importaciones.
10. Dashboard, reportes, ajustes, comercial y fiscal conforme existan datos/servicios reales.

La identidad final (colores aprobados y logo) y el hardware de impresión permanecen pendientes para v0.2. Las referencias visuales del usuario guían el estilo provisional de esta fase: superficies claras, acento azul, navegación compacta y tarjetas. Los tokens se documentan en `../DEV-07-UI-referencia-visual.md`; las funciones sin datos permanecen pendientes.

# GUÍA VISUAL · De las capturas de referencia al código WPF

Fuente: las 8 composiciones de `Imagenes/` (73 pantallas). Este documento las traduce a reglas escritas para que cualquier herramienta (Copilot, otra IA o una persona) pueda aplicarlas sin ver las imágenes.

Las medidas son **estimadas** a partir de capturas de baja resolución. Se ajustan a ojo con F5 (±2 DIP). Los colores que ya están en `Themes/` no cambian salvo que se indique. Todo valor nuevo se define como token con su par Light y Dark.

## 0. Reglas de uso

- Precedencia: documentos funcionales (DM-01, DB-01, VEN-01, SEC-01, FIS-01…) > esta guía > pantallas actuales.
- Solo se tocan tokens y estilos compartidos (`Themes/`). Ningún hex ni radio dentro de las vistas. Light y Dark deben seguir funcionando.
- No se crean módulos, columnas, métricas ni datos que no existan en los servicios (DEV-07 y DEV-21).
- **No copiar de las imágenes:** monedas S/ y MXN (usar USD, formato `$ 1,250.00`); DNI/RUC/IGV/RFC; Lima/CDMX; los módulos Pacientes, Citas, Agenda, Tratamientos, Comercial/Online, Fiscal/DTE y Reportes (aún no existen); gráficas con cifras inventadas; el logotipo (se mantiene el símbolo provisional).
- Un paso = un commit pequeño. Después de cada paso: F5, revisar Light y Dark a 1366×768 y ejecutar `scripts\verify.ps1` antes de integrar.

## 1. Tokens (primer commit; es lo que más acerca la app a las capturas)

| Token / estilo | Hoy | Objetivo |
| --- | --- | --- |
| Radio de controles (botón, campo, combo, ítem de menú) | 4 | 8 |
| `CardRadius` | 6 | 12 |
| `StateBadge` | radio 4 | píldora: radio 10, padding 8,2, texto 11–12 SemiBold |
| Anillo de foco | radio 6 | radio de controles + 2 |
| `CardPadding` | 16 | 20 |
| Separación entre tarjetas | 12 | 16 |
| Padding de página | revisar | 24 |
| Sombra de tarjeta | ninguna | suave (ver nota abajo) |
| Fuente | Segoe UI 13 | `Segoe UI Variable Text, Segoe UI` 13; títulos 22–24 SemiBold |
| Tabla | filas 38 | filas 40 (48 con avatar); cabecera 36, 12 px, color muted; sin líneas verticales; separador de 1 px; hover y fila seleccionada con tinte azul claro |
| Botón secundario | revisar | fondo blanco, borde 1 px gris azulado, texto `TextBrush` |
| Botón destructivo (Cerrar caja, Vaciar carrito) | no existe | contorno rojo: borde y texto `DangerBrush`, fondo blanco |
| Pestañas | revisar | subrayadas: la activa en azul con línea de 2 px; las inactivas muted; sin caja |
| Menú lateral | 184 de ancho | ítem de 36 de alto, radio 8, seleccionado con `SidebarSelectionBrush` y texto/icono azul; grupos con ▾ y subítems con sangría de 12 |

Colores sugeridos como tokens nuevos (con par Dark): borde fuerte de controles `#D5E3F1`, separador de tabla `#E8F0F8`, hover de fila `#F5FAFF`, fila seleccionada `#E6F2FF`.

**Sombra:** no uses `DropShadowEffect` en tarjetas que contengan tablas o formularios; puede degradar el texto de sus hijos (pierde ClearType) y ralentizar el scroll. Para esas, usa una segunda `Border` debajo con `Margin="0,2,0,-2"`, el mismo radio y fondo `#0B3A6B` a 5 % de opacidad. `DropShadowEffect` (BlurRadius 14, ShadowDepth 2, Direction 270, Opacity 0.08, Color `#0B3A6B`) solo en tarjetas pequeñas (KPI y métricas).

## 2. Componentes

- **Encabezado de página:** título y subtítulo muted a la izquierda; acción primaria a la derecha (alto 36). Debajo, la barra de herramientas: buscador (`SearchBox`, ancho flexible) + botón secundario «Filtrar» con icono + selector de fechas cuando aplique.
- **Listado:** tarjeta blanca con tabla. Pie: «Mostrando 1–6 de N» muted a la izquierda y paginación numerada a la derecha (botones de 28×28; la página activa en azul relleno).
- **Ficha:** «← Volver a …» arriba; avatar circular con iniciales (40), nombre 18 SemiBold, `StateBadge` y subtítulo; Cancelar (secundario) y Guardar cambios (primario) arriba a la derecha; pestañas subrayadas; formulario en dos columnas con la etiqueta de 12 muted sobre el campo.
- **Tarjeta KPI:** chip de icono de 32×32 (radio 8, tinte semántico azul/verde/ámbar/rojo), etiqueta muted 12 y valor 22–26 Bold.
- **Diálogo:** título 16–18 SemiBold con subtítulo muted; campos; pie con Cancelar (secundario) y acción primaria a la derecha.
- **Asistente de pasos** (apertura de caja, arqueo y cierre): indicador de 3 pasos con círculos numerados y conectores (activo azul, hecho con ✓, pendiente gris); botones Cancelar / Siguiente →.
- **Estados:** vacío, cargando y error ya existen; usar el mismo ancho y tinte en todas las pantallas.
- **Dinero:** alineado a la derecha, 2 decimales, `$ 1,250.00`; negativos en `DangerBrush`.

## 3. Pantallas que ya existen en la app

- **Inicio:** «Hola, {nombre}» y fecha; fila de tarjetas KPI **solo** con datos que entreguen los servicios actuales; sin gráficas si no hay datos reales.
- **Productos:** encabezado, barra de herramientas y tabla con miniatura de 32×32 (el símbolo provisional si no hay imagen); columnas código, producto, categoría, stock y estado con badge (Activo, Stock bajo en ámbar, Agotado en rojo).
- **Clientes:** lista con avatar y nombre, tipo, estado y última visita; la ficha conserva sus pestañas actuales.
- **Punto de venta:** arriba un selector Productos / Escanear (F3), buscador y botón «Seleccionar cliente (F2)»; a la izquierda, cuadrícula de tarjetas de producto (imagen o símbolo, nombre, precio azul Bold); a la derecha, resumen y carrito con «Continuar a cobro →».
- **Carrito:** tabla (#, producto, precio, cantidad, subtotal, papelera) y panel «Resumen» con el total grande; botones Vaciar carrito (destructivo), Guardar como cotización (secundario) y Continuar a cobro (primario).
- **Descuento y autorización:** diálogo con opciones de radio, resumen de la venta a la derecha, aviso azul informativo cuando requiera autorización, usuario autorizado + contraseña (`PasswordBox`), Cancelar / Aplicar descuento. Los umbrales los define el servicio, no la interfaz.
- **Cobro:** a la izquierda cuatro mosaicos de método (Efectivo, Tarjeta, Transferencia, Mixto; el seleccionado con borde y fondo azul tinte), monto recibido y «Cambio» en caja verde; a la derecha «Total a pagar» grande y el resumen; Cancelar / Confirmar cobro.
- **Venta confirmada:** check verde grande, título, tarjeta con número, fecha, cliente, total y estado; botones Imprimir comprobante (secundario) y Nueva venta (primario). Omitir «Enviar por email» (no existe).
- **Cotizaciones:** lista (número, fecha, cliente, total, estado con badge, vencimiento); detalle con Editar, Imprimir y Convertir a venta.
- **Caja:** turno activo con badge «Abierta», fecha y usuario; fila de KPI por método de pago; tarjeta «Resumen del turno»; botón destructivo «Cerrar caja». Apertura y arqueo como asistentes de 3 pasos. El arqueo por denominaciones va en USD (billetes de $100, $50, $20, $10, $5, $1 y monedas), no en S/.
- **Movimientos de caja:** tabla con Ingreso (verde) / Egreso (rojo) como badge y montos con signo y color.
- **Crédito y abonos:** lista con avatar, saldo pendiente en rojo y estado (Activo, Pagado, En mora); estado de cuenta con KPI (total crédito, pagado, saldo) y pestañas Resumen / Movimientos / Documentos; «Registrar abono» como diálogo con el cliente y su saldo pendiente destacado.
- **Proveedores:** lista y detalle con avatar o logotipo provisional; «Productos por proveedor» como pestaña con tabla.
- **Sin referencia en las capturas** (extender el patrón más cercano y anotarlo): Pedidos y entregas (usar el patrón de Cotizaciones con badges de estado), Conteos, Usuarios y Ajustes.

## 4. Orden de trabajo recomendado

1. Tokens (sección 1).
2. Menú lateral y barra superior.
3. Componentes comunes: encabezado, barra de herramientas, tabla, paginación.
4. Inicio.
5. Productos.
6. Clientes.
7. POS → Carrito → Cobro → Venta confirmada.
8. Caja.
9. Crédito.
10. Cotizaciones, Pedidos, Conteos y Proveedores.

## 5. Verificación de cada paso

- F5 y revisión visual en Light y Dark.
- Ventana de 1366×768 y escala de Windows al 125 % y 150 %: el botón de acción principal de cada pantalla sigue visible (en Cobro, «Confirmar cobro»).
- `scripts\verify.ps1` sin errores (incluye los smoke WPF y los enlaces de datos).
- Sin hex ni radios nuevos dentro de las vistas: `git grep -n "#[0-9A-Fa-f]\{6\}" src/GoDental.Wpf/Views` no debe devolver coincidencias nuevas.
