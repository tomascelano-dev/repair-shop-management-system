import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { TrackingConfig } from '../api/types'

const config: TrackingConfig = { ga4Id: 'G-TEST', googleAdsId: 'AW-123', googleAdsSignupLabel: 'signup', googleAdsPurchaseLabel: 'paid', metaPixelId: '12345' }

beforeEach(() => {
  vi.resetModules()
  localStorage.clear()
  sessionStorage.clear()
  document.head.querySelectorAll('script').forEach((s) => s.remove())
  delete window.gtag
  delete window.dataLayer
  delete window.fbq
  delete window._fbq
  history.replaceState(null, '', '/')
})

describe('consent and campaign measurement', () => {
  it('loads no trackers or persistent attribution until explicit consent, including rejection', async () => {
    const m = await import('./marketing')
    m.captureCampaign('https://app.example/en?utm_source=google&gclid=click')
    m.configureTracking(config)
    expect(document.querySelectorAll('script')).toHaveLength(0)
    expect(sessionStorage.length).toBe(0)
    expect(m.signupAttribution()).toEqual({ adConsent: false })
    m.setConsent('denied')
    m.trackPurchase('purchase_sub', 25, 'USD')
    expect(document.querySelectorAll('script')).toHaveLength(0)
    expect(window.fbq).toBeUndefined()
  })

  it('preserves first-touch attribution after opt-in without persisting unrelated URL parameters', async () => {
    const m = await import('./marketing')
    m.captureCampaign('https://app.example/en?utm_source=google&utm_campaign=launch&gclid=click&token=private', 'https://search.example/path?private=123')
    m.setConsent('granted')
    m.captureCampaign('https://app.example/en/signup?utm_source=other')
    const a = m.signupAttribution()
    expect(a).toMatchObject({ utmSource: 'google', utmCampaign: 'launch', gclid: 'click', landingPath: '/en', referrer: 'https://search.example', adConsent: true })
    expect(a.eventId).toMatch(/^trial_/)
    expect(sessionStorage.getItem('rs_campaign_v1')).not.toContain('private')
  })

  it('sets all Consent Mode v2 signals before loading and shares conversion IDs with Meta', async () => {
    const m = await import('./marketing')
    m.setConsent('granted')
    m.configureTracking(config)
    m.configureTracking(config)
    expect(document.querySelectorAll('script')).toHaveLength(2)
    const commands = window.dataLayer!.map((x) => Array.from(x as ArrayLike<unknown>))
    expect(commands[0]).toEqual(['consent', 'default', { ad_storage: 'denied', analytics_storage: 'denied', ad_user_data: 'denied', ad_personalization: 'denied' }])
    m.trackTrial({ adConsent: true, eventId: 'trial_test' })
    m.trackPurchase('purchase_sub_test', 25, 'USD')
    m.trackPurchase('purchase_sub_test', 25, 'USD')
    expect(window.fbq!.queue.filter((c) => c[1] === 'Purchase')).toEqual([
      ['track', 'Purchase', { value: 25, currency: 'USD' }, { eventID: 'purchase_sub_test' }],
    ])
    expect(window.fbq!.queue).toContainEqual(['track', 'StartTrial', { value: 0, currency: 'USD' }, { eventID: 'trial_test' }])
  })

  it('revokes future events, removes campaign storage and does not measure private app routes', async () => {
    const m = await import('./marketing')
    m.setConsent('granted')
    m.configureTracking(config)
    m.trackPage('/orders/private-order')
    expect(window.fbq!.queue.some((c) => c[1] === 'PageView')).toBe(false)
    m.setConsent('denied')
    const count = window.fbq!.queue.length
    m.trackTrial({ adConsent: true, eventId: 'trial_late' })
    expect(window.fbq!.queue.length).toBe(count)
    expect(window.fbq!.queue.at(-1)).toEqual(['consent', 'revoke'])
    expect(sessionStorage.getItem('rs_campaign_v1')).toBeNull()
  })

  it('honors withdrawal even when an earlier grant cannot be overwritten in storage', async () => {
    const m = await import('./marketing')
    m.setConsent('granted')
    m.configureTracking(config)
    const write = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('Storage unavailable') })
    m.setConsent('denied')
    write.mockRestore()
    expect(localStorage.getItem('rs_cookie_consent_v1')).toBe('granted')
    expect(m.getConsent()).toBe('denied')
    m.configureTracking(config)
    const count = window.fbq!.queue.length
    m.trackPurchase('purchase_after_withdrawal', 25, 'USD')
    expect(window.fbq!.queue.length).toBe(count)
    expect(m.signupAttribution()).toEqual({ adConsent: false })
  })
})
