# DEV-50 — Auditoría y preparación del nuevo plan DTE

23/10/2026, El Salvador. Primera iteración solicitada: doce entregables de especificación,
auditoría, arquitectura/modelos, seguridad y backlog antes de integración externa.
Fuentes de diseño consultadas: DM-01, DB-01, ARQ-01, SEC-01, DEV-44/45 y PDFs aportados.

## Estado real y baseline

- Se ejecutó `git fetch --all --prune` y fetch explícito de `feature/desarrollo`; rama remota
  y HEAD inicial coincidían en **657d836f5e13e036d0f3aa83cc3dab36ee6a78c9**.
- PR activo: [#19, draft](https://github.com/Gust4av033/GoDental/pull/19), asociado a la tarea.
  `main` tenía cambios posteriores: carpeta `DTEPDF.` con ocho PDF y `.gitkeep`, sin código
  nuevo. Se incorporó por merge en `feature/desarrollo` (`4a36917`), sin escribir sobre `main`.
- Antes de editar: restore y build Release con SDK 8.0.426 y Windows targeting en Linux,
  **0 warnings/errores; 418 xUnit correctas**. SQL/WPF no se ejecutan en Linux. Los conteos
  de CI #193 en documentación anterior son históricos, no se asumieron como nueva evidencia.
- Hay **26 migraciones, 001–026**. La última es `026_Online_Planificacion.sql`;
  revisión interna Fiscal se añadió en 024. No hay integración MH real ni migración fiscal nueva.

## Auditoría por área

| Área / código revisado | Resultado y requisito para recibir DTE |
| --- | --- |
| DEV-44, FIS-01 v0.1 | Preparación/revisión interna real; no especificación técnica externa. Se reemplaza FIS-01 por v0.2 con límites explícitos |
| FiscalView / FiscalViewModel | Bandeja, candidatas, revisión e historial. Sin emisión/firma/transmisión. El encabezado “Estado fiscal” mezclaba solicitud con DTE; se precisa “Estado de solicitud” |
| FiscalWorkspaceService / FiscalModels | Permisos, validación de GUID/rowversion/token/motivo y datos actuales. “REVISADO” es interno; chequeo CCFE no es schema oficial |
| SqlMvpRepository.Fiscal / Orders | Revalida SQL, bloqueo/rowversion/ledger/hash/auditoría; solicitud activa única, sin payload DTE. Receptor actual mutable invalida token de revisión |
| Ventas/POS/clientes | Venta confirmada independiente; FE puede prepararse sin cliente. Snapshot de comprobante comercial conservado, pero falta snapshot fiscal completo/inmutable y validadores FE/CCFE separados |
| Posventa (SqlMvpRepository.Returns) | Devoluciones/anulaciones comerciales conservadas; anula solicitudes internas pendientes y bloquea otros estados. No hay invalidación/evento MH/NCE automática; diseñar compatibilidad antes de emisión |
| PDF (ReceiptPdfRenderer y formatos) | QuestPDF y tamaños comerciales disponibles. No RG conforme, archivo oficial, QR fiscal ni entrega/consulta MH |
| Seguridad (SqlIdentityGateway / WorkstationConnectionFactory / DEV-45) | Hash PBKDF2/bloqueo y permisos presentes. WPF sigue SQL directo y pasa IdUsuario; no frontera confiable fiscal, mínimo privilegio instalado no demostrado |
| Operación durable (SqlMvpRepository / diario de operaciones) | Recupera operaciones SQL con GUID/hash/operador y exclusión compartida. No outbox de transmisión, consulta MH ni prueba de recepción externa; no reutilizar resolución SQL como prueba de ausencia MH |
| SQL 005 / 024 | Cimientos de solicitudes/DTE/log/configuración y revisión. Faltan ambiente/versiones/snapshots/artefactos/intentos/evento fiscal propio; ValorProtegido no cifra por sí mismo |
| Server/firma/transporte/secretos | No existe GoDental.Server, autenticación HTTP fiscal, firmador, transporte MH, store ni preflight. Diseño definido, implementación pendiente |

Evidencia de código/tests y fuente normativa por requisito en [matriz](FIS-01/Trazabilidad.md).
Las 34 comprobaciones de `FiscalIntegration` verifican preparación/revisión y concurrencia,
no DTE emitido. No se cierran hallazgos S1 por haber documentación o pruebas unitarias.

## Preparación realizada y límites

1. Incorporación de las ocho fuentes desde `main`, inventario exacto y estudio contextual;
   nombre portable **DTEPDF/**, sin cambiar bytes PDF. Hash/tamaño verificables en CI.
2. [FIS-01 v0.2](FIS-01/README.md), matriz y documentos de arquitectura/servidor, modelo de
   datos/estados/migraciones, secretos/certificados/rotación, TEST/PROD, seguridad, eventos,
   contingencia, reconciliación, representación/entrega y certificación.
3. ARQ-01 versionada para el servidor progresivo. Sin crear host vacío ni adaptadores que
   aparenten emisión; esta primera iteración entrega diseño, no toda la fase B.
4. Aclaración de estado de solicitud en UI; lógica comercial y revisión interna conservadas.
5. Verificador reproducible de integridad de fuentes y rutas Windows. Verifica material
   aportado; no certifica autenticidad de origen, vigencia ni autorización de Hacienda.

**No está todo preparado para recibir transmisión real.** La arquitectura y el backlog ya están
definidos; faltan servidor/identidad confiable/SQL mínimo/store y seis familias de fuentes técnicas.
El dominio `factura.gob.sv` está fuera de los destinos permitidos por la red de este entorno.
Por la instrucción expresa del plan se detiene la fase externa. No se inventan contratos ni
se descargan desde terceros. [Fuentes y contratos](FIS-01/Fuentes-y-Contratos.md) enumera
exactamente qué incorporar manualmente y cómo conservar origen/edición/hash.

No hay credenciales TEST, respuestas reales, autorización PROD ni cambios a la base personal.
No se pidió secreto alguno. **PRODUCCIÓN BLOQUEADA.**

## Validación del bloque

Validación local del bloque: restore correcto; Release sin advertencias/errores; ocho pruebas
del verificador documental correctas (deriva aun con igual tamaño, fuente ausente, PDF extra,
rutas inseguras/no portables, duplicado, JSON inválido y bloqueo de contratos faltantes).
Los ocho PDF se compararon byte por byte contra el commit original y todas las migraciones
001–026 contra el HEAD inicial: sin cambios. Enlaces locales de los quince documentos
FIS-01/DEV-50 comprobados. Consulta NuGet de siete proyectos sin problemas ni vulnerabilidades
conocidas; revisión limitada de cambios sin marcadores de clave privada añadidos.
`--require-contracts` devuelve 2, como se espera mientras falten T01–T06.
La nueva validación no equivale a VALIDADO_EN_TEST_MH. Resultado de xUnit/SQL/WPF final
y SHA de CI se registrarán tras completar la ejecución Windows.

## Continuación exacta

[Backlog A–J](FIS-01/Backlog.md). Próximo: A2 si hay fuentes oficiales disponibles; mientras
falten, B1 (servidor con autenticación real) y diseño/provisión B2, sin activar DTE externo.
No saltar a generación/firma/POST por disponer de una tabla base. FE/CCFE/eventos requeridos
primero; Online externo y escalas/listas/promociones después de esta nueva prioridad.

En la PC personal siguen pendientes **desde 021, incluida: 021 → 022 → 023 → 024 → 025 →
026**. Se aplican todas, no únicamente la última. No hay 027 en esta iteración.
