# DEV-46 — Online interno: prospectos, fuentes y exclusiones

23/10/2026, El Salvador. Diseño anterior a SQL/contratos. Fuentes consultadas: DM-01 §15,
DB-01 §7.9, ARQ-01, ONL-01, SEC-01 y DEV-44/45. Continúa Fiscal interno sin habilitar Hacienda.

## Alcance del primer bloque

Registro manual de datos empresariales públicos y su fuente/fecha; consulta/edición versionada,
fuentes adicionales inmutables, coincidencias contra prospectos/clientes y exclusiones centrales.
El usuario confirmó alta manual y vincular cliente, sin alta desde Online.
No descubre sitios, abre enlaces, importa archivos ni envía comunicaciones. Registrar fuente pública
no acredita consentimiento. Campañas, proveedores, scoring, consentimiento y automatización quedan
para ONL-01 v0.2; no inventar esos contratos. Zonas/campañas/plantillas no se declaran completadas.

Conversión respeta DM-01: se vincula a un cliente activo mediante una **venta CONFIRMADA** suya,
con versión de venta/prospecto y confirmación humana. No crea clientes ni concede crédito: si falta,
se registra desde Clientes y se confirma la compra desde POS antes de vincular. No reescribe venta,
cliente, stock, caja o deuda. Conserva fuente, exclusiones y referencia a la venta que acreditó el vínculo;
posventa posterior no borra el hecho histórico. Creación directa de cliente desde el prospecto sigue
como posible complemento, sujeto al requisito de compra y deduplicación, no implementado por inferencia.

Permisos: lectura COMERCIAL.VER + CLIENTES.VER (comparación con clientes); mantenimiento y
estados COMERCIAL.PROSPECTAR adicionales; exclusiones COMERCIAL.EXCLUSIONES específico;
vinculación además VENTAS.VER. Admin recibe sólo el permiso nuevo si no existe, sin restaurar denegaciones.
Todas las escrituras requieren estación, GUID recuperable, transacción/bitácora y versión cuando modifican
prospecto. Estados internos NUEVO/REVISADO/DESCARTADO; no fabricar CONTACTADO/INTERESADO ni envíos.
CONVERTIDO sólo por venta; ficha convertida se conserva y cambios de cliente se hacen en Clientes.

## Coincidencias y exclusiones

Comparación acotada/paginada y revalidada al guardar: teléfono/WhatsApp cruzados, correo,
perfiles sociales, dominio empresarial propio y nombre+departamento+municipio. Dominio corresponde
al sitio empresarial explícito, nunca al sitio de procedencia (Google/red social/directorio compartido).
Clientes compara datos generales/contactos y nombre comercial/razón social con direcciones; no tiene
sitio web empresarial en el modelo actual, por lo que no se inventa deduplicación por dominio de cliente.
Se detectan formatos nacionales SV y +503 equivalentes; otros países requieren prefijo explícito.
Coincidencia bloquea alta/edición que duplicaría otra ficha: revisar/añadir fuente al prospecto existente
o usar Clientes. Sin fusiones ni cambios automáticos de identidades; excepciones/limpieza histórica quedan
pendientes. Comparación no es una garantía de unicidad global frente a altas externas al módulo.

NoContactar global del prospecto es monotónico en este bloque. Bloquear agrega sus contactos conocidos
a ListaNoContactar; editar un prospecto ya bloqueado incorpora sus nuevos identificadores. La lista
permite alta/reactivación restrictiva, nunca levantar exclusiones. Consultas calculan supresión por flag
o coincidencia central, incluso en filas antiguas y sin escribirlas. Los futuros conectores deberán consultar
la lista por canal y también el bloqueo del prospecto vinculado a un cliente, aunque sus datos cambien.
Comprar/vincular no equivale a consentimiento ni elimina opt-out. Exclusiones no bloquean ventas.
Un bloqueo por canal no se convierte silenciosamente en autorización para otro canal.

URLs son referencias documentales HTTPS, sin credenciales, query, fragmento, IP, puerto personalizado
ni host local; perfiles requieren dominio de su red. No se solicita ni descarga el destino y no se usa como
credencial. URL pública puede contener datos personales: capturar sólo datos empresariales necesarios,
sin tokens/secretos en URL, motivo u observaciones. Futuro transporte necesita los controles S1 de DEV-45.

## Migración 025 — extensión aditiva aprobada en este diseño

Sobre tablas existentes: com.Prospectos añade RowVersion, Distrito nvarchar100 NULL,
SitioWebPublico nvarchar1000 NULL, DominioWeb nvarchar253 NULL, FechaRegistro datetimeoffset0 NULL,
IdVentaConversion bigint NULL FK ven.Ventas. Fechas se registran sólo para altas nuevas, no rellenar
históricos ni reconstruir ventas de conversiones previas. com.ListaNoContactar añade RowVersion.
Una función com.fn_ClaveOnline normaliza identificadores para comparar formatos heredados sin
reescribirlos; no impone índices únicos nuevos que fallen con duplicados históricos.
No agrega tablas/vistas/procedimientos: 79/8/18; función de comparación adicional. Índice de dominio
filtrado para nuevas fichas, permiso COMERCIAL.EXCLUSIONES. Reaplicación conserva datos/versiones/permisos.
Ledger y bitácora existentes almacenan identidades/auditoría; no nuevas tablas de operaciones.
Escrituras Online usan applock transaccional compartido para comprobar duplicados/supresión antes de
insertar/modificar; no usar locks locales para integridad entre PCs.

## Validación y continuación

Release local con Windows targeting: 0 advertencias y 0 errores. Solución xUnit: **364 aprobadas**,
0 fallidas/omitidas; validación/URLs, identidades congeladas, datos heredados, permisos, sesión,
filtros/selección y reintento con el mismo GUID. Linux no ejecuta SQL/WPF. Resultado Windows
pendiente de registrar: SQL aislado (duplicados/formatos, fuentes, exclusiones, conversión, carreras,
permisos, rollback/recuperación y reaplicación) y smoke WPF Light/Dark/compacto.
Mantener controles y hallazgos de DEV-45 abiertos según fase; este módulo no certifica seguridad productiva.
Después del bloque interno, cerrar ONL-01 v0.2 antes de campañas/conectores y continuar trabajo independiente
por escalas/listas/promociones mientras siguen pendientes acceso y especificaciones DTE verificables.

## Actualización local pendiente

**Desde 021, incluida: 021 → 022 → 023 → 024 → 025.** Cada script es incremental; 025 no incluye
los anteriores. Respaldar BD y evidencias, cerrar/actualizar ambas estaciones, migrar la base correcta y
reiniciar sesión. No se aplicó SQL a la PC personal desde cloud. La herramienta de desarrollo recorre
migraciones en orden sólo sobre GoDental_Development con marcador reconocido; no forzar el marcador.

```powershell
git switch feature/desarrollo
git pull --ff-only origin feature/desarrollo
$servidor = Read-Host 'Instancia SQL guardada en Ajustes'
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --install-development --server "$servidor" .
dotnet run --project src/GoDental.Wpf -c Release
```

Para otra base, ejecutar sólo archivos faltantes en orden sobre su instancia/base, con respaldo previo.
El migrador productivo y restauración automatizada siguen pendientes en DEV-36.
