import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ordersApi } from '../../../api/endpoints'
import { errorMessage } from '../../../api/http'
import type { Quote, QuoteInput, QuoteItemInput, QuoteItemKind, RepairOrder, SuggestedItem } from '../../../api/types'
import { useSession } from '../../../auth/session'
import { Money } from '../../../components/domain'
import { Alert, Badge, Button, Card, Checkbox, EmptyState, Field, Input, Loading, Modal, Select, Table, Td, Textarea, Th } from '../../../components/ui'
import { dateTime, money, parseAmount } from '../../../lib/format'
import { openPdf } from '../../../lib/files'
import { QUOTE_ITEM_KIND, QUOTE_STATUS_TONE } from '../../../lib/labels'
import { ItemPicker } from '../../inventory/ItemPicker'

export function QuotesPanel({ order }: { order: RepairOrder }) {
  const { can } = useSession()
  const queryClient = useQueryClient()
  const quotes = useQuery({ queryKey: ['order', order.id, 'quotes'], queryFn: () => ordersApi.quotes(order.id) })
  const [editing, setEditing] = useState<{ quote?: Quote; initial: QuoteInput } | null>(null)
  const [sending, setSending] = useState<Quote | null>(null)
  const [deciding, setDeciding] = useState<{ quote: Quote; approve: boolean } | null>(null)
  const canWork = can('orders.work') && order.status !== 'Delivered' && order.status !== 'Cancelled'

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['order', order.id] })
    void queryClient.invalidateQueries({ queryKey: ['orders'] })
  }

  if (quotes.isLoading) return <Loading />
  const list = quotes.data ?? []
  const latest = list[0]

  return (
    <div className="space-y-4">
      {canWork ? (
        <div className="flex flex-wrap gap-2">
          <Button
            variant="primary"
            onClick={() =>
              setEditing({
                initial: latest
                  ? { currency: latest.currency, items: latest.items.map(toInput), discountAmount: latest.discountAmount, warrantyDays: latest.warrantyDays, notes: latest.notes }
                  : { currency: order.currency || 'ARS', items: [{ kind: 'Labor', description: 'Mano de obra', quantity: 1, unitPrice: 0 }], discountAmount: 0 },
              })
            }
          >
            {latest ? '+ Nueva versión' : '+ Armar presupuesto'}
          </Button>
          {order.isWarrantyClaim ? <Badge tone="violet">Reingreso por garantía: sin cargo salvo que se presupueste algo nuevo</Badge> : null}
        </div>
      ) : null}

      {list.length === 0 ? (
        <EmptyState title="Todavía no hay presupuestos" description="Armá el presupuesto con mano de obra y repuestos. El cliente puede aprobarlo desde el link de seguimiento." />
      ) : (
        list.map((q) => (
          <Card
            key={q.id}
            title={
              <span className="flex items-center gap-2">
                Versión {q.version} <Badge tone={QUOTE_STATUS_TONE[q.status] ?? 'slate'}>{q.statusLabel}</Badge>
              </span>
            }
            actions={
              <>
                <Button size="sm" variant="ghost" onClick={() => void openPdf(`/orders/${order.id}/documents/quote`, { quoteId: q.id })}>
                  PDF
                </Button>
                {canWork && q.status === 'Draft' ? (
                  <>
                    <Button size="sm" onClick={() => setEditing({ quote: q, initial: { currency: q.currency, items: q.items.map(toInput), discountAmount: q.discountAmount, warrantyDays: q.warrantyDays, notes: q.notes } })}>
                      Editar
                    </Button>
                    <Button size="sm" variant="primary" onClick={() => setSending(q)}>
                      Enviar al cliente
                    </Button>
                  </>
                ) : null}
                {canWork && (q.status === 'Draft' || q.status === 'Sent') ? (
                  <>
                    <Button size="sm" variant="success" onClick={() => setDeciding({ quote: q, approve: true })}>
                      Aprobado
                    </Button>
                    <Button size="sm" onClick={() => setDeciding({ quote: q, approve: false })}>
                      Rechazado
                    </Button>
                  </>
                ) : null}
              </>
            }
          >
            <Table>
              <thead>
                <tr>
                  <Th>Tipo</Th>
                  <Th>Descripción</Th>
                  <Th align="right">Cant.</Th>
                  <Th align="right">Precio</Th>
                  <Th align="right">Subtotal</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {q.items.map((i) => (
                  <tr key={i.id}>
                    <Td className="text-slate-500">{QUOTE_ITEM_KIND[i.kind]}</Td>
                    <Td>{i.description}</Td>
                    <Td align="right">{i.quantity}</Td>
                    <Td align="right">{money(i.unitPrice, q.currency)}</Td>
                    <Td align="right">{money(i.lineTotal, q.currency)}</Td>
                  </tr>
                ))}
              </tbody>
              <tfoot className="text-sm">
                {q.discountAmount > 0 ? (
                  <tr>
                    <Td colSpan={4} align="right" className="text-slate-500">
                      Descuento
                    </Td>
                    <Td align="right">-{money(q.discountAmount, q.currency)}</Td>
                  </tr>
                ) : null}
                <tr>
                  <Td colSpan={4} align="right" className="font-semibold">
                    Total
                  </Td>
                  <Td align="right" className="font-semibold">
                    {money(q.total, q.currency)}
                  </Td>
                </tr>
              </tfoot>
            </Table>
            <div className="mt-3 flex flex-wrap gap-x-6 gap-y-1 text-xs text-slate-500">
              <span>Creado {dateTime(q.createdAtUtc)}</span>
              {q.sentAtUtc ? <span>Enviado {dateTime(q.sentAtUtc)}</span> : null}
              {q.validUntilUtc ? <span>Válido hasta {dateTime(q.validUntilUtc)}</span> : null}
              {q.warrantyDays ? <span>Garantía {q.warrantyDays} días</span> : null}
              {q.decidedAtUtc ? (
                <span>
                  Decisión {dateTime(q.decidedAtUtc)} ({q.decisionSource === 'CustomerPortal' ? 'por el cliente desde el link' : 'registrada por el local'}){q.decisionNote ? `: “${q.decisionNote}”` : ''}
                </span>
              ) : null}
            </div>
            {q.notes ? <p className="mt-2 text-sm text-slate-600">{q.notes}</p> : null}
          </Card>
        ))
      )}

      {editing ? <QuoteEditor order={order} quote={editing.quote} initial={editing.initial} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); refresh() }} /> : null}
      {sending ? <SendQuoteDialog order={order} quote={sending} onClose={() => setSending(null)} onDone={() => { setSending(null); refresh() }} /> : null}
      {deciding ? <DecideDialog order={order} quote={deciding.quote} approve={deciding.approve} onClose={() => setDeciding(null)} onDone={() => { setDeciding(null); refresh() }} /> : null}
    </div>
  )
}

