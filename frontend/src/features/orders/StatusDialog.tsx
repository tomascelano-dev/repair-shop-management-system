import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ordersApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { ChangeStatusResult, NotificationChannel, OrderStatus, RepairOrder } from '../../api/types'
import { useSession } from '../../auth/session'
import { Alert, Button, Checkbox, Field, Modal, Select, Textarea } from '../../components/ui'
import { money } from '../../lib/format'
import { CHANNEL, ORDER_STATUS } from '../../lib/labels'
import { transitionBlocker } from './rules'

export function StatusDialog({ order, target, onClose }: { order: RepairOrder; target: OrderStatus; onClose: () => void }) {
  const queryClient = useQueryClient()
  const { role } = useSession()
  const [notify, setNotify] = useState(true)
  const [channel, setChannel] = useState<NotificationChannel | ''>('')
  const [reason, setReason] = useState('')
  const [force, setForce] = useState(false)
  const [result, setResult] = useState<ChangeStatusResult | null>(null)
  const unpaid = target === 'Delivered' && order.balanceDue > 0
  const blocker = transitionBlocker(order, target)

  const mutation = useMutation({
    mutationFn: () =>
      ordersApi.changeStatus(order.id, {
        status: target,
        enqueueOutbox: notify,
        channel: channel || null,
        reason: reason.trim() || null,
        forceUnpaidDelivery: unpaid && force,
      }),
    onSuccess: (res) => {
      setResult(res)
      toast.success(`Estado: ${ORDER_STATUS[res.toStatus].label}`)
      void queryClient.invalidateQueries({ queryKey: ['order', order.id] })
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => toast.error('No se pudo cambiar el estado', { description: errorMessage(err) }),
  })

  if (result) {
    return (
      <Modal
        open
        onClose={onClose}
        title={`Orden ${order.code}: ${ORDER_STATUS[result.toStatus].label}`}
        footer={<Button variant="primary" onClick={onClose}>Listo</Button>}
      >
        <div className="space-y-3 text-sm">
          {result.outboxItemId ? (
            <Alert tone="green">El mensaje quedó en cola y se envía automáticamente al cliente.</Alert>
          ) : result.suggestedMessage ? (
            <Alert tone="slate">No se envió automáticamente. Podés mandarlo por WhatsApp con un clic.</Alert>
          ) : null}
          {result.suggestedMessage ? <pre className="whitespace-pre-wrap rounded-lg bg-slate-50 p-3 font-sans text-slate-700">{result.suggestedMessage}</pre> : null}
          {result.whatsAppUrl ? (
            <a href={result.whatsAppUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-2 rounded-lg bg-emerald-600 px-3 py-2 font-medium text-white hover:bg-emerald-700">
              Abrir WhatsApp
            </a>
          ) : null}
        </div>
      </Modal>
    )
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={`Pasar a “${ORDER_STATUS[target].label}”`}
      description={`Orden ${order.code} · ${order.deviceLabel}`}
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button
            variant={target === 'Cancelled' ? 'danger' : 'primary'}
            loading={mutation.isPending}
            disabled={!!blocker || (unpaid && !force) || (target === 'Cancelled' && reason.trim().length < 3)}
            onClick={() => mutation.mutate()}
          >
            Confirmar
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {blocker ? <Alert tone="amber">{blocker}</Alert> : null}
        {unpaid ? (
          <Alert tone="rose" title={`Saldo pendiente: ${money(order.balanceDue, order.currency)}`}>
            Registrá el pago antes de entregar.
            {role === 'Admin' ? (
              <Checkbox className="mt-2" label="Entregar igual (queda registrado en auditoría)" checked={force} onChange={(e) => setForce(e.target.checked)} />
            ) : (
              <p className="mt-1 text-xs">Solo un administrador puede entregar con saldo pendiente.</p>
            )}
          </Alert>
        ) : null}
        {target === 'Cancelled' ? (
          <Field label="Motivo de la cancelación" required hint="Se informa al cliente.">
            <Textarea value={reason} onChange={(e) => setReason(e.target.value)} maxLength={300} />
          </Field>
        ) : target === 'InProgress' && (order.status === 'Ready' || order.status === 'Testing') ? (
          <Field label="Motivo del reproceso" hint="Volver a reparación requiere un nuevo control de calidad.">
            <Textarea value={reason} onChange={(e) => setReason(e.target.value)} maxLength={300} />
          </Field>
        ) : null}
        <Checkbox label="Avisar al cliente" description="Usa la plantilla del estado. Si no hay proveedor configurado, te damos el link de WhatsApp." checked={notify} onChange={(e) => setNotify(e.target.checked)} />
        {notify ? (
          <Field label="Canal">
            <Select value={channel} onChange={(e) => setChannel(e.target.value as NotificationChannel | '')}>
              <option value="">El predeterminado de la sucursal</option>
              {(Object.keys(CHANNEL) as NotificationChannel[]).map((c) => (
                <option key={c} value={c}>
                  {CHANNEL[c]}
                </option>
              ))}
            </Select>
          </Field>
        ) : null}
      </div>
    </Modal>
  )
}
