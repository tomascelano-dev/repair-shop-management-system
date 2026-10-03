// DTOs of the RepairShop API (camelCase JSON, enums travel as their names).

export type Guid = string
export type IsoDate = string // UTC ISO-8601
export type DateOnly = string // yyyy-MM-dd

export interface ApiResponse<T> {
  data: T
}

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
  traceId?: string
  correlationId?: string
  retryAfterSeconds?: number
  /** 402 answers: 'subscription_inactive' or 'plan_upgrade_required' (+ module). */
  code?: string
  module?: string
}

export interface Page<T> {
  items: T[]
  total: number
}

// ===== Enums =====
export type Role = 'Admin' | 'Tech' | 'Reception' | 'Cashier'
export type Permission = 'orders.manage' | 'orders.work' | 'sales' | 'inventory.manage' | 'reports' | 'admin'

export type OrderStatus = 'Received' | 'Diagnosing' | 'InProgress' | 'Ready' | 'Delivered' | 'Cancelled' | 'WaitingParts' | 'Testing'
export type Priority = 'Low' | 'Normal' | 'High' | 'Urgent'
export type PaymentMethod = 'Cash' | 'Transfer' | 'Card' | 'MercadoPago' | 'Other'
export type NotificationChannel = 'WhatsApp' | 'Email' | 'Sms'
export type UnlockMethod = 'None' | 'Pin' | 'Password' | 'Pattern'
export type CloudLockStatus = 'Unknown' | 'Off' | 'On'
export type QuoteItemKind = 'Labor' | 'Part' | 'Other'
export type DocumentType = 'None' | 'Dni' | 'Cuit' | 'Cuil' | 'Passport'
export type TaxCondition = 'ConsumidorFinal' | 'ResponsableInscripto' | 'Monotributo' | 'Exento'
export type AdjustmentType = 'Manual' | 'Purchase' | 'Sale' | 'Consumption' | 'Correction' | 'Return' | 'TransferOut' | 'TransferIn'
export type CashMovementType = 'Sale' | 'OrderPayment' | 'Income' | 'Expense' | 'Withdrawal' | 'Refund'
export type OutboxStatus = 'Pending' | 'Processing' | 'Sent' | 'Failed' | 'Cancelled'
export type FiscalEnvironment = 'Homologacion' | 'Produccion'
export type SignatureKind = 'Reception' | 'Delivery'

// ===== Auth / users =====
export interface UserResponse {
  id: Guid
  shopId: Guid
  email: string
  displayName: string
  role: Role
  shopName?: string | null
  organizationId?: Guid | null
  emailVerified?: boolean
}

export interface ShopAccess {
  shopId: Guid
  shopName: string
  role: Role
  isHome: boolean
}

export interface LoginResponse {
  accessToken: string
  user: UserResponse
  accessTokenExpiresAtUtc: IsoDate
  shops: ShopAccess[]
  permissions: Permission[]
}

export interface MeResponse {
  user: UserResponse
  shops: ShopAccess[]
  permissions: Permission[]
}

export interface TokenInfo {
  email: string
  displayName: string
  shopName: string
  purpose: string
  expiresAtUtc: IsoDate
}

export interface UserAdmin {
  id: Guid
  email: string
  displayName: string
  role: Role
  isActive: boolean
  isHomeShop: boolean
  hasPendingInvitation: boolean
  lastLoginAtUtc?: IsoDate | null
  createdAtUtc: IsoDate
  shops: ShopAccess[]
}

export interface UserLink {
  user: UserAdmin
  url: string
  expiresAtUtc: IsoDate
}

// ===== Settings =====
export interface ShopSettings {
  id: Guid
  organizationId: Guid
  name: string
  phone?: string | null
  addressLine?: string | null
  city?: string | null
  country?: string | null
  legalName?: string | null
  taxId?: string | null
  taxCondition: TaxCondition
  email?: string | null
  logoFileId?: Guid | null
  logoUrl?: string | null
  defaultCurrency: string
  reportingCurrency: string
  phoneCountryCode: string
  timeZone: string
  defaultWarrantyDays: number
  quoteValidityDays: number
  receptionTerms?: string | null
  warrantyTerms?: string | null
  pickupHours?: string | null
  googleReviewUrl?: string | null
  readyReminderDays: string
  staleOrderDays: number
  notificationsEnabled: boolean
  defaultNotificationChannel: NotificationChannel
  sendFeedbackSurvey: boolean
  requireOpenCashSession: boolean
  isActive: boolean
}

