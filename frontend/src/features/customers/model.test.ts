import { describe, expect, it } from 'vitest'
import { customerToInput, emptyCustomer, isValidImei } from './model'
import type { Customer } from '../../api/types'

describe('isValidImei', () => {
  it('accepts a valid IMEI (Luhn)', () => {
    expect(isValidImei('356938035643809')).toBe(true)
    expect(isValidImei('35-693803-564380-9')).toBe(true)
  })

  it('rejects wrong check digits and lengths', () => {
    expect(isValidImei('356938035643808')).toBe(false)
    expect(isValidImei('35693803564380')).toBe(false)
    expect(isValidImei('')).toBe(false)
  })
})

describe('customer mapping', () => {
  it('starts with notifications enabled and marketing disabled', () => {
    const c = emptyCustomer()
    expect(c.notificationsOptIn).toBe(true)
    expect(c.marketingOptIn).toBe(false)
  })

  it('maps nulls to empty strings for the form', () => {
    const customer: Customer = {
      id: '1',
      shopId: 's',
      fullName: 'Ana Pérez',
      phone: '1155550000',
      createdAtUtc: '2026-01-01T00:00:00Z',
      documentType: 'Dni',
      taxCondition: 'ConsumidorFinal',
      notificationsOptIn: false,
      marketingOptIn: true,
      email: null,
      notes: null,
    }
    const input = customerToInput(customer)
    expect(input.email).toBe('')
    expect(input.notes).toBe('')
    expect(input.documentType).toBe('Dni')
    expect(input.marketingOptIn).toBe(true)
  })
})
