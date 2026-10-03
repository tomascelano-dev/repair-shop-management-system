import { useId, useState, type FormEvent } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { exportPaths, inventoryApi } from '../../api/endpoints'
import { errorMessage, fieldErrors } from '../../api/http'
import type { AdjustmentType, InventoryItem, InventoryItemInput } from '../../api/types'
import { useSession } from '../../auth/session'
import { Alert, Badge, Button, Card, Checkbox, EmptyState, ErrorState, Field, Input, Loading, Modal, PageHeader, Pagination, SearchInput, Select, Table, Tabs, Td, Th } from '../../components/ui'
import { cn } from '../../lib/cn'
import { dateTime, money, parseAmount } from '../../lib/format'
import { downloadFile } from '../../lib/files'
import { useDebounced } from '../../lib/hooks'
import { ADJUSTMENT_TYPE } from '../../lib/labels'
import { CURRENCIES, useDefaultCurrency } from '../../lib/shop'
import { ImportDialog } from '../customers/CustomersPage'

const TAKE = 25
const MANUAL_ADJUSTMENTS: AdjustmentType[] = ['Purchase', 'Correction', 'Return', 'Manual']

export function InventoryPage() {
  const { can } = useSession()
  const canManage = can('inventory.manage')
  const [params, setParams] = useSearchParams()
  const [q, setQ] = useState(params.get('q') ?? '')
  const debounced = useDebounced(q.trim(), 300)
  const lowStock = params.get('lowStock') === '1'
  const sellable = params.get('sellable') === '1'
  const inactive = params.get('inactive') === '1'
  const category = params.get('category') ?? ''
  const sortBy = params.get('sort') ?? 'name'
  const skip = Number(params.get('skip') ?? 0)
  const [editing, setEditing] = useState<InventoryItem | 'new' | null>(null)
  const [importing, setImporting] = useState(false)

  function setParam(key: string, value: string) {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    if (key !== 'skip') next.delete('skip')
    setParams(next, { replace: true })
  }

  const items = useQuery({
    queryKey: ['inventory', 'list', debounced, lowStock, sellable, inactive, category, sortBy, skip],
    queryFn: () =>
      inventoryApi.search({
        q: debounced || undefined,
        onlyLowStock: lowStock || undefined,
        onlySellable: sellable || undefined,
        includeInactive: inactive,
        category: category || undefined,
        sortBy,
        sortDir: sortBy === 'updatedAt' ? 'desc' : 'asc',
        skip,
        take: TAKE,
      }),
    placeholderData: keepPreviousData,
  })
  const list = items.data?.items ?? []

  return (
    <div>
      <PageHeader
        title="Inventario"
        subtitle="Repuestos y productos: stock disponible, reservado para órdenes y precios de venta."
        actions={
          canManage ? (
            <>
              <Button variant="ghost" onClick={() => void downloadFile(exportPaths.inventory, 'inventario.xlsx')}>
                Exportar
              </Button>
              <Button onClick={() => setImporting(true)}>Importar</Button>
              <Button variant="primary" onClick={() => setEditing('new')}>
                + Nuevo ítem
              </Button>
            </>
          ) : null
        }
      />

      <Card className="mb-4">
        <div className="grid gap-3 md:grid-cols-[minmax(0,2fr)_1fr_1fr]">
          <SearchInput value={q} onChange={(v) => { setQ(v); setParam('q', v.trim()) }} placeholder="Buscar por nombre, SKU, código de barras o categoría…" />
          <Input value={category} onChange={(e) => setParam('category', e.target.value)} placeholder="Categoría" aria-label="Categoría" />
          <Select value={sortBy} onChange={(e) => setParam('sort', e.target.value === 'name' ? '' : e.target.value)} aria-label="Ordenar">
            <option value="name">Ordenar por nombre</option>
            <option value="sku">Ordenar por SKU</option>
            <option value="stock">Ordenar por stock</option>
            <option value="updatedAt">Últimos modificados</option>
          </Select>
        </div>
        <div className="mt-3 flex flex-wrap gap-4">
          <Checkbox checked={lowStock} onChange={(e) => setParam('lowStock', e.target.checked ? '1' : '')} label="Solo stock bajo" />
          <Checkbox checked={sellable} onChange={(e) => setParam('sellable', e.target.checked ? '1' : '')} label="Solo vendibles en mostrador" />
          <Checkbox checked={inactive} onChange={(e) => setParam('inactive', e.target.checked ? '1' : '')} label="Incluir inactivos" />
        </div>
      </Card>

      <Card padded={false}>
        {items.isLoading ? (
          <Loading />
        ) : items.isError ? (
          <div className="p-4">
            <ErrorState error={errorMessage(items.error)} onRetry={() => void items.refetch()} />
          </div>
        ) : list.length === 0 ? (
          <div className="p-4">
            <EmptyState
              title={lowStock ? 'No hay ítems con stock bajo' : 'No hay ítems con estos filtros'}
              description={canManage ? 'Cargá tus repuestos uno por uno o importalos desde Excel/CSV.' : undefined}
              action={canManage ? <Button onClick={() => setEditing('new')}>+ Nuevo ítem</Button> : undefined}
            />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Ítem</Th>
                  <Th>SKU / código</Th>
                  <Th align="right">Stock</Th>
                  <Th align="right">Reservado</Th>
                  <Th align="right">Disponible</Th>
                  <Th align="right">Costo</Th>
                  <Th align="right">Precio venta</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {list.map((i) => (
                  <tr key={i.id} className={cn('cursor-pointer hover:bg-slate-50', !i.isActive && 'opacity-60')} onClick={() => setEditing(i)}>
                    <Td>
                      <button type="button" className="text-left font-medium text-slate-800 hover:underline" onClick={(e) => { e.stopPropagation(); setEditing(i) }}>
                        {i.name}
                      </button>
                      <span className="block text-xs text-slate-500">
                        {[i.category, i.location ? `Ubicación ${i.location}` : null].filter(Boolean).join(' · ') || '—'}
                      </span>
                    </Td>
                    <Td className="text-xs text-slate-600">
                      {i.sku}
                      {i.barcode ? <span className="block text-slate-400">{i.barcode}</span> : null}
                    </Td>
                    <Td align="right">{i.trackStock ? i.quantityOnHand : <span className="text-xs text-slate-400">sin control</span>}</Td>
                    <Td align="right" className="text-slate-500">
                      {i.reservedQuantity || '—'}
                    </Td>
                    <Td align="right">
                      {i.trackStock ? (
                        <span className="inline-flex items-center gap-1.5">
                          {i.isLowStock ? <Badge tone="amber">bajo</Badge> : null}
                          <span className={cn('font-medium', i.availableQuantity <= 0 ? 'text-rose-700' : 'text-slate-800')}>{i.availableQuantity}</span>
                        </span>
                      ) : (
                        '—'
                      )}
                    </Td>
                    <Td align="right">{money(i.unitCost, i.unitCostCurrency ?? 'ARS')}</Td>
                    <Td align="right">
                      {i.isSellable ? money(i.salePrice, i.salePriceCurrency ?? 'ARS') : <span className="text-xs text-slate-400">no se vende</span>}
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <div className="px-4 pb-3">
              <Pagination skip={skip} take={TAKE} total={items.data?.total ?? 0} onChange={(v) => setParam('skip', String(v))} />
            </div>
          </>
        )}
      </Card>

      {editing ? <ItemDialog item={editing === 'new' ? null : editing} canManage={canManage} onClose={() => setEditing(null)} /> : null}
      {importing ? <ImportDialog kind="inventory" onClose={() => setImporting(false)} /> : null}
    </div>
  )
}

type Tab = 'data' | 'moves' | 'compat'

function ItemDialog({ item, canManage, onClose }: { item: InventoryItem | null; canManage: boolean; onClose: () => void }) {
  const [tab, setTab] = useState<Tab>('data')
  const [adjusting, setAdjusting] = useState(false)
  const fresh = useQuery({ queryKey: ['inventory', item?.id], queryFn: () => inventoryApi.get(item!.id), enabled: !!item, initialData: item ?? undefined })
  const current = fresh.data ?? item

  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title={current ? current.name : 'Nuevo ítem'}
      description={current ? `${current.sku} · stock ${current.quantityOnHand} · disponible ${current.availableQuantity}` : 'Repuesto, accesorio o servicio con código.'}
    >
      {current ? (
        <>
          <Tabs<Tab>
            className="mb-4"
            value={tab}
            onChange={setTab}
            tabs={[
              { value: 'data', label: 'Datos' },
              { value: 'moves', label: 'Movimientos' },
              { value: 'compat', label: 'Modelos compatibles' },
            ]}
          />
          {tab === 'data' ? (
            <div className="space-y-4">
              {canManage && current.trackStock ? (
                <div className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2 text-sm">
                  <span>
                    Stock <strong>{current.quantityOnHand}</strong> · reservado {current.reservedQuantity} · mínimo {current.minStock}
                  </span>
                  <Button size="sm" onClick={() => setAdjusting(true)}>
                    Ajustar stock
                  </Button>
                </div>
              ) : null}
              <ItemForm item={current} readOnly={!canManage} onSaved={onClose} />
            </div>
          ) : tab === 'moves' ? (
            <AdjustmentsList itemId={current.id} />
          ) : (
            <CompatibilityEditor itemId={current.id} canManage={canManage} />
          )}
        </>
      ) : (
        <ItemForm item={null} readOnly={false} onSaved={onClose} />
      )}
      {adjusting && current ? <AdjustDialog item={current} onClose={() => setAdjusting(false)} /> : null}
    </Modal>
  )
}

function ItemForm({ item, readOnly, onSaved }: { item: InventoryItem | null; readOnly: boolean; onSaved: () => void }) {
  const queryClient = useQueryClient()
  const currency = useDefaultCurrency()
  const [v, setV] = useState({
    sku: item?.sku ?? '',
    name: item?.name ?? '',
    category: item?.category ?? '',
    barcode: item?.barcode ?? '',
    location: item?.location ?? '',
    trackStock: item?.trackStock ?? true,
    initialQuantity: '0',
    minStock: String(item?.minStock ?? 0),
    unitCost: item?.unitCost != null ? String(item.unitCost).replace('.', ',') : '',
    unitCostCurrency: item?.unitCostCurrency ?? currency,
    isSellable: item?.isSellable ?? false,
    salePrice: item?.salePrice != null ? String(item.salePrice).replace('.', ',') : '',
    salePriceCurrency: item?.salePriceCurrency ?? currency,
    warrantyDays: item?.warrantyDays != null ? String(item.warrantyDays) : '',
    isActive: item?.isActive ?? true,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const costId = useId()
  const priceId = useId()
  const set = <K extends keyof typeof v>(k: K, value: (typeof v)[K]) => setV((x) => ({ ...x, [k]: value }))
  const cost = parseAmount(v.unitCost)
  const price = parseAmount(v.salePrice)
  const margin = cost && price && v.unitCostCurrency === v.salePriceCurrency ? (price - cost) / price : null

  const save = useMutation({
    mutationFn: () => {
      const body: InventoryItemInput = {
        name: v.name.trim(),
        category: v.category.trim() || null,
        barcode: v.barcode.trim() || null,
        location: v.location.trim() || null,
        trackStock: v.trackStock,
        minStock: Number(v.minStock) || 0,
        unitCost: cost,
        unitCostCurrency: cost !== null ? v.unitCostCurrency : null,
        isSellable: v.isSellable,
        salePrice: price,
        salePriceCurrency: price !== null ? v.salePriceCurrency : null,
        warrantyDays: v.warrantyDays ? Number(v.warrantyDays) : null,
        isActive: v.isActive,
      }
      return item ? inventoryApi.update(item.id, body) : inventoryApi.create({ ...body, sku: v.sku.trim(), initialQuantity: v.trackStock ? Number(v.initialQuantity) || 0 : 0 })
    },
    onSuccess: () => {
      toast.success(item ? 'Ítem actualizado' : 'Ítem creado')
      void queryClient.invalidateQueries({ queryKey: ['inventory'] })
      void queryClient.invalidateQueries({ queryKey: ['pos'] })
      onSaved()
    },
    onError: (err) => {
      setErrors(fieldErrors(err))
      toast.error('No se pudo guardar', { description: errorMessage(err) })
    },
  })

  function submit(e: FormEvent) {
    e.preventDefault()
    if (readOnly) return
    if (v.isSellable && price === null) {
      setErrors({ salePrice: 'Indicá el precio de venta.' })
      return
    }
    save.mutate()
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      <fieldset disabled={readOnly} className="grid gap-3 sm:grid-cols-2">
        <Field label="SKU" required error={errors.sku} hint={item ? 'El SKU no se puede cambiar.' : 'Código interno único, ej. PANT-IP13.'}>
          <Input value={v.sku} disabled={!!item} onChange={(e) => set('sku', e.target.value.toUpperCase())} autoFocus={!item} />
        </Field>
        <Field label="Nombre" required error={errors.name}>
          <Input value={v.name} onChange={(e) => set('name', e.target.value)} />
        </Field>
        <Field label="Categoría">
          <Input value={v.category} onChange={(e) => set('category', e.target.value)} placeholder="Pantallas, baterías, accesorios…" />
        </Field>
        <Field label="Código de barras" error={errors.barcode}>
          <Input value={v.barcode} onChange={(e) => set('barcode', e.target.value)} placeholder="Escaneá el código" />
        </Field>
        <Field label="Ubicación">
          <Input value={v.location} onChange={(e) => set('location', e.target.value)} placeholder="Cajón A3, estante 2…" />
        </Field>
        <Field label="Garantía (días)">
          <Input type="number" min={0} value={v.warrantyDays} onChange={(e) => set('warrantyDays', e.target.value)} />
        </Field>

        <div className="sm:col-span-2">
          <Checkbox checked={v.trackStock} onChange={(e) => set('trackStock', e.target.checked)} label="Controlar stock" description="Desmarcalo para servicios o ítems sin existencia física." />
        </div>
        {v.trackStock ? (
          <>
            {!item ? (
              <Field label="Stock inicial">
                <Input type="number" min={0} value={v.initialQuantity} onChange={(e) => set('initialQuantity', e.target.value)} />
              </Field>
            ) : null}
            <Field label="Stock mínimo" hint="Avisa cuando el disponible baja de este número.">
              <Input type="number" min={0} value={v.minStock} onChange={(e) => set('minStock', e.target.value)} />
            </Field>
          </>
        ) : null}

        <Field label="Costo unitario" error={errors.unitCost} controlId={costId}>
          <div className="flex gap-2">
            <Input id={costId} inputMode="decimal" value={v.unitCost} aria-invalid={errors.unitCost ? true : undefined} onChange={(e) => set('unitCost', e.target.value)} />
            <Select className="w-24" value={v.unitCostCurrency} onChange={(e) => set('unitCostCurrency', e.target.value)} aria-label="Moneda del costo">
              {CURRENCIES.map((c) => (
                <option key={c}>{c}</option>
              ))}
            </Select>
          </div>
        </Field>
        <div className="flex items-end pb-2">
          <Checkbox checked={v.isSellable} onChange={(e) => set('isSellable', e.target.checked)} label="Se vende en mostrador" description="Aparece en el punto de venta." />
        </div>
        {v.isSellable ? (
          <Field label="Precio de venta" required error={errors.salePrice} hint={margin !== null ? `Margen ${Math.round(margin * 100)}%` : undefined} controlId={priceId}>
            <div className="flex gap-2">
              <Input id={priceId} inputMode="decimal" value={v.salePrice} aria-invalid={errors.salePrice ? true : undefined} onChange={(e) => set('salePrice', e.target.value)} />
              <Select className="w-24" value={v.salePriceCurrency} onChange={(e) => set('salePriceCurrency', e.target.value)} aria-label="Moneda del precio">
                {CURRENCIES.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </Select>
            </div>
          </Field>
        ) : null}
        {item ? (
          <div className="sm:col-span-2">
            <Checkbox checked={v.isActive} onChange={(e) => set('isActive', e.target.checked)} label="Activo" description="Los inactivos no aparecen en búsquedas ni en el punto de venta." />
          </div>
        ) : null}
      </fieldset>
      {!readOnly ? (
        <div className="flex justify-end gap-2 border-t border-slate-100 pt-3">
          <Button type="submit" variant="primary" loading={save.isPending} disabled={v.name.trim().length < 2 || (!item && v.sku.trim().length < 2)}>
            {item ? 'Guardar cambios' : 'Crear ítem'}
          </Button>
        </div>
      ) : null}
    </form>
  )
}

function AdjustDialog({ item, onClose }: { item: InventoryItem; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [type, setType] = useState<AdjustmentType>('Purchase')
  const [mode, setMode] = useState<'delta' | 'count'>('delta')
  const [value, setValue] = useState('')
  const [reason, setReason] = useState('')
  const n = Number(value)
  const delta = value === '' || Number.isNaN(n) ? null : mode === 'count' ? n - item.quantityOnHand : type === 'Purchase' || type === 'Return' ? Math.abs(n) : n
  const result = delta === null ? null : item.quantityOnHand + delta

  const adjust = useMutation({
    mutationFn: () => inventoryApi.adjust(item.id, { type: mode === 'count' ? 'Correction' : type, deltaQuantity: delta!, reason: reason.trim() || null }),
    onSuccess: () => {
      toast.success('Stock actualizado')
      void queryClient.invalidateQueries({ queryKey: ['inventory'] })
      void queryClient.invalidateQueries({ queryKey: ['pos'] })
      onClose()
    },
    onError: (err) => toast.error('No se pudo ajustar', { description: errorMessage(err) }),
  })

  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title="Ajustar stock"
      description={`${item.name} · stock actual ${item.quantityOnHand}`}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={adjust.isPending} disabled={!delta || (result !== null && result < 0)} onClick={() => adjust.mutate()}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Tabs<'delta' | 'count'>
          value={mode}
          onChange={setMode}
          tabs={[
            { value: 'delta', label: 'Entrada / salida' },
            { value: 'count', label: 'Conteo físico' },
          ]}
        />
        {mode === 'delta' ? (
          <Field label="Motivo">
            <Select value={type} onChange={(e) => setType(e.target.value as AdjustmentType)}>
              {MANUAL_ADJUSTMENTS.map((t) => (
                <option key={t} value={t}>
                  {ADJUSTMENT_TYPE[t]}
                </option>
              ))}
            </Select>
          </Field>
        ) : null}
        <Field label={mode === 'count' ? 'Cantidad contada' : type === 'Purchase' || type === 'Return' ? 'Cantidad que ingresa' : 'Cantidad (+ suma, − resta)'}>
          <Input type="number" value={value} onChange={(e) => setValue(e.target.value)} autoFocus />
        </Field>
        {result !== null ? (
          <p className={cn('text-sm', result < 0 ? 'text-rose-700' : 'text-slate-600')}>
            Quedará en <strong>{result}</strong> ({delta! > 0 ? '+' : ''}
            {delta})
          </p>
        ) : null}
        <Field label="Detalle">
          <Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Factura del proveedor, rotura, conteo mensual…" />
        </Field>
        <Alert tone="slate">Las compras con orden de compra, las ventas y el uso en reparaciones ajustan el stock solos.</Alert>
      </div>
    </Modal>
  )
}

function AdjustmentsList({ itemId }: { itemId: string }) {
  const q = useQuery({ queryKey: ['inventory', itemId, 'adjustments'], queryFn: () => inventoryApi.adjustments(itemId) })
  if (q.isLoading) return <Loading />
  if (q.isError) return <ErrorState error={errorMessage(q.error)} />
  if ((q.data ?? []).length === 0) return <EmptyState title="Sin movimientos" />
  return (
    <Table>
      <thead>
        <tr>
          <Th>Fecha</Th>
          <Th>Tipo</Th>
          <Th>Detalle</Th>
          <Th align="right">Cantidad</Th>
        </tr>
      </thead>
      <tbody className="divide-y divide-slate-100">
        {q.data!.map((a) => (
          <tr key={a.id}>
            <Td className="whitespace-nowrap">{dateTime(a.createdAtUtc)}</Td>
            <Td>{ADJUSTMENT_TYPE[a.type] ?? a.type}</Td>
            <Td className="text-slate-600">{a.reason === 'used_on_order' ? 'Usado en una orden' : a.reason ?? '—'}</Td>
            <Td align="right" className={cn('font-medium', a.deltaQuantity < 0 ? 'text-rose-700' : 'text-emerald-700')}>
              {a.deltaQuantity > 0 ? '+' : ''}
              {a.deltaQuantity}
            </Td>
          </tr>
        ))}
      </tbody>
    </Table>
  )
}

function CompatibilityEditor({ itemId, canManage }: { itemId: string; canManage: boolean }) {
  const queryClient = useQueryClient()
  const q = useQuery({ queryKey: ['inventory', itemId, 'compat'], queryFn: () => inventoryApi.compatibility(itemId) })
  const [brand, setBrand] = useState('')
  const [model, setModel] = useState('')
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['inventory', itemId, 'compat'] })
  const add = useMutation({
    mutationFn: () => inventoryApi.addCompatibility(itemId, brand.trim(), model.trim()),
    onSuccess: () => {
      setModel('')
      refresh()
    },
    onError: (err) => toast.error('No se pudo agregar', { description: errorMessage(err) }),
  })
  const remove = useMutation({
    mutationFn: (id: string) => inventoryApi.removeCompatibility(itemId, id),
    onSuccess: refresh,
    onError: (err) => toast.error('No se pudo quitar', { description: errorMessage(err) }),
  })
  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-600">Al presupuestar una orden se sugieren primero los repuestos compatibles con el equipo.</p>
      {canManage ? (
        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            if (brand.trim() && model.trim()) add.mutate()
          }}
        >
          <Field label="Marca">
            <Input value={brand} onChange={(e) => setBrand(e.target.value)} placeholder="Samsung" />
          </Field>
          <Field label="Modelo">
            <Input value={model} onChange={(e) => setModel(e.target.value)} placeholder="Galaxy A54" />
          </Field>
          <Button type="submit" loading={add.isPending} disabled={!brand.trim() || !model.trim()}>
            Agregar
          </Button>
        </form>
      ) : null}
      {q.isLoading ? (
        <Loading />
      ) : (q.data ?? []).length === 0 ? (
        <EmptyState title="Sin modelos cargados" />
      ) : (
        <ul className="flex flex-wrap gap-2">
          {q.data!.map((c) => (
            <li key={c.id} className="flex items-center gap-1 rounded-full border border-slate-200 bg-white py-1 pl-3 pr-1 text-sm">
              {c.brand} {c.model}
              {canManage ? (
                <button type="button" className="rounded-full px-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600" onClick={() => remove.mutate(c.id)} aria-label={`Quitar ${c.brand} ${c.model}`}>
                  ✕
                </button>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
