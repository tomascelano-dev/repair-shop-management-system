import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { billingApi } from '../../api/endpoints'
import { publicPath, usePublicLocale } from '../../lib/publicLocale'
import { SEO_TITLES } from '../../lib/seo'
import { PublicLayout } from './PublicLayout'

/** What the product does, in the words shops search with ("órdenes de reparación", "stock de repuestos"...). */
const details: Record<'es' | 'en', [string, string][]> = {
  es: [
    ['Órdenes de reparación', 'Ingreso del equipo con checklist, fotos y firma del cliente; diagnóstico, presupuesto, reparación y control de calidad antes de entregar.'],
    ['Seguimiento online para el cliente', 'Cada orden tiene un enlace donde el cliente ve el estado, aprueba el presupuesto y califica la atención, sin llamar al taller.'],
    ['Avisos por WhatsApp y email', 'Plantillas de mensajes para avisarle al cliente que su equipo está listo o que tiene un presupuesto para aprobar.'],
    ['Stock de repuestos y compras', 'Reserva repuestos para cada orden, controla el stock por sucursal y registra las compras a tus proveedores.'],
    ['Punto de venta y caja', 'Vende accesorios con lector de código de barras, cobra en efectivo o con Mercado Pago y cierra la caja del día con su arqueo.'],
    ['Factura electrónica ARCA y garantías', 'Emite facturas con tu propio certificado y punto de venta (Argentina) e imprime el certificado de garantía de cada reparación.'],
  ],
  en: [
    ['Repair orders', 'Device intake with checklist, photos and customer signature; diagnosis, estimate, repair and quality check before delivery.'],
    ['Online tracking for customers', 'Every order has a link where the customer sees the status, approves the estimate and rates the service.'],
    ['WhatsApp and email notifications', 'Message templates to tell customers their device is ready or an estimate is waiting for their approval.'],
    ['Parts inventory and purchasing', 'Reserve parts for each order, track stock per branch and record purchases from your suppliers.'],
    ['Point of sale and cash register', 'Sell accessories with a barcode scanner, take cash or Mercado Pago payments and close the register every day.'],
    ['Electronic invoicing and warranties', 'Issue ARCA invoices with your own certificate (Argentina) and print the warranty certificate of each repair.'],
  ],
}

function faq(locale: 'es' | 'en', days: number): [string, string][] {
  return locale === 'en' ? [
    ['What is repair shop software?', 'A system that organizes the work of a repair shop: it records every device that comes in, its estimate and progress, keeps customers informed and controls parts, sales and the cash register.'],
    ['Do I need to install anything?', 'No. RepairShop runs in the browser on any computer, tablet or phone. Your data is backed up every day.'],
    ['How much does it cost?', `Monthly plans with unlimited users, paid in your local currency with Mercado Pago. You can try every module free for ${days} days, no card required.`],
    ['Can I cancel anytime?', 'Yes. You cancel from the Subscription page and keep using the service until the end of the paid month.'],
  ] : [
    ['¿Qué es un sistema para servicio técnico?', 'Es un programa que ordena el trabajo de un taller de reparación: registra cada equipo que entra, su presupuesto y su avance, mantiene informado al cliente y controla repuestos, ventas y caja.'],
    ['¿Sirve para talleres de celulares y de computadoras?', 'Sí. Está pensado para servicio técnico de celulares, tablets, computadoras, consolas y electrónica en general, con uno o varios técnicos y sucursales.'],
    ['¿Tengo que instalar algo?', 'No. RepairShop funciona en el navegador, desde la computadora, la tablet o el celular. Los datos se respaldan todos los días.'],
    ['¿Cuánto cuesta?', `Planes mensuales en pesos con usuarios ilimitados, cobrados por Mercado Pago con débito automático. Puedes probar todos los módulos ${days} días gratis, sin tarjeta.`],
    ['¿Puedo hacer factura electrónica?', 'Sí. En Argentina emites facturas electrónicas ante ARCA desde RepairShop, con tu propio certificado y punto de venta.'],
    ['¿Puedo cancelar cuando quiera?', 'Sí. Cancelas desde la sección Suscripción y sigues usando el sistema hasta el final del mes pagado. También tienes el botón de arrepentimiento.'],
  ]
}

