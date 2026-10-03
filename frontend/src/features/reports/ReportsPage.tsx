import { useMemo, useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { exportPaths, reportsApi, type ReportRange } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { PaymentMethod, ReportPeriod } from '../../api/types'
import { BarChart, RankBars, StackedBar } from '../../components/charts'
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Input, Loading, PageHeader, Select, Stat, Table, Tabs, Td, Th } from '../../components/ui'
import { amounts, date, dateTime, money, number, percent, todayIso } from '../../lib/format'
import { downloadFile } from '../../lib/files'
import { PAYMENT_METHOD } from '../../lib/labels'

type Report = 'revenue' | 'margins' | 'repair-times' | 'quotes' | 'top-issues' | 'technicians' | 'warranty' | 'feedback' | 'inventory'

const TABS: { value: Report; label: string }[] = [
  { value: 'revenue', label: 'Ingresos' },
  { value: 'margins', label: 'Márgenes' },
  { value: 'repair-times', label: 'Tiempos' },
  { value: 'quotes', label: 'Presupuestos' },
  { value: 'top-issues', label: 'Fallas y modelos' },
  { value: 'technicians', label: 'Técnicos' },
  { value: 'warranty', label: 'Garantías' },
  { value: 'feedback', label: 'Satisfacción' },
  { value: 'inventory', label: 'Stock valorizado' },
]

const PRESETS: { value: string; label: string; range: () => [string, string] }[] = [
  { value: '7d', label: 'Últimos 7 días', range: () => [todayIso(-6), todayIso()] },
  { value: '30d', label: 'Últimos 30 días', range: () => [todayIso(-29), todayIso()] },
  {
    value: 'month',
    label: 'Este mes',
    range: () => {
      const t = todayIso()
      return [`${t.slice(0, 8)}01`, t]
    },
  },
  {
    value: 'prev',
    label: 'Mes anterior',
    range: () => {
      const d = new Date()
      const first = new Date(d.getFullYear(), d.getMonth() - 1, 1)
      const last = new Date(d.getFullYear(), d.getMonth(), 0)
      const fmt = (x: Date) => `${x.getFullYear()}-${String(x.getMonth() + 1).padStart(2, '0')}-${String(x.getDate()).padStart(2, '0')}`
      return [fmt(first), fmt(last)]
    },
  },
  { value: '90d', label: 'Últimos 90 días', range: () => [todayIso(-89), todayIso()] },
  {
    value: 'year',
    label: 'Este año',
    range: () => {
      const t = todayIso()
      return [`${t.slice(0, 4)}-01-01`, t]
    },
  },
]

export function ReportsPage() {
  const [params, setParams] = useSearchParams()
  const tab = (params.get('r') as Report) || 'revenue'
  const preset = params.get('p') ?? '30d'
  const [customFrom, setCustomFrom] = useState(params.get('from') ?? todayIso(-29))
  const [customTo, setCustomTo] = useState(params.get('to') ?? todayIso())
  const [groupBy, setGroupBy] = useState('day')

  const [from, to] = preset === 'custom' ? [customFrom, customTo] : (PRESETS.find((p) => p.value === preset) ?? PRESETS[1]!).range()
  const range: ReportRange = useMemo(() => ({ from: new Date(`${from}T00:00:00`).toISOString(), to: new Date(`${to}T23:59:59`).toISOString() }), [from, to])

  function set(key: string, value: string) {
    const next = new URLSearchParams(params)
    next.set(key, value)
    setParams(next, { replace: true })
  }

  return (
    <div>
      <PageHeader
        title="Reportes"
        subtitle="Montos por moneda y convertidos con la cotización de cada día."
        actions={
          <Button variant="ghost" onClick={() => void downloadFile(exportPaths.report(tab), `reporte-${tab}.xlsx`, tab === 'inventory' ? undefined : { ...range, groupBy })}>
            Exportar a Excel
          </Button>
        }
      />
      <Card className="mb-4">
        <div className="flex flex-wrap items-end gap-3">
          {tab !== 'inventory' ? (
            <>
              <Select className="w-48" value={preset} onChange={(e) => set('p', e.target.value)} aria-label="Período">
                {PRESETS.map((p) => (
                  <option key={p.value} value={p.value}>
                    {p.label}
                  </option>
                ))}
                <option value="custom">Personalizado</option>
              </Select>
              {preset === 'custom' ? (
                <>
                  <Input type="date" className="w-40" value={customFrom} max={customTo} onChange={(e) => setCustomFrom(e.target.value)} aria-label="Desde" />
                  <Input type="date" className="w-40" value={customTo} min={customFrom} onChange={(e) => setCustomTo(e.target.value)} aria-label="Hasta" />
                </>
              ) : (
                <span className="pb-2 text-sm text-slate-500">
                  {date(`${from}T12:00:00`)} – {date(`${to}T12:00:00`)}
                </span>
              )}
              {tab === 'revenue' ? (
                <Select className="w-36" value={groupBy} onChange={(e) => setGroupBy(e.target.value)} aria-label="Agrupar por">
                  <option value="day">Por día</option>
                  <option value="week">Por semana</option>
                  <option value="month">Por mes</option>
                </Select>
              ) : null}
            </>
          ) : (
            <span className="text-sm text-slate-500">Foto actual del inventario.</span>
          )}
        </div>
      </Card>
      <Tabs<Report> className="mb-4" value={tab} onChange={(v) => set('r', v)} tabs={TABS} />

      {tab === 'revenue' ? <RevenueReportView range={range} groupBy={groupBy} /> : null}
      {tab === 'margins' ? <MarginsView range={range} /> : null}
      {tab === 'repair-times' ? <RepairTimesView range={range} /> : null}
      {tab === 'quotes' ? <QuotesView range={range} /> : null}
      {tab === 'top-issues' ? <TopIssuesView range={range} /> : null}
      {tab === 'technicians' ? <TechniciansView range={range} /> : null}
      {tab === 'warranty' ? <WarrantyView range={range} /> : null}
      {tab === 'feedback' ? <FeedbackView range={range} /> : null}
      {tab === 'inventory' ? <InventoryView /> : null}
    </div>
  )
}

