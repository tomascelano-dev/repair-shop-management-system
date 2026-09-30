# Probar los módulos premium

Abrí http://127.0.0.1:5182/premium con **admin@local / Admin12345**. Los seis módulos están habilitados localmente y guardan los cambios en PostgreSQL. No hay WhatsApp, bots ni servicios de IA externos.

## 1. Compras y precios

Hay dos proveedores ficticios con baterías, pantallas y placas de carga. El indicador de menor precio compara exclusivamente el mismo texto de compatibilidad, calidad y moneda. No certifica compatibilidad técnica.

Para importar, elegí **Importar lista**, indicá proveedor y moneda predeterminada, subí el archivo y revisá las filas. **Confirmar importación** guarda o actualiza ofertas identificadas por proveedor + código. No cambia existencias por sí solo.

- Excel: `.xlsx`, primera hoja, hasta 500 filas de productos y 30 columnas. Se conservan valores numéricos de precio.
- CSV UTF-8: encabezados `codigo;descripcion;compatibilidad;calidad;costo;moneda`. Se admiten campos entre comillas y separador punto y coma, coma o tabulación. Hay ejemplo descargable en el formulario y en `tests/fixtures/proveedor-ejemplo.csv`.
- PDF: hasta 50 páginas, con texto seleccionable y líneas que terminan en precio. Es una extracción para revisión; tablas complejas pueden necesitar corrección. No incluye OCR para escaneos. Los códigos provisionales PDF deben reemplazarse por los del proveedor antes de confirmar.
- Archivos: hasta 5 MB. Monedas ARS/USD, sin conversión automática.

**Aplicar costo** actualiza el costo de referencia de un repuesto elegido. **Recibir compra** registra cantidad, sucursal, lote, costo histórico y, opcionalmente, serie. Si el SKU existe, seleccioná el repuesto del catálogo; si no, completá SKU y nombre para crearlo. Una serie admite una sola unidad.

## 2. Stock avanzado

El stock anterior aparece como lote `APERTURA`, en Casa central. No se duplicaron las cantidades. La disponibilidad es stock físico menos reservas.

1. Desde un lote con unidades disponibles, elegí **Reservar** y una orden abierta de la misma sucursal y moneda.
2. Aprobá el presupuesto y avanzá la orden a reparación.
3. En Reservas, elegí **Consumir**: baja el stock y registra el costo histórico en la orden. **Liberar** devuelve disponibilidad sin consumir.
4. **Configurar mínimo** crea una alerta por repuesto y sucursal.
5. **Transferir** mueve unidades libres entre sucursales. Conserva lote, proveedor y costo. **Ajustar** exige motivo y no permite dejar disponibilidad negativa.

Antes de cancelar o entregar una orden hay que consumir o liberar sus reservas. No se permiten escrituras mediante los endpoints de inventario anteriores. El historial muestra los últimos 300 movimientos; los anteriores permanecen en la base.

## 3. Rentabilidad

Podés entrar desde el módulo o desde una orden → **Costos y rentabilidad**.

- Mano de obra: minutos / 60 × costo por hora. Varias entradas se suman y reemplazan el costo manual de mano de obra de la ficha.
- Comisiones y otros gastos: importe manual en la moneda de la orden.
- Repuestos: si hay consumos registrados, usa esos costos históricos; si no, usa el costo estimado del presupuesto. Hay que registrar todos los consumos para tener un costo completo.
- Garantías: costo acumulado menos recuperación del proveedor.
- Ingreso neto: presupuesto aprobado menos devoluciones de dinero. Se muestra por separado lo cobrado.
- Las órdenes abiertas son proyecciones. La sugerencia de precio apunta a un margen del 30 % sobre los costos cargados; no incluye impuestos ni gastos generales.

ARS y USD se consultan por separado. No hay una utilidad contable certificada ni liquidación fiscal. Los costos cargados son registros acumulativos; esta primera versión no implementa anulación de gastos.

## 4. Garantías y calidad

Abrí una garantía sobre una reparación **entregada**. Podés vincular un repuesto consumido, lo que recupera su lote y proveedor, o registrar un reclamo del trabajo sin repuesto.

La fecha de cobertura se calcula a partir de la entrega y los días de garantía del presupuesto. Los casos fuera de plazo quedan identificados y pueden registrarse igualmente. Una categoría común permite contar fallas repetidas.

En **Gestionar**, cargá seguimiento/referencia del proveedor, costo total acumulado, monto recuperado y resolución. El costo neto se refleja en Rentabilidad. El historial de la orden conserva cada actualización. Los controles de calidad para entregar siguen en el diagnóstico de la orden. Un caso de garantía no crea automáticamente otra orden ni descuenta repuestos por sí solo.

## 5. Reacondicionados y canjes

Hay un iPhone 12 ficticio en preparación. También podés **Ingresar equipo** con IMEI/serie único, titular, compra o canje, valor reconocido, grado y precio objetivo.

Sumá repuestos/mano de obra/gastos mediante **Sumar costo**. Desde **Gestionar equipo**, avanzá Recibido → En reparación → Listo para vender. Para quedar listo debe tener diagnóstico y seis controles verificados. Luego elegí Registrar venta e indicá importe y comprador.

El margen es venta menos adquisición y gastos. La venta cierra la modificación de costos y queda en el historial del IMEI. El canje registra el valor del equipo recibido; no implementa una operación comercial combinada con el equipo entregado. Los costos de reacondicionamiento se cargan manualmente y no consumen inventario automáticamente.

## 6. Empresas y sucursales

La empresa ficticia Estudio Norte tiene tres equipos. Elegila y probá **Recibir lote**: cada equipo seleccionado crea una orden real con referencia común, sucursal y fecha límite.

- Contratos: vigencia, abono mensual, órdenes incluidas, tarifa adicional, plazo en horas corridas y condiciones.
- Equipos: identificador único dentro de la empresa; no se puede recibir otra vez mientras tenga una orden abierta.
- Liquidaciones: al cerrar un mes, calcula abono + tarifa × órdenes entregadas sobre el cupo. La liquidación conserva la lista de órdenes y queda fija. Puede marcarse como cobrada una sola vez.
- Meses en horario argentino (UTC−3); no hay prorrateo. El módulo no suma automáticamente los importes de presupuestos a la liquidación ni genera facturas fiscales. Definí el alcance del abono para no cobrar dos veces el mismo trabajo.
- Sucursales: alta, asignación de órdenes, transferencias de stock y resultados calculados por local y moneda. Usuarios compartidos dentro del taller; todavía no hay permisos limitados a una sucursal individual.

## Alcance técnico

Persistencia real con migración EF Core, referencias y restricciones por taller. Operaciones premium transaccionales con idempotencia y serialización por cuenta; coordinación con las órdenes mediante bloqueo de fila. Los endpoints administrativos requieren rol Admin.

Se necesita una siguiente etapa para publicar el SaaS: planes y cobro de suscripciones, paginación, onboarding, permisos granulares por sucursal, despliegue, copias programadas y monitoreo. Esta entrega permite probar funcionalmente los seis módulos en la PC; no se publicó nada.
