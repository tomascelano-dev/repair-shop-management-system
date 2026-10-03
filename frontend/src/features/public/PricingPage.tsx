import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { billingApi } from '../../api/endpoints'
import type { Plan } from '../../api/types'
import { Loading, Select } from '../../components/ui'
import { billingCountry, COUNTRIES, CORE_FEATURES, detectCountry, PLAN_TAGLINE, planFeatures, planPrice } from '../../lib/billing'
import { cn } from '../../lib/cn'

export function PublicHeader() {
  return (
    <header className="mx-auto flex max-w-6xl items-center justify-between px-4 py-5">
      <Link to="/precios" className="flex items-center gap-2">
        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-600 text-sm font-bold text-white">RS</span>
        <span className="text-lg font-semibold text-slate-900">RepairShop</span>
      </Link>
      <nav className="flex items-center gap-4 text-sm">
        <Link to="/login" className="text-slate-600 hover:text-slate-900">
          Ingresar
        </Link>
        <Link to="/registro" className="rounded-lg bg-brand-600 px-3 py-2 font-medium text-white hover:bg-brand-700">
          Probar gratis
        </Link>
      </nav>
    </header>
  )
}

export function PublicFooter() {
  const links = [
    { to: '/precios', label: 'Precios' },
    { to: '/terminos', label: 'Términos' },
    { to: '/privacidad', label: 'Privacidad' },
    { to: '/reembolsos', label: 'Reembolsos' },
  ]
  return (
    <footer className="mx-auto max-w-6xl px-4 py-8 text-center text-xs text-slate-500">
      {links.map((l, i) => (
        <span key={l.to}>
          {i > 0 && ' · '}
          <Link to={l.to} className="hover:underline">
            {l.label}
          </Link>
        </span>
      ))}
    </footer>
  )
}

/** `initialCountry` fixes the currency when the page is pre-rendered to static HTML (there is no browser language there). */
export function PricingPage({ initialCountry }: { initialCountry?: string } = {}) {
  const [country, setCountry] = useState(() => initialCountry ?? detectCountry())
  const plans = useQuery({ queryKey: ['plans', billingCountry(country)], queryFn: () => billingApi.plans(billingCountry(country)) })

  return (
    <div className="min-h-full bg-slate-50">
      <PublicHeader />
      <main className="mx-auto max-w-6xl px-4 pb-10">
        <section className="py-8 text-center">
          <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">El sistema para tu servicio técnico</h1>
          <p className="mx-auto mt-3 max-w-2xl text-slate-600">
            Órdenes, presupuestos, stock, caja y avisos a tus clientes en un solo lugar. Usuarios ilimitados en todos los planes.
          </p>
          <p className="mt-2 text-sm font-medium text-brand-700">{plans.data?.trialDays ?? 14} días gratis, sin tarjeta.</p>
          <label className="mt-5 inline-flex items-center gap-2 text-sm text-slate-600">
            Precios para
            <Select value={country} onChange={(e) => setCountry(e.target.value)} className="w-48">
              {COUNTRIES.map((c) => (
                <option key={c.code} value={c.code}>
                  {c.name}
                </option>
              ))}
            </Select>
          </label>
        </section>

        {plans.isLoading ? (
          <Loading />
        ) : plans.data ? (
          <div className="grid gap-4 md:grid-cols-3">
            {plans.data.plans.map((p) => (
              <PlanCard key={p.id} plan={p} highlight={p.id === 'Standard'} />
            ))}
          </div>
        ) : null}

        <section className="mt-10 rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <h2 className="font-semibold text-slate-900">Todos los planes incluyen</h2>
          <ul className="mt-3 grid gap-2 text-sm text-slate-700 sm:grid-cols-2">
            {CORE_FEATURES.map((f) => (
              <li key={f} className="flex gap-2">
                <span aria-hidden="true" className="text-brand-600">✓</span>
                {f}
              </li>
            ))}
            {country === 'AR' ? (
              <li className="flex gap-2">
                <span aria-hidden="true" className="text-brand-600">✓</span>
                Factura electrónica ARCA
              </li>
            ) : null}
          </ul>
          <p className="mt-4 text-xs text-slate-500">
            {country === 'AR'
              ? 'Precios finales en pesos, cobrados por Mercado Pago cada mes.'
              : 'Precios en dólares por mes. Paddle cobra el abono y suma los impuestos que correspondan en tu país.'}{' '}
            Cancelás cuando quieras, y si no te sirve te devolvemos el primer pago dentro de los 14 días (
            <Link to="/reembolsos" className="underline">
              política de reembolsos
            </Link>
            ).
          </p>
        </section>
      </main>
      <PublicFooter />
    </div>
  )
}

function PlanCard({ plan, highlight }: { plan: Plan; highlight: boolean }) {
  return (
    <div className={cn('flex flex-col rounded-2xl bg-white p-6 shadow-sm ring-1', highlight ? 'ring-2 ring-brand-600' : 'ring-slate-200')}>
      <p className="text-sm font-semibold text-brand-700">{plan.name}</p>
      <p className="mt-1 text-sm text-slate-500">{PLAN_TAGLINE[plan.id]}</p>
      <p className="mt-4">
        <span className="text-3xl font-bold text-slate-900">{planPrice(plan)}</span>
        <span className="text-sm text-slate-500"> /mes</span>
      </p>
      <ul className="mt-4 flex-1 space-y-2 text-sm text-slate-700">
        {planFeatures(plan).map((f) => (
          <li key={f} className="flex gap-2">
            <span aria-hidden="true" className="text-brand-600">✓</span>
            {f}
          </li>
        ))}
      </ul>
      <Link
        to="/registro"
        className={cn(
          'mt-6 rounded-lg px-4 py-2 text-center text-sm font-medium',
          highlight ? 'bg-brand-600 text-white hover:bg-brand-700' : 'bg-slate-100 text-slate-900 hover:bg-slate-200'
        )}
      >
        Empezar la prueba gratis
      </Link>
    </div>
  )
}
