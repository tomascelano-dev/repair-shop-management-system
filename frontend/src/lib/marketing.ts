import type { SignupAttribution, TrackingConfig } from '../api/types'
import { isPublicPath } from './publicLocale'

export type Consent = 'granted' | 'denied' | null
const CONSENT_KEY = 'rs_cookie_consent_v1'
const ATTRIBUTION_KEY = 'rs_campaign_v1'
export const CONSENT_CHANGED = 'rs:consent'
export const COOKIE_SETTINGS = 'rs:cookie-settings'
let campaign: Partial<SignupAttribution> | null = null
let config: TrackingConfig | undefined
let googleLoaded = false
let metaLoaded = false
let lastPage = ''
let storageFailed = false
const sent = new Set<string>()

type Pixel = ((...args: unknown[]) => void) & { queue: unknown[][]; callMethod?: (...args: unknown[]) => void; push?: Pixel; loaded?: boolean; version?: string }
declare global {
  interface Window {
    dataLayer?: unknown[]
    gtag?: (...args: unknown[]) => void
    fbq?: Pixel
    _fbq?: Pixel
  }
}

function read(storage: Storage, key: string): string | null {
  try { return storage.getItem(key) } catch { return null }
}

export function getConsent(): Consent {
  if (storageFailed) return 'denied'
  try {
    const value = read(window.localStorage, CONSENT_KEY)
    return value === 'granted' || value === 'denied' ? value : null
  } catch { return null }
}

const signals = (value: 'granted' | 'denied') => ({ ad_storage: value, analytics_storage: value, ad_user_data: value, ad_personalization: value })

function googleQueue() {
  window.dataLayer ??= []
  // gtag expects an Arguments object, not a nested array.
  // eslint-disable-next-line prefer-rest-params
  window.gtag ??= function () { window.dataLayer!.push(arguments) }
}

function script(id: string, src: string) {
  if (document.getElementById(id)) return
  const tag = document.createElement('script')
  tag.id = id
  tag.async = true
  tag.src = src
  document.head.appendChild(tag)
}

export function captureCampaign(href = window.location.href, referrer = document.referrer) {
  if (campaign) return
  const url = new URL(href)
  if (!isPublicPath(url.pathname)) return
  try {
    const saved = getConsent() === 'granted' ? read(window.sessionStorage, ATTRIBUTION_KEY) : null
    if (saved) { campaign = JSON.parse(saved); return }
  } catch { /* storage is optional */ }
  const params = url.searchParams
  const value = (key: string) => params.get(key)?.slice(0, 200) || undefined
  let origin: string | undefined
  try { origin = referrer ? new URL(referrer).origin : undefined } catch { /* invalid referrer */ }
  campaign = {
    utmSource: value('utm_source'), utmMedium: value('utm_medium'), utmCampaign: value('utm_campaign'),
    utmTerm: value('utm_term'), utmContent: value('utm_content'),
    gclid: value('gclid'), gbraid: value('gbraid'), wbraid: value('wbraid'), fbclid: value('fbclid'),
    landingPath: url.pathname, referrer: origin,
  }
  persistCampaign()
}

function persistCampaign() {
  if (getConsent() !== 'granted' || !campaign) return
  try { window.sessionStorage.setItem(ATTRIBUTION_KEY, JSON.stringify(campaign)) } catch { /* optional */ }
}

export function setConsent(value: Exclude<Consent, null>) {
  try {
    window.localStorage.setItem(CONSENT_KEY, value)
    storageFailed = false
  } catch {
    // A previous stored grant must not override a withdrawal when storage is full or blocked.
    storageFailed = true
    value = 'denied'
  }
  if (value === 'denied') {
    campaign = null
    try { window.sessionStorage.removeItem(ATTRIBUTION_KEY) } catch { /* optional */ }
    window.gtag?.('consent', 'update', signals('denied'))
    window.fbq?.('consent', 'revoke')
    // Remove first-party measurement cookies on this host and its parent domains.
    for (const cookie of document.cookie.split(';')) {
      const name = cookie.trim().split('=')[0]
      if (!/^(_ga(?:_|$)|_gid$|_gat|_gcl_|_fb[pc]$)/.test(name)) continue
      const parts = location.hostname.split('.')
      document.cookie = `${name}=; Max-Age=0; path=/`
      while (parts.length > 1) {
        document.cookie = `${name}=; Max-Age=0; path=/; domain=.${parts.join('.')}`
        parts.shift()
      }
    }
  } else {
    persistCampaign()
    configureTracking(config)
  }
  lastPage = ''
  window.dispatchEvent(new Event(CONSENT_CHANGED))
}

