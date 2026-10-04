import { describe, expect, it } from 'vitest'
import { alternatePath, langFromPath, publicPageOf } from './lang'

describe('public website languages', () => {
  it('reads the language from the path', () => {
    expect(langFromPath('/')).toBe('es')
    expect(langFromPath('/precios')).toBe('es')
    expect(langFromPath('/en')).toBe('en')
    expect(langFromPath('/en/pricing')).toBe('en')
    expect(langFromPath('/entrada')).toBe('es')
  })

  it('links each page to the same page in the other language', () => {
    expect(alternatePath('/precios', 'en')).toBe('/en/pricing')
    expect(alternatePath('/en/refunds', 'es')).toBe('/reembolsos')
    expect(alternatePath('/en', 'es')).toBe('/')
    expect(alternatePath('/login', 'en')).toBe('/en')
  })

  it('only treats the website pages as public', () => {
    expect(publicPageOf('/registro')).toBe('signup')
    expect(publicPageOf('/en/terms/')).toBe('terms')
    expect(publicPageOf('/orders')).toBeNull()
  })
})
