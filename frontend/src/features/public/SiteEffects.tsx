import { useEffect, useSyncExternalStore } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { billingApi } from '../../api/endpoints'
import { useSession } from '../../auth/session'
import { Button } from '../../components/ui'
import { captureAttribution } from '../../lib/attribution'
import { adsAllowed, bannerVisible, configure, isConsentRequired, setConsent, snapshot, subscribe, trackPageView } from '../../lib/tracking'
import { langFromPath, publicPageOf, PUBLIC_ROUTES } from './lang'

/**
 * Site-wide effects of the public website, mounted once next to the routes: document language, campaign
 * attribution, page views for the ad tags and the cookie banner. The app's own pages load no ad tags.
 */
export function SiteEffects() {
  const { pathname } = useLocation()
  const { state } = useSession()
  useSyncExternalStore(subscribe, snapshot, snapshot)

  const page = publicPageOf(pathname)
  // The bare domain is the landing page only for visitors; signed-in users see their dashboard there.
  const isPublic = page !== null && (page !== 'home' || state.status === 'anonymous')
  const config = useQuery({ queryKey: ['billing-config'], queryFn: billingApi.config, staleTime: 5 * 60_000, enabled: isPublic })
  const ready = !!config.data

  useEffect(() => {
    if (config.data) configure(config.data.tracking, config.data.visitorCountry)
  }, [config.data])

  useEffect(() => {
    document.documentElement.lang = langFromPath(pathname)
  }, [pathname])

  useEffect(() => {
    if (!isPublic) return
    captureAttribution(new URL(window.location.href), document.referrer, ready && adsAllowed())
  }, [isPublic, pathname, ready])

  useEffect(() => {
    if (isPublic && ready) trackPageView(pathname)
  }, [isPublic, ready, pathname])

  return isPublic && bannerVisible() ? <CookieBanner lang={langFromPath(pathname)} optIn={isConsentRequired()} /> : null
}

const BANNER = {
  es: {
    text: 'Usamos cookies de Google y Meta para medir nuestros anuncios y saber qué campañas traen nuevos talleres.',
    optIn: 'Solo las activamos si las aceptas.',
    more: 'Más información',
    accept: 'Aceptar',
    reject: 'Rechazar',
  },
  en: {
    text: 'We use Google and Meta cookies to measure our ads and learn which campaigns bring new shops.',
    optIn: 'They are only turned on if you accept.',
    more: 'Learn more',
    accept: 'Accept',
    reject: 'Reject',
  },
}

function CookieBanner({ lang, optIn }: { lang: 'es' | 'en'; optIn: boolean }) {
  const t = BANNER[lang]
  return (
    <div role="region" aria-label="Cookies" className="fixed inset-x-0 bottom-0 z-50 p-3 sm:p-4">
      <div className="mx-auto flex max-w-3xl flex-col gap-3 rounded-2xl bg-white p-4 text-sm text-slate-700 shadow-xl ring-1 ring-slate-200 sm:flex-row sm:items-center">
        <p className="flex-1">
          {t.text} {optIn ? `${t.optIn} ` : ''}
          <Link to={`${PUBLIC_ROUTES[lang].privacy}#cookies`} className="text-brand-700 underline">
            {t.more}
          </Link>
        </p>
        <div className="flex shrink-0 gap-2">
          <Button onClick={() => setConsent('denied')}>{t.reject}</Button>
          <Button variant="primary" onClick={() => setConsent('granted')}>
            {t.accept}
          </Button>
        </div>
      </div>
    </div>
  )
}