export function LandingPage() {
  const locale = usePublicLocale()
  const en = locale === 'en'
  const config = useQuery({ queryKey: ['billing-config'], queryFn: billingApi.config, staleTime: 5 * 60_000 })
  const days = config.data?.trialDays ?? 14
  const features = en ? [
    ['Every repair, under control', 'Record the device, diagnosis, estimate and progress. Keep the entire repair history in one place.'],
    ['Keep customers informed', 'Share a tracking link, collect estimate approvals and send the notifications your shop needs.'],
    ['Inventory and payments together', 'Manage parts, sales and your cash register. Add purchasing, reports and branches as your business grows.'],
  ] : [
    ['Cada reparación, bajo control', 'Registra el equipo, diagnóstico, presupuesto y avance. Consulta todo el historial de la reparación en un solo lugar.'],
    ['Clientes informados', 'Comparte un enlace de seguimiento, recibe aprobaciones de presupuestos y envía los avisos que necesita tu taller.'],
    ['Inventario y cobros juntos', 'Gestiona repuestos, ventas y caja. Añade compras, informes y sucursales a medida que crece tu negocio.'],
  ]
  return (
    <PublicLayout title={en ? 'Repair shop management' : 'Gestión de talleres de reparación'} documentTitle={SEO_TITLES.home[locale]}>
      <main className="mx-auto max-w-6xl px-4 pb-12">
        <section className="grid items-center gap-10 py-12 md:grid-cols-2 md:py-20">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wide text-brand-700">{en ? 'Repair shop management software' : 'Sistema para servicio técnico'}</p>
            <h1 className="mt-4 text-4xl font-bold tracking-tight text-slate-900 sm:text-5xl">{en ? 'More repairs. Less paperwork.' : 'Más reparaciones. Menos papeleo.'}</h1>
            <p className="mt-5 text-lg leading-relaxed text-slate-600">{en ? 'Orders, estimates, inventory and customer updates, all in one place. Give your team a clear view of the workshop.' : 'Órdenes, presupuestos, inventario y avisos a clientes en un solo lugar. Dale a tu equipo una visión clara de todo el taller.'}</p>
            <div className="mt-7 flex flex-wrap gap-4">
              <Link to={publicPath(locale, 'signup')} className="rounded-lg bg-brand-600 px-5 py-3 font-semibold text-white hover:bg-brand-700">{en ? 'Start your free trial' : 'Empezar la prueba gratis'}</Link>
              <Link to={publicPath(locale, 'pricing')} className="rounded-lg border border-slate-300 px-5 py-3 font-semibold text-slate-700">{en ? 'See plans and pricing' : 'Ver planes y precios'}</Link>
            </div>
            <p className="mt-4 text-sm text-slate-600">{en ? `${days} days free. No card required. Unlimited users.` : `${days} días gratis. Sin tarjeta. Usuarios ilimitados.`}</p>
          </div>
          <div className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{en ? 'A clear repair workflow' : 'Un flujo de reparación claro'}</p>
            <ol className="mt-5 space-y-5">
              {(en ? ['Receive the device', 'Diagnose and quote', 'Repair and test', 'Deliver and collect payment'] : ['Recibir el equipo', 'Diagnosticar y presupuestar', 'Reparar y comprobar', 'Entregar y cobrar']).map((step, i) => (
                <li key={step} className="flex items-center gap-3 text-slate-800"><span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-brand-50 text-sm font-semibold text-brand-700">{i + 1}</span>{step}</li>
              ))}
            </ol>
          </div>
        </section>
        <section className="grid gap-5 md:grid-cols-3" aria-label={en ? 'Features' : 'Funciones'}>
          {features.map(([title, text]) => <article key={title} className="rounded-xl bg-white p-6 ring-1 ring-slate-200"><h2 className="font-semibold text-slate-900">{title}</h2><p className="mt-3 text-sm leading-relaxed text-slate-600">{text}</p></article>)}
        </section>
        <section className="mt-16">
          <h2 className="text-2xl font-bold tracking-tight text-slate-900">
            {en ? 'Management software for phone, computer and electronics repair shops' : 'Sistema de gestión para servicio técnico de celulares, computadoras y electrónica'}
          </h2>
          <p className="mt-3 max-w-3xl leading-relaxed text-slate-600">
            {en
              ? 'RepairShop is repair shop software for phones, tablets, computers, game consoles and appliances. It runs in the browser on a computer or a phone, with nothing to install.'
              : 'RepairShop es un software para servicio técnico pensado para talleres de reparación de celulares, tablets, computadoras, consolas y electrodomésticos. Funciona en el navegador, desde la computadora o el celular, sin instalar nada.'}
          </p>
          <div className="mt-6 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {details[locale].map(([title, text]) => (
              <article key={title}>
                <h3 className="font-semibold text-slate-900">{title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-slate-600">{text}</p>
              </article>
            ))}
          </div>
        </section>
        <section className="mt-16">
          <h2 className="text-2xl font-bold tracking-tight text-slate-900">{en ? 'Frequently asked questions' : 'Preguntas frecuentes'}</h2>
          <div className="mt-6 space-y-3">
            {faq(locale, days).map(([q, a]) => (
              <details key={q} className="rounded-xl bg-white p-5 ring-1 ring-slate-200">
                <summary className="cursor-pointer font-semibold text-slate-900">{q}</summary>
                <p className="mt-3 text-sm leading-relaxed text-slate-600">{a}</p>
              </details>
            ))}
          </div>
          <p className="mt-6 text-sm text-slate-600">
            <Link to={publicPath(locale, 'pricing')} className="font-semibold text-brand-700 underline">{en ? 'See plans and pricing' : 'Ver planes y precios'}</Link>
            {' · '}
            <Link to={publicPath(locale, 'signup')} className="font-semibold text-brand-700 underline">{en ? 'Start your free trial' : 'Empezar la prueba gratis'}</Link>
          </p>
        </section>
      </main>
    </PublicLayout>
  )
}
