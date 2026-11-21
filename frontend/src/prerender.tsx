// Server-side entry used only at build time (scripts/prerender.mjs): renders the public pages to static HTML so
// that crawlers and payment-provider reviewers that don't run JavaScript see real content. In the browser the SPA
// takes over the page as usual.
import type { ReactElement } from 'react'
import { renderToString } from 'react-dom/server'
import { StaticRouter } from 'react-router-dom/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { BillingConfig, PlansResponse } from './api/types'
import { LandingPage } from './features/public/LandingPage'
import { publicPath, type PublicLocale, type PublicSection } from './lib/publicLocale'
import { PricingPage } from './features/public/PricingPage'
import { PrivacyPage, RefundPage, TermsPage } from './features/public/LegalPages'

/** Default Argentine prices (backend Billing:Countries:AR:Prices). The live page fetches the configured prices on load. */
const PLANS: PlansResponse = {
  country: 'AR',
  currency: 'ARS',
  trialDays: 14,
  plans: [
    { id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: 29900, currency: 'ARS' },
    { id: 'Standard', name: 'Estándar', maxBranches: 2, modules: ['purchasing', 'reports'], price: 44900, currency: 'ARS' },
    { id: 'Pro', name: 'Profesional', maxBranches: 10, modules: ['purchasing', 'reports', 'transfers', 'audit', 'ai'], price: 74900, currency: 'ARS' },
  ],
}

export interface PublicPage {
  path: string
  locale: PublicLocale
  section: PublicSection
  file: string
  title: string
  description: string
  /** Same page in each language (hreflang). */
  alternates: Record<PublicLocale, string>
  /** Text the rendered page must contain (the build fails otherwise). */
  expect: string
  element: () => ReactElement
}

/**
 * Titles and descriptions are what Google shows in the results: they carry the searches the product answers
 * ("sistema para servicio técnico", "software para taller de celulares"). Keep titles under ~60 characters.
 */
export const PUBLIC_PAGES: PublicPage[] = (['es', 'en'] as const).flatMap((locale) => {
  const en = locale === 'en'
  const pages = [
    {
      section: 'home' as const,
      title: en ? 'Repair shop software for phones and electronics | RepairShop' : 'Sistema para servicio técnico y taller de celulares | RepairShop',
      description: en
        ? 'Repair shop management software: repair orders, estimates, online tracking for customers, parts inventory, point of sale and cash register. 14-day free trial.'
        : 'Software para servicio técnico: órdenes de reparación, presupuestos, seguimiento online para clientes, stock de repuestos, caja y factura ARCA. 14 días gratis.',
      expect: en ? 'More repairs.' : 'Más reparaciones.',
      element: () => <LandingPage />,
    },
    {
      section: 'pricing' as const,
      title: en ? 'Pricing for repair shop software | RepairShop' : 'Precios del sistema para servicio técnico | RepairShop',
      description: en
        ? 'Monthly plans for repair shops with unlimited users. Paid in your local currency with Mercado Pago. Try it free for 14 days, no card required.'
        : 'Planes mensuales en pesos para tu servicio técnico, con usuarios ilimitados y débito automático por Mercado Pago. Prueba 14 días gratis, sin tarjeta.',
      expect: en ? 'Professional' : 'Profesional',
      element: () => <PricingPage initialCountry="AR" />,
    },
    { section: 'terms' as const, title: en ? 'Terms of service · RepairShop' : 'Términos del servicio · RepairShop', description: en ? 'Terms of service of RepairShop, the repair shop management software.' : 'Términos del servicio de RepairShop, el sistema de gestión para servicio técnico.', expect: en ? 'Terms of service' : 'Términos del servicio', element: () => <TermsPage /> },
    { section: 'privacy' as const, title: en ? 'Privacy policy · RepairShop' : 'Política de privacidad · RepairShop', description: en ? 'How RepairShop processes and protects personal data.' : 'Qué datos trata RepairShop y cómo los protege.', expect: en ? 'Privacy policy' : 'Política de privacidad', element: () => <PrivacyPage /> },
    { section: 'refunds' as const, title: en ? 'Refund policy · RepairShop' : 'Política de reembolsos · RepairShop', description: en ? 'Refunds, cancellation and the right of withdrawal for RepairShop subscriptions.' : 'Reintegros, cancelación y botón de arrepentimiento de las suscripciones de RepairShop.', expect: en ? 'Refund policy' : 'Política de reembolsos', element: () => <RefundPage /> },
  ]
  return pages.map((page) => {
    const path = publicPath(locale, page.section)
    return { ...page, locale, path, file: path === '/' ? 'home.html' : `${path.slice(1)}.html`,
      alternates: { es: publicPath('es', page.section), en: publicPath('en', page.section) } }
  })
})

/** Prices in the structured data of the landing and pricing pages (the same defaults the page shows). */
// Build-time SSR entry, not a Fast Refresh component module.
// eslint-disable-next-line react-refresh/only-export-components
export const SEO_PRICES = { currency: PLANS.currency, low: Math.min(...PLANS.plans.map((p) => p.price ?? Infinity)), high: Math.max(...PLANS.plans.map((p) => p.price ?? 0)), count: PLANS.plans.length }

/**
 * The contact email and seller name come from the server's environment, not from the build: the pages are rendered
 * with these markers and scripts/prerender.mjs turns them into Caddy template expressions filled in when served.
 */
export const CONTACT_MARKER = '__RS_CONTACT_EMAIL__'
export const SELLER_MARKER = '__RS_LEGAL_NAME__'

// Build-time SSR entry, not a Fast Refresh component module.
// eslint-disable-next-line react-refresh/only-export-components
export function render(page: PublicPage): string {
  const client = new QueryClient()
  client.setQueryData(['plans', 'AR'], PLANS)
  client.setQueryData(['billing-config'], {
    signupEnabled: true,
    trialDays: PLANS.trialDays,
    countries: [{ code: 'AR', currency: 'ARS' }],
    contactEmail: CONTACT_MARKER,
    legalName: SELLER_MARKER,
  } satisfies BillingConfig)
  return renderToString(
    <QueryClientProvider client={client}>
      <StaticRouter location={page.path}>{page.element()}</StaticRouter>
    </QueryClientProvider>
  )
}
