import type { PublicLocale } from './publicLocale'

/**
 * Search titles of the landing and pricing pages. The pre-rendered HTML and the running app must show the same
 * one: Google renders the JavaScript and keeps the title the page ends up with.
 */
export const SEO_TITLES: Record<'home' | 'pricing', Record<PublicLocale, string>> = {
  home: {
    es: 'Sistema para servicio técnico y taller de celulares | RepairShop',
    en: 'Repair shop software for phones and electronics | RepairShop',
  },
  pricing: {
    es: 'Precios del sistema para servicio técnico | RepairShop',
    en: 'Pricing for repair shop software | RepairShop',
  },
}
