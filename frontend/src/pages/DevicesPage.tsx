import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { DeviceCreateRequest, DeviceResponse } from '../api/types'
import { customersApi, devicesApi } from '../api/repairshop'
import { useAuth } from '../auth/AuthContext'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card'
import { Input } from '../components/ui/Input'
import { Textarea } from '../components/ui/Textarea'
import { Select } from '../components/ui/Select'
import { Button } from '../components/ui/Button'
import { Table, TBody, TD, TH, THead, TR } from '../components/ui/Table'

const emptyForm: DeviceCreateRequest = {
  customerId: '',
  brand: '',
  model: '',
  serialNumber: '',
  notes: ''
}

export default function DevicesPage() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const qc = useQueryClient()

  const [q, setQ] = useState('')
  const [customerFilterId, setCustomerFilterId] = useState<string>('')
  const [editing, setEditing] = useState<DeviceResponse | null>(null)
  const [form, setForm] = useState<DeviceCreateRequest>(emptyForm)

  const customers = useQuery({
    queryKey: ['customers'],
    queryFn: () => customersApi.list({ skip: 0, take: 200 })
  })

  // default customer filter
  useEffect(() => {
    if (!customerFilterId && (customers.data?.length ?? 0) > 0) {
      setCustomerFilterId(customers.data![0].id)
    }
  }, [customerFilterId, customers.data])

  const devices = useQuery({
    queryKey: ['devices', customerFilterId],
    enabled: !!customerFilterId,
    queryFn: () => devicesApi.listByCustomer(customerFilterId, { skip: 0, take: 200 })
  })

  const save = useMutation({
    mutationFn: async () => {
      if (!isAdmin) throw new Error('Admin only')
      if (!editing) return devicesApi.create(form)
      return devicesApi.update(editing.id, {
        brand: form.brand,
        model: form.model,
        serialNumber: form.serialNumber,
        notes: form.notes
      })
    },
    onSuccess: async () => {
      toast.success(editing ? 'Equipo actualizado' : 'Equipo creado')
      setEditing(null)
      setForm(emptyForm)
      await qc.invalidateQueries({ queryKey: ['devices'] })
    },
    onError: (e: any) => toast.error(e?.message ?? 'Error')
  })

  const remove = useMutation({
    mutationFn: async (id: string) => {
      if (!isAdmin) throw new Error('Admin only')
      await devicesApi.remove(id)
    },
    onSuccess: async () => {
      toast.success('Equipo eliminado')
      await qc.invalidateQueries({ queryKey: ['devices'] })
    },
    onError: (e: any) => toast.error(e?.message ?? 'Error')
  })

  const filtered = useMemo(() => {
    const s = q.trim().toLowerCase()
    const list = devices.data ?? []
    if (!s) return list
    return list.filter((d) =>
      [d.brand, d.model, d.serialNumber ?? '', d.notes ?? '', d.customerId].join(' ').toLowerCase().includes(s)
    )
  }, [devices.data, q])

  const customerName = (id: string) => customers.data?.find((c) => c.id === id)?.fullName ?? id.slice(0, 8)

  const startCreate = () => {
    setEditing(null)
    const first = customers.data?.[0]?.id ?? ''
    setCustomerFilterId((prev) => prev || first)
    setForm({ ...emptyForm, customerId: first })
  }

  const startEdit = (d: DeviceResponse) => {
    setEditing(d)
    setForm({
      customerId: d.customerId,
      brand: d.brand,
      model: d.model,
      serialNumber: d.serialNumber ?? '',
      notes: d.notes ?? ''
    })
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Equipos</CardTitle>
          <p className="mt-1 text-sm text-slate-500">Modelos por cliente. Admin puede crear/editar/eliminar.</p>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div className="w-full md:max-w-sm">
              <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Buscar…" />
            </div>
            <div className="w-full md:max-w-xs">
              <Select
                value={customerFilterId}
                onChange={(e) => setCustomerFilterId(e.target.value)}
                disabled={customers.isLoading || (customers.data?.length ?? 0) === 0}
              >
                <option value="">— Elegí cliente —</option>
                {(customers.data ?? []).map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.fullName}
                  </option>
                ))}
              </Select>
            </div>
            {isAdmin ? (
              <Button variant="primary" onClick={startCreate}>
                Nuevo equipo
              </Button>
            ) : null}
          </div>

          {isAdmin ? (
            <div className="mt-4 rounded-2xl border border-slate-200 bg-slate-50 p-4">
              <div className="mb-2 text-sm font-semibold">{editing ? 'Editar equipo' : 'Crear equipo'}</div>
              <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                <div>
                  <label className="text-sm font-medium">Cliente</label>
                  <Select
                    value={form.customerId}
                    onChange={(e) => setForm((f) => ({ ...f, customerId: e.target.value }))}
                  >
                    {(customers.data ?? []).map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.fullName}
                      </option>
                    ))}
                  </Select>
                </div>
                <div>
                  <label className="text-sm font-medium">Marca</label>
                  <Input value={form.brand} onChange={(e) => setForm((f) => ({ ...f, brand: e.target.value }))} />
                </div>
                <div>
                  <label className="text-sm font-medium">Modelo</label>
                  <Input value={form.model} onChange={(e) => setForm((f) => ({ ...f, model: e.target.value }))} />
                </div>
                <div>
                  <label className="text-sm font-medium">Serial</label>
                  <Input
                    value={form.serialNumber ?? ''}
                    onChange={(e) => setForm((f) => ({ ...f, serialNumber: e.target.value }))}
                  />
                </div>
                <div className="md:col-span-2">
                  <label className="text-sm font-medium">Notas</label>
                  <Textarea value={form.notes ?? ''} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} />
                </div>
              </div>
              <div className="mt-3 flex items-center gap-2">
                <Button variant="primary" onClick={() => save.mutate()} disabled={save.isPending}>
                  {save.isPending ? 'Guardando…' : 'Guardar'}
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => {
                    setEditing(null)
                    setForm(emptyForm)
                  }}
                >
                  Cancelar
                </Button>
              </div>
            </div>
          ) : null}

          <div className="mt-4 overflow-x-auto">
            <Table>
              <THead>
                <TR>
                  <TH>Cliente</TH>
                  <TH>Marca</TH>
                  <TH>Modelo</TH>
                  <TH>Serial</TH>
                  <TH className="w-[160px]">Acciones</TH>
                </TR>
              </THead>
              <TBody>
                {(filtered ?? []).map((d) => (
                  <TR key={d.id}>
                    <TD className="font-medium">{customerName(d.customerId)}</TD>
                    <TD>{d.brand}</TD>
                    <TD>{d.model}</TD>
                    <TD className="font-mono text-xs text-slate-600">{d.serialNumber ?? '-'}</TD>
                    <TD>
                      <div className="flex gap-2">
                        <Button variant="ghost" onClick={() => startEdit(d)} disabled={!isAdmin}>
                          Editar
                        </Button>
                        <Button
                          variant="danger"
                          onClick={() => remove.mutate(d.id)}
                          disabled={!isAdmin || remove.isPending}
                        >
                          Borrar
                        </Button>
                      </div>
                    </TD>
                  </TR>
                ))}
              </TBody>
            </Table>
          </div>

          {devices.isLoading ? <div className="mt-4 text-sm text-slate-500">Cargando…</div> : null}
          {devices.isError ? <div className="mt-4 text-sm text-rose-600">Error cargando equipos</div> : null}
        </CardContent>
      </Card>
    </div>
  )
}
