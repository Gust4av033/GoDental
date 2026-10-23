# DEV-43 — Complementos pendientes y orden de continuación

22/10/2026, El Salvador. Aclaración solicitada por el usuario después de DEV-41/42.
Este documento corrige la prioridad propuesta; no implementa funciones ni aplica migraciones.
Fuentes: DM-01, matriz de DEV-30 y límites de DEV-32/34/37/38/40/41/42.
Es una consolidación de pendientes documentados, no una nueva auditoría exhaustiva del código.

## Orden vigente solicitado

1. Revisar y completar los complementos de los módulos existentes respecto de DM-01 y sus
   documentos funcionales. Entregar cada incremento con lógica, UI, permisos, migración si
   corresponde y pruebas; registrar qué queda terminado y qué conserva decisiones abiertas.
2. Desarrollar los módulos nuevos pendientes, con su diseño y requisitos: Fiscal/DTE y Online,
   entre los documentados. No inferir emisión fiscal o proveedores externos sin requisitos,
   credenciales y autorizaciones correspondientes. Funciones adicionales fuera del alcance
   aprobado se diseñan antes de asumir que forman parte de esta entrega.
3. Preparar instalación/actualizaciones y respaldos/restauración de DEV-36; después ejecutar
   el piloto formal/aceptación en las dos PCs y con hardware. Se conserva el identificador
   histórico **2.4**, pero deja de ser la siguiente tarea inmediata tras 2.3.

Las pruebas automáticas de concurrencia, recuperación y conservación de datos continúan
con cada incremento. El piloto formal se coloca al final; los respaldos siguen siendo necesarios
antes de modificar una base con registros, sin esperar al piloto.

## Complementos todavía documentados como pendientes

| Área existente | Complemento | Referencia y condición |
| --- | --- | --- |
| Precios y promociones | Mantenimiento de listas, escalas y promociones que consume el motor comercial. | DEV-30 / VEN-01A. Primer bloque recomendado: cerrar alcance de mantenimiento y reglas soportadas antes de agregar UI/escrituras. |
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

Si la BD ya tiene 021, aplicar **022 y después 023**, ambas en la misma BD configurada:

- `022_Reparto_ContraEntrega.sql`: tablas de custodia/rendición, permisos y método exclusivo de reparto.
- `023_Reportes_Operativos.sql`: fecha de aplicación para nuevas recepciones. No ejecuta ni incluye 022.

Si la BD está más atrás, aplicar también las migraciones faltantes anteriores, en orden.
Que un archivo posterior pueda ejecutarse sin error no demuestra que la base esté completa.

La herramienta `--install-development` recorre los scripts en orden (009–023 al actualizar una
base completa existente marcada; incluye 022 y 023). Es distinta de ejecutar sólo el archivo 023.
Se limita a `GoDental_Development` con marcador reconocido; no es un migrador productivo con
historial/compatibilidad. No cambiar marcadores para forzar otra base. Ver DEV-36 y los comandos
locales de DEV-41. No se confirma aquí qué migraciones tiene la PC del usuario.
