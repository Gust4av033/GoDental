# Documentación de GoDental

Los documentos fuente vigentes se organizan por código dentro de este directorio. Los resúmenes Markdown históricos en la raíz de `docs/` se conservan como material de consulta y trazabilidad del repositorio inicial.

| Código | Documento | Versión |
|---|---|---:|
| DM-01 | Documento Maestro | v0.2 |
| DB-01 | Modelo de Datos y Diccionario SQL Server | v0.1 |
| ARQ-01 | Arquitectura C# | v0.1 |
| UX-01 | Arquitectura UI/UX y Design System | v0.1 |
| SEC-01 | Seguridad, Roles, Permisos, Auditoría y Sesiones | v0.1 |
| INV-01 | Inventario, Productos, Lotes, Costeo y Trazabilidad | v0.1 |
| IMP-01 | Compras, Proveedores, Importaciones, DHL, Recepciones y Costeo | v0.1 |
| VEN-01 | Ventas, POS, Precios, Descuentos, Caja, Pagos, Crédito y Devoluciones | v0.1 |
| VEN-01A | Cierre de pendientes comerciales previos al desarrollo | v0.1 |
| FIS-01 | Fiscal, Facturación, DTE y Crédito Fiscal | v0.1 |
| ONL-01 | Prospección, Campañas y Conversión | v0.1 |

## Jerarquía de uso

- **DM-01**: alcance y reglas maestras de negocio.
- **DB-01 / SQL aprobado**: estructura formal de datos.
- **ARQ-01**: dependencias, capas, estructura C# y convenciones técnicas.
- **SEC-01**: seguridad transversal, permisos, sesión y auditoría.
- **UX-01**: implementación visual y consistencia WPF.
- **INV/IMP/VEN/FIS/ONL**: reglas y flujos de sus módulos.
- **VEN-01A**: complementa VEN-01 y fija la precedencia de precios/promociones antes del desarrollo; cuando trate esa materia, prevalece sobre las propuestas provisionales de VEN-01 v0.1.

Si dos fuentes entran en conflicto, la discrepancia se documenta y resuelve en la fuente correspondiente; no se corrige silenciosamente desde C#.

## Incremento de lógica · 19/10/2026

- [DEV-09 · Conteos físicos](DEV-09-Conteos.md): flujo, concurrencia, migración 011 y contratos C#.
- [DEV-10 · Proveedores](DEV-10-Proveedores.md): fichas, contactos y referencias comerciales.
- [DEV-11 · Integración UI](DEV-11-Integracion-UI.md): servicios y flujos disponibles para el siguiente incremento de interfaz.

## Incremento de interfaz · 19/10/2026

- [DEV-12 · Interfaces de Conteos y Proveedores](DEV-12-UI-Conteos-Proveedores.md): flujos WPF conectados, pruebas y validación pendiente en Windows/SQL.

- [DEV-13 · Clientes](DEV-13-Clientes.md): ficha comercial, contactos, direcciones, crédito separado e historiales.
- [DEV-14 · VS Code en Windows](DEV-14-VSCode-Windows.md): preparación del SDK, tareas y depuración WPF.

- [DEV-15 · Coordinación UI y lógica](DEV-15-Coordinacion-UI.md)
- [DEV-16 · Refinamiento de componentes UI](DEV-16-UI-Refinamiento.md)
- [DEV-17 · Lógica del MVP operativo](DEV-17-MVP-Logica.md): ventas/POS, caja, crédito, cotizaciones, comprobante interno y pedidos/entrega; migraciones 012–013 y límites de validación.
- [DEV-18 · Validación compartida](DEV-18-Validacion-Compartida.md): cierre de capturas y confirmación de descarte en el smoke de Windows.
- [DEV-19 · Interfaces del recorrido MVP](DEV-19-UI-MVP.md)

- [DEV-20 · Códigos automáticos y cierre](DEV-20-Codigos-Cierre.md): identificadores al guardar, código de personal y salida del proceso con el host detenido.
