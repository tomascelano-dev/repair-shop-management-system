import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { authApi, billingApi } from '../../api/endpoints'
import { errorMessage, fieldErrors } from '../../api/http'
import { useSession } from '../../auth/session'
import { Alert, Button, Checkbox, Field, Input, Loading, Select } from '../../components/ui'
import { signupAttribution } from '../../lib/attribution'
import { billingCountry, browserTimeZone, countryOptions, detectCountry } from '../../lib/billing'
import { adsAllowed, trackSignup } from '../../lib/tracking'
import { AuthCard } from '../auth/AuthPages'
import { PUBLIC_ROUTES, useLang } from './lang'

const TEXT = {
  es: {
    closedTitle: 'Crear cuenta',
    closed: 'Por ahora el alta de talleres está cerrada. Vuelve a intentarlo en unos días.',
    haveAccount: 'Ya tengo cuenta',
    title: (days: number) => `Prueba gratis ${days} días`,
    subtitle: 'Sin tarjeta. Todos los módulos incluidos.',
    shopName: 'Nombre del taller',
    ownerName: 'Tu nombre',
    email: 'Email',
    password: 'Contraseña',
    passwordHint: 'Mínimo 8 caracteres, con letras y números.',
    country: 'País',
    accept: 'Acepto los',
    terms: 'Términos',
    and: 'y la',
    privacy: 'Política de privacidad',
    submit: 'Crear mi cuenta',
    already: '¿Ya tienes cuenta?',
    login: 'Ingresar',
    prices: 'Ver precios',
    appNote: '',
  },
  en: {
    closedTitle: 'Create account',
    closed: 'Signups are closed for now. Please try again in a few days.',
    haveAccount: 'I already have an account',
    title: (days: number) => `Try it free for ${days} days`,
    subtitle: 'No credit card. Every module included.',
    shopName: 'Shop name',
    ownerName: 'Your name',
    email: 'Email',
    password: 'Password',
    passwordHint: 'At least 8 characters, with letters and numbers.',
    country: 'Country',
    accept: 'I accept the',
    terms: 'Terms of Service',
    and: 'and the',
    privacy: 'Privacy Policy',
    submit: 'Create my account',
    already: 'Already have an account?',
    login: 'Log in',
    prices: 'See pricing',
    appNote: 'The app is in Spanish for now.',
  },
}

export function SignupPage() {
  const { state, acceptSession } = useSession()
  const navigate = useNavigate()
  const lang = useLang()
  const t = TEXT[lang]
  const routes = PUBLIC_ROUTES[lang]
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
      const attribution = signupAttribution(adsAllowed())
      const session = await authApi.signup({
        shopName: form.shopName.trim(),
        ownerName: form.ownerName.trim(),
        email: form.email.trim(),
        password: form.password,
        country: billingCountry(form.country),
        timeZone: browserTimeZone(),
        acceptTerms: form.acceptTerms,
        attribution,
      })
      trackSignup(attribution.eventId)
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
      <AuthCard title={t.closedTitle}>
        <Alert tone="amber">{t.closed}</Alert>
        <p className="mt-4 text-center text-sm">
          <Link to="/login" className="text-brand-700 hover:underline">
            {t.haveAccount}
          </Link>
        </p>
      </AuthCard>
    )
  }

  return (
    <AuthCard title={t.title(trialDays)} subtitle={t.subtitle}>
      <form onSubmit={submit} className="space-y-4" noValidate>
        <Field label={t.shopName} error={errors.shopName}>
          <Input value={form.shopName} onChange={(e) => set('shopName', e.target.value)} autoComplete="organization" required autoFocus />
        </Field>
        <Field label={t.ownerName} error={errors.ownerName}>
          <Input value={form.ownerName} onChange={(e) => set('ownerName', e.target.value)} autoComplete="name" required />
        </Field>
        <Field label={t.email} error={errors.email}>
          <Input type="email" value={form.email} onChange={(e) => set('email', e.target.value)} autoComplete="email" required />
        </Field>
        <Field label={t.password} hint={t.passwordHint} error={errors.password}>
          <Input type="password" value={form.password} onChange={(e) => set('password', e.target.value)} autoComplete="new-password" required />
        </Field>
        <Field label={t.country} error={errors.country}>
          <Select value={form.country} onChange={(e) => set('country', e.target.value)}>
            {countryOptions(lang).map((c) => (
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
              {t.accept}{' '}
              <Link to={routes.terms} target="_blank" className="text-brand-700 underline">
                {t.terms}
              </Link>{' '}
              {t.and}{' '}
              <Link to={routes.privacy} target="_blank" className="text-brand-700 underline">
                {t.privacy}
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
          {t.submit}
        </Button>
        {t.appNote ? <p className="text-center text-xs text-slate-500">{t.appNote}</p> : null}
        <p className="text-center text-sm text-slate-600">
          {t.already}{' '}
          <Link to="/login" className="text-brand-700 hover:underline">
            {t.login}
          </Link>
          {' · '}
          <Link to={routes.pricing} className="text-brand-700 hover:underline">
            {t.prices}
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
