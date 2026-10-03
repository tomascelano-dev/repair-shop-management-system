import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { ordersApi, usersApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { OrderStatus, Priority, RepairOrder, UnlockSecret } from '../../api/types'
import { useSession } from '../../auth/session'
import { Money, PriorityBadge, SignaturePad, StatusBadge } from '../../components/domain'
import { Alert, Badge, Button, Card, ConfirmDialog, ErrorState, Field, Input, KeyValue, Loading, Modal, Select, Tabs, Textarea } from '../../components/ui'
import { dateTime, fromLocalInput, relative, toLocalInput } from '../../lib/format'
import { openPdf, printPdf } from '../../lib/files'
import { ORDER_STATUS, PRIORITY, UNLOCK_METHOD } from '../../lib/labels'
import { transitionBlocker } from './rules'
import { StatusDialog } from './StatusDialog'
import { PartsPanel } from './panels/PartsPanel'
import { PaymentsPanel } from './panels/PaymentsPanel'
import { QuotesPanel } from './panels/QuotesPanel'
import { ChecklistsPanel, HistoryPanel, MessagesPanel, NotesPanel, PhotosPanel, SuggestionsPanel } from './panels/RecordsPanels'

type Tab = 'quotes' | 'parts' | 'payments' | 'checks' | 'notes' | 'photos' | 'messages' | 'history' | 'suggestions'

export function OrderDetailPage() {
  const { id = '' } = useParams()
  const [params, setParams] = useSearchParams()
  const navigate = useNavigate()
  const { can, role } = useSession()
  const queryClient = useQueryClient()
  const order = useQuery({ queryKey: ['order', id], queryFn: () => ordersApi.get(id) })
  const tab = (params.get('tab') as Tab) || 'quotes'
  const [target, setTarget] = useState<OrderStatus | null>(null)
  const [planning, setPlanning] = useState(false)
  const [editing, setEditing] = useState(false)
  const [unlock, setUnlock] = useState<UnlockSecret | null>(null)
  const [signing, setSigning] = useState(false)
  const [warranty, setWarranty] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const reveal = useMutation({
    mutationFn: () => ordersApi.revealUnlock(id),
    onSuccess: (u) => setUnlock(u),
    onError: (err) => toast.error(errorMessage(err)),
  })

  const regenerate = useMutation({
    mutationFn: () => ordersApi.regenerateToken(id),
    onSuccess: () => {
      toast.success('Se generó un nuevo link de seguimiento. El anterior dejó de funcionar.')
      void queryClient.invalidateQueries({ queryKey: ['order', id] })
    },
  })

  const remove = useMutation({
    mutationFn: () => ordersApi.remove(id),
    onSuccess: () => {
      toast.success('Orden eliminada')
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
      navigate('/orders')
    },
    onError: (err) => toast.error('No se pudo eliminar', { description: errorMessage(err) }),
  })

  if (order.isLoading) return <Loading />
  if (order.isError) return <ErrorState error={errorMessage(order.error)} onRetry={() => order.refetch()} />
  const o = order.data!
  const final = o.status === 'Delivered' || o.status === 'Cancelled'
  const canChange = can('orders.work') || (can('sales') && o.status === 'Ready')
  const next = o.allowedNextStatuses.filter((s) => (role === 'Cashier' ? s === 'Delivered' : true))

  function copy(text: string, label: string) {
    void navigator.clipboard.writeText(text).then(() => toast.success(`${label} copiado`))
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm text-slate-500">
            <Link to="/orders" className="hover:underline">
              Órdenes
            </Link>{' '}
            / {o.code}
          </p>
          <h1 className="mt-1 flex flex-wrap items-center gap-2 text-2xl font-semibold text-slate-900">
            {o.code}
            <StatusBadge status={o.status} />
            <PriorityBadge priority={o.priority} hideNormal />
            {o.isWarrantyClaim ? <Badge tone="violet">Reingreso por garantía</Badge> : null}
            {o.isOverdue ? <Badge tone="rose">Atrasada</Badge> : null}
            {o.underWarranty ? <Badge tone="green">En garantía hasta {dateTime(o.warrantyExpiresAtUtc).slice(0, 10)}</Badge> : null}
          </h1>
          <p className="mt-1 text-sm text-slate-600">
            {o.deviceLabel} · {o.customerName} · ingresó {relative(o.createdAtUtc)}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => void openPdf(`/orders/${o.id}/documents/intake`)}>Comprobante</Button>
          <Button onClick={() => void printPdf(`/orders/${o.id}/documents/label`)}>Etiqueta</Button>
          {o.status === 'Delivered' ? <Button onClick={() => void openPdf(`/orders/${o.id}/documents/warranty`)}>Garantía</Button> : null}
        </div>
      </div>

      {canChange && next.length > 0 ? (
        <Card>
          <div className="flex flex-wrap items-center gap-2">
            <span className="mr-1 text-sm font-medium text-slate-700">Pasar a:</span>
            {next.map((s) => {
              const blocker = transitionBlocker(o, s)
              return (
                <Button
                  key={s}
                  variant={s === 'Cancelled' ? 'ghost' : blocker ? 'default' : 'primary'}
                  className={s === 'Cancelled' ? 'text-rose-700' : undefined}
                  title={blocker ?? undefined}
                  onClick={() => {
                    if (blocker) {
                      toast.warning(blocker)
                      setParams({ tab: blocker.includes('presupuesto') ? 'quotes' : 'checks' }, { replace: true })
                      return
                    }
                    setTarget(s)
                  }}
                >
                  {ORDER_STATUS[s].label}
                  {blocker ? ' ⚠' : ''}
                </Button>
              )
            })}
          </div>
          {o.status === 'Ready' && o.balanceDue > 0 ? (
            <p className="mt-2 text-sm text-rose-700">
              Saldo pendiente <Money value={o.balanceDue} currency={o.currency} className="font-semibold" />: registralo en “Pagos” antes de entregar.
            </p>
          ) : null}
        </Card>
      ) : null}

      <div className="grid items-start gap-4 lg:grid-cols-3">
        <Card
          title="Cliente y equipo"
          className="lg:col-span-2"
          actions={
            can('orders.manage') && !final ? (
              <Button size="sm" variant="ghost" onClick={() => setEditing(true)}>
                Editar falla
              </Button>
            ) : null
          }
        >
          <KeyValue
            items={[
              { label: 'Cliente', value: <Link to={`/customers/${o.customerId}`} className="text-brand-700 hover:underline">{o.customerName}</Link> },
              {
                label: 'Teléfono',
                value: (
                  <span className="flex items-center gap-2">
                    <a href={`tel:${o.customerPhone}`} className="hover:underline">
                      {o.customerPhone}
                    </a>
                  </span>
                ),
              },
              { label: 'Equipo', value: o.deviceLabel },
              { label: 'IMEI', value: o.deviceImei ?? '—' },
              { label: 'Falla reportada', value: <span className="whitespace-pre-wrap">{o.issueDescription}</span> },
              { label: 'Categoría', value: o.issueCategory ?? '—' },
              { label: 'Notas internas', value: o.notes ?? '—', hidden: !o.notes },
              { label: 'Motivo de cancelación', value: o.cancellationReason ?? '—', hidden: !o.cancellationReason },
            ]}
          />
          <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-slate-100 pt-3 text-sm">
            <span className="text-slate-500">Seguimiento del cliente:</span>
            <a href={o.trackingUrl} target="_blank" rel="noreferrer" className="text-brand-700 hover:underline">
              abrir
            </a>
            <Button size="sm" variant="ghost" onClick={() => copy(o.trackingUrl, 'Link')}>
              Copiar link
            </Button>
            {can('orders.manage') ? (
              <Button size="sm" variant="ghost" loading={regenerate.isPending} onClick={() => regenerate.mutate()}>
                Generar link nuevo
              </Button>
            ) : null}
          </div>
        </Card>

        <div className="space-y-4">
          <Card
            title="Plan de trabajo"
            actions={
              can('orders.work') && !final ? (
                <Button size="sm" variant="ghost" onClick={() => setPlanning(true)}>
                  Editar
                </Button>
              ) : null
            }
          >
            <KeyValue
              className="sm:grid-cols-1"
              items={[
                { label: 'Técnico', value: o.assignedTechnicianName ?? 'Sin asignar' },
                { label: 'Prioridad', value: PRIORITY[o.priority].label },
                { label: 'Prometida', value: o.promisedAtUtc ? `${dateTime(o.promisedAtUtc)} (${relative(o.promisedAtUtc)})` : '—' },
                { label: 'Último cambio de estado', value: relative(o.lastStatusChangeAtUtc) },
              ]}
            />
          </Card>

          <Card title="Saldo">
            <p className="text-2xl font-semibold">
              <Money value={o.balanceDue} currency={o.currency} colorize />
            </p>
            <p className="text-xs text-slate-500">
              Total <Money value={o.totalAmount} currency={o.currency} /> · pagado <Money value={o.paidAmount} currency={o.currency} />
            </p>
            {!o.hasApprovedQuote && !o.isWarrantyClaim ? <p className="mt-1 text-xs text-amber-700">Sin presupuesto aprobado</p> : null}
          </Card>

          <Card title="Seguridad y firmas">
            <div className="space-y-2 text-sm">
              <p>
                Desbloqueo: <strong>{UNLOCK_METHOD[o.unlockMethod]}</strong>{' '}
                {o.hasUnlockSecret && can('orders.work') ? (
                  unlock ? (
                    <code className="ml-1 rounded bg-slate-100 px-1.5 py-0.5">{unlock.value}</code>
                  ) : (
                    <Button size="sm" variant="ghost" loading={reveal.isPending} onClick={() => reveal.mutate()}>
                      Ver código
                    </Button>
                  )
                ) : !o.hasUnlockSecret && o.unlockMethod !== 'None' ? (
                  <span className="text-xs text-slate-500">(borrado al finalizar)</span>
                ) : null}
              </p>
              <p>Firma de recepción: {o.hasReceptionSignature ? `✓ ${o.receptionSignedByName ?? ''}` : <span className="text-slate-500">no</span>}</p>
              <p>
                Firma de entrega: {o.hasDeliverySignature ? `✓ ${o.deliverySignedByName ?? ''}` : <span className="text-slate-500">no</span>}
                {!o.hasDeliverySignature && (o.status === 'Ready' || o.status === 'Delivered') && can('orders.manage') ? (
                  <Button size="sm" variant="ghost" onClick={() => setSigning(true)}>
                    Firmar
                  </Button>
                ) : null}
              </p>
            </div>
          </Card>

          <div className="flex flex-wrap gap-2">
            {o.status === 'Delivered' && can('orders.manage') ? (
              <Button size="sm" onClick={() => setWarranty(true)}>
                Reingreso por garantía
              </Button>
            ) : null}
            {role === 'Admin' && o.status === 'Received' ? (
              <Button size="sm" variant="ghost" className="text-rose-700" onClick={() => setConfirmDelete(true)}>
                Eliminar orden
              </Button>
            ) : null}
          </div>
        </div>
      </div>

      <Tabs<Tab>
        value={tab}
        onChange={(t) => setParams({ tab: t }, { replace: true })}
        tabs={[
          { value: 'quotes', label: 'Presupuestos' },
          { value: 'parts', label: 'Repuestos' },
          { value: 'payments', label: 'Pagos' },
          { value: 'checks', label: 'Checklists' },
          { value: 'notes', label: 'Notas' },
          { value: 'photos', label: 'Fotos', count: o.photosCount || undefined },
          { value: 'messages', label: 'Mensajes' },
          { value: 'history', label: 'Historial' },
          { value: 'suggestions', label: 'Sugerencias', hidden: !can('orders.work') },
        ]}
      />
      <div>
        {tab === 'quotes' ? <QuotesPanel order={o} /> : null}
        {tab === 'parts' ? <PartsPanel order={o} /> : null}
        {tab === 'payments' ? <PaymentsPanel order={o} /> : null}
        {tab === 'checks' ? <ChecklistsPanel order={o} /> : null}
        {tab === 'notes' ? <NotesPanel order={o} /> : null}
        {tab === 'photos' ? <PhotosPanel order={o} /> : null}
        {tab === 'messages' ? <MessagesPanel order={o} /> : null}
        {tab === 'history' ? <HistoryPanel order={o} /> : null}
        {tab === 'suggestions' ? <SuggestionsPanel order={o} /> : null}
      </div>

      {target ? <StatusDialog order={o} target={target} onClose={() => setTarget(null)} /> : null}
      {planning ? <PlanDialog order={o} onClose={() => setPlanning(false)} /> : null}
      {editing ? <EditIssueDialog order={o} onClose={() => setEditing(false)} /> : null}
      {signing ? <DeliverySignatureDialog order={o} onClose={() => setSigning(false)} /> : null}
      {warranty ? <WarrantyClaimDialog order={o} onClose={() => setWarranty(false)} /> : null}
      <ConfirmDialog
        open={confirmDelete}
        title="Eliminar orden"
        message="Solo se pueden eliminar órdenes sin pagos, repuestos ni presupuestos. Si ya hubo movimientos, cancelala."
        confirmLabel="Eliminar"
        danger
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
        onClose={() => setConfirmDelete(false)}
      />
    </div>
  )
}

