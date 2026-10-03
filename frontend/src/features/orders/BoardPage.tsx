import { useState, type ReactNode } from 'react'
import { DndContext, PointerSensor, KeyboardSensor, useDraggable, useDroppable, useSensor, useSensors, type DragEndEvent } from '@dnd-kit/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { ordersApi, usersApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { OrderCard, OrderStatus } from '../../api/types'
import { useSession } from '../../auth/session'
import { Money, PriorityBadge } from '../../components/domain'
import { Badge, Button, Checkbox, ErrorState, Loading, PageHeader, SearchInput, Select } from '../../components/ui'
import { cn } from '../../lib/cn'
import { relative } from '../../lib/format'
import { useDebounced } from '../../lib/hooks'
import { ORDER_STATUS, TONE_CLASSES } from '../../lib/labels'

/**
 * Kanban of open orders. Dragging a card moves the order to that status (same rules as the detail page:
 * quote approval, QA and payments are validated by the API, which explains what is missing).
 */
export function BoardPage() {
  const [params, setParams] = useSearchParams()
  const { user } = useSession()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const debounced = useDebounced(q.trim(), 300)
  const mine = params.get('mine') === 'true'
  const technicianId = params.get('technicianId') ?? undefined
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }), useSensor(KeyboardSensor))

  const board = useQuery({ queryKey: ['orders', 'board', debounced, mine, technicianId], queryFn: () => ordersApi.board({ q: debounced, mine, technicianId }), refetchInterval: 30_000 })
  const techs = useQuery({ queryKey: ['users', 'assignable'], queryFn: usersApi.assignable, staleTime: 5 * 60_000 })

  const move = useMutation({
    mutationFn: ({ id, status }: { id: string; status: OrderStatus }) => ordersApi.changeStatus(id, { status, enqueueOutbox: true }),
    onSuccess: (res) => {
      toast.success(`Orden movida a “${ORDER_STATUS[res.toStatus].label}”`, res.outboxItemId ? { description: 'Se avisó al cliente.' } : undefined)
    },
    onError: (err) => toast.error('No se pudo mover la orden', { description: errorMessage(err) }),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
  })

  function onDragEnd(e: DragEndEvent) {
    const card = e.active.data.current as OrderCard | undefined
    const target = e.over?.id as OrderStatus | undefined
    if (!card || !target || card.status === target) return
    move.mutate({ id: card.id, status: target })
  }

  return (
    <div>
      <PageHeader
        title="Tablero del taller"
        subtitle="Arrastrá una tarjeta para cambiar el estado. Las tarjetas rojas están atrasadas; las ámbar, estancadas."
        actions={<Button onClick={() => navigate('/orders')}>Ver lista</Button>}
      />
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchInput value={q} onChange={setQ} placeholder="Filtrar tarjetas…" className="w-full sm:w-72" />
        <Select
          className="w-full sm:w-56"
          value={technicianId ?? ''}
          onChange={(e) => {
            const next = new URLSearchParams(params)
            if (e.target.value) next.set('technicianId', e.target.value)
            else next.delete('technicianId')
            next.delete('mine')
            setParams(next, { replace: true })
          }}
          aria-label="Técnico"
        >
          <option value="">Todos los técnicos</option>
          {(techs.data ?? []).map((t) => (
            <option key={t.id} value={t.id}>
              {t.displayName}
            </option>
          ))}
        </Select>
        {user?.role === 'Tech' || mine ? (
          <Checkbox
            label="Solo mis órdenes"
            checked={mine}
            onChange={(e) => {
              const next = new URLSearchParams(params)
              if (e.target.checked) next.set('mine', 'true')
              else next.delete('mine')
              next.delete('technicianId')
              setParams(next, { replace: true })
            }}
          />
        ) : null}
      </div>

      {board.isLoading ? (
        <Loading />
      ) : board.isError ? (
        <ErrorState error={errorMessage(board.error)} onRetry={() => board.refetch()} />
      ) : (
        <DndContext sensors={sensors} onDragEnd={onDragEnd}>
          <div className="flex gap-3 overflow-x-auto pb-4">
            {board.data!.columns.map((col) => (
              <Column key={col.status} status={col.status} label={col.label} count={col.count}>
                {col.items.map((card) => (
                  <Card key={card.id} card={card} />
                ))}
              </Column>
            ))}
          </div>
        </DndContext>
      )}
    </div>
  )
}

function Column({ status, label, count, children }: { status: OrderStatus; label: string; count: number; children: ReactNode }) {
  const { setNodeRef, isOver } = useDroppable({ id: status })
  const tone = ORDER_STATUS[status]?.tone ?? 'slate'
  return (
    <section ref={setNodeRef} aria-label={label} className={cn('flex w-72 shrink-0 flex-col rounded-xl border bg-slate-100/70 transition', isOver ? 'border-brand-400 bg-brand-50' : 'border-slate-200')}>
      <header className="flex items-center justify-between px-3 py-2">
        <span className={cn('rounded-full px-2 py-0.5 text-xs font-semibold ring-1 ring-inset', TONE_CLASSES[tone])}>{label}</span>
        <span className="text-xs font-medium text-slate-500">{count}</span>
      </header>
      <div className="flex min-h-24 flex-1 flex-col gap-2 px-2 pb-3">{children}</div>
    </section>
  )
}

function Card({ card }: { card: OrderCard }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({ id: card.id, data: card })
  const style = transform ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)` } : undefined
  return (
    <article
      ref={setNodeRef}
      style={style}
      {...listeners}
      {...attributes}
      className={cn(
        'cursor-grab rounded-lg border bg-white p-3 text-sm shadow-sm active:cursor-grabbing',
        isDragging && 'z-10 opacity-80 shadow-lg',
        card.isOverdue ? 'border-rose-300' : card.isStale ? 'border-amber-300' : 'border-slate-200'
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <Link to={`/orders/${card.id}`} className="font-semibold text-brand-700 hover:underline" onPointerDown={(e) => e.stopPropagation()}>
          {card.code}
        </Link>
        <div className="flex gap-1">
          <PriorityBadge priority={card.priority} hideNormal />
          {card.isWarrantyClaim ? <Badge tone="violet">Garantía</Badge> : null}
        </div>
      </div>
      <p className="mt-1 font-medium text-slate-800">{card.deviceLabel}</p>
      <p className="line-clamp-2 text-xs text-slate-500">{card.issueDescription}</p>
      <p className="mt-1 text-xs text-slate-600">{card.customerName}</p>
      <div className="mt-2 flex items-center justify-between text-xs text-slate-500">
        <span>{card.assignedTechnicianName ?? 'Sin asignar'}</span>
        <span className={card.isOverdue ? 'font-medium text-rose-700' : undefined}>{card.promisedAtUtc ? relative(card.promisedAtUtc) : `${card.ageDays} d`}</span>
      </div>
      {card.balanceDue > 0 && card.status === 'Ready' ? (
        <p className="mt-1 text-xs">
          Saldo: <Money value={card.balanceDue} currency={card.currency} className="font-medium text-rose-700" />
        </p>
      ) : null}
    </article>
  )
}
