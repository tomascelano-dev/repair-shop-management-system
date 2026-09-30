import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { RepairOrderCreateRequest, RepairOrderResponse } from '../api/types'
import { customersApi, devicesApi, ordersApi } from '../api/repairshop'
import { useAuth } from '../auth/AuthContext'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card'
import { Input } from '../components/ui/Input'
import { Select } from '../components/ui/Select'
import { Button } from '../components/ui/Button'
import { Table, TBody, TD, TH, THead, TR } from '../components/ui/Table'
import StatusBadge from '../components/StatusBadge'

const emptyForm: RepairOrderCreateRequest = {
  customerId: '',
  deviceId: '',
  issueDescription: '',
  notes: ''
}

export default function OrdersPage() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const qc = useQueryClient()

  const [q, setQ] = useState('')
  const [form, setForm] = useState<RepairOrderCreateRequest>(emptyForm)

  const orders = useQuery({
    queryKey: ['orders'],
    queryFn: () => ordersApi.list({ skip: 0, take: 100 })
  })
  const customers = useQuery({
    queryKey: ['customers'],
    queryFn: () => customersApi.list({ skip: 0, take: 200 })
  })
  const devices = useQuery({
    queryKey: ['devices', form.customerId],
    enabled: Boolean(form.customerId),
    queryFn: () => devicesApi.listByCustomer(form.customerId, { skip: 0, take: 200 })
  })

  const create = useMutation({
    mutationFn: () => ordersApi.create(form),
    onSuccess: async () => {
      toast.success('Orden creada')
      setForm(emptyForm)
      await qc.invalidateQueries({ queryKey: ['orders'] })
    },
    onError: (e: any) => toast.error('No se pudo crear', { description: e?.message })
  })

  const customersById = useMemo(() => {
    const m = new Map<string, string>()
    ;(customers.data ?? []).forEach((c) => m.set(c.id, c.fullName))
    return m
  }, [customers.data])

  const devicesById = useMemo(() => {
    const m = new Map<string, string>()
    ;(devices.data ?? []).forEach((d) => m.set(d.id, `${d.brand} ${d.model}`))
    return m
  }, [devices.data])

  const filtered = useMemo(() => {
    const s = q.trim().toLowerCase()
    const list = orders.data ?? []
    if (!s) return list
    return list.filter((o) => {
      const txt = [
        o.issueDescription,
        o.notes ?? '',
        o.status,
        customersById.get(o.customerId) ?? '',
        devicesById.get(o.deviceId) ?? ''
      ]
        .join(' ')
        .toLowerCase()
      return txt.includes(s)
    })
  }, [orders.data, q, customersById, devicesById])

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Órdenes</CardTitle>
          <p className="mt-1 text-sm text-slate-500">
            Flujo principal: crear, cambiar estado, cobrar y generar mensajes.
          </p>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-col gap-3 md:flex-row md:items-end">
            <div className="flex-1">
              <label className="text-sm font-medium">Buscar</label>
              <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Cliente, equipo, estado…" />
            </div>
          </div>

          <div className="rounded-2xl border border-slate-200 overflow-hidden">
            <Table>
              <THead>
                <TR>
                  <TH>Estado</TH>
                  <TH>Cliente</TH>
                  <TH>Equipo</TH>
                  <TH>Problema</TH>
                  <TH>Updated</TH>
                </TR>
              </THead>
              <TBody>
                {filtered.map((o) => (
                  <TR key={o.id}>
                    <TD>
                      <StatusBadge status={o.status} />
                    </TD>
                    <TD>{customersById.get(o.customerId) ?? o.customerId}</TD>
                    <TD>{devicesById.get(o.deviceId) ?? o.deviceId}</TD>
                    <TD>
                      <Link className="font-medium hover:underline" to={`/app/orders/${o.id}`}>
                        {o.issueDescription}
                      </Link>
                      {o.quoteAmount != null ? (
                        <div className="text-xs text-slate-500">
                          Presupuesto: {o.quoteAmount} {o.quoteCurrency}
                        </div>
                      ) : null}
                    </TD>
                    <TD className="text-xs text-slate-500">{new Date(o.updatedAtUtc).toLocaleString()}</TD>
                  </TR>
                ))}
                {filtered.length === 0 ? (
                  <TR>
                    <TD colSpan={5} className="text-slate-500">
                      {orders.isLoading ? 'Cargando…' : 'Sin resultados.'}
                    </TD>
                  </TR>
                ) : null}
              </TBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Nueva orden</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <div>
              <label className="text-sm font-medium">Cliente</label>
              <Select value={form.customerId} onChange={(e) => setForm((f) => ({ ...f, customerId: e.target.value, deviceId: '' }))}>
                <option value="">Seleccionar…</option>
                {(customers.data ?? []).map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.fullName}
                  </option>
                ))}
              </Select>
            </div>
            <div>
              <label className="text-sm font-medium">Equipo</label>
              <Select disabled={!form.customerId || devices.isLoading} value={form.deviceId} onChange={(e) => setForm((f) => ({ ...f, deviceId: e.target.value }))}>
                <option value="">Seleccionar…</option>
                {devices.isLoading ? (
                  <option value="">Cargando…</option>
                ) : (devices.data ?? []).length === 0 ? (
                  <option value="">Sin equipos para este cliente</option>
                ) : (
                  (devices.data ?? []).map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.brand} {d.model}
                    </option>
                  ))
                )}
              </Select>
            </div>
            <div className="md:col-span-2">
              <label className="text-sm font-medium">Problema</label>
              <Input
                value={form.issueDescription}
                onChange={(e) => setForm((f) => ({ ...f, issueDescription: e.target.value }))}
                placeholder="Ej: No enciende / cambio módulo / batería…"
              />
            </div>
            <div className="md:col-span-2">
              <label className="text-sm font-medium">Notas</label>
              <Input value={form.notes ?? ''} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} />
            </div>
          </div>

          <div className="mt-4 flex justify-end">
            <Button
              variant="primary"
              disabled={create.isPending || !form.customerId || !form.deviceId || form.issueDescription.trim().length < 5}
              onClick={() => create.mutate()}
            >
              {create.isPending ? 'Creando…' : 'Crear orden'}
            </Button>
          </div>

          {!isAdmin ? (
            <p className="mt-3 text-xs text-slate-500">
              Nota: acciones de borrado son Admin-only. Crear orden puede hacerlo cualquier rol.
            </p>
          ) : null}
        </CardContent>
      </Card>
    </div>
  )
}