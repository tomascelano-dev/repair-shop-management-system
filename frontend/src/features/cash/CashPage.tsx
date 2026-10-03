import { useMemo, useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { cashApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { CashMovementType, CashSession, PaymentMethod } from '../../api/types'
import { Money } from '../../components/domain'
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Field, Input, Loading, Modal, PageHeader, Pagination, Select, Stat, Table, Td, Textarea, Th } from '../../components/ui'
import { cn } from '../../lib/cn'
import { dateTime, money, parseAmount, relative, time } from '../../lib/format'
import { openPdf } from '../../lib/files'
import { CASH_MOVEMENT, PAYMENT_METHOD } from '../../lib/labels'

const HISTORY_TAKE = 15
const ARS_BILLS = [20000, 10000, 2000, 1000, 500, 200, 100, 50, 20, 10]

export function CashPage() {
  const current = useQuery({ queryKey: ['cash', 'current'], queryFn: cashApi.current })
  const [skip, setSkip] = useState(0)
  const history = useQuery({ queryKey: ['cash', 'sessions', skip], queryFn: () => cashApi.sessions(skip, HISTORY_TAKE), placeholderData: keepPreviousData })
  const [viewing, setViewing] = useState<string | null>(null)
  // Lives here (not inside the open-session view) because closing the register unmounts that view.
  const [closed, setClosed] = useState<CashSession | null>(null)

  return (
    <div>
      <PageHeader title="Caja" subtitle="Apertura, movimientos y arqueo del turno. Todos los cobros en efectivo pasan por acá." />

      {current.isLoading ? (
        <Loading />
      ) : current.isError ? (
        <ErrorState error={errorMessage(current.error)} onRetry={() => void current.refetch()} />
      ) : current.data ? (
        <OpenSession session={current.data} onClosed={setClosed} />
      ) : (
        <OpenCashCard lastClosed={history.data?.items.find((s) => s.status === 'Closed') ?? null} />
      )}

      <Card title="Turnos anteriores" className="mt-6" padded={false}>
        {history.isLoading ? (
          <Loading />
        ) : (history.data?.items ?? []).length === 0 ? (
          <div className="p-4">
            <EmptyState title="Todavía no hay turnos" />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Turno</Th>
                  <Th>Apertura</Th>
                  <Th>Cierre</Th>
                  <Th align="right">Esperado</Th>
                  <Th align="right">Contado</Th>
                  <Th align="right">Diferencia</Th>
                  <Th />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {history.data!.items.map((s) => (
                  <tr key={s.id}>
                    <Td className="font-medium">
                      #{s.number} {s.status === 'Open' ? <Badge tone="green">Abierta</Badge> : null}
                    </Td>
                    <Td>
                      {dateTime(s.openedAtUtc)}
                      <span className="block text-xs text-slate-500">{s.openedByName}</span>
                    </Td>
                    <Td>
                      {s.closedAtUtc ? dateTime(s.closedAtUtc) : '—'}
                      <span className="block text-xs text-slate-500">{s.closedByName}</span>
                    </Td>
                    <Td align="right">{money(s.expectedCash, s.currency)}</Td>
                    <Td align="right">{money(s.countedCash, s.currency)}</Td>
                    <Td align="right">
                      <DifferenceText value={s.difference} currency={s.currency} />
                    </Td>
                    <Td align="right">
                      <span className="flex justify-end gap-1">
                        <Button size="sm" variant="ghost" onClick={() => setViewing(s.id)}>
                          Detalle
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => void openPdf(`/cash/sessions/${s.id}/report`)}>
                          PDF
                        </Button>
                      </span>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <div className="px-4 pb-3">
              <Pagination skip={skip} take={HISTORY_TAKE} total={history.data?.total ?? 0} onChange={setSkip} />
            </div>
          </>
        )}
      </Card>

      {viewing ? <SessionDialog id={viewing} onClose={() => setViewing(null)} /> : null}
      {closed ? <ClosedDialog session={closed} onClose={() => setClosed(null)} /> : null}
    </div>
  )
}

function ClosedDialog({ session, onClose }: { session: CashSession; onClose: () => void }) {
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title={`Turno #${session.number} cerrado`}
      footer={
        <>
          <Button onClick={() => void openPdf(`/cash/sessions/${session.id}/report`)}>Ver reporte PDF</Button>
          <Button variant="primary" onClick={onClose}>
            Listo
          </Button>
        </>
      }
    >
      <div className="space-y-2 text-center">
        <p className="text-sm text-slate-500">Diferencia de efectivo</p>
        <p className="text-2xl">
          <DifferenceText value={session.difference} currency={session.currency} />
        </p>
        <p className="text-xs text-slate-500">
          Esperado {money(session.expectedCash, session.currency)} · contado {money(session.countedCash, session.currency)}
        </p>
      </div>
    </Modal>
  )
}

function DifferenceText({ value, currency }: { value: number | null | undefined; currency: string }) {
  if (value === null || value === undefined) return <span className="text-slate-400">—</span>
  if (Math.abs(value) < 0.005) return <span className="text-emerald-700">Sin diferencia</span>
  return (
    <span className={cn('font-medium tabular-nums', value < 0 ? 'text-rose-700' : 'text-amber-700')}>
      {value > 0 ? '+' : ''}
      {money(value, currency)} {value < 0 ? 'faltante' : 'sobrante'}
    </span>
  )
}

function OpenCashCard({ lastClosed }: { lastClosed: CashSession | null }) {
  const queryClient = useQueryClient()
  const suggested = lastClosed?.countedCash ?? null
  const [amount, setAmount] = useState(suggested !== null ? String(suggested).replace('.', ',') : '')
  const [notes, setNotes] = useState('')
  const value = amount.trim() === '' ? 0 : parseAmount(amount)
  const open = useMutation({
    mutationFn: () => cashApi.open(value ?? 0, undefined, notes.trim() || undefined),
    onSuccess: () => {
      toast.success('Caja abierta')
      void queryClient.invalidateQueries({ queryKey: ['cash'] })
    },
    onError: (err) => toast.error('No se pudo abrir la caja', { description: errorMessage(err) }),
  })
  return (
    <Card title="La caja está cerrada">
      <form
        className="grid gap-4 md:grid-cols-[1fr_2fr_auto] md:items-end"
        onSubmit={(e) => {
          e.preventDefault()
          open.mutate()
        }}
      >
        <Field label="Efectivo inicial (cambio)" hint={suggested !== null ? `El último cierre contó ${money(suggested, lastClosed?.currency)}.` : 'Lo que hay en el cajón al empezar.'}>
          <Input inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} autoFocus placeholder="0" />
        </Field>
        <Field label="Nota (opcional)">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Turno mañana, cajero…" />
        </Field>
        <Button type="submit" variant="success" loading={open.isPending} disabled={value === null || value < 0}>
          Abrir caja
        </Button>
      </form>
    </Card>
  )
}

function OpenSession({ session, onClosed }: { session: CashSession; onClosed: (s: CashSession) => void }) {
  const [movement, setMovement] = useState<CashMovementType | null>(null)
  const [closing, setClosing] = useState(false)
  const cashLine = session.summary.find((l) => l.method === 'Cash' && l.currency === session.currency)
  const nonCash = session.summary.filter((l) => !(l.method === 'Cash' && l.currency === session.currency))
  const sales = (session.movements ?? []).filter((m) => m.type === 'Sale').length
  const orderPayments = (session.movements ?? []).filter((m) => m.type === 'OrderPayment').length

  return (
    <div className="space-y-4">
      <Card>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="flex items-center gap-2 text-base font-semibold text-slate-900">
              Caja abierta · turno #{session.number} <Badge tone="green">Abierta</Badge>
            </p>
            <p className="text-sm text-slate-500">
              Abrió {session.openedByName} {relative(session.openedAtUtc)} · inicial {money(session.openingCash, session.currency)}
              {session.openingNotes ? ` · ${session.openingNotes}` : ''}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button onClick={() => setMovement('Income')}>+ Ingreso</Button>
            <Button onClick={() => setMovement('Expense')}>− Gasto</Button>
            <Button onClick={() => setMovement('Withdrawal')}>Retiro</Button>
            <Button variant="ghost" onClick={() => void openPdf(`/cash/sessions/${session.id}/report`)}>
              Reporte parcial
            </Button>
            <Button variant="primary" onClick={() => setClosing(true)}>
              Cerrar caja
            </Button>
          </div>
        </div>
      </Card>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Efectivo esperado en cajón" value={money(cashLine?.expected ?? session.openingCash, session.currency)} tone="good" hint="Inicial + entradas − salidas en efectivo" />
        <Stat label="Entradas en efectivo" value={money(cashLine?.inflows ?? 0, session.currency)} />
        <Stat label="Salidas en efectivo" value={money(cashLine?.outflows ?? 0, session.currency)} tone={(cashLine?.outflows ?? 0) > 0 ? 'warn' : 'default'} />
        <Stat label="Operaciones" value={sales + orderPayments} hint={`${sales} venta(s) · ${orderPayments} cobro(s) de órdenes`} />
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]">
        <Card title="Por medio de pago">
          {session.summary.length === 0 ? (
            <p className="text-sm text-slate-500">Sin movimientos todavía.</p>
          ) : (
            <ul className="divide-y divide-slate-100 text-sm">
              {[...(cashLine ? [cashLine] : []), ...nonCash].map((l) => (
                <li key={`${l.method}-${l.currency}`} className="flex items-center justify-between py-2">
                  <span>
                    {PAYMENT_METHOD[l.method]} <span className="text-xs text-slate-400">{l.currency}</span>
                    <span className="block text-xs text-slate-500">
                      +{money(l.inflows, l.currency)} / −{money(l.outflows, l.currency)}
                    </span>
                  </span>
                  <span className="font-semibold tabular-nums">{money(l.method === 'Cash' ? l.expected : l.net, l.currency)}</span>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card title="Movimientos del turno" padded={false}>
          {(session.movements ?? []).length === 0 ? (
            <p className="p-4 text-sm text-slate-500">Todavía no hay movimientos. Las ventas y los cobros de órdenes se registran solos.</p>
          ) : (
            <div className="max-h-[420px] overflow-y-auto">
              <Table>
                <thead className="sticky top-0 bg-slate-50">
                  <tr>
                    <Th>Hora</Th>
                    <Th>Concepto</Th>
                    <Th>Medio</Th>
                    <Th align="right">Monto</Th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {[...(session.movements ?? [])].reverse().map((m) => (
                    <tr key={m.id}>
                      <Td className="whitespace-nowrap text-xs text-slate-500">{time(m.createdAtUtc)}</Td>
                      <Td>
                        <span className="text-slate-800">{m.description}</span>
                        <span className="block text-xs text-slate-500">
                          {CASH_MOVEMENT[m.type]}
                          {m.category ? ` · ${m.category}` : ''} · {m.createdByName}
                        </span>
                      </Td>
                      <Td className="text-xs">{PAYMENT_METHOD[m.method]}</Td>
                      <Td align="right" className={cn('font-medium', m.signedAmount < 0 ? 'text-rose-700' : 'text-emerald-700')}>
                        {m.signedAmount > 0 ? '+' : ''}
                        {money(m.signedAmount, m.currency)}
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </Card>
      </div>

      {movement ? <MovementDialog type={movement} currency={session.currency} onClose={() => setMovement(null)} /> : null}
      {closing ? (
        <CloseDialog
          session={session}
          onClose={() => setClosing(false)}
          onClosed={(s) => {
            setClosing(false)
            onClosed(s)
          }}
        />
      ) : null}
    </div>
  )
}

const MOVEMENT_TEXT: Record<string, { title: string; placeholder: string; categories: string[] }> = {
  Income: { title: 'Registrar ingreso', placeholder: 'Aporte de cambio, cobro varios…', categories: ['Cambio', 'Cobro varios', 'Otro'] },
  Expense: { title: 'Registrar gasto', placeholder: 'Delivery, limpieza, insumos…', categories: ['Insumos', 'Envíos', 'Limpieza', 'Comida', 'Servicios', 'Repuestos', 'Otro'] },
  Withdrawal: { title: 'Registrar retiro', placeholder: 'Retiro del dueño, depósito bancario…', categories: ['Retiro del dueño', 'Depósito', 'Otro'] },
}

function MovementDialog({ type, currency, onClose }: { type: CashMovementType; currency: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const text = MOVEMENT_TEXT[type]!
  const [amount, setAmount] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState(text.categories[0]!)
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  const value = parseAmount(amount)
  const save = useMutation({
    mutationFn: () => cashApi.movement({ type, amount: value!, method, currency, description: description.trim(), category }),
    onSuccess: () => {
      toast.success('Movimiento registrado')
      void queryClient.invalidateQueries({ queryKey: ['cash'] })
      onClose()
    },
    onError: (err) => toast.error('No se pudo registrar', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title={text.title}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} disabled={!value || value <= 0 || description.trim().length < 2} onClick={() => save.mutate()}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3">
          <Field label="Monto" required>
            <Input inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} autoFocus />
          </Field>
          <Field label="Medio">
            <Select value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
              {(Object.keys(PAYMENT_METHOD) as PaymentMethod[]).map((m) => (
                <option key={m} value={m}>
                  {PAYMENT_METHOD[m]}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <Field label="Categoría">
          <Select value={category} onChange={(e) => setCategory(e.target.value)}>
            {text.categories.map((c) => (
              <option key={c}>{c}</option>
            ))}
          </Select>
        </Field>
        <Field label="Descripción" required>
          <Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder={text.placeholder} />
        </Field>
      </div>
    </Modal>
  )
}

function CloseDialog({ session, onClose, onClosed }: { session: CashSession; onClose: () => void; onClosed: (s: CashSession) => void }) {
  const queryClient = useQueryClient()
  const cashLine = session.summary.find((l) => l.method === 'Cash' && l.currency === session.currency)
  const expected = cashLine?.expected ?? session.openingCash
  const others = session.summary.filter((l) => !(l.method === 'Cash' && l.currency === session.currency) && (l.inflows > 0 || l.outflows > 0))
  const [useCounter, setUseCounter] = useState(session.currency === 'ARS')
  const [bills, setBills] = useState<Record<number, string>>({})
  const [coins, setCoins] = useState('')
  const [countedText, setCountedText] = useState('')
  const [declared, setDeclared] = useState<Record<string, string>>(() => Object.fromEntries(others.map((l) => [`${l.method}|${l.currency}`, String(l.expected).replace('.', ',')])))
  const [notes, setNotes] = useState('')

  const counted = useMemo(() => {
    if (!useCounter) return parseAmount(countedText)
    const billsTotal = ARS_BILLS.reduce((s, b) => s + b * (Number(bills[b] ?? 0) || 0), 0)
    return Math.round((billsTotal + (parseAmount(coins) ?? 0)) * 100) / 100
  }, [useCounter, countedText, bills, coins])
  const difference = counted === null ? null : Math.round((counted - expected) * 100) / 100

  const close = useMutation({
    mutationFn: () =>
      cashApi.close({
        countedCash: counted ?? 0,
        declared: others.map((l) => ({ method: l.method, currency: l.currency, amount: parseAmount(declared[`${l.method}|${l.currency}`] ?? '') ?? 0 })),
        notes: notes.trim() || null,
      }),
    onSuccess: (s) => {
      onClosed(s)
      void queryClient.invalidateQueries({ queryKey: ['cash'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => toast.error('No se pudo cerrar la caja', { description: errorMessage(err) }),
  })

  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title={`Cerrar turno #${session.number}`}
      description="Contá el efectivo del cajón. La diferencia queda registrada en el cierre."
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={close.isPending} disabled={counted === null || counted < 0} onClick={() => close.mutate()}>
            Cerrar caja
          </Button>
        </>
      }
    >
      <div className="grid gap-5 md:grid-cols-2">
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <p className="text-sm font-medium text-slate-700">Efectivo contado</p>
            {session.currency === 'ARS' ? (
              <button type="button" className="text-xs text-brand-700 hover:underline" onClick={() => setUseCounter((v) => !v)}>
                {useCounter ? 'Ingresar total' : 'Contar billetes'}
              </button>
            ) : null}
          </div>
          {useCounter ? (
            <div className="space-y-1.5">
              {ARS_BILLS.map((b) => (
                <div key={b} className="grid grid-cols-[1fr_80px_1fr] items-center gap-2 text-sm">
                  <span className="text-slate-600">{money(b).replace(/,00$/, '')}</span>
                  <Input
                    className="h-8 text-right"
                    inputMode="numeric"
                    value={bills[b] ?? ''}
                    onChange={(e) => setBills((x) => ({ ...x, [b]: e.target.value.replace(/\D/g, '') }))}
                    aria-label={`Cantidad de billetes de ${b}`}
                    placeholder="0"
                  />
                  <span className="text-right tabular-nums text-slate-500">{money(b * (Number(bills[b] ?? 0) || 0))}</span>
                </div>
              ))}
              <div className="grid grid-cols-[1fr_80px_1fr] items-center gap-2 text-sm">
                <span className="text-slate-600">Monedas / otros</span>
                <Input className="h-8 text-right" inputMode="decimal" value={coins} onChange={(e) => setCoins(e.target.value)} aria-label="Monedas" placeholder="0" />
                <span />
              </div>
            </div>
          ) : (
            <Input inputMode="decimal" value={countedText} onChange={(e) => setCountedText(e.target.value)} autoFocus aria-label="Efectivo contado" />
          )}
        </div>

        <div className="space-y-4">
          <div className="rounded-xl bg-slate-50 p-4 text-sm">
            <p className="flex justify-between">
              <span className="text-slate-600">Esperado</span>
              <span className="tabular-nums">{money(expected, session.currency)}</span>
            </p>
            <p className="flex justify-between">
              <span className="text-slate-600">Contado</span>
              <span className="tabular-nums">{money(counted, session.currency)}</span>
            </p>
            <p className="mt-2 flex justify-between border-t border-slate-200 pt-2 text-base">
              <span>Diferencia</span>
              <DifferenceText value={difference} currency={session.currency} />
            </p>
          </div>

          {others.length > 0 ? (
            <div className="space-y-2">
              <p className="text-sm font-medium text-slate-700">Otros medios (controlá contra el posnet / home banking)</p>
              {others.map((l) => {
                const key = `${l.method}|${l.currency}`
                const value = parseAmount(declared[key] ?? '')
                const diff = value === null ? null : Math.round((value - l.expected) * 100) / 100
                return (
                  <div key={key} className="grid grid-cols-[1fr_120px] items-center gap-2 text-sm">
                    <span>
                      {PAYMENT_METHOD[l.method]} <span className="text-xs text-slate-400">{l.currency}</span>
                      <span className="block text-xs text-slate-500">
                        Sistema: {money(l.expected, l.currency)}
                        {diff !== null && Math.abs(diff) >= 0.01 ? <span className={diff < 0 ? ' text-rose-600' : ' text-amber-600'}> ({diff > 0 ? '+' : ''}{money(diff, l.currency)})</span> : null}
                      </span>
                    </span>
                    <Input className="h-8 text-right" inputMode="decimal" value={declared[key] ?? ''} onChange={(e) => setDeclared((d) => ({ ...d, [key]: e.target.value }))} aria-label={`Declarado ${PAYMENT_METHOD[l.method]}`} />
                  </div>
                )
              })}
            </div>
          ) : null}

          <Field label="Notas del cierre">
            <Textarea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </Field>
          {difference !== null && Math.abs(difference) >= 0.01 ? (
            <Alert tone={difference < 0 ? 'rose' : 'amber'}>Hay una diferencia de {money(difference, session.currency)}. Revisá el conteo o explicá el motivo en las notas.</Alert>
          ) : null}
        </div>
      </div>
    </Modal>
  )
}

function SessionDialog({ id, onClose }: { id: string; onClose: () => void }) {
  const s = useQuery({ queryKey: ['cash', 'session', id], queryFn: () => cashApi.session(id) })
  const session = s.data
  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title={session ? `Turno #${session.number}` : 'Turno'}
      footer={<Button onClick={() => void openPdf(`/cash/sessions/${id}/report`)}>Reporte PDF</Button>}
    >
      {s.isLoading ? (
        <Loading />
      ) : !session ? (
        <ErrorState error={errorMessage(s.error)} />
      ) : (
        <div className="space-y-4 text-sm">
          <p className="text-slate-600">
            {dateTime(session.openedAtUtc)} ({session.openedByName}) → {session.closedAtUtc ? `${dateTime(session.closedAtUtc)} (${session.closedByName})` : 'abierta'}
          </p>
          <Table>
            <thead>
              <tr>
                <Th>Medio</Th>
                <Th align="right">Entradas</Th>
                <Th align="right">Salidas</Th>
                <Th align="right">Esperado</Th>
                <Th align="right">Declarado</Th>
                <Th align="right">Diferencia</Th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {session.summary.map((l) => (
                <tr key={`${l.method}-${l.currency}`}>
                  <Td>
                    {PAYMENT_METHOD[l.method]} <span className="text-xs text-slate-400">{l.currency}</span>
                  </Td>
                  <Td align="right">{money(l.inflows, l.currency)}</Td>
                  <Td align="right">{money(l.outflows, l.currency)}</Td>
                  <Td align="right">{money(l.expected, l.currency)}</Td>
                  <Td align="right">{money(l.declared, l.currency)}</Td>
                  <Td align="right">
                    <DifferenceText value={l.difference} currency={l.currency} />
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
          {session.closingNotes ? <p className="text-slate-600">Notas: {session.closingNotes}</p> : null}
          {(session.movements ?? []).length > 0 ? (
            <div>
              <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Movimientos</p>
              <ul className="max-h-64 divide-y divide-slate-100 overflow-y-auto rounded-lg border border-slate-200">
                {session.movements!.map((m) => (
                  <li key={m.id} className="flex justify-between gap-2 px-3 py-1.5">
                    <span>
                      {m.description}
                      <span className="block text-xs text-slate-500">
                        {CASH_MOVEMENT[m.type]} · {PAYMENT_METHOD[m.method]} · {dateTime(m.createdAtUtc)}
                      </span>
                    </span>
                    <Money value={m.signedAmount} currency={m.currency} className={m.signedAmount < 0 ? 'text-rose-700' : 'text-emerald-700'} />
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
          <p className="text-xs text-slate-500">
            ¿Buscás las ventas del turno? <Link className="text-brand-700 underline" to="/sales">Ver ventas</Link>
          </p>
        </div>
      )}
    </Modal>
  )
}
