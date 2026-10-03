import { useLocation } from 'react-router-dom'

export type PublicLocale = 'es' | 'en'
export type PublicSection = 'home' | 'pricing' | 'signup' | 'terms' | 'privacy' | 'refunds'

const paths: Record<PublicLocale, Record<PublicSection, string>> = {
  es: { home: '/', pricing: '/precios', signup: '/registro', terms: '/terminos', privacy: '/privacidad', refunds: '/reembolsos' },
  en: { home: '/en', pricing: '/en/pricing', signup: '/en/signup', terms: '/en/terms', privacy: '/en/privacy', refunds: '/en/refunds' },
}

export function publicLocale(path: string): PublicLocale {
  return /^\/en(?:\/|$)/.test(path) ? 'en' : 'es'
}

export function usePublicLocale(): PublicLocale {
  return publicLocale(useLocation().pathname)
}

export function publicPath(locale: PublicLocale, section: PublicSection): string {
  return paths[locale][section]
}

export function languagePath(path: string, locale: PublicLocale): string {
  const section = (Object.keys(paths.es) as PublicSection[]).find((key) => paths.es[key] === path || paths.en[key] === path)
  return publicPath(locale, section ?? 'home')
}

export function isPublicPath(path: string): boolean {
  return Object.values(paths).some((locale) => Object.values(locale).includes(path))
}

export function countryName(code: string, locale: PublicLocale): string {
  if (code === 'OT') return locale === 'en' ? 'Other country' : 'Otro país'
  return new Intl.DisplayNames([locale], { type: 'region' }).of(code) ?? code
}
