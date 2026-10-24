# GoDental — VEN-01A

## Cierre de pendientes comerciales previos al desarrollo

**Versión 0.1 · Septiembre 2026**

VEN-01 v0.1 dejó identificadas varias decisiones comerciales para una revisión posterior. Este addendum registra expresamente esos pendientes y cierra la decisión que sí conviene fijar antes de iniciar el desarrollo: la precedencia entre lista de precio, tipo de cliente, promoción y descuento manual.

## 1. Relación con VEN-01 v0.1

Este documento complementa VEN-01 v0.1 y debe leerse junto con él. No reemplaza el módulo de Ventas/POS; únicamente resuelve y clasifica decisiones que VEN-01 dejó pendientes.

En lo referente a precedencia comercial, este addendum sustituye la condición de "propuesta inicial" de las secciones de reglas de mayoreo, prioridad de precios y combinación promoción + descuento manual.

## 2. Pendientes identificados en VEN-01 v0.1

| Pendiente | Estado |
|---|---|
| Prioridad definitiva entre lista de precio, promoción, cantidad, monto y precio especial | **Resuelto en este addendum** |
| Regla definitiva promoción + descuento manual | **Resuelto en este addendum** |
| Umbral/regla de venta bajo costo | Diferido a preproducción |
| Bloqueo exacto por mora | Diferido a preproducción |
| Ventas al contado a clientes morosos | Diferido a preproducción |
| Modelo definitivo de crédito a favor por devoluciones | Diferido a preproducción |
| Umbral de diferencia de caja que requiere Administrador | Diferido a preproducción |
| Proveedor/flujo de cobro con tarjeta | Diferido hasta definir proveedor |
| Formato final de ticket y factura | Diferido a UX/Fiscal |
| Atajos definitivos del POS | Diferido a validación de uso |
| Si las cotizaciones podrán reservar stock | Diferido a política comercial |

Los pendientes diferidos no bloquean el inicio del desarrollo base. Deben resolverse antes de poner en producción las funciones afectadas o cuando la operación real del depósito aporte la información necesaria.

## 3. Regla definitiva de precedencia de precios

La precedencia comercial adoptada para GoDental es:

1. **Precio base/lista:** constituye el precio inicial de referencia.
2. **Tipo de cliente:** determina la lista o condición comercial aplicable al cliente.
3. **Promoción:** se evalúa contra el precio resultante del cliente y sólo sustituye ese precio cuando produce un beneficio mayor.
4. **Descuento manual:** se aplica al final y queda sujeto a permisos, justificación y autorización según VEN-01/SEC-01.

**Regla resumida:** el sistema toma el mejor precio válido entre el precio determinado para el cliente y la promoción aplicable; después puede aplicar un descuento manual autorizado.

## 4. No acumulación por defecto

El tipo de cliente y una promoción no se acumulan automáticamente como descuentos sucesivos. Se comparan como alternativas comerciales y gana el precio más favorable para el cliente.

Una promoción podrá ser acumulable únicamente cuando la propia promoción lo declare expresamente mediante una regla/configuración equivalente a `PermiteAcumulacion = true`. El valor por defecto será `false`.

## 5. Ejemplos

- Lista USD 100, cliente mayorista USD 88 y promoción USD 90 → **USD 88**.
- Lista USD 100, cliente mayorista USD 88 y promoción USD 82 → **USD 82**.
- Si después se solicita descuento manual, se aplican las reglas de autorización ya definidas: 0%-7%, >7%-35% con justificación y >35% con autorización administrativa.

## 6. Trazabilidad requerida

La venta debe conservar el precio/lista base, condición o tipo de cliente aplicado, promoción aplicada si existió, precio antes y después del descuento manual y las autorizaciones correspondientes. El usuario debe poder conocer por qué se aplicó un precio sin exponer costos si no posee permiso.

## 7. Criterio de implementación

La regla debe existir en un único servicio/caso de uso de cálculo comercial en `Application`. WPF sólo presenta el resultado y su explicación. SQL Server vuelve a validar reglas críticas al confirmar la venta. POS, cotizaciones convertidas y demás rutas deben usar la misma política de cálculo.

## 8. Decisión de cierre previa al desarrollo

Con esta precedencia y la regla promoción + descuento manual ya definidas, VEN-01 queda suficientemente cerrado para iniciar el desarrollo base de GoDental sin improvisar reglas centrales de precio.

> **Decisión adoptada:** Lista base → condición/tipo de cliente → comparación con promoción → mejor precio válido → descuento manual autorizado. No existe acumulación automática entre beneficios; sólo se permite cuando una promoción lo declara expresamente.

## Continuación de posventa · DEV-38 (23/10/2026)

El usuario confirmó reducir primero deuda de la venta y reintegrar excedente en efectivo;
crédito/saldo a favor reutilizable conserva su estado diferido. Se adoptó carpeta central para fotos
y anulación completa también en ruta/entregado, con recuperación física y autorización administrativa.
Estas decisiones no modifican la precedencia comercial de este addendum.
