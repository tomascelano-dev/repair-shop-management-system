import type { GuideSection } from '../../lib/publicLocale'

/**
 * Long-tail landing pages, one per search intent ("sistema para servicio técnico de celulares", "orden de reparación
 * online"...). Spanish only (Argentina). Every claim here must match what the app does: check the code before adding
 * one, and keep plan limits in line with backend Domain/Billing/Plans.cs.
 */
export interface GuideBlock {
  heading: string
  paragraphs?: string[]
  items?: string[]
  /** Another guide this block points to. */
  related?: GuideSection
  /** Comparison table (first column = what is compared). */
  table?: { head: string[]; rows: string[][] }
  /** Free file to download (lead magnet), with a preview image. */
  download?: { href: string; label: string; preview: string; previewAlt: string; note?: string }
}

export interface Guide {
  section: GuideSection
  /** <title> and search result title (under ~60 characters). */
  title: string
  description: string
  /** Text of the links that point to this guide from other pages. */
  linkText: string
  eyebrow: string
  heading: string
  intro: string
  blocks: GuideBlock[]
  faq: [string, string][]
}

const phones: Guide = {
  section: 'guide-phones',
  title: 'Sistema para servicio técnico de celulares | RepairShop',
  description: 'Programa para taller de celulares: orden con IMEI y checklist, código de desbloqueo protegido, presupuesto que el cliente aprueba online, repuestos y garantía.',
  linkText: 'Sistema para servicio técnico de celulares',
  eyebrow: 'Taller de celulares',
  heading: 'Sistema para servicio técnico de celulares',
  intro: 'Un taller de celulares recibe muchos equipos parecidos, con poco tiempo para cada uno y clientes que preguntan todo el día cómo va su reparación. RepairShop es un sistema web pensado para ese ritmo: registras el equipo en un par de minutos, el cliente sigue el avance desde su teléfono y tú controlas repuestos, cobros y garantías sin planillas sueltas.',
  blocks: [
    {
      heading: 'Recepción del celular sin olvidos',
      paragraphs: ['Cada orden guarda marca, modelo, número de serie e IMEI. El IMEI se valida al cargarlo (15 dígitos con dígito verificador), así un error de tipeo no queda registrado, y después puedes encontrar el equipo buscando por IMEI. El checklist de recepción deja constancia de cómo llegó:'],
      items: [
        'Pantalla, cámaras, parlantes, micrófono y botones.',
        'Face ID o huella, porcentaje de batería y si la cuenta en la nube está bloqueada.',
        'Detalles estéticos (rayones, golpes, marcos doblados) y fotos adjuntas a la orden.',
        'Firma del cliente en pantalla, que se imprime en el comprobante junto con tus condiciones de recepción.',
      ],
    },
    {
      heading: 'El código de desbloqueo, protegido',
      paragraphs: ['Para probar un celular muchas veces necesitas el PIN, la contraseña o el patrón. RepairShop lo guarda cifrado dentro de la orden, valida que el patrón sea una secuencia de puntos posible y registra cada vez que alguien del equipo lo consulta. Se terminan los códigos anotados en papelitos o en el sobre del equipo.'],
    },
    {
      heading: 'Del diagnóstico a la entrega',
      paragraphs: [
        'Las órdenes pasan por estados claros: recibido, en diagnóstico, esperando repuesto, en reparación, en pruebas, listo para retirar y entregado. Asignas cada orden a un técnico con su prioridad y fecha prometida; las vencidas se destacan y el tablero del taller muestra todo el trabajo por columna.',
        'Antes de entregar, el control de calidad repasa encendido, pantalla, táctil, cámaras, audio, micrófono, botones, carga, conectividad, biometría y salud de la batería. Al retirar, el cliente firma la conformidad.',
      ],
      related: 'guide-orders',
    },
    {
      heading: 'El cliente sigue la reparación desde su teléfono',
      paragraphs: ['Cada orden tiene un enlace de seguimiento y un código QR, impreso en el comprobante y en la etiqueta del equipo. Desde ahí el cliente ve el estado, aprueba o rechaza el presupuesto, puede pagar el saldo con Mercado Pago si conectas tu cuenta y, al final, califica la atención. Para avisarle que el equipo está listo, RepairShop abre WhatsApp con el mensaje ya escrito a partir de tus plantillas.'],
    },
    {
      heading: 'Repuestos y garantía',
      paragraphs: ['Indicas con qué marcas y modelos es compatible cada módulo, batería o pin de carga y, al buscar el repuesto para una orden, ves solo los que sirven para ese equipo. Cuando el cliente aprueba el presupuesto, los repuestos quedan reservados. Al entregar defines los días de garantía e imprimes el certificado; si el equipo vuelve, abres un reingreso por garantía vinculado a la orden original.'],
      related: 'guide-stock',
    },
  ],
  faq: [
    ['¿Sirve para un taller chico con un solo técnico?', 'Sí. El plan Básico es para una sucursal con usuarios ilimitados. Puedes empezar usando solo las órdenes y el seguimiento, y sumar stock, caja y facturación cuando los necesites.'],
    ['¿Sirve también para tablets, computadoras y consolas?', 'Sí. La orden guarda marca, modelo y número de serie de cualquier equipo; el IMEI es opcional y solo lo completas cuando ingresa un celular.'],
    ['¿Funciona desde el celular?', 'Sí. RepairShop funciona en el navegador de la computadora, la tablet o el celular, sin instalar nada. En el punto de venta puedes escanear códigos de barras con la cámara.'],
    ['¿Cuánto cuesta?', 'Hay planes mensuales en pesos según la cantidad de sucursales, cobrados por Mercado Pago. Los valores están en la página de precios y puedes probar todo gratis antes de pagar, sin tarjeta.'],
  ],
}

