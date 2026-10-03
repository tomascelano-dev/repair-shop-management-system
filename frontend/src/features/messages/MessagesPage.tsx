import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { notificationsApi, settingsApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { OutboxMessage, OutboxStatus } from '../../api/types'
import { useSession } from '../../auth/session'
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Loading, Modal, PageHeader, Pagination, Table, Td, Th } from '../../components/ui'
import { dateTime, relative } from '../../lib/format'
import { CHANNEL, OUTBOX_STATUS } from '../../lib/labels'

const TAKE = 30

export function MessagesPage() {
  const { can } = useSession()
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()
  const status = (params.get('status') ?? '') as OutboxStatus | ''
  const [skip, setSkip] = useState(0)
  const [viewing, setViewing] = useState<OutboxMessage | null>(null)
  const messages = useQuery({
    queryKey: ['notifications', status, skip],
    queryFn: () => notificationsApi.list({ status: status || undefined, skip, take: TAKE }),
    placeholderData: keepPreviousData,
    refetchInterval: 30_000,
  })
  const integrations = useQuery({ queryKey: ['integrations'], queryFn: settingsApi.integrations, enabled: can('admin') })
  const channels = integrations.data?.notificationChannels ?? null

  const act = useMutation({
    mutationFn: ({ id, action }: { id: string; action: 'retry' | 'cancel' }) => (action === 'retry' ? notificationsApi.retry(id) : notificationsApi.cancel(id)),
    onSuccess: (_, v) => {
      toast.success(v.action === 'retry' ? 'Se reintentará el envío en instantes' : 'Envío cancelado')
      setViewing(null)
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
    onError: (err) => toast.error('No se pudo completar', { description: errorMessage(err) }),
  })

  return (
    <div>
      <PageHeader title="Mensajes" subtitle="Avisos automáticos a clientes (WhatsApp, email, SMS): enviados, pendientes y con error." />
      {channels && Object.values(channels).every((v) => !v) ? (
        <Alert tone="amber" className="mb-4" title="No hay canales de envío configurados">
          Los avisos se registran pero no salen solos. Mientras tanto podés enviarlos por WhatsApp desde cada orden con un clic.
        </Alert>
      ) : null}
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {(['', 'Pending', 'Failed', 'Sent', 'Cancelled'] as const).map((s) => (
          <button
            key={s || 'all'}
            type="button"
            onClick={() => {
              setSkip(0)
              setParams(s ? { status: s } : {}, { replace: true })
            }}
            className={`rounded-full border px-3 py-1 text-sm ${status === s ? 'border-brand-500 bg-brand-50 text-brand-700' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'}`}
          >
            {s ? OUTBOX_STATUS[s].label : 'Todos'}
          </button>
        ))}
      </div>
      <Card padded={false}>
        {messages.isLoading ? (
          <Loading />
        ) : messages.isError ? (
          <div className="p-4">
            <ErrorState error={errorMessage(messages.error)} onRetry={() => void messages.refetch()} />
          </div>
        ) : (messages.data?.items ?? []).length === 0 ? (
          <div className="p-4">
            <EmptyState title="No hay mensajes" />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Mensaje</Th>
                  <Th>Canal</Th>
                  <Th>Destinatario</Th>
                  <Th>Estado</Th>
                  <Th>Creado</Th>
                  <Th />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {messages.data!.items.map((m) => (
                  <tr key={m.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setViewing(m)}>
                    <Td>
                      <span className="font-medium text-slate-800">{m.title}</span>
                      <span className="block max-w-md truncate text-xs text-slate-500">{m.body}</span>
                    </Td>
                    <Td>{CHANNEL[m.channel]}</Td>
                    <Td className="text-xs">{m.recipient}</Td>
                    <Td>
                      <Badge tone={OUTBOX_STATUS[m.status].tone}>{OUTBOX_STATUS[m.status].label}</Badge>
                      {m.attemptCount > 1 ? <span className="block text-xs text-slate-500">{m.attemptCount} intentos</span> : null}
                    </Td>
                    <Td className="whitespace-nowrap text-xs text-slate-500">{relative(m.createdAtUtc)}</Td>
                    <Td align="right">
                      {m.relatedEntityType === 'repair_order' && m.relatedEntityId ? (
                        <Link to={`/orders/${m.relatedEntityId}`} onClick={(e) => e.stopPropagation()} className="text-xs text-brand-700 hover:underline">
                          Ver orden
                        </Link>
                      ) : null}
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <div className="px-4 pb-3">
              <Pagination skip={skip} take={TAKE} total={messages.data?.total ?? 0} onChange={setSkip} />
            </div>
          </>
        )}
      </Card>

      {viewing ? (
        <Modal
          open
          onClose={() => setViewing(null)}
          title={viewing.title}
          description={`${CHANNEL[viewing.channel]} a ${viewing.recipient}`}
          footer={
            viewing.status !== 'Sent' ? (
              <>
                {viewing.status !== 'Cancelled' ? (
                  <Button variant="ghost" loading={act.isPending} onClick={() => act.mutate({ id: viewing.id, action: 'cancel' })}>
                    No enviar
                  </Button>
                ) : null}
                <Button variant="primary" loading={act.isPending} onClick={() => act.mutate({ id: viewing.id, action: 'retry' })}>
                  Reintentar ahora
                </Button>
              </>
            ) : undefined
          }
        >
          <div className="space-y-3 text-sm">
            <Badge tone={OUTBOX_STATUS[viewing.status].tone}>{OUTBOX_STATUS[viewing.status].label}</Badge>
            <p className="whitespace-pre-wrap rounded-lg bg-slate-50 p-3 text-slate-800">{viewing.body}</p>
            <ul className="space-y-1 text-xs text-slate-500">
              <li>Creado {dateTime(viewing.createdAtUtc)}</li>
              {viewing.sentAtUtc ? <li>Enviado {dateTime(viewing.sentAtUtc)}{viewing.provider ? ` vía ${viewing.provider}` : ''}</li> : null}
              {viewing.nextAttemptAtUtc && viewing.status !== 'Sent' ? <li>Próximo intento {dateTime(viewing.nextAttemptAtUtc)}</li> : null}
              <li>Intentos: {viewing.attemptCount}</li>
            </ul>
            {viewing.lastError ? <Alert tone="rose" title="Último error">{viewing.lastError}</Alert> : null}
          </div>
        </Modal>
      ) : null}
    </div>
  )
}
