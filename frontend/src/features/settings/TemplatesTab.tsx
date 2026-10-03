import { useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { templatesApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { MessageTemplate } from '../../api/types'
import { Badge, Button, Card, Checkbox, ConfirmDialog, EmptyState, ErrorState, Field, Input, Loading, Modal, SearchInput, Textarea } from '../../components/ui'
import { relative } from '../../lib/format'

const GROUPS: { prefix: string; label: string }[] = [
  { prefix: 'order.status.', label: 'Cambios de estado' },
  { prefix: 'order.quote.', label: 'Presupuestos' },
  { prefix: 'order.', label: 'Órdenes' },
  { prefix: 'msg.', label: 'Atención' },
  { prefix: 'followup.', label: 'Seguimiento' },
  { prefix: '', label: 'Personalizadas' },
]

const SAMPLE: Record<string, string> = {
  customer_name: 'María López',
  customer_first_name: 'María',
  customer_phone: '11 5555-1234',
  device_brand: 'Samsung',
  device_model: 'Galaxy A54',
  device_label: 'Samsung Galaxy A54',
  device_serial: 'R58N12345',
  device_imei: '356938035643809',
  issue_description: 'Pantalla rota, no da imagen',
  order_code: 'OT-000123',
  order_status_label: 'Listo para retirar',
  order_total: '$ 85.000,00',
  paid_total: '$ 50.000,00',
  balance_due: '$ 35.000,00',
  promised_date: '10/10/2026',
  quote_amount: '$ 85.000,00',
  quote_currency: 'ARS',
  quote_items: '• Módulo de pantalla x1: $ 70.000,00\n• Mano de obra x1: $ 15.000,00',
  quote_valid_until: '17/10/2026',
  warranty_days: '90',
  warranty_expires_at: '08/01/2027',
  cancellation_reason: 'El cliente no aprobó el presupuesto',
  technician_name: 'Juan',
  tracking_url: 'https://taller.ejemplo.com/t/abc123',
  feedback_url: 'https://taller.ejemplo.com/t/abc123?encuesta=1',
  google_review_url: 'https://g.page/r/ejemplo',
  shop_name: 'Mi Taller',
  shop_phone: '11 5555-0000',
  shop_address: 'Av. Siempreviva 742',
  pickup_address: 'Av. Siempreviva 742',
  pickup_hours: 'Lun a vie 10 a 19 h',
}

function render(body: string) {
  return body.replace(/\{\{\s*([a-z0-9_]+)\s*\}\}/gi, (_, k: string) => SAMPLE[k] ?? `[${k}]`)
}

export function TemplatesTab() {
  const queryClient = useQueryClient()
  const templates = useQuery({ queryKey: ['templates', 'all'], queryFn: () => templatesApi.list(true) })
  const [q, setQ] = useState('')
  const [editing, setEditing] = useState<MessageTemplate | 'new' | null>(null)

  const grouped = useMemo(() => {
    const term = q.trim().toLowerCase()
    const list = (templates.data ?? []).filter((t) => !term || t.title.toLowerCase().includes(term) || t.key.toLowerCase().includes(term) || t.body.toLowerCase().includes(term))
    const used = new Set<string>()
    return GROUPS.map((g) => {
      const items = list.filter((t) => !used.has(t.id) && t.key.startsWith(g.prefix))
      items.forEach((t) => used.add(t.id))
      return { ...g, items }
    }).filter((g) => g.items.length > 0)
  }, [templates.data, q])

  if (templates.isLoading) return <Loading />
  if (templates.isError) return <ErrorState error={errorMessage(templates.error)} onRetry={() => void templates.refetch()} />

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <SearchInput className="min-w-64 flex-1" value={q} onChange={setQ} placeholder="Buscar plantilla…" />
        <Button variant="primary" onClick={() => setEditing('new')}>
          + Nueva plantilla
        </Button>
      </div>
      <p className="text-sm text-slate-600">
        Estos textos se usan en los avisos automáticos y en los mensajes que mandás desde cada orden. Usá variables como <code className="rounded bg-slate-100 px-1 text-xs">{'{{customer_first_name}}'}</code> para personalizarlos.
      </p>
      {grouped.length === 0 ? <EmptyState title="Sin plantillas" /> : null}
      {grouped.map((g) => (
        <Card key={g.label} title={g.label} padded={false}>
          <ul className="divide-y divide-slate-100">
            {g.items.map((t) => (
              <li key={t.id}>
                <button type="button" className="flex w-full items-start justify-between gap-3 px-4 py-3 text-left hover:bg-slate-50" onClick={() => setEditing(t)}>
                  <span className="min-w-0">
                    <span className="font-medium text-slate-800">{t.title}</span>
                    {!t.isActive ? (
                      <Badge tone="slate" className="ml-2">
                        inactiva
                      </Badge>
                    ) : null}
                    <span className="block truncate text-xs text-slate-500">{t.body.replace(/\s+/g, ' ')}</span>
                  </span>
                  <span className="shrink-0 text-xs text-slate-400">{t.key}</span>
                </button>
              </li>
            ))}
          </ul>
        </Card>
      ))}
      {editing ? (
        <TemplateEditor
          template={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null)
            void queryClient.invalidateQueries({ queryKey: ['templates'] })
          }}
        />
      ) : null}
    </div>
  )
}

