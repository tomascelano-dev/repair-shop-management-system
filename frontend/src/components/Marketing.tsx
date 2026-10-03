import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useLocation } from 'react-router-dom'
import { billingApi } from '../api/endpoints'
import { useSession } from '../auth/session'
import { captureCampaign, configureTracking, CONSENT_CHANGED, COOKIE_SETTINGS, getConsent, setConsent, trackPage } from '../lib/marketing'
import { isPublicPath, publicPath, usePublicLocale } from '../lib/publicLocale'

export function Marketing() {
  const locale = usePublicLocale()
  const en = locale === 'en'
  const { pathname, search } = useLocation()
  const { state, role } = useSession()
  const publicPage = isPublicPath(pathname) && (pathname !== '/' || state.status === 'anonymous')
  const config = useQuery({ queryKey: ['billing-config'], queryFn: billingApi.config, staleTime: 5 * 60_000, enabled: publicPage || pathname === '/billing' })
  const [consent, updateConsent] = useState(getConsent)
  const [editing, setEditing] = useState(false)
  const tracking = config.data?.tracking
  const enabled = !!(tracking?.ga4Id || tracking?.googleAdsId || tracking?.metaPixelId)

  useEffect(() => {
    const changed = () => updateConsent(getConsent())
    const settings = () => setEditing(true)
    window.addEventListener(CONSENT_CHANGED, changed)
    window.addEventListener('storage', changed)
    window.addEventListener(COOKIE_SETTINGS, settings)
    return () => {
      window.removeEventListener(CONSENT_CHANGED, changed)
      window.removeEventListener('storage', changed)
      window.removeEventListener(COOKIE_SETTINGS, settings)
    }
  }, [])

  useEffect(() => {
    if (publicPage) captureCampaign()
    configureTracking(tracking)
    if (publicPage) trackPage(pathname)
  }, [pathname, search, tracking, consent, publicPage])

  useEffect(() => {
    if (state.status === 'authenticated' && role === 'Admin' && consent === 'denied') {
      void billingApi.revokeAdConsent().catch(() => undefined)
    }
  }, [state.status, role, consent])

  if (!editing && (!enabled || consent !== null)) return null
  return (
    <aside aria-label={en ? 'Cookie preferences' : 'Preferencias de cookies'} className="fixed inset-x-3 bottom-3 z-50 mx-auto max-w-3xl rounded-xl border border-slate-200 bg-white p-5 shadow-xl">
      <h2 className="font-semibold text-slate-900">{en ? 'Your cookie choices' : 'Tú eliges las cookies'}</h2>
      <p className="mt-2 text-sm text-slate-600">{en ? 'We use essential cookies to keep you signed in. With your permission, Google and Meta help us measure visits, free trials and subscriptions. You can change your choice at any time.' : 'Usamos cookies necesarias para mantener tu sesión. Con tu permiso, Google y Meta nos ayudan a medir visitas, pruebas gratuitas y suscripciones. Puedes cambiar tu elección en cualquier momento.'} <Link to={publicPath(locale, 'privacy')} className="underline">{en ? 'Privacy policy' : 'Política de privacidad'}</Link></p>
      <div className="mt-4 flex flex-wrap gap-3">
        <button type="button" className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium" onClick={() => { setConsent('denied'); setEditing(false) }}>{en ? 'Only necessary' : 'Solo necesarias'}</button>
        <button type="button" className="rounded-lg border border-brand-600 bg-brand-600 px-4 py-2 text-sm font-medium text-white" onClick={() => { setConsent('granted'); setEditing(false) }}>{en ? 'Accept optional cookies' : 'Aceptar cookies opcionales'}</button>
      </div>
    </aside>
  )
}
