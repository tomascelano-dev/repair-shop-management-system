import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ordersApi } from '../../../api/endpoints'
import { errorMessage } from '../../../api/http'
import type { InventoryItem, RepairOrder } from '../../../api/types'
import { useSession } from '../../../auth/session'
import { Alert, Badge, Button, Card, Checkbox, EmptyState, Field, Input, Loading, Table, Td, Th } from '../../../components/ui'
import { dateTime, money, parseAmount } from '../../../lib/format'
import { ItemPicker } from '../../inventory/ItemPicker'

export function PartsPanel({ order }: { order: RepairOrder }) {
  const { can } = useSession()
  const queryClient = useQueryClient()
  const parts = useQuery({ queryKey: ['order', order.id, 'parts'], queryFn: () => ordersApi.parts(order.id) })
  const reservations = useQuery({ queryKey: ['order', order.id, 'reservations'], queryFn: () => ordersApi.reservations(order.id) })
  const [item, setItem] = useState<InventoryItem | null>(null)
  const [qty, setQty] = useState(1)
  const [charge, setCharge] = useState(false)
  const [price, setPrice] = useState('')
  const canUse = can('orders.work') && order.status !== 'Delivered' && order.status !== 'Cancelled'
  const reservedForItem = (reservations.data ?? []).filter((r) => item && r.inventoryItemId === item.id && r.status === 'Active').reduce((s, r) => s + r.quantity - r.consumedQuantity, 0)
  const [brand, ...rest] = order.deviceLabel.replace(/\s*\(.*\)\s*$/, '').split(' ')

  const use = useMutation({
    mutationFn: () =>
      ordersApi.usePart(order.id, { inventoryItemId: item!.id, quantityUsed: qty, unitPrice: charge ? parseAmount(price) : null, unitPriceCurrency: charge ? order.currency : null }),
    onSuccess: () => {
      toast.success('Repuesto descontado del stock')
      setItem(null)
      setQty(1)
      setCharge(false)
      setPrice('')
      void queryClient.invalidateQueries({ queryKey: ['order', order.id] })
      void queryClient.invalidateQueries({ queryKey: ['inventory'] })
    },
    onError: (err) => toast.error('No se pudo usar el repuesto', { description: errorMessage(err) }),
  })

  return (
    <div className="space-y-4">
      {canUse ? (
        <Card title="Usar repuesto del stock">
          <div className="space-y-3">
            {item ? (
              <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-slate-50 p-3 text-sm">
                <span>
                  <strong>{item.name}</strong> <span className="text-slate-500">({item.sku})</span>
                  <span className="ml-2 text-slate-500">{item.trackStock ? `${item.availableQuantity} disponibles` : 'sin control de stock'}</span>
                </span>
                <Button size="sm" variant="ghost" onClick={() => setItem(null)}>
                  Cambiar
                </Button>
              </div>
            ) : (
              <ItemPicker brand={brand} model={rest.join(' ')} onSelect={(i) => { setItem(i); setPrice(i.salePrice ? String(i.salePrice).replace('.', ',') : '') }} />
            )}
            {item ? (
              <>
                {reservedForItem > 0 ? <Alert tone="indigo">Este repuesto está en el presupuesto aprobado ({reservedForItem} reservado): se descuenta de la reserva y no se cobra de nuevo.</Alert> : null}
                <div className="grid gap-3 sm:grid-cols-3">
                  <Field label="Cantidad">
                    <Input type="number" min={1} value={qty} onChange={(e) => setQty(Math.max(1, Number(e.target.value) || 1))} />
                  </Field>
                  <div className="flex items-end pb-2 sm:col-span-2">
                    <Checkbox label="Cobrarlo como extra al cliente" description="Para repuestos que no estaban en el presupuesto aprobado." checked={charge} onChange={(e) => setCharge(e.target.checked)} />
                  </div>
                  {charge ? (
                    <Field label={`Precio unitario (${order.currency})`}>
                      <Input inputMode="decimal" value={price} onChange={(e) => setPrice(e.target.value)} />
                    </Field>
                  ) : null}
                </div>
                <div className="flex justify-end">
                  <Button variant="primary" loading={use.isPending} disabled={charge && !parseAmount(price)} onClick={() => use.mutate()}>
                    Descontar del stock
                  </Button>
                </div>
              </>
            ) : null}
          </div>
        </Card>
      ) : null}

      {(reservations.data ?? []).length > 0 ? (
        <Card title="Reservas del presupuesto aprobado">
          <ul className="space-y-1 text-sm">
            {reservations.data!.map((r) => (
              <li key={r.id} className="flex justify-between">
                <span>{r.itemName}</span>
                <span className="text-slate-600">
                  {r.consumedQuantity}/{r.quantity} usados · <Badge tone={r.status === 'Active' ? 'indigo' : r.status === 'Consumed' ? 'green' : 'slate'}>{r.status === 'Active' ? 'Reservado' : r.status === 'Consumed' ? 'Usado' : 'Liberado'}</Badge>
                </span>
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      <Card title="Repuestos usados" padded={false}>
        {parts.isLoading ? (
          <Loading />
        ) : (parts.data ?? []).length === 0 ? (
          <div className="p-4">
            <EmptyState title="Todavía no se usaron repuestos" />
          </div>
        ) : (
          <Table>
            <thead className="bg-slate-50">
              <tr>
                <Th>Repuesto</Th>
                <Th align="right">Cant.</Th>
                <Th align="right">Costo</Th>
                <Th align="right">Cobrado</Th>
                <Th>Fecha</Th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {parts.data!.map((p) => (
                <tr key={p.id}>
                  <Td>
                    {p.itemName} <span className="text-xs text-slate-500">{p.itemSku}</span>
                  </Td>
                  <Td align="right">{p.quantityUsed}</Td>
                  <Td align="right">{p.unitCost !== null && p.unitCost !== undefined ? money(p.unitCost * p.quantityUsed) : '—'}</Td>
                  <Td align="right">{p.chargedToCustomer ? money((p.unitPrice ?? 0) * p.quantityUsed, p.unitPriceCurrency ?? order.currency) : <span className="text-slate-400">Incluido</span>}</Td>
                  <Td className="text-slate-500">{dateTime(p.createdAtUtc)}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>
    </div>
  )
}