function TemplateEditor({ template, onClose, onSaved }: { template: MessageTemplate | null; onClose: () => void; onSaved: () => void }) {
  const tokens = useQuery({ queryKey: ['templates', 'tokens'], queryFn: templatesApi.tokens, staleTime: Infinity })
  const [key, setKey] = useState(template?.key ?? 'custom.')
  const [title, setTitle] = useState(template?.title ?? '')
  const [body, setBody] = useState(template?.body ?? '')
  const [active, setActive] = useState(template?.isActive ?? true)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const area = useRef<HTMLTextAreaElement | null>(null)
  const isBuiltIn = !!template && !template.key.startsWith('custom.')

  const save = useMutation({
    mutationFn: () => (template ? templatesApi.update(template.id, { title: title.trim(), body, isActive: active }) : templatesApi.create({ key: key.trim(), title: title.trim(), body, isActive: active })),
    onSuccess: () => {
      toast.success('Plantilla guardada')
      onSaved()
    },
    onError: (err) => toast.error('No se pudo guardar', { description: errorMessage(err) }),
  })
  const restore = useMutation({
    mutationFn: () => templatesApi.restoreDefault(template!.id),
    onSuccess: (t) => {
      setTitle(t.title)
      setBody(t.body)
      setActive(t.isActive)
      toast.success('Se restauró el texto original')
    },
    onError: (err) => toast.error('No se pudo restaurar', { description: errorMessage(err) }),
  })
  const remove = useMutation({
    mutationFn: () => templatesApi.remove(template!.id),
    onSuccess: () => {
      toast.success('Plantilla eliminada')
      onSaved()
    },
    onError: (err) => toast.error('No se pudo eliminar', { description: errorMessage(err) }),
  })

  function insert(token: string) {
    const el = area.current
    const text = `{{${token}}}`
    if (!el) {
      setBody((b) => b + text)
      return
    }
    const start = el.selectionStart ?? body.length
    const end = el.selectionEnd ?? body.length
    const next = body.slice(0, start) + text + body.slice(end)
    setBody(next)
    requestAnimationFrame(() => {
      el.focus()
      el.setSelectionRange(start + text.length, start + text.length)
    })
  }

  return (
    <Modal
      open
      size="xl"
      onClose={onClose}
      title={template ? template.title : 'Nueva plantilla'}
      description={template ? `${template.key} · editada ${relative(template.updatedAtUtc)}` : 'Las plantillas personalizadas se pueden usar al enviar mensajes desde una orden.'}
      footer={
        <>
          {template && isBuiltIn ? (
            <Button variant="ghost" className="mr-auto" loading={restore.isPending} onClick={() => restore.mutate()}>
              Restaurar original
            </Button>
          ) : null}
          {template && !isBuiltIn ? (
            <Button variant="ghost" className="mr-auto text-rose-700" onClick={() => setConfirmDelete(true)}>
              Eliminar
            </Button>
          ) : null}
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} disabled={title.trim().length < 2 || body.trim().length < 2 || (!template && key.trim().length < 3)} onClick={() => save.mutate()}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="grid gap-5 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <div className="space-y-3">
          {!template ? (
            <Field label="Clave" hint="Identificador único, ej. custom.promo_bateria">
              <Input value={key} onChange={(e) => setKey(e.target.value.toLowerCase().replace(/[^a-z0-9._]/g, ''))} />
            </Field>
          ) : null}
          <Field label="Título">
            <Input value={title} onChange={(e) => setTitle(e.target.value)} />
          </Field>
          <Field label="Texto">
            <Textarea ref={area} rows={12} value={body} onChange={(e) => setBody(e.target.value)} className="font-mono text-xs" />
          </Field>
          <Checkbox checked={active} onChange={(e) => setActive(e.target.checked)} label="Activa" description="Las inactivas no se envían automáticamente." />
        </div>
        <div className="space-y-4">
          <div>
            <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Vista previa (datos de ejemplo)</p>
            <div className="whitespace-pre-wrap rounded-2xl rounded-tl-sm bg-emerald-50 p-3 text-sm text-slate-800 shadow-inner">{render(body) || <span className="text-slate-400">Escribí el mensaje…</span>}</div>
          </div>
          <div>
            <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Variables (clic para insertar)</p>
            <ul className="flex max-h-64 flex-wrap gap-1.5 overflow-y-auto">
              {(tokens.data ?? []).map((t) => (
                <li key={t.token}>
                  <button type="button" title={t.description} onClick={() => insert(t.token)} className="rounded-md border border-slate-200 bg-white px-2 py-0.5 font-mono text-xs text-slate-700 hover:border-brand-400 hover:bg-brand-50">
                    {t.token}
                  </button>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
      <ConfirmDialog open={confirmDelete} title="¿Eliminar la plantilla?" danger confirmLabel="Eliminar" loading={remove.isPending} onConfirm={() => remove.mutate()} onClose={() => setConfirmDelete(false)} />
    </Modal>
  )
}
