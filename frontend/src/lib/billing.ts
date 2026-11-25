import type { BillingConfig, Plan, PlanId, SubscriptionStatus } from '../api/types'
import type { Tone } from './labels'

/**
 * RepairShop is sold where Mercado Pago charges subscriptions; the API lists the countries actually enabled
 * (each needs the platform's Mercado Pago account there). Argentina is the default.
 */
export const DEFAULT_COUNTRY = 'AR'

/** Countries open for signup and pricing, Argentina first (the API's list, or Argentina while it loads). */
export function offeredCountries(config?: Pick<BillingConfig, 'countries'> | null): string[] {
  const codes = config?.countries?.map((c) => c.code) ?? []
  return codes.length ? codes : [DEFAULT_COUNTRY]
}

/** Best guess among the offered countries from the browser language (es-MX → MX); otherwise the first one. */
export function detectCountry(
  offered: readonly string[] = [DEFAULT_COUNTRY],
  languages: readonly string[] = typeof navigator === 'undefined' ? [] : navigator.languages ?? [navigator.language],
): string {
  for (const lang of languages) {
    const region = lang.split('-')[1]?.toUpperCase()
    if (region && offered.includes(region)) return region
  }
  return offered[0] ?? DEFAULT_COUNTRY
}

const CURRENCY_NAMES: Record<string, [string, string]> = {
  ARS: ['pesos argentinos', 'Argentine pesos'],
  BRL: ['reales', 'Brazilian reais'],
  CLP: ['pesos chilenos', 'Chilean pesos'],
  COP: ['pesos colombianos', 'Colombian pesos'],
  MXN: ['pesos mexicanos', 'Mexican pesos'],
  PEN: ['soles', 'Peruvian soles'],
  UYU: ['pesos uruguayos', 'Uruguayan pesos'],
  USD: ['dólares', 'US dollars'],
}

/** "pesos argentinos" / "Argentine pesos" for the price notes. */
export function currencyName(code: string, locale: 'es' | 'en' = 'es'): string {
  return CURRENCY_NAMES[code]?.[locale === 'en' ? 1 : 0] ?? code
}

const CURRENCY_LOCALE: Record<string, string> = {
  ARS: 'es-AR', BRL: 'pt-BR', CLP: 'es-CL', COP: 'es-CO', MXN: 'es-MX', PEN: 'es-PE', UYU: 'es-UY',
}

export function browserTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone
  } catch {
    return undefined
  }
}

export const MODULE_LABEL: Record<string, string> = {
  purchasing: 'Compras y proveedores',
  reports: 'Reportes detallados y Excel',
  transfers: 'Transferencias entre sucursales',
  audit: 'Auditoría de cambios',
  ai: 'Sugerencias de diagnóstico con IA',
}

/** What every plan includes, shown above the per-plan extras. */
export const CORE_FEATURES = [
  'Órdenes, presupuestos y tablero del taller',
  'Portal del cliente con seguimiento y aprobación',
  'Punto de venta, caja e inventario',
  'Avisos por email y WhatsApp',
  'Cobro con Mercado Pago y factura electrónica ARCA',
  'Usuarios ilimitados',
]

export function planFeatures(plan: Plan): string[] {
  const branches = plan.maxBranches === 1 ? '1 sucursal' : `Hasta ${plan.maxBranches} sucursales`
  return [branches, ...plan.modules.map((m) => MODULE_LABEL[m] ?? m)]
}

export const PLAN_TAGLINE: Record<PlanId, string> = {
  Basic: 'Para el taller que arranca',
  Standard: 'Para el taller con stock y proveedores',
  Pro: 'Para cadenas y talleres grandes',
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
  return new Intl.NumberFormat(CURRENCY_LOCALE[plan.currency] ?? 'en-US', {
    style: 'currency',
    currency: plan.currency,
    maximumFractionDigits: 0,
  }).format(plan.price)
}
