import { useCallback, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { cashApi, inventoryApi, salesApi } from '../../api/endpoints'
import { errorMessage, newIdempotencyKey } from '../../api/http'
import type { Customer, PaymentMethod, PosCatalogItem, Sale } from '../../api/types'
import { CameraScanner, CustomerSearch } from '../../components/domain'
import { Alert, Badge, Button, Card, Field, Input, Modal, SearchInput, Select } from '../../components/ui'
import { cn } from '../../lib/cn'
import { money, parseAmount, relative } from '../../lib/format'
import { openPdf, printPdf } from '../../lib/files'
import { useDebounced, useHotkey, useLocalState } from '../../lib/hooks'
import { PAYMENT_METHOD } from '../../lib/labels'
import { cameraScanSupported } from '../../lib/scanner'
import { useShopSettings } from '../../lib/shop'
import { InvoiceDialog } from '../orders/panels/PaymentsPanel'
import { cartTotals, round2, type CartLine } from './cart'

interface HeldSale {
  id: string
  label: string
  createdAt: string
  lines: CartLine[]
  customer: Pick<Customer, 'id' | 'fullName'> | null
  discount: number
}

interface PaymentRow {
  method: PaymentMethod
  amountText: string
  reference: string
}

export function PosPage() {
  const queryClient = useQueryClient()
  const searchRef = useRef<HTMLInputElement | null>(null)
  const [q, setQ] = useState('')
  const [category, setCategory] = useState('')
  const debounced = useDebounced(q.trim(), 200)
  const [lines, setLines] = useState<CartLine[]>([])
  const [customer, setCustomer] = useState<Pick<Customer, 'id' | 'fullName'> | null>(null)
  const [discountText, setDiscountText] = useState('')
  const [discountMode, setDiscountMode] = useState<'amount' | 'percent'>('amount')
  const [held, setHeld] = useLocalState<HeldSale[]>('rs.pos.held', [])
  const [paying, setPaying] = useState(false)
  const [scanning, setScanning] = useState(false)
  const [freeItem, setFreeItem] = useState(false)
  const [done, setDone] = useState<Sale | null>(null)
  const [pickingCustomer, setPickingCustomer] = useState(false)

  const catalog = useQuery({ queryKey: ['pos', 'catalog', debounced], queryFn: () => salesApi.catalog(debounced || undefined, 60), staleTime: 30_000 })
  const cash = useQuery({ queryKey: ['cash', 'current'], queryFn: cashApi.current, staleTime: 30_000 })
  const settings = useShopSettings()
  // With "require an open register" every payment (any method) belongs to a cash session.
  const requireCash = settings.data?.requireOpenCashSession ?? true

  const categories = useMemo(() => Array.from(new Set((catalog.data ?? []).map((i) => i.category).filter(Boolean) as string[])).sort(), [catalog.data])
  const visible = (catalog.data ?? []).filter((i) => !category || i.category === category)

  const subtotalBeforeGlobal = cartTotals(lines, 0).subtotal
  const globalDiscount = useMemo(() => {
    const v = parseAmount(discountText) ?? 0
    return discountMode === 'percent' ? round2((subtotalBeforeGlobal * Math.min(100, v)) / 100) : v
  }, [discountText, discountMode, subtotalBeforeGlobal])
  const totals = cartTotals(lines, globalDiscount)

  const addItem = useCallback((item: PosCatalogItem) => {
    if (item.salePrice === null || item.salePrice === undefined) {
      toast.error(`“${item.name}” no tiene precio de venta`)
      return
    }
    setLines((prev) => {
      const existing = prev.find((l) => l.itemId === item.id)
      if (existing) {
        if (item.trackStock && existing.quantity + 1 > item.available) toast.warning(`Solo hay ${item.available} de “${item.name}”`)
        return prev.map((l) => (l.key === existing.key ? { ...l, quantity: l.quantity + 1 } : l))
      }
      if (item.trackStock && item.available <= 0) toast.warning(`“${item.name}” figura sin stock`)
      return [...prev, { key: newIdempotencyKey(), itemId: item.id, name: item.name, sku: item.sku, unitPrice: item.salePrice!, quantity: 1, discount: 0, trackStock: item.trackStock, available: item.available }]
    })
  }, [])

  // Exact lookup by barcode/SKU on the server (also finds items outside the first catalog page).
  async function addByCode(code: string): Promise<boolean> {
    try {
      const item = await inventoryApi.byCode(code)
      if (!item.isSellable || !item.isActive) {
        toast.error(`“${item.name}” no está habilitado para la venta`)
        return true
      }
      addItem({ id: item.id, sku: item.sku, barcode: item.barcode, name: item.name, category: item.category, salePrice: item.salePrice, currency: item.salePriceCurrency, trackStock: item.trackStock, available: item.availableQuantity, warrantyDays: item.warrantyDays })
      setQ('')
      return true
    } catch {
      return false
    }
  }

  // Barcode scanners type the code and press Enter: exact match first, then the single search result.
  async function onEnter() {
    const code = q.trim()
    if (!code) return
    const exact = (catalog.data ?? []).find((i) => i.barcode === code || i.sku.toLowerCase() === code.toLowerCase())
    if (exact) {
      addItem(exact)
      setQ('')
      return
    }
    if (await addByCode(code)) return
    if (visible.length === 1) {
      addItem(visible[0]!)
      setQ('')
    } else toast.error(`No se encontró el código “${code}”`)
  }

  const onCameraCode = useCallback(
    (code: string) => {
      setScanning(false)
      void addByCode(code).then((found) => {
        if (!found) toast.error(`No se encontró el código “${code}”`)
      })
    },
    // addByCode only uses stable setters and addItem
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [addItem]
  )

  function clearSale() {
    setLines([])
    setCustomer(null)
    setDiscountText('')
  }

  function hold() {
    if (lines.length === 0) return
    const label = customer?.fullName ?? `Venta en espera ${held.length + 1}`
    setHeld((prev) => [...prev, { id: newIdempotencyKey(), label, createdAt: new Date().toISOString(), lines, customer, discount: globalDiscount }])
    clearSale()
    toast.success('Venta puesta en espera')
  }

  function resume(h: HeldSale) {
    if (lines.length > 0) hold()
    setLines(h.lines)
    setCustomer(h.customer)
    setDiscountMode('amount')
    setDiscountText(h.discount ? String(h.discount).replace('.', ',') : '')
    setHeld((prev) => prev.filter((x) => x.id !== h.id))
  }

  useHotkey('F2', (e) => {
    e.preventDefault()
    searchRef.current?.focus()
  }, true)
  useHotkey('F4', (e) => {
    e.preventDefault()
    if (lines.length > 0 && totals.total >= 0) setPaying(true)
  }, true)
  useHotkey('F8', (e) => {
    e.preventDefault()
    hold()
  }, true)

  const cashClosed = cash.isSuccess && !cash.data

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_420px]">
      <div className="min-w-0 space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h1 className="text-xl font-semibold text-slate-900">Punto de venta</h1>
          <div className="flex flex-wrap gap-2 text-xs text-slate-500">
            <kbd className="rounded border bg-white px-1.5">F2</kbd> buscar <kbd className="rounded border bg-white px-1.5">F4</kbd> cobrar <kbd className="rounded border bg-white px-1.5">F8</kbd> en espera
          </div>
        </div>

        {cashClosed && requireCash ? (
          <Alert tone="amber" title="La caja está cerrada">
            Abrila para poder cobrar. <Link to="/cash" className="font-medium underline">Abrir caja</Link>
          </Alert>
        ) : null}

        <Card>
          <div className="flex gap-2">
            <SearchInput inputRef={searchRef} value={q} onChange={setQ} onEnter={() => void onEnter()} placeholder="Escaneá o buscá por nombre, SKU o código de barras…" className="flex-1" autoFocus />
            {cameraScanSupported() ? (
              <Button onClick={() => setScanning(true)} aria-label="Escanear con la cámara">
                📷
              </Button>
            ) : null}
            <Button onClick={() => setFreeItem(true)}>+ Ítem libre</Button>
          </div>
          {categories.length > 0 ? (
            <div className="mt-3 flex flex-wrap gap-1.5">
              <button type="button" onClick={() => setCategory('')} className={cn('rounded-full border px-3 py-1 text-xs', !category ? 'border-brand-500 bg-brand-50 text-brand-700' : 'border-slate-200 text-slate-600')}>
                Todo
              </button>
              {categories.map((c) => (
                <button key={c} type="button" onClick={() => setCategory(c)} className={cn('rounded-full border px-3 py-1 text-xs', category === c ? 'border-brand-500 bg-brand-50 text-brand-700' : 'border-slate-200 text-slate-600')}>
                  {c}
                </button>
              ))}
            </div>
          ) : null}
        </Card>

        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-4">
          {visible.map((i) => (
            <button
              key={i.id}
              type="button"
              onClick={() => addItem(i)}
              className="flex flex-col rounded-xl border border-slate-200 bg-white p-3 text-left shadow-sm transition hover:border-brand-400 hover:shadow focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500"
            >
              <span className="line-clamp-2 text-sm font-medium text-slate-800">{i.name}</span>
              <span className="mt-0.5 text-xs text-slate-400">{i.sku}</span>
              <span className="mt-auto pt-2">
                <span className="block text-base font-semibold tabular-nums text-slate-900">{money(i.salePrice, i.currency ?? 'ARS')}</span>
                {i.trackStock ? (
                  <span className={cn('block whitespace-nowrap text-xs', i.available <= 0 ? 'text-rose-600' : i.available <= 3 ? 'text-amber-600' : 'text-slate-500')}>
                    {i.available <= 0 ? 'Sin stock' : `Stock: ${i.available}`}
                  </span>
                ) : null}
              </span>
            </button>
          ))}
          {catalog.isSuccess && visible.length === 0 ? <p className="col-span-full py-8 text-center text-sm text-slate-500">No hay productos a la venta con ese criterio. Marcá ítems como “vendible” en Inventario.</p> : null}
        </div>

        {held.length > 0 ? (
          <Card title="Ventas en espera">
            <ul className="divide-y divide-slate-100 text-sm">
              {held.map((h) => (
                <li key={h.id} className="flex items-center justify-between py-2">
                  <span>
                    {h.label} · {h.lines.length} ítem{h.lines.length === 1 ? '' : 's'} · {money(cartTotals(h.lines, h.discount).total)}
                    <span className="ml-2 text-xs text-slate-400">{relative(h.createdAt)}</span>
                  </span>
                  <span className="flex gap-1">
                    <Button size="sm" onClick={() => resume(h)}>
                      Retomar
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => setHeld((prev) => prev.filter((x) => x.id !== h.id))}>
                      Descartar
                    </Button>
                  </span>
                </li>
              ))}
            </ul>
          </Card>
        ) : null}
      </div>

      <aside className="lg:sticky lg:top-20 lg:self-start">
        <Card padded={false} className="flex flex-col">
          <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
            <div className="min-w-0 text-sm">
              {customer ? (
                <span className="flex items-center gap-2">
                  <span className="truncate font-medium">{customer.fullName}</span>
                  <button type="button" className="text-xs text-slate-500 hover:underline" onClick={() => setCustomer(null)}>
                    quitar
                  </button>
                </span>
              ) : (
                <button type="button" className="text-brand-700 hover:underline" onClick={() => setPickingCustomer(true)}>
                  + Asignar cliente (opcional)
                </button>
              )}
            </div>
            <div className="flex gap-1">
              <Button size="sm" variant="ghost" disabled={lines.length === 0} onClick={hold}>
                En espera
              </Button>
              <Button size="sm" variant="ghost" disabled={lines.length === 0} onClick={clearSale}>
                Vaciar
              </Button>
            </div>
          </div>

          <ul className="max-h-[45vh] divide-y divide-slate-100 overflow-y-auto" aria-label="Carrito">
            {lines.length === 0 ? <li className="px-4 py-10 text-center text-sm text-slate-500">Escaneá un producto o tocá una tarjeta para agregarlo.</li> : null}
            {lines.map((l) => (
              <li key={l.key} className="px-4 py-3 text-sm">
                <div className="flex items-start justify-between gap-2">
                  <span className="min-w-0">
                    <span className="block truncate font-medium text-slate-800">{l.name}</span>
                    <span className="text-xs text-slate-500">
                      {money(l.unitPrice)} c/u{l.trackStock && l.available !== null && l.quantity > l.available ? <Badge tone="rose" className="ml-1">stock {l.available}</Badge> : null}
                    </span>
                  </span>
                  <span className="shrink-0 font-semibold tabular-nums">{money(Math.max(0, round2(l.unitPrice * l.quantity) - l.discount))}</span>
                </div>
                <div className="mt-2 flex items-center gap-2">
                  <div className="flex items-center rounded-lg border border-slate-200">
                    <button type="button" className="px-2.5 py-1 text-slate-600 hover:bg-slate-50" aria-label="Restar" onClick={() => setLines((p) => p.map((x) => (x.key === l.key ? { ...x, quantity: Math.max(1, x.quantity - 1) } : x)))}>
                      −
                    </button>
                    <input
                      className="w-12 border-x border-slate-200 py-1 text-center tabular-nums outline-none"
                      inputMode="numeric"
                      value={l.quantity}
                      aria-label="Cantidad"
                      onChange={(e) => {
                        const n = Math.max(1, Math.min(999, Number(e.target.value.replace(/\D/g, '')) || 1))
                        setLines((p) => p.map((x) => (x.key === l.key ? { ...x, quantity: n } : x)))
                      }}
                    />
                    <button type="button" className="px-2.5 py-1 text-slate-600 hover:bg-slate-50" aria-label="Sumar" onClick={() => setLines((p) => p.map((x) => (x.key === l.key ? { ...x, quantity: x.quantity + 1 } : x)))}>
                      +
                    </button>
                  </div>
                  <LineDiscount line={l} onChange={(discount) => setLines((p) => p.map((x) => (x.key === l.key ? { ...x, discount } : x)))} />
                  <button type="button" className="ml-auto text-xs text-rose-600 hover:underline" onClick={() => setLines((p) => p.filter((x) => x.key !== l.key))}>
                    Quitar
                  </button>
                </div>
              </li>
            ))}
          </ul>

          <div className="space-y-2 border-t border-slate-100 px-4 py-3 text-sm">
            <div className="flex justify-between text-slate-600">
              <span>Subtotal</span>
              <span className="tabular-nums">{money(totals.subtotal)}</span>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-600">Descuento</span>
              <div className="flex items-center gap-1">
                <Select className="h-8 w-16 px-2" value={discountMode} onChange={(e) => setDiscountMode(e.target.value as 'amount' | 'percent')} aria-label="Tipo de descuento">
                  <option value="amount">$</option>
                  <option value="percent">%</option>
                </Select>
                <Input className="h-8 w-24 text-right" inputMode="decimal" value={discountText} onChange={(e) => setDiscountText(e.target.value)} placeholder="0" aria-label="Descuento" />
              </div>
            </div>
            {totals.discount > 0 ? (
              <div className="flex justify-between text-slate-600">
                <span />
                <span className="tabular-nums">-{money(totals.discount)}</span>
              </div>
            ) : null}
            <div className="flex items-end justify-between pt-1">
              <span className="text-base font-medium">Total</span>
              <span className="text-3xl font-bold tabular-nums text-slate-900">{money(totals.total)}</span>
            </div>
            <Button variant="success" size="lg" className="mt-2 w-full" disabled={lines.length === 0} onClick={() => setPaying(true)}>
              Cobrar (F4)
            </Button>
          </div>
        </Card>
      </aside>

      {paying ? (
        <CheckoutDialog
          lines={lines}
          customerId={customer?.id ?? null}
          globalDiscount={totals.discount}
          total={totals.total}
          blockedByCash={requireCash && cash.isSuccess && !cash.data}
          onClose={() => setPaying(false)}
          onDone={(sale) => {
            setPaying(false)
            setDone(sale)
            clearSale()
            void queryClient.invalidateQueries({ queryKey: ['pos'] })
            void queryClient.invalidateQueries({ queryKey: ['cash'] })
            void queryClient.invalidateQueries({ queryKey: ['inventory'] })
            void queryClient.invalidateQueries({ queryKey: ['sales'] })
          }}
        />
      ) : null}
      {done ? <SaleDoneDialog sale={done} onClose={() => { setDone(null); searchRef.current?.focus() }} /> : null}
      {scanning ? (
        <Modal open onClose={() => setScanning(false)} title="Escanear código">
          <CameraScanner onClose={() => setScanning(false)} onDetected={onCameraCode} />
        </Modal>
      ) : null}
      {freeItem ? (
        <FreeItemDialog
          onClose={() => setFreeItem(false)}
          onAdd={(name, price, qty) => {
            setLines((p) => [...p, { key: newIdempotencyKey(), itemId: null, name, sku: 'LIBRE', unitPrice: price, quantity: qty, discount: 0, trackStock: false, available: null }])
            setFreeItem(false)
          }}
        />
      ) : null}
      {pickingCustomer ? (
        <Modal open onClose={() => setPickingCustomer(false)} title="Asignar cliente">
          <CustomerSearch
            autoFocus
            onSelect={(c) => {
              setCustomer({ id: c.id, fullName: c.fullName })
              setPickingCustomer(false)
            }}
          />
          <p className="mt-2 text-xs text-slate-500">Sirve para facturar a su nombre y para ver la compra en su ficha.</p>
        </Modal>
      ) : null}
    </div>
  )

}

