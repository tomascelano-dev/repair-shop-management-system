import type { Plan, PlanId, SubscriptionStatus } from '../api/types'
import type { Tone } from './labels'

/** Countries offered at signup (name in Spanish). Argentina pays in pesos; everyone else, in dollars. */
export const COUNTRIES: { code: string; name: string }[] = [
  { code: 'AR', name: 'Argentina' },
  { code: 'BO', name: 'Bolivia' },
  { code: 'BR', name: 'Brasil' },
  { code: 'CL', name: 'Chile' },
  { code: 'CO', name: 'Colombia' },
  { code: 'CR', name: 'Costa Rica' },
  { code: 'CU', name: 'Cuba' },
  { code: 'EC', name: 'Ecuador' },
  { code: 'SV', name: 'El Salvador' },
  { code: 'ES', name: 'España' },
  { code: 'US', name: 'Estados Unidos' },
  { code: 'GT', name: 'Guatemala' },
  { code: 'HN', name: 'Honduras' },
  { code: 'MX', name: 'México' },
  { code: 'NI', name: 'Nicaragua' },
  { code: 'PA', name: 'Panamá' },
  { code: 'PY', name: 'Paraguay' },
  { code: 'PE', name: 'Perú' },
  { code: 'PR', name: 'Puerto Rico' },
  { code: 'DO', name: 'República Dominicana' },
  { code: 'UY', name: 'Uruguay' },
  { code: 'VE', name: 'Venezuela' },
  { code: 'OT', name: 'Otro país' },
]

/** Best guess of the visitor's country from the browser language (es-MX → MX). Defaults to Argentina. */
export function detectCountry(languages: readonly string[] = typeof navigator === 'undefined' ? [] : navigator.languages ?? [navigator.language]): string {
  for (const lang of languages) {
    const region = lang.split('-')[1]?.toUpperCase()
    if (region && COUNTRIES.some((c) => c.code === region)) return region
  }
  return 'AR'
}

/** "Otro país" is billed like any country outside Argentina. */
export function billingCountry(code: string): string {
  return code === 'OT' ? 'US' : code
}

export function browserTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone
  } catch {
    return undefined
  }
}

/** Language of the public website (the app itself is in Spanish). */
type Lang = 'es' | 'en'

export const MODULE_LABEL: Record<string, string> = {
  purchasing: 'Compras y proveedores',
  reports: 'Reportes detallados y Excel',
  transfers: 'Transferencias entre sucursales',
  audit: 'Auditoría de cambios',
  ai: 'Sugerencias de diagnóstico con IA',
}

const MODULE_LABEL_EN: Record<string, string> = {
  purchasing: 'Purchasing and suppliers',
  reports: 'Detailed reports and Excel export',
  transfers: 'Transfers between branches',
  audit: 'Change history (audit log)',
  ai: 'AI diagnosis suggestions',
}

/** What every plan includes, shown above the per-plan extras. */
export const CORE_FEATURES = [
  'Órdenes, presupuestos y tablero del taller',
  'Portal del cliente con seguimiento y aprobación',
  'Punto de venta, caja e inventario',
  'Avisos por WhatsApp, email y SMS',
  'Usuarios ilimitados',
]

const CORE_FEATURES_EN = [
  'Repair orders, quotes and a workshop board',
  'Customer portal with status tracking and quote approval',
  'Point of sale, cash register and inventory',
  'WhatsApp, email and SMS notifications',
  'Unlimited users',
]

export function coreFeatures(lang: Lang = 'es'): string[] {
  return lang === 'en' ? CORE_FEATURES_EN : CORE_FEATURES
}

export function planFeatures(plan: Plan, lang: Lang = 'es'): string[] {
  if (lang === 'en') {
    const branches = plan.maxBranches === 1 ? '1 branch' : `Up to ${plan.maxBranches} branches`
    return [branches, ...plan.modules.map((m) => MODULE_LABEL_EN[m] ?? m)]
  }
  const branches = plan.maxBranches === 1 ? '1 sucursal' : `Hasta ${plan.maxBranches} sucursales`
  return [branches, ...plan.modules.map((m) => MODULE_LABEL[m] ?? m)]
}

export const PLAN_TAGLINE: Record<PlanId, string> = {
  Basic: 'Para el taller que arranca',
  Standard: 'Para el taller con stock y proveedores',
  Pro: 'Para cadenas y talleres grandes',
}

const PLAN_TAGLINE_EN: Record<PlanId, string> = {
  Basic: 'For a shop that is getting started',
  Standard: 'For shops with stock and suppliers',
  Pro: 'For chains and large shops',
}

const PLAN_NAME_EN: Record<PlanId, string> = { Basic: 'Basic', Standard: 'Standard', Pro: 'Professional' }

export function planTagline(plan: Plan, lang: Lang = 'es'): string {
  return (lang === 'en' ? PLAN_TAGLINE_EN : PLAN_TAGLINE)[plan.id]
}

export function planName(plan: Plan, lang: Lang = 'es'): string {
  return lang === 'en' ? PLAN_NAME_EN[plan.id] : plan.name
}

/** Countries for a select, named in the page's language ("Otro país" / "Other country" last). */
export function countryOptions(lang: Lang = 'es'): { code: string; name: string }[] {
  if (lang === 'es') return COUNTRIES
  let names: Intl.DisplayNames | null = null
  try {
    names = new Intl.DisplayNames(['en'], { type: 'region' })
  } catch {
    names = null
  }
  const named = COUNTRIES.filter((c) => c.code !== 'OT').map((c) => ({ code: c.code, name: names?.of(c.code) ?? c.name }))
  return [...named.sort((a, b) => a.name.localeCompare(b.name, 'en')), { code: 'OT', name: 'Other country' }]
}

export const SUBSCRIPTION_STATUS: Record<SubscriptionStatus, { label: string; tone: Tone }> = {
  Trialing: { label: 'Prueba gratis', tone: 'blue' },
  Active: { label: 'Activa', tone: 'green' },
  PastDue: { label: 'Pago pendiente', tone: 'amber' },
  Canceled: { label: 'Cancelada', tone: 'slate' },
  Expired: { label: 'Vencida', tone: 'rose' },
}

export function planPrice(plan: Plan): string {
  if (plan.price == null) return 'Consultar'
  return new Intl.NumberFormat(plan.currency === 'ARS' ? 'es-AR' : 'en-US', {
    style: 'currency',
    currency: plan.currency,
    maximumFractionDigits: 0,
  }).format(plan.price)
}
