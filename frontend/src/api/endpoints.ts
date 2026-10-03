import axios from 'axios'
import { API_BASE, authHeaders, del, get, getPage, post, put, upload, newIdempotencyKey } from './http'
import type * as T from './types'

// ===== Auth =====
export const authApi = {
  login: (email: string, password: string) => post<T.LoginResponse>('/auth/login', { email, password }),
  logout: () => axios.post(`${API_BASE}/auth/logout`, null, { withCredentials: true, headers: authHeaders }).catch(() => undefined),
  logoutAll: () => post<void>('/auth/logout-all'),
  me: () => get<T.MeResponse>('/auth/me'),
  switchShop: (shopId: T.Guid) => post<T.LoginResponse>('/auth/switch-shop', { shopId }, { headers: authHeaders }),
  changePassword: (currentPassword: string, newPassword: string) => post<T.LoginResponse>('/auth/change-password', { currentPassword, newPassword }),
  forgotPassword: (email: string) => post<void>('/auth/forgot-password', { email }),
  tokenInfo: (token: string) => get<T.TokenInfo>('/auth/token-info', { token }),
  resetPassword: (token: string, newPassword: string) => post<void>('/auth/reset-password', { token, newPassword }),
  acceptInvitation: (token: string, password: string, displayName?: string) =>
    post<T.LoginResponse>('/auth/accept-invitation', { token, password, displayName }, { headers: authHeaders }),
}

// ===== Users / settings =====
export const usersApi = {
  list: () => get<T.UserAdmin[]>('/users'),
  assignable: () => get<T.UserAdmin[]>('/users/assignable'),
  invite: (body: { email: string; displayName: string; role: T.Role }) => post<T.UserLink>('/users', body),
  update: (id: T.Guid, body: { displayName: string; role: T.Role; isActive: boolean }) => put<T.UserAdmin>(`/users/${id}`, body),
  resetLink: (id: T.Guid) => post<T.UserLink>(`/users/${id}/reset-link`),
  grantShop: (id: T.Guid, shopId: T.Guid, role: T.Role) => post<T.UserAdmin>(`/users/${id}/shops`, { shopId, role }),
  revokeShop: (id: T.Guid, shopId: T.Guid) => del(`/users/${id}/shops/${shopId}`),
}

export const settingsApi = {
  get: () => get<T.ShopSettings>('/settings'),
  update: (body: T.UpdateShopSettings) => put<T.ShopSettings>('/settings', body),
  uploadLogo: (file: File) => upload<T.ShopSettings>('/settings/logo', file),
  removeLogo: () => del('/settings/logo'),
  integrations: () => get<T.IntegrationsStatus>('/settings/integrations'),
  updateMercadoPago: (body: { enabled: boolean; accessToken?: string | null; webhookSecret?: string | null }) =>
    put<T.IntegrationsStatus>('/settings/integrations/mercadopago', body),
  updateFiscal: (body: { enabled: boolean; environment: T.FiscalEnvironment; pointOfSale: number; certificatePfxBase64?: string | null; certificatePassword?: string | null }) =>
    put<T.IntegrationsStatus>('/settings/integrations/fiscal', body),
  branches: () => get<T.Branch[]>('/settings/branches'),
  createBranch: (body: { name: string; phone?: string | null; addressLine?: string | null; city?: string | null; copyTemplates: boolean; copyCatalog: boolean }) =>
    post<T.Branch>('/settings/branches', body),
  setBranchActive: (id: T.Guid, value: boolean) => post<void>(`/settings/branches/${id}/active?value=${value}`),
}

export const templatesApi = {
  list: (includeInactive = true) => get<T.MessageTemplate[]>('/templates', { includeInactive }),
  tokens: () => get<T.TemplateToken[]>('/templates/tokens'),
  create: (body: { key: string; title: string; body: string; isActive: boolean }) => post<T.MessageTemplate>('/templates', body),
  update: (id: T.Guid, body: { title: string; body: string; isActive: boolean }) => put<T.MessageTemplate>(`/templates/${id}`, body),
  remove: (id: T.Guid) => del(`/templates/${id}`),
  restoreDefault: (id: T.Guid) => post<T.MessageTemplate>(`/templates/${id}/restore-default`),
}