/** Basic Consent Mode v2: no Google or Meta script/network request before explicit opt-in. */
export function configureTracking(next: TrackingConfig | undefined) {
  config = next
  if (getConsent() !== 'granted') {
    window.gtag?.('consent', 'update', signals('denied'))
    window.fbq?.('consent', 'revoke')
    return
  }
  if (!next) return
  const googleId = next.ga4Id || next.googleAdsId
  if (googleId) {
    googleQueue()
    if (!googleLoaded) {
      window.gtag!('consent', 'default', signals('denied'))
      window.gtag!('set', 'ads_data_redaction', true)
    }
    window.gtag!('consent', 'update', signals('granted'))
    if (!googleLoaded) {
      window.gtag!('js', new Date())
      for (const id of [next.ga4Id, next.googleAdsId].filter(Boolean)) {
        window.gtag!('config', id, { send_page_view: false, cookie_flags: 'SameSite=Lax;Secure', ignore_referrer: true })
      }
      script('rs-google-tag', `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(googleId)}`)
      googleLoaded = true
    }
  }
  if (next.metaPixelId) {
    if (!metaLoaded) {
      const pixel: Pixel = Object.assign((...args: unknown[]) => {
        if (pixel.callMethod) pixel.callMethod(...args)
        else pixel.queue.push(args)
      }, { queue: [] as unknown[][] })
      pixel.push = pixel
      pixel.loaded = true
      pixel.version = '2.0'
      window.fbq = window._fbq = pixel
      pixel('consent', 'grant')
      pixel('set', 'autoConfig', false, next.metaPixelId)
      pixel('init', next.metaPixelId)
      script('rs-meta-pixel', 'https://connect.facebook.net/en_US/fbevents.js')
      metaLoaded = true
    } else window.fbq?.('consent', 'grant')
  }
}

export function trackPage(path: string) {
  if (getConsent() !== 'granted' || !config || !isPublicPath(path) || lastPage === path) return
  lastPage = path
  // Never send auth tokens, checkout IDs, customer/order routes or raw query strings.
  window.gtag?.('event', 'page_view', { page_location: location.origin + path, page_title: document.title, page_referrer: '' })
  window.fbq?.('track', 'PageView')
}

function cookie(name: string): string | undefined {
  return document.cookie.split('; ').find((c) => c.startsWith(`${name}=`))?.slice(name.length + 1)
}

export function signupAttribution(): SignupAttribution {
  if (getConsent() !== 'granted') return { adConsent: false }
  captureCampaign()
  return { ...campaign, fbp: cookie('_fbp'), fbc: cookie('_fbc'), adConsent: true, eventId: `trial_${crypto.randomUUID()}` }
}

function conversion(name: 'StartTrial' | 'Purchase', id: string, value: number, currency: string) {
  if (getConsent() !== 'granted' || !config) return
  const key = `rs_conversion_${id}`
  if (sent.has(key)) return
  try { if (read(window.localStorage, key)) return } catch { /* memory dedup still works */ }
  sent.add(key)
  try { window.localStorage.setItem(key, '1') } catch { /* optional */ }
  const locationUrl = location.origin + (name === 'Purchase' ? '/billing' : '/registro')
  const data = { value, currency, transaction_id: id, page_location: locationUrl, page_referrer: '' }
  window.gtag?.('event', name === 'Purchase' ? 'purchase' : 'sign_up', { ...data, method: 'email' })
  const label = name === 'Purchase' ? config.googleAdsPurchaseLabel : config.googleAdsSignupLabel
  if (config.googleAdsId && label) window.gtag?.('event', 'conversion', { ...data, send_to: `${config.googleAdsId}/${label}` })
  window.fbq?.('track', name, { value, currency }, { eventID: id })
}

export function trackTrial(attribution: SignupAttribution) {
  if (attribution.eventId) conversion('StartTrial', attribution.eventId, 0, 'USD')
}

export function trackPurchase(id: string, value: number, currency: string) {
  conversion('Purchase', id, value, currency)
}
