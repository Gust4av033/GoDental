# Invalidación y otros eventos fiscales

**PREPARADO / PENDIENTE_DE_CONTRATO_OFICIAL.** F08 §13, anexos III/V y F07 p.7 aportan
contexto; T01/T02/T03/T04 definen contratos, plazos, causas, campos, relaciones y pruebas mínimas.
Sin estas fuentes no hay endpoint, formulario de invalidación fiscal ni evento generado.

`fis.EventosDte` histórico es una bitácora genérica, no un evento fiscal completo. El nuevo
agregado conservará identidad, snapshot/payload, referencias, contrato, firma, intentos, respuesta,
sello y auditoría propios. No borrar original ni modificar sus bytes; conservar la relación entre
evento y documento/eventos afectados según normativa. Distinguir estado del evento del efecto
fiscal acreditado sobre su original; sólo una respuesta verificable aplica ese efecto.

## Invalidación

La anulación comercial o `ANULADO` de una solicitud interna **no** invalida un DTE. Antes de
exponer la acción, definir desde las fuentes: sujeto autorizado, tipo/documento elegible, causa,
plazo, referencias/reemplazo si corresponde y tratamiento de rechazo/incertidumbre. Nunca
crear un reemplazo o cambiar la venta automáticamente por asumir que una invalidación funcionó.

Operación objetivo: obtener original y estado oficial → evaluar elegibilidad/permiso actual →
congelar evento y referencias en transacción → validar schema → firmar → transmitir →
resolver/reconciliar → registrar efecto confirmado y representación/entrega cuando corresponda.
Toda autorización excepcional debe tener evidencia, no un bypass administrativo genérico.

## Alcance inicial y posventa

FE/CCFE más eventos obligatorios para su certificación en Sistema de Transmisión. F07 p.7
incluye invalidación y contingencia en pruebas; no fija aquí cantidades. NCE/NDE/NRE/FEXE y
eventos de Retorno/Operaciones Especiales requieren análisis de aplicabilidad y autorización
separados. “Devolución de mercadería” no implica Evento de Retorno ni Nota de Crédito.

La posventa existente continúa según sus reglas. Hoy bloquea ciertos documentos fiscales
fuera de solicitud pendiente/anulada; antes de DTE real hay que diseñar y probar el recorrido
comercial/fiscal posterior, sin reemplazar reglas por un UPDATE de estado ni perder crédito,
caja, stock, evidencia o auditoría. Identificar decisión fiscal requerida y responsable; no dejar
la UI prometiendo que una anulación comercial corrigió el documento en Hacienda.

Pruebas: permiso, elegibilidad, plazo/catálogo oficial, original inexistente/ambiente distinto,
concurrencia sobre original, firma/artefacto inmutable, respuesta rechazada/incierta, evento ya
aplicado, recuperación, ausencia de doble efecto y conservación de comercial/historial.
