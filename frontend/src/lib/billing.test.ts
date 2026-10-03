import { describe, expect, it } from 'vitest'
import { billingCountry, detectCountry, planFeatures, planPrice } from './billing'

describe('billing helpers', () => {
  it('detects the country from the browser language', () => {
    expect(detectCountry(['es-MX', 'es'])).toBe('MX')
    expect(detectCountry(['en', 'es-ES'])).toBe('ES')
    expect(detectCountry(['fr-FR'])).toBe('AR')
    expect(detectCountry([])).toBe('AR')
  })

  it('bills "other country" like any country outside Argentina', () => {
    expect(billingCountry('OT')).toBe('US')
    expect(billingCountry('AR')).toBe('AR')
  })

  it('lists branches and extra modules per plan', () => {
    expect(planFeatures({ id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: 25, currency: 'USD' })).toEqual(['1 sucursal'])
    expect(planFeatures({ id: 'Standard', name: 'Estándar', maxBranches: 2, modules: ['reports'], price: 45, currency: 'USD' })).toEqual([
      'Hasta 2 sucursales',
      'Reportes detallados y Excel',
    ])
  })

  it('formats prices without decimals in the plan currency', () => {
    expect(planPrice({ id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: 25, currency: 'USD' })).toBe('$25')
    expect(planPrice({ id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: 29900, currency: 'ARS' })).toMatch(/29\.900/)
    expect(planPrice({ id: 'Basic', name: 'Básico', maxBranches: 1, modules: [], price: null, currency: 'USD' })).toBe('Consultar')
  })
})
