import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { customersApi } from '../../api/endpoints'
import { errorMessage, fieldErrors } from '../../api/http'
import type { CustomerInput, DeviceInput, DocumentType, TaxCondition } from '../../api/types'
import { Alert, Button, Checkbox, Field, Input, Select, Textarea } from '../../components/ui'
import { useDebounced } from '../../lib/hooks'
import { DOCUMENT_TYPE, TAX_CONDITION } from '../../lib/labels'
import { isValidImei } from './model'

/** Customer form with live duplicate detection by phone (any format). */
export function CustomerForm({ initial, customerId, submitLabel = 'Guardar', onSubmit, onCancel, onUseExisting, compact }: {
  initial: CustomerInput
  customerId?: string
  submitLabel?: string
  onSubmit: (input: CustomerInput) => Promise<void>
  onCancel?: () => void
  onUseExisting?: (id: string) => void
  compact?: boolean
}) {
  const [v, setV] = useState<CustomerInput>(initial)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const phone = useDebounced((v.phone ?? '').replace(/\D/g, ''), 400)
  const dups = useQuery({ queryKey: ['customers', 'duplicates', phone, customerId], queryFn: () => customersApi.duplicates(phone, customerId), enabled: phone.length >= 6 })


  const set = <K extends keyof CustomerInput>(k: K, value: CustomerInput[K]) => setV((prev) => ({ ...prev, [k]: value }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setErrors({})
    setError(null)
    setSaving(true)
    try {
      await onSubmit({
        ...v,
        email: v.email?.trim() || null,
        notes: v.notes?.trim() || null,
        documentNumber: v.documentType === 'None' ? null : v.documentNumber?.trim() || null,
        address: v.address?.trim() || null,
        tags: v.tags?.trim() || null,
      })
    } catch (err) {
      setErrors(fieldErrors(err))
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Nombre y apellido" required error={errors.fullName}>
          <Input value={v.fullName} onChange={(e) => set('fullName', e.target.value)} autoFocus />
        </Field>
        <Field label="Teléfono / WhatsApp" required error={errors.phone} hint="Con código de área, ej: 11 2345-6789">
          <Input type="tel" value={v.phone} onChange={(e) => set('phone', e.target.value)} />
        </Field>
      </div>

      {dups.data && dups.data.length > 0 ? (
        <Alert tone="amber" title="Ya hay clientes con ese teléfono">
          <ul className="mt-1 space-y-1">
            {dups.data.map((d) => (
              <li key={d.id} className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  {d.fullName} · {d.phone} · {d.orders} órdenes
                </span>
                {onUseExisting ? (
                  <Button size="sm" onClick={() => onUseExisting(d.id)}>
                    Usar este cliente
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        </Alert>
      ) : null}

      <Field label="Email" error={errors.email}>
        <Input type="email" value={v.email ?? ''} onChange={(e) => set('email', e.target.value)} />
      </Field>

      {!compact ? (
        <>
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label="Documento">
              <Select value={v.documentType} onChange={(e) => set('documentType', e.target.value as DocumentType)}>
                {(Object.keys(DOCUMENT_TYPE) as DocumentType[]).map((d) => (
                  <option key={d} value={d}>
                    {DOCUMENT_TYPE[d]}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Número" error={errors.documentNumber}>
              <Input value={v.documentNumber ?? ''} disabled={v.documentType === 'None'} onChange={(e) => set('documentNumber', e.target.value)} />
            </Field>
            <Field label="Condición fiscal">
              <Select value={v.taxCondition} onChange={(e) => set('taxCondition', e.target.value as TaxCondition)}>
                {(Object.keys(TAX_CONDITION) as TaxCondition[]).map((t) => (
                  <option key={t} value={t}>
                    {TAX_CONDITION[t]}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <Field label="Dirección">
            <Input value={v.address ?? ''} onChange={(e) => set('address', e.target.value)} />
          </Field>
          <Field label="Etiquetas" hint="Separadas por coma, ej: vip, empresa">
            <Input value={v.tags ?? ''} onChange={(e) => set('tags', e.target.value)} />
          </Field>
          <Field label="Notas internas">
            <Textarea value={v.notes ?? ''} onChange={(e) => set('notes', e.target.value)} />
          </Field>
        </>
      ) : null}

      <div className="space-y-2">
        <Checkbox label="Acepta recibir avisos del estado de su equipo" checked={v.notificationsOptIn ?? true} onChange={(e) => set('notificationsOptIn', e.target.checked)} />
        <Checkbox label="Acepta recibir promociones" checked={v.marketingOptIn ?? false} onChange={(e) => set('marketingOptIn', e.target.checked)} />
      </div>

      {error ? <Alert tone="rose">{error}</Alert> : null}
      <div className="flex justify-end gap-2">
        {onCancel ? <Button onClick={onCancel}>Cancelar</Button> : null}
        <Button type="submit" variant="primary" loading={saving} disabled={!v.fullName.trim() || !v.phone.trim()}>
          {submitLabel}
        </Button>
      </div>
    </form>
  )
}

const BRANDS = ['Apple', 'Samsung', 'Motorola', 'Xiaomi', 'Huawei', 'LG', 'Nokia', 'TCL', 'Alcatel', 'OPPO', 'Realme', 'Lenovo', 'HP', 'Dell', 'Asus', 'Acer', 'Sony', 'Nintendo']

export function DeviceForm({ initial, submitLabel = 'Guardar', onSubmit, onCancel }: { initial: DeviceInput; submitLabel?: string; onSubmit: (input: DeviceInput) => Promise<void>; onCancel?: () => void }) {
  const [v, setV] = useState<DeviceInput>(initial)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const set = <K extends keyof DeviceInput>(k: K, value: DeviceInput[K]) => setV((prev) => ({ ...prev, [k]: value }))
  const imeiDigits = (v.imei ?? '').replace(/\D/g, '')
  const imeiError = imeiDigits.length > 0 && !isValidImei(imeiDigits) ? 'El IMEI no es válido (15 dígitos). Marcá *#06# en el equipo para verlo.' : undefined

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (imeiError) return
    setErrors({})
    setError(null)
    setSaving(true)
    try {
      await onSubmit({ ...v, label: v.label?.trim() || null, serialNumber: v.serialNumber?.trim() || null, imei: imeiDigits || null, notes: v.notes?.trim() || null })
    } catch (err) {
      setErrors(fieldErrors(err))
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Marca" required error={errors.brand}>
          <Input list="device-brands" value={v.brand} onChange={(e) => set('brand', e.target.value)} autoFocus />
        </Field>
        <Field label="Modelo" required error={errors.model}>
          <Input value={v.model} onChange={(e) => set('model', e.target.value)} placeholder="ej: iPhone 13 Pro, Galaxy A54" />
        </Field>
      </div>
      <datalist id="device-brands">
        {BRANDS.map((b) => (
          <option key={b} value={b} />
        ))}
      </datalist>
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Detalle" hint="Color, capacidad…">
          <Input value={v.label ?? ''} onChange={(e) => set('label', e.target.value)} />
        </Field>
        <Field label="IMEI" error={imeiError ?? errors.imei}>
          <Input inputMode="numeric" value={v.imei ?? ''} onChange={(e) => set('imei', e.target.value)} />
        </Field>
        <Field label="N° de serie">
          <Input value={v.serialNumber ?? ''} onChange={(e) => set('serialNumber', e.target.value)} />
        </Field>
      </div>
      <Field label="Notas">
        <Textarea rows={2} value={v.notes ?? ''} onChange={(e) => set('notes', e.target.value)} />
      </Field>
      {error ? <Alert tone="rose">{error}</Alert> : null}
      <div className="flex justify-end gap-2">
        {onCancel ? <Button onClick={onCancel}>Cancelar</Button> : null}
        <Button type="submit" variant="primary" loading={saving} disabled={!v.brand.trim() || !v.model.trim() || !!imeiError}>
          {submitLabel}
        </Button>
      </div>
    </form>
  )
}
