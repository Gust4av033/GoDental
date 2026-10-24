# Matriz normativa y de implementación

23/10/2026. F01–F08 y T01–T06 se definen en [Fuentes y contratos](Fuentes-y-Contratos.md).
Las páginas son ordinales del PDF. **Los PDF aportados no tienen vigencia/origen de descarga
verificados en línea.** Una referencia contextual no autoriza implementar un contrato externo.
“Pendiente” en código/test significa que no existe y no se atribuye aceptación fiscal a una prueba
interna. Las decisiones de seguridad/idempotencia también derivan del plan del usuario, SEC-01
y ARQ-01; no se presentan como campos o estados exigidos por Hacienda.

## Código y pruebas existentes referidos por la matriz

- **C1**: [FiscalWorkspaceService](../../src/GoDental.Application/Services/FiscalWorkspaceService.cs)
  y [FiscalModels](../../src/GoDental.Application/DTOs/FiscalModels.cs).
- **C2**: [repositorio de revisión](../../src/GoDental.Infrastructure/Operations/SqlMvpRepository.Fiscal.cs),
  [preparación](../../src/GoDental.Infrastructure/Operations/SqlMvpRepository.Orders.cs)
  y [ledger/transacciones](../../src/GoDental.Infrastructure/Operations/SqlMvpRepository.cs).
- **C3**: [FiscalView](../../src/GoDental.Wpf/Views/Operations/FiscalView.xaml)
  y [FiscalViewModel](../../src/GoDental.Wpf/ViewModels/Operations/FiscalViewModel.cs).
- **C4**: [posventa](../../src/GoDental.Infrastructure/Operations/SqlMvpRepository.Returns.cs)
  y [PDF comercial](../../src/GoDental.Infrastructure/Operations/ReceiptPdfRenderer.cs).
- **P1**: [FiscalTests](../../tests/GoDental.Tests/Ui/FiscalTests.cs), incluye
  `ReviewCannotSetFiscalStates`; sólo revisión interna.
- **P2**: [FiscalIntegration](../../tests/GoDental.DatabaseTests/FiscalIntegration.cs), 34
  comprobaciones de preparación/revisión sobre SQL aislado, no transmisión MH.
- **P3**: [Smoke WPF](../../tests/GoDental.SmokeTests/Program.cs), fiscal interno y PDF
  comercial; sin firma, sello, QR fiscal ni llamadas MH.
- **C5 / P4 (DEV-51)**: [host](../../src/GoDental.Server/Program.cs),
  [servicio de identidad](../../src/GoDental.Application/Services/ServerAuthenticationService.cs),
  [repositorio](../../src/GoDental.Infrastructure/Security/SqlServerAuthenticationRepository.cs),
  [conexión limitada](../../src/GoDental.Infrastructure/Data/ServerSqlConnectionFactory.cs),
  [22 pruebas HTTP](../../tests/GoDental.Server.Tests/ServerHttpTests.cs) y
  [35 comprobaciones SQL](../../tests/GoDental.DatabaseTests/ServerIdentityIntegration.cs).
  Identidad GoDental, no autenticación/credencial de Hacienda.

## Requisitos

