// Remembers where a visitor came from (UTM parameters, Google and Meta click ids, external referrer) so the
// signup can be attributed to the campaign. Kept for the visit in sessionStorage, and for 30 days in
// localStorage when ad cookies are allowed.

import type { SignupAttributionInput } from '../api/types'

const KEY = 'rs.attribution'
const MAX_AGE_MS = 30 * 24 * 60 * 60 * 1000

const PARAMS = {
  utm_source: 'utmSource',
  utm_medium: 'utmMedium',
  utm_campaign: 'utmCampaign',
  utm_term: 'utmTerm',
  utm_content: 'utmContent',
  gclid: 'gclid',
  gbraid: 'gbraid',
  wbraid: 'wbraid',
  fbclid: 'fbclid',
} as const

type Field = (typeof PARAMS)[keyof typeof PARAMS]

export interface StoredAttribution extends Partial<Record<Field, string>> {
  landingPath?: string
  referrer?: string
  at: number
}

function externalReferrer(referrer: string, host: string): string | undefined {
  if (!referrer) return undefined
  try {
    return new URL(referrer).host === host ? undefined : referrer.slice(0, 500)
  } catch {
    return undefined
  }
}

/**
 * Reads the landing URL. A visit with campaign parameters replaces what was stored (last click wins); a visit
 * from another site without parameters is only stored when there is nothing else.
 */
export function captureAttribution(url: URL, referrer: string, persist: boolean, now = Date.now()): StoredAttribution | null {
  const found: StoredAttribution = { at: now }
  let tagged = false
  for (const [param, field] of Object.entries(PARAMS) as [keyof typeof PARAMS, Field][]) {
    const value = url.searchParams.get(param)?.trim()
    if (value) {
      found[field] = value.slice(0, 300)
      tagged = true
    }
  }
  const external = externalReferrer(referrer, url.host)
  if (!tagged && (!external || readAttribution(now))) return null

  found.landingPath = url.pathname.slice(0, 300)
  if (external) found.referrer = external
  write(found, persist)
  return found
}

function write(value: StoredAttribution, persist: boolean) {
  const json = JSON.stringify(value)
  try {
    sessionStorage.setItem(KEY, json)
  } catch {
    // storage blocked
  }
  try {
    if (persist) localStorage.setItem(KEY, json)
    else localStorage.removeItem(KEY)
  } catch {
    // storage blocked
  }
}

export function readAttribution(now = Date.now()): StoredAttribution | null {
  for (const store of [sessionStorage, localStorage]) {
    try {
      const raw = store.getItem(KEY)
      if (!raw) continue
      const value = JSON.parse(raw) as StoredAttribution
      if (typeof value.at === 'number' && now - value.at < MAX_AGE_MS) return value
    } catch {
      // ignore unreadable entries
    }
  }
  return null
}

function cookie(name: string): string | undefined {
  const match = document.cookie.split('; ').find((c) => c.startsWith(`${name}=`))
  return match ? decodeURIComponent(match.slice(name.length + 1)) : undefined
}

function newEventId(): string {
  try {
    return crypto.randomUUID()
  } catch {
    return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`
  }
}

/** What the signup sends to the API: the stored campaign data, the Meta pixel cookies and the consent. */
export function signupAttribution(adConsent: boolean): SignupAttributionInput {
  const stored = readAttribution()
  const { at, ...fields } = stored ?? { at: Date.now() }
  const fbclid = fields.fbclid
  return {
    ...fields,
    fbp: adConsent ? cookie('_fbp') : undefined,
    // The pixel writes _fbc from ?fbclid; build it the same way when the pixel did not run yet.
    fbc: adConsent ? cookie('_fbc') ?? (fbclid ? `fb.1.${at}.${fbclid}` : undefined) : undefined,
    adConsent,
    eventId: newEventId(),
  }
}
