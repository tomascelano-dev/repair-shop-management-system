import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { settingsApi, transfersApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { Transfer } from '../../api/types'
import { Alert, Badge, Button, Card, ConfirmDialog, EmptyState, ErrorState, Field, Input, Loading, Modal, PageHeader, Pagination, Select, Table, Td, Textarea, Th } from '../../components/ui'
import { dateTime } from '../../lib/format'
import { TRANSFER_STATUS } from '../../lib/labels'
import { ItemPicker } from './ItemPicker'

const TAKE = 25

export function TransfersPage() {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState('')
  const [skip, setSkip] = useState(0)
  const [creating, setCreating] = useState(false)
  const [confirm, setConfirm] = useState<{ transfer: Transfer; action: 'receive' | 'cancel' } | null>(null)
  const branches = useQuery({ queryKey: ['branches'], queryFn: settingsApi.branches })
  const transfers = useQuery({
    queryKey: ['transfers', status, skip],
    queryFn: () => transfersApi.list({ status: status || undefined, skip, take: TAKE }),
    placeholderData: keepPreviousData,
  })
  const others = (branches.data ?? []).filter((b) => !b.isCurrent && b.isActive)

  const act = useMutation({
    mutationFn: ({ transfer, action }: { transfer: Transfer; action: 'receive' | 'cancel' }) => (action === 'receive' ? transfersApi.receive(transfer.id) : transfersApi.cancel(transfer.id)),
    onSuccess: (_, v) => {
      toast.success(v.action === 'receive' ? 'Transferencia recibida: el stock ya está disponible' : 'Transferencia cancelada: el stock volvió al origen')
      setConfirm(null)
      void queryClient.invalidateQueries({ queryKey: ['transfers'] })
      void queryClient.invalidateQueries({ queryKey: ['inventory'] })
    },
    onError: (err) => toast.error('No se pudo completar', { description: errorMessage(err) }),
  })

  return (
    <div>
      <PageHeader
        title="Transferencias"
        subtitle="Mové repuestos entre sucursales. El stock sale al enviar y entra cuando la otra sucursal confirma la recepción."
        actions={
          <Button variant="primary" disabled={others.length === 0} onClick={() => setCreating(true)}>
            + Nueva transferencia
          </Button>
        }
      />
      {branches.isSuccess && others.length === 0 ? (
        <Alert tone="slate" className="mb-4">
          Necesitás al menos otra sucursal activa para transferir stock. Un administrador puede crearla en Configuración → Sucursales.
        </Alert>
      ) : null}

      <div className="mb-4">
        <Select className="w-56" value={status} onChange={(e) => { setStatus(e.target.value); setSkip(0) }} aria-label="Estado">
          <option value="">Todos los estados</option>
          {(Object.keys(TRANSFER_STATUS) as Transfer['status'][]).map((s) => (
            <option key={s} value={s}>
              {TRANSFER_STATUS[s].label}
            </option>
          ))}
        </Select>
      </div>

      <Card padded={false}>
        {transfers.isLoading ? (
          <Loading />
        ) : transfers.isError ? (
          <div className="p-4">
            <ErrorState error={errorMessage(transfers.error)} onRetry={() => void transfers.refetch()} />
          </div>
        ) : (transfers.data?.items ?? []).length === 0 ? (
          <div className="p-4">
            <EmptyState title="No hay transferencias" />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Transferencia</Th>
                  <Th>Origen → destino</Th>
                  <Th>Ítems</Th>
                  <Th>Fecha</Th>
                  <Th>Estado</Th>
                  <Th />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {transfers.data!.items.map((t) => (
                  <tr key={t.id} className="align-top">
                    <Td className="font-medium">
                      {t.code}
                      <span className="block text-xs font-normal text-slate-500">{t.isIncoming ? 'Entrante' : 'Saliente'}</span>
                    </Td>
                    <Td>
                      {t.fromShopName} → {t.toShopName}
                    </Td>
                    <Td className="text-xs text-slate-600">
                      {t.lines.map((l) => (
                        <span key={l.id} className="block">
                          {l.quantity} × {l.name}
                        </span>
                      ))}
                      {t.notes ? <span className="mt-1 block italic text-slate-500">{t.notes}</span> : null}
                    </Td>
                    <Td className="whitespace-nowrap">
                      {dateTime(t.createdAtUtc)}
                      {t.receivedAtUtc ? <span className="block text-xs text-slate-500">recibida {dateTime(t.receivedAtUtc)}</span> : null}
                    </Td>
                    <Td>
                      <Badge tone={TRANSFER_STATUS[t.status].tone}>{TRANSFER_STATUS[t.status].label}</Badge>
                    </Td>
                    <Td align="right">
                      {t.status === 'InTransit' ? (
                        <span className="flex justify-end gap-1">
                          {t.isIncoming ? (
                            <Button size="sm" variant="primary" onClick={() => setConfirm({ transfer: t, action: 'receive' })}>
                              Recibir
                            </Button>
                          ) : null}
                          {!t.isIncoming ? (
                            <Button size="sm" variant="ghost" onClick={() => setConfirm({ transfer: t, action: 'cancel' })}>
                              Cancelar
                            </Button>
                          ) : null}
                        </span>
                      ) : null}
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <div className="px-4 pb-3">
              <Pagination skip={skip} take={TAKE} total={transfers.data?.total ?? 0} onChange={setSkip} />
            </div>
          </>
        )}
      </Card>

      {creating ? <NewTransferDialog branches={others} onClose={() => setCreating(false)} /> : null}
      <ConfirmDialog
        open={!!confirm}
        title={confirm?.action === 'receive' ? `¿Recibir ${confirm.transfer.code}?` : `¿Cancelar ${confirm?.transfer.code ?? ''}?`}
        message={confirm?.action === 'receive' ? 'Confirmá que llegó todo: los ítems se suman al stock de esta sucursal (se crean si no existen).' : 'Los ítems vuelven al stock de la sucursal de origen.'}
        confirmLabel={confirm?.action === 'receive' ? 'Recibir' : 'Cancelar transferencia'}
        danger={confirm?.action === 'cancel'}
        loading={act.isPending}
        onConfirm={() => confirm && act.mutate(confirm)}
        onClose={() => setConfirm(null)}
      />
    </div>
  )
}

function NewTransferDialog({ branches, onClose }: { branches: { id: string; name: string }[]; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [toShopId, setToShopId] = useState(branches[0]?.id ?? '')
  const [lines, setLines] = useState<{ id: string; name: string; available: number; quantity: number }[]>([])
  const [notes, setNotes] = useState('')
  const create = useMutation({
    mutationFn: () => transfersApi.create({ toShopId, lines: lines.map((l) => ({ inventoryItemId: l.id, quantity: l.quantity })), notes: notes.trim() || null }),
    onSuccess: (t) => {
      toast.success(`Transferencia ${t.code} enviada`)
      void queryClient.invalidateQueries({ queryKey: ['transfers'] })
      void queryClient.invalidateQueries({ queryKey: ['inventory'] })
      onClose()
    },
    onError: (err) => toast.error('No se pudo crear la transferencia', { description: errorMessage(err) }),
  })
  const invalid = lines.some((l) => l.quantity < 1 || l.quantity > l.available)
  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title="Nueva transferencia"
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={create.isPending} disabled={!toShopId || lines.length === 0 || invalid} onClick={() => create.mutate()}>
            Enviar
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Sucursal de destino">
          <Select value={toShopId} onChange={(e) => setToShopId(e.target.value)}>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </Select>
        </Field>
        <ItemPicker
          placeholder="Agregar ítem a transferir…"
          onSelect={(item) => {
            if (!item.trackStock) {
              toast.error(`“${item.name}” no controla stock`)
              return
            }
            setLines((ls) => (ls.some((l) => l.id === item.id) ? ls : [...ls, { id: item.id, name: `${item.name} (${item.sku})`, available: item.availableQuantity, quantity: 1 }]))
          }}
        />
        {lines.length > 0 ? (
          <Table>
            <thead>
              <tr>
                <Th>Ítem</Th>
                <Th align="right">Disponible</Th>
                <Th align="right">Enviar</Th>
                <Th />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {lines.map((l, idx) => (
                <tr key={l.id}>
                  <Td>{l.name}</Td>
                  <Td align="right">{l.available}</Td>
                  <Td align="right">
                    <Input
                      type="number"
                      min={1}
                      max={l.available}
                      className="ml-auto h-8 w-20 text-right"
                      value={l.quantity}
                      aria-invalid={l.quantity > l.available}
                      onChange={(e) => setLines((ls) => ls.map((x, i) => (i === idx ? { ...x, quantity: Math.max(1, Number(e.target.value) || 1) } : x)))}
                      aria-label="Cantidad a enviar"
                    />
                  </Td>
                  <Td align="right">
                    <Button size="sm" variant="ghost" onClick={() => setLines((ls) => ls.filter((_, i) => i !== idx))} aria-label="Quitar">
                      ✕
                    </Button>
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        ) : null}
        {invalid ? <Alert tone="rose">Hay cantidades mayores al stock disponible.</Alert> : null}
        <Field label="Notas">
          <Textarea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Quién lo lleva, remito…" />
        </Field>
      </div>
    </Modal>
  )
}
