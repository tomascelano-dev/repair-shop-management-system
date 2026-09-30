import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { CreateMessageTemplateRequest, MessageTemplateResponse } from '../api/types'
import { templatesApi } from '../api/repairshop'
import { useAuth } from '../auth/AuthContext'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card'
import { Input } from '../components/ui/Input'
import { Textarea } from '../components/ui/Textarea'
import { Button } from '../components/ui/Button'
import { Select } from '../components/ui/Select'
import { Table, TBody, TD, TH, THead, TR } from '../components/ui/Table'

const emptyForm: CreateMessageTemplateRequest = {
  key: '',
  title: '',
  body: '',
  isActive: true
}

export default function TemplatesPage() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const qc = useQueryClient()

  const [q, setQ] = useState('')
  const [editing, setEditing] = useState<MessageTemplateResponse | null>(null)
  const [form, setForm] = useState<CreateMessageTemplateRequest>(emptyForm)

  const templates = useQuery({
    queryKey: ['templates'],
    queryFn: () => templatesApi.list({ includeInactive: true })
  })

  const save = useMutation({
    mutationFn: async () => {
      if (!isAdmin) throw new Error('Admin only')
      if (editing) {
        return templatesApi.update(editing.id, {
          title: form.title,
          body: form.body,
          isActive: form.isActive
        })
      }
      return templatesApi.create(form)
    },
    onSuccess: async () => {
      toast.success(editing ? 'Plantilla actualizada' : 'Plantilla creada')
      setEditing(null)
      setForm(emptyForm)
      await qc.invalidateQueries({ queryKey: ['templates'] })
    },
    onError: (e: any) => toast.error('No se pudo guardar', { description: e?.message })
  })

  const remove = useMutation({
    mutationFn: (id: string) => templatesApi.remove(id),
    onSuccess: async () => {
      toast.success('Plantilla eliminada')
      await qc.invalidateQueries({ queryKey: ['templates'] })
    },
    onError: (e: any) => toast.error('No se pudo eliminar', { description: e?.message })
  })

  const filtered = useMemo(() => {
    const s = q.trim().toLowerCase()
    const all = templates.data ?? []
    if (!s) return all
    return all.filter((t) => [t.key, t.title, t.body].join(' ').toLowerCase().includes(s))
  }, [templates.data, q])

  function startCreate() {
    setEditing(null)
    setForm(emptyForm)
  }

  function startEdit(t: MessageTemplateResponse) {
    setEditing(t)
    setForm({ key: t.key, title: t.title, body: t.body, isActive: t.isActive })
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Plantillas</h1>
          <p className="text-sm text-slate-500">Mensajes reutilizables (WhatsApp, etc.).</p>
        </div>
        {isAdmin ? (
          <Button variant="primary" onClick={startCreate}>
            Nueva plantilla
          </Button>
        ) : null}
      </div>

      {isAdmin ? (
        <Card>
          <CardHeader>
            <CardTitle>{editing ? 'Editar plantilla' : 'Crear plantilla'}</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
              <div>
                <label className="text-sm font-medium">Key</label>
                <Input
                  value={form.key}
                  disabled={!!editing}
                  onChange={(e) => setForm((f) => ({ ...f, key: e.target.value }))}
                  placeholder="order.status.ready"
                />
              </div>
              <div>
                <label className="text-sm font-medium">Estado</label>
                <Select value={String(form.isActive)} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.value === 'true' }))}>
                  <option value="true">Activa</option>
                  <option value="false">Inactiva</option>
                </Select>
              </div>
              <div className="md:col-span-2">
                <label className="text-sm font-medium">Título</label>
                <Input value={form.title} onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))} />
              </div>
              <div className="md:col-span-2">
                <label className="text-sm font-medium">Body</label>
                <Textarea value={form.body} onChange={(e) => setForm((f) => ({ ...f, body: e.target.value }))} />
              </div>
              <div className="md:col-span-2 flex items-center justify-end gap-2">
                {editing ? (
                  <Button variant="ghost" onClick={startCreate}>
                    Cancelar
                  </Button>
                ) : null}
                <Button variant="primary" onClick={() => save.mutate()} disabled={save.isPending}>
                  {save.isPending ? 'Guardando…' : 'Guardar'}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent>
            <div className="text-sm text-slate-500">Solo Admin puede crear/editar/eliminar plantillas.</div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <CardTitle>Listado</CardTitle>
            <div className="w-full md:w-80">
              <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Buscar por key, título o texto…" />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {templates.isLoading ? (
            <div className="text-sm text-slate-500">Cargando…</div>
          ) : templates.isError ? (
            <div className="text-sm text-rose-700">Error al cargar.</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <THead>
                  <TR>
                    <TH>Key</TH>
                    <TH>Título</TH>
                    <TH>Activo</TH>
                    <TH className="text-right">Acciones</TH>
                  </TR>
                </THead>
                <TBody>
                  {filtered.map((t) => (
                    <TR key={t.id}>
                      <TD className="font-mono text-xs">{t.key}</TD>
                      <TD>{t.title}</TD>
                      <TD>{t.isActive ? 'Sí' : 'No'}</TD>
                      <TD className="text-right">
                        <div className="inline-flex items-center gap-2">
                          <Button variant="ghost" onClick={() => startEdit(t)}>
                            Editar
                          </Button>
                          {isAdmin ? (
                            <Button
                              variant="danger"
                              onClick={() => {
                                if (confirm(`Eliminar plantilla "${t.key}"?`)) remove.mutate(t.id)
                              }}
                              disabled={remove.isPending}
                            >
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
    </div>
  )
}
