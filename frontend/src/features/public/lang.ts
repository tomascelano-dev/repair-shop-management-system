import { useLocation } from 'react-router-dom'

/** Languages of the public website (landing, prices, legal pages, signup). The app itself is in Spanish. */
export type Lang = 'es' | 'en'

export type PublicPageKey = 'home' | 'pricing' | 'terms' | 'privacy' | 'refunds' | 'signup'

/** The same page in each language: English lives under /en with English paths. */
export const PUBLIC_ROUTES: Record<Lang, Record<PublicPageKey, string>> = {
  es: { home: '/', pricing: '/precios', terms: '/terminos', privacy: '/privacidad', refunds: '/reembolsos', signup: '/registro' },
  en: { home: '/en', pricing: '/en/pricing', terms: '/en/terms', privacy: '/en/privacy', refunds: '/en/refunds', signup: '/en/signup' },
}

export function langFromPath(pathname: string): Lang {
  return pathname === '/en' || pathname.startsWith('/en/') ? 'en' : 'es'
}

/** Page key of a public path (null for the app's own routes). */
export function publicPageOf(pathname: string): PublicPageKey | null {
  const path = pathname.length > 1 ? pathname.replace(/\/+$/, '') : pathname
  for (const routes of Object.values(PUBLIC_ROUTES)) {
    for (const [key, value] of Object.entries(routes)) if (value === path) return key as PublicPageKey
  }
  return null
}

/** The equivalent page in the other language (the home page when there is no direct equivalent). */
export function alternatePath(pathname: string, to: Lang): string {
  return PUBLIC_ROUTES[to][publicPageOf(pathname) ?? 'home']
}

export function useLang(): Lang {
  return langFromPath(useLocation().pathname)
}

export function usePublicRoutes() {
  return PUBLIC_ROUTES[useLang()]
}