function LineDiscount({ line, onChange }: { line: CartLine; onChange: (amount: number) => void }) {
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  if (!open)
    return (
      <button type="button" className="text-xs text-brand-700 hover:underline" onClick={() => setOpen(true)}>
        {line.discount > 0 ? `Desc. ${money(line.discount)}` : 'Descuento'}
      </button>
    )
  return (
    <span className="flex items-center gap-1">
      <Input
        autoFocus
        className="h-7 w-20 text-xs"
        placeholder="$ o %"
        value={text}
        onChange={(e) => setText(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            const gross = round2(line.unitPrice * line.quantity)
            const isPct = text.trim().endsWith('%')
            const v = parseAmount(text.replace('%', '')) ?? 0
            onChange(Math.min(gross, isPct ? round2((gross * Math.min(100, v)) / 100) : v))
            setOpen(false)
          } else if (e.key === 'Escape') setOpen(false)
        }}
        onBlur={() => setOpen(false)}
        aria-label="Descuento de la línea (monto o porcentaje con %)"
      />
    </span>
  )
}

function FreeItemDialog({ onClose, onAdd }: { onClose: () => void; onAdd: (name: string, price: number, qty: number) => void }) {
  const [name, setName] = useState('')
  const [price, setPrice] = useState('')
  const [qty, setQty] = useState(1)
  const value = parseAmount(price)
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title="Ítem libre"
      description="Servicios u otros cobros que no están en el inventario (no mueven stock)."
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" disabled={name.trim().length < 2 || !value} onClick={() => onAdd(name.trim(), value!, qty)}>
            Agregar
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Descripción">
          <Input value={name} onChange={(e) => setName(e.target.value)} autoFocus placeholder="Instalación de templado, limpieza…" />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Precio">
            <Input inputMode="decimal" value={price} onChange={(e) => setPrice(e.target.value)} />
          </Field>
          <Field label="Cantidad">
            <Input type="number" min={1} value={qty} onChange={(e) => setQty(Math.max(1, Number(e.target.value) || 1))} />
          </Field>
        </div>
      </div>
    </Modal>
  )
}