// ===== CRM =====
export const customersApi = {
  search: (params: { q?: string; tag?: string; skip?: number; take?: number; sortBy?: string; sortDir?: string }) => getPage<T.Customer>('/customers', params),
  get: (id: T.Guid) => get<T.Customer>(`/customers/${id}`),
  summary: (id: T.Guid) => get<T.CustomerSummary>(`/customers/${id}/summary`),
  duplicates: (phone: string, excludeId?: T.Guid) => get<T.DuplicateCustomer[]>('/customers/duplicates', { phone, excludeId }),
  create: (body: T.CustomerInput) => post<T.Customer>('/customers', body),
  update: (id: T.Guid, body: T.CustomerInput) => put<T.Customer>(`/customers/${id}`, body),
  remove: (id: T.Guid) => del(`/customers/${id}`),
  merge: (targetId: T.Guid, sourceCustomerId: T.Guid) => post<T.Customer>(`/customers/${targetId}/merge`, { sourceCustomerId }),
  import: (file: File, dryRun: boolean) => upload<T.ImportResult>('/customers/import', file, undefined, { dryRun }),
}

export const devicesApi = {
  search: (params: { q?: string; customerId?: T.Guid; skip?: number; take?: number }) => getPage<T.Device>('/devices', params),
  get: (id: T.Guid) => get<T.Device>(`/devices/${id}`),
  create: (body: T.DeviceInput & { customerId: T.Guid }) => post<T.Device>('/devices', body),
  update: (id: T.Guid, body: T.DeviceInput) => put<T.Device>(`/devices/${id}`, body),
  remove: (id: T.Guid) => del(`/devices/${id}`),
}

