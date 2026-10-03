import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { auditApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { AuditEvent } from '../../api/types'
import { Card, EmptyState, ErrorState, Input, Loading, Modal, Pagination, Select, Table, Td, Th } from '../../components/ui'
import { dateTime } from '../../lib/format'

const TAKE = 50

const ENTITY: Record<string, string> = {
  repair_order: 'Orden',
  customer: 'Cliente',
  device: 'Equipo',
  sale: 'Venta',
  cash_session: 'Caja',
  purchase_order: 'Compra',
  stock_transfer: 'Transferencia',
  user: 'Usuario',
  shop: 'Sucursal y configuración',
}

const ACTION: Record<string, string> = {
  attachment_deleted: 'Eliminó un adjunto',
  attachment_uploaded: 'Subió un adjunto',
  branch_created: 'Creó una sucursal',
  branch_deactivated: 'Desactivó una sucursal',
  cash_closed: 'Cerró la caja',
  cash_movement_added: 'Movimiento de caja',
  cash_opened: 'Abrió la caja',
  checklist_updated: 'Checklist de recepción',
  credit_note_issued: 'Emitió nota de crédito',
  customer_created: 'Creó un cliente',
  customer_deleted: 'Eliminó un cliente',
  customer_merged: 'Unificó clientes',
  customer_updated: 'Modificó un cliente',
  customers_imported: 'Importó clientes',
  device_created: 'Registró un equipo',
  device_deleted: 'Eliminó un equipo',
  device_updated: 'Modificó un equipo',
  integration_fiscal_updated: 'Configuró ARCA',
  integration_mercadopago_updated: 'Configuró Mercado Pago',
  inventory_adjusted: 'Ajustó stock',
  inventory_imported: 'Importó inventario',
  inventory_item_created: 'Creó un ítem de inventario',
  inventory_item_updated: 'Modificó un ítem de inventario',
  invoice_issued: 'Emitió factura',
  order_created: 'Creó una orden',
  order_deleted: 'Eliminó una orden',
  order_planned: 'Planificó una orden',
  order_updated: 'Modificó una orden',
  part_used: 'Usó un repuesto',
  payment_added: 'Registró un pago',
  payment_link_created: 'Generó link de pago',
  payment_link_unapplied: 'Pago online sin aplicar',
  payment_refunded: 'Devolvió un pago',
  public_note_added: 'Nota visible para el cliente',
  purchase_order_cancelled: 'Canceló una compra',
  purchase_order_created: 'Creó una compra',
  purchase_order_ordered: 'Envió una compra',
  purchase_order_received: 'Recibió mercadería',
  purchase_order_updated: 'Modificó una compra',
  qa_saved: 'Control de calidad',
  quote_approved: 'Presupuesto aprobado',
  quote_cleared: 'Quitó el precio acordado',
  quote_created: 'Creó un presupuesto',
  quote_rejected: 'Presupuesto rechazado',
  quote_sent: 'Envió un presupuesto',
  quote_set: 'Fijó el precio acordado',
  quote_updated: 'Modificó un presupuesto',
  sale_created: 'Venta',
  sale_refunded: 'Devolución de venta',
  sale_voided: 'Anuló una venta',
  shop_logo_updated: 'Cambió el logo',
  shop_settings_updated: 'Cambió la configuración',
  status_changed: 'Cambió el estado',
  supplier_created: 'Creó un proveedor',
  supplier_updated: 'Modificó un proveedor',
  tracking_token_regenerated: 'Regeneró el link de seguimiento',
  transfer_cancelled: 'Canceló una transferencia',
  transfer_created: 'Envió una transferencia',
  transfer_received: 'Recibió una transferencia',
  unlock_secret_set: 'Guardó el código de desbloqueo',
  unlock_secret_viewed: 'Vio el código de desbloqueo',
  user_invited: 'Invitó un usuario',
  user_password_reset_link: 'Generó link de contraseña',
  user_shop_access_granted: 'Dio acceso a una sucursal',
  user_shop_access_revoked: 'Quitó acceso a una sucursal',
  user_updated: 'Modificó un usuario',
  warranty_claim_created: 'Abrió un reclamo de garantía',
  warranty_claimed: 'Garantía reclamada',
}

export function AuditTab() {
  const [entityType, setEntityType] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [skip, setSkip] = useState(0)
  const [viewing, setViewing] = useState<AuditEvent | null>(null)
  const events = useQuery({
    queryKey: ['audit', entityType, from, to, skip],
    queryFn: () =>
      auditApi.search({
        entityType: entityType || undefined,
        dateFrom: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
        dateTo: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
        skip,
        take: TAKE,
      }),
    placeholderData: keepPreviousData,
  })

  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-600">Registro de acciones sensibles: quién hizo qué y cuándo. No se puede modificar.</p>
      <Card>
        <div className="flex flex-wrap gap-3">
          <Select className="w-48" value={entityType} onChange={(e) => { setEntityType(e.target.value); setSkip(0) }} aria-label="Tipo">
            <option value="">Todo</option>
            {Object.entries(ENTITY).map(([k, label]) => (
              <option key={k} value={k}>
                {label}
              </option>
            ))}
          </Select>
          <Input type="date" className="w-40" value={from} onChange={(e) => { setFrom(e.target.value); setSkip(0) }} aria-label="Desde" />
          <Input type="date" className="w-40" value={to} onChange={(e) => { setTo(e.target.value); setSkip(0) }} aria-label="Hasta" />
        </div>
      </Card>
      <Card padded={false}>
        {events.isLoading ? (
          <Loading />
        ) : events.isError ? (
          <div className="p-4">
            <ErrorState error={errorMessage(events.error)} onRetry={() => void events.refetch()} />
          </div>
        ) : (events.data?.items ?? []).length === 0 ? (
          <div className="p-4">
            <EmptyState title="Sin eventos" />
          </div>
        ) : (
          <>
            <Table>
              <thead className="bg-slate-50">
                <tr>
                  <Th>Fecha</Th>
                  <Th>Usuario</Th>
                  <Th>Acción</Th>
                  <Th>Sobre</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {events.data!.items.map((e) => (
                  <tr key={e.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setViewing(e)}>
                    <Td className="whitespace-nowrap text-xs">{dateTime(e.createdAtUtc)}</Td>
                    <Td className="text-xs">{e.actorEmail ?? 'Sistema'}</Td>
                    <Td>{ACTION[e.action] ?? e.action.replace(/_/g, ' ')}</Td>
                    <Td className="text-xs text-slate-500">{ENTITY[e.entityType] ?? e.entityType}</Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <div className="px-4 pb-3">
              <Pagination skip={skip} take={TAKE} total={events.data?.total ?? 0} onChange={setSkip} />
            </div>
          </>
        )}
      </Card>
      {viewing ? (
        <Modal open onClose={() => setViewing(null)} title={ACTION[viewing.action] ?? viewing.action} description={`${dateTime(viewing.createdAtUtc)} · ${viewing.actorEmail ?? 'Sistema'}`}>
          <dl className="mb-3 grid grid-cols-2 gap-2 text-sm">
            <dt className="text-slate-500">Entidad</dt>
            <dd>{ENTITY[viewing.entityType] ?? viewing.entityType}</dd>
            <dt className="text-slate-500">Id</dt>
            <dd className="truncate font-mono text-xs">{viewing.entityId}</dd>
          </dl>
          {viewing.dataJson ? <pre className="max-h-80 overflow-auto rounded-lg bg-slate-900 p-3 text-xs text-slate-100">{pretty(viewing.dataJson)}</pre> : null}
        </Modal>
      ) : null}
    </div>
  )
}

function pretty(json: string) {
  try {
    return JSON.stringify(JSON.parse(json), null, 2)
  } catch {
    return json
  }
}
