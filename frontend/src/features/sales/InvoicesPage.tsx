import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { fiscalApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { FiscalInvoice } from '../../api/types'
import { useSession } from '../../auth/session'
import { Badge, Button, Card, ConfirmDialog, EmptyState, ErrorState, Field, Input, Loading, PageHeader, Pagination, Select, Table, Tabs, Td, Th } from '../../components/ui'
import { dateOnly, dateTime, money, number, parseAmount, todayIso } from '../../lib/format'
import { openPdf } from '../../lib/files'
import { INVOICE_STATUS, VOUCHER_TYPE } from '../../lib/labels'
import { SaleDialog } from './SalesPage'

const TAKE = 25

export function InvoicesPage() {
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') === 'rates' ? 'rates' : 'invoices'
  return (
    <div>
      <PageHeader title="Facturas" subtitle="Comprobantes electrónicos emitidos con ARCA y cotizaciones usadas en reportes." />
      <Tabs
        className="mb-4"
        value={tab}
        onChange={(v) => setParams(v === 'rates' ? { tab: 'rates' } : {}, { replace: true })}
        tabs={[
          { value: 'invoices', label: 'Comprobantes' },
          { value: 'rates', label: 'Cotizaciones' },
        ]}
      />
      {tab === 'invoices' ? <InvoicesTab /> : <RatesTab />}
    </div>
  )
}

function InvoicesTab() {
  const { role } = useSession()
  const queryClient = useQueryClient()
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [skip, setSkip] = useState(0)
  const [crediting, setCrediting] = useState<FiscalInvoice | null>(null)
  const [sale, setSale] = useState<string | null>(null)
  const invoices = useQuery({
    queryKey: ['invoices', from, to, skip],
    queryFn: () =>
      fiscalApi.invoices({
        dateFrom: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
        dateTo: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
        skip,
        take: TAKE,
      }),
    placeholderData: keepPreviousData,
  })
  const items = invoices.data?.items ?? []
  const creditedIds = new Set(items.filter((i) => i.associatedInvoiceId && i.status === 'Authorized').map((i) => i.associatedInvoiceId))

  const credit = useMutation({
    mutationFn: (id: string) => fiscalApi.creditNote(id),
    onSuccess: (nc) => {
      if (nc.status === 'Authorized') toast.success(`Nota de crédito ${nc.code} autorizada`)
      else toast.error('ARCA rechazó la nota de crédito', { description: nc.resultMessage ?? undefined })
      setCrediting(null)
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
    },
    onError: (err) => toast.error('No se pudo emitir la nota de crédito', { description: errorMessage(err) }),
  })

  return (
    <>
      <Card className="mb-4">
        <div className="flex flex-wrap items-end gap-3">
          <Field label="Desde">
            <Input type="date" value={from} onChange={(e) => { setFrom(e.target.value); setSkip(0) }} />
          </Field>
          <Field label="Hasta">
            <Input type="date" value={to} onChange={(e) => { setTo(e.target.value); setSkip(0) }} />
          </Field>
          <p className="pb-2 text-xs text-slate-500">Las facturas se emiten desde cada orden (pestaña Pagos) o venta.</p>
        </div>
      </Card>
      <Card padded={false}>
        {invoices.isLoading ? (
          <Loading />
        ) : invoices.isError ? (
          <div className="p-4">
            <ErrorState error={errorMessage(invoices.error)} onRetry={() => void invoices.refetch()} />
          </div>
        ) : items.length === 0 ? (
          <div className="p-4">
            <EmptyState title="Todavía no hay comprobantes" description="Configurá ARCA en Configuración → Integraciones para facturar electrónicamente." />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Comprobante</Th>
                  <Th>Fecha</Th>
                  <Th>Receptor</Th>
                  <Th>Origen</Th>
                  <Th>Estado</Th>
                  <Th align="right">Total</Th>
                  <Th />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {items.map((i) => {
                  const isCredit = i.voucherType.startsWith('NotaCredito')
                  return (
                    <tr key={i.id}>
                      <Td>
                        <span className="font-medium">{VOUCHER_TYPE[i.voucherType] ?? i.voucherType}</span>
                        <span className="block text-xs text-slate-500">{i.code}</span>
                      </Td>
                      <Td>{dateTime(i.issueDateUtc)}</Td>
                      <Td>
                        {i.receiverName}
                        {i.receiverDocumentNumber ? <span className="block text-xs text-slate-500">{i.receiverDocumentNumber}</span> : null}
                      </Td>
                      <Td>
                        {i.sourceType === 'sale' ? (
                          <button type="button" className="text-brand-700 hover:underline" onClick={() => setSale(i.sourceId)}>
                            Venta
                          </button>
                        ) : (
                          <Link to={`/orders/${i.sourceId}`} className="text-brand-700 hover:underline">
                            Orden
                          </Link>
                        )}
                      </Td>
                      <Td>
                        <Badge tone={INVOICE_STATUS[i.status]?.tone ?? 'slate'}>{INVOICE_STATUS[i.status]?.label ?? i.status}</Badge>
                        {i.cae ? <span className="block text-xs text-slate-500">CAE {i.cae}</span> : null}
                        {i.status !== 'Authorized' && i.resultMessage ? <span className="block max-w-xs text-xs text-rose-600">{i.resultMessage}</span> : null}
                        {creditedIds.has(i.id) ? <span className="block text-xs text-amber-700">Anulada con nota de crédito</span> : null}
                      </Td>
                      <Td align="right" className={isCredit ? 'text-rose-700' : 'font-medium'}>
                        {isCredit ? '-' : ''}
                        {money(i.total, 'ARS')}
                      </Td>
                      <Td align="right">
                        <span className="flex justify-end gap-1">
                          {i.status === 'Authorized' ? (
                            <Button size="sm" variant="ghost" onClick={() => void openPdf(`/invoices/${i.id}/pdf`)}>
                              PDF
                            </Button>
                          ) : null}
                          {role === 'Admin' && i.status === 'Authorized' && !isCredit && !creditedIds.has(i.id) ? (
                            <Button size="sm" variant="ghost" onClick={() => setCrediting(i)}>
                              Nota de crédito
                            </Button>
                          ) : null}
                        </span>
                      </Td>
                    </tr>
                  )
                })}
              </tbody>
            </Table>
            <div className="px-4 pb-3">
              <Pagination skip={skip} take={TAKE} total={invoices.data?.total ?? 0} onChange={setSkip} />
            </div>
          </>
        )}
      </Card>
      <ConfirmDialog
        open={!!crediting}
        title={`¿Emitir nota de crédito por ${crediting?.code ?? ''}?`}
        message={`Anula fiscalmente la factura por ${money(crediting?.total ?? 0, 'ARS')}. No devuelve dinero: registrá la devolución en la orden o venta.`}
        confirmLabel="Emitir nota de crédito"
        danger
        loading={credit.isPending}
        onConfirm={() => crediting && credit.mutate(crediting.id)}
        onClose={() => setCrediting(null)}
      />
      {sale ? <SaleDialog saleId={sale} onClose={() => setSale(null)} /> : null}
    </>
  )
}

function RatesTab() {
  const { role } = useSession()
  const queryClient = useQueryClient()
  const rates = useQuery({ queryKey: ['rates'], queryFn: () => fiscalApi.rates(60) })
  const [dateValue, setDateValue] = useState(todayIso())
  const [rate, setRate] = useState('')
  const [source, setSource] = useState('manual')
  const value = parseAmount(rate)
  const save = useMutation({
    mutationFn: () => fiscalApi.setRate({ date: dateValue, rate: value!, baseCurrency: 'USD', quoteCurrency: 'ARS', source }),
    onSuccess: () => {
      toast.success('Cotización guardada')
      setRate('')
      void queryClient.invalidateQueries({ queryKey: ['rates'] })
      void queryClient.invalidateQueries({ queryKey: ['reports'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => toast.error('No se pudo guardar', { description: errorMessage(err) }),
  })
  const refresh = useMutation({
    mutationFn: fiscalApi.refreshRates,
    onSuccess: (n) => {
      toast.success(n > 0 ? `Se actualizaron ${n} cotizaciones` : 'No hubo cotizaciones nuevas')
      void queryClient.invalidateQueries({ queryKey: ['rates'] })
    },
    onError: (err) => toast.error('No se pudo consultar el proveedor de cotizaciones', { description: errorMessage(err) }),
  })

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]">
      {role === 'Admin' ? (
        <Card title="Cargar cotización USD → ARS">
          <form
            className="space-y-3"
            onSubmit={(e) => {
              e.preventDefault()
              if (value && value > 0) save.mutate()
            }}
          >
            <Field label="Fecha">
              <Input type="date" value={dateValue} max={todayIso()} onChange={(e) => setDateValue(e.target.value)} />
            </Field>
            <Field label="Cotización (1 USD en ARS)">
              <Input inputMode="decimal" value={rate} onChange={(e) => setRate(e.target.value)} placeholder="1.250,00" />
            </Field>
            <Field label="Tipo">
              <Select value={source} onChange={(e) => setSource(e.target.value)}>
                <option value="manual">Manual (la que usa el negocio)</option>
                <option value="oficial">Oficial</option>
                <option value="blue">Blue</option>
              </Select>
            </Field>
            <div className="flex flex-wrap gap-2">
              <Button type="submit" variant="primary" loading={save.isPending} disabled={!value || value <= 0}>
                Guardar
              </Button>
              <Button loading={refresh.isPending} onClick={() => refresh.mutate()}>
                Traer cotizaciones automáticas
              </Button>
            </div>
            <p className="text-xs text-slate-500">Para convertir se usa, por día, la manual; si no hay, la oficial y luego la blue.</p>
          </form>
        </Card>
      ) : null}
      <Card title="Últimas cotizaciones" padded={false} className={role === 'Admin' ? '' : 'lg:col-span-2'}>
        {rates.isLoading ? (
          <Loading />
        ) : (rates.data ?? []).length === 0 ? (
          <div className="p-4">
            <EmptyState title="Sin cotizaciones cargadas" description="Sin cotización, los montos en dólares no se convierten en los reportes." />
          </div>
        ) : (
          <Table>
            <thead className="bg-slate-50">
              <tr>
                <Th>Fecha</Th>
                <Th>Par</Th>
                <Th>Tipo</Th>
                <Th align="right">Cotización</Th>
                <Th>Actualizada</Th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rates.data!.map((r) => (
                <tr key={r.id}>
                  <Td>{dateOnly(r.date)}</Td>
                  <Td>
                    {r.baseCurrency} → {r.quoteCurrency}
                  </Td>
                  <Td className="capitalize">{r.source}</Td>
                  <Td align="right" className="font-medium">
                    {number(r.rate, 2)}
                  </Td>
                  <Td className="text-xs text-slate-500">{dateTime(r.updatedAtUtc)}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>
    </div>
  )
}