export type UpdateShopSettings = Omit<ShopSettings, 'id' | 'organizationId' | 'logoFileId' | 'logoUrl' | 'isActive'>

export interface IntegrationsStatus {
  mercadoPagoEnabled: boolean
  mercadoPagoAccessTokenConfigured: boolean
  mercadoPagoWebhookSecretConfigured: boolean
  mercadoPagoWebhookUrl: string
  fiscalEnabled: boolean
  fiscalEnvironment: FiscalEnvironment
  fiscalPointOfSale: number
  fiscalCertificateConfigured: boolean
  storageProvider: string
  notificationChannels: Record<string, boolean>
  aiConfigured: boolean
}

export interface Branch {
  id: Guid
  name: string
  city?: string | null
  addressLine?: string | null
  isActive: boolean
  isCurrent: boolean
  myRole?: Role | null
}

export interface MessageTemplate {
  id: Guid
  shopId: Guid
  key: string
  title: string
  body: string
  isActive: boolean
  createdAtUtc: IsoDate
  updatedAtUtc: IsoDate
}

export interface TemplateToken {
  token: string
  description: string
}

// ===== CRM =====
export interface Customer {
  id: Guid
  shopId: Guid
  fullName: string
  phone: string
  notes?: string | null
  createdAtUtc: IsoDate
  email?: string | null
  documentType: DocumentType
  documentNumber?: string | null
  taxCondition: TaxCondition
  address?: string | null
  tags?: string | null
  notificationsOptIn: boolean
  marketingOptIn: boolean
  updatedAtUtc?: IsoDate | null
  whatsAppNumber?: string | null
}

export interface CustomerInput {
  fullName: string
  phone: string
  notes?: string | null
  email?: string | null
  documentType?: DocumentType
  documentNumber?: string | null
  taxCondition?: TaxCondition
  address?: string | null
  tags?: string | null
  notificationsOptIn?: boolean
  marketingOptIn?: boolean
}

export interface DuplicateCustomer {
  id: Guid
  fullName: string
  phone: string
  email?: string | null
  createdAtUtc: IsoDate
  devices: number
  orders: number
}

export interface CurrencyAmount {
  currency: string
  amount: number
}

export interface CustomerOrderItem {
  id: Guid
  code: string
  status: OrderStatus
  deviceLabel: string
  issueDescription: string
  total: number
  balance: number
  currency: string
  createdAtUtc: IsoDate
  deliveredAtUtc?: IsoDate | null
}

export interface CustomerSaleItem {
  id: Guid
  code: string
  status: string
  total: number
  currency: string
  createdAtUtc: IsoDate
}

export interface CustomerSummary {
  customer: Customer
  devices: Device[]
  orders: CustomerOrderItem[]
  sales: CustomerSaleItem[]
  totalSpent: CurrencyAmount[]
  balanceDue: CurrencyAmount[]
  openOrders: number
  lastVisitAtUtc?: IsoDate | null
  averageFeedbackScore?: number | null
}

export interface Device {
  id: Guid
  shopId: Guid
  customerId: Guid
  brand: string
  model: string
  label?: string | null
  serialNumber?: string | null
  notes?: string | null
  createdAtUtc: IsoDate
  imei?: string | null
  updatedAtUtc?: IsoDate | null
  customerName?: string | null
}

export interface DeviceInput {
  customerId?: Guid
  brand: string
  model: string
  label?: string | null
  serialNumber?: string | null
  notes?: string | null
  imei?: string | null
}

export interface ImportResult {
  entity: string
  dryRun: boolean
  totalRows: number
  created: number
  updated: number
  skipped: number
  errors: { row: number; message: string }[]
  preview: Record<string, string>[]
  detectedColumns: string[]
}

