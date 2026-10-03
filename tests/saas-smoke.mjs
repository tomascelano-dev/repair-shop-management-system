import assert from 'node:assert/strict';
import { randomUUID, createHmac } from 'node:crypto';
import http from 'node:http';

// End-to-end checks for the SaaS layer. Every record is created in a brand-new shop from the signup endpoint.
const base = 'http://127.0.0.1:5282'; let count = 0;
async function call(path, { method = 'GET', body, token, key = randomUUID(), status = 200, portal, headers = {}, raw = false } = {}) {
  const r = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key, ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(portal ? { 'X-Portal-Token': portal } : {}), ...headers }, body: body === undefined ? undefined : JSON.stringify(body) });
  if (raw) { assert.equal(r.status, status, path); count++; return r.text(); }
  const data = await r.json().catch(() => ({})); assert.equal(r.status, status, `${method} ${path}: ${JSON.stringify(data)}`); count++; return data.data ?? data;
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
async function eventually(check, label, timeout = 40000) {
  const end = Date.now() + timeout; let last;
  while (Date.now() < end) { try { const v = await check(); if (v) return v; } catch (e) { last = e; } await sleep(1000); }
  throw new Error(`Timeout: ${label} ${last ? last.message : ''}`);
}

// Webhook receiver that verifies the signature.
const received = []; let hookSecret = '';
const server = http.createServer((req, res) => {
  let body = ''; req.on('data', (c) => (body += c)); req.on('end', () => {
    const ts = req.headers['x-repairshop-timestamp']; const sig = req.headers['x-repairshop-signature'];
    const expected = 'v1=' + createHmac('sha256', hookSecret).update(`${ts}.${body}`).digest('hex');
    received.push({ event: req.headers['x-repairshop-event'], valid: sig === expected, body: JSON.parse(body) });
    res.writeHead(200); res.end('ok');
  });
});
await new Promise((r) => server.listen(5999, '127.0.0.1', r));

