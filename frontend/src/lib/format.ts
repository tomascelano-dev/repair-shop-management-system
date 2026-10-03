// Formatting for Argentina (es-AR): "$ 1.234,56", "03/10/2026 14:05".

export const TIME_ZONE = 'America/Argentina/Buenos_Aires'

const moneyFormatters = new Map<string, Intl.NumberFormat>()

export function money(amount: number | null | undefined, currency = 'ARS'): string {
  if (amount === null || amount === undefined || Number.isNaN(amount)) return '—'
  const key = currency || 'ARS'
  let f = moneyFormatters.get(key)
  if (!f) {
    try {
      f = new Intl.NumberFormat('es-AR', { style: 'currency', currency: key, minimumFractionDigits: 2, maximumFractionDigits: 2 })
    } catch {
      f = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    }
    moneyFormatters.set(key, f)
  }
  return f.format(amount)
}

export function number(value: number | null | undefined, digits = 0): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—'
  return new Intl.NumberFormat('es-AR', { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value)
}

export function percent(value: number | null | undefined, digits = 0): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—'
  return new Intl.NumberFormat('es-AR', { style: 'percent', minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value)
}

export function amounts(list: { currency: string; amount: number }[] | null | undefined): string {
  if (!list || list.length === 0) return money(0)
  return list.map((x) => money(x.amount, x.currency)).join(' + ')
}

function toDate(value: string | Date | null | undefined): Date | null {
  if (!value) return null
  const d = value instanceof Date ? value : new Date(value)
  return Number.isNaN(d.getTime()) ? null : d
}

export function dateTime(value: string | Date | null | undefined): string {
  const d = toDate(value)
  if (!d) return '—'
  return new Intl.DateTimeFormat('es-AR', { timeZone: TIME_ZONE, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(d)
}

export function date(value: string | Date | null | undefined): string {
  const d = toDate(value)
  if (!d) return '—'
  return new Intl.DateTimeFormat('es-AR', { timeZone: TIME_ZONE, day: '2-digit', month: '2-digit', year: 'numeric' }).format(d)
}

export function time(value: string | Date | null | undefined): string {
  const d = toDate(value)
  if (!d) return '—'
  return new Intl.DateTimeFormat('es-AR', { timeZone: TIME_ZONE, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(d)
}

/** DateOnly values from the API ("2026-10-03") without timezone shifts. */
export function dateOnly(value: string | null | undefined): string {
  if (!value) return '—'
  const [y, m, d] = value.split('-')
  return `${d}/${m}/${y}`
}

export function relative(value: string | Date | null | undefined, now = new Date()): string {
  const d = toDate(value)
  if (!d) return '—'
  const diffMs = d.getTime() - now.getTime()
  const abs = Math.abs(diffMs)
  const rtf = new Intl.RelativeTimeFormat('es-AR', { numeric: 'auto' })
  const minutes = Math.round(diffMs / 60000)
  if (abs < 3600000) return rtf.format(minutes, 'minute')
  const hours = Math.round(diffMs / 3600000)
  if (abs < 86400000) return rtf.format(hours, 'hour')
  const days = Math.round(diffMs / 86400000)
  if (Math.abs(days) < 30) return rtf.format(days, 'day')
  return date(d)
}

/** Value for <input type="datetime-local"> in the shop time zone (approximation using the browser zone). */
export function toLocalInput(value: string | null | undefined): string {
  const d = toDate(value)
  if (!d) return ''
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function fromLocalInput(value: string): string | null {
  if (!value) return null
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? null : d.toISOString()
}

export function todayIso(offsetDays = 0): string {
  const d = new Date()
  d.setDate(d.getDate() + offsetDays)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** Parses "1.234,56" / "1234.56" / "1234,5" typed by users. */
export function parseAmount(value: string): number | null {
  const s = value.trim().replace(/[^\d.,-]/g, '')
  if (!s) return null
  const lastComma = s.lastIndexOf(',')
  const lastDot = s.lastIndexOf('.')
  let normalized = s
  if (lastComma > -1 && lastDot > -1) normalized = lastComma > lastDot ? s.replace(/\./g, '').replace(',', '.') : s.replace(/,/g, '')
  else if (lastComma > -1) normalized = s.replace(/\./g, '').replace(',', '.')
  else if ((s.match(/\./g) ?? []).length > 1) normalized = s.replace(/\./g, '')
  // es-AR: "2.000" / "15.500" are thousands, not decimals ("1.5" and "0.250" stay decimals).
  else if (/^-?[1-9]\d{0,2}\.\d{3}$/.test(s)) normalized = s.replace('.', '')
  const n = Number(normalized)
  return Number.isFinite(n) ? Math.round(n * 100) / 100 : null
}

export function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toUpperCase())
    .join('')
}
