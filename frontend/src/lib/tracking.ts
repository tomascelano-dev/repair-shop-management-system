// Advertising measurement for the public website: Google tag (GA4 + Google Ads) and the Meta pixel.
// Nothing loads until the API reports which tags are configured, and in the EU/EEA, the UK and Switzerland
// nothing loads until the visitor accepts cookies. Elsewhere the tags load unless the visitor rejects them.
// Google gets the choice through Consent Mode v2; the Meta pixel is never loaded without consent.

import type { TrackingConfig } from '../api/types'

export type Consent = 'granted' | 'denied'

type Gtag = (...args: unknown[]) => void
interface Fbq {
  (...args: unknown[]): void
  callMethod?: (...args: unknown[]) => void
  queue: unknown[]
  push: Fbq
  loaded: boolean
  version: string
}

declare global {
  interface Window {
    dataLayer?: unknown[]
    gtag?: Gtag
    fbq?: Fbq
    _fbq?: Fbq
  }
}

const CONSENT_KEY = 'rs.consent'
const REPORTED_KEY = 'rs.conversions'

/** EU and EEA countries, the United Kingdom and Switzerland: ad cookies need opt-in consent. */
const OPT_IN_COUNTRIES = new Set(
  'AT BE BG HR CY CZ DK EE FI FR DE GR HU IE IT LV LT LU MT NL PL PT RO SK SI ES SE IS LI NO GB CH'.split(' ')
)
const OPT_IN_TIME_ZONES = ['Atlantic/Canary', 'Atlantic/Madeira', 'Atlantic/Azores', 'Atlantic/Reykjavik', 'Atlantic/Faroe']

/**
 * Whether the visitor must accept ad cookies before any tag loads. Uses the country Cloudflare reported to the
 * API and falls back to the browser's time zone.
 */
export function consentRequired(visitorCountry: string | null | undefined, timeZone: string | undefined = browserZone()): boolean {
  if (visitorCountry) return OPT_IN_COUNTRIES.has(visitorCountry.toUpperCase())
  if (!timeZone) return true
  return timeZone.startsWith('Europe/') || OPT_IN_TIME_ZONES.includes(timeZone)
}

function browserZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone
  } catch {
    return undefined
  }
}

export function storedConsent(): Consent | null {
  try {
    const value = localStorage.getItem(CONSENT_KEY)
    return value === 'granted' || value === 'denied' ? value : null
  } catch {
    return null
  }
}

// ---- state shared with React (useSyncExternalStore) -------------------------------------------------------------

let config: TrackingConfig | null = null
let visitorCountry: string | null = null
let consent: Consent | null = typeof window === 'undefined' ? null : storedConsent()
let settingsOpen = false
let googleLoaded = false
let metaLoaded = false
const listeners = new Set<() => void>()
let version = 0

function changed() {
  version++
  listeners.forEach((l) => l())
}

export function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function snapshot(): number {
  return version
}

export function hasTags(c: TrackingConfig | null = config): boolean {
  return !!c && !!(c.ga4Id || c.googleAdsId || c.metaPixelId)
}

/** Ad tags may run: the visitor accepted, or was not asked (outside the opt-in countries) and did not reject. */
export function adsAllowed(): boolean {
  if (consent) return consent === 'granted'
  return !consentRequired(visitorCountry)
}

/** The cookie banner shows while there are tags and no choice was made (or the visitor reopened it). */
export function bannerVisible(): boolean {
  return hasTags() && (settingsOpen || consent === null)
}

export function currentConsent(): Consent | null {
  return consent
}

export function isConsentRequired(): boolean {
  return consentRequired(visitorCountry)
}

export function openCookieSettings() {
  settingsOpen = true
  changed()
}

/** Called once the public config arrives. Loads nothing by itself. */
export function configure(c: TrackingConfig | null | undefined, country: string | null | undefined) {
  config = c ?? null
  visitorCountry = country ?? null
  changed()
}

export function setConsent(value: Consent) {
  consent = value
  settingsOpen = false
  try {
    localStorage.setItem(CONSENT_KEY, value)
  } catch {
    // storage blocked: the choice lasts for this page view
  }
  const wasRunning = googleLoaded || metaLoaded
  applyConsent()
  // Accepting in the EU loads the tags now: count the page the visitor is on.
  if (!wasRunning && (googleLoaded || metaLoaded)) trackPageView(window.location.pathname)
  changed()
}

// ---- loading the tags ---------------------------------------------------------------------------------------------

function consentState(granted: boolean) {
  const v = granted ? 'granted' : 'denied'
  return { ad_storage: v, ad_user_data: v, ad_personalization: v, analytics_storage: v }
}

