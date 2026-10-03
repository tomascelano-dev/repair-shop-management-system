import { useId, useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { purchasingApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { PurchaseOrder, PurchaseOrderStatus, Supplier } from '../../api/types'
import { Alert, Badge, Button, Card, Checkbox, ConfirmDialog, EmptyState, ErrorState, Field, Input, Loading, Modal, PageHeader, Pagination, SearchInput, Select, Table, Tabs, Td, Textarea, Th } from '../../components/ui'
import { date, dateTime, money, parseAmount, todayIso } from '../../lib/format'
import { useDebounced } from '../../lib/hooks'
import { PURCHASE_STATUS } from '../../lib/labels'
import { CURRENCIES, useDefaultCurrency } from '../../lib/shop'
import { ItemPicker } from './ItemPicker'

const TAKE = 25

export function PurchasesPage() {
  const [tab, setTab] = useState<'orders' | 'suppliers'>('orders')
  return (
    <div>
      <PageHeader title="Compras" subtitle="Pedidos a proveedores y recepción de mercadería (actualiza stock y costo)." />
      <Tabs
        className="mb-4"
        value={tab}
        onChange={setTab}
        tabs={[
          { value: 'orders', label: 'Órdenes de compra' },
          { value: 'suppliers', label: 'Proveedores' },
        ]}
      />
      {tab === 'orders' ? <PurchaseOrdersTab /> : <SuppliersTab />}
    </div>
  )
}

// ===== Purchase orders =====

function PurchaseOrdersTab() {
  const [status, setStatus] = useState<PurchaseOrderStatus | ''>('')
  const [skip, setSkip] = useState(0)
  const [editing, setEditing] = useState<PurchaseOrder | 'new' | null>(null)
  const [viewing, setViewing] = useState<string | null>(null)
  const orders = useQuery({
    queryKey: ['purchases', 'list', status, skip],
    queryFn: () => purchasingApi.orders({ status: status || undefined, skip, take: TAKE }),
    placeholderData: keepPreviousData,
  })

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <Select className="w-56" value={status} onChange={(e) => { setStatus(e.target.value as PurchaseOrderStatus | ''); setSkip(0) }} aria-label="Estado">
          <option value="">Todos los estados</option>
          {(Object.keys(PURCHASE_STATUS) as PurchaseOrderStatus[]).map((s) => (
            <option key={s} value={s}>
              {PURCHASE_STATUS[s].label}
            </option>
          ))}
        </Select>
        <Button variant="primary" onClick={() => setEditing('new')}>
          + Nueva orden de compra
        </Button>
      </div>
      <Card padded={false}>
        {orders.isLoading ? (
          <Loading />
        ) : orders.isError ? (
          <div className="p-4">
            <ErrorState error={errorMessage(orders.error)} onRetry={() => void orders.refetch()} />
          </div>
        ) : (orders.data?.items ?? []).length === 0 ? (
          <div className="p-4">
            <EmptyState title="No hay órdenes de compra" description="Armá un pedido con los repuestos que necesitás; al recibirlo se suma el stock." action={<Button onClick={() => setEditing('new')}>+ Nueva orden</Button>} />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Orden</Th>
                  <Th>Proveedor</Th>
                  <Th>Creada</Th>
                  <Th>Entrega estimada</Th>
                  <Th>Estado</Th>
                  <Th align="right">Total</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {orders.data!.items.map((po) => (
                  <tr key={po.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setViewing(po.id)}>
                    <Td className="font-medium text-brand-700">{po.code}</Td>
                    <Td>{po.supplierName}</Td>
                    <Td>{date(po.createdAtUtc)}</Td>
                    <Td>{po.expectedAtUtc ? date(po.expectedAtUtc) : '—'}</Td>
                    <Td>
                      <Badge tone={PURCHASE_STATUS[po.status].tone}>{PURCHASE_STATUS[po.status].label}</Badge>
                    </Td>
                    <Td align="right">{money(po.total, po.currency)}</Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <div className="px-4 pb-3">
              <Pagination skip={skip} take={TAKE} total={orders.data?.total ?? 0} onChange={setSkip} />
            </div>
          </>
        )}
      </Card>
      {editing ? <PurchaseOrderEditor order={editing === 'new' ? null : editing} onClose={() => setEditing(null)} /> : null}
      {viewing ? (
        <PurchaseOrderDialog
          id={viewing}
          onClose={() => setViewing(null)}
          onEdit={(po) => {
            setViewing(null)
            setEditing(po)
          }}
        />
      ) : null}
    </>
  )
}

interface DraftLine {
  inventoryItemId: string
  description: string
  quantity: number
  unitCostText: string
}

function PurchaseOrderEditor({ order, onClose }: { order: PurchaseOrder | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const defaultCurrency = useDefaultCurrency()
  const suppliers = useQuery({ queryKey: ['suppliers', 'all'], queryFn: () => purchasingApi.suppliers({ take: 200 }) })
  const [supplierId, setSupplierId] = useState(order?.supplierId ?? '')
  const [currency, setCurrency] = useState(order?.currency ?? defaultCurrency)
  const [expected, setExpected] = useState(order?.expectedAtUtc ? order.expectedAtUtc.slice(0, 10) : '')
  const [notes, setNotes] = useState(order?.notes ?? '')
  const [lines, setLines] = useState<DraftLine[]>(order?.lines.map((l) => ({ inventoryItemId: l.inventoryItemId, description: l.description, quantity: l.quantity, unitCostText: String(l.unitCost).replace('.', ',') })) ?? [])
  const [newSupplier, setNewSupplier] = useState(false)
  const supplierFieldId = useId()

  const total = lines.reduce((s, l) => s + l.quantity * (parseAmount(l.unitCostText) ?? 0), 0)
  const valid = supplierId && lines.length > 0 && lines.every((l) => l.quantity > 0 && (parseAmount(l.unitCostText) ?? -1) >= 0)

  const save = useMutation({
    mutationFn: () => {
      const body = {
        supplierId,
        currency,
        lines: lines.map((l) => ({ inventoryItemId: l.inventoryItemId, quantity: l.quantity, unitCost: parseAmount(l.unitCostText) ?? 0 })),
        notes: notes.trim() || null,
        expectedAtUtc: expected ? new Date(`${expected}T12:00:00`).toISOString() : null,
      }
      return order ? purchasingApi.update(order.id, body) : purchasingApi.create(body)
    },
    onSuccess: (po) => {
      toast.success(order ? 'Orden actualizada' : `Orden ${po.code} creada`)
      void queryClient.invalidateQueries({ queryKey: ['purchases'] })
      onClose()
    },
    onError: (err) => toast.error('No se pudo guardar', { description: errorMessage(err) }),
  })

  return (
    <Modal
      open
      size="xl"
      onClose={onClose}
      title={order ? `Editar ${order.code}` : 'Nueva orden de compra'}
      footer={
        <>
          <span className="mr-auto self-center text-sm text-slate-600">
            Total <strong className="tabular-nums">{money(total, currency)}</strong>
          </span>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} disabled={!valid} onClick={() => save.mutate()}>
            Guardar borrador
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="grid gap-3 md:grid-cols-[2fr_1fr_1fr]">
          <Field label="Proveedor" required controlId={supplierFieldId}>
            <div className="flex gap-2">
              <Select id={supplierFieldId} value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
                <option value="">Elegí un proveedor…</option>
                {(suppliers.data?.items ?? []).map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </Select>
              <Button onClick={() => setNewSupplier(true)} aria-label="Nuevo proveedor">
                +
              </Button>
            </div>
          </Field>
          <Field label="Moneda">
            <Select value={currency} onChange={(e) => setCurrency(e.target.value)}>
              {CURRENCIES.map((c) => (
                <option key={c}>{c}</option>
              ))}
            </Select>
          </Field>
          <Field label="Entrega estimada">
            <Input type="date" min={todayIso()} value={expected} onChange={(e) => setExpected(e.target.value)} />
          </Field>
        </div>

        <div>
          <p className="mb-1 text-sm font-medium text-slate-700">Agregar ítems</p>
          <ItemPicker
            placeholder="Buscar repuesto para pedir…"
            onSelect={(item) =>
              setLines((ls) =>
                ls.some((l) => l.inventoryItemId === item.id)
                  ? ls.map((l) => (l.inventoryItemId === item.id ? { ...l, quantity: l.quantity + 1 } : l))
                  : [...ls, { inventoryItemId: item.id, description: `${item.name} (${item.sku})`, quantity: Math.max(1, item.minStock - item.availableQuantity), unitCostText: item.unitCost != null ? String(item.unitCost).replace('.', ',') : '' }]
              )
            }
          />
        </div>

        {lines.length === 0 ? (
          <EmptyState title="Todavía no agregaste ítems" description="Tip: filtrá el inventario por “stock bajo” para ver qué reponer." />
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Ítem</Th>
                <Th align="right">Cantidad</Th>
                <Th align="right">Costo unitario</Th>
                <Th align="right">Subtotal</Th>
                <Th />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {lines.map((l, idx) => (
                <tr key={l.inventoryItemId}>
                  <Td>{l.description}</Td>
                  <Td align="right">
                    <Input type="number" min={1} className="ml-auto h-8 w-20 text-right" value={l.quantity} onChange={(e) => setLines((ls) => ls.map((x, i) => (i === idx ? { ...x, quantity: Math.max(1, Number(e.target.value) || 1) } : x)))} aria-label="Cantidad" />
                  </Td>
                  <Td align="right">
                    <Input inputMode="decimal" className="ml-auto h-8 w-28 text-right" value={l.unitCostText} onChange={(e) => setLines((ls) => ls.map((x, i) => (i === idx ? { ...x, unitCostText: e.target.value } : x)))} aria-label="Costo unitario" />
                  </Td>
                  <Td align="right">{money(l.quantity * (parseAmount(l.unitCostText) ?? 0), currency)}</Td>
                  <Td align="right">
                    <Button size="sm" variant="ghost" onClick={() => setLines((ls) => ls.filter((_, i) => i !== idx))} aria-label="Quitar">
                      ✕
                    </Button>
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
        <Field label="Notas">
          <Textarea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      {newSupplier ? (
        <SupplierDialog
          supplier={null}
          onClose={() => setNewSupplier(false)}
          onSaved={(s) => {
            setNewSupplier(false)
            setSupplierId(s.id)
            void queryClient.invalidateQueries({ queryKey: ['suppliers'] })
          }}
        />
      ) : null}
    </Modal>
  )
}

function PurchaseOrderDialog({ id, onClose, onEdit }: { id: string; onClose: () => void; onEdit: (po: PurchaseOrder) => void }) {
  const queryClient = useQueryClient()
  const q = useQuery({ queryKey: ['purchases', id], queryFn: () => purchasingApi.get(id) })
  const [receiving, setReceiving] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const po = q.data
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['purchases'] })
    void queryClient.invalidateQueries({ queryKey: ['inventory'] })
  }
  const ordered = useMutation({
    mutationFn: () => purchasingApi.markOrdered(id),
    onSuccess: () => {
      toast.success('Marcada como pedida')
      refresh()
    },
    onError: (err) => toast.error('No se pudo actualizar', { description: errorMessage(err) }),
  })
  const cancel = useMutation({
    mutationFn: () => purchasingApi.cancel(id),
    onSuccess: () => {
      toast.success('Orden cancelada')
      setCancelling(false)
      refresh()
    },
    onError: (err) => toast.error('No se pudo cancelar', { description: errorMessage(err) }),
  })
  const open = po && (po.status === 'Ordered' || po.status === 'PartiallyReceived')

  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title={po ? `${po.code} · ${po.supplierName}` : 'Orden de compra'}
      description={po ? `Creada ${dateTime(po.createdAtUtc)}${po.orderedAtUtc ? ` · pedida ${dateTime(po.orderedAtUtc)}` : ''}${po.receivedAtUtc ? ` · recibida ${dateTime(po.receivedAtUtc)}` : ''}` : undefined}
      footer={
        po ? (
          <>
            {po.status === 'Draft' || po.status === 'Ordered' ? (
              <Button variant="ghost" className="mr-auto text-rose-700" onClick={() => setCancelling(true)}>
                Cancelar orden
              </Button>
            ) : null}
            {po.status === 'Draft' ? (
              <>
                <Button onClick={() => onEdit(po)}>Editar</Button>
                <Button variant="primary" loading={ordered.isPending} onClick={() => ordered.mutate()}>
                  Marcar como pedida
                </Button>
              </>
            ) : null}
            {open ? (
              <Button variant="primary" onClick={() => setReceiving(true)}>
                Recibir mercadería
              </Button>
            ) : null}
          </>
        ) : null
      }
    >
      {q.isLoading ? (
        <Loading />
      ) : !po ? (
        <ErrorState error={errorMessage(q.error)} />
      ) : (
        <div className="space-y-3">
          <Badge tone={PURCHASE_STATUS[po.status].tone}>{PURCHASE_STATUS[po.status].label}</Badge>
          <Table>
            <thead>
              <tr>
                <Th>Ítem</Th>
                <Th align="right">Pedido</Th>
                <Th align="right">Recibido</Th>
                <Th align="right">Costo</Th>
                <Th align="right">Subtotal</Th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {po.lines.map((l) => (
                <tr key={l.id}>
                  <Td>{l.description}</Td>
                  <Td align="right">{l.quantity}</Td>
                  <Td align="right" className={l.receivedQuantity >= l.quantity ? 'text-emerald-700' : l.receivedQuantity > 0 ? 'text-amber-700' : ''}>
                    {l.receivedQuantity}
                  </Td>
                  <Td align="right">{money(l.unitCost, po.currency)}</Td>
                  <Td align="right">{money(l.lineTotal, po.currency)}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
          <p className="text-right text-sm">
            Total <strong className="tabular-nums">{money(po.total, po.currency)}</strong>
          </p>
          {po.notes ? <p className="text-sm text-slate-600">Notas: {po.notes}</p> : null}
        </div>
      )}
      {receiving && po ? (
        <ReceiveDialog
          po={po}
          onClose={() => setReceiving(false)}
          onDone={() => {
            setReceiving(false)
            refresh()
          }}
        />
      ) : null}
      <ConfirmDialog open={cancelling} title="¿Cancelar la orden de compra?" message="No se puede deshacer." danger confirmLabel="Cancelar orden" loading={cancel.isPending} onConfirm={() => cancel.mutate()} onClose={() => setCancelling(false)} />
    </Modal>
  )
}

function ReceiveDialog({ po, onClose, onDone }: { po: PurchaseOrder; onClose: () => void; onDone: () => void }) {
  const pending = po.lines.filter((l) => l.receivedQuantity < l.quantity)
  const [qty, setQty] = useState<Record<string, number>>(() => Object.fromEntries(pending.map((l) => [l.id, l.quantity - l.receivedQuantity])))
  const receive = useMutation({
    mutationFn: () => purchasingApi.receive(po.id, pending.filter((l) => (qty[l.id] ?? 0) > 0).map((l) => ({ lineId: l.id, quantity: qty[l.id]! }))),
    onSuccess: (r) => {
      toast.success(r.status === 'Received' ? 'Mercadería recibida completa' : 'Recepción parcial registrada', { description: 'El stock y el costo de los ítems se actualizaron.' })
      onDone()
    },
    onError: (err) => toast.error('No se pudo registrar la recepción', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      onClose={onClose}
      title={`Recibir ${po.code}`}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={receive.isPending} disabled={!pending.some((l) => (qty[l.id] ?? 0) > 0)} onClick={() => receive.mutate()}>
            Confirmar recepción
          </Button>
        </>
      }
    >
      <ul className="divide-y divide-slate-100 text-sm">
        {pending.map((l) => {
          const max = l.quantity - l.receivedQuantity
          return (
            <li key={l.id} className="flex items-center justify-between gap-3 py-2">
              <span>
                {l.description}
                <span className="block text-xs text-slate-500">Pendiente {max}</span>
              </span>
              <Input type="number" min={0} max={max} className="h-8 w-20 text-right" value={qty[l.id] ?? 0} onChange={(e) => setQty((q) => ({ ...q, [l.id]: Math.max(0, Math.min(max, Number(e.target.value) || 0)) }))} aria-label={`Cantidad recibida de ${l.description}`} />
            </li>
          )
        })}
      </ul>
      <Alert tone="slate" className="mt-3">
        Lo que no llegue queda pendiente para una próxima recepción.
      </Alert>
    </Modal>
  )
}

// ===== Suppliers =====

function SuppliersTab() {
  const [q, setQ] = useState('')
  const debounced = useDebounced(q.trim(), 300)
  const [inactive, setInactive] = useState(false)
  const [editing, setEditing] = useState<Supplier | 'new' | null>(null)
  const queryClient = useQueryClient()
  const suppliers = useQuery({ queryKey: ['suppliers', debounced, inactive], queryFn: () => purchasingApi.suppliers({ q: debounced || undefined, includeInactive: inactive, take: 100 }) })
  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchInput className="min-w-64 flex-1" value={q} onChange={setQ} placeholder="Buscar proveedor…" />
        <Checkbox checked={inactive} onChange={(e) => setInactive(e.target.checked)} label="Incluir inactivos" />
        <Button variant="primary" onClick={() => setEditing('new')}>
          + Nuevo proveedor
        </Button>
      </div>
      <Card padded={false}>
        {suppliers.isLoading ? (
          <Loading />
        ) : (suppliers.data?.items ?? []).length === 0 ? (
          <div className="p-4">
            <EmptyState title="Sin proveedores" />
          </div>
        ) : (
          <Table>
            <thead className="bg-slate-50">
              <tr>
                <Th>Proveedor</Th>
                <Th>Contacto</Th>
                <Th>Teléfono</Th>
                <Th>Email</Th>
                <Th>CUIT</Th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {suppliers.data!.items.map((s) => (
                <tr key={s.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setEditing(s)}>
                  <Td className="font-medium">
                    {s.name} {!s.isActive ? <Badge>inactivo</Badge> : null}
                  </Td>
                  <Td>{s.contactName ?? '—'}</Td>
                  <Td>{s.phone ?? '—'}</Td>
                  <Td>{s.email ?? '—'}</Td>
                  <Td>{s.taxId ?? '—'}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>
      {editing ? (
        <SupplierDialog
          supplier={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null)
            void queryClient.invalidateQueries({ queryKey: ['suppliers'] })
          }}
        />
      ) : null}
    </>
  )
}

function SupplierDialog({ supplier, onClose, onSaved }: { supplier: Supplier | null; onClose: () => void; onSaved: (s: Supplier) => void }) {
  const [v, setV] = useState({
    name: supplier?.name ?? '',
    contactName: supplier?.contactName ?? '',
    phone: supplier?.phone ?? '',
    email: supplier?.email ?? '',
    taxId: supplier?.taxId ?? '',
    notes: supplier?.notes ?? '',
    isActive: supplier?.isActive ?? true,
  })
  const set = (k: keyof typeof v, value: string | boolean) => setV((x) => ({ ...x, [k]: value }))
  const save = useMutation({
    mutationFn: () => {
      const body = { name: v.name.trim(), contactName: v.contactName.trim() || null, phone: v.phone.trim() || null, email: v.email.trim() || null, taxId: v.taxId.trim() || null, notes: v.notes.trim() || null, isActive: v.isActive }
      return supplier ? purchasingApi.updateSupplier(supplier.id, body) : purchasingApi.createSupplier(body)
    },
    onSuccess: (s) => {
      toast.success(supplier ? 'Proveedor actualizado' : 'Proveedor creado')
      onSaved(s)
    },
    onError: (err) => toast.error('No se pudo guardar', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      onClose={onClose}
      title={supplier ? supplier.name : 'Nuevo proveedor'}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} disabled={v.name.trim().length < 2} onClick={() => save.mutate()}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Nombre" required className="sm:col-span-2">
          <Input value={v.name} onChange={(e) => set('name', e.target.value)} autoFocus />
        </Field>
        <Field label="Contacto">
          <Input value={v.contactName} onChange={(e) => set('contactName', e.target.value)} />
        </Field>
        <Field label="Teléfono">
          <Input value={v.phone} onChange={(e) => set('phone', e.target.value)} />
        </Field>
        <Field label="Email">
          <Input type="email" value={v.email} onChange={(e) => set('email', e.target.value)} />
        </Field>
        <Field label="CUIT">
          <Input value={v.taxId} onChange={(e) => set('taxId', e.target.value)} />
        </Field>
        <Field label="Notas" className="sm:col-span-2">
          <Textarea rows={2} value={v.notes} onChange={(e) => set('notes', e.target.value)} />
        </Field>
        {supplier ? <Checkbox className="sm:col-span-2" checked={v.isActive} onChange={(e) => set('isActive', e.target.checked)} label="Activo" /> : null}
      </div>
    </Modal>
  )
}