const orders: Guide = {
  section: 'guide-orders',
  title: 'Orden de reparación online para servicio técnico | RepairShop',
  description: 'Crea órdenes de servicio técnico numeradas con checklist, firma y QR de seguimiento. El cliente aprueba el presupuesto online y lo avisas por WhatsApp.',
  linkText: 'Orden de servicio técnico online',
  eyebrow: 'Órdenes de reparación',
  heading: 'Orden de servicio técnico online: de la recepción a la entrega',
  intro: 'La orden de servicio técnico (también llamada orden de reparación u orden de trabajo) es el documento que respalda al taller y al cliente: qué equipo entró, en qué estado, qué falla se informó y qué se acordó. En papel se pierde, no se puede buscar y el cliente igual llama para preguntar. En RepairShop la orden es digital, se imprime cuando la necesitas y el cliente la sigue online.',
  blocks: [
    {
      heading: 'Qué guarda cada orden',
      items: [
        'Número de orden correlativo, para encontrarla al instante.',
        'Cliente con teléfono y email, y el equipo con marca, modelo, número de serie o IMEI.',
        'Falla informada, categoría, prioridad, técnico asignado y fecha prometida.',
        'Checklist de recepción, fotos, notas internas y firma del cliente.',
        'Presupuestos, repuestos usados, pagos y señas, y el historial de cambios de estado.',
      ],
    },
    {
      heading: 'Comprobante de ingreso y etiqueta con QR',
      paragraphs: ['Al crear la orden imprimes el comprobante de ingreso en PDF, con la firma del cliente y tus términos de recepción, y una etiqueta de 62 × 40 mm para pegar en el equipo. Los dos llevan un código QR que abre el seguimiento de esa orden, así el equipo y su historia nunca se separan.'],
    },
    {
      heading: 'Presupuesto que el cliente aprueba online',
      paragraphs: ['El presupuesto se arma por ítems, repuestos y mano de obra, con descuento, fecha de validez y días de garantía. Si cambia, generas una nueva versión sin perder la anterior. El cliente lo ve en el enlace de seguimiento o en el PDF y lo aprueba o rechaza desde su teléfono; la decisión queda registrada con su fecha. No hace falta que vuelva al local ni que mande una foto firmada.'],
    },
    {
      heading: 'Estados, tablero y avisos al cliente',
      paragraphs: [
        'La orden avanza por estados (recibido, en diagnóstico, esperando repuesto, en reparación, en pruebas, listo para retirar, entregado o cancelado) y cada cambio queda en el historial. El tablero muestra las órdenes abiertas por columna y el listado te deja encontrar cualquiera por número, cliente o equipo.',
        'Las plantillas de mensajes completan solas el nombre del cliente, el equipo, el número de orden, el saldo y el enlace de seguimiento. Con un clic se abre WhatsApp con el texto listo para enviar.',
      ],
    },
    {
      heading: 'Entrega, cobro y garantía',
      paragraphs: ['Registras señas y pagos en efectivo, transferencia, tarjeta o Mercado Pago, con su recibo, y el saldo queda siempre a la vista. Al entregar, el cliente firma la conformidad, imprimes el certificado de garantía y, si corresponde, emites la factura electrónica de la orden.'],
      related: 'guide-invoicing',
    },
  ],
  faq: [
    ['¿Qué diferencia hay entre orden de servicio y orden de reparación?', 'En la práctica son lo mismo: el registro de un equipo que entra al taller para diagnóstico o reparación. En RepairShop se llama orden de reparación e incluye la recepción, el presupuesto, el trabajo y la entrega.'],
    ['¿Puedo imprimir la orden y los presupuestos?', 'Sí. El comprobante de ingreso, el presupuesto, la etiqueta, los recibos de pago y el certificado de garantía se generan en PDF para imprimir o enviar al cliente.'],
    ['¿El cliente necesita crear una cuenta para ver su orden?', 'No. Entra al seguimiento con el enlace o el QR de su orden, sin usuario ni contraseña.'],
    ['¿Puedo traer mis clientes desde una planilla?', 'Sí. Importas clientes y artículos de stock desde un archivo de Excel o CSV, y puedes exportarlos cuando quieras.'],
  ],
}

