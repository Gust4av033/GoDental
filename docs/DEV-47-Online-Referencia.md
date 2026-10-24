# DEV-47 — Online: referencia visual y preparación comercial

23/10/2026. Diseño anterior a contratos/SQL. Continúa DEV-46; fuentes: DM-01 §15,
DB-01 §7.9, ARQ-01, UX-01, ONL-01 y `Imagenes/Captura de pantalla 2026-10-18 221200.png`.
El usuario pide que las funciones de la referencia guíen Online. Se amplía el bloque manual;
no se equipara una maqueta de clínica con reglas del depósito dental.

## Recorridos y límites

Navegación local: Zonas, Prospectos, Ficha/origen, Segmentación, Plantillas, Campañas,
Aprobación, Respuestas/seguimiento, Conversión y No contactar. Formularios acotados modales;
consulta y vista previa en página. Temas compartidos, sin mapas/datos/contadores ficticios.
Zonas usan departamento/municipio SV; el mapa geográfico real necesita proveedor/licencia y
fuentes autorizadas. No inferir áreas por coordenadas inventadas.

- Zonas activas con prioridad y conteo real por territorio; desactivación conserva historia.
- Segmentos reutilizables: nombre, territorio opcional, tipo de negocio, estado, tipo de fuente
  y canal. Filtros combinados AND; fuentes usa EXISTS sin duplicar prospectos. No se deduce
  interés clínico, edad ni consentimiento. Vista previa paginada excluye convertidos,
  descartados, bloqueos globales/centrales y fichas sin contacto publicado o fuente. Preparar valida el formato del contacto por canal.
- Plantillas de texto (hasta 4000 caracteres) por canal; marcador admitido `{nombre}`.
  No ejecutar HTML, scripts ni expresiones. Vista previa sustituye sólo ese marcador;
  no se abren URLs ni archivos. Desactivar no borra versiones históricas de campañas.
- Campaña se crea BORRADOR desde un segmento y plantilla activos del mismo canal.
  Copia criterios y contenido: editar el segmento/plantilla no cambia lo que se revisa.
  Preparar toma destinatarios elegibles actuales, máximo 1000; si excede, acotar el segmento.
  Guarda versiones/contactos documentales. Pendiente → aprobación interna humana con
  COMERCIAL.CAMPANIAS_APROBAR; volver a borrador elimina destinatarios preparados y aprobación,
  sin eliminar bitácora. Cancelar es terminal. No editar campañas heredadas sin snapshot interno.
  Reconsulta exclusiones al preparar/aprobar y al consultar destinatarios. APROBADA sólo significa
  preparación aprobada: no prueba consentimiento ni habilita envío. No hay transición a enviado.
- Seguimiento manual inmutable registra canal, fecha, nota/resultado y próxima gestión opcional;
  CONTACTADO/RESPONDIO/INTERESADO/SIN_INTERES/NOTA no fabrican entregas de mensajes.
  CONTACTADO exige prospecto no excluido; una respuesta entrante o nota puede conservarse aun
  con opt-out. No levantar bloqueos ni modificar convertidos; SIN_INTERES marca DESCARTADO,
  una nota no cambia estado. Una respuesta sola no crea cliente: la conversión conserva compra
  confirmada y fuente conforme DM-01. No crea credenciales de usuario ni envía bienvenida. No hay chat ni bandeja remota conectados.

## Migración 026 y controles

Tablas nuevas definidas aquí antes de implementar: `com.SegmentosProspecto` con filtros
explícitos y RowVersion; `com.SeguimientosProspecto` inmutable, FK prospecto/usuario, resultado,
fecha, próxima fecha, canal y nota. Extensiones aditivas: RowVersion en Zonas, Plantillas y
Campanias; Campanias incorpora IdSegmento, CriteriosJson y ContenidoSnapshot;
CampaniaDestinatarios incorpora ContactoSnapshot y VersionProspecto. Sin reescribir histórico.
Conteo esperado 81 tablas / 8 vistas / 18 procedimientos. No nuevos proveedores ni credenciales.

Lectura mantiene COMERCIAL.VER + CLIENTES.VER. Zonas/segmentos/seguimiento exigen
COMERCIAL.PROSPECTAR; plantillas/preparar campañas, COMERCIAL.CAMPANIAS_CREAR;
aprobar/devolver/cancelar campañas, COMERCIAL.CAMPANIAS_APROBAR. Los permisos ya existen:
no conceder permisos a roles automáticamente ni restaurar denegaciones. SQL revalida permisos,
estación y RowVersion; GUID/ledger recuperable, applock Online compartido y auditoría atómicos.
Identidades/versiones se congelan antes del IO. Consultas paginadas y sin resultados entre sesiones.

## Secuencia pendiente

