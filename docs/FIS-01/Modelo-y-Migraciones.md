# Modelo fiscal, estados y migraciones

**PREPARADO — modelo conceptual y plan aditivo; no DDL nuevo aprobado.**
Los nombres de atributos de esta página son conceptos internos, no campos JSON de Hacienda.
Tipos, longitudes, precisión, literales y relaciones finales dependen de T01–T06. DB-01 conserva
la baseline histórica; no modificar `005` ni `024` para aparentar compatibilidad fiscal.

## Punto de partida SQL

| Objeto existente | Uso actual / brecha |
| --- | --- |
| `fis.DocumentosFiscales` (005/024) | Solicitud comercial FE/CCFE y revisión interna. Estado de solicitud y `EstadoRevision` son ejes distintos |
| `fis.DocumentosDte` (005) | Cimiento con identificadores, JSON y respuesta; no tiene por sí solo snapshots/versiones/ambiente/cola/intentos verificables |
| `fis.EventosDte` (005) | Registro genérico de sucesos con tipos internos; no es el agregado fiscal de evento firmado/transmitido |
| `fis.ConfiguracionFiscal` (005) | Configuración base con ambiente `PRUEBAS`/`PRODUCCION`; `ValorProtegido` no prueba cifrado ni autoriza guardar claves |
| Ledger/diario actual | Idempotencia de operaciones SQL internas; no modela evidencia de envío HTTP ni reconciliación MH |

No inferir que una fila histórica `GENERADO`, `RECIBIDO` o `ANULADO` demuestra firma, sello,
invalidation o aceptación. Una importación/migración futura clasificará el origen y conservará
la evidencia disponible; datos ambiguos quedarán para revisión, nunca promoción automática.

## Agregados propuestos

| Concepto interno | Datos y relaciones a conservar |
| --- | --- |
| Perfil emisor versionado | NIT/NRC, razón/nombre comercial, actividad y dirección conforme catálogos, ambiente, vigencia, versión y auditoría; sin secretos |
| Mapeo establecimiento/POS | Identidad local y códigos MH explícitos, origen/autorización/vigencia; almacén no implica establecimiento, caja no implica POS |
| Autorización por tipo/ambiente | Tipo FE/CCFE/evento, evidencia formal, fechas de autorización/inicio, alcance, vigencia y revocación; no un único booleano de emisor |
| Solicitud | Venta/operador/operación, tipo interno, estado/revisión/versión ya existentes; vínculo a generaciones sin perder historial |
| Snapshot fiscal | Versión inmutable de emisor/receptor/direcciones/líneas/montos/fechas, mapeos, fuente comercial, hash y contratos aplicados |
| Documento DTE | Identidad propia, snapshot, código generación/control según contrato, tipo, ambiente/emisor, versión/schema/catálogos, estado técnico y evidencia MH separada |
| Evento fiscal | Identidad y snapshot propios, clase oficial, referencias a DTE/eventos originales y datos permitidos por contrato; firma/transmisión/resultados propios |
| Artefacto inmutable | Bytes exactos, clase (generado, firmado, enviado, recibido, archivo entregable, RG), hash/longitud, contrato o plantilla, propietario y acceso |
| Intento de transmisión/consulta | Dueño DTE o evento, ordinal, operación interna, hash enviado, instante, lease/fencing, destino/ambiente aprobados, resultado técnico y referencia a respuesta |
| Resultado MH | Respuesta original protegida y datos interpretados por adaptador versionado, sello sólo si acreditado por contrato; no se escribe desde UI |
| Trabajo durable | Dueño/acción, habilitación, versión, lease/fencing, incertidumbre, próxima acción autorizada y causa; sin duplicar payload sensible en la cola |
| Entrega | Artefactos entregados, canal/destinatario autorizado, evidencia/fecha/resultado; entrega no cambia el resultado MH |
| Revisión/preflight | Evidencia y aprobador por ambiente/emisor/tipo/contrato, con caducidad y condiciones que invalidan activación |

Relaciones: una venta puede tener historial de solicitudes; una solicitud puede conservar
versiones/intentos de preparación. No habilitar dos emisiones activas de la misma obligación
sin regla fiscal explícita. Un DTE/evento tiene muchos intentos y artefactos; sus bytes emitidos
son inmutables. Un evento puede referir varios originales si el contrato lo permite. La relación
es explícita; no tratar todos los eventos como hijos de una única venta.

## Snapshot y precisión

Congelar datos en transacción con la versión de venta/receptor/emisor/revisión. El snapshot
de comprobante comercial es insumo, no snapshot fiscal completo. Incluye identidad del
receptor cuando corresponde, sin completar opcionales inventados. Conservar datos efectivamente
utilizados aunque después Clientes o el perfil emisor cambien. Registrar instante de generación
y zona/fecha de negocio según contrato; no confundirlo con creación de venta o hora de envío.

Definir decimales SQL/C# y reglas de cantidades/precios/descuentos/tributos/cuerpo/resumen
al cerrar T02/T04. F08 §7.2–7.3, p.16 impide asumir dos decimales para todo. Usar `decimal`,
nunca `double` para montos, y pruebas de límites basadas en los ejemplos oficiales completos.
La holgura no es un ajuste silencioso de venta ni permiso para cuadrar diferencias arbitrarias.

## Identidad y secuencias

