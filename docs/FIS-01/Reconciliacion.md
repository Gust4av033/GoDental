# Resultado incierto y reconciliación

**PREPARADO — no existe consulta MH implementada.** Protocolo concreto, endpoints, claves
de consulta, códigos, plazos, estados de lote y condiciones de reenvío:
PENDIENTE_DE_CONTRATO_OFICIAL (T01/T02). Consulta pública F03 no es sustituto de ese contrato.

## Invariantes internas

- Timeout, cancelación, conexión interrumpida, proceso terminado o respuesta ilegible tras
  posible envío no son aceptación ni rechazo definitivo: `RESULTADO_INCIERTO` interno.
- Conservar DTE/evento, código/control, snapshot, bytes firmados/enviados, hash, ambiente,
  emisor, intento y correlación. No generar otro documento para “probar otra vez”.
- `OperationId` durable es distinto de la identidad fiscal. Ledger SQL actual no demuestra si
  Hacienda recibió algo. La resolución “ausente” de una operación SQL no se aplica a un POST MH.
- No retry genérico de POST en HttpClient/resilience; sólo acciones expresamente permitidas
  por el contrato para esa evidencia y estado. Error HTTP y código funcional se interpretan juntos.

## Flujo objetivo

1. Registrar en SQL la intención/intento con artefacto y hash antes de IO; distinguir trabajo
   que nunca comenzó de aquel cuyo envío pudo ocurrir. Recuperar ambos al arrancar el host.
2. Una respuesta completa se valida contra contrato, identidad/ambiente y versión. Guardar
   original protegido y resultado interpretado en transacción; un HTTP exitoso no basta.
3. Si hay incertidumbre, cerrar el intento técnico dejando el documento sin resultado fiscal
   definitivo. UI muestra incertidumbre y operación durable, no “volver a cobrar”.
4. Worker reclama consulta con lease/fencing compartido y usa **servicio oficial definido por
   T01**. Un proceso antiguo no sobrescribe la resolución obtenida por otro.
5. Persistir cada consulta y su evidencia. “No encontrado”, error de consulta o servicio temporal
   no se interpretan automáticamente como permiso de reenvío. Respetar consistencia/plazos
   documentados. Si la fuente no define salida segura, revisión humana sin alterar evidencia.
6. Sólo el protocolo oficial puede habilitar una nueva acción sobre el mismo artefacto o indicar
   corrección/sustitución. Conservar historia y autorización; no inventar un DTE de reemplazo.

Sin contrato disponible, el diseño mantiene **envío deshabilitado**, no una consulta simulada.
La interfaz operativa futura permitirá consultar estado/evidencia y pedir reconciliación con
permiso específico; no editar sello ni asignar resultado MH manualmente.

## Pruebas requeridas en fase F

Caída antes de intención; después de intención antes de red; MH procesa y se pierde respuesta;
respuesta recibida antes de commit; dos workers; lease vencido; respuesta tardía; cuerpo
truncado; estado desconocido; identidad discordante; consulta sin respuesta; consulta que
resuelve el intento; reinicio con permisos revocados; reintento idempotente oficialmente permitido.
Fixtures etiquetados sintéticos, definidos a partir del contrato. Evidencia real TEST fuera de CI.