// ===== Orders =====
export const ordersApi = {
  search: (params: T.OrderSearch) => getPage<T.RepairOrder>('/orders', params),
  board: (params: { q?: string; technicianId?: T.Guid; mine?: boolean }) => get<T.OrderBoard>('/orders/board', params),
  get: (id: T.Guid) => get<T.RepairOrder>(`/orders/${id}`),
  create: (body: T.CreateOrderInput, idempotencyKey = newIdempotencyKey()) => post<T.RepairOrder>('/orders', body, { idempotencyKey }),
  update: (id: T.Guid, body: { issueDescription: string; notes?: string | null; issueCategory?: string | null; warrantyDays?: number | null }) =>
    put<T.RepairOrder>(`/orders/${id}`, body),
  remove: (id: T.Guid) => del(`/orders/${id}`),
  plan: (id: T.Guid, body: { assignedTechnicianId?: T.Guid | null; priority: T.Priority; promisedAtUtc?: string | null }) => put<T.RepairOrder>(`/orders/${id}/plan`, body),
  setAgreedPrice: (id: T.Guid, amount: number | null, currency?: string) => put<T.RepairOrder>(`/orders/${id}/quote`, { amount, currency }),
  changeStatus: (id: T.Guid, body: T.ChangeStatusInput) => post<T.ChangeStatusResult>(`/orders/${id}/status`, body),
  history: (id: T.Guid) => get<T.StatusHistoryItem[]>(`/orders/${id}/history`),
  warrantyClaim: (id: T.Guid, issueDescription: string, notes?: string) => post<T.RepairOrder>(`/orders/${id}/warranty-claim`, { issueDescription, notes }),
  regenerateToken: (id: T.Guid) => post<T.RepairOrder>(`/orders/${id}/tracking-token`),
  setUnlock: (id: T.Guid, method: T.UnlockMethod, value?: string | null) => put<void>(`/orders/${id}/unlock`, { method, value }),
  revealUnlock: (id: T.Guid) => post<T.UnlockSecret>(`/orders/${id}/unlock/reveal`),
  saveSignature: (id: T.Guid, kind: T.SignatureKind, signerName: string, imageDataUrl: string) =>
    post<T.RepairOrder>(`/orders/${id}/signatures`, { kind, signerName, imageDataUrl }),
  suggestions: (id: T.Guid, ai = false) => get<T.RepairSuggestion>(`/orders/${id}/suggestions`, { ai }),

  notes: (id: T.Guid) => get<T.OrderNote[]>(`/orders/${id}/notes`),
  addNote: (id: T.Guid, body: string, isPublic: boolean) => post<T.OrderNote>(`/orders/${id}/notes`, { body, isPublic }),
  attachments: (id: T.Guid) => get<T.OrderAttachment[]>(`/orders/${id}/attachments`),
  addLink: (id: T.Guid, url: string, label?: string) => post<T.OrderAttachment>(`/orders/${id}/attachments`, { url, label }),
  uploadAttachment: (id: T.Guid, file: File, label?: string) => upload<T.OrderAttachment>(`/orders/${id}/attachments/upload`, file, label ? { label } : undefined),
  deleteAttachment: (id: T.Guid, attachmentId: T.Guid) => del(`/orders/${id}/attachments/${attachmentId}`),
  reception: (id: T.Guid) => get<T.ReceptionChecklist | null>(`/orders/${id}/checklist`),
  saveReception: (id: T.Guid, body: T.ReceptionChecklist) => put<T.ReceptionChecklist>(`/orders/${id}/checklist`, body),
  qa: (id: T.Guid) => get<T.QaChecklist | null>(`/orders/${id}/qa`),
  saveQa: (id: T.Guid, body: T.QaChecklist & { approve: boolean }) => put<T.QaChecklist>(`/orders/${id}/qa`, body),
  parts: (id: T.Guid) => get<T.PartUsage[]>(`/orders/${id}/parts`),
  usePart: (id: T.Guid, body: { inventoryItemId: T.Guid; quantityUsed: number; unitPrice?: number | null; unitPriceCurrency?: string | null }) =>
    post<T.PartUsage[]>(`/orders/${id}/parts`, body, { idempotencyKey: newIdempotencyKey() }),
  reservations: (id: T.Guid) => get<T.Reservation[]>(`/orders/${id}/reservations`),
  previewMessage: (id: T.Guid, templateKey: string) => get<T.MessagePreview>(`/orders/${id}/messages/preview`, { templateKey }),
  sendMessage: (id: T.Guid, body: { templateKey: string; channel?: T.NotificationChannel | null; customBody?: string | null }) =>
    post<T.NotificationResult>(`/orders/${id}/messages`, body),
  logManualMessage: (id: T.Guid, body: { channel: T.NotificationChannel; templateKey?: string | null; body: string }) =>
    post<T.OutboxMessage>(`/orders/${id}/messages/manual`, body),
  messages: (id: T.Guid) => get<T.OutboxMessage[]>(`/orders/${id}/messages`),

  quotes: (id: T.Guid) => get<T.Quote[]>(`/orders/${id}/quotes`),
  createQuote: (id: T.Guid, body: T.QuoteInput) => post<T.Quote>(`/orders/${id}/quotes`, body),
  updateQuote: (id: T.Guid, quoteId: T.Guid, body: T.QuoteInput) => put<T.Quote>(`/orders/${id}/quotes/${quoteId}`, body),
  sendQuote: (id: T.Guid, quoteId: T.Guid, body: { validDays?: number | null; enqueueOutbox?: boolean; channel?: T.NotificationChannel | null }) =>
    post<T.QuoteActionResult>(`/orders/${id}/quotes/${quoteId}/send`, body),
  approveQuote: (id: T.Guid, quoteId: T.Guid, note?: string) => post<T.QuoteActionResult>(`/orders/${id}/quotes/${quoteId}/approve`, { note }),
  rejectQuote: (id: T.Guid, quoteId: T.Guid, note?: string) => post<T.QuoteActionResult>(`/orders/${id}/quotes/${quoteId}/reject`, { note }),

  financials: (id: T.Guid) => get<T.OrderFinancials>(`/orders/${id}/financials`),
  payments: (id: T.Guid) => get<T.OrderPayment[]>(`/orders/${id}/payments`),
  addPayment: (id: T.Guid, body: { amount: number; currency: string; method: T.PaymentMethod; reference?: string | null }, idempotencyKey = newIdempotencyKey()) =>
    post<T.OrderPayment>(`/orders/${id}/payments`, body, { idempotencyKey }),
  refundPayment: (id: T.Guid, paymentId: T.Guid, body: { amount: number; method: T.PaymentMethod; reason: string }) =>
    post<T.OrderPayment>(`/orders/${id}/payments/${paymentId}/refund`, body, { idempotencyKey: newIdempotencyKey() }),
  paymentLinks: (id: T.Guid) => get<T.PaymentLink[]>(`/orders/${id}/payment-links`),
  createPaymentLink: (id: T.Guid) => post<T.PaymentLink>(`/orders/${id}/payment-links`),
  invoices: (id: T.Guid) => get<T.FiscalInvoice[]>(`/orders/${id}/invoices`),
  issueInvoice: (id: T.Guid, body: T.IssueInvoiceInput) => post<T.FiscalInvoice>(`/orders/${id}/invoices`, body, { idempotencyKey: newIdempotencyKey() }),
}

