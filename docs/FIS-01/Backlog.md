# Backlog de implementación DTE — orden vigente

23/10/2026. Sustituye planes DTE previos. No agrega otros tipos por existir en catálogos.
Alcance inicial FE + CCFE y eventos oficialmente requeridos para Sistema de Transmisión.
Módulos Online existentes se conservan; siguientes conectores Meta y escalas/listas/promociones
quedan después de esta prioridad, salvo correcciones necesarias para mantener el MVP.

| Orden | Bloque verificable | Entrada | Salida exigida / estado actual |
| --- | --- | --- | --- |
| A1 | Auditar repo y baseline, incorporar fuentes aportadas, preparar FIS-01 v0.2 e inventario | HEAD remoto/PR/001–026 reales | Esta iteración; evidencia en DEV-50. No equivale a contratos finales |
| A2 | Obtener/leer T01–T06 vigentes y verificar F01–F08 | Fuentes desde factura.gob.sv, URL/edición/hash/términos | PENDIENTE_DE_DOCUMENTACION_OFICIAL; cerrar matriz a nivel de campo/regla y resolver contradicciones |
| A3 | Cerrar modelo fiscal definitivo/DDL y arquitectura de adaptadores | A2 y revisión ARQ-01/DB-01/SEC-01 | PENDIENTE_DE_CONTRATO_OFICIAL; sin endpoints o longitudes adivinados |
| B1 | Host GoDental.Server .NET 8 y autenticación de operador | ARQ-01 v0.2, política SEC-01 / DEV-51 | IMPLEMENTADO / VALIDADO_LOCALMENTE: CI #203, 22 HTTP y 35 SQL nuevas; regresión 440 xUnit / 825 SQL / 1106 WPF; sin emisión |
| B2 | Autorización, identidad SQL y acceso a evidencias | B1, roles/provisión runtime/migración diseñados | Parcial DEV-51/55: permisos actuales, rol EXECUTE completo, barrera de arranque y diagnóstico de SID/TLS/SQL; CI #231 correcto: 503 xUnit / 829 SQL / 1268 WPF. Faltan provisión/TLS real, estaciones y operaciones/evidencias fiscales, cierre SEC-01 |
| B3 | Secret store y TEST/PROD | B1/B2; custodia genérica interna; formato final tras T05/T06 | Base implementada DEV-53: candidatos opacos Windows/DPAPI/ACL, particiones/versiones/revocación y diagnóstico local; CI #215 correcto (468 xUnit / 825 SQL / 1170 WPF); importador/aprobación/auditoría/recuperación entre hosts pendientes |
| C1 | Registro de contratos, catálogos y schemas locales versionados | A2/A3 y términos aprobados | Pendiente; validación sin red, referencias resueltas localmente, hash/edición por DTE |
| C2 | Perfiles/autorizaciones/mapeos y snapshot inmutable | C1, migración aditiva aprobada | Pendiente; FE sin opcionales indebidos, CCFE independiente, Cliente posterior no altera snapshot |
| C3 | Modelos/validadores FE/CCFE y DteAmountCalculator | C1/C2, ejemplos oficiales | Pendiente; fixtures y trazabilidad por regla/campo, sin alterar cálculo comercial por inferencia |
| D1 | Proveedores de código/control y reserva concurrente | C2/C3 y reglas de ámbito oficial | Pendiente; identidad distinta del ledger, modo central/establecimiento correcto, límites/ejercicio probados |
| D2 | Generación FE y CCFE y validación JSON Schema | D1, C1–C3 | Pendiente; artefactos inmutables y evidencia de schema; aún sin transmisión |
| E1 | Adaptador del Firmador oficial / IDteSigner | T05/T06 y B3/D2 | Pendiente; interoperabilidad, pertenencia/vigencia, fallo seguro y ninguna clave en cliente/logs |
| F1 | Autenticación/transporte MH TEST | T01/T02, B1–B3/E1, S1 aplicable cerrado | Pendiente documentación y credenciales; HttpClientFactory, destinos aprobados, respuestas factuales |
| F2 | Outbox/intentos/consulta/reconciliación/reinicio | F1 y modelo durable aprobado | Pendiente; corte tras envío/timeout/dos workers resueltos sin identidad duplicada ni POST ciego |
| G1 | Invalidación | Contrato oficial, F2 y permiso real con migración nueva | Pendiente; efectos sólo acreditados por respuesta, posventa y original conservados |
| G2 | Contingencia y otros eventos sólo aplicables | T02/anexo vigente, G1/F2 | Pendiente; supuestos/plazos/casos oficiales y recuperación, no fallback técnico arbitrario |
| H1 | Representación/QR | F2/G, contrato RG y consulta verificados | Pendiente; fidelidad, leyendas TEST, reimpresión sin nueva emisión y formatos conformes |
| H2 | Entrega/consulta pública | H1 y mecanismos oficiales | Pendiente; archivo oficial+RG, evidencia, sin correo obligatorio, autorización de descarga |
| I1 | Certificación TEST FE/CCFE/eventos | A–H, acceso real TEST y tabla de pruebas vigente | PENDIENTE_DE_CREDENCIALES y de pruebas reales; conservar evidencia oficial de cada caso |
| J1 | Autorización por tipo y preflight PROD | Certificación/evidencia formal, secretos PROD, backup/restauración, S1 cerrado | PRODUCCIÓN BLOQUEADA; sólo activar tras checklist completo y aprobación registrada |

B1 y el diseño/provisión interna de B2 pueden avanzar mientras se obtienen los documentos,
porque no definen un contrato fiscal externo. B3 puede diseñar custodia genérica, pero no importar
un formato de certificado supuesto. C–H dependen de fuentes verificadas; F/I/J dependen además
de acceso real y evidencias. La espera no se resuelve con mocks de aceptación ni con cambiar
estados manualmente. No se implementa todo B–J en esta primera iteración.

## Próxima sesión

Leer DEV-50, FIS-01 v0.2 y este backlog. Verificar nuevamente fetch/HEAD/PR/migraciones y
baseline. Primero A2 si se aportaron archivos oficiales; si siguen faltando, continuar **B2/B3**
desde DEV-51/DEV-53/DEV-55. B1 ya tiene host/autenticación/sesiones; B3 tiene base de candidatos; DEV-55 prepara comprobación de despliegue; faltan provisión/TLS real y fronteras de las
operaciones/evidencias fiscales e importador/aprobación fiscal de secretos. No crear permisos/emisión sin caso real.
No migrar WPF completo. Política inicial de sesión definida en SEC-01.

Con cada bloque: commits por responsabilidad, validación local y Windows según corresponda,
actualización de matriz/estado/fuentes, y continuación explícita. No modificar `main` directamente.
El usuario confirma **027 aplicada** y código actualizado (24/10/2026). DEV-55 no añade
migraciones. En otra PC aplicar sólo pendientes del tramo **021 → 022 → 023 → 024 → 025 →
026 → 027** en orden; ninguna se aplicó desde cloud. No repetir pendientes históricos como
si estuvieran confirmados en la PC ya actualizada.
