import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { authApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import { useSession } from '../../auth/session'
import { Alert, Button, Field, Input, Loading } from '../../components/ui'
import { LegalLinks } from '../public/PublicLayout'

export function AuthCard({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="flex min-h-full flex-col items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-brand-900 px-4 py-10">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-2xl">
        <div className="mb-5 flex items-center gap-2">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-600 text-sm font-bold text-white">RS</span>
          <div>
            <h1 className="text-lg font-semibold text-slate-900">{title}</h1>
            {subtitle ? <p className="text-sm text-slate-500">{subtitle}</p> : null}
          </div>
        </div>
        {children}
      </div>
      <LegalLinks className="mt-4 text-center text-xs text-slate-400" />
    </div>
  )
}

const DEMO_USERS = [
  { email: 'admin@local', password: 'Admin123456', role: 'Administrador' },
  { email: 'tech@local', password: 'Tech123456', role: 'Técnico' },
  { email: 'recepcion@local', password: 'Recepcion123', role: 'Recepción' },
  { email: 'caja@local', password: 'Caja123456', role: 'Caja' },
]

export function LoginPage() {
  const { state, login } = useSession()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  if (state.status === 'authenticated') return <Navigate to={from} replace />

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await login(email.trim(), password)
      navigate(from, { replace: true })
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthCard title="Ingresar" subtitle="Gestión de servicio técnico">
      <form onSubmit={submit} className="space-y-4" noValidate>
        <Field label="Email">
          <Input type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
        </Field>
        <Field label="Contraseña">
          <Input type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </Field>
        {error ? <Alert tone="rose">{error}</Alert> : null}
        <Button type="submit" variant="primary" className="w-full" loading={loading} disabled={!email || !password}>
          Ingresar
        </Button>
        <p className="text-center text-sm">
          <Link to="/olvide" className="text-brand-700 hover:underline">
            Olvidé mi contraseña
          </Link>
        </p>
        <p className="border-t border-slate-100 pt-4 text-center text-sm text-slate-600">
          ¿Todavía no usás RepairShop?{' '}
          <Link to="/registro" className="font-medium text-brand-700 hover:underline">
            Probalo gratis
          </Link>
        </p>
      </form>

      {import.meta.env.DEV || import.meta.env.VITE_SHOW_DEMO_USERS === 'true' ? (
        <div className="mt-6 rounded-lg bg-slate-50 p-3 text-xs text-slate-600">
          <p className="mb-1 font-medium">Usuarios de demo</p>
          <ul className="space-y-1">
            {DEMO_USERS.map((u) => (
              <li key={u.email}>
                <button
                  type="button"
                  className="text-left text-brand-700 hover:underline"
                  onClick={() => {
                    setEmail(u.email)
                    setPassword(u.password)
                  }}
                >
                  {u.role}: {u.email} / {u.password}
                </button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </AuthCard>
  )
}

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [loading, setLoading] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      await authApi.forgotPassword(email.trim())
      setSent(true)
    } catch (err) {
      toast.error(errorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthCard title="Recuperar acceso">
      {sent ? (
        <div className="space-y-4 text-sm text-slate-700">
          <Alert tone="green">Si el email está registrado, te enviamos un link para restablecer la contraseña (vence en 2 horas).</Alert>
          <p>Si no te llega, pedile a un administrador que genere un link desde Configuración → Usuarios.</p>
          <Link to="/login" className="text-brand-700 hover:underline">
            Volver al inicio de sesión
          </Link>
        </div>
      ) : (
        <form onSubmit={submit} className="space-y-4">
          <Field label="Email">
            <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
          </Field>
          <Button type="submit" variant="primary" className="w-full" loading={loading} disabled={!email}>
            Enviar link
          </Button>
          <p className="text-center text-sm">
            <Link to="/login" className="text-brand-700 hover:underline">
              Volver
            </Link>
          </p>
        </form>
      )}
    </AuthCard>
  )
}

function PasswordForm({ submitLabel, onSubmit, withName }: { submitLabel: string; onSubmit: (password: string, displayName?: string) => Promise<void>; withName?: boolean }) {
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (password !== confirm) {
      setError('Las contraseñas no coinciden.')
      return
    }
    setError(null)
    setLoading(true)
    try {
      await onSubmit(password, name.trim() || undefined)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      {withName ? (
        <Field label="Tu nombre (opcional)">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
      ) : null}
      <Field label="Nueva contraseña" hint="Mínimo 8 caracteres, con letras y números.">
        <Input type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
      </Field>
      <Field label="Repetir contraseña">
        <Input type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required />
      </Field>
      {error ? <Alert tone="rose">{error}</Alert> : null}
      <Button type="submit" variant="primary" className="w-full" loading={loading} disabled={!password || !confirm}>
        {submitLabel}
      </Button>
    </form>
  )
}

function useTokenInfo() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const info = useQuery({ queryKey: ['token-info', token], queryFn: () => authApi.tokenInfo(token), enabled: !!token, retry: false })
  return { token, info }
}

export function ResetPasswordPage() {
  const { token, info } = useTokenInfo()
  const navigate = useNavigate()
  return (
    <AuthCard title="Nueva contraseña" subtitle={info.data ? info.data.email : undefined}>
      {!token || info.isError ? (
        <Alert tone="rose">El link es inválido o venció. Pedí uno nuevo desde “Olvidé mi contraseña”.</Alert>
      ) : info.isLoading ? (
        <Loading />
      ) : (
        <PasswordForm
          submitLabel="Guardar contraseña"
          onSubmit={async (password) => {
            await authApi.resetPassword(token, password)
            toast.success('Contraseña actualizada. Ya podés ingresar.')
            navigate('/login')
          }}
        />
      )}
    </AuthCard>
  )
}

export function AcceptInvitationPage() {
  const { token, info } = useTokenInfo()
  const { acceptSession } = useSession()
  const navigate = useNavigate()
  return (
    <AuthCard title="Bienvenido/a" subtitle={info.data ? `${info.data.shopName} · ${info.data.email}` : undefined}>
      {!token || info.isError ? (
        <Alert tone="rose">La invitación es inválida o venció. Pedile al administrador que te envíe una nueva.</Alert>
      ) : info.isLoading ? (
        <Loading />
      ) : (
        <PasswordForm
          withName
          submitLabel="Activar mi cuenta"
          onSubmit={async (password, displayName) => {
            const session = await authApi.acceptInvitation(token, password, displayName)
            acceptSession(session)
            navigate('/')
          }}
        />
      )}
    </AuthCard>
  )
}