// ===== Orders =====
export interface RepairOrder {
  id: Guid
  shopId: Guid
  customerId: Guid
  deviceId: Guid
  issueDescription: string
  notes?: string | null
  status: OrderStatus
  quoteAmount?: number | null
  quoteCurrency?: string | null
  quoteUpdatedByUserId?: Guid | null
  quoteUpdatedAtUtc?: IsoDate | null
  createdAtUtc: IsoDate
  updatedAtUtc: IsoDate
  orderNumber: number
  code: string
  statusLabel: string
  issueCategory?: string | null
  priority: Priority
  assignedTechnicianId?: Guid | null
  assignedTechnicianName?: string | null
  promisedAtUtc?: IsoDate | null
  isOverdue: boolean
  customerName: string
  customerPhone: string
  deviceLabel: string
  deviceImei?: string | null
  currency: string
  totalAmount: number
  paidAmount: number
  balanceDue: number
  extraCharges: number
  hasApprovedQuote: boolean
  qaPassed: boolean
  warrantyDays?: number | null
  warrantyExpiresAtUtc?: IsoDate | null
  underWarranty: boolean
  isWarrantyClaim: boolean
  warrantyOfOrderId?: Guid | null
  lastStatusChangeAtUtc: IsoDate
  readyAtUtc?: IsoDate | null
  deliveredAtUtc?: IsoDate | null
  cancelledAtUtc?: IsoDate | null
  cancellationReason?: string | null
  unlockMethod: UnlockMethod
  hasUnlockSecret: boolean
  hasReceptionSignature: boolean
  receptionSignedByName?: string | null
  hasDeliverySignature: boolean
  deliverySignedByName?: string | null
  trackingUrl: string
  allowedNextStatuses: OrderStatus[]
  photosCount: number
}

export interface CreateOrderInput {
  customerId: Guid
  deviceId: Guid
  issueDescription: string
  notes?: string | null
  issueCategory?: string | null
  priority?: Priority
  assignedTechnicianId?: Guid | null
  promisedAtUtc?: IsoDate | null
  warrantyDays?: number | null
  sendReceivedMessage?: boolean
}

export interface OrderSearch {
  q?: string
  status?: OrderStatus
  statuses?: string
  technicianId?: Guid
  mine?: boolean
  customerId?: Guid
  deviceId?: Guid
  onlyOpen?: boolean
  onlyOverdue?: boolean
  priority?: Priority
  dateFrom?: string
  dateTo?: string
  sortBy?: string
  sortDir?: 'asc' | 'desc'
  skip?: number
  take?: number
}

export interface OrderCard {
  id: Guid
  code: string
  status: OrderStatus
  customerName: string
  deviceLabel: string
  issueDescription: string
  priority: Priority
  assignedTechnicianId?: Guid | null
  assignedTechnicianName?: string | null
  promisedAtUtc?: IsoDate | null
  isOverdue: boolean
  isStale: boolean
  ageDays: number
  lastStatusChangeAtUtc: IsoDate
  isWarrantyClaim: boolean
  balanceDue: number
  currency: string
}

export interface OrderBoard {
  columns: { status: OrderStatus; label: string; count: number; items: OrderCard[] }[]
}

export interface ChangeStatusInput {
  status: OrderStatus
  enqueueOutbox?: boolean
  channel?: NotificationChannel | null
  reason?: string | null
  forceUnpaidDelivery?: boolean
}

export interface ChangeStatusResult {
  orderId: Guid
  fromStatus: OrderStatus
  toStatus: OrderStatus
  suggestedMessage: string
  outboxItemId?: Guid | null
  whatsAppUrl?: string | null
}

export interface StatusHistoryItem {
  id: Guid
  fromStatus: OrderStatus
  toStatus: OrderStatus
  changedByUserId: Guid
  changedAtUtc: IsoDate
  fromLabel?: string | null
  toLabel?: string | null
  changedByName?: string | null
  reason?: string | null
}

export interface OrderNote {
  id: Guid
  body: string
  createdByUserId: Guid
  createdAtUtc: IsoDate
  isPublic: boolean
  createdByName?: string | null
}

