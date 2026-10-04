import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { billingApi } from '../../api/endpoints'
import type { Plan } from '../../api/types'
import { Loading, Select } from '../../components/ui'
import { billingCountry, coreFeatures, countryOptions, detectCountry, planFeatures, planName, planPrice, planTagline } from '../../lib/billing'
import { cn } from '../../lib/cn'
import { PUBLIC_ROUTES, useLang, type Lang } from './lang'
import { PublicLayout } from './PublicLayout'

export { PublicFooter, PublicHeader } from './PublicLayout'

const TEXT = {
  es: {
    title: 'Precios · RepairShop',
    heading: 'El sistema para tu servicio técnico',
    lead: 'Órdenes, presupuestos, stock, caja y avisos a tus clientes en un solo lugar. Usuarios ilimitados en todos los planes.',
    trial: (days: number) => `${days} días gratis, sin tarjeta.`,
    pricesFor: 'Precios para',
    includes: 'Todos los planes incluyen',
    arca: 'Factura electrónica ARCA',
    perMonth: '/mes',
    start: 'Empezar la prueba gratis',
    noteAr: 'Precios finales en pesos, cobrados por Mercado Pago cada mes.',
    noteUsd: 'Precios en dólares por mes. Paddle cobra el abono y suma los impuestos que correspondan en tu país.',
    cancel: 'Cancela cuando quieras, y si no te sirve te devolvemos el primer pago dentro de los 14 días',
    refundPolicy: 'política de reembolsos',
  },
  en: {
    title: 'Pricing · RepairShop',
    heading: 'Software for your repair shop',
    lead: 'Repair orders, quotes, stock, cash register and customer notifications in one place. Unlimited users on every plan.',
    trial: (days: number) => `${days}-day free trial, no credit card.`,
    pricesFor: 'Prices for',
    includes: 'Every plan includes',
    arca: 'ARCA electronic invoicing',
    perMonth: '/month',
    start: 'Start the free trial',
    noteAr: 'Final prices in Argentine pesos, charged monthly through Mercado Pago.',
    noteUsd: 'Prices in US dollars per month. Paddle charges the subscription and adds the taxes that apply in your country.',
    cancel: 'Cancel anytime, and if it is not for you we refund your first payment within 14 days',
    refundPolicy: 'refund policy',
  },
}

/** `initialCountry` fixes the currency when the page is pre-rendered to static HTML (there is no browser language there). */
export function PricingPage({ initialCountry }: { initialCountry?: string } = {}) {
  const lang = useLang()
  const t = TEXT[lang]
  return (
    <PublicLayout title={t.title}>
      <main className="mx-auto max-w-6xl px-4 pb-10">
        <section className="py-8 text-center">
          <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">{t.heading}</h1>
          <p className="mx-auto mt-3 max-w-2xl text-slate-600">{t.lead}</p>
        </section>
        <PricingSection lang={lang} initialCountry={initialCountry} />
      </main>
    </PublicLayout>
  )
}

/** Country picker, plan cards and what every plan includes (pricing page and landing page). */
export function PricingSection({ lang, initialCountry }: { lang: Lang; initialCountry?: string }) {
  const t = TEXT[lang]
  const [country, setCountry] = useState(() => initialCountry ?? detectCountry())
  const plans = useQuery({ queryKey: ['plans', billingCountry(country)], queryFn: () => billingApi.plans(billingCountry(country)) })
  const routes = PUBLIC_ROUTES[lang]

  return (
    <>
      <div className="pb-6 text-center">
        <p className="text-sm font-medium text-brand-700">{t.trial(plans.data?.trialDays ?? 14)}</p>
        <label className="mt-4 inline-flex items-center gap-2 text-sm text-slate-600">
          {t.pricesFor}
          <Select value={country} onChange={(e) => setCountry(e.target.value)} className="w-48">
            {countryOptions(lang).map((c) => (
              <option key={c.code} value={c.code}>
                {c.name}
              </option>
            ))}
          </Select>
        </label>
      </div>

      {plans.isLoading ? (
        <Loading />
      ) : plans.data ? (
        <div className="grid gap-4 md:grid-cols-3">
          {plans.data.plans.map((p) => (
            <PlanCard key={p.id} plan={p} lang={lang} highlight={p.id === 'Standard'} />
          ))}
        </div>
      ) : null}

      <section className="mt-10 rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
        <h2 className="font-semibold text-slate-900">{t.includes}</h2>
        <ul className="mt-3 grid gap-2 text-sm text-slate-700 sm:grid-cols-2">
          {coreFeatures(lang).map((f) => (
            <li key={f} className="flex gap-2">
              <span aria-hidden="true" className="text-brand-600">✓</span>
              {f}
            </li>
          ))}
          {country === 'AR' ? (
            <li className="flex gap-2">
              <span aria-hidden="true" className="text-brand-600">✓</span>
              {t.arca}
            </li>
          ) : null}
        </ul>
        <p className="mt-4 text-xs text-slate-500">
          {country === 'AR' ? t.noteAr : t.noteUsd} {t.cancel} (
          <Link to={routes.refunds} className="underline">
            {t.refundPolicy}
          </Link>
          ).
        </p>
      </section>
    </>
  )
}

function PlanCard({ plan, lang, highlight }: { plan: Plan; lang: Lang; highlight: boolean }) {
  const t = TEXT[lang]
  return (
    <div className={cn('flex flex-col rounded-2xl bg-white p-6 shadow-sm ring-1', highlight ? 'ring-2 ring-brand-600' : 'ring-slate-200')}>
      <p className="text-sm font-semibold text-brand-700">{planName(plan, lang)}</p>
      <p className="mt-1 text-sm text-slate-500">{planTagline(plan, lang)}</p>
      <p className="mt-4">
        <span className="text-3xl font-bold text-slate-900">{planPrice(plan)}</span>
        <span className="text-sm text-slate-500"> {t.perMonth}</span>
      </p>
      <ul className="mt-4 flex-1 space-y-2 text-sm text-slate-700">
        {planFeatures(plan, lang).map((f) => (
          <li key={f} className="flex gap-2">
            <span aria-hidden="true" className="text-brand-600">✓</span>
            {f}
          </li>
        ))}
      </ul>
      <Link
        to={PUBLIC_ROUTES[lang].signup}
        className={cn(
          'mt-6 rounded-lg px-4 py-2 text-center text-sm font-medium',
          highlight ? 'bg-brand-600 text-white hover:bg-brand-700' : 'bg-slate-100 text-slate-900 hover:bg-slate-200'
        )}
      >
        {t.start}
      </Link>
    </div>
  )
}
