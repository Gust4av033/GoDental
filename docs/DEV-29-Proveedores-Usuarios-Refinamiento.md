# DEV-29 · Refinamiento de Proveedores y revisión de UI

21/10/2026 · `feature/desarrollo`, sin crear ramas. Se consultan AGENTS, DM-01, DB-01 (cmp.Proveedores/contactos/productos), ARQ-01, UX-01, SEC-01, DEV-10 y DEV-12.

## Diseño y alcance antes de implementar

Referencia inspeccionada: `Imagenes/Captura de pantalla 2026-10-18 221124.png`, primeras tres pantallas (listado, ficha y productos por proveedor). Se adopta buscador flexible, filtros recogidos, identidad con iniciales, pestañas subrayadas, campos de consulta en dos columnas y tablas compactas. La separación lista/ficha y los tres editores ya existían; se refina su composición, sin agregar compras, importaciones, pagos, documentos o condiciones comerciales sin contrato.

- Listado: búsqueda con Enter, acceso directo por nombre a la ficha, filtro de inactivos desplegable, actualización y pie de paginación. Acciones de 152×36 DIP e iconos de 36×36.
- Ficha: volver, actualizar y editar en cabecera; información general, contactos y productos relacionados en pestañas. Vacíos opcionales explícitos, Sí/No legibles, estados activo/inactivo y costo con hasta seis decimales junto a la moneda original. El costo sigue restringido a gestión de proveedores; no se interpreta como precio de venta ni costo de inventario.
- Editores independientes: proveedor con secciones general/contacto y estado, contacto y referencia comercial. Campos flexibles, dirección/motivo multilínea y confirmación; el contenedor modal mantiene encabezado y acciones fijos. La referencia nueva conserva selección de producto del catálogo y edición aislada.
- Se conservan servicios, permisos, versiones, validaciones, auditoría y recuperación de resultados inciertos. No cambia SQL ni se requiere migración.

## Revisión de pantallas existentes

Clientes; productos/categorías/inventario/lotes/Kardex/movimientos/conteos; Caja; POS/cobro/cotizaciones; crédito/abonos; pedidos/entregas; ventas/comprobante; login, Mi cuenta y Ajustes ya tienen el refinamiento específico de DEV-24 a DEV-28. Se revisan sus vistas y diálogos: no se detecta otro módulo operativo completo con el patrón anterior.

Usuarios conserva el listado y acciones anteriores. Se actualiza su presentación con identidad, tabla compacta, badges legibles y acciones uniformes; alta con secciones de cuenta/contraseña y administración con pestañas de estado/restablecimiento. Se extiende el patrón de roles/seguridad de `221214.png`, sin inventar editor de permisos, bitácora o estaciones. Se conserva PasswordBox, limpieza de claves, motivos, permisos y protección de la propia cuenta. Inicio y el selector compartido de productos ya usan los recursos recientes.

Esta revisión cubre las interfaces implementadas; no implica completar módulos futuros del roadmap ni certificar el monitor físico del usuario.

## Validación

Compilación Release local de toda la solución con `EnableWindowsTargeting=true`, sin errores ni advertencias; **173 pruebas xUnit correctas**. El caso nuevo comprueba apertura directa sin selección anterior y denegación sin permiso de lectura; se conservan los casos de versión, copia aislada, precisión, conflicto y respuesta perdida.

El smoke WPF amplía capturas de listado/filtros y las tres pestañas de Proveedores en 1366×768 y 1093×614, ambos temas; editores de proveedor/contacto/referencia, referencia nueva con catálogo visible, pestaña de contacto/estado y lectura con costo restringido. Usuarios se captura en ambos tamaños/temas y su administración incluye el restablecimiento de contraseña. Se comprueban acciones uniformes/visibles y motivo flexible al reducir los diálogos a 780×540. Resultado nativo pendiente de CI Windows tras sincronizar el incremento.

No hay cambios SQL ni migraciones. Las pruebas SQL usan bases aisladas de CI; no se toca la base del usuario. Las referencias se inspeccionan directamente; las nuevas capturas WPF son automáticas y no constituyen revisión del monitor físico ni DPI real.
