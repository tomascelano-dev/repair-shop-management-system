import { configureTracking, trackPurchase } from '../../lib/marketing'
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { billingApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { Plan, PlanId, Subscription } from '../../api/types'
import { Alert, Badge, Button, Card, ConfirmDialog, ErrorState, KeyValue, Loading, PageHeader } from '../../components/ui'
import { CORE_FEATURES, PLAN_TAGLINE, planFeatures, planPrice, SUBSCRIPTION_STATUS } from '../../lib/billing'
import { cn } from '../../lib/cn'
import { date, money } from '../../lib/format'
import { openPendingPaddleCheckout } from '../../lib/paddle'
import { SUBSCRIPTION_KEY } from './shared'

export function BillingPage() {
  const qc = useQueryClient()
  const [params, setParams] = useSearchParams()
  const [waitingPayment, setWaitingPayment] = useState(() => params.has('preapproval_id'))
  const [confirmCancel, setConfirmCancel] = useState(false)

  const sub = useQuery({
    queryKey: SUBSCRIPTION_KEY,
    queryFn: billingApi.subscription,
    // After paying, the provider confirms through a webhook a few seconds later.
    refetchInterval: waitingPayment ? 3000 : false,
  })
  const config = useQuery({ queryKey: ['billing-config'], queryFn: billingApi.config, staleTime: 5 * 60_000 })

  // Stop polling once the payment shows up (or after a minute).
  useEffect(() => {
    if (!waitingPayment || !config.data) return
    if (sub.data && (sub.data.status === 'Active' && !sub.data.pendingPlan)) {
      if (sub.data.conversionId && config.data) {
        configureTracking(config.data.tracking)
        trackPurchase(sub.data.conversionId, sub.data.amount ?? 0, sub.data.currency ?? 'USD')
      }
      setWaitingPayment(false)
      toast.success('¡Listo! Tu plan está activo.')
      return
    }
    const t = window.setTimeout(() => setWaitingPayment(false), 60_000)
    return () => window.clearTimeout(t)
  }, [waitingPayment, sub.data, config.data])

  // Paddle sends the browser back here with ?_ptxn=txn_...: Paddle.js opens the checkout for that transaction.
  const ptxn = params.get('_ptxn')
  useEffect(() => {
    if (!ptxn || !config.data) return
    const token = config.data.paddleClientToken
    if (!token) {
      toast.error('El pago con tarjeta no está disponible en este momento.')
      return
    }
    openPendingPaddleCheckout(token, config.data.paddleEnvironment, {
      completed: () => {
        setWaitingPayment(true)
        setParams({}, { replace: true })
      },
      closed: () => setParams({}, { replace: true }),
    }).catch((err: unknown) => toast.error(errorMessage(err)))
  }, [ptxn, config.data, setParams])

  const checkout = useMutation({
    mutationFn: (plan: PlanId) => billingApi.checkout(plan),
    onSuccess: (result) => {
      if (result.url && !result.changed) {
        window.location.assign(result.url)
        return
      }
      toast.success('Plan actualizado.')
      void qc.invalidateQueries({ queryKey: SUBSCRIPTION_KEY })
    },
    onError: (err) => toast.error(errorMessage(err)),
  })

  const cancel = useMutation({
    mutationFn: billingApi.cancel,
    onSuccess: (data) => {
      qc.setQueryData(SUBSCRIPTION_KEY, data)
      setConfirmCancel(false)
      toast.success('Suscripción cancelada.')
    },
    onError: (err) => toast.error(errorMessage(err)),
  })

  if (sub.isLoading) return <Loading />
  if (sub.isError || !sub.data) return <ErrorState error={errorMessage(sub.error)} onRetry={() => void sub.refetch()} />
  const s = sub.data

  return (
    <div className="space-y-6">
      <PageHeader title="Suscripción" subtitle="Tu plan de RepairShop, los pagos y los límites de cada plan." />

      {waitingPayment ? <Alert tone="blue">Estamos confirmando tu pago. Esto tarda unos segundos…</Alert> : null}

      <Card title="Tu plan" actions={<Badge tone={SUBSCRIPTION_STATUS[s.status].tone}>{SUBSCRIPTION_STATUS[s.status].label}</Badge>}>
        <KeyValue items={summary(s)} />
        {s.status === 'Expired' ? (
          <Alert tone="rose" className="mt-4">
            Tu cuenta está en modo consulta: podés ver todo, pero para cargar o modificar datos tenés que elegir un plan.
          </Alert>
        ) : null}
        {s.canManageAtProvider && s.status !== 'Canceled' ? (
          <div className="mt-4">
            <Button variant="ghost" onClick={() => setConfirmCancel(true)}>
              Cancelar suscripción
            </Button>
          </div>
        ) : null}
      </Card>

      {s.provider === 'Manual' ? (
        <Alert tone="green">Tu plan está bonificado. Si necesitás cambiarlo, escribinos.</Alert>
      ) : (
        <>
          {!s.checkoutAvailable ? (
            <Alert tone="amber">El cobro online todavía no está habilitado para tu país. Escribinos y activamos tu plan a mano.</Alert>
          ) : null}
          <div className="grid gap-4 md:grid-cols-3">
            {s.plans.map((p) => (
              <PlanOption
                key={p.id}
                plan={p}
                current={isCurrent(s, p.id)}
                tooSmall={s.branchesUsed > p.maxBranches}
                disabled={!s.checkoutAvailable || checkout.isPending}
                loading={checkout.isPending && checkout.variables === p.id}
                changing={s.canManageAtProvider}
                onChoose={() => checkout.mutate(p.id)}
              />
            ))}
          </div>
          <p className="text-xs text-slate-500">
            Todos los planes incluyen: {CORE_FEATURES.join(' · ')}.{' '}
            {s.billingCountry === 'AR' ? 'Se cobra en pesos con Mercado Pago.' : 'Se cobra en dólares con Paddle, que suma los impuestos de tu país.'}
          </p>
        </>
      )}

      <ConfirmDialog
        open={confirmCancel}
        title="Cancelar la suscripción"
        message={
          s.currentPeriodEndsAtUtc
            ? `Vas a poder usar RepairShop normalmente hasta el ${date(s.currentPeriodEndsAtUtc)}. Después, la cuenta queda en modo consulta.`
            : 'Después del período pagado, la cuenta queda en modo consulta.'
        }
        confirmLabel="Cancelar suscripción"
        danger
        loading={cancel.isPending}
        onConfirm={() => cancel.mutate()}
        onClose={() => setConfirmCancel(false)}
      />
    </div>
  )
}

function isCurrent(s: Subscription, plan: PlanId) {
  return s.plan === plan && (s.status === 'Active' || s.status === 'PastDue')
}

function summary(s: Subscription) {
  return [
    { label: 'Plan', value: s.status === 'Trialing' ? `Prueba gratis con todos los módulos` : s.planName },
    { label: 'Prueba gratis', value: s.trialDaysLeft > 0 ? `Te quedan ${s.trialDaysLeft} días (hasta el ${date(s.trialEndsAtUtc)})` : null, hidden: s.status !== 'Trialing' },
    {
      label: s.status === 'Canceled' ? 'Activa hasta' : 'Próximo cobro',
      value: s.currentPeriodEndsAtUtc ? date(s.currentPeriodEndsAtUtc) : '—',
      hidden: !s.currentPeriodEndsAtUtc || s.provider === 'Manual',
    },
    { label: 'Abono', value: s.amount != null ? `${money(s.amount, s.currency ?? 'ARS')} por mes` : '—', hidden: s.amount == null },
    { label: 'Sucursales', value: `${s.branchesUsed} de ${s.maxBranches}` },
  ]
}

function PlanOption({
  plan,
  current,
  tooSmall,
  disabled,
  loading,
  changing,
  onChoose,
}: {
  plan: Plan
  current: boolean
  tooSmall: boolean
  disabled: boolean
  loading: boolean
  changing: boolean
  onChoose: () => void
}) {
  return (
    <div className={cn('flex flex-col rounded-xl bg-white p-5 shadow-sm ring-1', current ? 'ring-2 ring-brand-600' : 'ring-slate-200')}>
      <div className="flex items-center justify-between">
        <p className="font-semibold text-slate-900">{plan.name}</p>
        {current ? <Badge tone="blue">Tu plan</Badge> : null}
      </div>
      <p className="text-xs text-slate-500">{PLAN_TAGLINE[plan.id]}</p>
      <p className="mt-3">
        <span className="text-2xl font-bold text-slate-900">{planPrice(plan)}</span>
        <span className="text-sm text-slate-500"> /mes</span>
      </p>
      <ul className="mt-3 flex-1 space-y-1.5 text-sm text-slate-700">
        {planFeatures(plan).map((f) => (
          <li key={f} className="flex gap-2">
            <span aria-hidden="true" className="text-brand-600">✓</span>
            {f}
          </li>
        ))}
      </ul>
      {tooSmall ? <p className="mt-3 text-xs text-amber-700">Tenés más sucursales activas de las que permite este plan.</p> : null}
      <Button className="mt-4" variant={current ? 'default' : 'primary'} disabled={current || tooSmall || disabled} loading={loading} onClick={onChoose}>
        {current ? 'Plan actual' : changing ? 'Cambiar a este plan' : 'Elegir este plan'}
      </Button>
    </div>
  )
}