const METHODS: PaymentMethod[] = ['Cash', 'Card', 'Transfer', 'MercadoPago', 'Other']

function CheckoutDialog({ lines, customerId, globalDiscount, total, blockedByCash, onClose, onDone }: { lines: CartLine[]; customerId: string | null; globalDiscount: number; total: number; blockedByCash: boolean; onClose: () => void; onDone: (sale: Sale) => void }) {
  const [payments, setPayments] = useState<PaymentRow[]>([{ method: 'Cash', amountText: String(total).replace('.', ','), reference: '' }])
  const [notes, setNotes] = useState('')
  // One key per checkout: retries after a network error never charge twice.
  const [idempotencyKey] = useState(newIdempotencyKey())
  const paid = round2(payments.reduce((s, p) => s + (parseAmount(p.amountText) ?? 0), 0))
  const cashPaid = round2(payments.filter((p) => p.method === 'Cash').reduce((s, p) => s + (parseAmount(p.amountText) ?? 0), 0))
  const missing = round2(total - paid)
  const change = round2(Math.max(0, paid - total))
  const invalidChange = change > 0 && change > cashPaid
  const needsCash = blockedByCash

  const checkout = useMutation({
    mutationFn: () =>
      salesApi.create(
        {
          lines: lines.map((l) => (l.itemId ? { inventoryItemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice, discountAmount: l.discount } : { description: l.name, quantity: l.quantity, unitPrice: l.unitPrice, discountAmount: l.discount })),
          payments: payments.filter((p) => (parseAmount(p.amountText) ?? 0) > 0).map((p) => ({ method: p.method, amount: parseAmount(p.amountText)!, reference: p.reference || null })),
          customerId,
          discountAmount: globalDiscount,
          notes: notes.trim() || null,
        },
        idempotencyKey
      ),
    onSuccess: (sale) => onDone(sale),
    onError: (err) => toast.error('No se pudo cobrar', { description: errorMessage(err) }),
  })

  function quick(amount: number) {
    setPayments([{ method: 'Cash', amountText: String(amount).replace('.', ','), reference: '' }])
  }
  const roundUps = Array.from(new Set([1000, 2000, 5000, 10000, 20000].map((step) => Math.ceil(total / step) * step))).filter((v) => v > total).slice(0, 3)

  return (
    <Modal
      open
      onClose={onClose}
      title={`Cobrar ${money(total)}`}
      footer={
        <>
          <Button onClick={onClose}>Volver</Button>
          <Button variant="success" size="lg" loading={checkout.isPending} disabled={missing > 0 || invalidChange || needsCash || paid <= 0 && total > 0} onClick={() => checkout.mutate()}>
            Confirmar cobro
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="flex flex-wrap gap-2">
          <Button size="sm" onClick={() => quick(total)}>
            Efectivo justo
          </Button>
          {roundUps.map((v) => (
            <Button key={v} size="sm" onClick={() => quick(v)}>
              {money(v).replace(/,00$/, '')}
            </Button>
          ))}
        </div>

        <div className="space-y-2">
          {payments.map((p, idx) => (
            <div key={idx} className="grid grid-cols-[1fr_1fr_auto] items-end gap-2">
              <Field label={idx === 0 ? 'Medio' : ''}>
                <Select value={p.method} onChange={(e) => setPayments((ps) => ps.map((x, i) => (i === idx ? { ...x, method: e.target.value as PaymentMethod } : x)))}>
                  {METHODS.map((m) => (
                    <option key={m} value={m}>
                      {PAYMENT_METHOD[m]}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label={idx === 0 ? 'Monto' : ''}>
                <Input inputMode="decimal" value={p.amountText} autoFocus={idx === payments.length - 1} onChange={(e) => setPayments((ps) => ps.map((x, i) => (i === idx ? { ...x, amountText: e.target.value } : x)))} />
              </Field>
              <Button variant="ghost" aria-label="Quitar pago" disabled={payments.length === 1} onClick={() => setPayments((ps) => ps.filter((_, i) => i !== idx))}>
                ✕
              </Button>
              {p.method !== 'Cash' ? (
                <Input className="col-span-3 h-8 text-xs" placeholder="Referencia (opcional): últimos 4, n° de operación…" value={p.reference} onChange={(e) => setPayments((ps) => ps.map((x, i) => (i === idx ? { ...x, reference: e.target.value } : x)))} />
              ) : null}
            </div>
          ))}
          <Button size="sm" variant="ghost" onClick={() => setPayments((ps) => [...ps, { method: 'Card', amountText: missing > 0 ? String(missing).replace('.', ',') : '', reference: '' }])}>
            + Dividir pago
          </Button>
        </div>

        <div className="rounded-xl bg-slate-50 p-4">
          <div className="flex justify-between text-sm">
            <span className="text-slate-600">Pagado</span>
            <span className="tabular-nums">{money(paid)}</span>
          </div>
          {missing > 0 ? (
            <div className="mt-1 flex justify-between text-lg font-semibold text-rose-700">
              <span>Falta</span>
              <span className="tabular-nums">{money(missing)}</span>
            </div>
          ) : (
            <div className="mt-1 flex justify-between text-2xl font-bold text-emerald-700">
              <span>Vuelto</span>
              <span className="tabular-nums">{money(change)}</span>
            </div>
          )}
        </div>
        {invalidChange ? <Alert tone="rose">Solo el efectivo puede dar vuelto: bajá el monto de los otros medios.</Alert> : null}
        {needsCash ? (
          <Alert tone="amber">
            La caja está cerrada. <Link to="/cash" className="font-medium underline">Abrila</Link> para registrar el cobro.
          </Alert>
        ) : null}
        <Field label="Nota (opcional)">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

function SaleDoneDialog({ sale, onClose }: { sale: Sale; onClose: () => void }) {
  const [invoicing, setInvoicing] = useState(false)
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title={`Venta ${sale.code} registrada`}
      footer={
        <Button variant="primary" onClick={onClose} autoFocus>
          Nueva venta
        </Button>
      }
    >
      <div className="space-y-4 text-center">
        <p className="text-sm text-slate-500">Total {money(sale.total, sale.currency)}</p>
        {sale.changeAmount > 0 ? (
          <div className="rounded-xl bg-emerald-50 p-4">
            <p className="text-sm text-emerald-700">Vuelto</p>
            <p className="text-4xl font-bold tabular-nums text-emerald-700">{money(sale.changeAmount, sale.currency)}</p>
          </div>
        ) : null}
        <div className="flex flex-wrap justify-center gap-2">
          <Button onClick={() => void printPdf(`/sales/${sale.id}/ticket`)}>Imprimir ticket</Button>
          <Button variant="ghost" onClick={() => void openPdf(`/sales/${sale.id}/ticket`)}>
            Ver ticket
          </Button>
          <Button variant="ghost" onClick={() => setInvoicing(true)}>
            Facturar
          </Button>
        </div>
      </div>
      {invoicing ? <InvoiceDialog saleId={sale.id} onClose={() => setInvoicing(false)} onDone={() => setInvoicing(false)} /> : null}
    </Modal>
  )
}
