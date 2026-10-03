import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { ordersApi, usersApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { OrderSearch, OrderStatus, Priority } from '../../api/types'
import { useSession } from '../../auth/session'
import { Money, PriorityBadge, StatusBadge } from '../../components/domain'
import { Badge, Button, Card, Checkbox, EmptyState, ErrorState, Loading, PageHeader, Pagination, SearchInput, Select, Table, Td, Th } from '../../components/ui'
import { dateTime, relative } from '../../lib/format'
import { useDebounced } from '../../lib/hooks'
import { ORDER_STATUS, ORDER_STATUS_FLOW, PRIORITY } from '../../lib/labels'
import { useEffect, useState } from 'react'

const TAKE = 25

export function OrdersPage() {
  const [params, setParams] = useSearchParams()
  const navigate = useNavigate()
  const { can, user } = useSession()
  const [q, setQ] = useState(params.get('q') ?? '')
  const debouncedQ = useDebounced(q.trim(), 300)

  const filters: OrderSearch = {
    q: params.get('q') ?? undefined,
    status: (params.get('status') as OrderStatus) || undefined,
    technicianId: params.get('technicianId') ?? undefined,
    mine: params.get('mine') === 'true' || undefined,
    onlyOpen: params.get('onlyOpen') === 'true' || undefined,
    onlyOverdue: params.get('onlyOverdue') === 'true' || undefined,
    priority: (params.get('priority') as Priority) || undefined,
    customerId: params.get('customerId') ?? undefined,
    sortBy: params.get('sortBy') ?? 'createdAt',
    sortDir: (params.get('sortDir') as 'asc' | 'desc') ?? 'desc',
    skip: Number(params.get('skip') ?? 0),
    take: TAKE,
  }

  function update(changes: Record<string, string | undefined | null | boolean>) {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(changes)) {
      if (v === undefined || v === null || v === '' || v === false) next.delete(k)
      else next.set(k, String(v))
    }
    if (!('skip' in changes)) next.delete('skip')
    setParams(next, { replace: true })
  }

  useEffect(() => {
    if ((params.get('q') ?? '') !== debouncedQ) update({ q: debouncedQ })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedQ])

  const orders = useQuery({ queryKey: ['orders', 'list', filters], queryFn: () => ordersApi.search(filters), placeholderData: keepPreviousData })
  const techs = useQuery({ queryKey: ['users', 'assignable'], queryFn: usersApi.assignable, staleTime: 5 * 60_000 })

  return (
    <div>
      <PageHeader
        title="Órdenes de reparación"
        subtitle="Buscá por número (#123), cliente, teléfono, equipo, IMEI o falla."
        actions={
          <>
            <Button onClick={() => navigate('/orders/board')}>Ver tablero</Button>
            {can('orders.manage') ? (
              <Button variant="primary" onClick={() => navigate('/orders/new')}>
                + Nueva orden
              </Button>
            ) : null}
          </>
        }
      />

      <Card className="mb-4">
        <div className="grid gap-3 md:grid-cols-[2fr_1fr_1fr_1fr]">
          <SearchInput value={q} onChange={setQ} placeholder="Buscar órdenes…" autoFocus />
          <Select value={filters.status ?? ''} onChange={(e) => update({ status: e.target.value })} aria-label="Estado">
            <option value="">Todos los estados</option>
            {ORDER_STATUS_FLOW.map((s) => (
              <option key={s} value={s}>
                {ORDER_STATUS[s].label}
              </option>
            ))}
          </Select>
          <Select value={filters.technicianId ?? ''} onChange={(e) => update({ technicianId: e.target.value, mine: undefined })} aria-label="Técnico">
            <option value="">Todos los técnicos</option>
            {(techs.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>
                {t.displayName}
              </option>
            ))}
          </Select>
          <Select value={filters.priority ?? ''} onChange={(e) => update({ priority: e.target.value })} aria-label="Prioridad">
            <option value="">Cualquier prioridad</option>
            {(Object.keys(PRIORITY) as Priority[]).map((p) => (
              <option key={p} value={p}>
                {PRIORITY[p].label}
              </option>
            ))}
          </Select>
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-4">
          <Checkbox label="Solo abiertas" checked={!!filters.onlyOpen} onChange={(e) => update({ onlyOpen: e.target.checked })} />
          <Checkbox label="Atrasadas" checked={!!filters.onlyOverdue} onChange={(e) => update({ onlyOverdue: e.target.checked })} />
          {user?.role === 'Tech' || filters.mine ? <Checkbox label="Asignadas a mí" checked={!!filters.mine} onChange={(e) => update({ mine: e.target.checked, technicianId: undefined })} /> : null}
          <label className="ml-auto flex items-center gap-2 text-sm text-slate-600">
            Ordenar
            <Select
              className="h-8 w-auto"
              value={`${filters.sortBy}:${filters.sortDir}`}
              onChange={(e) => {
                const [sortBy, sortDir] = e.target.value.split(':')
                update({ sortBy, sortDir })
              }}
            >
              <option value="createdAt:desc">Más recientes</option>
              <option value="createdAt:asc">Más antiguas</option>
              <option value="promisedAt:asc">Fecha prometida</option>
              <option value="priority:desc">Prioridad</option>
              <option value="updatedAt:desc">Última actualización</option>
            </Select>
          </label>
          {filters.customerId ? (
            <Button size="sm" variant="ghost" onClick={() => update({ customerId: undefined })}>
              Quitar filtro de cliente ✕
            </Button>
          ) : null}
        </div>
      </Card>

      <Card padded={false}>
        {orders.isLoading ? (
          <Loading />
        ) : orders.isError ? (
          <div className="p-4">
            <ErrorState error={errorMessage(orders.error)} onRetry={() => orders.refetch()} />
          </div>
        ) : orders.data!.items.length === 0 ? (
          <div className="p-4">
            <EmptyState title="No hay órdenes con esos filtros" description="Probá con otra búsqueda o cargá una nueva orden." />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Orden</Th>
                  <Th>Cliente</Th>
                  <Th>Equipo / falla</Th>
                  <Th>Estado</Th>
                  <Th>Técnico</Th>
                  <Th>Prometida</Th>
                  <Th align="right">Saldo</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 bg-white">
                {orders.data!.items.map((o) => (
                  <tr key={o.id} className="cursor-pointer hover:bg-slate-50" onClick={() => navigate(`/orders/${o.id}`)}>
                    <Td className="whitespace-nowrap">
                      <Link to={`/orders/${o.id}`} className="font-semibold text-brand-700 hover:underline" onClick={(e) => e.stopPropagation()}>
                        {o.code}
                      </Link>
                      <div className="text-xs text-slate-500" title={dateTime(o.createdAtUtc)}>
                        {relative(o.createdAtUtc)}
                      </div>
                    </Td>
                    <Td>
                      <div className="font-medium text-slate-800">{o.customerName}</div>
                      <div className="text-xs text-slate-500">{o.customerPhone}</div>
                    </Td>
                    <Td className="max-w-xs">
                      <div className="truncate text-slate-800">{o.deviceLabel}</div>
                      <div className="truncate text-xs text-slate-500">{o.issueDescription}</div>
                    </Td>
                    <Td>
                      <div className="flex flex-wrap items-center gap-1">
                        <StatusBadge status={o.status} />
                        <PriorityBadge priority={o.priority} hideNormal />
                        {o.isWarrantyClaim ? <Badge tone="violet">Garantía</Badge> : null}
                      </div>
                    </Td>
                    <Td className="whitespace-nowrap text-slate-600">{o.assignedTechnicianName ?? '—'}</Td>
                    <Td className={o.isOverdue ? 'whitespace-nowrap font-medium text-rose-700' : 'whitespace-nowrap text-slate-600'}>
                      {o.promisedAtUtc ? relative(o.promisedAtUtc) : '—'}
                    </Td>
                    <Td align="right">{o.balanceDue > 0 ? <Money value={o.balanceDue} currency={o.currency} className="font-medium text-rose-700" /> : <span className="text-slate-400">—</span>}</Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <div className="px-4 pb-3">
              <Pagination skip={filters.skip ?? 0} take={TAKE} total={orders.data!.total} onChange={(skip) => update({ skip: String(skip) })} />
            </div>
          </>
        )}
      </Card>
    </div>
  )
}
