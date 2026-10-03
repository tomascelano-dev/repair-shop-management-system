import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { billingApi } from '../../api/endpoints'
import { useSession } from '../../auth/session'
import { SUBSCRIPTION_KEY, VerifyEmailAlert } from '../../features/billing/shared'
import { Alert } from '../ui'

/** Trial countdown, read-only mode and unconfirmed email, shown on every screen. */
export function SubscriptionBanner() {
  const { can } = useSession()
  const sub = useQuery({ queryKey: SUBSCRIPTION_KEY, queryFn: billingApi.subscription, staleTime: 60_000, retry: false })
  const s = sub.data
  if (!s) return null

  const isAdmin = can('admin')
  const plansLink = isAdmin ? (
    <Link to="/billing" className="font-medium underline">
      Elegir un plan
    </Link>
  ) : (
    <span>Pedile a un administrador que elija un plan.</span>
  )

  return (
    <div className="no-print mb-4 space-y-2">
      {s.status === 'Expired' ? (
        <Alert tone="rose">La prueba gratis o la suscripción terminó y la cuenta está en modo consulta. {plansLink}</Alert>
      ) : s.status === 'PastDue' ? (
        <Alert tone="amber">No pudimos cobrar el último abono. Revisá el medio de pago para no perder el acceso. {isAdmin ? plansLink : null}</Alert>
      ) : s.status === 'Trialing' ? (
        <Alert tone="blue">
          Prueba gratis: {s.trialDaysLeft === 1 ? 'te queda 1 día' : `te quedan ${s.trialDaysLeft} días`} con todos los módulos. {isAdmin ? plansLink : null}
        </Alert>
      ) : null}
      {isAdmin && !s.emailVerified ? <VerifyEmailAlert /> : null}
    </div>
  )
}
