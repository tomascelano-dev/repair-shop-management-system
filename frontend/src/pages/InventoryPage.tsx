import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { CreateInventoryItemRequest, InventoryItemResponse } from '../api/types'
import { inventoryApi } from '../api/repairshop'
import { useAuth } from '../auth/AuthContext'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card'
import { Input } from '../components/ui/Input'
import { Button } from '../components/ui/Button'
import { Table, TBody, TD, TH, THead, TR } from '../components/ui/Table'
import { Textarea } from '../components/ui/Textarea'
import { Select } from '../components/ui/Select'

const emptyItem: CreateInventoryItemRequest = {
  sku: '',
  name: '',
  initialQuantity: 0,
  unitCost: null,
  unitCostCurrency: null,
  isActive: true
}

export default function InventoryPage() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const qc = useQueryClient()

  const [q, setQ] = useState('')
  const [editing, setEditing] = useState<InventoryItemResponse | null>(null)
  const [form, setForm] = useState<CreateInventoryItemRequest>(emptyItem)

  const items = useQuery({
    queryKey: ['inventory'],
    queryFn: () => inventoryApi.list({ includeInactive: false, skip: 0, take: 200 })
  })

  const save = useMutation({
    mutationFn: async () => {
      if (!isAdmin) throw new Error('Admin only')
      if (editing) {
        return inventoryApi.update(editing.id, {
          name: form.name,
          isActive: form.isActive
        })
      }
      return inventoryApi.create(form)
    },
    onSuccess: async () => {
      toast.success(editing ? 'Item actualizado' : 'Item creado')
      setEditing(null)
      setForm(emptyItem)
      await qc.invalidateQueries({ queryKey: ['inventory'] })
    },
    onError: (e: any) => toast.error(e?.message ?? 'Error')
  })

  const filtered = useMemo(() => {
    const list = items.data ?? []
    const s = q.trim().toLowerCase()
    if (!s) return list
    return list.filter((i) => [i.sku, i.name].join(' ').toLowerCase().includes(s))
  }, [items.data, q])

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Inventario</CardTitle>
          <p className="mt-1 text-sm text-slate-500">Repuestos, insumos y ajustes.</p>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div className="w-full md:max-w-sm">
              <label className="text-sm font-medium">Buscar</label>
              <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="SKU / Nombre" />
            </div>
            {isAdmin ? (
              <div className="flex gap-2">
                <Button
                  onClick={() => {
                    setEditing(null)
                    setForm(emptyItem)
                  }}
                >
                  Nuevo
                </Button>
              </div>
            ) : null}
          </div>

          <div className="mt-4 overflow-x-auto rounded-2xl border border-slate-200">
            <Table>
              <THead>
                <TR>
                  <TH>SKU</TH>
                  <TH>Nombre</TH>
                  <TH className="text-right">Stock</TH>
                  <TH>Activo</TH>
                  <TH className="text-right">Acciones</TH>
                </TR>
              </THead>
              <TBody>
                {filtered.map((i) => (
                  <TR key={i.id}>
                    <TD className="font-mono text-xs">{i.sku}</TD>
                    <TD>{i.name}</TD>
                    <TD className="text-right font-mono">{i.quantityOnHand}</TD>
                    <TD>{i.isActive ? 'Sí' : 'No'}</TD>
                    <TD className="text-right">
                      {isAdmin ? (
                        <Button
                          variant="ghost"
                          onClick={() => {
                            setEditing(i)
                            setForm({
                              sku: i.sku,
                              name: i.name,
                              initialQuantity: 0,
                              unitCost: i.unitCost ?? null,
                              unitCostCurrency: i.unitCostCurrency ?? null,
                              isActive: i.isActive
                            })
                          }}
                        >
                          Editar
                        </Button>
                      ) : null}
                    </TD>
                  </TR>
                ))}
              </TBody>
            </Table>
          </div>

          {items.isLoading ? <div className="mt-3 text-sm text-slate-500">Cargando…</div> : null}
          {items.isError ? <div className="mt-3 text-sm text-rose-700">Error al cargar inventario</div> : null}
        </CardContent>
      </Card>

      {isAdmin ? (
        <Card>
          <CardHeader>
            <CardTitle>{editing ? 'Editar item' : 'Nuevo item'}</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 md:grid-cols-2">
              <div>
                <label className="text-sm font-medium">SKU</label>
                <Input
                  value={form.sku}
                  onChange={(e) => setForm((s) => ({ ...s, sku: e.target.value }))}
                  disabled={!!editing}
                  placeholder="BAT-IP13-PRO"
                />
              </div>
              <div>
                <label className="text-sm font-medium">Nombre</label>
                <Input value={form.name} onChange={(e) => setForm((s) => ({ ...s, name: e.target.value }))} />
              </div>
              {!editing ? (
                <div>
                  <label className="text-sm font-medium">Cantidad inicial</label>
                  <Input
                    type="number"
                    value={form.initialQuantity}
                    onChange={(e) => setForm((s) => ({ ...s, initialQuantity: Number(e.target.value) }))}
                  />
                </div>
              ) : (
                <div>
                  <label className="text-sm font-medium">Activo</label>
                  <Select
                    value={form.isActive ? 'true' : 'false'}
                    onChange={(e) => setForm((s) => ({ ...s, isActive: e.target.value === 'true' }))}
                  >
                    <option value="true">Sí</option>
                    <option value="false">No</option>
                  </Select>
                </div>
              )}
              <div>
                <label className="text-sm font-medium">Costo unitario (opcional)</label>
                <Input
                  type="number"
                  value={form.unitCost ?? ''}
                  onChange={(e) =>
                    setForm((s) => ({ ...s, unitCost: e.target.value ? Number(e.target.value) : null }))
                  }
                />
              </div>
              <div>
                <label className="text-sm font-medium">Moneda</label>
                <Input
                  value={form.unitCostCurrency ?? ''}
                  onChange={(e) => setForm((s) => ({ ...s, unitCostCurrency: e.target.value || null }))}
                  placeholder="USD / ARS"
                />
              </div>
            </div>

            <div className="mt-4 flex justify-end">
              <Button variant="primary" onClick={() => save.mutate()} disabled={save.isPending}>
                {save.isPending ? 'Guardando…' : 'Guardar'}
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  )
}