// ===== Inventory / purchasing =====
export const inventoryApi = {
  search: (params: { q?: string; includeInactive?: boolean; onlySellable?: boolean; onlyLowStock?: boolean; category?: string; brand?: string; model?: string; skip?: number; take?: number; sortBy?: string; sortDir?: string }) =>
    getPage<T.InventoryItem>('/inventory', params),
  get: (id: T.Guid) => get<T.InventoryItem>(`/inventory/${id}`),
  byCode: (code: string) => get<T.InventoryItem>(`/inventory/by-code/${encodeURIComponent(code)}`),
  create: (body: T.InventoryItemInput) => post<T.InventoryItem>('/inventory', body),
  update: (id: T.Guid, body: T.InventoryItemInput) => put<T.InventoryItem>(`/inventory/${id}`, body),
  adjust: (id: T.Guid, body: { type: T.AdjustmentType; deltaQuantity: number; reason?: string | null }) =>
    post<T.InventoryItem>(`/inventory/${id}/adjustments`, body, { idempotencyKey: newIdempotencyKey() }),
  adjustments: (id: T.Guid) => get<T.InventoryAdjustment[]>(`/inventory/${id}/adjustments`, { take: 100 }),
  compatibility: (id: T.Guid) => get<T.Compatibility[]>(`/inventory/${id}/compatibility`),
  addCompatibility: (id: T.Guid, brand: string, model: string) => post<T.Compatibility>(`/inventory/${id}/compatibility`, { brand, model }),
  removeCompatibility: (id: T.Guid, compatibilityId: T.Guid) => del(`/inventory/${id}/compatibility/${compatibilityId}`),
  import: (file: File, dryRun: boolean, updateExisting: boolean) => upload<T.ImportResult>('/inventory/import', file, undefined, { dryRun, updateExisting }),
}

export const purchasingApi = {
  suppliers: (params: { q?: string; includeInactive?: boolean; skip?: number; take?: number }) => getPage<T.Supplier>('/suppliers', params),
  createSupplier: (body: Omit<T.Supplier, 'id' | 'createdAtUtc'>) => post<T.Supplier>('/suppliers', body),
  updateSupplier: (id: T.Guid, body: Omit<T.Supplier, 'id' | 'createdAtUtc'>) => put<T.Supplier>(`/suppliers/${id}`, body),
  orders: (params: { status?: T.PurchaseOrderStatus; supplierId?: T.Guid; skip?: number; take?: number }) => getPage<T.PurchaseOrder>('/purchase-orders', params),
  get: (id: T.Guid) => get<T.PurchaseOrder>(`/purchase-orders/${id}`),
  create: (body: { supplierId: T.Guid; currency: string; lines: { inventoryItemId: T.Guid; quantity: number; unitCost: number }[]; notes?: string | null; expectedAtUtc?: string | null }) =>
    post<T.PurchaseOrder>('/purchase-orders', body),
  update: (id: T.Guid, body: { supplierId: T.Guid; currency: string; lines: { inventoryItemId: T.Guid; quantity: number; unitCost: number }[]; notes?: string | null; expectedAtUtc?: string | null }) =>
    put<T.PurchaseOrder>(`/purchase-orders/${id}`, body),
  markOrdered: (id: T.Guid) => post<T.PurchaseOrder>(`/purchase-orders/${id}/ordered`),
  cancel: (id: T.Guid) => post<T.PurchaseOrder>(`/purchase-orders/${id}/cancel`),
  receive: (id: T.Guid, lines: { lineId: T.Guid; quantity: number }[]) =>
    post<T.PurchaseOrder>(`/purchase-orders/${id}/receive`, { lines }, { idempotencyKey: newIdempotencyKey() }),
}

