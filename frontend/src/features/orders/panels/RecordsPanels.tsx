import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ordersApi, templatesApi } from '../../../api/endpoints'
import { errorMessage } from '../../../api/http'
import type { NotificationChannel, QaChecklist, RepairOrder } from '../../../api/types'
import { useSession } from '../../../auth/session'
import { StatusBadge } from '../../../components/domain'
import { Alert, Badge, Button, Card, Checkbox, EmptyState, Field, Input, Loading, Modal, Select, Textarea } from '../../../components/ui'
import { cn } from '../../../lib/cn'
import { dateTime, money, relative } from '../../../lib/format'
import { CHANNEL, CLOUD_LOCK, OUTBOX_STATUS, QUOTE_ITEM_KIND } from '../../../lib/labels'

// ===== Notes =====
export function NotesPanel({ order }: { order: RepairOrder }) {
  const { can } = useSession()
  const queryClient = useQueryClient()
  const notes = useQuery({ queryKey: ['order', order.id, 'notes'], queryFn: () => ordersApi.notes(order.id) })
  const [body, setBody] = useState('')
  const [isPublic, setIsPublic] = useState(false)
  const add = useMutation({
    mutationFn: () => ordersApi.addNote(order.id, body.trim(), isPublic),
    onSuccess: () => {
      setBody('')
      setIsPublic(false)
      void queryClient.invalidateQueries({ queryKey: ['order', order.id, 'notes'] })
    },
    onError: (err) => toast.error(errorMessage(err)),
  })

  return (
    <div className="space-y-4">
      {can('orders.work') ? (
        <Card>
          <div className="space-y-2">
            <Textarea value={body} onChange={(e) => setBody(e.target.value)} placeholder="Diagnóstico, avances, lo que hablaste con el cliente…" rows={3} aria-label="Nueva nota" />
            <div className="flex flex-wrap items-center justify-between gap-2">
              <Checkbox label="Visible para el cliente en el link de seguimiento" checked={isPublic} onChange={(e) => setIsPublic(e.target.checked)} />
              <Button variant="primary" loading={add.isPending} disabled={body.trim().length < 2} onClick={() => add.mutate()}>
                Agregar nota
              </Button>
            </div>
          </div>
        </Card>
      ) : null}
      {notes.isLoading ? (
        <Loading />
      ) : (notes.data ?? []).length === 0 ? (
        <EmptyState title="Sin notas todavía" />
      ) : (
        <ol className="space-y-3">
          {notes.data!.map((n) => (
            <li key={n.id} className={cn('rounded-lg border p-3 text-sm', n.isPublic ? 'border-emerald-200 bg-emerald-50/40' : 'border-slate-200 bg-white')}>
              <div className="mb-1 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                <span className="font-medium text-slate-700">{n.createdByName ?? 'Usuario'}</span>
                <span title={dateTime(n.createdAtUtc)}>{relative(n.createdAtUtc)}</span>
                {n.isPublic ? <Badge tone="green">Visible para el cliente</Badge> : <Badge>Interna</Badge>}
              </div>
              <p className="whitespace-pre-wrap text-slate-800">{n.body}</p>
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}

// ===== Photos / files =====
export function PhotosPanel({ order }: { order: RepairOrder }) {
  const { can } = useSession()
  const queryClient = useQueryClient()
  const inputRef = useRef<HTMLInputElement | null>(null)
  const attachments = useQuery({ queryKey: ['order', order.id, 'attachments'], queryFn: () => ordersApi.attachments(order.id) })
  const [uploading, setUploading] = useState(false)
  const [preview, setPreview] = useState<string | null>(null)

  async function onFiles(files: FileList | null) {
    if (!files || files.length === 0) return
    setUploading(true)
    let ok = 0
    for (const file of Array.from(files)) {
      try {
        await ordersApi.uploadAttachment(order.id, file)
        ok++
      } catch (err) {
        toast.error(`No se pudo subir ${file.name}`, { description: errorMessage(err) })
      }
    }
    setUploading(false)
    if (ok) toast.success(ok === 1 ? 'Archivo subido' : `${ok} archivos subidos`)
    void queryClient.invalidateQueries({ queryKey: ['order', order.id] })
    if (inputRef.current) inputRef.current.value = ''
  }

  const remove = useMutation({
    mutationFn: (id: string) => ordersApi.deleteAttachment(order.id, id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['order', order.id, 'attachments'] }),
    onError: (err) => toast.error(errorMessage(err)),
  })

  const items = attachments.data ?? []
  return (
    <div className="space-y-4">
      {can('orders.work') ? (
        <Card>
          <div className="flex flex-wrap items-center gap-3">
            <input ref={inputRef} type="file" accept="image/*,application/pdf" capture="environment" multiple className="hidden" onChange={(e) => void onFiles(e.target.files)} />
            <Button variant="primary" loading={uploading} onClick={() => inputRef.current?.click()}>
              Sacar foto / subir archivo
            </Button>
            <p className="text-xs text-slate-500">Fotos del estado de ingreso, del diagnóstico o de la reparación terminada. JPG, PNG, WEBP, HEIC o PDF.</p>
          </div>
        </Card>
      ) : null}
      {attachments.isLoading ? (
        <Loading />
      ) : items.length === 0 ? (
        <EmptyState title="Sin fotos ni archivos" />
      ) : (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {items.map((a) => (
            <li key={a.id} className="overflow-hidden rounded-lg border border-slate-200 bg-white">
              {a.kind === 'Photo' && a.url ? (
                <button type="button" className="block w-full" onClick={() => setPreview(a.url!)}>
                  <img src={a.url} alt={a.label ?? 'Foto de la orden'} loading="lazy" className="aspect-square w-full object-cover" />
                </button>
              ) : (
                <a href={a.url ?? '#'} target="_blank" rel="noreferrer" className="flex aspect-square items-center justify-center bg-slate-50 text-3xl text-slate-400">
                  {a.kind === 'Document' ? 'PDF' : '🔗'}
                </a>
              )}
              <div className="flex items-center justify-between gap-1 p-2 text-xs">
                <span className="truncate text-slate-600" title={a.label ?? a.fileName ?? ''}>
                  {a.label ?? a.fileName ?? a.url}
                </span>
                {can('orders.work') ? (
                  <button type="button" className="text-rose-600 hover:underline" onClick={() => remove.mutate(a.id)}>
                    Borrar
                  </button>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      )}
      {preview ? (
        <Modal open size="xl" onClose={() => setPreview(null)} title="Foto">
          <img src={preview} alt="Foto ampliada" className="mx-auto max-h-[75vh] rounded-lg" />
        </Modal>
      ) : null}
    </div>
  )
}

// ===== Messages =====
export function MessagesPanel({ order }: { order: RepairOrder }) {
  const { can } = useSession()
  const queryClient = useQueryClient()
  const messages = useQuery({ queryKey: ['order', order.id, 'messages'], queryFn: () => ordersApi.messages(order.id) })
  const templates = useQuery({ queryKey: ['templates'], queryFn: () => templatesApi.list(false), staleTime: 5 * 60_000 })
  const [templateKey, setTemplateKey] = useState('')
  const [body, setBody] = useState('')
  const [channel, setChannel] = useState<NotificationChannel | ''>('')
  const [waUrl, setWaUrl] = useState<string | null>(null)

  const preview = useMutation({
    mutationFn: (key: string) => ordersApi.previewMessage(order.id, key),
    onSuccess: (p) => {
      setBody(p.body)
      setWaUrl(p.whatsAppUrl ?? null)
    },
    onError: (err) => toast.error(errorMessage(err)),
  })

  const send = useMutation({
    mutationFn: () => ordersApi.sendMessage(order.id, { templateKey: templateKey || 'custom', channel: channel || null, customBody: body }),
    onSuccess: (r) => {
      if (r.outboxItemId) toast.success('Mensaje en cola de envío')
      else toast.warning('No se envió automáticamente', { description: reasonLabel(r.skippedReason) })
      setWaUrl(r.whatsAppUrl ?? null)
      void queryClient.invalidateQueries({ queryKey: ['order', order.id, 'messages'] })
    },
    onError: (err) => toast.error(errorMessage(err)),
  })

  const logManual = useMutation({
    mutationFn: () => ordersApi.logManualMessage(order.id, { channel: 'WhatsApp', templateKey: templateKey || null, body }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['order', order.id, 'messages'] }),
  })

  const waLink = waUrl ? waUrl.replace(/text=[^&]*/, `text=${encodeURIComponent(body)}`) : null

  return (
    <div className="space-y-4">
      {can('orders.work') ? (
        <Card title="Enviar mensaje al cliente">
          <div className="space-y-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Plantilla">
                <Select
                  value={templateKey}
                  onChange={(e) => {
                    setTemplateKey(e.target.value)
                    if (e.target.value) preview.mutate(e.target.value)
                  }}
                >
                  <option value="">Elegí una plantilla…</option>
                  {(templates.data ?? []).map((t) => (
                    <option key={t.id} value={t.key}>
                      {t.title}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Canal">
                <Select value={channel} onChange={(e) => setChannel(e.target.value as NotificationChannel | '')}>
                  <option value="">Predeterminado</option>
                  {(Object.keys(CHANNEL) as NotificationChannel[]).map((c) => (
                    <option key={c} value={c}>
                      {CHANNEL[c]}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
            <Textarea rows={6} value={body} onChange={(e) => setBody(e.target.value)} aria-label="Texto del mensaje" placeholder="Elegí una plantilla o escribí el mensaje." />
            <div className="flex flex-wrap justify-end gap-2">
              {waLink ? (
                <a
                  href={waLink}
                  target="_blank"
                  rel="noreferrer"
                  onClick={() => logManual.mutate()}
                  className="inline-flex h-10 items-center rounded-lg border border-emerald-600 px-3.5 text-sm font-medium text-emerald-700 hover:bg-emerald-50"
                >
                  Abrir en WhatsApp
                </a>
              ) : null}
              <Button variant="primary" loading={send.isPending} disabled={body.trim().length < 2} onClick={() => send.mutate()}>
                Enviar automáticamente
              </Button>
            </div>
          </div>
        </Card>
      ) : null}

      <Card title="Historial de mensajes">
        {messages.isLoading ? (
          <Loading />
        ) : (messages.data ?? []).length === 0 ? (
          <EmptyState title="Todavía no se enviaron mensajes" />
        ) : (
          <ol className="space-y-3">
            {messages.data!.map((m) => (
              <li key={m.id} className="rounded-lg border border-slate-200 p-3 text-sm">
                <div className="mb-1 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                  <Badge tone={OUTBOX_STATUS[m.status].tone}>{OUTBOX_STATUS[m.status].label}</Badge>
                  <span>{CHANNEL[m.channel]}</span>
                  <span>{m.recipient}</span>
                  <span>{dateTime(m.sentAtUtc ?? m.createdAtUtc)}</span>
                  {m.provider ? <span>vía {m.provider}</span> : null}
                </div>
                <p className="whitespace-pre-wrap text-slate-700">{m.body}</p>
                {m.lastError && m.status !== 'Sent' ? <p className="mt-1 text-xs text-rose-700">{m.lastError}</p> : null}
              </li>
            ))}
          </ol>
        )}
      </Card>
    </div>
  )
}

function reasonLabel(reason?: string | null) {
  switch (reason) {
    case 'notifications_disabled':
      return 'Los avisos están desactivados en la configuración.'
    case 'customer_opted_out':
      return 'El cliente no acepta mensajes.'
    case 'no_email':
      return 'El cliente no tiene email.'
    case 'no_phone':
      return 'El cliente no tiene teléfono válido.'
    case 'duplicate':
      return 'Ese mensaje ya se había enviado.'
    default:
      return 'Usá el botón de WhatsApp para mandarlo a mano.'
  }
}

// ===== History =====
export function HistoryPanel({ order }: { order: RepairOrder }) {
  const history = useQuery({ queryKey: ['order', order.id, 'history'], queryFn: () => ordersApi.history(order.id) })
  if (history.isLoading) return <Loading />
  return (
    <Card>
      <ol className="relative space-y-4 border-l border-slate-200 pl-5">
        <li>
          <span className="absolute -left-1.5 mt-1 h-3 w-3 rounded-full bg-slate-300" />
          <p className="text-sm">
            <StatusBadge status="Received" /> <span className="text-slate-500">Ingreso · {dateTime(order.createdAtUtc)}</span>
          </p>
        </li>
        {(history.data ?? []).map((h) => (
          <li key={h.id}>
            <span className="absolute -left-1.5 mt-1 h-3 w-3 rounded-full bg-brand-500" />
            <p className="text-sm">
              <StatusBadge status={h.toStatus} />{' '}
              <span className="text-slate-500">
                {dateTime(h.changedAtUtc)} · {h.changedByName ?? 'Sistema'}
              </span>
            </p>
            {h.reason ? <p className="mt-1 text-sm text-slate-600">“{h.reason}”</p> : null}
          </li>
        ))}
      </ol>
    </Card>
  )
}

// ===== Checklists =====
const QA_ITEMS: { key: keyof QaChecklist; label: string }[] = [
  { key: 'powersOn', label: 'Enciende' },
  { key: 'screenOk', label: 'Pantalla' },
  { key: 'touchOk', label: 'Táctil' },
  { key: 'camerasOk', label: 'Cámaras' },
  { key: 'audioOk', label: 'Audio / parlantes' },
  { key: 'microphoneOk', label: 'Micrófono' },
  { key: 'buttonsOk', label: 'Botones' },
  { key: 'chargingOk', label: 'Carga' },
  { key: 'connectivityOk', label: 'WiFi / señal / Bluetooth' },
  { key: 'biometricsOk', label: 'Face ID / huella' },
]

export function ChecklistsPanel({ order }: { order: RepairOrder }) {
  const { can } = useSession()
  const queryClient = useQueryClient()
  const reception = useQuery({ queryKey: ['order', order.id, 'reception'], queryFn: () => ordersApi.reception(order.id) })
  const qa = useQuery({ queryKey: ['order', order.id, 'qa'], queryFn: () => ordersApi.qa(order.id) })
  const [values, setValues] = useState<QaChecklist | null>(null)
  const current = values ?? qa.data ?? {}

  const save = useMutation({
    mutationFn: (approve: boolean) => ordersApi.saveQa(order.id, { ...current, approve }),
    onSuccess: (res) => {
      toast.success(res.passed ? 'Control de calidad aprobado' : 'Control de calidad guardado')
      setValues(null)
      void queryClient.invalidateQueries({ queryKey: ['order', order.id] })
    },
    onError: (err) => toast.error(errorMessage(err)),
  })

  const editable = can('orders.work') && ['InProgress', 'Testing', 'WaitingParts'].includes(order.status)
  const set = (key: keyof QaChecklist, value: unknown) => setValues({ ...current, [key]: value })
  const anyFail = QA_ITEMS.some((i) => current[i.key] === false)

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card title="Recepción (cómo ingresó)">
        {reception.isLoading ? (
          <Loading />
        ) : !reception.data ? (
          <EmptyState title="Sin checklist de recepción" />
        ) : (
          <div className="space-y-2 text-sm">
            <ul className="grid grid-cols-2 gap-1">
              {(
                [
                  ['screenOk', 'Pantalla'],
                  ['camerasOk', 'Cámaras'],
                  ['speakersOk', 'Parlantes'],
                  ['microphoneOk', 'Micrófono'],
                  ['buttonsOk', 'Botones'],
                  ['faceIdOk', 'Face ID'],
                  ['fingerprintOk', 'Huella'],
                ] as const
              ).map(([k, label]) => (
                <li key={k} className={reception.data![k] ? 'text-slate-700' : 'font-medium text-rose-700'}>
                  {reception.data![k] ? '✓' : '✗'} {label}
                </li>
              ))}
            </ul>
            <p>Cuenta: {CLOUD_LOCK[reception.data.cloudLock]}</p>
            {reception.data.batteryPercent !== null && reception.data.batteryPercent !== undefined ? <p>Batería: {reception.data.batteryPercent}%</p> : null}
            {reception.data.cosmeticNotes ? <p className="text-slate-600">Estético: {reception.data.cosmeticNotes}</p> : null}
          </div>
        )}
      </Card>

      <Card title="Control de calidad de salida" actions={qa.data?.passed ? <Badge tone="green">Aprobado {qa.data.checkedAtUtc ? relative(qa.data.checkedAtUtc) : ''}</Badge> : null}>
        {qa.isLoading ? (
          <Loading />
        ) : (
          <div className="space-y-3 text-sm">
            <p className="text-xs text-slate-500">Probá cada función antes de marcar la orden como lista. Es obligatorio para pasar a “Listo para retirar”.</p>
            <ul className="space-y-1">
              {QA_ITEMS.map((i) => (
                <li key={i.key} className="flex items-center justify-between gap-2">
                  <span>{i.label}</span>
                  <div className="flex gap-1" role="group" aria-label={i.label}>
                    {(
                      [
                        [true, 'OK'],
                        [false, 'Falla'],
                        [null, 'N/A'],
                      ] as const
                    ).map(([val, label]) => (
                      <button
                        key={label}
                        type="button"
                        disabled={!editable}
                        aria-pressed={current[i.key] === val || (val === null && current[i.key] === undefined)}
                        onClick={() => set(i.key, val)}
                        className={cn(
                          'rounded-md border px-2 py-0.5 text-xs',
                          (current[i.key] === val || (val === null && current[i.key] === undefined)) &&
                            (val === true ? 'border-emerald-500 bg-emerald-50 text-emerald-700' : val === false ? 'border-rose-500 bg-rose-50 text-rose-700' : 'border-slate-400 bg-slate-100'),
                          'disabled:opacity-60'
                        )}
                      >
                        {label}
                      </button>
                    ))}
                  </div>
                </li>
              ))}
            </ul>
            <div className="grid grid-cols-2 gap-3">
              <Field label="Salud de batería (%)">
                <Input type="number" min={0} max={100} disabled={!editable} value={current.batteryHealthPercent ?? ''} onChange={(e) => set('batteryHealthPercent', e.target.value ? Number(e.target.value) : null)} />
              </Field>
              <Field label="Notas">
                <Input disabled={!editable} value={current.notes ?? ''} onChange={(e) => set('notes', e.target.value)} />
              </Field>
            </div>
            {anyFail ? <Alert tone="rose">Hay funciones con falla: no se puede aprobar.</Alert> : null}
            {editable ? (
              <div className="flex justify-end gap-2">
                <Button loading={save.isPending && save.variables === false} onClick={() => save.mutate(false)}>
                  Guardar
                </Button>
                <Button variant="success" disabled={anyFail} loading={save.isPending && save.variables === true} onClick={() => save.mutate(true)}>
                  Aprobar control
                </Button>
              </div>
            ) : null}
          </div>
        )}
      </Card>
    </div>
  )
}

// ===== Suggestions (similar repairs + optional AI) =====
export function SuggestionsPanel({ order }: { order: RepairOrder }) {
  const [ai, setAi] = useState(false)
  const suggestions = useQuery({ queryKey: ['order', order.id, 'suggestions', ai], queryFn: () => ordersApi.suggestions(order.id, ai), staleTime: 5 * 60_000 })
  if (suggestions.isLoading) return <Loading label={ai ? 'Consultando al asistente…' : 'Buscando reparaciones parecidas…'} />
  const s = suggestions.data
  if (!s) return null
  return (
    <div className="space-y-4">
      {s.aiAvailable && !ai ? (
        <Button variant="outline" onClick={() => setAi(true)}>
          Pedir sugerencia al asistente de IA
        </Button>
      ) : null}
      {s.aiError ? <Alert tone="amber">{s.aiError}</Alert> : null}
      {s.ai ? (
        <Card title={`Asistente de IA · ${s.ai.model}`}>
          <div className="space-y-3 text-sm">
            <div>
              <p className="font-medium text-slate-700">Causas probables</p>
              <ol className="ml-5 list-decimal space-y-1 text-slate-700">
                {s.ai.diagnosisHypotheses.map((h) => (
                  <li key={h}>{h}</li>
                ))}
              </ol>
            </div>
            {s.ai.items.length > 0 ? (
              <div>
                <p className="font-medium text-slate-700">Ítems sugeridos</p>
                <ul className="space-y-0.5 text-slate-700">
                  {s.ai.items.map((i) => (
                    <li key={i.description}>
                      {QUOTE_ITEM_KIND[i.kind] ?? i.kind}: {i.description} {i.medianUnitPrice ? `· ~${money(i.medianUnitPrice, i.currency ?? 'ARS')}` : ''}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
            {s.ai.customerMessage ? <p className="rounded-lg bg-slate-50 p-3 text-slate-700">“{s.ai.customerMessage}”</p> : null}
            <p className="text-xs text-slate-400">Sugerencia orientativa: verificá siempre con el diagnóstico. No se envían datos del cliente al asistente.</p>
          </div>
        </Card>
      ) : null}
      <Card title="Reparaciones parecidas">
        {s.similarOrders.length === 0 ? (
          <EmptyState title="No hay reparaciones parecidas todavía" />
        ) : (
          <ul className="divide-y divide-slate-100 text-sm">
            {s.similarOrders.map((o) => (
              <li key={o.id} className="py-2">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <a href={`/orders/${o.id}`} className="font-medium text-brand-700 hover:underline">
                    {o.code} · {o.device}
                  </a>
                  <span className="text-slate-600">
                    {o.total ? money(o.total, o.currency ?? 'ARS') : '—'} {o.hoursToReady ? `· ${Math.round(o.hoursToReady)} h` : ''}
                  </span>
                </div>
                <p className="text-slate-500">{o.issueDescription}</p>
                {o.items.length > 0 ? <p className="text-xs text-slate-400">{o.items.join(' · ')}</p> : null}
              </li>
            ))}
          </ul>
        )}
      </Card>
      {s.compatibleParts.length > 0 ? (
        <Card title="Repuestos compatibles en stock">
          <ul className="space-y-1 text-sm">
            {s.compatibleParts.map((p) => (
              <li key={p.id} className="flex justify-between">
                <span>{p.name}</span>
                <span className="text-slate-500">{p.availableQuantity} disp.</span>
              </li>
            ))}
          </ul>
        </Card>
      ) : null}
    </div>
  )
}
