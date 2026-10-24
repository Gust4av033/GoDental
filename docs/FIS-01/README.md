# FIS-01 — Fiscal y Sistema de Transmisión DTE

**Versión:** v0.2 · **Fecha:** 23/10/2026 · **Estado:** PREPARADO (especificación inicial).
La integración externa está en PENDIENTE_DE_DOCUMENTACION_OFICIAL /
PENDIENTE_DE_CONTRATO_OFICIAL y PENDIENTE_DE_CREDENCIALES. No está VALIDADO_EN_TEST_MH
ni AUTORIZADO_PARA_PRODUCCION. **PRODUCCIÓN BLOQUEADA.**

Este plan sustituye los planes DTE anteriores. DM-01 y DB-01 conservan el comportamiento
comercial; [ARQ-01 v0.2](../ARQ-01/README.md) define la introducción progresiva del servidor.
La auditoría y evidencia de esta iteración están en [DEV-50](../DEV-50-Preparacion-DTE.md).
El análisis disponible fija arquitectura e invariantes internas; los contratos externos y el DDL
definitivo se cerrarán después de verificar las fuentes técnicas vigentes.

## Entregables y recorrido de lectura

| Entregable | Documento |
| --- | --- |
| Auditoría del repositorio y preparación | [DEV-50](../DEV-50-Preparacion-DTE.md) |
| Fuentes, versiones y contratos faltantes | [Fuentes y contratos](Fuentes-y-Contratos.md), [inventario con hashes](fuentes-oficiales.json) |
| Requisito → fuente → sección/página → código → prueba | [Trazabilidad](Trazabilidad.md) |
| Arquitectura y diseño de GoDental.Server | [Arquitectura y servidor](Arquitectura-y-Servidor.md) |
| Modelo de datos, estados y migraciones | [Modelo y migraciones](Modelo-y-Migraciones.md) |
| Amenazas, identidad y controles S1 | [Seguridad fiscal](Seguridad-Fiscal.md) |
| Custodia, instalación y rotación | [Secretos y certificados](Secretos-y-Certificados.md) |
| Separación de ambientes y preflight | [TEST y PROD](TEST-y-PROD.md) |
| Resultado incierto y recuperación | [Reconciliación](Reconciliacion.md) |
| Invalidación y demás eventos | [Eventos](Eventos.md), [Contingencia](Contingencia.md) |
| PDF, QR, entrega y consulta pública | [Representación y entrega](Representacion-y-Entrega.md) |
| Pruebas locales y certificación real | [Certificación](Certificacion.md) |
| Bloques de implementación A → J | [Backlog](Backlog.md) |

## Entidades y responsabilidades

**Venta != SolicitudFiscal != DTE != EventoDTE != Transmisión != RepresentaciónGráfica.**

- **Venta**: operación comercial, stock, cobro/crédito y comprobante interno existentes.
  Un fallo de Hacienda conserva la venta confirmada y su historial. No se revierte un cobro
  para compensar un timeout fiscal.
- **SolicitudFiscal**: intención interna ligada a la venta, actualmente en
  `fis.DocumentosFiscales`. Preparación FE/CCFE y revisión: IMPLEMENTADO.
- **DTE**: artefacto fiscal con snapshot propio, versiones de contratos/catálogos, identidad
  fiscal y evidencia de validación/firma/respuestas; todavía no generado por GoDental.
- **EventoDTE**: documento/evento fiscal independiente relacionado con originales,
  con contenido, firma y resultado según contrato oficial. No es una línea de log.
- **Transmisión**: intento durable de enviar un artefacto concreto; no crea otra venta ni otra
  identidad fiscal por repetirse. Respuesta técnica y resultado fiscal son distintos.
- **RepresentaciónGráfica**: interpretación fiel/versionada del artefacto. El PDF no es el DTE.
  Reimprimir no solicita generación ni transmisión.

## Compatibilidad con la preparación interna

La solicitud `PENDIENTE` tiene un eje de revisión separado:
`SIN_REVISAR → REVISADO / OBSERVADO`. La expresión funcional
`PENDIENTE → REVISADO / OBSERVADO` describe el recorrido de trabajo, **no** un cambio del
estado fiscal. `ANULADO` de una solicitud interna no es invalidación de Hacienda.

`REVISADO` no significa generado, firmado, transmitido, recibido, sellado, aceptado ni
autorizado por Hacienda. Es revisión de los datos actuales para preparar un futuro intento de
generación. Hoy el token detecta cambios de receptor/detalle; no existe snapshot fiscal emitido.
Antes de generar se revalidará y congelará el snapshot de forma atómica. Cambios posteriores
de Clientes no alterarán los bytes de un DTE generado.