export const transfersApi = {
  list: (params: { status?: string; skip?: number; take?: number }) => getPage<T.Transfer>('/transfers', params),
  create: (body: { toShopId: T.Guid; lines: { inventoryItemId: T.Guid; quantity: number }[]; notes?: string | null }) =>
    post<T.Transfer>('/transfers', body, { idempotencyKey: newIdempotencyKey() }),
  receive: (id: T.Guid) => post<T.Transfer>(`/transfers/${id}/receive`, undefined, { idempotencyKey: newIdempotencyKey() }),
  cancel: (id: T.Guid) => post<T.Transfer>(`/transfers/${id}/cancel`),
}

// ===== Sales / cash / fiscal =====
export const salesApi = {
  catalog: (q?: string, take = 60) => get<T.PosCatalogItem[]>('/sales/catalog', { q, take }),
  search: (params: { q?: string; status?: T.SaleStatus; customerId?: T.Guid; cashSessionId?: T.Guid; dateFrom?: string; dateTo?: string; skip?: number; take?: number }) =>
    getPage<T.Sale>('/sales', params),
  get: (id: T.Guid) => get<T.Sale>(`/sales/${id}`),
  create: (body: T.CreateSaleInput, idempotencyKey: string) => post<T.Sale>('/sales', body, { idempotencyKey }),
  refund: (id: T.Guid, body: { lines: { saleLineId: T.Guid; quantity: number }[]; method: T.PaymentMethod; restock: boolean; reason?: string | null }) =>
    post<T.Sale>(`/sales/${id}/refund`, body, { idempotencyKey: newIdempotencyKey() }),
  void: (id: T.Guid, reason: string, method: T.PaymentMethod) => post<T.Sale>(`/sales/${id}/void`, { reason, method }),
  invoices: (id: T.Guid) => get<T.FiscalInvoice[]>(`/sales/${id}/invoices`),
  issueInvoice: (id: T.Guid, body: T.IssueInvoiceInput) => post<T.FiscalInvoice>(`/sales/${id}/invoices`, body, { idempotencyKey: newIdempotencyKey() }),
}

export const cashApi = {
  current: () => get<T.CashSession | null>('/cash/current'),
  sessions: (skip = 0, take = 30) => getPage<T.CashSession>('/cash/sessions', { skip, take }),
  session: (id: T.Guid) => get<T.CashSession>(`/cash/sessions/${id}`),
  open: (openingCash: number, currency?: string, notes?: string) => post<T.CashSession>('/cash/open', { openingCash, currency, notes }, { idempotencyKey: newIdempotencyKey() }),
  close: (body: { countedCash: number; declared?: { method: T.PaymentMethod; currency: string; amount: number }[]; notes?: string | null }) =>
    post<T.CashSession>('/cash/close', body, { idempotencyKey: newIdempotencyKey() }),
  movement: (body: { type: T.CashMovementType; amount: number; method?: T.PaymentMethod; currency?: string | null; description: string; category?: string | null }) =>
    post<T.CashMovement>('/cash/movements', body, { idempotencyKey: newIdempotencyKey() }),
}

