import type { OrderStatus, RepairOrder } from '../../api/types'

/** What blocks a transition before calling the API (the API enforces the same rules). */
export function transitionBlocker(order: RepairOrder, target: OrderStatus): string | null {
  if (order.status === 'Diagnosing' && (target === 'InProgress' || target === 'WaitingParts') && !order.isWarrantyClaim && !order.hasApprovedQuote)
    return 'Falta el presupuesto aprobado por el cliente.'
  if (target === 'Ready' && !order.qaPassed) return 'Falta aprobar el control de calidad de salida.'
  return null
}
