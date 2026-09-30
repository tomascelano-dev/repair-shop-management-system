import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { CustomerCreateRequest, CustomerResponse } from '../api/types'
import { customersApi } from '../api/repairshop'
import { useAuth } from '../auth/AuthContext'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card'
import { Input } from '../components/ui/Input'
import { Textarea } from '../components/ui/Textarea'
import { Button } from '../components/ui/Button'
import { Table, TBody, TD, TH, THead, TR } from '../components/ui/Table'

const emptyForm: CustomerCreateRequest = { fullName: '', phone: '', notes: '' }

export default function CustomersPage() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const qc = useQueryClient()

  const [q, setQ] = useState('')
  const [editing, setEditing] = useState<CustomerResponse | null>(null)
  const [form, setForm] = useState<CustomerCreateRequest>(emptyForm)

  const customers = useQuery({
    queryKey: ['customers'],
    queryFn: () => customersApi.list({ skip: 0, take: 200 })
  })

  const save = useMutation({
    mutationFn: async () => {
      if (!isAdmin) throw new Error('Admin only')
      if (editing) return customersApi.update(editing.id, form)
      return customersApi.create(form)
    },
    onSuccess: async () => {
      toast.success(editing ? 'Cliente actualizado' : 'Cliente creado')
      setEditing(null)
      setForm(emptyForm)
      await qc.invalidateQueries({ queryKey: ['customers'] })
    },
    onError: (e: any) => {
      toast.error('No se pudo guardar', { description: e?.message })
    }
  })

  const remove = useMutation({
    mutationFn: async (id: string) => {
      if (!isAdmin) throw new Error('Admin only')
      await customersApi.remove(id)
    },
    onSuccess: async () => {
      toast.success('Cliente eliminado')
      await qc.invalidateQueries({ queryKey: ['customers'] })
    },
    onError: (e: any) => toast.error('No se pudo eliminar', { description: e?.message })
  })

  const filtered = useMemo(() => {
    const items = customers.data ?? []
    const s = q.trim().toLowerCase()
    if (!s) return items
    return items.filter((c) => [c.fullName, c.phone, c.notes ?? ''].join(' ').toLowerCase().includes(s))
  }, [customers.data, q])

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <div>
              <CardTitle>Clientes</CardTitle>
              <p className="mt-1 text-sm text-slate-500">Gestión básica de clientes.</p>
            </div>
            <Input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Buscar por nombre, teléfono o notas…"
              className="max-w-sm"
            />
          </div>
        </CardHeader>
        <CardContent>
          {customers.isLoading ? (
            <div className="text-sm text-slate-500">Cargando…</div>
          ) : customers.isError ? (
            <div className="text-sm text-rose-600">Error al cargar clientes.</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <THead>
                  <TR>
                    <TH>Nombre</TH>
                    <TH>Teléfono</TH>
                    <TH>Notas</TH>
                    <TH className="w-[140px]"></TH>
                  </TR>
                </THead>
                <TBody>
                  {filtered.map((c) => (
                    <TR key={c.id}>
                      <TD className="font-medium">{c.fullName}</TD>
                      <TD className="font-mono">{c.phone}</TD>
                      <TD className="text-slate-600">{c.notes ?? ''}</TD>
                      <TD>
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="ghost"
                            onClick={() => {
                              setEditing(c)
                              setForm({ fullName: c.fullName, phone: c.phone, notes: c.notes ?? '' })
                            }}
                          >
                            Editar
                          </Button>
                          {isAdmin ? (
                            <Button variant="danger" onClick={() => remove.mutate(c.id)} disabled={remove.isPending}>
                              Eliminar
                            </Button>
                          ) : null}
                        </div>
                      </TD>
                    </TR>
                  ))}
                </TBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{editing ? 'Editar cliente' : 'Nuevo cliente'}</CardTitle>
          {!isAdmin ? (
            <p className="mt-1 text-sm text-slate-500">Solo Admin puede crear/editar/eliminar.</p>
          ) : null}
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <div>
              <label className="text-sm font-medium">Nombre</label>
              <Input
                value={form.fullName}
                onChange={(e) => setForm((s) => ({ ...s, fullName: e.target.value }))}
                placeholder="Juan Pérez"
                disabled={!isAdmin}
              />
            </div>
            <div>
              <label className="text-sm font-medium">Teléfono</label>
              <Input
                value={form.phone}
                onChange={(e) => setForm((s) => ({ ...s, phone: e.target.value }))}
                placeholder="11 5555-5555"
                disabled={!isAdmin}
              />
            </div>
            <div className="md:col-span-2">
              <label className="text-sm font-medium">Notas</label>
              <Textarea
                value={form.notes ?? ''}
                onChange={(e) => setForm((s) => ({ ...s, notes: e.target.value }))}
                placeholder="Observaciones…"
                disabled={!isAdmin}
              />
            </div>
          </div>

          <div className="mt-4 flex items-center justify-end gap-2">
            {editing ? (
              <Button
                variant="ghost"
                onClick={() => {
                  setEditing(null)
                  setForm(emptyForm)
                }}
              >
                Cancelar
              </Button>
            ) : null}

            <Button
              variant="primary"
              onClick={() => save.mutate()}
              disabled={!isAdmin || save.isPending}
            >
              {save.isPending ? 'Guardando…' : editing ? 'Guardar cambios' : 'Crear cliente'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
