import { useEffect, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { openCookieSettings } from '../../lib/tracking'
import { alternatePath, PUBLIC_ROUTES, useLang, type Lang } from './lang'

const TEXT = {
  es: {
    pricing: 'Precios',
    login: 'Ingresar',
    trial: 'Prueba gratis',
    other: 'English',
    terms: 'Términos',
    privacy: 'Privacidad',
    refunds: 'Reembolsos',
    cookies: 'Cookies',
    legal: 'Legales',
  },
  en: {
    pricing: 'Pricing',
    login: 'Log in',
    trial: 'Start free trial',
    other: 'Español',
    terms: 'Terms',
    privacy: 'Privacy',
    refunds: 'Refunds',
    cookies: 'Cookies',
    legal: 'Legal',
  },
} satisfies Record<Lang, Record<string, string>>

function otherLang(lang: Lang): Lang {
  return lang === 'es' ? 'en' : 'es'
}

export function PublicHeader() {
  const lang = useLang()
  const routes = PUBLIC_ROUTES[lang]
  const t = TEXT[lang]
  const { pathname } = useLocation()
  return (
    <header className="mx-auto flex max-w-6xl items-center justify-between gap-3 px-4 py-5">
      <Link to={routes.home} className="flex items-center gap-2">
        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-600 text-sm font-bold text-white">RS</span>
        <span className="text-lg font-semibold text-slate-900">RepairShop</span>
      </Link>
      <nav className="flex items-center gap-3 text-sm sm:gap-4">
        <Link to={routes.pricing} className="hidden text-slate-600 hover:text-slate-900 sm:inline">
          {t.pricing}
        </Link>
        <Link to={alternatePath(pathname, otherLang(lang))} hrefLang={otherLang(lang)} className="text-slate-600 hover:text-slate-900">
          {t.other}
        </Link>
        <Link to="/login" className="text-slate-600 hover:text-slate-900">
          {t.login}
        </Link>
        <Link to={routes.signup} className="rounded-lg bg-brand-600 px-3 py-2 font-medium text-white hover:bg-brand-700">
          {t.trial}
        </Link>
      </nav>
    </header>
  )
}

export function PublicFooter() {
  const lang = useLang()
  const routes = PUBLIC_ROUTES[lang]
  const t = TEXT[lang]
  const links = [
    { to: routes.pricing, label: t.pricing },
    { to: routes.terms, label: t.terms },
    { to: routes.privacy, label: t.privacy },
    { to: routes.refunds, label: t.refunds },
  ]
  return (
    <footer className="mx-auto max-w-6xl px-4 py-8 text-center text-xs text-slate-500">
      {links.map((l, i) => (
        <span key={l.to}>
          {i > 0 && ' · '}
          <Link to={l.to} className="hover:underline">
            {l.label}
          </Link>
        </span>
      ))}
      {' · '}
      <button type="button" onClick={openCookieSettings} className="hover:underline">
        {t.cookies}
      </button>
    </footer>
  )
}

/** Links to the legal pages under the sign-in and signup cards. */
export function LegalLinks({ className }: { className?: string }) {
  const lang = useLang()
  const routes = PUBLIC_ROUTES[lang]
  const t = TEXT[lang]
  const links = [
    { to: routes.pricing, label: t.pricing },
    { to: routes.terms, label: t.terms },
    { to: routes.privacy, label: t.privacy },
    { to: routes.refunds, label: t.refunds },
  ]
  return (
    <nav aria-label={t.legal} className={className}>
      {links.map((l, i) => (
        <span key={l.to}>
          {i > 0 && ' · '}
          <Link to={l.to} className="hover:text-white hover:underline">
            {l.label}
          </Link>
        </span>
      ))}
    </nav>
  )
}

/** Header, content and footer of the public pages; keeps the document title in sync. */
export function PublicLayout({ title, children }: { title: string; children: ReactNode }) {
  useEffect(() => {
    document.title = title
  }, [title])
  return (
    <div className="min-h-full bg-slate-50">
      <PublicHeader />
      {children}
      <PublicFooter />
    </div>
  )
}
