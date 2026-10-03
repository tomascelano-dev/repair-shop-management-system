import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { cashApi, ordersApi, salesApi } from '../../../api/endpoints'
import { errorMessage, newIdempotencyKey } from '../../../api/http'
import type { DocumentType, OrderPayment, PaymentMethod, RepairOrder, TaxCondition } from '../../../api/types'
import { useSession } from '../../../auth/session'
import { Money } from '../../../components/domain'
import { Alert, Badge, Button, Card, EmptyState, Field, Input, KeyValue, Loading, Modal, Select, Table, Td, Th } from '../../../components/ui'
import { dateTime, money, parseAmount } from '../../../lib/format'
import { openPdf } from '../../../lib/files'
import { DOCUMENT_TYPE, PAYMENT_METHOD, TAX_CONDITION } from '../../../lib/labels'
import { useShopSettings } from '../../../lib/shop'

export function PaymentsPanel({ order }: { order: RepairOrder }) {
  const { can } = useSession()
  const queryClient = useQueryClient()
  const financials = useQuery({ queryKey: ['order', order.id, 'financials'], queryFn: () => ordersApi.financials(order.id) })
  const payments = useQuery({ queryKey: ['order', order.id, 'payments'], queryFn: () => ordersApi.payments(order.id) })
  const links = useQuery({ queryKey: ['order', order.id, 'links'], queryFn: () => ordersApi.paymentLinks(order.id), enabled: can('sales') })
  const invoices = useQuery({ queryKey: ['order', order.id, 'invoices'], queryFn: () => ordersApi.invoices(order.id), enabled: can('sales') })
  const [paying, setPaying] = useState(false)
  const [refunding, setRefunding] = useState<OrderPayment | null>(null)
  const [invoicing, setInvoicing] = useState(false)
  const [agreed, setAgreed] = useState(false)

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['order', order.id] })
    void queryClient.invalidateQueries({ queryKey: ['orders'] })
    void queryClient.invalidateQueries({ queryKey: ['cash'] })
  }

  const createLink = useMutation({
    mutationFn: () => ordersApi.createPaymentLink(order.id),
    onSuccess: async (link) => {
      try {
        await navigator.clipboard.writeText(link.url)
        toast.success('Link de pago copiado', { description: 'Pegalo en WhatsApp o mandá el link de seguimiento: también tiene el botón de pago.' })
      } catch {
        toast.success('Link de pago creado')
      }
      void queryClient.invalidateQueries({ queryKey: ['order', order.id, 'links'] })
    },
    onError: (err) => toast.error('No se pudo crear el link', { description: errorMessage(err) }),
  })

  const f = financials.data
  const cancelled = order.status === 'Cancelled'

  return (
    <div className="space-y-4">
      <Card
        title="Resumen"
        actions={
          can('orders.work') && !order.isWarrantyClaim && order.status !== 'Delivered' && !cancelled ? (
            <Button size="sm" variant="ghost" onClick={() => setAgreed(true)}>
              {f?.hasAgreedPrice ? 'Cambiar precio acordado' : 'Fijar precio sin presupuesto detallado'}
            </Button>
          ) : null
        }
      >
        {financials.isLoading || !f ? (
          <Loading />
        ) : (
          <div className="space-y-3">
            <KeyValue
              items={[
                { label: 'Precio acordado', value: f.hasAgreedPrice ? money(f.agreedPrice, f.currency) : <span className="text-slate-400">Sin presupuesto aprobado</span> },
                { label: 'Repuestos extra', value: money(f.extraCharges, f.currency), hidden: f.extraCharges === 0 },
                { label: 'Total', value: <strong>{money(f.total, f.currency)}</strong> },
                { label: 'Pagado', value: money(f.paid, f.currency) },
                { label: 'Saldo', value: <Money value={f.balanceDue} currency={f.currency} colorize className="font-semibold" /> },
              ]}
            />
            {can('sales') && !cancelled ? (
              <div className="flex flex-wrap gap-2">
                <Button variant="primary" onClick={() => setPaying(true)}>
                  Registrar pago / seña
                </Button>
                {f.balanceDue > 0 ? (
                  <Button loading={createLink.isPending} onClick={() => createLink.mutate()}>
                    Link de pago Mercado Pago
                  </Button>
                ) : null}
                <Button onClick={() => setInvoicing(true)} disabled={f.paid <= 0}>
                  Facturar
                </Button>
              </div>
            ) : null}
          </div>
        )}
      </Card>

      <Card title="Movimientos" padded={false}>
        {payments.isLoading ? (
          <Loading />
        ) : (payments.data ?? []).length === 0 ? (
          <div className="p-4">
            <EmptyState title="Sin pagos registrados" />
          </div>
        ) : (
          <Table>
            <thead className="bg-slate-50">
              <tr>
                <Th>Fecha</Th>
                <Th>Tipo</Th>
                <Th>Medio</Th>
                <Th>Referencia</Th>
                <Th align="right">Monto</Th>
                <Th />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {payments.data!.map((p) => (
                <tr key={p.id}>
                  <Td className="whitespace-nowrap text-slate-600">{dateTime(p.createdAtUtc)}</Td>
                  <Td>{p.type === 'Refund' ? <Badge tone="rose">Devolución</Badge> : p.isDeposit ? <Badge tone="indigo">Seña</Badge> : <Badge tone="green">Pago</Badge>}</Td>
                  <Td>{PAYMENT_METHOD[p.method]}</Td>
                  <Td className="text-slate-500">{p.reference ?? '—'}</Td>
                  <Td align="right" className={p.type === 'Refund' ? 'text-rose-700' : undefined}>
                    {p.type === 'Refund' ? '-' : ''}
                    {money(p.amount, p.currency)}
                    {p.refundedAmount > 0 ? <span className="block text-xs text-slate-500">devuelto {money(p.refundedAmount, p.currency)}</span> : null}
                  </Td>
                  <Td align="right" className="whitespace-nowrap">
                    {p.type === 'Payment' ? (
                      <>
                        <Button size="sm" variant="ghost" onClick={() => void openPdf(`/orders/${order.id}/documents/payments/${p.id}`)}>
                          Recibo
                        </Button>
                        {can('sales') && p.refundedAmount < p.amount ? (
                          <Button size="sm" variant="ghost" onClick={() => setRefunding(p)}>
                            Devolver
                          </Button>
                        ) : null}
                      </>
                    ) : null}
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>

      {(links.data ?? []).length > 0 ? (
        <Card title="Links de pago">
          <ul className="space-y-2 text-sm">
            {links.data!.map((l) => (
              <li key={l.id} className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  {money(l.amount, l.currency)} · <Badge tone={l.status === 'Paid' ? 'green' : l.status === 'Pending' ? 'blue' : 'slate'}>{l.status === 'Paid' ? 'Pagado' : l.status === 'Pending' ? 'Pendiente' : 'Vencido'}</Badge>
                  <span className="ml-2 text-xs text-slate-500">vence {dateTime(l.expiresAtUtc)}</span>
                </span>
                {l.status === 'Pending' ? (
                  <Button size="sm" variant="ghost" onClick={() => void navigator.clipboard.writeText(l.url).then(() => toast.success('Link copiado'))}>
                    Copiar link
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      {(invoices.data ?? []).length > 0 ? (
        <Card title="Comprobantes electrónicos">
          <ul className="space-y-2 text-sm">
            {invoices.data!.map((i) => (
              <li key={i.id} className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  {i.voucherType} {i.code} · {money(i.total)} · <Badge tone={i.status === 'Authorized' ? 'green' : 'rose'}>{i.status === 'Authorized' ? `CAE ${i.cae}` : i.status}</Badge>
                  {i.resultMessage && i.status !== 'Authorized' ? <span className="block text-xs text-rose-700">{i.resultMessage}</span> : null}
                </span>
                {i.status === 'Authorized' ? (
                  <Button size="sm" variant="ghost" onClick={() => void openPdf(`/invoices/${i.id}/pdf`)}>
                    PDF
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      {paying && f ? <PaymentDialog order={order} balance={f.balanceDue} currency={f.currency} onClose={() => setPaying(false)} onDone={() => { setPaying(false); refresh() }} /> : null}
      {refunding ? <RefundDialog order={order} payment={refunding} onClose={() => setRefunding(null)} onDone={() => { setRefunding(null); refresh() }} /> : null}
      {invoicing ? <InvoiceDialog orderId={order.id} onClose={() => setInvoicing(false)} onDone={() => { setInvoicing(false); refresh() }} /> : null}
      {agreed && f ? <AgreedPriceDialog order={order} current={f.hasAgreedPrice ? f.agreedPrice : null} currency={f.currency} onClose={() => setAgreed(false)} onDone={() => { setAgreed(false); refresh() }} /> : null}
    </div>
  )
}

function PaymentDialog({ order, balance, currency, onClose, onDone }: { order: RepairOrder; balance: number; currency: string; onClose: () => void; onDone: () => void }) {
  const [amount, setAmount] = useState(balance > 0 ? String(balance).replace('.', ',') : '')
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  const [reference, setReference] = useState('')
  const [key] = useState(newIdempotencyKey())
  const value = parseAmount(amount) ?? 0
  const settings = useShopSettings()
  const cash = useQuery({ queryKey: ['cash', 'current'], queryFn: cashApi.current, staleTime: 30_000 })
  const cashBlocked = (settings.data?.requireOpenCashSession ?? true) && cash.isSuccess && !cash.data
  const pay = useMutation({
    mutationFn: () => ordersApi.addPayment(order.id, { amount: value, currency, method, reference: reference.trim() || null }, key),
    onSuccess: () => {
      toast.success('Pago registrado')
      onDone()
    },
    onError: (err) => toast.error('No se pudo registrar el pago', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title="Registrar pago"
      description={balance > 0 ? `Saldo: ${money(balance, currency)}` : 'La orden no tiene saldo pendiente: se registra como seña.'}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={pay.isPending} disabled={value <= 0 || cashBlocked} onClick={() => pay.mutate()}>
            Registrar {value > 0 ? money(value, currency) : ''}
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label={`Monto (${currency})`}>
          <Input inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} autoFocus />
        </Field>
        <Field label="Medio de pago">
          <Select value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
            {(Object.keys(PAYMENT_METHOD) as PaymentMethod[]).map((m) => (
              <option key={m} value={m}>
                {PAYMENT_METHOD[m]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Referencia" hint="N° de transferencia, últimos 4 de la tarjeta…">
          <Input value={reference} onChange={(e) => setReference(e.target.value)} />
        </Field>
        {balance > 0 && value > balance ? <Alert tone="rose">El monto supera el saldo pendiente.</Alert> : null}
        {cashBlocked ? (
          <Alert tone="amber" title="La caja está cerrada">
            Los cobros se registran dentro de un turno de caja. <Link to="/cash" className="font-medium underline">Abrir caja</Link>
          </Alert>
        ) : null}
      </div>
    </Modal>
  )
}

function RefundDialog({ order, payment, onClose, onDone }: { order: RepairOrder; payment: OrderPayment; onClose: () => void; onDone: () => void }) {
  const max = payment.amount - payment.refundedAmount
  const [amount, setAmount] = useState(String(max).replace('.', ','))
  const [method, setMethod] = useState<PaymentMethod>(payment.method)
  const [reason, setReason] = useState('')
  const value = parseAmount(amount) ?? 0
  const refund = useMutation({
    mutationFn: () => ordersApi.refundPayment(order.id, payment.id, { amount: value, method, reason: reason.trim() }),
    onSuccess: () => {
      toast.success('Devolución registrada')
      onDone()
    },
    onError: (err) => toast.error('No se pudo devolver', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title="Devolver pago"
      description={`Pago de ${money(payment.amount, payment.currency)} (${PAYMENT_METHOD[payment.method]})`}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="danger" loading={refund.isPending} disabled={value <= 0 || value > max || reason.trim().length < 3} onClick={() => refund.mutate()}>
            Devolver
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Monto">
          <Input inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} />
        </Field>
        <Field label="Se devuelve en">
          <Select value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
            {(Object.keys(PAYMENT_METHOD) as PaymentMethod[]).map((m) => (
              <option key={m} value={m}>
                {PAYMENT_METHOD[m]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Motivo" required>
          <Input value={reason} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

export function InvoiceDialog({ orderId, saleId, onClose, onDone }: { orderId?: string; saleId?: string; onClose: () => void; onDone: () => void }) {
  const [docType, setDocType] = useState<DocumentType>('None')
  const [docNumber, setDocNumber] = useState('')
  const [name, setName] = useState('')
  const [tax, setTax] = useState<TaxCondition | ''>('')
  const issue = useMutation({
    mutationFn: () => {
      const body = { documentType: docType === 'None' ? null : docType, documentNumber: docNumber || null, receiverName: name || null, receiverTaxCondition: tax || null }
      return orderId ? ordersApi.issueInvoice(orderId, body) : salesApi.issueInvoice(saleId!, body)
    },
    onSuccess: (inv) => {
      if (inv.status === 'Authorized') toast.success(`Factura ${inv.letter} autorizada`, { description: `CAE ${inv.cae}` })
      else toast.error('ARCA rechazó el comprobante', { description: inv.resultMessage ?? undefined })
      onDone()
    },
    onError: (err) => toast.error('No se pudo facturar', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      onClose={onClose}
      title="Emitir factura electrónica (ARCA)"
      description="Si no completás los datos, se usan los del cliente (o consumidor final)."
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={issue.isPending} onClick={() => issue.mutate()}>
            Emitir
          </Button>
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Documento">
          <Select value={docType} onChange={(e) => setDocType(e.target.value as DocumentType)}>
            {(Object.keys(DOCUMENT_TYPE) as DocumentType[]).map((d) => (
              <option key={d} value={d}>
                {d === 'None' ? 'Usar el del cliente' : DOCUMENT_TYPE[d]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Número">
          <Input value={docNumber} disabled={docType === 'None'} onChange={(e) => setDocNumber(e.target.value)} />
        </Field>
        <Field label="Razón social / nombre">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="Condición frente al IVA">
          <Select value={tax} onChange={(e) => setTax(e.target.value as TaxCondition | '')}>
            <option value="">Usar la del cliente</option>
            {(Object.keys(TAX_CONDITION) as TaxCondition[]).map((t) => (
              <option key={t} value={t}>
                {TAX_CONDITION[t]}
              </option>
            ))}
          </Select>
        </Field>
      </div>
    </Modal>
  )
}

function AgreedPriceDialog({ order, current, currency, onClose, onDone }: { order: RepairOrder; current: number | null; currency: string; onClose: () => void; onDone: () => void }) {
  const [amount, setAmount] = useState(current !== null ? String(current).replace('.', ',') : '')
  const [cur, setCur] = useState(currency)
  const save = useMutation({
    mutationFn: () => ordersApi.setAgreedPrice(order.id, amount.trim() ? parseAmount(amount) : null, cur),
    onSuccess: () => {
      toast.success('Precio acordado actualizado')
      onDone()
    },
    onError: (err) => toast.error(errorMessage(err)),
  })
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title="Precio acordado"
      description="Para reparaciones simples sin presupuesto detallado. Cuenta como aprobado por el cliente."
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} onClick={() => save.mutate()}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-3 gap-3">
        <Field label="Monto" className="col-span-2" hint="Vacío para quitarlo">
          <Input inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} autoFocus />
        </Field>
        <Field label="Moneda">
          <Select value={cur} onChange={(e) => setCur(e.target.value)}>
            <option value="ARS">ARS</option>
            <option value="USD">USD</option>
          </Select>
        </Field>
      </div>
    </Modal>
  )
}
