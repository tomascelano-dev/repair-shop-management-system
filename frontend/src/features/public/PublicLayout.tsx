import { useEffect, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { COOKIE_SETTINGS } from '../../lib/marketing'
import { languagePath, publicPath, usePublicLocale } from '../../lib/publicLocale'

export function PublicHeader() {
  const locale = usePublicLocale()
  const { pathname, search } = useLocation()
  const en = locale === 'en'
  return (
    <header className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-4 py-5">
      <Link to={publicPath(locale, 'home')} className="flex items-center gap-2 font-semibold text-slate-900">
        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-600 text-sm font-bold text-white">RS</span>
        <span className="text-lg">RepairShop</span>
      </Link>
      <nav aria-label={en ? 'Main navigation' : 'Navegación principal'} className="flex flex-wrap items-center gap-4 text-sm">
        <Link to={publicPath(locale, 'pricing')}>{en ? 'Pricing' : 'Precios'}</Link>
        <Link to={languagePath(pathname, en ? 'es' : 'en') + search} lang={en ? 'es' : 'en'} className="underline">{en ? 'Español' : 'English'}</Link>
        <Link to="/login">{en ? 'Sign in' : 'Ingresar'}</Link>
        <Link to={publicPath(locale, 'signup')} className="rounded-lg bg-brand-600 px-3 py-2 font-medium text-white hover:bg-brand-700">
          {en ? 'Try for free' : 'Probar gratis'}
        </Link>
      </nav>
    </header>
  )
}

export function PublicFooter() {
  const locale = usePublicLocale()
  const labels = locale === 'en' ? ['Pricing', 'Terms', 'Privacy', 'Refunds'] : ['Precios', 'Términos', 'Privacidad', 'Reembolsos']
  return (
    <footer className="mx-auto flex max-w-6xl flex-wrap justify-center gap-4 px-4 py-8 text-xs text-slate-600">
      {(['pricing', 'terms', 'privacy', 'refunds'] as const).map((section, i) => (
        <Link key={section} to={publicPath(locale, section)} className="hover:underline">{labels[i]}</Link>
      ))}
      <button type="button" onClick={() => window.dispatchEvent(new Event(COOKIE_SETTINGS))} className="underline">
        {locale === 'en' ? 'Cookie settings' : 'Preferencias de cookies'}
      </button>
    </footer>
  )
}

export function PublicLayout({ title, children }: { title: string; children: ReactNode }) {
  const locale = usePublicLocale()
  useEffect(() => {
    document.documentElement.lang = locale
    document.title = `${title} · RepairShop`
  }, [locale, title])
  return <div className="min-h-full bg-slate-50"><PublicHeader />{children}<PublicFooter /></div>
}