export const fiscalApi = {
  invoices: (params: { dateFrom?: string; dateTo?: string; skip?: number; take?: number }) => getPage<T.FiscalInvoice>('/invoices', params),
  creditNote: (id: T.Guid) => post<T.FiscalInvoice>(`/invoices/${id}/credit-note`, undefined, { idempotencyKey: newIdempotencyKey() }),
  rates: (take = 30) => get<T.ExchangeRate[]>('/exchange-rates', { take }),
  setRate: (body: { date: string; rate: number; baseCurrency?: string; quoteCurrency?: string; source?: string }) => put<T.ExchangeRate>('/exchange-rates', body),
  refreshRates: () => post<number>('/exchange-rates/refresh'),
}

// ===== Dashboard / reports / admin =====
export const dashboardApi = {
  summary: () => get<T.DashboardSummary>('/dashboard/summary'),
  revenue: (days = 30) => get<T.RevenuePoint[]>('/dashboard/revenue', { days }),
  consolidated: () => get<T.ConsolidatedDashboard>('/dashboard/consolidated'),
}

export type ReportRange = { from?: string; to?: string }
export const reportsApi = {
  revenue: (r: ReportRange, groupBy = 'day') => get<T.RevenueReport>('/reports/revenue', { ...r, groupBy }),
  margins: (r: ReportRange) => get<T.MarginReport>('/reports/margins', r),
  repairTimes: (r: ReportRange) => get<T.RepairTimesReport>('/reports/repair-times', r),
  quotes: (r: ReportRange) => get<T.QuoteStatsReport>('/reports/quotes', r),
  topIssues: (r: ReportRange) => get<T.TopIssuesReport>('/reports/top-issues', r),
  technicians: (r: ReportRange) => get<T.TechniciansReport>('/reports/technicians', r),
  warranty: (r: ReportRange) => get<T.WarrantyReport>('/reports/warranty', r),
  feedback: (r: ReportRange) => get<T.FeedbackReport>('/reports/feedback', r),
  inventory: () => get<T.InventoryReport>('/reports/inventory'),
}

export const auditApi = {
  search: (params: { entityType?: string; entityId?: T.Guid; action?: string; userId?: T.Guid; dateFrom?: string; dateTo?: string; skip?: number; take?: number }) =>
    getPage<T.AuditEvent>('/audit', params),
}

export const notificationsApi = {
  list: (params: { status?: T.OutboxStatus; skip?: number; take?: number }) => getPage<T.OutboxMessage>('/notifications', params),
  retry: (id: T.Guid) => post<T.OutboxMessage>(`/notifications/${id}/retry`),
  cancel: (id: T.Guid) => post<T.OutboxMessage>(`/notifications/${id}/cancel`),
}

// ===== Public portal (no session) =====
const publicHttp = axios.create({ baseURL: API_BASE })
export const portalApi = {
  get: async (token: string) => (await publicHttp.get<T.ApiResponse<T.PublicTracking>>(`/public/orders/${encodeURIComponent(token)}`)).data.data,
  approve: async (token: string, quoteId: T.Guid, note?: string) =>
    (await publicHttp.post<T.ApiResponse<T.PublicTracking>>(`/public/orders/${encodeURIComponent(token)}/quotes/${quoteId}/approve`, { note })).data.data,
  reject: async (token: string, quoteId: T.Guid, note?: string) =>
    (await publicHttp.post<T.ApiResponse<T.PublicTracking>>(`/public/orders/${encodeURIComponent(token)}/quotes/${quoteId}/reject`, { note })).data.data,
  paymentLink: async (token: string) =>
    (await publicHttp.post<T.ApiResponse<{ url: string; amount: number; currency: string }>>(`/public/orders/${encodeURIComponent(token)}/payment-link`)).data.data,
  feedback: async (token: string, score: number, comment?: string) =>
    (await publicHttp.post<T.ApiResponse<{ saved: boolean; googleReviewUrl?: string | null }>>(`/public/orders/${encodeURIComponent(token)}/feedback`, { score, comment })).data.data,
  pdfUrl: (token: string, doc: 'quote' | 'warranty') => `${API_BASE}/public/orders/${encodeURIComponent(token)}/${doc}.pdf`,
}

// ===== Exports (binary) =====
export const exportPaths = {
  customers: '/customers/export',
  inventory: '/inventory/export',
  report: (name: string) => `/reports/${name}/export`,
}
