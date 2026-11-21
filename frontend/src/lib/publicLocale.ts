import { useLocation } from 'react-router-dom'

export type PublicLocale = 'es' | 'en'
/** Long-tail guide pages (features/public/guides.tsx). Spanish only: they answer searches made in Argentina. */
export type GuideSection = 'guide-phones' | 'guide-orders' | 'guide-stock' | 'guide-invoicing' | 'guide-template' | 'guide-vs-excel'
export type PublicSection = 'home' | 'pricing' | 'signup' | 'terms' | 'privacy' | 'refunds' | 'withdrawal' | GuideSection

export const GUIDE_SECTIONS: readonly GuideSection[] = ['guide-phones', 'guide-orders', 'guide-template', 'guide-stock', 'guide-invoicing', 'guide-vs-excel']

const es: Record<PublicSection, string> = {
  home: '/', pricing: '/precios', signup: '/registro', terms: '/terminos', privacy: '/privacidad', refunds: '/reembolsos', withdrawal: '/arrepentimiento',
  'guide-phones': '/sistema-servicio-tecnico-celulares',
  'guide-orders': '/orden-de-reparacion-online',
  'guide-stock': '/control-de-stock-de-repuestos',
  'guide-invoicing': '/factura-electronica-servicio-tecnico',
  'guide-template': '/plantilla-orden-de-servicio-tecnico',
  'guide-vs-excel': '/sistema-vs-excel-servicio-tecnico',
}
const en: Partial<Record<PublicSection, string>> = {
  home: '/en', pricing: '/en/pricing', signup: '/en/signup', terms: '/en/terms', privacy: '/en/privacy', refunds: '/en/refunds', withdrawal: '/en/withdrawal',
}
const paths: Record<PublicLocale, Partial<Record<PublicSection, string>>> = { es, en }

export function publicLocale(path: string): PublicLocale {
  return /^\/en(?:\/|$)/.test(path) ? 'en' : 'es'
}

export function usePublicLocale(): PublicLocale {
  return publicLocale(useLocation().pathname)
}

/** Whether the section has a page in that language (the guides are Spanish only). */
export function hasPublicPage(locale: PublicLocale, section: PublicSection): boolean {
  return paths[locale][section] !== undefined
}

/** Path of the section in that language; a section without a page in it falls back to that language's home. */
export function publicPath(locale: PublicLocale, section: PublicSection): string {
  return paths[locale][section] ?? (locale === 'en' ? '/en' : '/')
}

export function languagePath(path: string, locale: PublicLocale): string {
  const section = (Object.keys(es) as PublicSection[]).find((key) => es[key] === path || en[key] === path)
  return publicPath(locale, section ?? 'home')
}

export function isPublicPath(path: string): boolean {
  return Object.values(paths).some((locale) => Object.values(locale).includes(path))
}

export function countryName(code: string, locale: PublicLocale): string {
  if (code === 'OT') return locale === 'en' ? 'Other country' : 'Otro país'
  return new Intl.DisplayNames([locale], { type: 'region' }).of(code) ?? code
}
