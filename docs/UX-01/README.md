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

Los valores Hex finales, logo, mockups de alta fidelidad y hardware de impresión permanecen pendientes para v0.2.
