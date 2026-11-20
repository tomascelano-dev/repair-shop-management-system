import { describe, expect, it } from 'vitest'
import { currencyName, detectCountry, offeredCountries, planFeatures, planPrice } from './billing'

describe('billing helpers', () => {
  it('detects the country from the browser language, among the offered ones only', () => {
    const offered = ['AR', 'MX', 'UY']
    expect(detectCountry(offered, ['es-MX', 'es'])).toBe('MX')
    expect(detectCountry(offered, ['en', 'es-UY'])).toBe('UY')
    expect(detectCountry(offered, ['es-ES'])).toBe('AR')
    expect(detectCountry(offered, ['en-US'])).toBe('AR')
    expect(detectCountry(['AR'], ['es-MX'])).toBe('AR')
    expect(detectCountry(offered, [])).toBe('AR')
  })

  it('offers Argentina until the API lists the enabled countries', () => {
    expect(offeredCountries(undefined)).toEqual(['AR'])
    expect(offeredCountries({ countries: [] })).toEqual(['AR'])
    expect(offeredCountries({ countries: [{ code: 'AR', currency: 'ARS' }, { code: 'MX', currency: 'MXN' }] })).toEqual(['AR', 'MX'])
  })

  it('names the currency charged in each country', () => {
    expect(currencyName('ARS')).toBe('pesos argentinos')
    expect(currencyName('MXN', 'en')).toBe('Mexican pesos')
    expect(currencyName('XYZ')).toBe('XYZ')
  })

  it('lists branches and extra modules per plan', () => {
    expect(planFeatures({ id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: 29900, currency: 'ARS' })).toEqual(['1 sucursal'])
    expect(planFeatures({ id: 'Standard', name: 'Estándar', maxBranches: 2, modules: ['reports'], price: 44900, currency: 'ARS' })).toEqual([
      'Hasta 2 sucursales',
      'Reportes detallados y Excel',
    ])
  })

  it('formats prices without decimals in the plan currency', () => {
    expect(planPrice({ id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: 29900, currency: 'ARS' })).toMatch(/29\.900/)
    expect(planPrice({ id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: 449, currency: 'MXN' })).toMatch(/449/)
    expect(planPrice({ id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: null, currency: 'ARS' })).toBe('Consultar')
  })
})
