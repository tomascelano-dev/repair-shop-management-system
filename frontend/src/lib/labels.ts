import type { AdjustmentType, CashMovementType, CloudLockStatus, DocumentType, NotificationChannel, OrderStatus, OutboxStatus, PaymentMethod, Priority, PurchaseOrderStatus, QuoteItemKind, Role, SaleStatus, TaxCondition, Transfer, UnlockMethod } from '../api/types'

export const ORDER_STATUS: Record<OrderStatus, { label: string; tone: Tone }> = {
  Received: { label: 'Recibido', tone: 'slate' },
  Diagnosing: { label: 'En diagnóstico', tone: 'indigo' },
  WaitingParts: { label: 'Esperando repuesto', tone: 'amber' },
  InProgress: { label: 'En reparación', tone: 'blue' },
  Testing: { label: 'En pruebas', tone: 'violet' },
  Ready: { label: 'Listo para retirar', tone: 'emerald' },
  Delivered: { label: 'Entregado', tone: 'green' },
  Cancelled: { label: 'Cancelado', tone: 'rose' },
}

/** Workflow order used by the board and filters. */
export const ORDER_STATUS_FLOW: OrderStatus[] = ['Received', 'Diagnosing', 'WaitingParts', 'InProgress', 'Testing', 'Ready', 'Delivered', 'Cancelled']
export const OPEN_STATUSES: OrderStatus[] = ['Received', 'Diagnosing', 'WaitingParts', 'InProgress', 'Testing', 'Ready']

export const PRIORITY: Record<Priority, { label: string; tone: Tone }> = {
  Low: { label: 'Baja', tone: 'slate' },
  Normal: { label: 'Normal', tone: 'blue' },
  High: { label: 'Alta', tone: 'amber' },
  Urgent: { label: 'Urgente', tone: 'rose' },
}

export const PAYMENT_METHOD: Record<PaymentMethod, string> = {
  Cash: 'Efectivo',
  Transfer: 'Transferencia',
  Card: 'Tarjeta',
  MercadoPago: 'Mercado Pago',
  Other: 'Otro',
}

export const ROLE: Record<Role, string> = {
  Admin: 'Administrador',
  Tech: 'Técnico',
  Reception: 'Recepción',
  Cashier: 'Caja',
}

export const CHANNEL: Record<NotificationChannel, string> = {
  WhatsApp: 'WhatsApp',
  Email: 'Email',
  Sms: 'SMS',
}

export const OUTBOX_STATUS: Record<OutboxStatus, { label: string; tone: Tone }> = {
  Pending: { label: 'Pendiente', tone: 'slate' },
  Processing: { label: 'Enviando', tone: 'blue' },
  Sent: { label: 'Enviado', tone: 'green' },
  Failed: { label: 'Reintentando', tone: 'amber' },
  Cancelled: { label: 'No enviado', tone: 'rose' },
}

export const DOCUMENT_TYPE: Record<DocumentType, string> = {
  None: 'Sin documento',
  Dni: 'DNI',
  Cuit: 'CUIT',
  Cuil: 'CUIL',
  Passport: 'Pasaporte',
}

export const TAX_CONDITION: Record<TaxCondition, string> = {
  ConsumidorFinal: 'Consumidor final',
  ResponsableInscripto: 'Responsable inscripto',
  Monotributo: 'Monotributo',
  Exento: 'Exento',
}

export const UNLOCK_METHOD: Record<UnlockMethod, string> = {
  None: 'Sin bloqueo / no informado',
  Pin: 'PIN',
  Password: 'Contraseña',
  Pattern: 'Patrón',
}

export const CLOUD_LOCK: Record<CloudLockStatus, string> = {
  Unknown: 'No verificado',
  Off: 'Desactivado',
  On: 'Activado',
}

export const QUOTE_ITEM_KIND: Record<QuoteItemKind, string> = {
  Labor: 'Mano de obra',
  Part: 'Repuesto',
  Other: 'Otro',
}

