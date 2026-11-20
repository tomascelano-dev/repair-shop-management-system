import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { billingApi } from '../../api/endpoints'
import { errorMessage, fieldErrors } from '../../api/http'
import type { WithdrawalResult } from '../../api/types'
import { Alert, Button, Field, Input, Textarea } from '../../components/ui'
import { publicPath, usePublicLocale } from '../../lib/publicLocale'
import { PublicLayout } from './PublicLayout'

/**
 * "Botón de arrepentimiento" (Argentina, Res. 424/2020): withdraw from the contract without signing in. The
 * identification code is shown right away and emailed to account holders.
 */
export function WithdrawalPage() {
  const locale = usePublicLocale()
  const en = locale === 'en'
  const [form, setForm] = useState({ email: '', name: '', reason: '' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState<WithdrawalResult | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setErrors({})
    setLoading(true)
    try {
      setResult(await billingApi.withdrawal({ email: form.email.trim(), name: form.name.trim(), reason: form.reason.trim() || null }))
    } catch (err) {
      setErrors(fieldErrors(err))
      setError(errorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <PublicLayout title={en ? 'Withdraw from your contract' : 'Botón de arrepentimiento'}>
      <main className="mx-auto max-w-xl px-4 pb-6">
        <article className="space-y-4 rounded-2xl bg-white p-6 text-sm leading-relaxed text-slate-700 shadow-sm ring-1 ring-slate-200 sm:p-8">
          <h1 className="text-2xl font-bold text-slate-900">{en ? 'Withdraw from your contract' : 'Botón de arrepentimiento'}</h1>
          <p>
            {en
              ? 'You can withdraw from your RepairShop subscription within 10 days of contracting, at no cost and without giving a reason. We cancel the subscription and refund any payment through Mercado Pago.'
              : 'Puedes revocar la contratación de RepairShop dentro de los 10 días corridos desde que contrataste, sin costo y sin dar explicaciones. Cancelamos la suscripción y, si hubo un pago, lo reintegramos por Mercado Pago.'}{' '}
            <Link to={publicPath(locale, 'refunds')} className="text-brand-700 underline">{en ? 'Refund policy' : 'Política de reembolsos'}</Link>.
          </p>

          {result ? (
            <Alert tone="green" title={en ? 'Request received' : 'Recibimos tu solicitud'}>
              <p>
                {en ? 'Your identification code is ' : 'Tu código de identificación es '}
                <strong className="font-mono text-base">{result.code}</strong>.
              </p>
              <p className="mt-1">
                {en
                  ? 'Keep it. If the email belongs to an account, we also sent it there. We will write to you once it is done.'
                  : 'Guárdalo. Si el email es el de una cuenta, también te lo enviamos ahí. Te escribimos cuando esté resuelto.'}
              </p>
            </Alert>
          ) : (
            <form onSubmit={submit} className="space-y-4" noValidate>
              <Field label={en ? 'Account email' : 'Email de tu cuenta'} error={errors.email}>
                <Input type="email" value={form.email} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} autoComplete="email" required />
              </Field>
              <Field label={en ? 'Full name' : 'Nombre y apellido'} error={errors.name}>
                <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} autoComplete="name" required />
              </Field>
              <Field label={en ? 'Reason (optional)' : 'Motivo (opcional)'} error={errors.reason}>
                <Textarea value={form.reason} maxLength={1000} onChange={(e) => setForm((f) => ({ ...f, reason: e.target.value }))} />
              </Field>
              {error ? <Alert tone="rose">{error}</Alert> : null}
              <Button type="submit" variant="primary" className="w-full" loading={loading} disabled={!form.email || !form.name}>
                {en ? 'Withdraw from the contract' : 'Arrepentirme de la contratación'}
              </Button>
            </form>
          )}
        </article>
      </main>
    </PublicLayout>
  )
}