| ID / requisito | Fuente / ubicación | Implementación real | Prueba / pendiente | Estado |
| --- | --- | --- | --- | --- |
| R01 Venta separada de solicitud/DTE | F08 §3, pp.9–11; plan §§3/6 | C1/C2 preparan solicitud de venta confirmada | P2: revisión no mueve caja/stock ni crea DTE/eventos | IMPLEMENTADO (interno) |
| R02 Revisión no equivale a aceptación | Plan §§3/13/25; DEV-44 | C1/C2/C3, revisión separada de solicitud | P1 rechaza estados fiscales; P2 conserva PENDIENTE | IMPLEMENTADO |
| R03 FE consumidor final sin opcionales forzados | F01 p.1; F08 anexos II/IV receptor; faltan T02/T04 vigentes | Preparación FE sin exigir cliente; no validador FE oficial | Actual no cubre emisión; casos FE sin datos/identificado pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R04 CCFE y receptor contribuyente | F08 anexos II/IV receptor; T02/T03/T04 | C1/C2 chequeo interno NIT/NRC/dirección/activo | P2 datos incompletos/actuales; validador CCFE oficial pendiente | IMPLEMENTADO interno; contrato pendiente |
| R05 Identificación, campos y condiciones por tipo | F08 §5/7, pp.12–17 y anexos II/IV; T02/T04 | No DTO JSON oficial | Fixtures por campo y condición pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R06 Código de Generación | F08 §7.1.1 p.15, anexo I §1.2; T01–T04 | No proveedor fiscal | Formato/versión/colisión/unicidad y no reutilizar OperationId pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R07 Número de Control / secuencia | F08 §7.1.2 pp.15–16, anexo I §1.3; T01–T04 | Secuencias actuales son comerciales | Reserva concurrente, ámbito autorizado y cambio de ejercicio pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R08 Establecimiento y punto de venta | F08 anexo I §1.3 p.37 y §1.4 p.38; F04; T03/T04 | Almacén/caja existentes no son mapeos MH | Mapeo explícito, códigos/catálogos y autorización pendientes | PREPARADO diseño |
| R09 Fechas / generación frente a transmisión | F08 §3/6/9, anexos identificación; T01/T02/T04 | Fechas comerciales existentes | Zona, ejercicio, límites, reloj y fecha fiscal oficial pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R10 Montos, cantidades, descuentos y tributos | F08 anexos II/IV cuerpo/resumen; T02/T03/T04 | Motor comercial no es calculador DTE | Ejemplos oficiales de impuestos/exentos/no sujetos según alcance pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R11 Redondeo y tolerancia | F08 §7.2–7.3 p.16, visualmente contrastado; T02/T04 | No DteAmountCalculator | Ejemplos oficiales/límites por campo pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R12 Snapshot inmutable y cambios de Cliente | F08 §11.3.1/12 pp.26–27; plan §§6/17 | C2 token de datos actuales; snapshot comercial conservado | P2 cambio receptor/dirección invalida revisión; snapshot fiscal emitido pendiente | PREPARADO diseño fiscal |
| R13 JSON/schema y versiones de catálogos | F08 §5 y anexo I §1.4–1.8; T02/T03/T04 | No serializador/validador oficial | Schema correcto/incorrecto, JSON válido/inválido, dependencias locales pendientes | PENDIENTE_DE_DOCUMENTACION_OFICIAL |
| R14 Firma/certificado | F08 §8 pp.17–18; F05 pp.7–14; T05/T06 | Sin firmador ni almacén fiscal aprobado; custodia de candidatos DEV-53 | Interoperabilidad, inválido/vencido, secreto ausente pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R15 Autenticación/recepción/transmisión | F08 §9 pp.18–21, anexo I §3 pp.41–42; F04 pp.18–19; T01 | Sin transporte fiscal | Éxito/rechazo/observación HTTP y funcionales según contrato pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R16 Sello y estados oficiales | F08 §10–10.1 pp.21–22; F03; T01/T02 | No sello real ni mapeo de respuesta | Sello ausente/incompatible, literal desconocido y cuerpo truncado pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R17 Resultado incierto e idempotencia | Plan §14; servicios concretos pendientes T01 | C2 ledger interno; no outbox/consulta MH | P2 rollback/concurrencia internos; reinicio tras POST/reconciliación pendientes | PREPARADO diseño |
| R18 Invalidación | F08 §13.1 y anexos III/V; F07 p.7; T01/T02/T04 | C4 anula solicitud interna; no evento MH | Plazos/causas/reemplazo/firma/reconciliación del evento pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R19 Contingencia | F08 §6.3/13.2 y anexos III/V; F07 p.7; T01/T02/T04 | No contingencia fiscal | Causas/plazos/evidencias/recuperación oficial pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R20 Otros eventos y posventa | F08 §13.3–13.4, anexos III/V; T02/T04 | C4 devoluciones comerciales; sin mapeo fiscal automático | Determinar alcance Retorno/Especiales/NCE sin inferencias | PENDIENTE_DE_CONTRATO_OFICIAL |
| R21 Representación gráfica / PDF / QR | F08 §11.3 pp.25–26 y columnas RG anexos II/III; T01–T04 | C4 PDF comercial; no RG DTE | P3 comercial; campos/leyendas/QR oficial/reimpresión pendientes | PREPARADO infraestructura PDF |
| R22 Entrega al receptor | F08 §11 pp.23–26, anexo I §4 p.42; T02/T04 | Descarga/impresión comercial | Archivo oficial+RG, evidencia, acceso sin correo obligatorio pendientes | PREPARADO diseño |
| R23 Consulta pública | F03 pp.6–17; F08 §14; mecanismo vigente T01 | No acción fiscal conectada | URL/parámetros oficiales y distinción frente a API pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |
| R24 TEST/PROD y autorizaciones por tipo | F05 pp.7–14; F07 pp.7/11; F08 anexo I §3.1 p.41 | Sin activación externa | Credenciales cruzadas, tipo no autorizado y preflight pendientes | PREPARADO diseño; credenciales pendientes |
| R25 Custodia, permisos y auditoría | F08 §12.2 p.27; plan §§8–12/25/26; SEC-01/DEV-45/51 | C1/C2 internos; C5 host/identidad/rol limitado; candidatos DPAPI/ACL DEV-53; S1 global abierto | P2 y P4 revocación/rollback/logs/SQL limitado; aprobación/importador fiscal y despliegue pendientes | IMPLEMENTADO interno / B1; S1 pendiente |
| R26 Pruebas mínimas/certificación | F06 p.8; F07 p.7; anexo 1 de T02 faltante | Ninguna prueba real MH | No fijar cantidades. Checklist por tipo/evento, respuesta real y autorización pendientes | PENDIENTE_DE_DOCUMENTACION_OFICIAL |
| R27 Integridad de fuentes | Plan §§2/4/5 | Inventario y verificador local de archivos | Ocho pruebas de hash/ausencia/extra/rutas/duplicado/JSON/bloqueo documental | IMPLEMENTADO / VALIDADO_LOCALMENTE; DEV-50 |

## Regla para cada implementación posterior

Cada nuevo campo/regla fiscal debe añadir una fila o desglosar la existente: edición/hash de
fuente, sección y página o ruta exacta de schema, código/miembro concreto, fixture oficial y
test. Esta matriz de fase A no sustituye esa granularidad. Un cambio de contrato invalida la
revisión aplicable y necesita análisis de impacto antes de activarse. Nunca sustituir las fuentes
faltantes por un test que simplemente refleje una suposición de implementación.

## Custodia genérica · DEV-53

R14/R24/R25 (según alcance de custodia): contrato Application `IFiscalSecretCandidateStore`, backend
`WindowsFiscalSecretCandidateStore`/`WindowsSecretDirectory`, comandos locales `SecretStoreCommand`,
pruebas `FiscalSecretModelsTests`/`WindowsSecretStoreTests` y frontera `ServerHttpTests`. Cubre
candidatos opacos, no aprobación, certificado, interoperabilidad ni contrato de firma oficial.
Evidencia final y límites en DEV-53; no cambiar estos requisitos a cumplimiento fiscal completo.
