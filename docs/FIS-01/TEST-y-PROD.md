# Ambientes y checklist de activación de producción

**PREPARADO. TEST pendiente de habilitación/configuración; PRODUCCIÓN BLOQUEADA.**
F05 pp.7–14 y F08 anexo I §3.1, p.41 distinguen ambientes. `TEST`/`PROD` aquí son conceptos
GoDental, no códigos JSON oficiales. La traducción de `PRUEBAS`/`PRODUCCION` de la baseline
a contratos MH se definirá con T01/T03/T04, sin alterar scripts históricos.

## Aislamiento exigido

| Dimensión | TEST | PROD |
| --- | --- | --- |
| Perfil/autoridad | Emisor y tipos para pruebas verificables | Autorización formal vigente por tipo/evento |
| Credenciales | Referencias a material TEST/API TEST | Referencias exclusivas PROD/API PROD |
| Destinos | Sólo host/ruta TEST aprobados desde T01 | Sólo host/ruta PROD aprobados desde T01 |
| Datos y artefactos | Identidad/ambiente explícitos, ensayos identificados | Evidencia real y acceso fiscal restringido |
| Trabajos y secuencias | Aislados por ambiente/emisor y ámbito autorizado | Sin copiar colas/números de TEST |
| Representación | Advertencia de ensayo sin valor legal según F08 §11.3 | Sólo artefactos y resultado factual del ambiente PROD |
| Activación | Sólo tras contratos y controles de servidor | Preflight completo, auditoría y aprobación explícita |

Primer despliegue propuesto: instalaciones/configuraciones de servicio y stores separados
para reducir cruces; si comparten SQL/host, todas las FK, consultas y permisos deben preservar
la partición emisor/ambiente. Copiar una BD de producción para pruebas exige protección de
PII y no habilita material ni transmisión PROD en TEST. Nunca configurar selección automática
de ambiente a partir de la credencial “que funcione”.

La UI muestra ambiente y tipo habilitados; no decide destinos ni entrega un alias de secreto.
Un ComboBox, un registro SQL editado o una variable local no bastan para habilitar producción.
El servidor verifica preflight al reclamar cada trabajo; cambios de contrato, emisor, tipo,
certificado o autorización invalidan la aprobación que corresponda.

## Preflight de producción, todo pendiente salvo evidencia futura explícita

- [ ] Fuentes técnicas oficiales vigentes verificadas, hashes/versiones compatibles y trazabilidad
  de cada regla; no filas de contrato pendientes para el alcance activado.
- [ ] Autorización real del emisor y de cada tipo DTE/evento, con fechas/evidencia verificable;
  FE autorizado no habilita automáticamente CCFE u otros tipos.
- [ ] Emisor/dirección/actividad con catálogos correctos; establecimiento y POS MH mapeados
  explícitamente, sin inferirlos de almacén/caja.
- [ ] Certificado y credencial PROD instalados por mecanismo seguro, pertenencia/vigencia
  comprobadas, versión de firmador oficial compatible; TEST no puede acceder a ellos.
- [ ] Pruebas oficiales TEST del alcance y eventos requeridos concluidas; evidencias reales y
  autorización formal conservadas. Build/fixtures locales no satisfacen este punto.
- [ ] SEC-01/03 cerrados con pruebas reales de identidad/permisos SQL, custodia y logs;
  HTTPS, red/destinos y sesiones verificados. Demás controles de DEV-45 aplicables atendidos.
- [ ] Idempotencia, corte de proceso tras envío, resultado incierto, reconciliación y recuperación
  ensayados con procedimiento oficial, sin doble emisión.
- [ ] Invalidación y contingencia implementadas/ensayadas para los supuestos exigidos;
  responsabilidades operativas y plazos vigentes documentados.
- [ ] Backup SQL+artefactos+store protegido y **restauración** probados; responsables,
  retención y reaprovisionamiento de claves conforme a contrato/política.
- [ ] Permisos finales, monitoreo, expiraciones, auditoría, entrega/RG/consulta y respuesta a
  fallos comprobados. Reloj/fechas y disponibilidad del host adecuados al contrato.
- [ ] Versiones de aplicación/BD compatibles en estaciones y servidor, migraciones completas,
  procedimiento de actualización/desactivación y restauración aprobado.
- [ ] Aprobación explícita por operador autorizado, con evidencia de cada punto y huella de
  configuración/versiones. No se puede asignar a sí mismo aceptación de Hacienda.

Al faltar un punto: **PRODUCCIÓN BLOQUEADA**, motivo visible sin secretos. La desactivación
detiene trabajo nuevo pero conserva consulta, evidencias y tratamiento de trabajos inciertos;
no elimina registros ni transforma pendientes en rechazados/aceptados.

## Pruebas de aislamiento antes de cierre

Material TEST en perfil PROD y viceversa; alias de otro emisor/purpose; destino cambiado por
cliente; configuración ausente; tipo sin autorización; versión de schema incompatible; certificado
ausente/vencido; preflight anterior a cambio de credencial; restauración de TEST con trabajos
PROD; permiso revocado después de encolar. Todos deben impedir la acción sensible sin fuga.