function toInput(i: { kind: QuoteItemKind; description: string; quantity: number; unitPrice: number; inventoryItemId?: string | null; warrantyDays?: number | null }): QuoteItemInput {
  return { kind: i.kind, description: i.description, quantity: i.quantity, unitPrice: i.unitPrice, inventoryItemId: i.inventoryItemId ?? null, warrantyDays: i.warrantyDays ?? null }
}

function QuoteEditor({ order, quote, initial, onClose, onSaved }: { order: RepairOrder; quote?: Quote; initial: QuoteInput; onClose: () => void; onSaved: () => void }) {
  const [currency, setCurrency] = useState(initial.currency)
  const [items, setItems] = useState<(QuoteItemInput & { priceText: string })[]>(initial.items.map((i) => ({ ...i, priceText: String(i.unitPrice).replace('.', ',') })))
  const [discount, setDiscount] = useState(initial.discountAmount ? String(initial.discountAmount).replace('.', ',') : '')
  const [warranty, setWarranty] = useState(initial.warrantyDays ? String(initial.warrantyDays) : '')
  const [notes, setNotes] = useState(initial.notes ?? '')
  const [error, setError] = useState<string | null>(null)
  const suggestions = useQuery({ queryKey: ['order', order.id, 'suggestions', false], queryFn: () => ordersApi.suggestions(order.id, false), staleTime: 5 * 60_000 })
  const [brand, model] = splitDevice(order.deviceLabel)

  const subtotal = items.reduce((s, i) => s + Math.round((parseAmount(i.priceText) ?? 0) * i.quantity * 100) / 100, 0)
  const discountValue = parseAmount(discount) ?? 0
  const total = Math.max(0, subtotal - discountValue)

  const save = useMutation({
    mutationFn: () => {
      const body: QuoteInput = {
        currency,
        items: items.map(({ priceText, ...i }) => ({ ...i, unitPrice: parseAmount(priceText) ?? 0 })),
        discountAmount: discountValue,
        warrantyDays: warranty ? Number(warranty) : null,
        notes: notes.trim() || null,
      }
      return quote ? ordersApi.updateQuote(order.id, quote.id, body) : ordersApi.createQuote(order.id, body)
    },
    onSuccess: () => {
      toast.success('Presupuesto guardado')
      onSaved()
    },
    onError: (err) => setError(errorMessage(err)),
  })

  function addSuggested(s: SuggestedItem) {
    setItems((prev) => [...prev, { kind: s.kind, description: s.description, quantity: 1, unitPrice: s.medianUnitPrice ?? 0, priceText: String(s.medianUnitPrice ?? '').replace('.', ','), inventoryItemId: s.inventoryItemId ?? null }])
  }

  return (
    <Modal
      open
      size="xl"
      onClose={onClose}
      title={quote ? `Editar presupuesto v${quote.version}` : 'Nuevo presupuesto'}
      description={`${order.code} · ${order.deviceLabel}`}
      footer={
        <>
          <span className="mr-auto self-center text-sm">
            Total: <strong className="tabular-nums">{money(total, currency)}</strong>
          </span>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} disabled={items.length === 0 || items.some((i) => i.description.trim().length < 2)} onClick={() => save.mutate()}>
            Guardar borrador
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="space-y-2">
          {items.map((it, idx) => (
            <div key={idx} className="grid grid-cols-12 items-end gap-2 rounded-lg border border-slate-200 p-2">
              <Field label="Tipo" className="col-span-6 sm:col-span-2">
                <Select value={it.kind} onChange={(e) => setItems((p) => p.map((x, i) => (i === idx ? { ...x, kind: e.target.value as QuoteItemKind } : x)))}>
                  {(Object.keys(QUOTE_ITEM_KIND) as QuoteItemKind[]).map((k) => (
                    <option key={k} value={k}>
                      {QUOTE_ITEM_KIND[k]}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Descripción" className="col-span-12 sm:col-span-5">
                <Input value={it.description} onChange={(e) => setItems((p) => p.map((x, i) => (i === idx ? { ...x, description: e.target.value } : x)))} />
              </Field>
              <Field label="Cant." className="col-span-3 sm:col-span-1">
                <Input type="number" min={1} value={it.quantity} onChange={(e) => setItems((p) => p.map((x, i) => (i === idx ? { ...x, quantity: Math.max(1, Number(e.target.value) || 1) } : x)))} />
              </Field>
              <Field label="Precio unit." className="col-span-6 sm:col-span-2">
                <Input inputMode="decimal" value={it.priceText} onChange={(e) => setItems((p) => p.map((x, i) => (i === idx ? { ...x, priceText: e.target.value } : x)))} />
              </Field>
              <div className="col-span-3 flex items-center justify-end gap-1 sm:col-span-2">
                {it.inventoryItemId ? <Badge tone="indigo">Stock</Badge> : null}
                <Button size="sm" variant="ghost" aria-label="Quitar ítem" onClick={() => setItems((p) => p.filter((_, i) => i !== idx))}>
                  ✕
                </Button>
              </div>
            </div>
          ))}
          <div className="grid gap-2 sm:grid-cols-[1fr_auto]">
            <ItemPicker
              brand={brand}
              model={model}
              placeholder="Agregar repuesto del inventario…"
              onSelect={(i) => setItems((p) => [...p, { kind: 'Part', description: i.name, quantity: 1, unitPrice: i.salePrice ?? 0, priceText: String(i.salePrice ?? '').replace('.', ','), inventoryItemId: i.id }])}
            />
            <Button onClick={() => setItems((p) => [...p, { kind: 'Labor', description: '', quantity: 1, unitPrice: 0, priceText: '' }])}>+ Ítem libre</Button>
          </div>
        </div>

        {suggestions.data && (suggestions.data.suggestedItems.length > 0 || suggestions.data.priceRange) ? (
          <Alert tone="indigo" title="Sugerido por reparaciones parecidas">
            {suggestions.data.priceRange ? (
              <p>
                En {suggestions.data.priceRange.samples} casos similares se cobró entre {money(suggestions.data.priceRange.min, suggestions.data.priceRange.currency)} y {money(suggestions.data.priceRange.max, suggestions.data.priceRange.currency)} (mediana {money(suggestions.data.priceRange.median, suggestions.data.priceRange.currency)}).
              </p>
            ) : null}
            <div className="mt-2 flex flex-wrap gap-2">
              {suggestions.data.suggestedItems.slice(0, 6).map((s) => (
                <Button key={s.description} size="sm" onClick={() => addSuggested(s)}>
                  + {s.description} {s.medianUnitPrice ? `(${money(s.medianUnitPrice, s.currency ?? currency)})` : ''}
                </Button>
              ))}
            </div>
          </Alert>
        ) : null}

        <div className="grid gap-3 sm:grid-cols-4">
          <Field label="Moneda">
            <Select value={currency} onChange={(e) => setCurrency(e.target.value)}>
              <option value="ARS">ARS</option>
              <option value="USD">USD</option>
            </Select>
          </Field>
          <Field label="Descuento">
            <Input inputMode="decimal" value={discount} onChange={(e) => setDiscount(e.target.value)} placeholder="0" />
          </Field>
          <Field label="Garantía (días)" hint="Vacío = la de la sucursal">
            <Input type="number" min={0} value={warranty} onChange={(e) => setWarranty(e.target.value)} />
          </Field>
          <div className="flex flex-col justify-end text-right text-sm">
            <span className="text-slate-500">Subtotal {money(subtotal, currency)}</span>
            <span className="text-base font-semibold">Total {money(total, currency)}</span>
          </div>
        </div>
        <Field label="Notas para el cliente">
          <Textarea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Incluye…, plazo estimado…" />
        </Field>
        {error ? <Alert tone="rose">{error}</Alert> : null}
      </div>
    </Modal>
  )
}

function SendQuoteDialog({ order, quote, onClose, onDone }: { order: RepairOrder; quote: Quote; onClose: () => void; onDone: () => void }) {
  const [validDays, setValidDays] = useState('')
  const [notify, setNotify] = useState(true)
  const [result, setResult] = useState<{ message?: string | null; wa?: string | null; queued: boolean; warnings: string[] } | null>(null)
  const send = useMutation({
    mutationFn: () => ordersApi.sendQuote(order.id, quote.id, { validDays: validDays ? Number(validDays) : null, enqueueOutbox: notify }),
    onSuccess: (r) => setResult({ message: r.suggestedMessage, wa: r.whatsAppUrl, queued: !!r.outboxItemId, warnings: r.warnings }),
    onError: (err) => toast.error('No se pudo enviar', { description: errorMessage(err) }),
  })

  return (
    <Modal
      open
      onClose={result ? onDone : onClose}
      title={`Enviar presupuesto v${quote.version}`}
      description={`Total ${money(quote.total, quote.currency)}`}
      footer={
        result ? (
          <Button variant="primary" onClick={onDone}>
            Listo
          </Button>
        ) : (
          <>
            <Button onClick={onClose}>Cancelar</Button>
            <Button variant="primary" loading={send.isPending} onClick={() => send.mutate()}>
              Enviar
            </Button>
          </>
        )
      }
    >
      {result ? (
        <div className="space-y-3 text-sm">
          {result.queued ? <Alert tone="green">El mensaje con el link de aprobación quedó en cola.</Alert> : null}
          {result.warnings.map((w) => (
            <Alert key={w} tone="amber">
              {w}
            </Alert>
          ))}
          {result.message ? <pre className="whitespace-pre-wrap rounded-lg bg-slate-50 p-3 font-sans">{result.message}</pre> : null}
          {result.wa ? (
            <a href={result.wa} target="_blank" rel="noreferrer" className="inline-flex rounded-lg bg-emerald-600 px-3 py-2 font-medium text-white hover:bg-emerald-700">
              Abrir WhatsApp
            </a>
          ) : null}
        </div>
      ) : (
        <div className="space-y-3">
          <Field label="Validez (días)" hint="Vacío = la configurada en la sucursal. Vencido, el cliente ya no puede aprobarlo.">
            <Input type="number" min={1} value={validDays} onChange={(e) => setValidDays(e.target.value)} />
          </Field>
          <Checkbox label="Enviar mensaje al cliente con el link para aprobar o rechazar" checked={notify} onChange={(e) => setNotify(e.target.checked)} />
        </div>
      )}
    </Modal>
  )
}

function DecideDialog({ order, quote, approve, onClose, onDone }: { order: RepairOrder; quote: Quote; approve: boolean; onClose: () => void; onDone: () => void }) {
  const [note, setNote] = useState('')
  const decide = useMutation({
    mutationFn: () => (approve ? ordersApi.approveQuote(order.id, quote.id, note || undefined) : ordersApi.rejectQuote(order.id, quote.id, note || undefined)),
    onSuccess: (r) => {
      toast.success(approve ? 'Presupuesto aprobado' : 'Presupuesto rechazado', r.warnings.length ? { description: r.warnings.join(' ') } : undefined)
      onDone()
    },
    onError: (err) => toast.error(errorMessage(err)),
  })
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title={approve ? 'Registrar aprobación' : 'Registrar rechazo'}
      description={approve ? 'El cliente aprobó por teléfono, WhatsApp o en el local.' : 'El cliente no quiere avanzar con la reparación.'}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant={approve ? 'success' : 'danger'} loading={decide.isPending} onClick={() => decide.mutate()}>
            {approve ? 'Aprobar' : 'Rechazar'}
          </Button>
        </>
      }
    >
      <div className="space-y-3 text-sm">
        <p>
          Total: <Money value={quote.total} currency={quote.currency} className="font-semibold" />
        </p>
        {approve ? <p className="text-slate-600">Al aprobar se fija el precio de la orden y se reservan los repuestos del inventario.</p> : null}
        <Field label="Nota (opcional)">
          <Input value={note} onChange={(e) => setNote(e.target.value)} placeholder={approve ? 'Aprobó por WhatsApp' : 'Le pareció caro'} />
        </Field>
      </div>
    </Modal>
  )
}

function splitDevice(label: string): [string | undefined, string | undefined] {
  const clean = label.replace(/\s*\(.*\)\s*$/, '')
  const [brand, ...rest] = clean.split(' ')
  return [brand || undefined, rest.join(' ') || undefined]
}
