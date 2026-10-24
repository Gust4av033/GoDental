# Validación GoDental y checklist de certificación Hacienda

**PREPARADO. Ninguna prueba real Hacienda ejecutada.** F06 p.8 remite las cantidades/casos
de Sistema de Transmisión al anexo 1 del Manual Funcional (T02). F07 p.7 incluye pruebas de
invalidación y contingencia. Ese anexo y sus dependencias están
PENDIENTE_DE_DOCUMENTACION_OFICIAL; no se define un número de pruebas por suposición.

## Cobertura local a implementar por bloques

| Bloque | Escenarios mínimos internos | Dependencia oficial |
| --- | --- | --- |
| B: frontera | Anónimo/identidad falseada, permisos revocados, sesión aislada, rate limit, SQL mínimo, secreto ausente/cruzado, logs sin material sensible | Diseño de seguridad interno; formato/custodia final T05/T06 |
| C: receptor/snapshot | FE sin opcionales e identificado, CCFE válido/incompleto, cambios posteriores de Cliente/emisor no alteran snapshot | T02/T03/T04 |
| C: cálculo | Cantidades/precios/descuentos/impuestos, límites/negativos permitidos, cuerpo/resumen/holgura y ejemplos oficiales | F08 §7.2–7.3 y T02/T04 |
| C–D: contratos | JSON válido/inválido, schema correcto/incorrecto, dependencias locales, catálogo/versiones y fechas | T01–T04 |
| D: identidad | Código generación, control/ámbito autorizado, establecimiento/POS, simultaneidad de estaciones y cambio de ejercicio | F08 §7.1 y T01–T04 |
| E: firma | Interoperabilidad firmador, certificado no válido/vencido, formato incompatible, error sin revelar secreto | T05/T06; material ficticio compatible sólo si contrato lo permite |
| F: transporte | Éxito/rechazo/observación y estados funcionales, HTTP erróneo, timeout, respuesta ambigua, recuperación/reconciliación, reintento permitido idempotente | T01/T02 |
| G: eventos | Invalidación/contingencia, relación/efecto sobre original, permisos, concurrencia, incertidumbre y recuperación | T01–T04, anexo mínimo T02 |
| H: representación | PDF/QR oficial, fidelidad/leyendas, reimpresión, entrega sin correo, acceso y consulta pública | F08/F03 y T01–T04 |
| Transversal | Auditoría, rollback, corte de proceso, TEST/PROD separados, acceso a artefactos, backup/restauración | Diseño interno y contratos aplicables |

CI normal: fixtures identificados sintéticos, `FakeDteTransport` cuando exista el contrato y
material ficticio creado para pruebas; nunca credenciales reales ni requests a Hacienda. Los
tests internos de revisión actuales no cuentan como FE/CCFE firmadas/transmitidas. No dar
al usuario la impresión de “sellado” a partir de un fixture de aceptación.

Después de cada bloque: restore, Release, unit tests, SQL aislado, WPF relevante, análisis
NuGet, migraciones/reaplicación, warnings, logs y diff. Registrar SHA/entorno/resultados
nuevos. Linux con Windows targeting no ejecuta LocalDB ni WPF; documentar CI Windows.

## Checklist real TEST (fuera de CI ordinario)

- [ ] Fuentes técnicas verificadas y matriz completa para el alcance FE/CCFE/eventos exigidos.
- [ ] Solicitud de acceso al **Sistema de Transmisión**, distinta del portal de Facturación;
  habilitación efectiva del emisor/tipos para TEST y contacto administrativo del negocio.
- [ ] Credenciales TEST instaladas mediante mecanismo local seguro, sin compartirlas aquí.
- [ ] Servidor, identidad SQL, secretos y aislamiento probados; firma oficial interoperable.
- [ ] Tabla de pruebas oficiales transcrita desde T02 vigente: identificador, tipo/evento,
  condición, resultado esperado oficial, fuente/edición y evidencia de ejecución.
- [ ] Cada caso ejecutado explícitamente en TEST con respuesta real, artefactos protegidos,
  hash/identidad/fecha y resultado factual. No sólo captura del botón ni una fila manual SQL.
- [ ] Rechazos y resultados inciertos resueltos según contrato, sin saltar excepciones.
- [ ] Evidencia de requisitos completos en el portal y autorización formal **por tipo**, según
  F07; no inferir autorización de un DTE exitoso aislado.
- [ ] Configuración/certificado/credencial PROD obtenidos separadamente cuando corresponda.
- [ ] [Preflight PROD](TEST-y-PROD.md) revisado con todos sus puntos antes de activación.

Evidencias con PII/respuestas reales en almacén privado del negocio, nunca en Git/artifact público.
Documentar sólo IDs de evidencia interna, fecha, versión y conclusión sin contenido sensible.
La cadena final exigida es venta → snapshot → schema oficial → firma → TEST real → resultado
factual → persistencia/reconciliación → RG/entrega/consulta → auditoría. Hasta entonces usar
“PREPARADO” o “VALIDADO_LOCALMENTE” con alcance, nunca “emite DTE reales”.