function Result<T>({ q, children }: { q: { isLoading: boolean; isError: boolean; error: unknown; data?: T; refetch: () => unknown }; children: (data: T) => ReactNode }) {
  if (q.isLoading) return <Loading />
  if (q.isError || !q.data) return <ErrorState error={errorMessage(q.error)} onRetry={() => void q.refetch()} />
  const missing = (q.data as { period?: ReportPeriod }).period?.missingRates ?? []
  return (
    <div className="space-y-4">
      {missing.length > 0 ? (
        <Alert tone="amber" title="Faltan cotizaciones">
          No hay cotización cargada para {missing.slice(0, 5).join(', ')}
          {missing.length > 5 ? ` y ${missing.length - 5} más` : ''}: esos montos no se suman al total convertido. Cargalas en <Link to="/invoices?tab=rates" className="underline">Facturas → Cotizaciones</Link>.
        </Alert>
      ) : null}
      {children(q.data)}
    </div>
  )
}

function RevenueReportView({ range, groupBy }: { range: ReportRange; groupBy: string }) {
  const q = useQuery({ queryKey: ['reports', 'revenue', range, groupBy], queryFn: () => reportsApi.revenue(range, groupBy) })
  return (
    <Result q={q}>
      {(r) => {
        const currency = r.period.reportingCurrency
        const byPeriod = new Map<string, number>()
        for (const row of r.rows) byPeriod.set(row.period, (byPeriod.get(row.period) ?? 0) + (row.netConverted ?? 0))
        const series = [...byPeriod.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([label, value]) => ({ label: label.length === 10 ? label.slice(8, 10) + '/' + label.slice(5, 7) : label, value }))
        const orders = r.rows.reduce((s, x) => s + x.orders, 0)
        const sales = r.rows.reduce((s, x) => s + x.sales, 0)
        const refunds = r.rows.reduce((s, x) => s + x.refunds, 0)
        return (
          <>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Stat label={`Total neto (${currency})`} value={money(r.totalConverted, currency)} tone="good" />
              <Stat label="Por moneda" value={<span className="text-base">{amounts(r.totalsByCurrency)}</span>} />
              <Stat label="Cobros de órdenes" value={number(orders, 2)} hint="Monto en moneda original" />
              <Stat label="Ventas de mostrador" value={number(sales, 2)} hint={refunds ? `Devoluciones ${number(refunds, 2)}` : undefined} />
            </div>
            <Card title={`Ingresos netos por ${groupBy === 'day' ? 'día' : groupBy === 'week' ? 'semana' : 'mes'} (${currency})`}>
              <BarChart data={series} title="Ingresos netos por período" format={(v) => money(v, currency).replace(/,00$/, '')} />
            </Card>
            <Card title="Por medio de pago">
              {r.byMethod.length === 0 ? (
                <p className="text-sm text-slate-500">Sin ingresos en el período.</p>
              ) : (
                <RankBars data={r.byMethod.map((m) => ({ label: PAYMENT_METHOD[m.currency as PaymentMethod] ?? m.currency, value: m.amount }))} title="Ingresos por medio de pago" format={(v) => number(v, 2)} />
              )}
            </Card>
            <Card title="Detalle" padded={false}>
              <Table>
                <thead className="bg-slate-50">
                  <tr>
                    <Th>Período</Th>
                    <Th>Medio</Th>
                    <Th>Moneda</Th>
                    <Th align="right">Órdenes</Th>
                    <Th align="right">Ventas</Th>
                    <Th align="right">Devoluciones</Th>
                    <Th align="right">Neto</Th>
                    <Th align="right">Neto {currency}</Th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {r.rows.map((row, i) => (
                    <tr key={i}>
                      <Td>{row.period}</Td>
                      <Td>{PAYMENT_METHOD[row.method as PaymentMethod] ?? row.method}</Td>
                      <Td>{row.currency}</Td>
                      <Td align="right">{number(row.orders, 2)}</Td>
                      <Td align="right">{number(row.sales, 2)}</Td>
                      <Td align="right">{row.refunds ? number(row.refunds, 2) : '—'}</Td>
                      <Td align="right">{money(row.net, row.currency)}</Td>
                      <Td align="right">{money(row.netConverted, currency)}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </Card>
          </>
        )
      }}
    </Result>
  )
}

function MarginsView({ range }: { range: ReportRange }) {
  const q = useQuery({ queryKey: ['reports', 'margins', range], queryFn: () => reportsApi.margins(range) })
  return (
    <Result q={q}>
      {(r) => (
        <>
          <div className="grid gap-3 sm:grid-cols-3">
            <Stat label="Facturado" value={<span className="text-base">{amounts(r.revenueByCurrency)}</span>} />
            <Stat label="Margen bruto" value={<span className="text-base">{amounts(r.marginByCurrency)}</span>} tone="good" />
            <Stat label={`Margen convertido (${r.period.reportingCurrency})`} value={money(r.marginConverted, r.period.reportingCurrency)} />
          </div>
          <Card title="Por orden y venta" padded={false}>
            {r.rows.length === 0 ? (
              <div className="p-4">
                <EmptyState title="Sin operaciones en el período" />
              </div>
            ) : (
              <Table>
                <thead className="bg-slate-50">
                  <tr>
                    <Th>Operación</Th>
                    <Th>Fecha</Th>
                    <Th align="right">Ingreso</Th>
                    <Th align="right">Costo</Th>
                    <Th align="right">Margen</Th>
                    <Th align="right">%</Th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {r.rows.map((row) => (
                    <tr key={`${row.kind}-${row.id}`}>
                      <Td>
                        {row.kind === 'order' ? (
                          <Link to={`/orders/${row.id}`} className="font-medium text-brand-700 hover:underline">
                            {row.code}
                          </Link>
                        ) : (
                          <span className="font-medium">{row.code}</span>
                        )}
                        <span className="block max-w-md truncate text-xs text-slate-500">{row.description}</span>
                      </Td>
                      <Td>{date(row.dateUtc)}</Td>
                      <Td align="right">{money(row.revenue, row.currency)}</Td>
                      <Td align="right">{money(row.cost, row.currency)}</Td>
                      <Td align="right" className={row.margin < 0 ? 'text-rose-700' : 'font-medium'}>
                        {money(row.margin, row.currency)}
                      </Td>
                      <Td align="right">{percent(row.marginPercent)}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Card>
          <p className="text-xs text-slate-500">El costo usa el costo de los repuestos al momento de usarlos o venderlos. La mano de obra no tiene costo asignado.</p>
        </>
      )}
    </Result>
  )
}

function hours(h: number | null | undefined) {
  if (h === null || h === undefined) return '—'
  if (h < 48) return `${number(h, 1)} h`
  return `${number(h / 24, 1)} días`
}

function RepairTimesView({ range }: { range: ReportRange }) {
  const q = useQuery({ queryKey: ['reports', 'times', range], queryFn: () => reportsApi.repairTimes(range) })
  return (
    <Result q={q}>
      {(r) => (
        <>
          <div className="grid gap-3 sm:grid-cols-3">
            <Stat label="Órdenes analizadas" value={r.ordersAnalyzed} />
            <Stat label="Ingreso → listo (promedio)" value={hours(r.averageHoursToReady)} />
            <Stat label="Ingreso → entregado (promedio)" value={hours(r.averageHoursToDelivery)} />
          </div>
          {r.bottleneck ? <Alert tone="amber" title="Cuello de botella">Las órdenes pasan más tiempo en “{r.bottleneck}”.</Alert> : null}
          <Card title="Tiempo en cada estado">
            <RankBars data={r.byStatus.map((s) => ({ label: s.label, value: s.averageHours, hint: `mediana ${hours(s.medianHours)} · ${s.orders} órdenes` }))} title="Horas promedio por estado" format={hours} />
          </Card>
        </>
      )}
    </Result>
  )
}

function QuotesView({ range }: { range: ReportRange }) {
  const q = useQuery({ queryKey: ['reports', 'quotes', range], queryFn: () => reportsApi.quotes(range) })
  return (
    <Result q={q}>
      {(r) => (
        <>
          <div className="grid gap-3 sm:grid-cols-3">
            <Stat label="Presupuestos enviados" value={r.total} />
            <Stat label="Tasa de aprobación" value={percent(r.approvalRate)} tone={(r.approvalRate ?? 0) >= 0.6 ? 'good' : 'warn'} />
            <Stat label="Ticket promedio aprobado" value={<span className="text-base">{amounts(r.averageApprovedTotal)}</span>} />
          </div>
          <Card title="Resultado">
            <StackedBar
              title="Resultado de los presupuestos"
              parts={[
                { label: 'Aprobados', value: r.approved, color: 'bg-emerald-500' },
                { label: 'Rechazados', value: r.rejected, color: 'bg-rose-500' },
                { label: 'Vencidos', value: r.expired, color: 'bg-amber-400' },
                { label: 'Esperando respuesta', value: r.pending, color: 'bg-slate-300' },
              ]}
            />
          </Card>
        </>
      )}
    </Result>
  )
}

function TopIssuesView({ range }: { range: ReportRange }) {
  const q = useQuery({ queryKey: ['reports', 'issues', range], queryFn: () => reportsApi.topIssues(range) })
  const fmt = (v: number) => `${v} órdenes`
  return (
    <Result q={q}>
      {(r) => (
        <div className="grid gap-4 lg:grid-cols-3">
          <Card title="Fallas más frecuentes">
            <RankBars data={r.byCategory.map((x) => ({ label: x.key, value: x.orders, hint: x.averageTicket ? `ticket ${money(x.averageTicket, x.currency ?? 'ARS')}` : undefined }))} title="Fallas más frecuentes" format={fmt} />
          </Card>
          <Card title="Modelos más reparados">
            <RankBars data={r.byModel.map((x) => ({ label: x.key, value: x.orders }))} title="Modelos más reparados" format={fmt} color="bg-indigo-500" />
          </Card>
          <Card title="Marcas">
            <RankBars data={r.byBrand.map((x) => ({ label: x.key, value: x.orders }))} title="Marcas" format={fmt} color="bg-violet-500" />
          </Card>
        </div>
      )}
    </Result>
  )
}

function TechniciansView({ range }: { range: ReportRange }) {
  const q = useQuery({ queryKey: ['reports', 'techs', range], queryFn: () => reportsApi.technicians(range) })
  return (
    <Result q={q}>
      {(r) =>
        r.rows.length === 0 ? (
          <EmptyState title="Sin órdenes asignadas en el período" description="Asigná técnicos a las órdenes para medir su productividad." />
        ) : (
          <Card padded={false}>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Técnico</Th>
                  <Th align="right">Asignadas</Th>
                  <Th align="right">Entregadas</Th>
                  <Th align="right">Tiempo a listo</Th>
                  <Th align="right">Facturado</Th>
                  <Th align="right">Reingresos por garantía</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {r.rows.map((t) => (
                  <tr key={t.userId}>
                    <Td className="font-medium">{t.name}</Td>
                    <Td align="right">{t.assigned}</Td>
                    <Td align="right">{t.delivered}</Td>
                    <Td align="right">{hours(t.averageHoursToReady)}</Td>
                    <Td align="right">{amounts(t.revenue)}</Td>
                    <Td align="right">
                      {t.warrantyClaims} {t.reentryRate !== null && t.reentryRate !== undefined ? <span className="text-xs text-slate-500">({percent(t.reentryRate, 1)})</span> : null}
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </Card>
        )
      }
    </Result>
  )
}

function WarrantyView({ range }: { range: ReportRange }) {
  const q = useQuery({ queryKey: ['reports', 'warranty', range], queryFn: () => reportsApi.warranty(range) })
  return (
    <Result q={q}>
      {(r) => (
        <>
          <div className="grid gap-3 sm:grid-cols-3">
            <Stat label="Órdenes entregadas" value={r.deliveredOrders} />
            <Stat label="Reclamos de garantía" value={r.warrantyClaims} tone={r.warrantyClaims > 0 ? 'warn' : 'default'} />
            <Stat label="Tasa de reingreso" value={percent(r.reentryRate, 1)} tone={(r.reentryRate ?? 0) > 0.05 ? 'bad' : 'good'} />
          </div>
          <Card title="Repuestos con más reclamos" padded={false}>
            {r.byPart.length === 0 ? (
              <p className="p-4 text-sm text-slate-500">Sin reclamos asociados a repuestos.</p>
            ) : (
              <Table>
                <thead className="bg-slate-50">
                  <tr>
                    <Th>Repuesto</Th>
                    <Th align="right">Usos</Th>
                    <Th align="right">Reclamos</Th>
                    <Th align="right">Tasa</Th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {r.byPart.map((p) => (
                    <tr key={p.inventoryItemId}>
                      <Td>
                        {p.name} <span className="text-xs text-slate-400">{p.sku}</span>
                      </Td>
                      <Td align="right">{p.uses}</Td>
                      <Td align="right">{p.claims}</Td>
                      <Td align="right">{(p.claimRate ?? 0) > 0.1 ? <Badge tone="rose">{percent(p.claimRate, 1)}</Badge> : percent(p.claimRate, 1)}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Card>
        </>
      )}
    </Result>
  )
}

function FeedbackView({ range }: { range: ReportRange }) {
  const q = useQuery({ queryKey: ['reports', 'feedback', range], queryFn: () => reportsApi.feedback(range) })
  return (
    <Result q={q}>
      {(r) => (
        <>
          <div className="grid gap-3 sm:grid-cols-2">
            <Stat label="Respuestas" value={r.responses} />
            <Stat label="Puntaje promedio" value={r.averageScore ? `${number(r.averageScore, 1)} / 5` : '—'} tone={(r.averageScore ?? 5) >= 4 ? 'good' : 'warn'} />
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <Card title="Distribución">
              <RankBars data={[5, 4, 3, 2, 1].map((s) => ({ label: '★'.repeat(s), value: r.distribution[String(s)] ?? 0 }))} title="Distribución de puntajes" format={(v) => String(v)} color="bg-amber-400" />
            </Card>
            <Card title="Últimos comentarios">
              {r.latest.length === 0 ? (
                <p className="text-sm text-slate-500">Todavía no hay comentarios.</p>
              ) : (
                <ul className="divide-y divide-slate-100 text-sm">
                  {r.latest.map((f, i) => (
                    <li key={i} className="py-2">
                      <span className="text-amber-500" aria-label={`${f.score} de 5`}>
                        {'★'.repeat(f.score)}
                        <span className="text-slate-200">{'★'.repeat(5 - f.score)}</span>
                      </span>
                      <span className="ml-2 text-xs text-slate-500">
                        {f.orderCode} · {dateTime(f.createdAtUtc)}
                      </span>
                      {f.comment ? <p className="mt-0.5 text-slate-700">{f.comment}</p> : null}
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          </div>
        </>
      )}
    </Result>
  )
}

function InventoryView() {
  const q = useQuery({ queryKey: ['reports', 'inventory'], queryFn: reportsApi.inventory })
  return (
    <Result q={q}>
      {(r) => (
        <>
          <div className="grid gap-3 sm:grid-cols-2">
            <Stat label="Valor del stock (a costo)" value={<span className="text-base">{amounts(r.totalValue)}</span>} tone="good" />
            <Stat label="Ítems con stock bajo" value={r.lowStockCount} tone={r.lowStockCount > 0 ? 'warn' : 'good'} hint={r.lowStockCount > 0 ? <Link to="/inventory?lowStock=1" className="text-brand-700 underline">Ver y reponer</Link> : undefined} />
          </div>
          <Card padded={false}>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Ítem</Th>
                  <Th>Categoría</Th>
                  <Th align="right">Stock</Th>
                  <Th align="right">Reservado</Th>
                  <Th align="right">Mínimo</Th>
                  <Th align="right">Costo</Th>
                  <Th align="right">Valor</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {r.rows.map((row) => (
                  <tr key={row.id}>
                    <Td>
                      {row.name} <span className="text-xs text-slate-400">{row.sku}</span>
                    </Td>
                    <Td>{row.category ?? '—'}</Td>
                    <Td align="right">
                      {row.lowStock ? (
                        <Badge tone="amber" className="mr-1">
                          bajo
                        </Badge>
                      ) : null}
                      {row.onHand}
                    </Td>
                    <Td align="right">{row.reserved || '—'}</Td>
                    <Td align="right">{row.minStock}</Td>
                    <Td align="right">{money(row.unitCost, row.currency ?? 'ARS')}</Td>
                    <Td align="right">{money(row.value, row.currency ?? 'ARS')}</Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </Card>
        </>
      )}
    </Result>
  )
}