export interface OrderAttachment {
  id: Guid
  url?: string | null
  label?: string | null
  createdByUserId: Guid
  createdAtUtc: IsoDate
  kind: 'Link' | 'Photo' | 'Document'
  fileId?: Guid | null
  fileName?: string | null
  contentType?: string | null
  sizeBytes?: number | null
}

export interface OrderPayment {
  id: Guid
  repairOrderId: Guid
  amount: number
  currency: string
  method: PaymentMethod
  reference?: string | null
  createdByUserId: Guid
  createdAtUtc: IsoDate
  type: 'Payment' | 'Refund'
  isDeposit: boolean
  refundOfPaymentId?: Guid | null
  refundedAmount: number
}

export interface OrderFinancials {
  currency: string
  agreedPrice: number
  extraCharges: number
  total: number
  paid: number
  balanceDue: number
  hasAgreedPrice: boolean
}

export interface ReceptionChecklist {
  id?: Guid
  repairOrderId?: Guid
  screenOk: boolean
  camerasOk: boolean
  speakersOk: boolean
  microphoneOk: boolean
  buttonsOk: boolean
  faceIdOk: boolean
  fingerprintOk: boolean
  cloudLock: CloudLockStatus
  batteryPercent?: number | null
  cosmeticNotes?: string | null
  updatedByUserId?: Guid
  updatedAtUtc?: IsoDate
}

export interface QaChecklist {
  id?: Guid
  repairOrderId?: Guid
  powersOn?: boolean | null
  screenOk?: boolean | null
  touchOk?: boolean | null
  camerasOk?: boolean | null
  audioOk?: boolean | null
  microphoneOk?: boolean | null
  buttonsOk?: boolean | null
  chargingOk?: boolean | null
  connectivityOk?: boolean | null
  biometricsOk?: boolean | null
  batteryHealthPercent?: number | null
  notes?: string | null
  passed?: boolean
  checkedByUserId?: Guid
  checkedAtUtc?: IsoDate
}

export interface PartUsage {
  id: Guid
  repairOrderId: Guid
  inventoryItemId: Guid
  quantityUsed: number
  unitPrice?: number | null
  unitPriceCurrency?: string | null
  createdByUserId: Guid
  createdAtUtc: IsoDate
  itemName?: string | null
  itemSku?: string | null
  chargedToCustomer: boolean
  unitCost?: number | null
}

export interface Reservation {
  id: Guid
  inventoryItemId: Guid
  itemName: string
  repairOrderId: Guid
  quantity: number
  consumedQuantity: number
  status: string
  createdAtUtc: IsoDate
}

export interface MessagePreview {
  templateKey: string
  title: string
  body: string
  whatsAppUrl?: string | null
  recipient?: string | null
}

export interface NotificationResult {
  templateKey: string
  title: string
  body: string
  whatsAppUrl?: string | null
  outboxItemId?: Guid | null
  skippedReason?: string | null
}

export interface OutboxMessage {
  id: Guid
  channel: NotificationChannel
  recipient: string
  title: string
  body: string
  status: OutboxStatus
  attemptCount: number
  nextAttemptAtUtc?: IsoDate | null
  lastError?: string | null
  templateKey?: string | null
  relatedEntityType?: string | null
  relatedEntityId?: Guid | null
  provider?: string | null
  sentAtUtc?: IsoDate | null
  createdAtUtc: IsoDate
}

export interface UnlockSecret {
  method: UnlockMethod
  value?: string | null
}

// ===== Quotes =====
export interface QuoteItemInput {
  kind: QuoteItemKind
  description: string
  quantity: number
  unitPrice: number
  inventoryItemId?: Guid | null
  warrantyDays?: number | null
}

export interface QuoteInput {
  currency: string
  items: QuoteItemInput[]
  discountAmount?: number
  warrantyDays?: number | null
  notes?: string | null
}

export interface QuoteItem {
  id: Guid
  position: number
  kind: QuoteItemKind
  description: string
  quantity: number
  unitPrice: number
  lineTotal: number
  inventoryItemId?: Guid | null
  warrantyDays?: number | null
}