Cuatro identidades distintas: ID comercial, `OperationId`, Código de Generación y Número
de Control. El ledger liga solicitud/cuerpo/operador/ambiente y rechaza reutilizar un GUID con
otro contenido. La identidad fiscal se reserva una sola vez y se conserva ante incertidumbre.

F08 §7.1.2, pp.15–16 dispone consecutivo centralizado o por establecimiento; por POS/tipo
requiere autorización expresa, y contempla reinicio por ejercicio. Por eso **no** diseñar una
clave de secuencia independiente por tipo o caja como regla predeterminada. El ámbito final
se resolverá con emisor/ambiente/ejercicio y modo autorizado, respaldado por documentación;
la inclusión del tipo en la representación no implica secuencia propia. Confirmar representación,
longitudes y excepciones con T01–T04. Reserva con bloqueo transaccional y unicidad compartida,
no contador en memoria. No reciclar números por errores sin procedimiento oficial.

## Máquina de estados: tres planos separados

| Plano | Modelo actual / objetivo | Evidencia de transición |
| --- | --- | --- |
| Comercial | Venta `CONFIRMADA` y posventa actuales | Transacción comercial; fallo fiscal no la elimina |
| Solicitud/revisión | Estado de solicitud `PENDIENTE`; revisión `SIN_REVISAR`, `REVISADO`, `OBSERVADO` | Permiso, token/hash vigente, rowversion, ledger y bitácora actuales |
| Técnico futuro | Preparación → generación → validación local → firma → cola → envío → resolución o incertidumbre | Artefacto/hash/prueba durable por paso; etiquetas conceptuales internas, contrato de transición pendiente |
| Fiscal MH futuro | Resultado oficial y eventos posteriores | Respuesta/consulta oficial verificable, con interpretación versionada; no tomar etiquetas internas como códigos MH |

```mermaid
flowchart LR
  P[Preparación interna] --> G[Generación y snapshot]
  G --> V[Validación local]
  V --> F[Firma]
  F --> C[Cola durable]
  C --> T[Intento registrado]
  T --> R[Respuesta verificable]
  T --> U[RESULTADO_INCIERTO interno]
  U --> Q[Reconciliación oficial]
  Q --> R
  Q --> U
```

El diagrama expresa responsabilidades, **no** una máquina oficial final. Fallo de schema/firma
antes del envío queda como fallo local con evidencia. Tras posible envío, un reinicio, HTTP 200
sin cuerpo interpretable o cancelación no implica éxito ni reenvío seguro. La respuesta puede
indicar rechazo, observación u otra condición según contrato. F08 §10.1 describe estados
normativos, pero falta cerrar los literales/códigos API. Invalidación y contingencia conservan
su propio proceso; no se reducen a sobrescribir una columna del DTE original.

## Política de artefactos

Conservar bytes originales y hash SHA-256 como evidencia interna (no algoritmo de firma MH).
Separar resumen consultable de datos sensibles completos. No copiar el mismo JSON en snapshot,
cola, cada log e historial; referencias a un artefacto inmutable con dueño/control de acceso.
No deduplicar entre emisores/ambientes de modo que revele información por hash.

Decisión de diseño: catálogo y metadatos en SQL; almacenamiento de bytes acotados y política
de ubicación/backup a cerrar después de conocer límites oficiales. No introducir ahora una
carpeta compartida editable por todos. Si se usan archivos, escritura atómica, hash verificado,
ACL, rechazo de rutas/enlaces, recuperación de huérfanos y respaldo coordinado con SQL.
Retención y eliminación sólo desde política legal verificada; no inventar años de conservación.

## Plan aditivo de migraciones

1. Inventariar estado instalado y backup; aplicar todos los scripts pendientes **021, 022, 023,
   024, 025, 026**, sin saltos. Esta iteración no ejecuta cambios en la BD personal.
2. Fase B: definir identidad SQL, permisos reales y esquema de sesiones/revocación. Aprobación
   en DB-01/SEC-01 antes de DDL. Scripts de provisión de identidades sin contraseñas en Git.
3. Fase C: cerrar fuentes, modelo relacional, tipos/precisión y compatibilidad. Añadir perfiles,
   snapshots y contratos versionados mediante migraciones nuevas con sus restricciones.
4. Fases D–F: añadir identidad/numeración, artefactos, jobs/intentos/resultados y recuperación.
   Backfill sólo de metadatos demostrables; no convertir revisión en DTE generado.
5. Fases G–H: agregar agregado de eventos y entrega/RG conforme contratos, sin renombrar
   destructivamente ni reinterpretar `fis.EventosDte` histórico.

En DEV-50 no se creó 027. **DEV-51 asigna 027 a identidad interna del servidor**, independiente
del DDL fiscal aquí pendiente. La siguiente migración disponible es 028, sin diseño fiscal
aprobado aún. No anunciar un salto a la última como sustituto de los scripts anteriores.
Compatibilidad de clientes antiguos antes de activar funciones: versionado de capacidad,
rechazo de escritor incompatible y ventana coordinada. Una actualización no debe recrear la BD.

Cada bloque SQL necesita instalación limpia, actualización sobre 026 con datos sintéticos,
reaplicación según instalador, índices/constraints/FK, concurrencia, permisos, rollback y
recuperación. Reversión operativa por desactivación/versión compatible o restauración probada;
no borrar DTE ni deshacer una migración con datos fiscales mediante DROP automático.
