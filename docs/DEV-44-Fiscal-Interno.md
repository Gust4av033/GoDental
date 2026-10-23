# DEV-44 — Prioridad de módulos nuevos: Fiscal interno

23/10/2026, El Salvador. Diseño registrado antes de implementar SQL/contratos.
Fuentes: DM-01 §13/15, DB-01 fiscal, FIS-01, ONL-01, ARQ-01 y SEC-01.
El usuario prioriza módulos nuevos antes de escalas/listas/promociones y confirma que todavía
no tiene ambiente de pruebas de Hacienda: preparar módulo interno. Solicita revisión de seguridad.

## Secuencia vigente

1. Fiscal interno: bandeja de solicitudes, venta asociada, revisión de datos y observaciones.
2. Online interno según ONL-01: fuentes/prospectos, exclusiones/deduplicación y trazabilidad;
   cerrar consentimiento, fuentes/canales y conversión antes de habilitar acciones externas.
3. Integración DTE de pruebas cuando se disponga de requisitos, esquemas y acceso verificables.
   Si siguen pendientes, registrar el bloqueo y continuar trabajo independiente de Online.
4. Escalas, listas y promociones; luego los demás complementos priorizados de DEV-43.
5. Endurecimiento final de seguridad, instalación/actualizaciones/respaldos y piloto formal.
   Los riesgos altos/críticos se corrigen antes de habilitar su función, no se posponen al final.

## Entrega acotada y controles

- Nuevo módulo Fiscal con bandeja filtrada/paginada y búsqueda por documento comercial;
  venta/cliente se consultan con FISCAL.VER + VENTAS.VER. Crear solicitud reutiliza el contrato
  PendingFiscal y su ledger existentes, con FISCAL.CONFIGURAR. Una venta no crea dos solicitudes
  activas o de distinto tipo. FE/CCFE son solicitudes internas; no son DTE emitidos.
- Ficha muestra la venta, datos originales del comprobante y datos fiscales actuales del cliente,
  con dirección fiscal; cambios se hacen desde Clientes. No expone payloads, tokens, certificados,
  valores de ConfiguracionFiscal ni respuestas técnicas completas.
- Revisión interna REVISADO/OBSERVADO exige permiso específico FISCAL.REVISAR_INTERNO,
  rowversion, token de datos consultados, motivo, usuario/estación, ledger y bitácora atómicos.
  REVISADO sólo cuando la venta sigue CONFIRMADA, el tipo es FE/CCFE, el documento PENDIENTE,
  no tiene DTE asociado y, para CCFE, cliente activo con NIT/NRC/dirección fiscal disponibles.
  Estas comprobaciones son operativas; no validan normativa ni esquema oficial de Hacienda.
- Huella de datos compara venta/líneas y datos actuales del receptor; un cambio posterior deja
  la revisión visible como desactualizada. Otra estación no sobrescribe revisión con versión/token
  anterior. Historial paginado desde la bitácora de esta operación, sin JSON crudo.
- Nunca cambia Estado fiscal a GENERADO/ACEPTADO ni crea JSON, códigos fiscales, sellos,
  firmas, eventos de transmisión o movimientos financieros. Sin adaptador de red/firma en DI.
  No hay botón que simule emisión ni importación de respuestas fiscales no verificadas.
- WPF/MVVM, permisos en Application y SQL, consultas asíncronas, respuestas tardías descartadas,
  UI Light/Dark/compacto y recuperación durable del mismo GUID. Sin alterar solicitudes antiguas.

## Migración 024 — diseño aditivo e idempotente

fis.DocumentosFiscales añade RowVersion rowversion; RevisionInterna nvarchar20 NOT NULL
DEFAULT SIN_REVISAR CHECK SIN_REVISAR/REVISADO/OBSERVADO; ObservacionRevision nvarchar500 NULL;
FechaRevision datetimeoffset0 NULL; RevisadoPor int NULL FK seg.Usuarios; HuellaRevision char64 NULL.
CHECK coherencia entre estado interno/fecha/autor/huella/motivo. No rellenar revisiones antiguas.
No añade tablas: 79 tablas / 8 vistas / 18 procedimientos. Permiso FISCAL.REVISAR_INTERNO,
Administrador por defecto, preservando concesiones/denegaciones existentes. Revisión en bitácora.

## Actualización pendiente en la PC del usuario

**Faltan desde la 021, incluida: 021 → 022 → 023 → 024.** Si se agrega otra migración después,
continuar en orden; los archivos no son acumulativos. Actualizar ambas aplicaciones y volver a
iniciar sesión tras migrar para obtener los permisos nuevos. Respaldar BD y carpeta de evidencias.
No se han aplicado estas migraciones a la BD personal desde cloud.

En PowerShell, raíz del repositorio, para GoDental_Development con marcador reconocido:

```powershell
git switch feature/desarrollo
git pull --ff-only origin feature/desarrollo
$servidor = Read-Host 'Instancia SQL guardada en Ajustes'
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --install-development --server "$servidor" .
dotnet run --project src/GoDental.Wpf -c Release
```

La herramienta recorre las migraciones en orden, incluidas 021–024. Para otra base, ejecutar sólo
los archivos faltantes en orden con la instancia/base correctas; no forzar el marcador de desarrollo.
El migrador productivo/compatibilidad/respaldos automatizados sigue siendo DEV-36.

## Seguridad y continuación

DEV-45 registra alcance, evidencias, hallazgos y fase de corrección. Antes de DTE real se requiere
FIS-01 v0.2: especificaciones oficiales verificadas, identidad del emisor, pruebas separadas,
referencias seguras a secretos, TLS/validación de destino, firma protegida, sanitización de errores,
consulta/reconciliación tras respuesta incierta e idempotencia por documento independiente de Venta.
No se inventan esos contratos fiscales en esta fase ni se certifica la aceptación por Hacienda.

Validación local del incremento: Release con Windows targeting, 0 advertencias y 0 errores;
`dotnet test GoDental.sln -c Release -p:EnableWindowsTargeting=true --no-build --no-restore`:
**347 aprobadas**, 0 fallidas/omitidas. Incluye validación, identidad congelada, permisos separados,
respuestas tardías/cambio de usuario y reintento de revisión con el mismo GUID. No ejecuta WPF
ni SQL Server en Linux. Se agregan suite SQL `--run-fiscal` y smoke de pantallas/formularios en
Windows; su resultado definitivo se registra después del CI sobre el código exacto.
