import { useEffect, useRef, useState, type ReactNode, type Ref } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams, useSearchParams } from 'react-router-dom'
import type { AxiosError } from 'axios'
import { portalApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { OrderStatus, PublicTracking } from '../../api/types'
import { Alert, Button, Loading, Modal, Textarea } from '../../components/ui'
import { cn } from '../../lib/cn'
import { date, dateTime, money } from '../../lib/format'
import { ORDER_STATUS } from '../../lib/labels'

const STEPS: { status: OrderStatus[]; label: string }[] = [
  { status: ['Received'], label: 'Recibido' },
  { status: ['Diagnosing'], label: 'Diagnóstico' },
  { status: ['WaitingParts', 'InProgress', 'Testing'], label: 'Reparación' },
  { status: ['Ready'], label: 'Listo' },
  { status: ['Delivered'], label: 'Entregado' },
]

export function TrackingPage() {
  const { token = '' } = useParams()
  const [params] = useSearchParams()
  const q = useQuery({ queryKey: ['portal', token], queryFn: () => portalApi.get(token), retry: (n, err) => (err as AxiosError)?.response?.status !== 404 && n < 2, refetchInterval: 60_000 })

  useEffect(() => {
    document.title = q.data ? `${q.data.order.code} · ${q.data.shop.name}` : 'Seguimiento de reparación'
  }, [q.data])

  if (q.isLoading) {
    return (
      <Shell>
        <Loading label="Buscando tu orden…" />
      </Shell>
    )
  }
  if (q.isError || !q.data) {
    const status = (q.error as AxiosError | null)?.response?.status
    return (
      <Shell>
        <div className="rounded-2xl bg-white p-8 text-center shadow-sm">
          <p className="text-lg font-semibold text-slate-900">{status === 404 ? 'No encontramos esta orden' : 'No pudimos cargar la orden'}</p>
          <p className="mt-2 text-sm text-slate-600">{status === 404 ? 'Revisá que el link esté completo o pedile uno nuevo al taller.' : errorMessage(q.error)}</p>
          {status !== 404 ? (
            <Button className="mt-4" onClick={() => void q.refetch()}>
              Reintentar
            </Button>
          ) : null}
        </div>
      </Shell>
    )
  }

  const data = q.data
  const payStatus = params.get('collection_status') ?? params.get('status')
  return (
    <Shell shop={data.shop}>
      <div className="space-y-4">
        {payStatus === 'approved' ? (
          <Alert tone="green" title="¡Recibimos tu pago!">
            Puede demorar unos minutos en verse reflejado acá.
          </Alert>
        ) : payStatus === 'pending' || payStatus === 'in_process' ? (
          <Alert tone="amber" title="Tu pago está pendiente">
            Te avisamos cuando se acredite.
          </Alert>
        ) : payStatus === 'rejected' || payStatus === 'null' ? (
          <Alert tone="rose" title="El pago no se completó">
            Podés intentarlo de nuevo.
          </Alert>
        ) : null}
        <StatusCard data={data} />
        {data.quote ? <QuoteCard token={token} data={data} /> : null}
        {data.money && data.money.total > 0 ? <MoneyCard token={token} data={data} /> : null}
        {data.notes.length > 0 ? (
          <Section title="Novedades del taller">
            <ul className="space-y-3">
              {data.notes.map((n, i) => (
                <li key={i} className="text-sm">
                  <p className="whitespace-pre-wrap text-slate-800">{n.body}</p>
                  <p className="text-xs text-slate-500">{dateTime(n.atUtc)}</p>
                </li>
              ))}
            </ul>
          </Section>
        ) : null}
        {data.warranty ? (
          <Section title="Garantía">
            <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
              <p className={data.warranty.active ? 'text-emerald-700' : 'text-slate-600'}>
                {data.warranty.days} días{data.warranty.expiresAtUtc ? ` · ${data.warranty.active ? 'vigente hasta' : 'venció el'} ${date(data.warranty.expiresAtUtc)}` : ''}
              </p>
              <a className="text-sm font-medium text-brand-700 underline" href={portalApi.pdfUrl(token, 'warranty')} target="_blank" rel="noreferrer">
                Descargar certificado
              </a>
            </div>
          </Section>
        ) : null}
        {data.canLeaveFeedback || data.feedbackSubmitted ? <FeedbackCard token={token} data={data} highlight={params.get('encuesta') === '1'} /> : null}
        <ContactCard data={data} />
      </div>
    </Shell>
  )
}

function Shell({ shop, children }: { shop?: PublicTracking['shop']; children: ReactNode }) {
  return (
    <div className="min-h-full bg-slate-100">
      <header className="bg-white shadow-sm">
        <div className="mx-auto flex max-w-xl items-center gap-3 px-4 py-4">
          {shop?.logoUrl ? <img src={shop.logoUrl} alt="" className="h-10 w-10 rounded-lg object-contain" /> : <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-brand-600 text-sm font-bold text-white">{shop?.name?.slice(0, 2).toUpperCase() ?? 'RS'}</span>}
          <div className="min-w-0">
            <p className="truncate font-semibold text-slate-900">{shop?.name ?? 'Seguimiento de reparación'}</p>
            {shop?.city ? <p className="truncate text-xs text-slate-500">{shop.city}</p> : null}
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-xl px-4 py-5">{children}</main>
    </div>
  )
}

function Section({ title, children, className, innerRef }: { title: string; children: ReactNode; className?: string; innerRef?: Ref<HTMLElement> }) {
  return (
    <section ref={innerRef} className={cn('rounded-2xl bg-white p-4 shadow-sm', className)}>
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">{title}</h2>
      {children}
    </section>
  )
}

function StatusCard({ data }: { data: PublicTracking }) {
  const o = data.order
  const cancelled = o.status === 'Cancelled'
  const current = STEPS.findIndex((s) => s.status.includes(o.status))
  const tone = ORDER_STATUS[o.status]?.tone ?? 'slate'
  return (
    <section className="rounded-2xl bg-white p-5 shadow-sm">
      <p className="text-sm text-slate-500">
        Hola {o.customerFirstName} · Orden <strong className="text-slate-700">{o.code}</strong>
      </p>
      <p className="mt-1 text-lg font-semibold text-slate-900">{o.device}</p>
      <p
        className={cn(
          'mt-3 inline-flex rounded-full px-3 py-1 text-sm font-semibold',
          tone === 'emerald' || tone === 'green' ? 'bg-emerald-100 text-emerald-800' : tone === 'rose' ? 'bg-rose-100 text-rose-800' : tone === 'amber' ? 'bg-amber-100 text-amber-800' : 'bg-brand-100 text-brand-800'
        )}
      >
        {o.statusLabel}
      </p>
      {o.isWarrantyClaim ? <p className="mt-2 text-xs text-slate-500">Ingreso por garantía.</p> : null}

      {!cancelled ? (
        <ol className="mt-5 grid grid-cols-5 gap-1" aria-label="Progreso de la reparación">
          {STEPS.map((s, i) => (
            <li key={s.label} className="text-center">
              <span className={cn('mx-auto block h-1.5 rounded-full', i <= current ? 'bg-brand-600' : 'bg-slate-200')} />
              <span className={cn('mt-1.5 block text-[11px]', i === current ? 'font-semibold text-brand-700' : i < current ? 'text-slate-600' : 'text-slate-400')} aria-current={i === current ? 'step' : undefined}>
                {s.label}
              </span>
            </li>
          ))}
        </ol>
      ) : null}

      <dl className="mt-5 grid grid-cols-2 gap-3 text-sm">
        <div>
          <dt className="text-xs text-slate-500">Ingresó</dt>
          <dd className="text-slate-800">{date(o.createdAtUtc)}</dd>
        </div>
        {o.status === 'Delivered' && o.deliveredAtUtc ? (
          <div>
            <dt className="text-xs text-slate-500">Entregado</dt>
            <dd className="text-slate-800">{date(o.deliveredAtUtc)}</dd>
          </div>
        ) : o.readyAtUtc && o.status === 'Ready' ? (
          <div>
            <dt className="text-xs text-slate-500">Listo desde</dt>
            <dd className="text-slate-800">{dateTime(o.readyAtUtc)}</dd>
          </div>
        ) : o.promisedAtUtc && !cancelled ? (
          <div>
            <dt className="text-xs text-slate-500">Fecha estimada</dt>
            <dd className="text-slate-800">{date(o.promisedAtUtc)}</dd>
          </div>
        ) : null}
        <div className="col-span-2">
          <dt className="text-xs text-slate-500">Motivo de ingreso</dt>
          <dd className="text-slate-800">{o.issueDescription}</dd>
        </div>
      </dl>
      {o.status === 'Ready' ? (
        <Alert tone="green" className="mt-4" title="¡Tu equipo está listo!">
          {data.shop.pickupHours ? `Podés retirarlo ${data.shop.pickupHours}.` : 'Ya podés pasar a retirarlo.'}
          {data.shop.address ? ` Dirección: ${data.shop.address}.` : ''}
        </Alert>
      ) : null}

      {data.timeline.length > 1 ? (
        <details className="mt-4 text-sm">
          <summary className="cursor-pointer text-brand-700">Ver historial</summary>
          <ol className="mt-2 space-y-1.5 border-l-2 border-slate-200 pl-3">
            {data.timeline.map((t, i) => (
              <li key={i}>
                <span className="text-slate-800">{t.label}</span> <span className="text-xs text-slate-500">{dateTime(t.atUtc)}</span>
              </li>
            ))}
          </ol>
        </details>
      ) : null}
    </section>
  )
}

function QuoteCard({ token, data }: { token: string; data: PublicTracking }) {
  const queryClient = useQueryClient()
  const quote = data.quote!
  const [deciding, setDeciding] = useState<'approve' | 'reject' | null>(null)
  const [note, setNote] = useState('')
  const decide = useMutation({
    mutationFn: () => (deciding === 'approve' ? portalApi.approve(token, quote.id, note.trim() || undefined) : portalApi.reject(token, quote.id, note.trim() || undefined)),
    onSuccess: (fresh) => {
      queryClient.setQueryData(['portal', token], fresh)
      setDeciding(null)
      setNote('')
    },
  })
  return (
    <Section title={`Presupuesto${quote.version > 1 ? ` (versión ${quote.version})` : ''}`}>
      <ul className="divide-y divide-slate-100 text-sm">
        {quote.items.map((i, idx) => (
          <li key={idx} className="flex justify-between gap-3 py-2">
            <span className="text-slate-800">
              {i.description}
              {i.quantity !== 1 ? <span className="text-slate-500"> × {i.quantity}</span> : null}
            </span>
            <span className="shrink-0 tabular-nums">{money(i.lineTotal, quote.currency)}</span>
          </li>
        ))}
      </ul>
      {quote.discountAmount > 0 ? (
        <p className="flex justify-between border-t border-slate-100 pt-2 text-sm text-slate-600">
          <span>Descuento</span>
          <span className="tabular-nums">-{money(quote.discountAmount, quote.currency)}</span>
        </p>
      ) : null}
      <p className="mt-1 flex justify-between border-t border-slate-200 pt-2 text-lg font-semibold">
        <span>Total</span>
        <span className="tabular-nums">{money(quote.total, quote.currency)}</span>
      </p>
      <p className="mt-1 text-xs text-slate-500">
        {quote.warrantyDays ? `Garantía ${quote.warrantyDays} días. ` : ''}
        {quote.validUntilUtc ? `Válido hasta ${date(quote.validUntilUtc)}.` : ''}
      </p>
      {quote.notes ? <p className="mt-2 whitespace-pre-wrap text-sm text-slate-700">{quote.notes}</p> : null}

      {quote.canDecide ? (
        <div className="mt-4 grid grid-cols-2 gap-2">
          <Button variant="success" size="lg" onClick={() => setDeciding('approve')}>
            Aprobar
          </Button>
          <Button size="lg" onClick={() => setDeciding('reject')}>
            Rechazar
          </Button>
        </div>
      ) : (
        <p className={cn('mt-3 rounded-lg px-3 py-2 text-sm font-medium', quote.status === 'Approved' ? 'bg-emerald-50 text-emerald-800' : quote.status === 'Rejected' ? 'bg-rose-50 text-rose-800' : 'bg-slate-50 text-slate-700')}>{quote.statusLabel}</p>
      )}
      <a className="mt-3 inline-block text-sm text-brand-700 underline" href={portalApi.pdfUrl(token, 'quote')} target="_blank" rel="noreferrer">
        Descargar presupuesto (PDF)
      </a>

      {deciding ? (
        <Modal
          open
          size="sm"
          onClose={() => setDeciding(null)}
          title={deciding === 'approve' ? '¿Aprobás el presupuesto?' : '¿Rechazás el presupuesto?'}
          description={deciding === 'approve' ? `Total ${money(quote.total, quote.currency)}. El taller empieza la reparación.` : 'Te avisamos cuando puedas retirar el equipo.'}
          footer={
            <>
              <Button onClick={() => setDeciding(null)}>Volver</Button>
              <Button variant={deciding === 'approve' ? 'success' : 'danger'} loading={decide.isPending} onClick={() => decide.mutate()}>
                {deciding === 'approve' ? 'Sí, aprobar' : 'Sí, rechazar'}
              </Button>
            </>
          }
        >
          <Textarea rows={2} value={note} onChange={(e) => setNote(e.target.value)} placeholder="Comentario para el taller (opcional)" aria-label="Comentario" />
          {decide.isError ? <Alert tone="rose" className="mt-2">{errorMessage(decide.error)}</Alert> : null}
        </Modal>
      ) : null}
    </Section>
  )
}

function MoneyCard({ token, data }: { token: string; data: PublicTracking }) {
  const m = data.money!
  const [error, setError] = useState<string | null>(null)
  const pay = useMutation({
    mutationFn: () => portalApi.paymentLink(token),
    onSuccess: (r) => {
      window.location.href = r.url
    },
    onError: (err) => setError(errorMessage(err)),
  })
  return (
    <Section title="Pagos">
      <dl className="grid grid-cols-3 gap-2 text-center text-sm">
        <div>
          <dt className="text-xs text-slate-500">Total</dt>
          <dd className="font-semibold tabular-nums">{money(m.total, m.currency)}</dd>
        </div>
        <div>
          <dt className="text-xs text-slate-500">Pagado</dt>
          <dd className="font-semibold tabular-nums text-emerald-700">{money(m.paid, m.currency)}</dd>
        </div>
        <div>
          <dt className="text-xs text-slate-500">Saldo</dt>
          <dd className={cn('font-semibold tabular-nums', m.balance > 0 ? 'text-rose-700' : 'text-slate-800')}>{money(m.balance, m.currency)}</dd>
        </div>
      </dl>
      {data.canPayOnline && m.balance > 0 ? (
        <Button variant="primary" size="lg" className="mt-4 w-full" loading={pay.isPending} onClick={() => pay.mutate()}>
          Pagar {money(m.balance, m.currency)} con Mercado Pago
        </Button>
      ) : null}
      {error ? <Alert tone="rose" className="mt-2">{error}</Alert> : null}
    </Section>
  )
}

function FeedbackCard({ token, data, highlight }: { token: string; data: PublicTracking; highlight: boolean }) {
  const ref = useRef<HTMLElement | null>(null)
  const [score, setScore] = useState(0)
  const [comment, setComment] = useState('')
  const [result, setResult] = useState<{ googleReviewUrl?: string | null } | null>(null)
  const send = useMutation({
    mutationFn: () => portalApi.feedback(token, score, comment.trim() || undefined),
    onSuccess: (r) => setResult(r),
  })

  useEffect(() => {
    if (highlight) ref.current?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }, [highlight])

  if (data.feedbackSubmitted && !result) {
    return (
      <Section title="Tu opinión">
        <p className="text-sm text-slate-600">¡Gracias por contarnos cómo te fue!</p>
      </Section>
    )
  }

  return (
    <Section title="¿Cómo te atendimos?" innerRef={ref} className={highlight ? 'ring-2 ring-brand-400' : undefined}>
      {result ? (
        <div className="space-y-3 text-sm">
          <p className="text-slate-700">¡Gracias por tu opinión! Nos ayuda a mejorar.</p>
          {result.googleReviewUrl && score >= 4 ? (
            <a href={result.googleReviewUrl} target="_blank" rel="noreferrer" className="inline-flex rounded-lg bg-brand-600 px-4 py-2 font-medium text-white">
              Dejanos una reseña en Google ★
            </a>
          ) : null}
        </div>
      ) : (
        <div className="space-y-3">
          <div className="flex justify-center gap-1" role="radiogroup" aria-label="Puntaje">
            {[1, 2, 3, 4, 5].map((s) => (
              <button
                key={s}
                type="button"
                role="radio"
                aria-checked={score === s}
                aria-label={`${s} de 5`}
                onClick={() => setScore(s)}
                className={cn('text-4xl transition', s <= score ? 'text-amber-400' : 'text-slate-300 hover:text-amber-200')}
              >
                ★
              </button>
            ))}
          </div>
          {score > 0 ? (
            <>
              <Textarea rows={3} value={comment} onChange={(e) => setComment(e.target.value)} placeholder={score >= 4 ? '¿Qué fue lo que más te gustó?' : '¿Qué podemos mejorar?'} aria-label="Comentario" />
              <Button variant="primary" className="w-full" loading={send.isPending} onClick={() => send.mutate()}>
                Enviar
              </Button>
              {send.isError ? <Alert tone="rose">{errorMessage(send.error)}</Alert> : null}
            </>
          ) : null}
        </div>
      )}
    </Section>
  )
}

function ContactCard({ data }: { data: PublicTracking }) {
  const s = data.shop
  if (!s.phone && !s.whatsAppUrl && !s.address) return null
  return (
    <Section title="Contacto">
      <div className="space-y-2 text-sm">
        {s.address ? (
          <p className="text-slate-700">
            {s.address}
            {s.city ? `, ${s.city}` : ''}
          </p>
        ) : null}
        {s.pickupHours ? <p className="text-slate-500">Horario: {s.pickupHours}</p> : null}
        <div className="flex flex-wrap gap-2 pt-1">
          {s.whatsAppUrl ? (
            <a href={s.whatsAppUrl} target="_blank" rel="noreferrer" className="inline-flex items-center rounded-lg bg-emerald-600 px-4 py-2 font-medium text-white">
              Escribinos por WhatsApp
            </a>
          ) : null}
          {s.phone ? (
            <a href={`tel:${s.phone.replace(/[^\d+]/g, '')}`} className="inline-flex items-center rounded-lg border border-slate-300 px-4 py-2 font-medium text-slate-700">
              Llamar
            </a>
          ) : null}
        </div>
      </div>
    </Section>
  )
}