try {
  const run = randomUUID().slice(0, 6);
  // 1. Signup: trial with every module.
  await call('/api/saas/signup', { method: 'POST', body: { shopName: 'Taller QA', ownerName: 'Dueño QA', email: 'x', password: 'corta' }, status: 400 });
  const signup = await call('/api/saas/signup', { method: 'POST', body: { shopName: `Taller QA ${run}`, ownerName: 'Dueño QA', email: `qa-${run}@example.com`, password: 'ClaveSegura123', phone: '1144445555', city: 'CABA' } });
  const T = signup.accessToken; assert.ok(T);
  await call('/api/saas/signup', { method: 'POST', body: { shopName: 'Duplicado', ownerName: 'Otro', email: `qa-${run}@example.com`, password: 'ClaveSegura123' }, status: 400 });
  let me = await call('/api/saas/me', { token: T });
  assert.equal(me.subscription.plan, 'Pro'); assert.equal(me.subscription.status, 'Trialing'); assert.ok(me.subscription.modules.includes('api'));
  assert.ok(me.profile.slug.startsWith('taller-qa'));
  const slug = me.profile.slug;

  // 2. Branding and settings.
  let profile = await call('/api/saas/profile', { token: T });
  const tinyPng = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==';
  const profileBody = { ...profile, displayName: `Taller QA ${run}`, legalName: 'QA SRL', taxId: '20-12345678-6', taxCondition: 'Monotributo', email: `taller-${run}@example.com`, address: 'Av. Siempre Viva 123', logoDataUrl: tinyPng, primaryColor: '#0f766e', requireSignature: true, onlineBookingEnabled: true, openingHours: { days: [0, 1, 2, 3, 4, 5, 6], from: '00:00', to: '23:30', slotMinutes: 30 }, notifyEmail: true, notifySms: true, surveysEnabled: true, pickupReminderDays: 7, weeklySummaryEmail: `owner-${run}@example.com` };
  await call('/api/saas/profile', { token: T, method: 'PUT', body: { ...profileBody, taxId: '123' }, status: 400 });
  profile = await call('/api/saas/profile', { token: T, method: 'PUT', body: profileBody });
  assert.equal(profile.taxId, '20123456786'); assert.equal(profile.primaryColor, '#0f766e');
  await call('/api/saas/profile', { token: T, method: 'PUT', body: profileBody, status: 409 });
  await call('/api/saas/onboarding', { token: T, method: 'POST', body: { step: 3, completed: true } });

  // 3. Users: technician account.
  await call('/api/saas/users', { token: T, method: 'POST', body: { displayName: 'Técnico QA', email: `tech-${run}@example.com`, role: 'Tech', password: 'TecnicoQA1234' } });
  const techLogin = await call('/api/v1/auth/login', { method: 'POST', body: { email: `tech-${run}@example.com`, password: 'TecnicoQA1234' } });
  const TT = techLogin.accessToken; const techId = techLogin.user.id;
  await call('/api/saas/users', { token: TT, method: 'POST', body: { displayName: 'X', email: `x-${run}@example.com`, role: 'Admin', password: 'TecnicoQA1234' }, status: 403 });

  // 4. Service catalog: create, bulk price update and CSV import.
  await call('/api/saas/catalog', { token: T, method: 'POST', body: { version: 0, code: 'BAT-IP11', name: 'Cambio de batería iPhone 11', category: 'Baterías', price: 40000, estimatedCost: 18000, currency: 'ARS', estimatedMinutes: 45, warrantyDays: 90, active: true } });
  await call('/api/saas/catalog', { token: TT, method: 'POST', body: { version: 0, code: 'NOPE', name: 'Técnico no puede', category: '', price: 1, estimatedCost: 0, currency: 'ARS', estimatedMinutes: 0, warrantyDays: 0, active: true }, status: 403 });
  const bulk = await call('/api/saas/catalog/bulk-price', { token: T, method: 'POST', body: { category: 'Baterías', currency: 'ARS', percent: 10, rounding: '100' } });
  assert.equal(bulk.updated, 1);
  const imported = await call('/api/saas/catalog/import', { token: T, method: 'POST', body: { csv: 'codigo;nombre;categoria;precio;costo;moneda;minutos;garantia\nPIN-USB;Cambio de pin de carga;Placas;25.000,50;8000;ARS;60;60\nBAT-IP11;Cambio de batería iPhone 11;Baterías;45000;18000;ARS;45;90' } });
  assert.deepEqual(imported, { created: 1, updated: 1 });
  const catalog = await call('/api/saas/catalog', { token: T });
  assert.equal(catalog.find((x) => x.code === 'PIN-USB').price, 25000.5);

  // 5. Cash register and counter sale with stock.
  await call('/api/saas/sales', { token: T, method: 'POST', body: { lines: [{ description: 'Funda', quantity: 1, unitPrice: 5000 }], discount: 0, currency: 'ARS', method: 'Cash' }, status: 400 });
  await call('/api/saas/cash/open', { token: T, method: 'POST', body: { openingCash: 10000, openingCashUsd: 0 } });
  await call('/api/saas/cash/open', { token: T, method: 'POST', body: { openingCash: 1, openingCashUsd: 0 }, status: 400 });
  const ws = await call('/api/v2/premium/workspace', { token: T });
  const branch = ws.branches[0]; assert.ok(branch, 'la sucursal inicial existe');
  await call('/api/v2/premium/stock/receive', { token: T, method: 'POST', body: { itemId: null, sku: `FUN-${run}`, name: 'Funda silicona', branchId: branch.id, supplier: 'Proveedor QA', lotCode: 'L1', quantity: 5, unitCost: 1500, currency: 'ARS', serial: null } });
  const item = (await call('/api/v2/premium/workspace', { token: T })).items.find((x) => x.sku.toUpperCase() === `FUN-${run}`.toUpperCase());
  await call('/api/saas/sales', { token: T, method: 'POST', body: { lines: [{ description: 'Funda', quantity: 6, unitPrice: 5000, itemId: item.id }], discount: 0, currency: 'ARS', method: 'Cash' }, status: 400 });
  const sale = await call('/api/saas/sales', { token: T, method: 'POST', body: { lines: [{ description: 'Funda silicona', quantity: 2, unitPrice: 5000, itemId: item.id }, { description: 'Cambio de pin', quantity: 1, unitPrice: 25000.5, serviceId: catalog.find((x) => x.code === 'PIN-USB').id }], discount: 500.5, currency: 'ARS', method: 'Cash' } });
  assert.equal(sale.total, 34500);
  let stock = (await call('/api/v2/premium/workspace', { token: T })).lots.filter((l) => l.itemId === item.id).reduce((s, l) => s + l.quantity, 0);
  assert.equal(stock, 3);
  await call('/api/saas/cash/movements', { token: T, method: 'POST', body: { kind: 'Expense', method: 'Cash', amount: 2000, currency: 'ARS', description: 'Artículos de limpieza' } });
  await call('/api/saas/cash/movements', { token: TT, method: 'POST', body: { kind: 'Expense', method: 'Cash', amount: 1, currency: 'ARS', description: 'No autorizado' }, status: 403 });

  // Account sale for a customer, partial payment, void of another sale restores stock.
  const intakeCustomer = await call('/api/v2/intake', { token: T, method: 'POST', body: { customerName: 'Cliente Cuenta QA', phone: '1155556666', brand: 'Samsung', model: 'A54', identifier: `CC-${run}`, issue: 'No carga el equipo', condition: 'Usado', accessories: 'Ninguno', priority: 'Normal', checks: {} } });
  const board = await call('/api/v2/orders', { token: T });
  const customerOrder = board.orders.find((o) => o.id === intakeCustomer.id);
  const orderDetail0 = await call(`/api/v2/orders/${intakeCustomer.id}`, { token: T });
  const customerId = orderDetail0.order.customerId;
  await call('/api/saas/sales', { token: T, method: 'POST', body: { lines: [{ description: 'Cargador', quantity: 1, unitPrice: 12000 }], discount: 0, currency: 'ARS', method: 'Account' }, status: 400 });
  await call('/api/saas/sales', { token: T, method: 'POST', body: { customerId, lines: [{ description: 'Cargador', quantity: 1, unitPrice: 12000 }], discount: 0, currency: 'ARS', method: 'Account' } });
  await call(`/api/saas/accounts/${customerId}/payments`, { token: T, method: 'POST', body: { amount: 13000, currency: 'ARS', method: 'Cash' }, status: 400 });
  await call(`/api/saas/accounts/${customerId}/payments`, { token: T, method: 'POST', body: { amount: 5000, currency: 'ARS', method: 'Transfer' } });
  const account = await call(`/api/saas/accounts/${customerId}`, { token: T });
  assert.equal(account.balances[0].balance, 7000);
  const toVoid = await call('/api/saas/sales', { token: T, method: 'POST', body: { lines: [{ description: 'Funda silicona', quantity: 1, unitPrice: 5000, itemId: item.id }], discount: 0, currency: 'ARS', method: 'Card' } });
  await call(`/api/saas/sales/${toVoid.id}/void`, { token: TT, method: 'POST', body: { reason: 'Error de carga' }, status: 403 });
  await call(`/api/saas/sales/${toVoid.id}/void`, { token: T, method: 'POST', body: { reason: 'Error de carga' } });
  stock = (await call('/api/v2/premium/workspace', { token: T })).lots.filter((l) => l.itemId === item.id).reduce((s, l) => s + l.quantity, 0);
  assert.equal(stock, 3);

  // 6. Invoicing (internal receipts in this environment) and credit note.
  const invoice = await call('/api/saas/invoices', { token: T, method: 'POST', body: { sourceType: 'Sale', sourceId: sale.id } });
  assert.equal(invoice.voucherType, 11); assert.ok(['Simulated', 'Authorized'].includes(invoice.status)); assert.equal(invoice.total, 34500);
  await call('/api/saas/invoices', { token: T, method: 'POST', body: { sourceType: 'Sale', sourceId: sale.id }, status: 400 });
  await call(`/api/saas/sales/${sale.id}/void`, { token: T, method: 'POST', body: { reason: 'Tiene factura' }, status: 400 });
  const note = await call(`/api/saas/invoices/${invoice.id}/credit-note`, { token: T, method: 'POST', body: { reason: 'Devolución total' } });
  assert.equal(note.voucherType, 13); assert.equal(note.cancelsInvoiceId, invoice.id);
  await call('/api/saas/invoices', { token: T, method: 'POST', body: { sourceType: 'Sale', sourceId: sale.id } });
  const fiscal = await call('/api/saas/fiscal', { token: T });
  await call('/api/saas/fiscal', { token: T, method: 'PUT', body: { version: fiscal.version, enabled: true, environment: 'Homologacion', pointOfSale: 2, defaultVatRate: 21 }, status: 400 });

  // 7. Full repair: intake with email, send portal, signed approval, ready, payment into cash, invoice, delivery, survey.
  const order = await call('/api/v2/intake', { token: T, method: 'POST', body: { customerName: 'Lucía QA', phone: '11 5555-0000', email: `cliente-${run}@example.com`, brand: 'Apple', model: 'iPhone 13', identifier: `IMEI-${run}`, issue: 'Pantalla rota tras una caída', condition: 'Marcas', accessories: 'Funda', priority: 'Alta', checks: {} } });
  const path = `/api/v2/orders/${order.id}`;
  let d = await call(path, { token: T });
  const act = (suffix, body, o = {}) => call(path + suffix, { token: T, method: 'POST', body: { version: d.workflow.version, ...body }, ...o });
  const refresh = async () => (d = await call(path, { token: T }));
  let extras = await call(`/api/saas/orders/${order.id}`, { token: T });
  assert.equal(extras.email, `cliente-${run}@example.com`); assert.equal(extras.code.length, 8);
  const tracked = await call(`/api/public/shops/${slug}/track?code=${extras.code}`);
  assert.equal(tracked.status, 'Received'); assert.equal(tracked.customer, 'Lucía');
  await call(`/api/public/shops/${slug}/track?code=ZZZZZZZZ`, { status: 404 });
  await act('/stage', { status: 'Diagnosing' }); await refresh();
  await act('/quotes', { lines: [{ description: 'Cambio de pantalla', quantity: 1, unitPrice: 120000, unitCost: 60000 }], currency: 'ARS', terms: 'Garantía 90 días', validDays: 7, warrantyDays: 90 }); await refresh();
  const sent = await call(`/api/saas/orders/${order.id}/send-portal`, { token: T, method: 'POST', body: { version: d.workflow.version } }); await refresh();
  assert.ok(sent.sentTo.includes(`cliente-${run}@example.com`));
  const portal = await call('/api/v2/portal', { portal: sent.token });
  assert.equal(portal.branding.primaryColor, '#0f766e'); assert.equal(portal.branding.requireSignature, true);
  await call('/api/v2/portal/decision', { method: 'POST', portal: sent.token, body: { quoteId: portal.quote.id, accept: true, name: 'Lucía QA' }, status: 400 });
  await call('/api/v2/portal/decision', { method: 'POST', portal: sent.token, body: { quoteId: portal.quote.id, accept: true, name: 'Lucía QA', signature: tinyPng } });
  await refresh(); assert.equal(d.quotes[0].status, 'Accepted');
  extras = await call(`/api/saas/orders/${order.id}`, { token: T });
  assert.equal(extras.signatures.length, 1); assert.equal(extras.signatures[0].signerName, 'Lucía QA');

  // Technician: assign, timer and diagram.
  await call(`/api/saas/tech/orders/${order.id}/assign`, { token: T, method: 'POST', body: { technicianId: techId, mode: 'Workshop' } });
  let queue = await call('/api/saas/tech', { token: TT });
  let mine = queue.assignments.find((a) => a.orderId === order.id); assert.ok(mine);
  let timer = await call(`/api/saas/tech/orders/${order.id}/timer`, { token: TT, method: 'POST', body: { version: mine.version, action: 'start' } });
  timer = await call(`/api/saas/tech/orders/${order.id}/timer`, { token: TT, method: 'POST', body: { version: timer.version, action: 'pause' } });
  assert.equal(timer.state, 'Paused'); assert.ok(timer.workedMinutes >= 1);
  await call(`/api/saas/diagram/${order.id}`, { token: TT, method: 'PUT', body: { version: 0, template: 'phone', marks: [{ x: 40, y: 20, kind: 'crack', note: 'Pantalla astillada' }] } });
  await call(`/api/saas/diagram/${order.id}`, { token: TT, method: 'PUT', body: { version: 0, template: 'phone', marks: [] }, status: 409 });
  assert.equal((await call(`/api/saas/diagram/${order.id}`, { token: T })).marks.length, 1);
  await refresh();

  await act('/stage', { status: 'InProgress' }); await refresh();
  await act('/stage', { status: 'QualityCheck' }); await refresh();
  const checks = Object.fromEntries(['Pantalla y táctil', 'Cámaras', 'Audio y micrófono', 'Carga', 'Botones', 'Biometría'].map((k) => [k, 'ok']));
  await act('/diagnosis', { diagnosis: 'Pantalla reemplazada y probada', laborCost: 20000, qualityChecks: checks }, { method: 'PUT' }); await refresh();
  await act('/stage', { status: 'Ready' }); await refresh();
  await act('/payments', { amount: 120000, currency: 'ARS', method: 0 }); await refresh();
  const cash = await call('/api/saas/cash', { token: T });
  assert.ok(cash.movements.some((m) => m.kind === 'OrderPayment' && m.amount === 120000));
  const orderInvoice = await call('/api/saas/invoices', { token: T, method: 'POST', body: { sourceType: 'Order', sourceId: order.id, docType: 96, docNumber: '30111222', taxCondition: 'ConsumidorFinal' } });
  assert.equal(orderInvoice.total, 120000); assert.equal(orderInvoice.docNumber, '30111222');
  await call(`/api/saas/tech/orders/${order.id}/timer`, { token: TT, method: 'POST', body: { version: timer.version, action: 'finish', report: 'Equipo entregado a mostrador' } });
  await act('/handover', { recipient: 'Lucía QA' }); await refresh();

  // Messages: received, portal, ready and survey were queued; the dispatcher marks them sent.
  const messages = await eventually(async () => {
    const m = await call('/api/saas/notifications', { token: T });
    const mine = m.filter((x) => x.relatedEntityId === order.id);
    return mine.length >= 6 && mine.every((x) => x.status === 'Sent') ? mine : null;
  }, 'mensajes enviados');
  assert.ok(messages.some((m) => m.channel === 'Sms' && m.recipient === '+5491155550000'));
  const surveyMail = messages.find((m) => m.channel === 'Email' && m.body.includes('/encuesta/'));
  const surveyToken = surveyMail.body.match(/encuesta\/([0-9A-F]+)/)[1];
  const survey = await call(`/api/public/surveys/${surveyToken}`);
  assert.equal(survey.answered, false);
  await call(`/api/public/surveys/${surveyToken}`, { method: 'POST', body: { score: 11 }, status: 400 });
  await call(`/api/public/surveys/${surveyToken}`, { method: 'POST', body: { score: 10, comment: 'Excelente atención' } });
  await call(`/api/public/surveys/${surveyToken}`, { method: 'POST', body: { score: 9 }, status: 400 });

  // 8. Agenda: staff appointment, preventive recurrence, online booking and conversion to an order.
  const tomorrow = new Date(Date.now() + 36 * 3600 * 1000);
  const appt = await call('/api/saas/appointments', { token: T, method: 'POST', body: { version: 0, customerName: 'Empresa QA', phone: '1144440000', email: '', deviceLabel: 'Notebook', reason: 'Mantenimiento preventivo', startsAtUtc: tomorrow.toISOString(), durationMinutes: 60, technicianId: techId, kind: 'Field', address: 'Calle Falsa 123', recurrenceMonths: 6 } });
  let agenda = await call('/api/saas/appointments', { token: T });
  const created = agenda.appointments.find((a) => a.id === appt.id);
  const done = await call(`/api/saas/appointments/${appt.id}/status`, { token: T, method: 'POST', body: { version: created.version, status: 'Done' } });
  assert.ok(done.next, 'el mantenimiento preventivo agenda la próxima visita');
  const shop = await call(`/api/public/shops/${slug}`);
  assert.equal(shop.onlineBooking, true);
  const day = new Date(Date.now() + 3 * 24 * 3600 * 1000).toISOString().slice(0, 10);
  const slots = await call(`/api/public/shops/${slug}/slots?date=${day}`);
  assert.ok(slots.length > 10);
  const booked = await call(`/api/public/shops/${slug}/appointments`, { method: 'POST', body: { customerName: 'Cliente Online', phone: '1133332222', email: `online-${run}@example.com`, deviceLabel: 'Moto G84', reason: 'No enciende', startsAtUtc: slots[0] } });
  await call(`/api/public/shops/${slug}/appointments`, { method: 'POST', body: { customerName: 'Otro Cliente', phone: '1133332223', reason: 'Mismo horario', startsAtUtc: slots[0] }, status: 409 });
  agenda = await call('/api/saas/appointments', { token: T, headers: {} });
  const online = (await call(`/api/saas/appointments?from=${new Date(Date.now() - 86400000).toISOString()}&to=${new Date(Date.now() + 10 * 86400000).toISOString()}`, { token: T })).appointments.find((a) => a.id === booked.id);
  assert.equal(online.source, 'Online');
  const converted = await call(`/api/saas/appointments/${booked.id}/convert`, { token: T, method: 'POST', body: { version: online.version, brand: 'Motorola', model: 'Moto G84', identifier: '', issue: 'No enciende después de cargar' } });
  assert.ok(converted.orderId);

  // 9. Alerts, reports and CSV export.
  const alerts = await call('/api/saas/alerts', { token: T });
  for (const kind of ['QuoteDecision', 'OrderReady', 'Survey', 'Booking']) assert.ok(alerts.some((a) => a.kind === kind), `alerta ${kind}`);
  await call('/api/saas/alerts/read', { token: T, method: 'POST' });
  assert.equal((await call('/api/saas/me', { token: T })).unreadAlerts, 0);
  const report = await call('/api/saas/reports', { token: T });
  assert.equal(report.satisfaction.nps, 100); assert.ok(report.technicians.some((t) => t.technicianId === techId));
  const csv = await call('/api/saas/reports/export?kind=cash', { token: T, raw: true });
  assert.ok(csv.includes('OrderPayment'));

  // 10. API keys, external API and signed webhooks.
  const hook = await call('/api/saas/integrations/webhooks', { token: T, method: 'POST', body: { url: 'http://127.0.0.1:5999/hook', events: ['order.created', 'order.status_changed'] } });
  hookSecret = hook.secret;
  const key = (await call('/api/saas/integrations/keys', { token: T, method: 'POST', body: { name: 'Zapier' } })).key;
  await call('/api/ext/v1/orders', { status: 401 });
  await call('/api/ext/v1/orders', { headers: { 'X-Api-Key': key + 'x' }, status: 401 });
  const ext = await call('/api/ext/v1/orders?pageSize=5', { headers: { 'X-Api-Key': key } });
  assert.ok(ext.length >= 3);
  await call(`/api/ext/v1/orders/${order.id}`, { headers: { 'X-Api-Key': key } });
  await call('/api/ext/v1/appointments', { method: 'POST', headers: { 'X-Api-Key': key }, body: { customerName: 'Desde Zapier', phone: '1122223333', reason: 'Turno desde formulario web', startsAtUtc: tomorrow.toISOString() } });
  await call('/api/v2/intake', { token: T, method: 'POST', body: { customerName: 'Webhook QA', phone: '1100001111', brand: 'Xiaomi', model: 'Redmi 12', identifier: `WH-${run}`, issue: 'Prueba de webhook', condition: '', accessories: '', priority: 'Normal', checks: {} } });
  await eventually(() => received.some((r) => r.event === 'order.created' && r.valid), 'webhook firmado');
  assert.ok(received.every((r) => r.valid));

  // 11. Other shops cannot see these records.
  const beta = (await call('/api/v1/auth/login', { method: 'POST', body: { email: 'beta@local', password: 'DemoBeta12345' } })).accessToken;
  await call(`/api/saas/orders/${order.id}`, { token: beta, status: 404 });
  await call(`/api/saas/accounts/${customerId}`, { token: beta, status: 404 });
  assert.ok(!(await call('/api/saas/invoices', { token: beta })).some((i) => i.id === invoice.id));

  // 12. Plans: switching to Basic removes modules; the gate answers 403.
  const checkout = await call('/api/saas/billing/checkout', { token: T, method: 'POST', body: { plan: 'Basic' } });
  assert.ok(checkout.url.includes('/billing/simulated'));
  await call('/api/saas/billing/simulated/confirm', { token: T, method: 'POST', body: { subscriptionId: checkout.subscriptionId } });
  const billing = await call('/api/saas/billing', { token: T });
  assert.equal(billing.subscription.plan, 'Basic'); assert.equal(billing.subscription.status, 'Active');
  await call('/api/v2/premium/stock/minimum', { token: T, method: 'POST', body: { itemId: item.id, branchId: branch.id, minimum: 1 }, status: 403 });
  await call('/api/saas/tech', { token: T, status: 403 });
  await call('/api/ext/v1/orders', { headers: { 'X-Api-Key': key }, status: 401 });
  await call('/api/saas/cash', { token: T });
  const cancelled = await call('/api/saas/billing/cancel', { token: T, method: 'POST' });
  assert.equal(cancelled.status, 'Cancelled');

  console.log(`PASS: ${count} verificaciones HTTP del SaaS: alta, marca, usuarios, catálogo, caja, cuentas corrientes, comprobantes, portal con firma, técnico, diagrama, avisos, encuestas, agenda online, alertas, reportes, API, webhooks firmados, aislamiento y planes. Taller ${slug}.`);
} finally {
  server.close();
}