export type QuoteStatus = 'Draft' | 'Sent' | 'Approved' | 'Rejected' | 'Expired' | 'Superseded'

export interface Quote {
  id: Guid
  repairOrderId: Guid
  version: number
  status: QuoteStatus
  statusLabel: string
  currency: string
  items: QuoteItem[]
  subtotal: number
  discountAmount: number
  total: number
  validUntilUtc?: IsoDate | null
  warrantyDays?: number | null
  notes?: string | null
  createdAtUtc: IsoDate
  sentAtUtc?: IsoDate | null
  decidedAtUtc?: IsoDate | null
  decisionSource?: string | null
  decisionNote?: string | null
  decidedByUserId?: Guid | null
}

export interface QuoteActionResult {
  quote: Quote
  suggestedMessage?: string | null
  whatsAppUrl?: string | null
  outboxItemId?: Guid | null
  warnings: string[]
}

// ===== Suggestions =====
export interface SimilarOrder {
  id: Guid
  code: string
  device: string
  issueDescription: string
  category?: string | null
  status: string
  total?: number | null
  currency?: string | null
  hoursToReady?: number | null
  similarity: number
  items: string[]
}

export interface SuggestedItem {
  kind: QuoteItemKind
  description: string
  frequency: number
  medianUnitPrice?: number | null
  currency?: string | null
  inventoryItemId?: Guid | null
}

export interface RepairSuggestion {
  similarOrders: SimilarOrder[]
  suggestedItems: SuggestedItem[]
  priceRange?: { min: number; median: number; max: number; currency: string; samples: number } | null
  compatibleParts: InventoryItem[]
  aiAvailable: boolean
  ai?: { diagnosisHypotheses: string[]; items: SuggestedItem[]; customerMessage?: string | null; model: string } | null
  aiError?: string | null
}

// ===== Inventory =====
export interface InventoryItem {
  id: Guid
  shopId: Guid
  sku: string
  name: string
  quantityOnHand: number
  unitCost?: number | null
  unitCostCurrency?: string | null
  isActive: boolean
  createdAtUtc: IsoDate
  updatedAtUtc: IsoDate
  category?: string | null
  barcode?: string | null
  minStock: number
  trackStock: boolean
  isSellable: boolean
  salePrice?: number | null
  salePriceCurrency?: string | null
  warrantyDays?: number | null
  location?: string | null
  reservedQuantity: number
  availableQuantity: number
  isLowStock: boolean
}

export interface InventoryItemInput {
  sku?: string
  name: string
  initialQuantity?: number
  unitCost?: number | null
  unitCostCurrency?: string | null
  isActive?: boolean
  category?: string | null
  barcode?: string | null
  minStock?: number
  trackStock?: boolean
  isSellable?: boolean
  salePrice?: number | null
  salePriceCurrency?: string | null
  warrantyDays?: number | null
  location?: string | null
}

export interface InventoryAdjustment {
  id: Guid
  inventoryItemId: Guid
  type: AdjustmentType
  deltaQuantity: number
  reason?: string | null
  repairOrderId?: Guid | null
  createdByUserId: Guid
  createdAtUtc: IsoDate
  referenceType?: string | null
  referenceId?: Guid | null
}

export interface Compatibility {
  id: Guid
  brand: string
  model: string
}

export interface Supplier {
  id: Guid
  name: string
  contactName?: string | null
  phone?: string | null
  email?: string | null
  taxId?: string | null
  notes?: string | null
  isActive: boolean
  createdAtUtc: IsoDate
}

export type PurchaseOrderStatus = 'Draft' | 'Ordered' | 'PartiallyReceived' | 'Received' | 'Cancelled'

export interface PurchaseOrder {
  id: Guid
  number: number
  code: string
  supplierId: Guid
  supplierName?: string | null
  status: PurchaseOrderStatus
  currency: string
  total: number
  notes?: string | null
  expectedAtUtc?: IsoDate | null
  createdAtUtc: IsoDate
  orderedAtUtc?: IsoDate | null
  receivedAtUtc?: IsoDate | null
  lines: { id: Guid; inventoryItemId: Guid; description: string; quantity: number; unitCost: number; receivedQuantity: number; lineTotal: number }[]
}

