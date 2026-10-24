# Documentación de GoDental

Prioridad del 23/10/2026: [DEV-44](DEV-44-Fiscal-Interno.md), Fiscal interno y Online ampliado ([DEV-47](DEV-47-Online-Referencia.md), [DEV-48](DEV-48-Online-Conectores.md)); después escalas/listas/promociones. [DEV-45](DEV-45-Seguridad-Hallazgos.md) registra seguridad y fases de corrección. En la PC faltan migraciones **desde 021, incluida: 021 → 022 → 023 → 024 → 025 → 026**.

Los documentos fuente vigentes se organizan por código dentro de este directorio. Los resúmenes Markdown históricos en la raíz de `docs/` se conservan como material de consulta y trazabilidad del repositorio inicial.

Estado funcional vigente, hallazgos y plan de la segunda etapa: [DEV-30 · Revisión del proyecto](DEV-30-Revision-Fase-2.md), 21/10/2026. Los avances posteriores están en [DEV-31 · Recuperación](DEV-31-Recuperacion-Operaciones.md), [DEV-32 · Complementos MVP](DEV-32-Complementos-MVP.md) y [DEV-33 · Compras y recepciones](DEV-33-Compras-Recepciones.md). [DEV-34](DEV-34-Impresion-Formatos.md) implementa barras/etiquetas, formatos configurables de comprobantes y notas de entrega, con vista previa/PDF/impresión al 100%. Consultarlos junto con [ROADMAP](ROADMAP.md) para distinguir resultados históricos de pendientes actuales.

[DEV-37](DEV-37-Importaciones-Costeo.md) conecta importaciones, eventos manuales, gastos y cierre/prorrateo con recepciones parciales (018). [DEV-38](DEV-38-Posventa.md) implementa 2.2A: devoluciones/anulaciones autorizadas, evidencias centrales y compensaciones de stock/caja/CxC (019). [DEV-39](DEV-39-Formularios-Datos.md) agrega el paréntesis de formularios: teléfonos/países, formatos DUI/NIT, direcciones actuales con distrito, contactos y alta directa de categorías (020). [DEV-40](DEV-40-Cobranza-Documentos.md) continúa 2.2B: avisos, recibos de abono y estados de cuenta con posventa trazable (021).

Preparación de instalador y actualizaciones con conservación de datos: [DEV-36](DEV-36-Instalador-Actualizaciones.md). Es un diseño propuesto; aún no hay instalador productivo ni migrador con historial.

| Código | Documento | Versión |
|---|---|---:|
| DM-01 | Documento Maestro | v0.2 |
| DB-01 | Modelo de Datos y Diccionario SQL Server | v0.1 |
| ARQ-01 | Arquitectura C# | v0.1 |
| UX-01 | Arquitectura UI/UX y Design System | v0.1 |
| SEC-01 | Seguridad, Roles, Permisos, Auditoría y Sesiones | v0.1 |
| INV-01 | Inventario, Productos, Lotes, Costeo y Trazabilidad | v0.1 |
| IMP-01 | Compras, Proveedores, Importaciones, DHL, Recepciones y Costeo | v0.2 |
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
- [DEV-21 · Fidelidad visual](DEV-21-UI-Fidelidad-visual.md): componentes, listados/fichas y distribución de POS/caja ajustados a las ocho capturas del usuario.

- [DEV-23 · Referencias de Imagenes](DEV-23-UI-Referencias-Imagenes.md): revisión de conexiones y refinamiento compartido de POS/cobro, caja y crédito en `feature/desarrollo`.

- [DEV-24 · Clientes](DEV-24-Clientes-Refinamiento.md): refinamiento del listado, ficha y formularios a partir de su referencia, con permisos e historiales reales.

- [DEV-25 · Productos e Inventario](DEV-25-Productos-Inventario-Refinamiento.md): catálogo, categorías, ficha, existencias, lotes, Kardex, movimientos y conteos refinados sobre los servicios existentes.

- [DEV-26 · Ventas, Caja, Crédito y Pedidos](DEV-26-Ventas-Caja-Refinamiento.md): POS, cobro, cotizaciones, comprobantes, turnos, abonos y entregas refinados sobre sus servicios, con listados y fichas separados.

- [DEV-31 · Recuperación de operaciones](DEV-31-Recuperacion-Operaciones.md): etapa 2.0A, migración 015, diario local y reconciliación después de un reinicio.

- [DEV-35 · Inicio y panel de operación](DEV-35-Inicio-Panel-Operativo.md): indicadores autorizados, actividad reciente y composición adaptable del Inicio.

[DEV-41](DEV-41-Reparto-ContraEntrega.md) implementa reparto contra entrega con custodia y rendición (022). [DEV-42](DEV-42-Reportes-Operativos.md) conecta Reportes e Inicio: ventas/posventa, margen histórico, inventario/custodia, vencimientos y compras recibidas (023). La continuación vigente se detalla en DEV-43; el piloto y DEV-36 quedan después de los complementos y módulos nuevos.

- [DEV-44 · Fiscal interno](DEV-44-Fiscal-Interno.md): preparación/revisión administrativa, sin emisión DTE.
- [DEV-45 · Seguridad](DEV-45-Seguridad-Hallazgos.md): hallazgos abiertos y controles previos por fase.
- [DEV-46 · Primer bloque Online](DEV-46-Online-Interno.md): prospectos, fuente, exclusiones y vínculo por compra confirmada (025).
- [DEV-47 · Online conforme a referencia](DEV-47-Online-Referencia.md): zonas, segmentos, plantillas, campañas/aprobación y seguimiento manual (026); mapa y conectores externos pendientes.
- [Guía de uso de Online interno](ONL-01/Guia-Online-Interno.md). Pendiente local vigente: **021 → 022 → 023 → 024 → 025 → 026**, en orden.

- [DEV-48 · Mapa, descubrimiento y preparación Meta](DEV-48-Online-Conectores.md): mapa sin red, búsqueda pública con revisión y componentes de servidor probados, sin activar transmisiones. Sin migración nueva.
- [Configuración Meta / requisitos de HTTPS](ONL-01/Configuracion-Meta.md): iniciar con el número de prueba; Cloud API, autorización por canal y servicio central seguro pendientes.

- [DEV-49 · Búsqueda y acceso](DEV-49-Busqueda-y-Acceso.md): coordenadas de búsqueda, códigos HTTP/servidor alternativo manual y login con mostrar/ocultar contraseña; sin migración nueva.

- [DEV-52 · Scroll y navegación](DEV-52-Scroll-Navegacion.md): lateral fijo, opción activa visible y continuidad de la rueda en tablas/listas.

- [DEV-53 · Custodia genérica de candidatos secretos](DEV-53-Custodia-Secretos.md): base B3 Windows, sin importación ni activación fiscal y sin migración nueva.

- [DEV-54 · Online: navegación y carga](DEV-54-Online-Navegacion-Carga.md): menú interno independiente e indicadores azules visibles durante el desplazamiento.

- [DEV-55 · Preparación verificable de despliegue B2](DEV-55-Servidor-Despliegue.md): diagnóstico de identidad/TLS/SQL y arranque bloqueado, sin migración nueva.
