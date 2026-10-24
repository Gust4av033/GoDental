# DEV-24 · Refinamiento del módulo Clientes

20/10/2026 · Rama `feature/desarrollo`. Incremento centrado en Clientes sobre `19b2792`, sin crear ramas ni incorporar `feature/mejora-ui`. Los commits de este bloque incluyen `clientes` en su nombre.

## Referencia y alcance

Se inspeccionó directamente `Imagenes/Captura de pantalla 2026-10-18 221111.png`: listado compacto, identidad del cliente, pestañas subrayadas, formularios en dos columnas y tablas ligeras. Se consultaron AGENTS, DM-01, DB-01, ARQ-01, UX-01 y DEV-13. Se adapta la composición XAML además de los estilos, como solicita el refinamiento del módulo; los estilos y geometrías nuevos quedan en `Themes/Customers.xaml`, con los colores dinámicos de los temas existentes.

- Listado: buscador flexible con Enter, filtros desplegables por tipo/estado, actualización, avatar de iniciales, nombre que abre la ficha directamente y pie con rango/página. Se mantiene la apertura por selección y botón para navegación con teclado.
- Ficha: volver al listado, actualización y edición en cabecera; iniciales, nombre, tipo, código y estado real. Ocho pestañas: información general, contactos, direcciones, datos fiscales, comercial, crédito, ventas y cuenta.
- Información general, fiscal y comercial: campos de consulta alineados en dos columnas. Los datos opcionales sin contenido muestran «Sin registrar»; los importes no consultados muestran «—», sin asumir cero.
- Contactos y direcciones: acción de alta sobre la tabla, edición separada, estado semántico y principal Sí/No. Teléfono y WhatsApp se conservan en una misma columna de contacto; direcciones largas siguen disponibles por tooltip y en su editor.
- Crédito y cuenta: condiciones y bloqueo del DTO, importes USD y tarjetas con saldo utilizado, vencido y disponible **devueltos por el servicio**. El disponible nominal sigue siendo informativo. Se conservan las consultas explícitas y la paginación independiente de ventas, cuentas y abonos.
- Formularios: ventana independiente para ficha, contacto, dirección y crédito. La ficha editable separa información general, fiscal y comercial/estado en pestañas. Campos flexibles de dos columnas, dirección multilínea y sección de motivo/confirmación; el pie existente mantiene Guardar/Cancelar. Acciones de 152×36 DIP; iconos de herramientas de 36×36 DIP.

No se añaden última visita, fecha de alta, foto almacenada, descuentos por categoría, reportes ni datos clínicos: no forman parte de los contratos actuales. No hay cambios de SQL ni reglas transaccionales nuevas.

## Conexiones y estados

Se reutilizan CustomerService y los comandos existentes de alta/edición de agregado, contactos, direcciones, crédito e historiales. El nuevo enlace de fila recibe el cliente concreto y verifica lectura/sesión; no depende del orden de actualización de SelectedItem. Se conservan las copias aisladas de edición, permisos independientes, motivo, confirmación, rowversion, manejo de conflictos/respuestas inciertas y recarga después de guardar.

Los historiales distinguen «sin consultar» de «sin registros». Una recarga de ficha limpia esa marca y los saldos anteriores. Al cambiar/cerrar sesión se elimina el detalle; las respuestas tardías no repueblan otra sesión. Se mantiene la barra de carga y el estado de error; el vacío de las tablas se oculta durante la carga.

## Validación

- Compilación Release local con `EnableWindowsTargeting=true`: cero errores y advertencias.
- 145 pruebas xUnit correctas. Se verifica la apertura directa de fila con permiso de lectura y la invalidación de estados consultados al recargar la ficha; siguen pasando los escenarios de permisos, edición aislada, concurrencia y respuesta perdida.
- Smoke ampliado: filtros compactos, capturas de las ocho pestañas en ambos temas, acciones uniformes y posición de la acción principal a 1366×768 y 1093×614 DIP. Se adapta la comprobación del selector de tipo a su nueva pestaña comercial.

Resultado del código `9bf3920`: [CI Windows correcto](https://github.com/Gust4av033/GoDental/actions/runs/39453462175), con compilación Release sin errores/advertencias, **145 xUnit**, **349 comprobaciones SQL** (incluidas 33 de Clientes) y **168 comprobaciones WPF**, sin errores de binding. Se generan las capturas de las ocho pestañas, listado, filtros y cuatro formularios en Light/Dark; disponibles en el artefacto de resultados de ese run.

La referencia original se inspeccionó directamente. Las capturas nuevas las genera el runner; no se presenta esta ejecución como inspección manual de esas imágenes ni como comprobación física del monitor del usuario. La revisión en su equipo con DPI 125/150 % sigue siendo distinta del renderizado automatizado. No se ejecutó F5 en su laptop ni se modificó su base de datos; las suites SQL usan bases aisladas en CI.