const stock: Guide = {
  section: 'guide-stock',
  title: 'Control de stock de repuestos para taller | RepairShop',
  description: 'Controla el stock de repuestos de tu servicio técnico: compatibilidad por modelo, reservas por orden, stock mínimo, compras a proveedores y varias sucursales.',
  linkText: 'Control de stock de repuestos',
  eyebrow: 'Stock de repuestos',
  heading: 'Control de stock de repuestos para tu taller',
  intro: 'En un servicio técnico el stock no es solo lo que hay en el estante: es lo que ya está prometido a una reparación, lo que se usó sin cobrar y lo que falta pedir. Si lo llevas en una planilla, la planilla nunca coincide con el cajón. RepairShop une el stock con las órdenes de reparación y el punto de venta, así cada movimiento sale de algo que realmente pasó.',
  blocks: [
    {
      heading: 'Cada repuesto con sus datos',
      items: [
        'Código interno (SKU), código de barras, categoría y ubicación en el taller.',
        'Costo, precio de venta y días de garantía propios del artículo.',
        'Stock mínimo: lo que queda por debajo se marca en el inventario y se cuenta en el inicio.',
        'Artículos sin stock, como la mano de obra, y artículos que también vendes en el mostrador.',
      ],
    },
    {
      heading: 'Compatibilidad por marca y modelo',
      paragraphs: ['Un mismo módulo o batería puede servir para varios modelos. Cargas la compatibilidad una vez y, al agregar un repuesto a una orden, el buscador te muestra solo los compatibles con el equipo que estás reparando. Menos errores al pedir y al colocar.'],
    },
    {
      heading: 'Reservas y consumo por orden',
      paragraphs: ['Cuando el cliente aprueba el presupuesto, sus repuestos quedan reservados para esa orden: el disponible baja aunque la pieza siga en el cajón, y nadie la vende por mostrador. Al usarla, la descuentas de la reserva y eliges si se cobra al cliente o va incluida. Así sabes qué repuesto se usó en cada reparación y cuánto costó.'],
      related: 'guide-orders',
    },
    {
      heading: 'Historial de cada movimiento',
      paragraphs: ['Cada entrada y salida queda registrada con su motivo: compra, venta, consumo en una orden, devolución, corrección, ajuste manual o transferencia entre sucursales. Si un número no cierra, buscas el movimiento en lugar de recontar todo.'],
    },
    {
      heading: 'Compras, proveedores y sucursales',
      paragraphs: ['En los planes Estándar y Profesional registras proveedores y órdenes de compra; al recibir la mercadería, aunque llegue en partes, el stock se actualiza solo, y los informes muestran la valorización del inventario y se exportan a Excel. Cada sucursal tiene su propio stock y, en el plan Profesional, transfieres repuestos de una a otra con envío y recepción.'],
    },
    {
      heading: 'Venta de accesorios en el mostrador',
      paragraphs: ['Los artículos vendibles se cobran en el punto de venta con lector de código de barras o con la cámara del celular. La venta descuenta el stock y entra en la caja del día.'],
    },
  ],
  faq: [
    ['¿Puedo cargar mi stock desde Excel?', 'Sí. Importas los artículos desde un archivo de Excel o CSV (con una prueba previa que muestra qué se va a crear o actualizar) y exportas el inventario cuando quieras.'],
    ['¿Qué pasa si un repuesto se usa sin cobrarlo?', 'Lo registras como incluido: descuenta el stock y su costo queda en la orden, pero no se suma a lo que paga el cliente.'],
    ['¿Me avisa cuando un repuesto se está por terminar?', 'Cada artículo tiene un stock mínimo. Los que quedan por debajo aparecen marcados y puedes filtrar «Solo stock bajo» para armar el pedido al proveedor.'],
    ['¿Sirve para varias sucursales?', 'Sí. El plan Estándar incluye 2 sucursales y el Profesional hasta 10, cada una con su propio stock.'],
  ],
}

