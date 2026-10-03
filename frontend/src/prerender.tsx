// Server-side entry used only at build time (scripts/prerender.mjs): renders the public pages to static HTML so
// that crawlers and payment-provider reviewers that don't run JavaScript see real content. In the browser the SPA
// takes over the page as usual.
import type { ReactElement } from 'react'
import { renderToString } from 'react-dom/server'
import { StaticRouter } from 'react-router-dom/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { PlansResponse } from './api/types'
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
  file: string
  title: string
  description: string
  /** Text the rendered page must contain (the build fails otherwise). */
  expect: string
  element: () => ReactElement
}

export const PUBLIC_PAGES: PublicPage[] = [
  {
    path: '/precios',
    file: 'precios.html',
    title: 'Precios · RepairShop',
    description: 'Software de gestión para talleres de reparación: órdenes, presupuestos, stock, caja y avisos. Planes mensuales con prueba gratis.',
    expect: 'Profesional',
    element: () => <PricingPage initialCountry="US" />,
  },
  {
    path: '/terminos',
    file: 'terminos.html',
    title: 'Términos del servicio · RepairShop',
    description: 'Términos del servicio de RepairShop.',
    expect: 'Términos del servicio',
    element: () => <TermsPage />,
  },
  {
    path: '/privacidad',
    file: 'privacidad.html',
    title: 'Política de privacidad · RepairShop',
    description: 'Política de privacidad de RepairShop.',
    expect: 'Política de privacidad',
    element: () => <PrivacyPage />,
  },
  {
    path: '/reembolsos',
    file: 'reembolsos.html',
    title: 'Política de reembolsos · RepairShop',
    description: 'Política de reembolsos de RepairShop.',
    expect: 'Política de reembolsos',
    element: () => <RefundPage />,
  },
]

export function render(page: PublicPage): string {
  const client = new QueryClient()
  client.setQueryData(['plans', 'US'], PLANS)
  return renderToString(
    <QueryClientProvider client={client}>
      <StaticRouter location={page.path}>{page.element()}</StaticRouter>
    </QueryClientProvider>
  )
}
