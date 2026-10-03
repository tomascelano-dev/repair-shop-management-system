import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { dashboardApi, ordersApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import { useSession } from '../../auth/session'
import { BarChart } from '../../components/charts'
import { PriorityBadge, StatusBadge } from '../../components/domain'
import { Alert, Button, Card, EmptyState, ErrorState, Loading, PageHeader, Stat, Table, Td, Th } from '../../components/ui'
import { amounts, dateOnly, money, relative } from '../../lib/format'
import { OPEN_STATUSES } from '../../lib/labels'

export function DashboardPage() {
  const { user, can, shops } = useSession()
  const navigate = useNavigate()
  const summary = useQuery({ queryKey: ['dashboard', 'summary'], queryFn: dashboardApi.summary, refetchInterval: 60_000 })
  const revenue = useQuery({ queryKey: ['dashboard', 'revenue', 30], queryFn: () => dashboardApi.revenue(30), enabled: can('reports') })
  const consolidated = useQuery({ queryKey: ['dashboard', 'consolidated'], queryFn: dashboardApi.consolidated, enabled: can('reports') && shops.length > 1 })
  const mine = useQuery({
    queryKey: ['orders', 'mine'],
    queryFn: () => ordersApi.search({ mine: true, onlyOpen: true, sortBy: 'promisedAt', sortDir: 'asc', take: 10 }),
    enabled: user?.role === 'Tech',
  })

  if (summary.isLoading) return <Loading />
  if (summary.isError) return <ErrorState error={errorMessage(summary.error)} onRetry={() => summary.refetch()} />
  const s = summary.data!
  const currency = s.reportingCurrency ?? 'ARS'

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Hola, ${user?.displayName?.split(' ')[0] ?? ''}`}
        subtitle={`${user?.shopName ?? ''} · actualizado ${relative(s.generatedAtUtc)}`}
        actions={
          <>
            {can('orders.manage') ? (
              <Button variant="primary" onClick={() => navigate('/orders/new')}>
                + Nueva orden
              </Button>
            ) : null}
            {can('sales') ? <Button onClick={() => navigate('/pos')}>Punto de venta</Button> : null}
          </>
        }
      />

      {can('sales') && !s.cashSessionOpen ? (
        <Alert tone="amber" title="La caja está cerrada">
          Abrila para registrar cobros en efectivo. <Link to="/cash" className="font-medium underline">Ir a Caja</Link>
        </Alert>
      ) : null}
      {s.missingRates && s.missingRates.length > 0 ? (
        <Alert tone="amber" title="Faltan cotizaciones">
          No hay tipo de cambio cargado para {s.missingRates.join(', ')}; esos montos no se suman al total en {currency}.
        </Alert>
      ) : null}

      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <Stat label="Órdenes abiertas" value={s.openOrders} onClick={() => navigate('/orders?onlyOpen=true')} />
        <Stat label="Listas para retirar" value={s.readyOrders} hint={s.readyNotPickedUp > 0 ? `${s.readyNotPickedUp} hace más de 7 días` : undefined} tone={s.readyNotPickedUp > 0 ? 'warn' : 'good'} onClick={() => navigate('/orders?status=Ready')} />
        <Stat label="Atrasadas" value={s.overdueOrders} hint="Pasaron la fecha prometida" tone={s.overdueOrders > 0 ? 'bad' : 'default'} onClick={() => navigate('/orders?onlyOverdue=true')} />
        <Stat label="Estancadas" value={s.staleOrders} hint="Sin cambios de estado hace días" tone={s.staleOrders > 0 ? 'warn' : 'default'} onClick={() => navigate('/orders/board')} />
        <Stat label="Presupuestos sin respuesta" value={s.quotesPendingDecision} tone={s.quotesPendingDecision > 0 ? 'warn' : 'default'} onClick={() => navigate('/orders?status=Diagnosing')} />
        {user?.role === 'Tech' ? <Stat label="Asignadas a mí" value={s.myOpenOrders} onClick={() => navigate('/orders?mine=true&onlyOpen=true')} /> : null}
        <Stat label="Stock bajo" value={s.lowStockItems} tone={s.lowStockItems > 0 ? 'warn' : 'default'} onClick={() => navigate('/inventory?lowStock=true')} />
        {can('sales') || can('reports') ? (
          <>
            <Stat label="Cobrado hoy" value={s.today?.converted !== null && s.today?.converted !== undefined ? money(s.today.converted, currency) : amounts(s.today?.byCurrency)} hint={s.today && s.today.byCurrency.length > 1 ? amounts(s.today.byCurrency) : undefined} />
            <Stat label="Cobrado en el mes" value={s.month?.converted !== null && s.month?.converted !== undefined ? money(s.month.converted, currency) : amounts(s.month?.byCurrency)} hint={s.month && s.month.byCurrency.length > 1 ? amounts(s.month.byCurrency) : undefined} />
          </>
        ) : null}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card title="Órdenes por estado" className="lg:col-span-1">
          <ul className="space-y-2 text-sm">
            {OPEN_STATUSES.map((st) => (
              <li key={st}>
                <Link to={`/orders?status=${st}`} className="flex items-center justify-between rounded-md px-1 py-0.5 hover:bg-slate-50">
                  <StatusBadge status={st} />
                  <span className="font-medium tabular-nums">{s.statusCounts?.[st] ?? 0}</span>
                </Link>
              </li>
            ))}
          </ul>
        </Card>

        {can('reports') ? (
          <Card title={`Ingresos de los últimos 30 días (${currency})`} className="lg:col-span-2">
            {revenue.isLoading ? (
              <Loading />
            ) : (
              <BarChart
                title="Ingresos diarios"
                data={(revenue.data ?? []).map((p) => ({ label: dateOnly(p.date).slice(0, 5), value: p.converted ?? 0, hint: p.byCurrency.length > 1 ? amounts(p.byCurrency) : undefined }))}
                format={(v) => money(v, currency).replace(/,00$/, '')}
              />
            )}
          </Card>
        ) : user?.role === 'Tech' ? (
          <Card title="Mis órdenes abiertas" className="lg:col-span-2" actions={<Link to="/orders/board?mine=true" className="text-sm text-brand-700 hover:underline">Ver tablero</Link>}>
            {mine.isLoading ? (
              <Loading />
            ) : (mine.data?.items.length ?? 0) === 0 ? (
              <EmptyState title="No tenés órdenes asignadas" />
            ) : (
              <Table>
                <thead>
                  <tr>
                    <Th>Orden</Th>
                    <Th>Equipo</Th>
                    <Th>Estado</Th>
                    <Th>Prometida</Th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {mine.data!.items.map((o) => (
                    <tr key={o.id} className="cursor-pointer hover:bg-slate-50" onClick={() => navigate(`/orders/${o.id}`)}>
                      <Td className="font-medium">
                        {o.code} <PriorityBadge priority={o.priority} hideNormal />
                      </Td>
                      <Td>{o.deviceLabel}</Td>
                      <Td>
                        <StatusBadge status={o.status} />
                      </Td>
                      <Td className={o.isOverdue ? 'text-rose-700' : undefined}>{o.promisedAtUtc ? relative(o.promisedAtUtc) : '—'}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Card>
        ) : null}
      </div>

      {s.technicians && s.technicians.length > 0 ? (
        <Card title="Carga por técnico">
          <Table>
            <thead>
              <tr>
                <Th>Técnico</Th>
                <Th align="right">Abiertas</Th>
                <Th align="right">Atrasadas</Th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {s.technicians.map((t) => (
                <tr key={t.userId}>
                  <Td>
                    <Link to={`/orders?technicianId=${t.userId}&onlyOpen=true`} className="hover:underline">
                      {t.name}
                    </Link>
                  </Td>
                  <Td align="right">{t.openOrders}</Td>
                  <Td align="right" className={t.overdueOrders > 0 ? 'font-medium text-rose-700' : undefined}>
                    {t.overdueOrders}
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        </Card>
      ) : null}

      {consolidated.data && consolidated.data.branches.length > 1 ? (
        <Card title={`Todas las sucursales · mes actual (${consolidated.data.reportingCurrency})`}>
          <Table>
            <thead>
              <tr>
                <Th>Sucursal</Th>
                <Th align="right">Abiertas</Th>
                <Th align="right">Listas</Th>
                <Th align="right">Atrasadas</Th>
                <Th align="right">Stock bajo</Th>
                <Th align="right">Cobrado</Th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {consolidated.data.branches.map((b) => (
                <tr key={b.shopId}>
                  <Td className="font-medium">{b.shopName}</Td>
                  <Td align="right">{b.openOrders}</Td>
                  <Td align="right">{b.readyOrders}</Td>
                  <Td align="right">{b.overdueOrders}</Td>
                  <Td align="right">{b.lowStockItems}</Td>
                  <Td align="right">{b.month.converted !== null && b.month.converted !== undefined ? money(b.month.converted, consolidated.data!.reportingCurrency) : amounts(b.month.byCurrency)}</Td>
                </tr>
              ))}
              <tr className="bg-slate-50 font-medium">
                <Td>Total</Td>
                <Td colSpan={4} />
                <Td align="right">{money(consolidated.data.monthTotalConverted, consolidated.data.reportingCurrency)}</Td>
              </tr>
            </tbody>
          </Table>
        </Card>
      ) : null}
    </div>
  )
}
