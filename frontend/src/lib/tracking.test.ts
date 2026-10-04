import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import type { TrackingConfig } from '../api/types'
import { adsAllowed, bannerVisible, configure, consentRequired, resetTrackingForTests, setConsent, trackPageView, trackPurchase, trackSignup } from './tracking'

const TAGS: TrackingConfig = { ga4Id: 'G-TEST1', googleAdsId: 'AW-123', googleAdsSignupLabel: 'signupLbl', googleAdsPurchaseLabel: 'buyLbl', metaPixelId: '999' }

function scripts() {
  return Array.from(document.head.querySelectorAll('script')).map((s) => s.src)
}

function calls(): unknown[][] {
  return (window.dataLayer ?? []).map((a) => Array.from(a as ArrayLike<unknown>))
}

describe('ad tags and cookie consent', () => {
  beforeEach(() => {
    localStorage.clear()
    document.head.innerHTML = ''
    delete window.dataLayer
    delete window.gtag
    delete window.fbq
    delete window._fbq
    resetTrackingForTests()
  })
  afterEach(() => resetTrackingForTests())

  it('asks for opt-in in the EU, the UK and Switzerland', () => {
    expect(consentRequired('ES')).toBe(true)
    expect(consentRequired('GB')).toBe(true)
    expect(consentRequired('MX')).toBe(false)
    expect(consentRequired('AR')).toBe(false)
    expect(consentRequired(null, 'Europe/Madrid')).toBe(true)
    expect(consentRequired(null, 'America/Bogota')).toBe(false)
  })

  it('loads nothing in the EU until the visitor accepts', () => {
    configure(TAGS, 'ES')
    trackPageView('/')
    trackSignup('evt-1')
    expect(scripts()).toEqual([])
    expect(window.fbq).toBeUndefined()
    expect(bannerVisible()).toBe(true)

    setConsent('granted')
    expect(bannerVisible()).toBe(false)
    expect(scripts().some((s) => s.includes('googletagmanager.com/gtag/js?id=G-TEST1'))).toBe(true)
    expect(scripts().some((s) => s.includes('connect.facebook.net'))).toBe(true)
    expect(calls()).toContainEqual(['consent', 'default', expect.objectContaining({ ad_storage: 'granted', ad_user_data: 'granted' })])
  })

  it('loads by default elsewhere, and a rejection sends the denied consent update', () => {
    configure(TAGS, 'MX')
    expect(adsAllowed()).toBe(true)
    trackPageView('/precios')
    expect(scripts()).toHaveLength(2)

    setConsent('denied')
    expect(adsAllowed()).toBe(false)
    expect(calls()).toContainEqual(['consent', 'update', expect.objectContaining({ ad_storage: 'denied', analytics_storage: 'denied' })])
    expect(window.fbq!.queue).toContainEqual(['consent', 'revoke'])
  })

  it('reports conversions with the shared event id, and each purchase once', () => {
    configure(TAGS, 'US')
    trackSignup('evt-signup')
    expect(calls()).toContainEqual(['event', 'conversion', { send_to: 'AW-123/signupLbl', transaction_id: 'evt-signup' }])
    expect(window.fbq!.queue).toContainEqual(['track', 'StartTrial', { value: 0, currency: 'USD' }, { eventID: 'evt-signup' }])

    trackPurchase('purchase_sub_1', 45, 'USD', 'Standard')
    trackPurchase('purchase_sub_1', 45, 'USD', 'Standard')
    expect(window.fbq!.queue.filter((c) => (c as unknown[])[1] === 'Purchase')).toEqual([['track', 'Purchase', { value: 45, currency: 'USD' }, { eventID: 'purchase_sub_1' }]])
    expect(calls()).toContainEqual(['event', 'conversion', { send_to: 'AW-123/buyLbl', value: 45, currency: 'USD', transaction_id: 'purchase_sub_1' }])
  })

  it('shows no banner when no tag is configured', () => {
    configure({ ga4Id: null, googleAdsId: null, googleAdsSignupLabel: null, googleAdsPurchaseLabel: null, metaPixelId: null }, 'ES')
    expect(bannerVisible()).toBe(false)
  })
})
