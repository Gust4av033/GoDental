# Contingencia fiscal

**PREPARADO / PENDIENTE_DE_CONTRATO_OFICIAL.** F08 §6.3/13.2 y anexos III/V; T01/T02/T04
pendientes. No hay modo de contingencia fiscal habilitado. Timeout técnico no equivale a
contingencia ni permite emitir/entregar sin el procedimiento exigido.

El diseño conservará causa y evidencia, inicio/fin, operador autorizado, emisor/ambiente,
documentos/eventos afectados, versión de regla, evento y sus intentos/resultados, plan de
recuperación y transmisión posterior, auditoría y representación correspondiente. Relacionar
artefactos originales, no reconstruirlos desde Clientes al recuperar.

Antes de implementar se deben extraer de fuentes vigentes: supuestos admitidos, quién informa,
plazos, límites, códigos de motivo, orden de transmisiones, tratamiento de documentos/eventos,
forma de cierre y respuesta al rechazo. No inferir esos valores de un error HTTP, del tiempo
que el operador estuvo sin Internet o de documentación de otra plataforma.

Recorrido objetivo: incidente técnico identificado → evaluación de supuesto fiscal autorizado →
registro y autorización según norma → generación/entrega si el procedimiento lo permite →
evento/recuperación/transmisión en orden oficial → reconciliación de inciertos → cierre con
evidencias. No cronómetro ni botón “saltar Hacienda”; tampoco cambiar a PROD/TEST para evitar
una incidencia. La operación comercial conserva sus datos y se informa la limitación fiscal.

F07 p.7 exige contemplar pruebas de contingencia para Sistema de Transmisión. Los escenarios
y cantidades del Manual Funcional/anexos siguen pendientes. CI ensayará fixtures explícitamente
sintéticos y reinicios; el cumplimiento real TEST requiere acceso/evidencia fuera de CI.
