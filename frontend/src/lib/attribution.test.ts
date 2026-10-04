import { beforeEach, describe, expect, it } from 'vitest'
import { captureAttribution, readAttribution, signupAttribution } from './attribution'

const NOW = 1_800_000_000_000

describe('campaign attribution', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
    document.cookie = '_fbp=; Max-Age=0'
    document.cookie = '_fbc=; Max-Age=0'
  })

  it('keeps the campaign parameters and click ids of the landing URL', () => {
    captureAttribution(new URL('https://app.techxto.ar/?utm_source=google&utm_campaign=talleres-mx&gclid=abc'), 'https://www.google.com/', false, NOW)
    expect(readAttribution(NOW)).toMatchObject({ utmSource: 'google', utmCampaign: 'talleres-mx', gclid: 'abc', landingPath: '/', referrer: 'https://www.google.com/' })
    expect(localStorage.getItem('rs.attribution')).toBeNull()
  })

  it('remembers it for later visits only when ad cookies are allowed', () => {
    captureAttribution(new URL('https://app.techxto.ar/en?fbclid=xyz'), '', true, NOW)
    sessionStorage.clear()
    expect(readAttribution(NOW + 1000)?.fbclid).toBe('xyz')
    expect(readAttribution(NOW + 31 * 24 * 3600 * 1000)).toBeNull()
  })

  it('does not let an untagged visit from another site replace a campaign', () => {
    captureAttribution(new URL('https://app.techxto.ar/?utm_source=meta'), '', false, NOW)
    expect(captureAttribution(new URL('https://app.techxto.ar/precios'), 'https://blog.example.com/post', false, NOW + 1)).toBeNull()
    expect(captureAttribution(new URL('https://app.techxto.ar/precios'), 'https://app.techxto.ar/', false, NOW + 2)).toBeNull()
    expect(readAttribution(NOW + 3)?.utmSource).toBe('meta')
  })

  it('sends the Meta cookies only with consent, and builds _fbc from the click id', () => {
    captureAttribution(new URL('https://app.techxto.ar/?fbclid=click1'), '', false, NOW)
    document.cookie = '_fbp=fb.1.123.456'
    const allowed = signupAttribution(true)
    expect(allowed).toMatchObject({ fbclid: 'click1', fbp: 'fb.1.123.456', fbc: `fb.1.${NOW}.click1`, adConsent: true })
    expect(allowed.eventId).toBeTruthy()

    const denied = signupAttribution(false)
    expect(denied.fbp).toBeUndefined()
    expect(denied.fbc).toBeUndefined()
    expect(denied.fbclid).toBe('click1')
    expect(denied.eventId).not.toBe(allowed.eventId)
  })
})
