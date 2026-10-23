# DEV-43 — Complementos pendientes y orden de continuación

22/10/2026, El Salvador. Aclaración solicitada por el usuario después de DEV-41/42.
Este documento corrige la prioridad propuesta; no implementa funciones ni aplica migraciones.
Fuentes: DM-01, matriz de DEV-30 y límites de DEV-32/34/37/38/40/41/42.
Es una consolidación de pendientes documentados, no una nueva auditoría exhaustiva del código.

## Orden vigente solicitado

Prioridad sustituida por instrucción del 23/10/2026; ver [DEV-44](DEV-44-Fiscal-Interno.md)
y [DEV-45](DEV-45-Seguridad-Hallazgos.md):

1. Módulos nuevos: Fiscal interno primero y Online interno a continuación, según requisitos.
   El usuario todavía no tiene ambiente de pruebas de Hacienda; emisión real no habilitada.
2. Después, escalas/listas/promociones y los demás complementos priorizados.
3. Endurecimiento final de seguridad, instalador/actualizaciones/respaldos y piloto formal en las
   dos PCs. Seguridad se revisa también con cada módulo; riesgos altos/críticos no esperan al final.

Las pruebas automáticas de concurrencia, recuperación y conservación de datos continúan
con cada incremento. El piloto formal se coloca al final; los respaldos siguen siendo necesarios
antes de modificar una base con registros, sin esperar al piloto.

## Complementos todavía documentados como pendientes

| Área existente | Complemento | Referencia y condición |
| --- | --- | --- |
| Precios y promociones | Mantenimiento de listas, escalas y promociones que consume el motor comercial. | DEV-30 / VEN-01A. Después de módulos nuevos: cerrar alcance de mantenimiento y reglas soportadas antes de agregar UI/escrituras. |
| Productos/inventario | Control operativo de series; reposición/alertas y analítica más avanzada según alcance. | DEV-30/32. Las barras gráficas y etiquetas configurables ya están hechas en DEV-34; no repetirlas. POS todavía no admite productos serializados. |
| Crédito | Política de mora y excepciones, saldo a favor reutilizable y tratamiento autorizado de anulación de abonos. | DEV-30/38/40. Requieren decisiones de negocio antes de modificar dinero o bloquear clientes. Avisos, recibos y estados de cuenta ya están conectados. |
| Caja/pagos | Conciliación bancaria/tarjeta y documentos adicionales de arqueo según alcance. | DEV-30/32. Registrar tarjeta o verificar una rendición no completa una conciliación bancaria general. |
| Posventa/reparto | Resolución posterior de daño/cuarentena y faltantes/daño en custodia; corrección autorizada de operaciones aplicadas cuando se defina. | DEV-38/41. Entrega o retorno completos ya están implementados; reparto parcial no se convierte en requisito: el usuario eligió explícitamente el recorrido completo. |
| Compras/importaciones | Documentos adicionales de compra; tratamiento de costos posteriores/cancelaciones si se incorpora. | DEV-30/33/37. El cierre por valor neto antes de recibir ya está aprobado e implementado. Costos incompletos o posteriores no se habilitan por inferencia; cuentas por pagar requiere alcance y diseño propios. |
| Seguridad/administración | Administración granular de roles/permisos y consulta operativa de bitácora/estaciones. | Matriz DEV-30 / SEC-01. No confundir los permisos ya validados en SQL con un mantenimiento administrativo completo. |
| Reportes | Analítica/recomendaciones más avanzadas previstas por DM-01 §14. | DEV-42. Reportes operativos están hechos; unidades netas no equivalen al índice contable de rotación ni a predicciones de demanda. |

Esta lista no declara obligatoria toda función diferida: antes de cada bloque se distingue lo
exigido por documentación vigente de mejoras opcionales o reglas todavía por definir. El estado
«implementado» de DEV-31–42 corresponde al alcance de cada incremento, no a todo DM-01.

## Migraciones: son incrementales, no acumulativas por archivo

El usuario aclaró el 23/10/2026 que **falta desde 021, incluida**. La secuencia pendiente actual es
**021 → 022 → 023 → 024**, en la misma BD configurada. No suponer 021 aplicada.
Si otra estación ya tuviera 021, continuaría por 022 → 023 → 024.

- `021_Cobranza_Documentos.sql`: avisos y snapshots de abonos.
- `022_Reparto_ContraEntrega.sql`: tablas de custodia/rendición, permisos y método exclusivo de reparto.
- `023_Reportes_Operativos.sql`: fecha de aplicación para nuevas recepciones. No ejecuta ni incluye 022.
- `024_Fiscal_Revision_Interna.sql`: revisión administrativa de solicitudes fiscales, sin emisión.

Si la BD está más atrás, aplicar también las migraciones faltantes anteriores, en orden.
Que un archivo posterior pueda ejecutarse sin error no demuestra que la base esté completa.

La herramienta `--install-development` recorre los scripts en orden (009–024 al actualizar una
base completa existente marcada; incluye 021–024). Es distinta de ejecutar sólo el archivo 023.
Se limita a `GoDental_Development` con marcador reconocido; no es un migrador productivo con
historial/compatibilidad. No cambiar marcadores para forzar otra base. Ver DEV-36 y los comandos
locales de DEV-41. El usuario reportó pendientes desde 021; no se aplicaron a su PC desde cloud.


Continuación del 23/10/2026: DEV-46 conecta el primer bloque Online interno, después de DEV-44.
Pendiente local actualizado: **021 → 022 → 023 → 024 → 025**, incluida 021. Siguiente trabajo
independiente: escalas/listas/promociones. Campañas/conectores/automatización y DTE externo siguen
sujetos a sus requisitos y controles previos; no se declaran completados por este bloque manual.

## Ampliación de Online según referencia · 23/10/2026

El usuario pide asemejar las funciones a Imagenes/221200. [DEV-47](DEV-47-Online-Referencia.md)
amplía el bloque manual con zonas, segmentos, plantillas, campañas preparadas/aprobadas y
seguimiento. Esta continuación toma prioridad antes de escalas/listas/promociones.
El alcance es interno; mapa, búsqueda remota, consentimiento/proveedores y envíos quedan para
ONL externo con controles S1. Conversión conserva compra confirmada de DM-01.

**Pendiente local vigente: 021 → 022 → 023 → 024 → 025 → 026, incluida 021.**
La herramienta de desarrollo recorre 009–026 al actualizar una base completa reconocida;
no equivale a aplicar sólo 026. 025 añade el primer bloque Online y 026 la planificación.
Después del recorrido interno: escalas/listas/promociones y los requisitos externos pendientes;
instalador/actualizaciones/respaldos antes del piloto formal de dos PCs. No se aplicó la BD personal.

Validación de DEV-47: [CI Windows #183](https://github.com/Gust4av033/GoDental/actions/runs/39974506383),
`c6c26aa`, Release 0 advertencias/errores, 375 xUnit / 789 SQL / 1087 WPF. Después del recorrido
interno validado, siguiente bloque independiente: escalas/listas/promociones (VEN-01A), cerrando
reglas de mantenimiento antes de SQL/UI. Mapa, bandeja remota, proveedores, consentimiento y
DTE real mantienen sus requisitos/bloqueos previos: no se declaran terminados por este incremento.
