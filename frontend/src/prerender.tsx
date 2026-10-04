// Server-side entry used only at build time (scripts/prerender.mjs): renders the public pages to static HTML so
// that crawlers and payment-provider reviewers that don't run JavaScript see real content. In the browser the SPA
// takes over the page as usual.
import type { ReactElement } from 'react'
import { renderToString } from 'react-dom/server'
import { StaticRouter } from 'react-router-dom/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { BillingConfig, PlansResponse } from './api/types'
import type { Lang } from './features/public/lang'
import { LandingPage } from './features/public/LandingPage'
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
  lang: Lang
  title: string
  description: string
  /** Text the rendered page must contain (the build fails otherwise). */
  expect: string
  element: () => ReactElement
}

const ES_DESCRIPTION =
  'Software de gestión para talleres de reparación: órdenes, presupuestos, stock, caja y avisos por WhatsApp. Prueba gratis 14 días, sin tarjeta.'
const EN_DESCRIPTION =
  'Repair shop management software: repair orders, quotes, stock, cash register and WhatsApp notifications. 14-day free trial, no credit card.'

export const PUBLIC_PAGES: PublicPage[] = [
  {
    // Served at the bare domain to browsers without a session (deploy/Caddyfile).
    path: '/',
    file: 'inicio.html',
    lang: 'es',
    title: 'RepairShop · Software para servicio técnico',
    description: ES_DESCRIPTION,
    expect: 'Prueba gratis 14 días',
    element: () => <LandingPage initialCountry="US" />,
  },
  {
    path: '/precios',
    file: 'precios.html',
    lang: 'es',
    title: 'Precios · RepairShop',
    description: ES_DESCRIPTION,
    expect: 'Profesional',
    element: () => <PricingPage initialCountry="US" />,
  },
  { path: '/terminos', file: 'terminos.html', lang: 'es', title: 'Términos del servicio · RepairShop', description: 'Términos del servicio de RepairShop.', expect: 'Términos del servicio', element: () => <TermsPage /> },
  { path: '/privacidad', file: 'privacidad.html', lang: 'es', title: 'Política de privacidad · RepairShop', description: 'Política de privacidad de RepairShop.', expect: 'Política de privacidad', element: () => <PrivacyPage /> },
  { path: '/reembolsos', file: 'reembolsos.html', lang: 'es', title: 'Política de reembolsos · RepairShop', description: 'Política de reembolsos de RepairShop.', expect: 'Política de reembolsos', element: () => <RefundPage /> },
  {
    path: '/en',
    file: 'en.html',
    lang: 'en',
    title: 'RepairShop · Repair shop management software',
    description: EN_DESCRIPTION,
    expect: 'Start your 14-day free trial',
    element: () => <LandingPage initialCountry="US" />,
  },
  {
    path: '/en/pricing',
    file: 'en/pricing.html',
    lang: 'en',
    title: 'Pricing · RepairShop',
    description: EN_DESCRIPTION,
    expect: 'Professional',
    element: () => <PricingPage initialCountry="US" />,
  },
  { path: '/en/terms', file: 'en/terms.html', lang: 'en', title: 'Terms of Service · RepairShop', description: 'RepairShop Terms of Service.', expect: 'Terms of Service', element: () => <TermsPage /> },
  { path: '/en/privacy', file: 'en/privacy.html', lang: 'en', title: 'Privacy Policy · RepairShop', description: 'RepairShop Privacy Policy.', expect: 'Privacy Policy', element: () => <PrivacyPage /> },
  { path: '/en/refunds', file: 'en/refunds.html', lang: 'en', title: 'Refund Policy · RepairShop', description: 'RepairShop Refund Policy.', expect: 'Refund Policy', element: () => <RefundPage /> },
]

export { contactFallback, sellerSentence } from './features/public/legalText'

/**
 * The contact email and seller name come from the server's environment, not from the build: the pages are rendered
 * with these markers and scripts/prerender.mjs turns them into Caddy template expressions filled in when served.
 */
export const CONTACT_MARKER = '__RS_CONTACT_EMAIL__'
export const SELLER_MARKER = '__RS_LEGAL_NAME__'

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
    tracking: { ga4Id: null, googleAdsId: null, googleAdsSignupLabel: null, googleAdsPurchaseLabel: null, metaPixelId: null },
    visitorCountry: null,
  } satisfies BillingConfig)
  return renderToString(
    <QueryClientProvider client={client}>
      <StaticRouter location={page.path}>{page.element()}</StaticRouter>
    </QueryClientProvider>
  )
}
