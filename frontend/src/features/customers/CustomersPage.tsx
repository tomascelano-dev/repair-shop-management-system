import { useRef, useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { customersApi, devicesApi, exportPaths, inventoryApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { Customer, Device, ImportResult } from '../../api/types'
import { useSession } from '../../auth/session'
import { CustomerSearch, StatusBadge } from '../../components/domain'
import { Alert, Badge, Button, Card, Checkbox, ConfirmDialog, EmptyState, ErrorState, KeyValue, Loading, Modal, PageHeader, Pagination, SearchInput, Stat, Table, Td, Th } from '../../components/ui'
import { amounts, date, dateTime, money, number, relative } from '../../lib/format'
import { downloadFile } from '../../lib/files'
import { useDebounced } from '../../lib/hooks'
import { DOCUMENT_TYPE, TAX_CONDITION } from '../../lib/labels'
import { CustomerForm, DeviceForm } from './forms'
import { customerToInput, deviceToInput, emptyCustomer, emptyDevice } from './model'

const TAKE = 25

export function CustomersPage() {
  const navigate = useNavigate()
  const { can } = useSession()
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [skip, setSkip] = useState(0)
  const [creating, setCreating] = useState(false)
  const [importing, setImporting] = useState(false)
  const debounced = useDebounced(q.trim(), 300)
  const customers = useQuery({
    queryKey: ['customers', 'list', debounced, skip],
    queryFn: () => customersApi.search({ q: debounced || undefined, skip, take: TAKE, sortBy: debounced ? 'name' : 'createdAt', sortDir: debounced ? 'asc' : 'desc' }),
    placeholderData: keepPreviousData,
  })

  return (
    <div>
      <PageHeader
        title="Clientes"
        subtitle="Buscá por nombre, teléfono en cualquier formato, email, documento o etiqueta."
        actions={
          <>
            {can('admin') ? (
              <>
                <Button onClick={() => setImporting(true)}>Importar</Button>
                <Button onClick={() => void downloadFile(exportPaths.customers, 'clientes.xlsx')}>Exportar Excel</Button>
              </>
            ) : null}
            {can('orders.manage') ? (
              <Button variant="primary" onClick={() => setCreating(true)}>
                + Nuevo cliente
              </Button>
            ) : null}
          </>
        }
      />
      <Card className="mb-4">
        <SearchInput
          value={q}
          onChange={(v) => {
            setQ(v)
            setSkip(0)
          }}
          placeholder="Buscar clientes…"
          autoFocus
        />
      </Card>
      <Card padded={false}>
        {customers.isLoading ? (
          <Loading />
        ) : customers.isError ? (
          <div className="p-4">
            <ErrorState error={errorMessage(customers.error)} />
          </div>
        ) : customers.data!.items.length === 0 ? (
          <div className="p-4">
            <EmptyState title={debounced ? 'Sin resultados' : 'Todavía no hay clientes'} />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Nombre</Th>
                  <Th>Teléfono</Th>
                  <Th>Email</Th>
                  <Th>Etiquetas</Th>
                  <Th>Alta</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {customers.data!.items.map((c) => (
                  <tr key={c.id} className="cursor-pointer hover:bg-slate-50" onClick={() => navigate(`/customers/${c.id}`)}>
                    <Td className="font-medium text-slate-800">{c.fullName}</Td>
                    <Td>{c.phone}</Td>
                    <Td className="text-slate-500">{c.email ?? '—'}</Td>
                    <Td>
                      <div className="flex flex-wrap gap-1">
                        {(c.tags ?? '')
                          .split(',')
                          .filter(Boolean)
                          .map((t) => (
                            <Badge key={t}>{t}</Badge>
                          ))}
                      </div>
                    </Td>
                    <Td className="text-slate-500">{date(c.createdAtUtc)}</Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <div className="px-4 pb-3">
              <Pagination skip={skip} take={TAKE} total={customers.data!.total} onChange={setSkip} />
            </div>
          </>
        )}
      </Card>

      {creating ? (
        <Modal open onClose={() => setCreating(false)} title="Nuevo cliente" size="lg">
          <CustomerForm
            initial={emptyCustomer()}
            submitLabel="Crear cliente"
            onCancel={() => setCreating(false)}
            onUseExisting={(id) => navigate(`/customers/${id}`)}
            onSubmit={async (input) => {
              const c = await customersApi.create(input)
              toast.success('Cliente creado')
              void queryClient.invalidateQueries({ queryKey: ['customers'] })
              navigate(`/customers/${c.id}`)
            }}
          />
        </Modal>
      ) : null}
      {importing ? <ImportDialog kind="customers" onClose={() => setImporting(false)} /> : null}
    </div>
  )
}

export function ImportDialog({ kind, onClose }: { kind: 'customers' | 'inventory'; onClose: () => void }) {
  const queryClient = useQueryClient()
  const inputRef = useRef<HTMLInputElement | null>(null)
  const [file, setFile] = useState<File | null>(null)
  const [updateExisting, setUpdateExisting] = useState(false)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [busy, setBusy] = useState(false)

  async function run(dryRun: boolean) {
    if (!file) return
    setBusy(true)
    try {
      const r = kind === 'customers' ? await customersApi.import(file, dryRun) : await inventoryApi.import(file, dryRun, updateExisting)
      setResult(r)
      if (!dryRun) {
        toast.success(`Importación lista: ${r.created} nuevos, ${r.updated} actualizados`)
        void queryClient.invalidateQueries({ queryKey: [kind === 'customers' ? 'customers' : 'inventory'] })
      }
    } catch (err) {
      toast.error('No se pudo importar', { description: errorMessage(err) })
    } finally {
      setBusy(false)
    }
  }

  const columns =
    kind === 'customers'
      ? 'nombre, telefono, email, documento, direccion, notas, etiquetas'
      : 'sku, nombre, stock, costo, precio, categoria, codigo_barras, stock_minimo'

  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title={kind === 'customers' ? 'Importar clientes' : 'Importar inventario'}
      description="Subí un Excel (.xlsx) o CSV. Primero se valida sin guardar nada."
      footer={
        <>
          <Button onClick={onClose}>Cerrar</Button>
          <Button disabled={!file} loading={busy} onClick={() => void run(true)}>
            Validar
          </Button>
          <Button variant="primary" disabled={!file || !result || !result.dryRun} loading={busy} onClick={() => void run(false)}>
            Importar
          </Button>
        </>
      }
    >
      <div className="space-y-4 text-sm">
        <p className="text-slate-600">
          Columnas reconocidas: <code className="text-xs">{columns}</code>. La primera fila tiene que ser el encabezado.
        </p>
        <input
          ref={inputRef}
          type="file"
          accept=".xlsx,.csv,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          onChange={(e) => {
            setFile(e.target.files?.[0] ?? null)
            setResult(null)
          }}
          className="block w-full text-sm"
        />
        {kind === 'inventory' ? <Checkbox label="Actualizar los ítems que ya existen (por SKU)" checked={updateExisting} onChange={(e) => setUpdateExisting(e.target.checked)} /> : null}
        {result ? (
          <div className="space-y-2">
            <Alert tone={result.errors.length ? 'amber' : 'green'} title={result.dryRun ? 'Resultado de la validación' : 'Importación terminada'}>
              {result.totalRows} filas · {result.created} nuevas · {result.updated} a actualizar · {result.skipped} omitidas · {result.errors.length} con errores
            </Alert>
            {result.errors.length > 0 ? (
              <ul className="max-h-40 overflow-y-auto rounded-lg bg-rose-50 p-2 text-xs text-rose-800">
                {result.errors.map((e) => (
                  <li key={`${e.row}-${e.message}`}>
                    Fila {e.row}: {e.message}
                  </li>
                ))}
              </ul>
            ) : null}
            {result.preview.length > 0 ? (
              <Table className="max-h-56 rounded-lg border border-slate-200">
                <thead className="bg-slate-50">
                  <tr>
                    {Object.keys(result.preview[0]!).map((k) => (
                      <Th key={k}>{k}</Th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {result.preview.slice(0, 10).map((row, idx) => (
                    <tr key={idx}>
                      {Object.values(row).map((v, i) => (
                        <Td key={i}>{v}</Td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </Table>
            ) : null}
          </div>
        ) : null}
      </div>
    </Modal>
  )
}

export function CustomerDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const { can } = useSession()
  const queryClient = useQueryClient()
  const summary = useQuery({ queryKey: ['customer', id, 'summary'], queryFn: () => customersApi.summary(id) })
  const [editing, setEditing] = useState(false)
  const [device, setDevice] = useState<Device | 'new' | null>(null)
  const [merging, setMerging] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const remove = useMutation({
    mutationFn: () => customersApi.remove(id),
    onSuccess: () => {
      toast.success('Cliente eliminado')
      void queryClient.invalidateQueries({ queryKey: ['customers'] })
      navigate('/customers')
    },
    onError: (err) => toast.error('No se pudo eliminar', { description: errorMessage(err) }),
  })

  if (summary.isLoading) return <Loading />
  if (summary.isError) return <ErrorState error={errorMessage(summary.error)} />
  const s = summary.data!
  const c = s.customer
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['customer', id] })

  return (
    <div className="space-y-5">
      <PageHeader
        title={c.fullName}
        subtitle={
          <>
            Cliente desde {date(c.createdAtUtc)}
            {s.lastVisitAtUtc ? ` · última visita ${relative(s.lastVisitAtUtc)}` : ''}
          </>
        }
        actions={
          <>
            {c.whatsAppNumber ? (
              <a href={`https://wa.me/${c.whatsAppNumber}`} target="_blank" rel="noreferrer" className="inline-flex h-10 items-center rounded-lg border border-emerald-600 px-3.5 text-sm font-medium text-emerald-700 hover:bg-emerald-50">
                WhatsApp
              </a>
            ) : null}
            {can('orders.manage') ? (
              <>
                <Button onClick={() => setEditing(true)}>Editar</Button>
                <Button variant="primary" onClick={() => navigate(`/orders/new?customerId=${c.id}`)}>
                  + Nueva orden
                </Button>
              </>
            ) : null}
          </>
        }
      />

      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <Stat label="Órdenes abiertas" value={s.openOrders} />
        <Stat label="Total gastado" value={amounts(s.totalSpent)} />
        <Stat label="Saldo pendiente" value={amounts(s.balanceDue)} tone={s.balanceDue.some((b) => b.amount > 0) ? 'bad' : 'default'} />
        <Stat label="Satisfacción" value={s.averageFeedbackScore ? `${number(s.averageFeedbackScore, 1)} / 5` : '—'} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card title="Datos">
          <KeyValue
            className="sm:grid-cols-1"
            items={[
              { label: 'Teléfono', value: c.phone },
              { label: 'Email', value: c.email ?? '—' },
              { label: 'Documento', value: c.documentType === 'None' ? '—' : `${DOCUMENT_TYPE[c.documentType]} ${c.documentNumber}` },
              { label: 'Condición fiscal', value: TAX_CONDITION[c.taxCondition] },
              { label: 'Dirección', value: c.address ?? '—' },
              { label: 'Avisos', value: c.notificationsOptIn ? 'Acepta mensajes' : 'No acepta mensajes' },
              { label: 'Notas', value: c.notes ?? '—', hidden: !c.notes },
            ]}
          />
          {can('admin') ? (
            <div className="mt-4 flex flex-wrap gap-2 border-t border-slate-100 pt-3">
              <Button size="sm" onClick={() => setMerging(true)}>
                Fusionar duplicado
              </Button>
              <Button size="sm" variant="ghost" className="text-rose-700" onClick={() => setDeleting(true)}>
                Eliminar
              </Button>
            </div>
          ) : null}
        </Card>

        <Card
          title="Equipos"
          className="lg:col-span-2"
          actions={
            can('orders.manage') ? (
              <Button size="sm" onClick={() => setDevice('new')}>
                + Equipo
              </Button>
            ) : null
          }
        >
          {s.devices.length === 0 ? (
            <EmptyState title="Sin equipos" />
          ) : (
            <ul className="divide-y divide-slate-100 text-sm">
              {s.devices.map((d) => (
                <li key={d.id} className="flex items-center justify-between py-2">
                  <span>
                    <strong>
                      {d.brand} {d.model}
                    </strong>
                    {d.label ? <span className="ml-2 text-slate-500">{d.label}</span> : null}
                    {d.imei ? <span className="ml-2 text-xs text-slate-400">IMEI {d.imei}</span> : null}
                  </span>
                  {can('orders.manage') ? (
                    <Button size="sm" variant="ghost" onClick={() => setDevice(d)}>
                      Editar
                    </Button>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      <Card title="Órdenes" padded={false}>
        {s.orders.length === 0 ? (
          <div className="p-4">
            <EmptyState title="Sin órdenes" />
          </div>
        ) : (
          <Table>
            <thead className="bg-slate-50">
              <tr>
                <Th>Orden</Th>
                <Th>Equipo</Th>
                <Th>Estado</Th>
                <Th>Fecha</Th>
                <Th align="right">Total</Th>
                <Th align="right">Saldo</Th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {s.orders.map((o) => (
                <tr key={o.id} className="cursor-pointer hover:bg-slate-50" onClick={() => navigate(`/orders/${o.id}`)}>
                  <Td className="font-medium text-brand-700">{o.code}</Td>
                  <Td>
                    {o.deviceLabel}
                    <span className="block truncate text-xs text-slate-500">{o.issueDescription}</span>
                  </Td>
                  <Td>
                    <StatusBadge status={o.status} />
                  </Td>
                  <Td className="text-slate-500">{date(o.createdAtUtc)}</Td>
                  <Td align="right">{money(o.total, o.currency)}</Td>
                  <Td align="right" className={o.balance > 0 ? 'text-rose-700' : 'text-slate-400'}>
                    {o.balance > 0 ? money(o.balance, o.currency) : '—'}
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>

      {s.sales.length > 0 ? (
        <Card title="Compras en mostrador">
          <ul className="divide-y divide-slate-100 text-sm">
            {s.sales.map((v) => (
              <li key={v.id} className="flex justify-between py-2">
                <Link to={`/sales?id=${v.id}`} className="text-brand-700 hover:underline">
                  {v.code}
                </Link>
                <span className="text-slate-600">
                  {dateTime(v.createdAtUtc)} · {money(v.total, v.currency)}
                </span>
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      {editing ? (
        <Modal open size="lg" onClose={() => setEditing(false)} title="Editar cliente">
          <CustomerForm
            initial={customerToInput(c)}
            customerId={c.id}
            onCancel={() => setEditing(false)}
            onSubmit={async (input) => {
              await customersApi.update(c.id, input)
              toast.success('Cliente actualizado')
              setEditing(false)
              refresh()
            }}
          />
        </Modal>
      ) : null}
      {device ? (
        <Modal open onClose={() => setDevice(null)} title={device === 'new' ? 'Nuevo equipo' : 'Editar equipo'}>
          <DeviceForm
            initial={device === 'new' ? emptyDevice() : deviceToInput(device)}
            onCancel={() => setDevice(null)}
            onSubmit={async (input) => {
              if (device === 'new') await devicesApi.create({ ...input, customerId: c.id })
              else await devicesApi.update(device.id, input)
              toast.success('Equipo guardado')
              setDevice(null)
              refresh()
            }}
          />
        </Modal>
      ) : null}
      {merging ? <MergeDialog target={c} onClose={() => setMerging(false)} onDone={() => { setMerging(false); refresh() }} /> : null}
      <ConfirmDialog
        open={deleting}
        title="Eliminar cliente"
        message="Solo se pueden eliminar clientes sin órdenes ni ventas. Para unificar registros duplicados usá “Fusionar”."
        danger
        confirmLabel="Eliminar"
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
        onClose={() => setDeleting(false)}
      />
    </div>
  )
}

function MergeDialog({ target, onClose, onDone }: { target: Customer; onClose: () => void; onDone: () => void }) {
  const [source, setSource] = useState<Customer | null>(null)
  const merge = useMutation({
    mutationFn: () => customersApi.merge(target.id, source!.id),
    onSuccess: () => {
      toast.success('Clientes fusionados')
      onDone()
    },
    onError: (err) => toast.error('No se pudo fusionar', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      onClose={onClose}
      title="Fusionar cliente duplicado"
      description={`Los equipos, órdenes y ventas del cliente elegido pasan a ${target.fullName}, y el duplicado se elimina.`}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="danger" disabled={!source || source.id === target.id} loading={merge.isPending} onClick={() => merge.mutate()}>
            Fusionar
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <CustomerSearch onSelect={setSource} placeholder="Buscar el cliente duplicado…" />
        {source ? (
          <Alert tone="amber">
            Se va a eliminar <strong>{source.fullName}</strong> ({source.phone}).
          </Alert>
        ) : null}
      </div>
    </Modal>
  )
}