export interface Transfer {
  id: Guid
  code: string
  fromShopId: Guid
  fromShopName?: string | null
  toShopId: Guid
  toShopName?: string | null
  status: 'InTransit' | 'Received' | 'Cancelled'
  notes?: string | null
  createdAtUtc: IsoDate
  receivedAtUtc?: IsoDate | null
  isIncoming: boolean
  lines: { id: Guid; sourceItemId: Guid; sku: string; name: string; quantity: number }[]
}

// ===== Sales / POS =====
export interface PosCatalogItem {
  id: Guid
  sku: string
  barcode?: string | null
  name: string
  category?: string | null
  salePrice?: number | null
  currency?: string | null
  trackStock: boolean
  available: number
  warrantyDays?: number | null
}

export interface SaleLineInput {
  inventoryItemId?: Guid | null
  description?: string | null
  quantity: number
  unitPrice?: number | null
  discountAmount?: number
  discountPercent?: number | null
}

export interface SalePaymentInput {
  method: PaymentMethod
  amount: number
  reference?: string | null
}

export interface CreateSaleInput {
  lines: SaleLineInput[]
  payments: SalePaymentInput[]
  customerId?: Guid | null
  currency?: string | null
  discountAmount?: number
  discountPercent?: number | null
  notes?: string | null
}

export type SaleStatus = 'Completed' | 'Voided' | 'PartiallyRefunded' | 'Refunded'

export interface Sale {
  id: Guid
  number: number
  code: string
  status: SaleStatus
  customerId?: Guid | null
  customerName?: string | null
  currency: string
  subtotal: number
  discountAmount: number
  total: number
  paidAmount: number
  changeAmount: number
  refundedAmount: number
  cashSessionId?: Guid | null
  notes?: string | null
  createdByUserId: Guid
  createdByName?: string | null
  createdAtUtc: IsoDate
  voidReason?: string | null
  lines: { id: Guid; position: number; inventoryItemId?: Guid | null; sku: string; description: string; quantity: number; unitPrice: number; discountAmount: number; lineTotal: number; refundedQuantity: number; warrantyDays?: number | null }[]
  payments: { id: Guid; method: PaymentMethod; amount: number; reference?: string | null }[]
  refunds: { id: Guid; amount: number; method: PaymentMethod; restocked: boolean; reason?: string | null; createdAtUtc: IsoDate }[]
}

// ===== Cash =====
export interface CashSummaryLine {
  currency: string
  method: PaymentMethod
  inflows: number
  outflows: number
  net: number
  expected: number
  declared?: number | null
  difference?: number | null
}

export interface CashMovement {
  id: Guid
  type: CashMovementType
  method: PaymentMethod
  amount: number
  signedAmount: number
  currency: string
  description: string
  category?: string | null
  relatedEntityType?: string | null
  relatedEntityId?: Guid | null
  createdByUserId: Guid
  createdByName?: string | null
  createdAtUtc: IsoDate
}

export interface CashSession {
  id: Guid
  number: number
  status: 'Open' | 'Closed'
  currency: string
  openingCash: number
  openedByUserId: Guid
  openedByName?: string | null
  openedAtUtc: IsoDate
  openingNotes?: string | null
  closedByUserId?: Guid | null
  closedByName?: string | null
  closedAtUtc?: IsoDate | null
  expectedCash?: number | null
  countedCash?: number | null
  difference?: number | null
  closingNotes?: string | null
  summary: CashSummaryLine[]
  movements?: CashMovement[] | null
}

// ===== Payments / fiscal / currency =====
export interface PaymentLink {
  id: Guid
  provider: string
  url: string
  amount: number
  currency: string
  status: string
  expiresAtUtc: IsoDate
  createdAtUtc: IsoDate
  paidAtUtc?: IsoDate | null
}

