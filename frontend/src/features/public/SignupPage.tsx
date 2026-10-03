import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { authApi, billingApi } from '../../api/endpoints'
import { errorMessage, fieldErrors } from '../../api/http'
import { useSession } from '../../auth/session'
import { Alert, Button, Checkbox, Field, Input, Loading, Select } from '../../components/ui'
import { billingCountry, browserTimeZone, COUNTRIES, detectCountry } from '../../lib/billing'
import { AuthCard } from '../auth/AuthPages'

export function SignupPage() {
  const { state, acceptSession } = useSession()
  const navigate = useNavigate()
  const config = useQuery({ queryKey: ['billing-config'], queryFn: billingApi.config, staleTime: 5 * 60_000 })
  const [form, setForm] = useState({ shopName: '', ownerName: '', email: '', password: '', country: detectCountry(), acceptTerms: false })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  if (state.status === 'authenticated') return <Navigate to="/" replace />

  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((f) => ({ ...f, [key]: value }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setErrors({})
    setLoading(true)
    try {
      const session = await authApi.signup({
        shopName: form.shopName.trim(),
        ownerName: form.ownerName.trim(),
        email: form.email.trim(),
        password: form.password,
        country: billingCountry(form.country),
        timeZone: browserTimeZone(),
        acceptTerms: form.acceptTerms,
      })
      acceptSession(session)
      navigate('/?bienvenida=1', { replace: true })
    } catch (err) {
      setErrors(fieldErrors(err))
      setError(errorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  if (config.isLoading) return <Loading />
  const trialDays = config.data?.trialDays ?? 14

  if (config.data && !config.data.signupEnabled) {
    return (
      <AuthCard title="Crear cuenta">
        <Alert tone="amber">Por ahora el alta de talleres está cerrada. Volvé a intentar en unos días.</Alert>
        <p className="mt-4 text-center text-sm">
          <Link to="/login" className="text-brand-700 hover:underline">
            Ya tengo cuenta
          </Link>
        </p>
      </AuthCard>
    )
  }

  return (
    <AuthCard title={`Probá gratis ${trialDays} días`} subtitle="Sin tarjeta. Todos los módulos incluidos.">
      <form onSubmit={submit} className="space-y-4" noValidate>
        <Field label="Nombre del taller" error={errors.shopName}>
          <Input value={form.shopName} onChange={(e) => set('shopName', e.target.value)} autoComplete="organization" required autoFocus />
        </Field>
        <Field label="Tu nombre" error={errors.ownerName}>
          <Input value={form.ownerName} onChange={(e) => set('ownerName', e.target.value)} autoComplete="name" required />
        </Field>
        <Field label="Email" error={errors.email}>
          <Input type="email" value={form.email} onChange={(e) => set('email', e.target.value)} autoComplete="email" required />
        </Field>
        <Field label="Contraseña" hint="Mínimo 8 caracteres, con letras y números." error={errors.password}>
          <Input type="password" value={form.password} onChange={(e) => set('password', e.target.value)} autoComplete="new-password" required />
        </Field>
        <Field label="País" error={errors.country}>
          <Select value={form.country} onChange={(e) => set('country', e.target.value)}>
            {COUNTRIES.map((c) => (
              <option key={c.code} value={c.code}>
                {c.name}
              </option>
            ))}
          </Select>
        </Field>
        <Checkbox
          checked={form.acceptTerms}
          onChange={(e) => set('acceptTerms', e.target.checked)}
          label={
            <>
              Acepto los{' '}
              <Link to="/terminos" target="_blank" className="text-brand-700 underline">
                Términos
              </Link>{' '}
              y la{' '}
              <Link to="/privacidad" target="_blank" className="text-brand-700 underline">
                Política de privacidad
              </Link>
            </>
          }
        />
        {error ? <Alert tone="rose">{error}</Alert> : null}
        <Button
          type="submit"
          variant="primary"
          className="w-full"
          loading={loading}
          disabled={!form.shopName || !form.ownerName || !form.email || !form.password || !form.acceptTerms}
        >
          Crear mi cuenta
        </Button>
        <p className="text-center text-sm text-slate-600">
          ¿Ya tenés cuenta?{' '}
          <Link to="/login" className="text-brand-700 hover:underline">
            Ingresar
          </Link>
          {' · '}
          <Link to="/precios" className="text-brand-700 hover:underline">
            Ver precios
          </Link>
        </p>
      </form>
    </AuthCard>
  )
}

export function VerifyEmailPage() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const result = useQuery({ queryKey: ['verify-email', token], queryFn: () => authApi.verifyEmail(token).then(() => true), enabled: !!token, retry: false })
  const { state } = useSession()

  return (
    <AuthCard title="Confirmar email">
      {!token || result.isError ? (
        <Alert tone="rose">{result.error ? errorMessage(result.error) : 'El link es inválido.'} Podés pedir uno nuevo desde el aviso dentro de la app.</Alert>
      ) : result.isLoading ? (
        <Loading />
      ) : (
        <Alert tone="green">¡Listo! Tu email quedó confirmado.</Alert>
      )}
      <p className="mt-4 text-center text-sm">
        <Link to={state.status === 'authenticated' ? '/' : '/login'} className="text-brand-700 hover:underline">
          {state.status === 'authenticated' ? 'Ir al panel' : 'Ingresar'}
        </Link>
      </p>
    </AuthCard>
  )
}
