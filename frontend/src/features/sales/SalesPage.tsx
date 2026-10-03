import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { salesApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { PaymentMethod, Sale, SaleStatus } from '../../api/types'
import { useSession } from '../../auth/session'
import { Money } from '../../components/domain'
import { Alert, Badge, Button, Card, Checkbox, EmptyState, ErrorState, Field, Input, Loading, Modal, PageHeader, Pagination, SearchInput, Select, Table, Td, Textarea, Th } from '../../components/ui'
import { dateTime, money } from '../../lib/format'
import { openPdf, printPdf } from '../../lib/files'
import { useDebounced } from '../../lib/hooks'
import { INVOICE_STATUS, PAYMENT_METHOD, SALE_STATUS, VOUCHER_TYPE } from '../../lib/labels'
import { InvoiceDialog } from '../orders/panels/PaymentsPanel'

const TAKE = 25
const METHODS: PaymentMethod[] = ['Cash', 'Card', 'Transfer', 'MercadoPago', 'Other']

export function SalesPage() {
  const [params, setParams] = useSearchParams()
  const [q, setQ] = useState(params.get('q') ?? '')
  const debounced = useDebounced(q.trim(), 300)
  const status = (params.get('status') ?? '') as SaleStatus | ''
  const dateFrom = params.get('from') ?? ''
  const dateTo = params.get('to') ?? ''
  const skip = Number(params.get('skip') ?? 0)
  const [selected, setSelected] = useState<string | null>(params.get('id'))

  function setParam(key: string, value: string) {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    if (key !== 'skip') next.delete('skip')
    setParams(next, { replace: true })
  }

  const sales = useQuery({
    queryKey: ['sales', 'list', debounced, status, dateFrom, dateTo, skip],
    queryFn: () =>
      salesApi.search({
        q: debounced || undefined,
        status: status || undefined,
        dateFrom: dateFrom ? new Date(`${dateFrom}T00:00:00`).toISOString() : undefined,
        dateTo: dateTo ? new Date(`${dateTo}T23:59:59`).toISOString() : undefined,
        skip,
        take: TAKE,
      }),
    placeholderData: keepPreviousData,
  })

  const items = sales.data?.items ?? []
  const pageTotal = items.filter((s) => s.status !== 'Voided').reduce((sum, s) => sum + s.total - s.refundedAmount, 0)

  return (
    <div>
      <PageHeader
        title="Ventas"
        subtitle="Historial del mostrador: tickets, devoluciones, anulaciones y facturas."
        actions={
          <Link to="/pos">
            <Button variant="primary">Ir al punto de venta</Button>
          </Link>
        }
      />

      <Card className="mb-4">
        <div className="grid gap-3 md:grid-cols-[minmax(0,2fr)_1fr_1fr_1fr]">
          <SearchInput value={q} onChange={(v) => { setQ(v); setParam('q', v.trim()) }} placeholder="Buscar por número, cliente o producto…" />
          <Select value={status} onChange={(e) => setParam('status', e.target.value)} aria-label="Estado">
            <option value="">Todos los estados</option>
            {(Object.keys(SALE_STATUS) as SaleStatus[]).map((s) => (
              <option key={s} value={s}>
                {SALE_STATUS[s].label}
              </option>
            ))}
          </Select>
          <Input type="date" value={dateFrom} onChange={(e) => setParam('from', e.target.value)} aria-label="Desde" />
          <Input type="date" value={dateTo} onChange={(e) => setParam('to', e.target.value)} aria-label="Hasta" />
        </div>
      </Card>

      <Card padded={false}>
        {sales.isLoading ? (
          <Loading />
        ) : sales.isError ? (
          <div className="p-4">
            <ErrorState error={errorMessage(sales.error)} onRetry={() => void sales.refetch()} />
          </div>
        ) : items.length === 0 ? (
          <div className="p-4">
            <EmptyState title="No hay ventas con estos filtros" description="Las ventas del punto de venta aparecen acá con su ticket." />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Venta</Th>
                  <Th>Fecha</Th>
                  <Th>Cliente</Th>
                  <Th>Pagos</Th>
                  <Th>Vendedor</Th>
                  <Th>Estado</Th>
                  <Th align="right">Total</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {items.map((s) => (
                  <tr key={s.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setSelected(s.id)}>
                    <Td className="font-medium text-brand-700">
                      <button type="button" className="hover:underline" onClick={(e) => { e.stopPropagation(); setSelected(s.id) }}>
                        {s.code}
                      </button>
                    </Td>
                    <Td>{dateTime(s.createdAtUtc)}</Td>
                    <Td>{s.customerName ?? <span className="text-slate-400">Consumidor final</span>}</Td>
                    <Td className="text-xs text-slate-600">{s.payments.map((p) => PAYMENT_METHOD[p.method]).join(' + ') || '—'}</Td>
                    <Td className="text-slate-600">{s.createdByName ?? '—'}</Td>
                    <Td>
                      <Badge tone={SALE_STATUS[s.status].tone}>{SALE_STATUS[s.status].label}</Badge>
                    </Td>
                    <Td align="right">
                      <Money value={s.total} currency={s.currency} className={s.status === 'Voided' ? 'text-slate-400 line-through' : 'font-medium'} />
                      {s.refundedAmount > 0 && s.status !== 'Voided' ? <span className="block text-xs text-amber-700">-{money(s.refundedAmount, s.currency)} devuelto</span> : null}
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-100 px-4 pb-3">
              <p className="pt-3 text-xs text-slate-500">
                Neto de esta página: <strong className="text-slate-700">{money(pageTotal, items[0]?.currency)}</strong>
              </p>
              <Pagination skip={skip} take={TAKE} total={sales.data?.total ?? 0} onChange={(v) => setParam('skip', String(v))} />
            </div>
          </>
        )}
      </Card>

      {selected ? (
        <SaleDialog
          saleId={selected}
          onClose={() => {
            setSelected(null)
            if (params.has('id')) setParam('id', '')
          }}
        />
      ) : null}
    </div>
  )
}

export function SaleDialog({ saleId, onClose }: { saleId: string; onClose: () => void }) {
  const { role } = useSession()
  const queryClient = useQueryClient()
  const sale = useQuery({ queryKey: ['sales', saleId], queryFn: () => salesApi.get(saleId) })
  const invoices = useQuery({ queryKey: ['sales', saleId, 'invoices'], queryFn: () => salesApi.invoices(saleId) })
  const [refunding, setRefunding] = useState(false)
  const [voiding, setVoiding] = useState(false)
  const [invoicing, setInvoicing] = useState(false)

  function refresh() {
    void queryClient.invalidateQueries({ queryKey: ['sales'] })
    void queryClient.invalidateQueries({ queryKey: ['cash'] })
    void queryClient.invalidateQueries({ queryKey: ['inventory'] })
    void queryClient.invalidateQueries({ queryKey: ['pos'] })
  }

  const s = sale.data
  const canRefund = s && s.status !== 'Voided' && s.status !== 'Refunded' && s.lines.some((l) => l.refundedQuantity < l.quantity)
  const authorized = (invoices.data ?? []).some((i) => i.status === 'Authorized' && !i.voucherType.startsWith('NotaCredito'))

  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title={s ? `Venta ${s.code}` : 'Venta'}
      description={s ? `${dateTime(s.createdAtUtc)} · ${s.createdByName ?? ''}` : undefined}
      footer={
        s ? (
          <>
            {role === 'Admin' && s.status === 'Completed' ? (
              <Button variant="ghost" className="mr-auto text-rose-700" onClick={() => setVoiding(true)}>
                Anular venta
              </Button>
            ) : null}
            <Button onClick={() => void openPdf(`/sales/${s.id}/ticket`)}>Ver ticket</Button>
            <Button onClick={() => void printPdf(`/sales/${s.id}/ticket`)}>Imprimir</Button>
            {!authorized && s.status !== 'Voided' ? <Button onClick={() => setInvoicing(true)}>Facturar</Button> : null}
            {canRefund ? (
              <Button variant="primary" onClick={() => setRefunding(true)}>
                Devolución
              </Button>
            ) : null}
          </>
        ) : null
      }
    >
      {sale.isLoading ? (
        <Loading />
      ) : sale.isError || !s ? (
        <ErrorState error={errorMessage(sale.error)} />
      ) : (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={SALE_STATUS[s.status].tone}>{SALE_STATUS[s.status].label}</Badge>
            <span className="text-sm text-slate-600">{s.customerName ?? 'Consumidor final'}</span>
          </div>
          {s.status === 'Voided' && s.voidReason ? <Alert tone="rose" title="Venta anulada">{s.voidReason}</Alert> : null}

          <Table>
            <thead>
              <tr>
                <Th>Producto</Th>
                <Th align="right">Cant.</Th>
                <Th align="right">Precio</Th>
                <Th align="right">Desc.</Th>
                <Th align="right">Total</Th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {s.lines.map((l) => (
                <tr key={l.id}>
                  <Td>
                    <span className="font-medium text-slate-800">{l.description}</span>
                    <span className="block text-xs text-slate-400">
                      {l.sku}
                      {l.warrantyDays ? ` · garantía ${l.warrantyDays} días` : ''}
                      {l.refundedQuantity > 0 ? <span className="ml-1 text-amber-700">· {l.refundedQuantity} devuelto(s)</span> : null}
                    </span>
                  </Td>
                  <Td align="right">{l.quantity}</Td>
                  <Td align="right">{money(l.unitPrice, s.currency)}</Td>
                  <Td align="right">{l.discountAmount > 0 ? `-${money(l.discountAmount, s.currency)}` : '—'}</Td>
                  <Td align="right" className="font-medium">
                    {money(l.lineTotal, s.currency)}
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1 text-sm">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Pagos</p>
              {s.payments.map((p) => (
                <p key={p.id} className="flex justify-between">
                  <span>
                    {PAYMENT_METHOD[p.method]}
                    {p.reference ? <span className="text-xs text-slate-400"> · {p.reference}</span> : null}
                  </span>
                  <span className="tabular-nums">{money(p.amount, s.currency)}</span>
                </p>
              ))}
              {s.changeAmount > 0 ? (
                <p className="flex justify-between text-slate-500">
                  <span>Vuelto</span>
                  <span className="tabular-nums">-{money(s.changeAmount, s.currency)}</span>
                </p>
              ) : null}
            </div>
            <div className="space-y-1 rounded-lg bg-slate-50 p-3 text-sm">
              <p className="flex justify-between">
                <span className="text-slate-600">Subtotal</span>
                <span className="tabular-nums">{money(s.subtotal, s.currency)}</span>
              </p>
              {s.discountAmount > 0 ? (
                <p className="flex justify-between">
                  <span className="text-slate-600">Descuento</span>
                  <span className="tabular-nums">-{money(s.discountAmount, s.currency)}</span>
                </p>
              ) : null}
              <p className="flex justify-between text-base font-semibold">
                <span>Total</span>
                <span className="tabular-nums">{money(s.total, s.currency)}</span>
              </p>
              {s.refundedAmount > 0 ? (
                <p className="flex justify-between text-amber-700">
                  <span>Devuelto</span>
                  <span className="tabular-nums">-{money(s.refundedAmount, s.currency)}</span>
                </p>
              ) : null}
            </div>
          </div>

          {s.refunds.length > 0 ? (
            <div className="text-sm">
              <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Devoluciones</p>
              <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200">
                {s.refunds.map((r) => (
                  <li key={r.id} className="flex flex-wrap justify-between gap-2 px-3 py-2">
                    <span>
                      {dateTime(r.createdAtUtc)} · {PAYMENT_METHOD[r.method]}
                      {r.restocked ? ' · volvió al stock' : ''}
                      {r.reason ? <span className="block text-xs text-slate-500">{r.reason}</span> : null}
                    </span>
                    <span className="tabular-nums text-amber-700">-{money(r.amount, s.currency)}</span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {(invoices.data ?? []).length > 0 ? (
            <div className="text-sm">
              <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Comprobantes fiscales</p>
              <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200">
                {invoices.data!.map((i) => (
                  <li key={i.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                    <span>
                      {VOUCHER_TYPE[i.voucherType] ?? i.voucherType} {i.code}
                      <Badge tone={INVOICE_STATUS[i.status]?.tone ?? 'slate'} className="ml-2">
                        {INVOICE_STATUS[i.status]?.label ?? i.status}
                      </Badge>
                      {i.resultMessage && i.status !== 'Authorized' ? <span className="block text-xs text-rose-600">{i.resultMessage}</span> : null}
                    </span>
                    {i.status === 'Authorized' ? (
                      <Button size="sm" variant="ghost" onClick={() => void openPdf(`/invoices/${i.id}/pdf`)}>
                        PDF
                      </Button>
                    ) : null}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
          {s.notes ? <p className="text-sm text-slate-600">Nota: {s.notes}</p> : null}
        </div>
      )}

      {refunding && s ? (
        <RefundSaleDialog
          sale={s}
          onClose={() => setRefunding(false)}
          onDone={() => {
            setRefunding(false)
            refresh()
          }}
        />
      ) : null}
      {voiding && s ? (
        <VoidSaleDialog
          sale={s}
          onClose={() => setVoiding(false)}
          onDone={() => {
            setVoiding(false)
            refresh()
          }}
        />
      ) : null}
      {invoicing && s ? (
        <InvoiceDialog
          saleId={s.id}
          onClose={() => setInvoicing(false)}
          onDone={() => {
            setInvoicing(false)
            void queryClient.invalidateQueries({ queryKey: ['sales', saleId, 'invoices'] })
            void queryClient.invalidateQueries({ queryKey: ['invoices'] })
          }}
        />
      ) : null}
    </Modal>
  )
}

function RefundSaleDialog({ sale, onClose, onDone }: { sale: Sale; onClose: () => void; onDone: () => void }) {
  const refundable = sale.lines.filter((l) => l.refundedQuantity < l.quantity)
  const [qty, setQty] = useState<Record<string, number>>(() => Object.fromEntries(refundable.map((l) => [l.id, l.quantity - l.refundedQuantity])))
  const [method, setMethod] = useState<PaymentMethod>(sale.payments[0]?.method ?? 'Cash')
  const [restock, setRestock] = useState(true)
  const [reason, setReason] = useState('')

  const amount = refundable.reduce((sum, l) => sum + ((qty[l.id] ?? 0) * l.lineTotal) / l.quantity, 0)
  const selected = refundable.filter((l) => (qty[l.id] ?? 0) > 0)

  const refund = useMutation({
    mutationFn: () => salesApi.refund(sale.id, { lines: selected.map((l) => ({ saleLineId: l.id, quantity: qty[l.id]! })), method, restock, reason: reason.trim() || null }),
    onSuccess: () => {
      toast.success('Devolución registrada')
      onDone()
    },
    onError: (err) => toast.error('No se pudo registrar la devolución', { description: errorMessage(err) }),
  })

  return (
    <Modal
      open
      onClose={onClose}
      title={`Devolución de ${sale.code}`}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={refund.isPending} disabled={selected.length === 0} onClick={() => refund.mutate()}>
            Devolver {money(Math.round(amount * 100) / 100, sale.currency)}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 text-sm">
          {refundable.map((l) => {
            const max = l.quantity - l.refundedQuantity
            return (
              <li key={l.id} className="flex items-center justify-between gap-3 px-3 py-2">
                <span className="min-w-0">
                  <span className="block truncate font-medium">{l.description}</span>
                  <span className="text-xs text-slate-500">
                    Vendidos {l.quantity}
                    {l.refundedQuantity ? ` · ya devueltos ${l.refundedQuantity}` : ''}
                  </span>
                </span>
                <Input
                  type="number"
                  min={0}
                  max={max}
                  className="h-8 w-20 text-right"
                  value={qty[l.id] ?? 0}
                  onChange={(e) => setQty((q) => ({ ...q, [l.id]: Math.max(0, Math.min(max, Number(e.target.value) || 0)) }))}
                  aria-label={`Cantidad a devolver de ${l.description}`}
                />
              </li>
            )
          })}
        </ul>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Devolver el dinero en">
            <Select value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
              {METHODS.map((m) => (
                <option key={m} value={m}>
                  {PAYMENT_METHOD[m]}
                </option>
              ))}
            </Select>
          </Field>
          <div className="flex items-end pb-2">
            <Checkbox checked={restock} onChange={(e) => setRestock(e.target.checked)} label="Reingresar al stock" description="Desmarcalo si el producto vuelve fallado." />
          </div>
        </div>
        <Field label="Motivo">
          <Textarea rows={2} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Cambio, falla, error de cobro…" />
        </Field>
        {method === 'Cash' ? <p className="text-xs text-slate-500">El efectivo devuelto se descuenta de la caja abierta.</p> : null}
      </div>
    </Modal>
  )
}

function VoidSaleDialog({ sale, onClose, onDone }: { sale: Sale; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState('')
  const [method, setMethod] = useState<PaymentMethod>(sale.payments[0]?.method ?? 'Cash')
  const voidSale = useMutation({
    mutationFn: () => salesApi.void(sale.id, reason.trim(), method),
    onSuccess: () => {
      toast.success('Venta anulada')
      onDone()
    },
    onError: (err) => toast.error('No se pudo anular', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title={`Anular ${sale.code}`}
      description="Devuelve el dinero, repone el stock y deja la venta anulada. Si tiene factura, emití la nota de crédito desde Facturas."
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="danger" loading={voidSale.isPending} disabled={reason.trim().length < 3} onClick={() => voidSale.mutate()}>
            Anular venta
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Motivo" required>
          <Textarea rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Field>
        <Field label="Devolver el dinero en">
          <Select value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
            {METHODS.map((m) => (
              <option key={m} value={m}>
                {PAYMENT_METHOD[m]}
              </option>
            ))}
          </Select>
        </Field>
      </div>
    </Modal>
  )
}