const invoicing: Guide = {
  section: 'guide-invoicing',
  title: 'Factura electrónica ARCA para servicio técnico | RepairShop',
  description: 'Emite factura A, B o C desde la orden de reparación o la venta, con CAE y QR de ARCA, tu propio certificado y punto de venta. Con notas de crédito.',
  linkText: 'Factura electrónica ARCA para servicio técnico',
  eyebrow: 'Factura electrónica',
  heading: 'Factura electrónica ARCA para servicio técnico',
  intro: 'Facturar una reparación suele significar salir del sistema del taller, entrar a la web de ARCA (ex AFIP) y volver a cargar cliente, importe y concepto. RepairShop emite la factura electrónica directamente desde la orden de reparación o desde la venta del mostrador, con tu CUIT, tu certificado y tu punto de venta, y la guarda junto al trabajo que la originó.',
  blocks: [
    {
      heading: 'Qué necesitas para empezar',
      items: [
        'Tu CUIT y tu condición frente al IVA: responsable inscripto, monotributo o exento.',
        'Un punto de venta habilitado para «Web Services» en ARCA.',
        'Un certificado digital (.pfx o .p12) asociado al servicio de factura electrónica (wsfe).',
      ],
      paragraphs: ['Lo cargas en Configuración › Integraciones. Conviene probar primero en el entorno de homologación de ARCA, donde los comprobantes no tienen validez fiscal, y pasar a producción cuando todo esté en orden.'],
    },
    {
      heading: 'La letra correcta sin pensarlo',
      paragraphs: ['RepairShop elige el tipo de comprobante según tu condición y la del cliente. Si eres responsable inscripto, emites factura A al cliente responsable inscripto con CUIT y factura B al resto. Si eres monotributista o exento, emites factura C. Para una factura A el sistema exige el CUIT del cliente antes de enviarla.'],
    },
    {
      heading: 'Desde la orden o desde el mostrador',
      paragraphs: ['Al entregar un equipo facturas la orden de reparación, con sus repuestos y su mano de obra. Las ventas de accesorios del punto de venta se facturan desde la venta. Cada comprobante queda vinculado a su orden o venta, y en Comprobantes fiscales ves todo lo emitido por fecha.'],
      related: 'guide-orders',
    },
    {
      heading: 'CAE, código QR y PDF',
      paragraphs: ['Cuando ARCA autoriza el comprobante, RepairShop guarda el CAE con su vencimiento y genera el PDF con el código QR de ARCA, listo para imprimir o enviar al cliente. Si ARCA lo rechaza, ves el motivo del rechazo para corregir el dato.'],
    },
    {
      heading: 'Notas de crédito',
      paragraphs: ['Si tienes que anular una factura autorizada, emites la nota de crédito de la misma letra (A, B o C) asociada a la factura original, sin volver a cargar los datos.'],
    },
    {
      heading: 'Lo que conviene saber',
      paragraphs: ['La facturación electrónica funciona en pesos argentinos. Cada taller emite con su propio certificado y punto de venta y es responsable de sus comprobantes: RepairShop los envía a ARCA en tu nombre, no en el de un tercero.'],
    },
  ],
  faq: [
    ['¿La factura electrónica está en todos los planes?', 'Sí. La facturación con ARCA está incluida en todos los planes, también en el Básico.'],
    ['¿Sirve si soy monotributista?', 'Sí. Como monotributista emites facturas C y, si hace falta anular una, su nota de crédito.'],
    ['¿Puedo probar sin emitir facturas reales?', 'Sí. En el entorno de homologación de ARCA los comprobantes son de prueba y no tienen validez fiscal. Cuando estés conforme, cambias a producción.'],
    ['¿Tengo que cargar las facturas también en la web de ARCA?', 'No. RepairShop las envía a ARCA por el servicio web de factura electrónica y recibe el CAE; quedan registradas en ARCA como cualquier comprobante electrónico.'],
  ],
}