`OBSERVADO` interno tampoco es la condición descrita por Hacienda para un documento
transmitido. Las pantallas deben indicar siempre qué eje muestran.

## FE y CCFE

Habrá validadores y modelos fiscales independientes, alimentados por contratos oficiales
versionados. El chequeo interno CCFE de NIT/NRC/dirección **no** certifica un CCFE válido.
La FE actual permite venta sin cliente; conservar esa capacidad. No añadir obligación universal
de DUI, NRC, correo, teléfono o dirección. F01 y F08 documentan supuestos de identificación,
pero umbrales/excepciones requieren contraste de vigencia, Manual Funcional y schemas antes
de implementarse. No copiar restricciones de alta de emisor al receptor de una FE.

Los DTO de ventas/clientes no se serializarán como DTE. Recorrido objetivo:
`VentaSnapshot → FiscalSnapshot → modelo FE/CCFE → JSON → schema oficial → firma`.
El cálculo fiscal será especializado, con decimales y ejemplos oficiales; no se modificará el
cálculo comercial por inferencia. F08 §7.2–7.3 distingue cuerpo/resumen y holgura.

## Identidad, concurrencia y evidencia

`OperationId interno != CódigoGeneración fiscal != NúmeroControl != número de venta`.
El ledger actual protege operaciones SQL internas. La transmisión necesita además outbox,
intentos, exclusión compartida y reconciliación por contrato oficial. Un GUID no garantiza
“exactamente una vez”.

Toda generación tendrá snapshot inmutable, versión de fuentes/schema/catálogos, emisor,
ambiente, autorización por tipo, establecimiento/POS explícitos y hash por artefacto.
Guardar exactamente lo generado, firmado, enviado y recibido, con política de acceso y
retención. Un rechazo no permite editar silenciosamente el artefacto histórico ni borrarlo.
Timeout o caída posterior al envío → `RESULTADO_INCIERTO` interno; conservar identidad y
evidencia, consultar/reconciliar antes de cualquier nueva acción de envío.

## Frontera confiable y activación

WPF → HTTPS → GoDental.Server → Application/Domain → SQL. Android futuro usa esa misma
frontera. Sólo el servidor custodia secretos, firma, autentica ante Hacienda y transmite.
Se conservará temporalmente el acceso SQL comercial, identificado como deuda S1; no se
reutilizará como mecanismo confiable para autorizar emisión remota.

Identidad del operador desde contexto autenticado, permisos revalidados, identidad SQL mínima,
secretos separados TEST/PROD y preflight verificable. Ningún campo editable convierte una
revisión interna en aceptación MH. PROD exige evidencia formal por tipo, contratos vigentes,
credenciales correctas, pruebas, restauración y S1 cerrado.

## Estados documentales

| Estado | Significado y evidencia requerida |
| --- | --- |
| IMPLEMENTADO | Código real presente; declarar por separado dónde se ejecutó |
| PREPARADO | Diseño/contrato interno o requisito disponible, no recorrido externo |
| PENDIENTE_DE_DOCUMENTACION_OFICIAL | Falta fuente o verificación de vigencia/origen |
| PENDIENTE_DE_CONTRATO_OFICIAL | Falta definir dato/comportamiento desde esa fuente |
| PENDIENTE_DE_CREDENCIALES | Requiere instalación segura local, nunca datos en el chat |
| VALIDADO_LOCALMENTE | Pruebas internas reproducibles; no aceptación MH |
| VALIDADO_EN_TEST_MH | Respuestas reales TEST verificadas, fuera de CI normal |
| AUTORIZADO_PARA_PRODUCCION | Autorización formal por tipo y preflight aprobado |

El estado de un documento de diseño no se hereda al módulo. Esta iteración no crea emisor,
certificado, JSON fiscal, firma, QR fiscal, autorización o sello ficticios. Migraciones personales
pendientes: **021 → 022 → 023 → 024 → 025 → 026 → 027**, todas en orden. DEV-51 añade
GoDental.Server B1/identidad interna con 027; firma, transporte, secretos fiscales y producción
siguen pendientes. Ver [configuración del servidor](../Servidor-Configuracion.md).

Base B3 de custodia genérica: [DEV-53](../DEV-53-Custodia-Secretos.md), candidatos opacos
Windows sin aprobación/importación fiscal, sin migración nueva. No cierra custodia fiscal S1.