export const ADJUSTMENT_TYPE: Record<AdjustmentType, string> = {
  Manual: 'Ajuste manual',
  Purchase: 'Compra',
  Sale: 'Venta',
  Consumption: 'Uso en reparación',
  Correction: 'Corrección de conteo',
  Return: 'Devolución',
  TransferOut: 'Transferencia enviada',
  TransferIn: 'Transferencia recibida',
}

export const CASH_MOVEMENT: Record<CashMovementType, string> = {
  Sale: 'Venta',
  OrderPayment: 'Cobro de orden',
  Income: 'Ingreso',
  Expense: 'Gasto',
  Withdrawal: 'Retiro',
  Refund: 'Devolución',
}

export const QUOTE_STATUS_TONE: Record<string, Tone> = {
  Draft: 'slate',
  Sent: 'blue',
  Approved: 'green',
  Rejected: 'rose',
  Expired: 'amber',
  Superseded: 'slate',
}

export const SALE_STATUS: Record<SaleStatus, { label: string; tone: Tone }> = {
  Completed: { label: 'Completada', tone: 'green' },
  PartiallyRefunded: { label: 'Devolución parcial', tone: 'amber' },
  Refunded: { label: 'Devuelta', tone: 'slate' },
  Voided: { label: 'Anulada', tone: 'rose' },
}

export const PURCHASE_STATUS: Record<PurchaseOrderStatus, { label: string; tone: Tone }> = {
  Draft: { label: 'Borrador', tone: 'slate' },
  Ordered: { label: 'Pedida', tone: 'blue' },
  PartiallyReceived: { label: 'Recibida parcial', tone: 'amber' },
  Received: { label: 'Recibida', tone: 'green' },
  Cancelled: { label: 'Cancelada', tone: 'rose' },
}

export const TRANSFER_STATUS: Record<Transfer['status'], { label: string; tone: Tone }> = {
  InTransit: { label: 'En tránsito', tone: 'blue' },
  Received: { label: 'Recibida', tone: 'green' },
  Cancelled: { label: 'Cancelada', tone: 'rose' },
}

export const INVOICE_STATUS: Record<string, { label: string; tone: Tone }> = {
  Pending: { label: 'Pendiente', tone: 'slate' },
  Authorized: { label: 'Autorizada', tone: 'green' },
  Rejected: { label: 'Rechazada', tone: 'rose' },
  Error: { label: 'Error', tone: 'amber' },
}

export const VOUCHER_TYPE: Record<string, string> = {
  FacturaA: 'Factura A',
  NotaDebitoA: 'Nota de débito A',
  NotaCreditoA: 'Nota de crédito A',
  FacturaB: 'Factura B',
  NotaDebitoB: 'Nota de débito B',
  NotaCreditoB: 'Nota de crédito B',
  FacturaC: 'Factura C',
  NotaDebitoC: 'Nota de débito C',
  NotaCreditoC: 'Nota de crédito C',
}

export const ISSUE_CATEGORIES = ['Pantalla', 'Batería', 'Pin de carga', 'Humedad', 'Software', 'Cámara', 'Audio', 'Botones', 'Placa', 'Otro']

export type Tone = 'slate' | 'blue' | 'indigo' | 'violet' | 'amber' | 'emerald' | 'green' | 'rose'

export const TONE_CLASSES: Record<Tone, string> = {
  slate: 'bg-slate-100 text-slate-700 ring-slate-200',
  blue: 'bg-blue-50 text-blue-700 ring-blue-200',
  indigo: 'bg-indigo-50 text-indigo-700 ring-indigo-200',
  violet: 'bg-violet-50 text-violet-700 ring-violet-200',
  amber: 'bg-amber-50 text-amber-800 ring-amber-200',
  emerald: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  green: 'bg-green-50 text-green-700 ring-green-200',
  rose: 'bg-rose-50 text-rose-700 ring-rose-200',
}
