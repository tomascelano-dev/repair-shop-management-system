import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { billingApi } from '../../api/endpoints'
import type { Plan } from '../../api/types'
import { Alert, Loading, Select } from '../../components/ui'
import { billingCountry, COUNTRIES, detectCountry, planPrice } from '../../lib/billing'
import { countryName, publicPath, usePublicLocale } from '../../lib/publicLocale'
import { cn } from '../../lib/cn'
import { PublicLayout } from './PublicLayout'

const names = { es: { Basic: 'Básico', Standard: 'Estándar', Pro: 'Profesional' }, en: { Basic: 'Basic', Standard: 'Standard', Pro: 'Professional' } }
const modules: Record<string, [string, string]> = {
  purchasing: ['Compras y proveedores', 'Purchasing and suppliers'], reports: ['Informes detallados y Excel', 'Detailed reports and Excel'],
  transfers: ['Transferencias entre sucursales', 'Stock transfers between branches'], audit: ['Auditoría de cambios', 'Audit trail'], ai: ['Sugerencias de diagnóstico con IA', 'AI diagnosis suggestions'],
}

export function PricingPage({ initialCountry }: { initialCountry?: string } = {}) {
  const locale = usePublicLocale()
  const en = locale === 'en'
  const [country, setCountry] = useState(() => initialCountry ?? detectCountry())
  const plans = useQuery({ queryKey: ['plans', billingCountry(country)], queryFn: () => billingApi.plans(billingCountry(country)) })
  const features = en ? ['Orders, estimates and workshop board', 'Customer tracking and estimate approval', 'Point of sale, cash register and inventory', 'WhatsApp, email and SMS notifications', 'Unlimited users'] : ['Órdenes, presupuestos y tablero del taller', 'Seguimiento y aprobación de presupuestos para clientes', 'Punto de venta, caja e inventario', 'Avisos por WhatsApp, email y SMS', 'Usuarios ilimitados']
  return (
    <PublicLayout title={en ? 'Pricing' : 'Precios'}>
      <main className="mx-auto max-w-6xl px-4 pb-10">
        <section className="py-8 text-center">
          <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">{en ? 'A plan for every repair shop' : 'El sistema para tu servicio técnico'}</h1>
          <p className="mx-auto mt-3 max-w-2xl text-slate-600">{en ? 'Orders, estimates, inventory, payments and customer updates in one place. Unlimited users on every plan.' : 'Órdenes, presupuestos, inventario, caja y avisos a tus clientes en un solo lugar. Usuarios ilimitados en todos los planes.'}</p>
          <p className="mt-2 text-sm font-medium text-brand-700">{en ? `${plans.data?.trialDays ?? 14} days free. No card required.` : `${plans.data?.trialDays ?? 14} días gratis, sin tarjeta.`}</p>
          <label className="mt-5 inline-flex items-center gap-2 text-sm text-slate-600">
            {en ? 'Prices for' : 'Precios para'}
            <Select value={country} onChange={(e) => setCountry(e.target.value)} className="w-48">
              {COUNTRIES.map((c) => <option key={c.code} value={c.code}>{countryName(c.code, locale)}</option>)}
            </Select>
          </label>
        </section>
        {plans.isLoading ? <Loading /> : plans.data ? (
          <div className="grid gap-4 md:grid-cols-3">{plans.data.plans.map((p) => <PlanCard key={p.id} plan={p} />)}</div>
        ) : <Alert tone="amber">{en ? 'Prices could not be loaded. Please try again.' : 'No se pudieron cargar los precios. Inténtalo de nuevo.'}</Alert>}
        <section className="mt-10 rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <h2 className="font-semibold text-slate-900">{en ? 'Every plan includes' : 'Todos los planes incluyen'}</h2>
          <ul className="mt-3 grid gap-2 text-sm text-slate-700 sm:grid-cols-2">
            {features.map((f) => <li key={f} className="flex gap-2"><span aria-hidden="true" className="text-brand-600">✓</span>{f}</li>)}
            {country === 'AR' && <li>{en ? 'ARCA electronic invoicing (Argentina)' : 'Factura electrónica ARCA'}</li>}
          </ul>
          <p className="mt-4 text-xs text-slate-500">{country === 'AR'
            ? (en ? 'Final monthly prices in Argentine pesos, charged by Mercado Pago.' : 'Precios finales en pesos argentinos, cobrados por Mercado Pago cada mes.')
            : (en ? 'Monthly prices in US dollars. Paddle charges your subscription and adds applicable taxes.' : 'Precios mensuales en dólares estadounidenses. Paddle cobra la suscripción y añade los impuestos que correspondan.')}
            {' '}{en ? 'Cancel anytime. You can request a refund of your first payment within 14 days. See our ' : 'Cancela cuando quieras. Puedes solicitar el reembolso del primer pago dentro de los 14 días. Consulta la '}
            <Link to={publicPath(locale, 'refunds')} className="underline">{en ? 'refund policy' : 'política de reembolsos'}</Link>.
          </p>
        </section>
      </main>
    </PublicLayout>
  )
}

function PlanCard({ plan }: { plan: Plan }) {
  const locale = usePublicLocale()
  const en = locale === 'en'
  const highlight = plan.id === 'Standard'
  const branches = en ? (plan.maxBranches === 1 ? '1 branch' : `Up to ${plan.maxBranches} branches`) : (plan.maxBranches === 1 ? '1 sucursal' : `Hasta ${plan.maxBranches} sucursales`)
  return (
    <div className={cn('flex flex-col rounded-2xl bg-white p-6 shadow-sm ring-1', highlight ? 'ring-2 ring-brand-600' : 'ring-slate-200')}>
      <h2 className="text-lg font-semibold text-brand-700">{names[locale][plan.id]}</h2>
      <p className="mt-4"><span className="text-3xl font-bold text-slate-900">{planPrice(plan)}</span><span className="text-sm text-slate-500">{en ? ' /month' : ' /mes'}</span></p>
      <p className="mt-1 text-xs text-slate-500">{plan.currency}</p>
      <ul className="mt-4 flex-1 space-y-2 text-sm text-slate-700">{[branches, ...plan.modules.map((m) => modules[m]?.[en ? 1 : 0] ?? m)].map((f) => <li key={f} className="flex gap-2"><span aria-hidden="true" className="text-brand-600">✓</span>{f}</li>)}</ul>
      <Link to={publicPath(locale, 'signup')} className={cn('mt-6 rounded-lg px-4 py-2 text-center text-sm font-medium', highlight ? 'bg-brand-600 text-white hover:bg-brand-700' : 'bg-slate-100 text-slate-900 hover:bg-slate-200')}>
        {en ? 'Start your free trial' : 'Empezar la prueba gratis'}
      </Link>
    </div>
  )
}
