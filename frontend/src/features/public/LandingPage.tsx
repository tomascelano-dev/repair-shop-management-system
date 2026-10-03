import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { billingApi } from '../../api/endpoints'
import { publicPath, usePublicLocale } from '../../lib/publicLocale'
import { PublicLayout } from './PublicLayout'

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
    <PublicLayout title={en ? 'Repair shop management' : 'Gestión de talleres de reparación'}>
      <main className="mx-auto max-w-6xl px-4 pb-12">
        <section className="grid items-center gap-10 py-12 md:grid-cols-2 md:py-20">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wide text-brand-700">{en ? 'Built for repair shops' : 'Diseñado para talleres de reparación'}</p>
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
      </main>
    </PublicLayout>
  )
}
