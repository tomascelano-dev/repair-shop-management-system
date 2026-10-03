// Server-side entry used only at build time (scripts/prerender.mjs): renders the public pages to static HTML so
// that crawlers and payment-provider reviewers that don't run JavaScript see real content. In the browser the SPA
// takes over the page as usual.
import type { ReactElement } from 'react'
import { renderToString } from 'react-dom/server'
import { StaticRouter } from 'react-router-dom/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { BillingConfig, PlansResponse } from './api/types'
import { LandingPage } from './features/public/LandingPage'
import { publicPath, type PublicLocale } from './lib/publicLocale'
import { PricingPage } from './features/public/PricingPage'
import { PrivacyPage, RefundPage, TermsPage } from './features/public/LegalPages'

/** Default dollar prices (backend Billing:UsdPrices). The live page fetches the configured prices on load. */
const PLANS: PlansResponse = {
  country: 'US',
  currency: 'USD',
  trialDays: 14,
  plans: [
    { id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: 25, currency: 'USD' },
    { id: 'Standard', name: 'Estándar', maxBranches: 2, modules: ['purchasing', 'reports'], price: 45, currency: 'USD' },
    { id: 'Pro', name: 'Profesional', maxBranches: 10, modules: ['purchasing', 'reports', 'transfers', 'audit', 'ai'], price: 79, currency: 'USD' },
  ],
}

export interface PublicPage {
  path: string
  locale: PublicLocale
  file: string
  title: string
  description: string
  /** Text the rendered page must contain (the build fails otherwise). */
  expect: string
  element: () => ReactElement
}

export const PUBLIC_PAGES: PublicPage[] = (['es', 'en'] as const).flatMap((locale) => {
  const en = locale === 'en'
  const pages = [
    { section: 'home' as const, title: en ? 'Repair shop management' : 'Gestión de talleres', expect: en ? 'More repairs.' : 'Más reparaciones.', element: () => <LandingPage /> },
    { section: 'pricing' as const, title: en ? 'Pricing' : 'Precios', expect: en ? 'Professional' : 'Profesional', element: () => <PricingPage initialCountry="US" /> },
    { section: 'terms' as const, title: en ? 'Terms of service' : 'Términos del servicio', expect: en ? 'Terms of service' : 'Términos del servicio', element: () => <TermsPage /> },
    { section: 'privacy' as const, title: en ? 'Privacy policy' : 'Política de privacidad', expect: en ? 'Privacy policy' : 'Política de privacidad', element: () => <PrivacyPage /> },
    { section: 'refunds' as const, title: en ? 'Refund policy' : 'Política de reembolsos', expect: en ? 'Refund policy' : 'Política de reembolsos', element: () => <RefundPage /> },
  ]
  return pages.map(({ section, ...page }) => {
    const path = publicPath(locale, section)
    return { ...page, locale, path, file: path === '/' ? 'home.html' : `${path.slice(1)}.html`, title: `${page.title} · RepairShop`,
      description: en ? 'Repair shop software for orders, estimates, inventory and customer updates. Monthly plans with a free trial.' : 'Software para talleres: órdenes, presupuestos, inventario y avisos a clientes. Planes mensuales con prueba gratis.' }
  })
})

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
  client.setQueryData(['plans', 'US'], PLANS)
  client.setQueryData(['billing-config'], {
    signupEnabled: true,
    trialDays: PLANS.trialDays,
    paddleClientToken: null,
    paddleEnvironment: 'production',
    contactEmail: CONTACT_MARKER,
    legalName: SELLER_MARKER,
  } satisfies BillingConfig)
  return renderToString(
    <QueryClientProvider client={client}>
      <StaticRouter location={page.path}>{page.element()}</StaticRouter>
    </QueryClientProvider>
  )
}