export interface FiscalInvoice {
  id: Guid
  sourceType: string
  sourceId: Guid
  voucherType: string
  letter: string
  pointOfSale: number
  number?: number | null
  code: string
  issueDateUtc: IsoDate
  receiverName: string
  receiverDocumentNumber?: string | null
  netAmount: number
  vatAmount: number
  total: number
  status: string
  cae?: string | null
  caeDueDate?: DateOnly | null
  resultMessage?: string | null
  associatedInvoiceId?: Guid | null
}

export interface IssueInvoiceInput {
  documentType?: DocumentType | null
  documentNumber?: string | null
  receiverName?: string | null
  receiverTaxCondition?: TaxCondition | null
}

export interface ExchangeRate {
  id: Guid
  date: DateOnly
  baseCurrency: string
  quoteCurrency: string
  source: string
  rate: number
  updatedAtUtc: IsoDate
}

// ===== Dashboard / reports =====
export interface RevenueSummary {
  byCurrency: CurrencyAmount[]
  converted?: number | null
  currency: string
}

export interface DashboardSummary {
  shopId: Guid
  totalOrders: number
  openOrders: number
  readyOrders: number
  deliveredOrders: number
  cancelledOrders: number
  totalPaymentsAmount: number
  paymentsCurrency?: string | null
  generatedAtUtc: IsoDate
  statusCounts?: Record<string, number> | null
  overdueOrders: number
  staleOrders: number
  readyNotPickedUp: number
  quotesPendingDecision: number
  lowStockItems: number
  myOpenOrders: number
  today?: RevenueSummary | null
  month?: RevenueSummary | null
  technicians?: { userId: Guid; name: string; openOrders: number; overdueOrders: number }[] | null
  cashSessionOpen: boolean
  reportingCurrency?: string | null
  missingRates?: string[] | null
  totalPaymentsByCurrency?: CurrencyAmount[] | null
}

export interface RevenuePoint {
  date: DateOnly
  converted?: number | null
  byCurrency: CurrencyAmount[]
}

export interface ConsolidatedDashboard {
  reportingCurrency: string
  branches: { shopId: Guid; shopName: string; openOrders: number; readyOrders: number; overdueOrders: number; lowStockItems: number; month: RevenueSummary }[]
  monthTotalConverted?: number | null
  missingRates: string[]
}

export interface ReportPeriod {
  fromUtc: IsoDate
  toUtc: IsoDate
  reportingCurrency: string
  rateSources: string[]
  missingRates: string[]
}

export interface RevenueReport {
  period: ReportPeriod
  rows: { period: string; currency: string; method: string; orders: number; sales: number; refunds: number; net: number; netConverted?: number | null }[]
  totalsByCurrency: CurrencyAmount[]
  totalConverted?: number | null
  byMethod: CurrencyAmount[]
}

export interface MarginReport {
  period: ReportPeriod
  rows: { kind: string; id: Guid; code: string; description: string; dateUtc: IsoDate; currency: string; revenue: number; cost: number; margin: number; marginPercent?: number | null }[]
  revenueByCurrency: CurrencyAmount[]
  marginByCurrency: CurrencyAmount[]
  marginConverted?: number | null
}

export interface RepairTimesReport {
  period: ReportPeriod
  ordersAnalyzed: number
  averageHoursToReady?: number | null
  averageHoursToDelivery?: number | null
  byStatus: { status: string; label: string; orders: number; averageHours: number; medianHours: number }[]
  bottleneck?: string | null
}

export interface QuoteStatsReport {
  period: ReportPeriod
  total: number
  approved: number
  rejected: number
  expired: number
  pending: number
  approvalRate?: number | null
  averageApprovedTotal: CurrencyAmount[]
}

export interface TopRow {
  key: string
  orders: number
  averageTicket?: number | null
  currency?: string | null
}

export interface TopIssuesReport {
  period: ReportPeriod
  byCategory: TopRow[]
  byModel: TopRow[]
  byBrand: TopRow[]
}

export interface TechniciansReport {
  period: ReportPeriod
  rows: { userId: Guid; name: string; assigned: number; delivered: number; averageHoursToReady?: number | null; revenue: CurrencyAmount[]; warrantyClaims: number; reentryRate?: number | null }[]
}