function useRefreshOrder(id: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['order', id] })
    void queryClient.invalidateQueries({ queryKey: ['orders'] })
  }
}

function PlanDialog({ order, onClose }: { order: RepairOrder; onClose: () => void }) {
  const refresh = useRefreshOrder(order.id)
  const techs = useQuery({ queryKey: ['users', 'assignable'], queryFn: usersApi.assignable, staleTime: 5 * 60_000 })
  const [tech, setTech] = useState(order.assignedTechnicianId ?? '')
  const [priority, setPriority] = useState<Priority>(order.priority)
  const [promised, setPromised] = useState(toLocalInput(order.promisedAtUtc))
  const save = useMutation({
    mutationFn: () => ordersApi.plan(order.id, { assignedTechnicianId: tech || null, priority, promisedAtUtc: fromLocalInput(promised) }),
    onSuccess: () => {
      toast.success('Plan actualizado')
      refresh()
      onClose()
    },
    onError: (err) => toast.error(errorMessage(err)),
  })
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title="Plan de trabajo"
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} onClick={() => save.mutate()}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Técnico">
          <Select value={tech} onChange={(e) => setTech(e.target.value)}>
            <option value="">Sin asignar</option>
            {(techs.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>
                {t.displayName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Prioridad">
          <Select value={priority} onChange={(e) => setPriority(e.target.value as Priority)}>
            {(Object.keys(PRIORITY) as Priority[]).map((p) => (
              <option key={p} value={p}>
                {PRIORITY[p].label}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Fecha prometida">
          <Input type="datetime-local" value={promised} onChange={(e) => setPromised(e.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

function EditIssueDialog({ order, onClose }: { order: RepairOrder; onClose: () => void }) {
  const refresh = useRefreshOrder(order.id)
  const [issue, setIssue] = useState(order.issueDescription)
  const [notes, setNotes] = useState(order.notes ?? '')
  const [category, setCategory] = useState(order.issueCategory ?? '')
  const [warrantyDays, setWarrantyDays] = useState(order.warrantyDays ? String(order.warrantyDays) : '')
  const save = useMutation({
    mutationFn: () => ordersApi.update(order.id, { issueDescription: issue.trim(), notes: notes.trim() || null, issueCategory: category || null, warrantyDays: warrantyDays ? Number(warrantyDays) : null }),
    onSuccess: () => {
      toast.success('Orden actualizada')
      refresh()
      onClose()
    },
    onError: (err) => toast.error(errorMessage(err)),
  })
  return (
    <Modal
      open
      onClose={onClose}
      title="Editar orden"
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} disabled={issue.trim().length < 5} onClick={() => save.mutate()}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Falla">
          <Textarea value={issue} maxLength={500} onChange={(e) => setIssue(e.target.value)} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Categoría">
            <Input value={category} onChange={(e) => setCategory(e.target.value)} />
          </Field>
          <Field label="Garantía (días)" hint="Vacío = presupuesto o sucursal">
            <Input type="number" min={0} value={warrantyDays} onChange={(e) => setWarrantyDays(e.target.value)} />
          </Field>
        </div>
        <Field label="Notas internas">
          <Textarea value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

function DeliverySignatureDialog({ order, onClose }: { order: RepairOrder; onClose: () => void }) {
  const refresh = useRefreshOrder(order.id)
  const [name, setName] = useState(order.customerName)
  const [data, setData] = useState<string | null>(null)
  const save = useMutation({
    mutationFn: () => ordersApi.saveSignature(order.id, 'Delivery', name.trim(), data!),
    onSuccess: () => {
      toast.success('Firma guardada')
      refresh()
      onClose()
    },
    onError: (err) => toast.error(errorMessage(err)),
  })
  return (
    <Modal
      open
      onClose={onClose}
      title="Firma de retiro"
      description="El cliente confirma que retira el equipo funcionando."
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} disabled={!data || name.trim().length < 2} onClick={() => save.mutate()}>
            Guardar firma
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Nombre de quien retira">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <SignaturePad onChange={setData} />
      </div>
    </Modal>
  )
}

function WarrantyClaimDialog({ order, onClose }: { order: RepairOrder; onClose: () => void }) {
  const navigate = useNavigate()
  const [issue, setIssue] = useState('')
  const create = useMutation({
    mutationFn: () => ordersApi.warrantyClaim(order.id, issue.trim()),
    onSuccess: (created) => {
      toast.success(`Reingreso ${created.code} creado sin cargo`)
      navigate(`/orders/${created.id}`)
    },
    onError: (err) => toast.error(errorMessage(err)),
  })
  return (
    <Modal
      open
      onClose={onClose}
      title="Reingreso por garantía"
      description={order.underWarranty ? `Garantía vigente hasta ${dateTime(order.warrantyExpiresAtUtc)}` : 'La garantía de esta orden está vencida.'}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={create.isPending} disabled={issue.trim().length < 5} onClick={() => create.mutate()}>
            Crear reingreso
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        {!order.underWarranty ? <Alert tone="amber">Fuera de garantía: el sistema puede rechazarlo.</Alert> : null}
        <Field label="¿Qué falla ahora?">
          <Textarea value={issue} onChange={(e) => setIssue(e.target.value)} autoFocus />
        </Field>
      </div>
    </Modal>
  )
}
