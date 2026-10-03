import { describe, expect, it } from 'vitest'
import { cartTotals, type CartLine } from './cart'

function line(partial: Partial<CartLine>): CartLine {
  return { key: Math.random().toString(), itemId: 'i', name: 'Item', sku: 'SKU', unitPrice: 100, quantity: 1, discount: 0, trackStock: true, available: 10, ...partial }
}

describe('cartTotals', () => {
  it('adds up lines with quantity and per-line discounts', () => {
    const t = cartTotals([line({ unitPrice: 1500, quantity: 2, discount: 200 }), line({ unitPrice: 999.99 })], 0)
    expect(t.subtotal).toBe(3799.99)
    expect(t.total).toBe(3799.99)
  })

  it('applies a global discount but never below zero', () => {
    expect(cartTotals([line({ unitPrice: 100 })], 30)).toEqual({ subtotal: 100, discount: 30, total: 70 })
    expect(cartTotals([line({ unitPrice: 100 })], 500)).toEqual({ subtotal: 100, discount: 100, total: 0 })
  })

  it('does not let a line discount make the line negative', () => {
    expect(cartTotals([line({ unitPrice: 50, discount: 80 })], 0).total).toBe(0)
  })

  it('rounds to cents', () => {
    expect(cartTotals([line({ unitPrice: 0.1, quantity: 3 })], 0).total).toBe(0.3)
  })
})