Completar este recorrido interno y validar antes de continuar escalas/listas/promociones.
Después: ONL externo (fuentes, mapa, consentimiento por canal, proveedores oficiales,
atribución y tareas periódicas) y DTE real cuando existan requisitos/ambiente. Mantener DEV-45
como bloqueos antes de conectores; instalador/actualizaciones/respaldos y piloto dos PCs posteriores.
No declarar Online completo ni seguridad certificada por terminar sus pantallas internas.

**PC personal: siguen pendientes 021 → 022 → 023 → 024 → 025 → 026, en orden.**
El instalador SQL básico fija GoDental explícitamente después de 001: cada sqlcmd abre
una conexión nueva y no debe ejecutar las migraciones sin USE en master.
026 no contiene las anteriores. Respaldo previo, ambas aplicaciones cerradas y base/instancia
correctas. Desde cloud no se aplica SQL a la PC personal. Comandos de desarrollo en DEV-46
recorren scripts en orden únicamente sobre GoDental_Development reconocida.

## Correspondencia con la imagen

| Panel | Implementación interna | Pendiente externo o complemento |
| --- | --- | --- |
| 1 · Zonas | Catálogo SV, prioridad, vigencia y conteos reales | Mapa y búsqueda por fuentes autorizadas |
| 2–3 · Prospectos/ficha | Captura, deduplicación, territorio, fuentes, historial y exclusiones | Asignación de cartera, intereses estructurados, adjuntos |
| 4 · Segmentación | Filtros comerciales reutilizables y vista previa paginada | Scoring y atribución definidos con evidencia |
| 5 · Plantillas | Texto por canal, vigencia y vista previa de {nombre} | Plantillas aprobadas del proveedor oficial |
| 6–7 · Campañas/aprobación | Copias de mensaje/criterios, destinatarios y aprobación humana | Consentimiento, programación y transmisión oficial |
| 8 · Respuestas | Registro manual inmutable, resultado y próxima gestión | Bandeja remota, webhook y chat mediante proveedor |
| 9 · Conversión | Vinculación trazable con compra confirmada, sin duplicar cliente | Alta asistida desde prospecto según requisitos de DM-01 |
| 10 · No contactar | Exclusión central por canal y global, sin levantamiento implícito | Política expresa de restitución de consentimiento |

Los ejemplos de pacientes/edades/intereses clínicos de la imagen no se copian como reglas del
depósito. No hay mapa ficticio, estados de mensajes simulados ni creación de acceso de usuario
por convertir un contacto comercial.

## Comprobación del incremento

[CI Windows #183](https://github.com/Gust4av033/GoDental/actions/runs/39974506383), evento push,
correcto sobre **`c6c26aa35bc55b5f415024bbdf091189df22bcbb`**, job `120485611196`:
Release **0 advertencias / 0 errores**, **375 xUnit / 789 comprobaciones SQL / 1087 WPF**.
El flujo PR #184 también pasó sobre la misma cabeza. No hay errores de binding en la suite.
Auditoría NuGet: siete proyectos consultados, cero avisos altos/críticos reportados.
El instalador básico pasa el parser de PowerShell; no se ejecutó sobre la BD personal.

- 28 comprobaciones SQL nuevas de planificación: zona/GUID/duplicado y versión entre estaciones;
  segmentos con origen sin multiplicar destinatarios; exclusiones y formatos al preparar;
  canales compatibles y copias inmutables del contenido/criterios; estados controlados,
  aprobación humana, devolución/cancelación, cambio de ficha/exclusión posterior; seguimiento
  inmutable paginado, reintentos, permisos revocados, rollback/recuperación y reaplicación.
  Online acumula 68 comprobaciones SQL con el bloque anterior.
- 11 casos unitarios nuevos: límites de filtros/fechas/marcadores, identidades congeladas,
  separación de permisos, respuesta tardía/cambio de sesión, misma solicitud de aprobación
  ante respuesta perdida y selectores territoriales dependientes.
- 76 comprobaciones WPF adicionales: capturas de diez secciones en Light/Dark y tamaño normal/
  compacto; formularios de zonas/segmentos/plantillas/campañas/revisión/seguimiento y recorridos
  anteriores. Vista previa de texto y acciones de confirmación/cierre conservadas. No se envían
  comunicaciones durante las pruebas ni se confunde una gestión manual con entrega remota.

Release local y 375 pruebas también pasan con Windows targeting. Linux no ejecuta SQL Server
ni WPF. Se revisaron registros automáticos: las capturas no equivalen a aceptación humana del
monitor/impresora/LAN ni al piloto físico en dos estaciones. La confirmación posterior sólo
registra documentación, sin cambiar el código validado ni aplicar SQL a la PC personal.