const template: Guide = {
  section: 'guide-template',
  title: 'Plantilla de orden de servicio técnico gratis | RepairShop',
  description: 'Descarga gratis una plantilla de orden de servicio técnico en PDF para imprimir: cliente, equipo, checklist de ingreso, presupuesto, firmas y talón de retiro.',
  linkText: 'Plantilla de orden de servicio técnico (PDF gratis)',
  eyebrow: 'Descarga gratis',
  heading: 'Plantilla de orden de servicio técnico gratis para imprimir',
  intro: 'Si todavía trabajas en papel, una buena orden de servicio técnico te evita discusiones: deja por escrito qué equipo entró, en qué estado, qué falla contó el cliente y qué presupuesto aceptó. Esta plantilla está pensada para talleres de celulares, computadoras y electrónica. Es una hoja A4 en PDF, gratis y sin registrarte.',
  blocks: [
    {
      heading: 'Descarga la plantilla',
      download: {
        href: '/plantillas/orden-de-servicio-tecnico.pdf',
        label: 'Descargar la plantilla en PDF',
        preview: '/plantillas/orden-de-servicio-tecnico.png',
        previewAlt: 'Vista previa de la plantilla de orden de servicio técnico: datos del cliente y del equipo, checklist de ingreso, presupuesto, firmas y talón para el cliente',
        note: 'Hoja A4, lista para imprimir. Puedes escribir a mano o completarla en la computadora antes de imprimir.',
      },
    },
    {
      heading: 'Qué incluye',
      items: [
        'Espacio para el logo y los datos de tu taller, número de orden, fecha de ingreso y fecha prometida.',
        'Datos del cliente: nombre, DNI o CUIT, teléfono, WhatsApp y email.',
        'Datos del equipo: tipo, marca, modelo, color, IMEI o número de serie y accesorios entregados.',
        'Checklist de estado al ingreso: encendido, pantalla, cámaras, audio, botones, carga, señal, biometría, humedad y daños estéticos.',
        'Falla informada, diagnóstico y presupuesto por ítems con seña, saldo, validez y días de garantía.',
        'Condiciones de recepción, firmas de ingreso y retiro conforme, y un talón recortable que el cliente presenta para retirar.',
      ],
    },
    {
      heading: 'Cómo completarla bien',
      items: [
        'Numera las órdenes de forma correlativa y anota el mismo número en el talón y en una etiqueta pegada al equipo.',
        'Revisa el equipo delante del cliente y marca el checklist con él: es tu respaldo si después aparece un daño.',
        'Copia la falla con las palabras del cliente; el diagnóstico técnico va aparte.',
        'No anotes el PIN, la contraseña ni el patrón en la hoja: pídelos aparte y guárdalos de forma segura.',
        'Completa los días de validez del presupuesto, la garantía y el plazo para retirar, y revisa las condiciones con tu asesor según tu negocio.',
      ],
    },
    {
      heading: 'Cuando el papel ya no alcanza',
      paragraphs: [
        'Con muchas órdenes por semana, el papel tiene límites: no se puede buscar, se pierde y el cliente igual llama para preguntar cómo va su equipo. En RepairShop la misma orden es digital: lleva firma en pantalla, fotos y un código QR con el que el cliente sigue la reparación y aprueba el presupuesto desde su celular, y el comprobante se imprime en PDF con tus condiciones.',
      ],
      related: 'guide-orders',
    },
  ],
  faq: [
    ['¿La plantilla es gratis?', 'Sí. Puedes descargarla, imprimirla y usarla en tu taller sin registrarte ni pagar nada.'],
    ['¿Puedo poner el logo de mi taller?', 'Sí. Arriba a la izquierda hay un recuadro para el logo y los datos del taller: puedes pegar un sello, escribirlos o agregarlos con un editor de PDF antes de imprimir.'],
    ['¿Cuántas copias tengo que imprimir?', 'Una hoja por equipo alcanza: el taller se queda con la hoja y el cliente se lleva el talón recortable para retirar. Si prefieres que el cliente tenga la orden completa, imprime dos.'],
    ['¿Sirve para computadoras y consolas?', 'Sí. Los campos de equipo e IMEI o número de serie sirven para celulares, tablets, computadoras, consolas y electrónica en general.'],
  ],
}

