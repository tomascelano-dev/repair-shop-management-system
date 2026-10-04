import { lazy, type ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import type { Permission } from '../api/types'
import { Loading } from '../components/ui'
import { useSession } from './session'

const LandingPage = lazy(async () => ({ default: (await import('../features/public/LandingPage')).LandingPage }))

export function RequireAuth({ children }: { children: ReactNode }) {
  const { state } = useSession()
  const location = useLocation()
  if (state.status === 'loading') return <Loading label="Iniciando sesión…" />
  if (state.status === 'anonymous') {
    // The bare domain is the public face of the product (ads, Paddle's review): visitors see the landing page, not a
    // login form, also when they arrive with campaign parameters (?utm_source=..., ?gclid=...).
    if (location.pathname === '/') return <LandingPage />
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }
  return <>{children}</>
}

export function RequirePermission({ permission, children }: { permission: Permission; children: ReactNode }) {
  const { can } = useSession()
  if (!can(permission)) {
    return (
      <div className="mx-auto max-w-md rounded-xl border border-amber-200 bg-amber-50 p-6 text-center text-sm text-amber-900">
        <p className="font-medium">No tenés permiso para ver esta sección.</p>
        <p className="mt-1">Pedile acceso a un administrador.</p>
      </div>
    )
  }
  return <>{children}</>
}