function addScript(src: string) {
  const script = document.createElement('script')
  script.async = true
  script.src = src
  document.head.appendChild(script)
}

function loadGoogle(c: TrackingConfig) {
  const ids = [c.ga4Id, c.googleAdsId].filter((x): x is string => !!x)
  if (googleLoaded || ids.length === 0) return
  googleLoaded = true
  window.dataLayer = window.dataLayer ?? []
  window.gtag = function gtag() {
    // eslint-disable-next-line prefer-rest-params -- the Google tag reads the arguments object
    window.dataLayer!.push(arguments)
  }
  window.gtag('consent', 'default', consentState(true))
  window.gtag('js', new Date())
  // Page views are sent by hand on each route change (single-page app).
  for (const id of ids) window.gtag('config', id, { send_page_view: false })
  addScript(`https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(ids[0])}`)
}

function loadMeta(c: TrackingConfig) {
  if (metaLoaded || !c.metaPixelId) return
  metaLoaded = true
  if (!window.fbq) {
    // Meta's standard pixel stub: queues calls until fbevents.js loads.
    const fbq = function (...args: unknown[]) {
      if (fbq.callMethod) fbq.callMethod(...args)
      else fbq.queue.push(args)
    } as Fbq
    fbq.push = fbq
    fbq.loaded = true
    fbq.version = '2.0'
    fbq.queue = []
    window.fbq = fbq
    window._fbq = window._fbq ?? fbq
    addScript('https://connect.facebook.net/en_US/fbevents.js')
  }
  window.fbq('init', c.metaPixelId)
}

/** Loads the configured tags when allowed. Returns whether they are running. */
function ensureLoaded(): boolean {
  if (typeof window === 'undefined' || !config || !hasTags() || !adsAllowed()) return false
  loadGoogle(config)
  loadMeta(config)
  return true
}

function applyConsent() {
  const granted = adsAllowed()
  if (googleLoaded) window.gtag?.('consent', 'update', consentState(granted))
  if (metaLoaded) window.fbq?.('consent', granted ? 'grant' : 'revoke')
  if (granted) ensureLoaded()
}

// ---- events -----------------------------------------------------------------------------------------------------

export function trackPageView(path: string) {
  if (!ensureLoaded()) return
  if (googleLoaded) window.gtag?.('event', 'page_view', { page_path: path, page_location: window.location.href, page_title: document.title })
  if (metaLoaded) window.fbq?.('track', 'PageView')
}

/** A new shop started its free trial. `eventId` is shared with the server-side copy (Meta Conversions API). */
export function trackSignup(eventId: string) {
  if (!ensureLoaded()) return
  const c = config!
  window.gtag?.('event', 'sign_up', { method: 'email' })
  if (c.googleAdsId && c.googleAdsSignupLabel)
    window.gtag?.('event', 'conversion', { send_to: `${c.googleAdsId}/${c.googleAdsSignupLabel}`, transaction_id: eventId })
  if (metaLoaded) window.fbq?.('track', 'StartTrial', { value: 0, currency: 'USD' }, { eventID: eventId })
}

/** A subscription was paid for the first time. Reported once per `eventId` in this browser. */
export function trackPurchase(eventId: string, value: number, currency: string, plan: string) {
  if (alreadyReported(eventId) || !ensureLoaded()) return
  const c = config!
  window.gtag?.('event', 'purchase', { transaction_id: eventId, value, currency, items: [{ item_id: plan, item_name: plan }] })
  if (c.googleAdsId && c.googleAdsPurchaseLabel)
    window.gtag?.('event', 'conversion', { send_to: `${c.googleAdsId}/${c.googleAdsPurchaseLabel}`, value, currency, transaction_id: eventId })
  if (metaLoaded) window.fbq?.('track', 'Purchase', { value, currency }, { eventID: eventId })
  markReported(eventId)
}

function alreadyReported(eventId: string): boolean {
  try {
    return (JSON.parse(localStorage.getItem(REPORTED_KEY) ?? '[]') as string[]).includes(eventId)
  } catch {
    return false
  }
}

function markReported(eventId: string) {
  try {
    const ids = JSON.parse(localStorage.getItem(REPORTED_KEY) ?? '[]') as string[]
    localStorage.setItem(REPORTED_KEY, JSON.stringify([...ids, eventId].slice(-20)))
  } catch {
    // storage blocked: nothing to remember
  }
}

/** Test helper: forget loaded tags and state. */
export function resetTrackingForTests() {
  config = null
  visitorCountry = null
  consent = storedConsent()
  settingsOpen = false
  googleLoaded = false
  metaLoaded = false
  changed()
}