export interface WarrantyReport {
  period: ReportPeriod
  deliveredOrders: number
  warrantyClaims: number
  reentryRate?: number | null
  byPart: { inventoryItemId: Guid; sku: string; name: string; uses: number; claims: number; claimRate?: number | null }[]
}

export interface FeedbackReport {
  period: ReportPeriod
  responses: number
  averageScore?: number | null
  distribution: Record<string, number>
  latest: { orderCode: string; score: number; comment?: string | null; createdAtUtc: IsoDate }[]
}

export interface InventoryReport {
  rows: { id: Guid; sku: string; name: string; category?: string | null; onHand: number; reserved: number; minStock: number; unitCost?: number | null; currency?: string | null; value?: number | null; lowStock: boolean }[]
  totalValue: CurrencyAmount[]
  lowStockCount: number
}

export interface AuditEvent {
  id: Guid
  entityType: string
  entityId: Guid
  action: string
  actorUserId?: Guid | null
  actorEmail?: string | null
  dataJson?: string | null
  createdAtUtc: IsoDate
}

// ===== Public portal =====
export interface PublicTracking {
  shop: { name: string; phone?: string | null; whatsAppUrl?: string | null; address?: string | null; city?: string | null; pickupHours?: string | null; logoUrl?: string | null }
  order: {
    code: string
    status: OrderStatus
    statusLabel: string
    customerFirstName: string
    device: string
    issueDescription: string
    createdAtUtc: IsoDate
    promisedAtUtc?: IsoDate | null
    readyAtUtc?: IsoDate | null
    deliveredAtUtc?: IsoDate | null
    isWarrantyClaim: boolean
  }
  timeline: { status: OrderStatus; label: string; atUtc: IsoDate }[]
  notes: { body: string; atUtc: IsoDate }[]
  quote?: {
    id: Guid
    version: number
    status: QuoteStatus
    statusLabel: string
    currency: string
    items: { description: string; quantity: number; unitPrice: number; lineTotal: number }[]
    subtotal: number
    discountAmount: number
    total: number
    validUntilUtc?: IsoDate | null
    warrantyDays?: number | null
    notes?: string | null
    canDecide: boolean
  } | null
  money?: { currency: string; total: number; paid: number; balance: number } | null
  canPayOnline: boolean
  warranty?: { days: number; expiresAtUtc?: IsoDate | null; active: boolean } | null
  canLeaveFeedback: boolean
  feedbackSubmitted: boolean
}

// ===== Billing (SaaS subscription) =====

export type PlanId = 'Basic' | 'Standard' | 'Pro'
export type SubscriptionStatus = 'Trialing' | 'Active' | 'PastDue' | 'Canceled' | 'Expired'
export type BillingProvider = 'None' | 'Manual' | 'MercadoPago' | 'Paddle' | 'Simulated'

export interface Plan {
  id: PlanId
  name: string
  maxBranches: number
  modules: string[]
  price: number | null
  currency: string
}

export interface PlansResponse {
  country: string
  currency: string
  trialDays: number
  plans: Plan[]
}

export interface BillingConfig {
  signupEnabled: boolean
  trialDays: number
  paddleClientToken: string | null
  paddleEnvironment: 'sandbox' | 'production'
}

export interface Subscription {
  plan: PlanId
  planName: string
  status: SubscriptionStatus
  provider: BillingProvider
  billingCountry: string
  hasFullAccess: boolean
  trialEndsAtUtc: IsoDate
  trialDaysLeft: number
  currentPeriodEndsAtUtc: IsoDate | null
  canceledAtUtc: IsoDate | null
  amount: number | null
  currency: string | null
  pendingPlan: PlanId | null
  modules: string[]
  maxBranches: number
  branchesUsed: number
  canManageAtProvider: boolean
  checkoutAvailable: boolean
  emailVerified: boolean
  plans: Plan[]
}

export interface CheckoutResult {
  url: string | null
  changed: boolean
}

export interface SignupInput {
  shopName: string
  ownerName: string
  email: string
  password: string
  country: string
  timeZone?: string
  acceptTerms: boolean
}
