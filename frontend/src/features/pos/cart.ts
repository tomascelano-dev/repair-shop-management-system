export interface CartLine {
  key: string
  itemId: string | null
  name: string
  sku: string
  unitPrice: number
  quantity: number
  discount: number // amount per line
  trackStock: boolean
  available: number | null
}

export const round2 = (n: number) => Math.round(n * 100) / 100

export function cartTotals(lines: CartLine[], globalDiscount: number) {
  const subtotal = round2(lines.reduce((s, l) => s + Math.max(0, round2(l.unitPrice * l.quantity) - l.discount), 0))
  const discount = Math.min(globalDiscount, subtotal)
  return { subtotal, discount, total: round2(subtotal - discount) }
}
