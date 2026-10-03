import { useRef, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { settingsApi } from '../../api/endpoints'
import { errorMessage, fieldErrors } from '../../api/http'
import type { NotificationChannel, ShopSettings, TaxCondition, UpdateShopSettings } from '../../api/types'
import { Button, Card, Checkbox, ErrorState, Field, Input, Loading, PageHeader, Select, Tabs, Textarea } from '../../components/ui'
import { CHANNEL, TAX_CONDITION } from '../../lib/labels'
import { CURRENCIES } from '../../lib/shop'
import { AuditTab } from './AuditTab'
import { IntegrationsTab } from './IntegrationsTab'
import { TemplatesTab } from './TemplatesTab'
import { BranchesTab, UsersTab } from './UsersTab'

type Tab = 'general' | 'operation' | 'templates' | 'integrations' | 'users' | 'branches' | 'audit'

const TABS: { value: Tab; label: string }[] = [
  { value: 'general', label: 'Negocio' },
  { value: 'operation', label: 'Operación' },
  { value: 'templates', label: 'Mensajes' },
  { value: 'integrations', label: 'Integraciones' },
  { value: 'users', label: 'Usuarios' },
  { value: 'branches', label: 'Sucursales' },
  { value: 'audit', label: 'Auditoría' },
]

const TIME_ZONES = [
  'America/Argentina/Buenos_Aires',
  'America/Argentina/Cordoba',
  'America/Argentina/Mendoza',
  'America/Argentina/Salta',
  'America/Argentina/Ushuaia',
  'America/Montevideo',
  'America/Santiago',
  'America/Asuncion',
  'America/Sao_Paulo',
  'America/Lima',
  'America/Bogota',
  'America/Mexico_City',
  'Europe/Madrid',
  'UTC',
]

export function SettingsPage() {
  const [params, setParams] = useSearchParams()
  const tab = (params.get('tab') as Tab) || 'general'
  return (
    <div>
      <PageHeader title="Configuración" subtitle="Datos del negocio, reglas de trabajo, mensajes, integraciones y equipo." />
      <Tabs<Tab> className="mb-5" value={tab} onChange={(v) => setParams({ tab: v }, { replace: true })} tabs={TABS} />
      {tab === 'general' || tab === 'operation' ? <SettingsForm section={tab} /> : null}
      {tab === 'templates' ? <TemplatesTab /> : null}
      {tab === 'integrations' ? <IntegrationsTab /> : null}
      {tab === 'users' ? <UsersTab /> : null}
      {tab === 'branches' ? <BranchesTab /> : null}
      {tab === 'audit' ? <AuditTab /> : null}
    </div>
  )
}

function toForm(s: ShopSettings): UpdateShopSettings {
  const copy: Partial<ShopSettings> = { ...s }
  delete copy.id
  delete copy.organizationId
  delete copy.logoFileId
  delete copy.logoUrl
  delete copy.isActive
  return copy as UpdateShopSettings
}

function SettingsForm({ section }: { section: 'general' | 'operation' }) {
  const settings = useQuery({ queryKey: ['settings'], queryFn: settingsApi.get })
  if (settings.isLoading) return <Loading />
  if (settings.isError || !settings.data) return <ErrorState error={errorMessage(settings.error)} onRetry={() => void settings.refetch()} />
  return <SettingsEditor key={settings.data.id + section} settings={settings.data} section={section} />
}

function SettingsEditor({ settings, section }: { settings: ShopSettings; section: 'general' | 'operation' }) {
  const queryClient = useQueryClient()
  const [v, setV] = useState<UpdateShopSettings>(() => toForm(settings))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const set = <K extends keyof UpdateShopSettings>(k: K, value: UpdateShopSettings[K]) => setV((x) => ({ ...x, [k]: value }))
  const text = (k: keyof UpdateShopSettings) => (v[k] as string | null | undefined) ?? ''

  const save = useMutation({
    mutationFn: () => {
      const body: UpdateShopSettings = { ...v }
      for (const [k, val] of Object.entries(body)) if (typeof val === 'string' && val.trim() === '' && k !== 'name') (body as Record<string, unknown>)[k] = null
      return settingsApi.update(body)
    },
    onSuccess: (s) => {
      queryClient.setQueryData(['settings'], s)
      setErrors({})
      toast.success('Configuración guardada')
    },
    onError: (err) => {
      setErrors(fieldErrors(err))
      toast.error('No se pudo guardar', { description: errorMessage(err) })
    },
  })

  function submit(e: FormEvent) {
    e.preventDefault()
    save.mutate()
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      {section === 'general' ? (
        <>
          <LogoCard settings={settings} />
          <Card title="Datos del negocio">
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Nombre comercial" required error={errors.name}>
                <Input value={v.name} onChange={(e) => set('name', e.target.value)} />
              </Field>
              <Field label="Razón social" error={errors.legalName}>
                <Input value={text('legalName')} onChange={(e) => set('legalName', e.target.value)} />
              </Field>
              <Field label="CUIT" error={errors.taxId}>
                <Input value={text('taxId')} onChange={(e) => set('taxId', e.target.value)} placeholder="20-12345678-9" />
              </Field>
              <Field label="Condición frente al IVA">
                <Select value={v.taxCondition} onChange={(e) => set('taxCondition', e.target.value as TaxCondition)}>
                  {(Object.keys(TAX_CONDITION) as TaxCondition[]).map((t) => (
                    <option key={t} value={t}>
                      {TAX_CONDITION[t]}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Teléfono / WhatsApp" error={errors.phone}>
                <Input value={text('phone')} onChange={(e) => set('phone', e.target.value)} />
              </Field>
              <Field label="Email" error={errors.email}>
                <Input type="email" value={text('email')} onChange={(e) => set('email', e.target.value)} />
              </Field>
              <Field label="Dirección">
                <Input value={text('addressLine')} onChange={(e) => set('addressLine', e.target.value)} />
              </Field>
              <Field label="Ciudad">
                <Input value={text('city')} onChange={(e) => set('city', e.target.value)} />
              </Field>
              <Field label="País">
                <Input value={text('country')} onChange={(e) => set('country', e.target.value)} />
              </Field>
              <Field label="Horario de retiro" hint="Se muestra en el portal del cliente y en los avisos.">
                <Input value={text('pickupHours')} onChange={(e) => set('pickupHours', e.target.value)} placeholder="Lun a vie 10 a 19 h · Sáb 10 a 13 h" />
              </Field>
            </div>
          </Card>
          <Card title="Regional">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="Moneda principal">
                <Select value={v.defaultCurrency} onChange={(e) => set('defaultCurrency', e.target.value)}>
                  {CURRENCIES.map((c) => (
                    <option key={c}>{c}</option>
                  ))}
                </Select>
              </Field>
              <Field label="Moneda de reportes" hint="Todo se convierte a esta moneda.">
                <Select value={v.reportingCurrency} onChange={(e) => set('reportingCurrency', e.target.value)}>
                  {CURRENCIES.map((c) => (
                    <option key={c}>{c}</option>
                  ))}
                </Select>
              </Field>
              <Field label="Código de país (teléfonos)" error={errors.phoneCountryCode} hint="54 para Argentina.">
                <Input value={v.phoneCountryCode} onChange={(e) => set('phoneCountryCode', e.target.value.replace(/\D/g, ''))} />
              </Field>
              <Field label="Zona horaria">
                <Select value={v.timeZone} onChange={(e) => set('timeZone', e.target.value)}>
                  {(TIME_ZONES.includes(v.timeZone) ? TIME_ZONES : [v.timeZone, ...TIME_ZONES]).map((z) => (
                    <option key={z}>{z}</option>
                  ))}
                </Select>
              </Field>
            </div>
          </Card>
        </>
      ) : (
        <>
          <Card title="Órdenes y garantías">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              <Field label="Garantía por defecto (días)" error={errors.defaultWarrantyDays}>
                <Input type="number" min={0} value={v.defaultWarrantyDays} onChange={(e) => set('defaultWarrantyDays', Number(e.target.value) || 0)} />
              </Field>
              <Field label="Validez de presupuestos (días)" error={errors.quoteValidityDays}>
                <Input type="number" min={1} value={v.quoteValidityDays} onChange={(e) => set('quoteValidityDays', Number(e.target.value) || 1)} />
              </Field>
              <Field label="Orden estancada después de (días)" hint="Se marca en el tablero si no cambia de estado." error={errors.staleOrderDays}>
                <Input type="number" min={1} value={v.staleOrderDays} onChange={(e) => set('staleOrderDays', Number(e.target.value) || 1)} />
              </Field>
              <Field label="Términos de recepción" className="sm:col-span-2 lg:col-span-3" hint="Se imprimen en el comprobante de ingreso que firma el cliente.">
                <Textarea rows={4} value={text('receptionTerms')} onChange={(e) => set('receptionTerms', e.target.value)} />
              </Field>
              <Field label="Términos de garantía" className="sm:col-span-2 lg:col-span-3" hint="Se imprimen en el certificado de garantía.">
                <Textarea rows={4} value={text('warrantyTerms')} onChange={(e) => set('warrantyTerms', e.target.value)} />
              </Field>
            </div>
          </Card>
          <Card title="Avisos al cliente">
            <div className="grid gap-4 sm:grid-cols-2">
              <Checkbox checked={v.notificationsEnabled} onChange={(e) => set('notificationsEnabled', e.target.checked)} label="Enviar avisos automáticos" description="Cambios de estado, presupuestos y recordatorios." />
              <Field label="Canal preferido">
                <Select value={v.defaultNotificationChannel} onChange={(e) => set('defaultNotificationChannel', e.target.value as NotificationChannel)}>
                  {(Object.keys(CHANNEL) as NotificationChannel[]).map((c) => (
                    <option key={c} value={c}>
                      {CHANNEL[c]}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Recordar retiro a los (días)" hint="Separados por coma, ej. 3,7,15. Vacío para no recordar." error={errors.readyReminderDays}>
                <Input value={v.readyReminderDays} onChange={(e) => set('readyReminderDays', e.target.value.replace(/[^\d,]/g, ''))} />
              </Field>
              <div className="space-y-3">
                <Checkbox checked={v.sendFeedbackSurvey} onChange={(e) => set('sendFeedbackSurvey', e.target.checked)} label="Encuesta de satisfacción al entregar" />
                <Field label="Link de reseñas de Google" hint="A los clientes con 4 o 5 estrellas se les sugiere dejar una reseña." error={errors.googleReviewUrl}>
                  <Input type="url" value={text('googleReviewUrl')} onChange={(e) => set('googleReviewUrl', e.target.value)} placeholder="https://g.page/r/…" />
                </Field>
              </div>
            </div>
          </Card>
          <Card title="Caja">
            <Checkbox
              checked={v.requireOpenCashSession}
              onChange={(e) => set('requireOpenCashSession', e.target.checked)}
              label="Exigir caja abierta para cobrar"
              description="Recomendado: todos los cobros quedan dentro de un turno y se pueden arquear."
            />
          </Card>
        </>
      )}
      <div className="sticky bottom-0 flex justify-end border-t border-slate-200 bg-slate-50/90 py-3 backdrop-blur">
        <Button type="submit" variant="primary" loading={save.isPending}>
          Guardar cambios
        </Button>
      </div>
    </form>
  )
}

function LogoCard({ settings }: { settings: ShopSettings }) {
  const queryClient = useQueryClient()
  const input = useRef<HTMLInputElement | null>(null)
  const upload = useMutation({
    mutationFn: (file: File) => settingsApi.uploadLogo(file),
    onSuccess: (s) => {
      queryClient.setQueryData(['settings'], s)
      toast.success('Logo actualizado')
    },
    onError: (err) => toast.error('No se pudo subir el logo', { description: errorMessage(err) }),
  })
  const remove = useMutation({
    mutationFn: settingsApi.removeLogo,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['settings'] })
      toast.success('Logo quitado')
    },
    onError: (err) => toast.error('No se pudo quitar el logo', { description: errorMessage(err) }),
  })
  return (
    <Card title="Logo" actions={settings.logoUrl ? <Button size="sm" variant="ghost" loading={remove.isPending} onClick={() => remove.mutate()}>Quitar</Button> : null}>
      <div className="flex items-center gap-4">
        <div className="flex h-20 w-20 items-center justify-center overflow-hidden rounded-xl border border-dashed border-slate-300 bg-slate-50">
          {settings.logoUrl ? <img src={settings.logoUrl} alt="Logo del negocio" className="max-h-full max-w-full object-contain" /> : <span className="text-xs text-slate-400">Sin logo</span>}
        </div>
        <div className="text-sm text-slate-600">
          <p>Aparece en comprobantes, presupuestos, tickets y en el portal del cliente.</p>
          <input
            ref={input}
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="hidden"
            onChange={(e) => {
              const f = e.target.files?.[0]
              if (f) upload.mutate(f)
              e.target.value = ''
            }}
          />
          <Button size="sm" className="mt-2" loading={upload.isPending} onClick={() => input.current?.click()}>
            {settings.logoUrl ? 'Cambiar logo' : 'Subir logo'}
          </Button>
        </div>
      </div>
    </Card>
  )
}