const vsExcel: Guide = {
  section: 'guide-vs-excel',
  title: 'Sistema para servicio técnico vs Excel o cuaderno | RepairShop',
  description: 'Cuaderno, Excel o sistema para tu servicio técnico: qué resuelve cada uno, cuándo alcanza con una planilla y cuándo conviene pasar a un sistema.',
  linkText: 'Sistema, Excel o cuaderno: cuál conviene',
  eyebrow: 'Comparativa',
  heading: 'Cuaderno, Excel o sistema para servicio técnico: cuál conviene',
  intro: 'Casi todos los talleres empiezan con un cuaderno o una planilla de Excel, y está bien: son gratis y no hay que aprender nada. El problema aparece cuando crecen las órdenes, los técnicos y los repuestos. Esta comparación te ayuda a decidir con honestidad si te conviene seguir como estás o pasar a un sistema.',
  blocks: [
    {
      heading: 'Cuándo alcanza con un cuaderno o un Excel',
      paragraphs: ['Si reparas pocos equipos por semana, trabajas solo, casi no tienes stock de repuestos y tus clientes no te llaman para preguntar por su equipo, un cuaderno ordenado o una planilla bien armada pueden ser suficientes. En ese caso, una buena plantilla de orden en papel resuelve lo más importante: tener constancia de cómo entró cada equipo.'],
      related: 'guide-template',
    },
    {
      heading: 'Dónde empieza a complicarse',
      items: [
        'Buscar una orden vieja cuando el cliente vuelve por la garantía.',
        'Responder todo el día «¿cómo va mi equipo?» por teléfono y WhatsApp.',
        'Saber qué repuesto ya está prometido a una reparación y cuál se puede vender.',
        'Cobrar señas y saldos sin perder la cuenta de quién debe qué.',
        'Que dos personas editen la misma planilla y se pisen los datos.',
      ],
    },
    {
      heading: 'Comparación punto por punto',
      table: {
        head: ['', 'Cuaderno', 'Excel', 'RepairShop'],
        rows: [
          ['Buscar una orden', 'Hojear páginas', 'Filtros, si todo se cargó igual', 'Por número, cliente, equipo o IMEI'],
          ['Constancia del estado del equipo', 'Lo que se anote', 'Una celda de texto', 'Checklist, fotos y firma del cliente'],
          ['El cliente consulta cómo va', 'Llama al taller', 'Llama al taller', 'Enlace y código QR de seguimiento'],
          ['Aprobación del presupuesto', 'En persona o por teléfono', 'Por teléfono o WhatsApp', 'Online, con fecha registrada'],
          ['Repuestos para cada orden', 'No se controla', 'Fórmulas hechas a mano', 'Reservados al aprobar el presupuesto'],
          ['Varias personas a la vez', 'Un solo cuaderno', 'Riesgo de pisar cambios', 'Usuarios ilimitados con permisos por rol'],
          ['Caja y factura electrónica', 'Por separado', 'Por separado', 'Incluidas'],
          ['Costo', 'Casi nulo', 'Gratis o la licencia de Office', 'Plan mensual, con prueba gratis'],
        ],
      },
    },
    {
      heading: 'Cómo pasar de Excel a RepairShop sin empezar de cero',
      paragraphs: [
        'Importas tus clientes y tus artículos de stock desde un archivo de Excel o CSV, con una prueba previa que muestra qué se va a crear. Puedes empezar usando solo las órdenes y el seguimiento, y sumar stock, caja y facturación cuando estés cómodo. Y si algún día quieres llevarte los datos, los exportas a Excel.',
      ],
      related: 'guide-orders',
    },
  ],
  faq: [
    ['¿Puedo seguir usando Excel para algunas cosas?', 'Sí. Exportas clientes, inventario e informes a Excel cuando quieras, por ejemplo para tu contador.'],
    ['¿Qué pasa con mis datos si dejo de pagar?', 'La cuenta queda en modo consulta: sigues viendo tus órdenes, clientes y stock, pero no puedes cargar datos nuevos hasta elegir un plan.'],
    ['¿Cuánto tarda empezar?', 'Unos minutos: creas la cuenta, cargas el nombre del taller y ya puedes registrar la primera orden. Todo funciona en el navegador, sin instalar nada.'],
    ['¿Es más caro que un cuaderno?', 'Sí, un cuaderno es casi gratis. La pregunta es cuánto tiempo pierdes buscando órdenes y respondiendo consultas: puedes probar todos los módulos gratis antes de decidir.'],
  ],
}

export const GUIDES: Record<GuideSection, Guide> = {
  'guide-phones': phones,
  'guide-orders': orders,
  'guide-template': template,
  'guide-stock': stock,
  'guide-invoicing': invoicing,
  'guide-vs-excel': vsExcel,
}
