import { describe, expect, it } from 'vitest'
import { amounts, dateOnly, money, parseAmount, percent } from './format'

describe('parseAmount', () => {
  it.each([
    ['1.234,56', 1234.56],
    ['1234.56', 1234.56],
    ['1234,5', 1234.5],
    ['1.234.567', 1234567],
    ['1,234.50', 1234.5],
    ['$ 2.000', 2000],
    [' 15 ', 15],
    ['0,005', 0.01],
    ['15.500', 15500],
    ['1.5', 1.5],
    ['0.250', 0.25],
  ])('parses %s', (input, expected) => {
    expect(parseAmount(input)).toBe(expected)
  })

  it('returns null for empty or invalid input', () => {
    expect(parseAmount('')).toBeNull()
    expect(parseAmount('abc')).toBeNull()
  })
})

describe('money', () => {
  it('formats pesos the Argentine way', () => {
    expect(money(1234.5)).toMatch(/1\.234,50/)
    expect(money(1234.5)).toContain('$')
  })

  it('renders a dash for missing values', () => {
    expect(money(null)).toBe('—')
    expect(money(undefined)).toBe('—')
  })

  it('keeps the currency', () => {
    expect(money(10, 'USD')).toMatch(/US\$|USD/)
  })
})

describe('other formatters', () => {
  it('formats date-only values without timezone shifts', () => {
    expect(dateOnly('2026-10-03')).toBe('03/10/2026')
  })

  it('joins amounts in several currencies', () => {
    expect(amounts([{ currency: 'ARS', amount: 100 }, { currency: 'USD', amount: 5 }])).toContain(' + ')
    expect(amounts([])).toMatch(/0,00/)
  })

  it('formats percentages', () => {
    expect(percent(0.256)).toMatch(/26\s?%/)
    expect(percent(null)).toBe('—')
  })
})
