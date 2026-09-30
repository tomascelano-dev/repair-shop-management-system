import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
const base = 'http://127.0.0.1:5282'; let count = 0;
async function call(path, { method = 'GET', body, token, key = randomUUID(), status = 200, portal } = {}) {
  const response = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key, ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(portal ? { 'X-Portal-Token': portal } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
  const data = await response.json().catch(() => ({})); assert.equal(response.status, status, `${path}: ${JSON.stringify(data)}`); count++; return data.data ?? data;
}
const login = async (email, password) => (await call('/api/v1/auth/login', { method: 'POST', body: { email, password } })).accessToken;
const admin = await login('admin@local', 'Admin12345'), beta = await login('beta@local', 'DemoBeta12345'), tech = await login('tech@local', 'Tech123456');
const get = () => call('/api/v2/premium/workspace', { token: beta });
const post = (path, body, extra = {}) => call('/api/v2/premium/' + path, { token: beta, method: 'POST', body, ...extra });
const run = randomUUID().slice(0, 8);
await call('/api/v2/premium/workspace', { status: 401 });
const mainBefore = await call('/api/v2/premium/workspace', { token: admin });
const before = await get(); const branch = before.branches[0];
const branch2 = await post('branches', { name: 'QA ' + run, address: 'Sucursal de prueba' });
await post('branches', { name: 'QA ' + run, address: '' }, { status: 400 });
await post('branches', { name: 'No autorizado', address: '' }, { token: tech, status: 403 });

const csv = '\uFEFFcodigo;descripcion;compatibilidad;calidad;costo;moneda\n' + `QA-${run};"Pantalla; premium";Modelo QA;OLED;"1.234,50";ARS\n`;
const preview = await post('prices/preview', { fileName: 'prueba.csv', base64: Buffer.from(csv).toString('base64'), supplier: 'Proveedor QA ' + run, currency: 'ARS' });
assert.equal(preview.rows[0].unitCost, 1234.5); assert.equal(preview.rows[0].description, 'Pantalla; premium');
await post('prices/preview', { fileName: 'invalido.csv', base64: Buffer.from('codigo;descripcion;costo\nA1;Prueba;no-es-precio').toString('base64'), supplier: 'QA', currency: 'ARS' }, { status: 400 });

// A minimal real XLSX, generated without a spreadsheet runtime, with a numeric price cell.
function crc32(data) { let crc = -1; for (const b of data) { crc ^= b; for (let i = 0; i < 8; i++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1)); } return (crc ^ -1) >>> 0; }
function zip(files) { const local = [], central = []; let offset = 0; for (const [name, text] of Object.entries(files)) { const n = Buffer.from(name), b = Buffer.from(text), crc = crc32(b); const h = Buffer.alloc(30); h.writeUInt32LE(0x04034b50); h.writeUInt16LE(20, 4); h.writeUInt32LE(crc, 14); h.writeUInt32LE(b.length, 18); h.writeUInt32LE(b.length, 22); h.writeUInt16LE(n.length, 26); local.push(h, n, b); const c = Buffer.alloc(46); c.writeUInt32LE(0x02014b50); c.writeUInt16LE(20, 4); c.writeUInt16LE(20, 6); c.writeUInt32LE(crc, 16); c.writeUInt32LE(b.length, 20); c.writeUInt32LE(b.length, 24); c.writeUInt16LE(n.length, 28); c.writeUInt32LE(offset, 42); central.push(c, n); offset += h.length + n.length + b.length; } const cd = Buffer.concat(central), end = Buffer.alloc(22); end.writeUInt32LE(0x06054b50); end.writeUInt16LE(Object.keys(files).length, 8); end.writeUInt16LE(Object.keys(files).length, 10); end.writeUInt32LE(cd.length, 12); end.writeUInt32LE(offset, 16); return Buffer.concat([...local, cd, end]); }
const xlsx = zip({
  '[Content_Types].xml': '<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>',
  '_rels/.rels': '<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>',
  'xl/workbook.xml': '<?xml version="1.0"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Precios" sheetId="1" r:id="rId1"/></sheets></workbook>',
  'xl/_rels/workbook.xml.rels': '<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>',
  'xl/worksheets/sheet1.xml': '<?xml version="1.0"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>codigo</t></is></c><c r="B1" t="inlineStr"><is><t>descripcion</t></is></c><c r="C1" t="inlineStr"><is><t>costo</t></is></c></row><row r="2"><c r="A2" t="inlineStr"><is><t>QA-XLSX</t></is></c><c r="B2" t="inlineStr"><is><t>Bateria QA</t></is></c><c r="C2"><v>1234.56</v></c></row></sheetData></worksheet>'
});
const sheet = await post('prices/preview', { fileName: 'prueba.xlsx', base64: xlsx.toString('base64'), supplier: 'QA Excel', currency: 'ARS' }); assert.equal(sheet.rows[0].unitCost, 1234.56);
function pdf(text) { const stream = `BT /F1 12 Tf 50 750 Td (${text}) Tj ET`; const objs = ['<< /Type /Catalog /Pages 2 0 R >>', '<< /Type /Pages /Kids [3 0 R] /Count 1 >>', '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>', '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>', `<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`]; let s = '%PDF-1.4\n', offsets = [0]; for (let i = 0; i < objs.length; i++) { offsets.push(s.length); s += `${i + 1} 0 obj\n${objs[i]}\nendobj\n`; } const pos = s.length; s += 'xref\n0 6\n0000000000 65535 f \n' + offsets.slice(1).map(o => String(o).padStart(10, '0') + ' 00000 n \n').join('') + `trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${pos}\n%%EOF`; return Buffer.from(s); }
const pdfPreview = await post('prices/preview', { fileName: 'prueba.pdf', base64: pdf('BAT-QA Bateria de prueba ARS 1250,50').toString('base64'), supplier: 'QA PDF', currency: 'ARS' }); assert.equal(pdfPreview.rows[0].unitCost, 1250.5); assert.equal(pdfPreview.rows[0].sku, 'BAT-QA');
const explicitArs = await post('prices/preview', { fileName: 'moneda.pdf', base64: pdf('BAT-QA Bateria de prueba ARS 1250,50').toString('base64'), supplier: 'QA PDF', currency: 'USD' }); assert.equal(explicitArs.rows[0].currency, 'ARS');

const price = { supplier: 'Proveedor QA ' + run, sku: ('QA-' + run).toUpperCase(), description: 'Repuesto QA', compatibility: 'Equipo QA', quality: 'Premium', unitCost: 120.5, currency: 'ARS' };
const priceKey = randomUUID(); await post('prices', { rows: [price], source: 'QA' }, { key: priceKey }); await post('prices', { rows: [price], source: 'QA' }, { key: priceKey });
await post('prices', { rows: [{ ...price, unitCost: 2 }], source: 'QA' }, { key: priceKey, status: 409 });
await post('prices', { rows: [price, price], source: 'QA' }, { status: 400 });
let workspace = await get(); const offer = workspace.offers.find(x => x.sku === price.sku);
const receiptBody = { offerId: offer.id, itemId: null, branchId: branch.id, sku: price.sku, name: price.description, supplier: price.supplier, lotCode: 'QA-LOTE-' + run, serial: null, quantity: 3, unitCost: 120.5, currency: 'ARS' };
await post('stock/receive', { ...receiptBody, unitCost: 1 }, { status: 409 });
const receiptKey = randomUUID(); const lot = await post('stock/receive', receiptBody, { key: receiptKey }); const repeated = await post('stock/receive', receiptBody, { key: receiptKey }); assert.equal(lot.id, repeated.id);
await post('stock/minimum', { itemId: lot.itemId, branchId: branch.id, minimum: 2 });
await post('stock/transfer', { lotId: mainBefore.lots[0].id, branchId: branch2.id, quantity: 1, version: 1 }, { status: 404 });
await post('stock/receive', { ...receiptBody, sku: 'X-' + run, branchId: mainBefore.branches[0].id }, { status: 404 });

const intake = await call('/api/v2/intake', { token: beta, method: 'POST', body: { customerName: 'QA Premium', phone: '1100000000', brand: 'Prueba', model: 'Equipo premium', identifier: 'QA-' + run, issue: 'Reparacion integral de prueba', condition: 'Prueba', accessories: '', priority: 'Normal', checks: {} } });
const orderPath = '/api/v2/orders/' + intake.id;
let detail; async function orderAct(path, body, extra = {}) { detail = await call(orderPath, { token: beta }); return call(orderPath + '/' + path, { token: beta, method: 'POST', body: { version: detail.workflow.version, ...body }, ...extra }); }
const requests = [1, 2].map(() => fetch(base + '/api/v2/premium/stock/reserve', { method: 'POST', headers: { Authorization: `Bearer ${beta}`, 'Idempotency-Key': randomUUID(), 'Content-Type': 'application/json' }, body: JSON.stringify({ lotId: lot.id, orderId: intake.id, quantity: 2 }) }));
const results = await Promise.all(requests); assert.deepEqual(results.map(r => r.status).sort(), [200, 400]); count += 2;
workspace = await get(); const reservation = workspace.reservations.find(r => r.orderId === intake.id);
await post(`stock/reservations/${reservation.id}`, { version: reservation.version, action: 'consume' }, { status: 400 });
await post('stock/adjust', { lotId: lot.id, version: workspace.lots.find(l => l.id === lot.id).version, delta: -2, reason: 'QA stock reservado' }, { status: 400 });
await orderAct('stage', { status: 'Cancelled', reason: 'No debe cancelar con reservas' }, { status: 400 });
await orderAct('stage', { status: 'Diagnosing' });
const quoteBody = { lines: [{ description: 'Servicio QA', quantity: 1, unitPrice: 1200, unitCost: 99 }], currency: 'ARS', terms: 'Prueba local', validDays: 7, warrantyDays: 90 };
await orderAct('quotes', { ...quoteBody, currency: 'USD' }, { status: 400 });
const quote = await orderAct('quotes', quoteBody); const portal = await orderAct('portal', {});
await call('/api/v2/portal/decision', { method: 'POST', portal: portal.token, body: { quoteId: quote.id, accept: true, name: 'QA Cliente' } });
await orderAct('stage', { status: 'InProgress' });
const consumeKey = randomUUID(); await post(`stock/reservations/${reservation.id}`, { version: reservation.version, action: 'consume' }, { key: consumeKey }); await post(`stock/reservations/${reservation.id}`, { version: reservation.version, action: 'consume' }, { key: consumeKey });
workspace = await get(); assert.equal(workspace.lots.find(l => l.id === lot.id).quantity, 1); assert.equal(workspace.items.find(i => i.id === lot.itemId).quantityOnHand, 1);
await post(`stock/reservations/${reservation.id}`, { version: 1, action: 'release' }, { status: 409 });
await post('stock/transfer', { lotId: lot.id, branchId: branch2.id, quantity: 1, version: workspace.lots.find(l => l.id === lot.id).version });
await post('expenses', { orderId: intake.id, kind: 'Labor', description: 'Trabajo QA', minutes: 30, hourlyRate: 200, amount: 0, currency: 'USD' }, { status: 400 });
await post('expenses', { orderId: intake.id, kind: 'Labor', description: 'Trabajo QA', minutes: 30, hourlyRate: 200, amount: 999, currency: 'ARS' });
await post('expenses', { orderId: intake.id, kind: 'Commission', description: 'Comision QA', minutes: 0, hourlyRate: 0, amount: 10, currency: 'ARS' });
await orderAct('stage', { status: 'QualityCheck' });
await orderAct('diagnosis', { diagnosis: 'Pruebas completadas', laborCost: 777, qualityChecks: Object.fromEntries(['Pantalla y táctil', 'Cámaras', 'Audio y micrófono', 'Carga', 'Botones', 'Biometría'].map(k => [k, 'ok'])) }, { method: 'PUT' });
await orderAct('stage', { status: 'Ready' }); await orderAct('payments', { amount: 1200, currency: 'ARS', method: 0 }); await orderAct('handover', { recipient: 'QA Cliente' });
const warranty = await post('warranties', { orderId: intake.id, reservationId: reservation.id, problem: 'Falla de prueba repetida', failureCode: 'PANTALLA', supplier: '' });
await post(`warranties/${warranty.id}`, { version: 1, status: 'Resolved', supplierClaim: 'QA-RECLAMO', cost: 20, recovered: 21, resolution: 'Prueba de limite' }, { status: 400 });
await post(`warranties/${warranty.id}`, { version: 1, status: 'Resolved', supplierClaim: 'QA-RECLAMO', cost: 20, recovered: 5, resolution: 'Repuesto reemplazado' });
workspace = await get(); const result = workspace.profitability.find(p => p.orderId === intake.id); assert.equal(result.parts, 241); assert.equal(result.labor, 100); assert.equal(result.warranty, 15); assert.equal(result.net, 834);
await post('stock/receive', { ...receiptBody, offerId: null, itemId: lot.itemId, serial: 'SER-' + run, quantity: 2 }, { status: 400 });
await post('stock/receive', { ...receiptBody, offerId: null, itemId: lot.itemId, serial: 'SER-' + run, quantity: 1 });
await post('stock/receive', { ...receiptBody, offerId: null, itemId: lot.itemId, serial: 'SER-' + run, quantity: 1 }, { status: 400 });

const acquire = { branchId: branch.id, model: 'Telefono QA', identifier: 'IMEI-' + run, seller: 'Titular QA', acquisition: 'TradeIn', grade: 'B', diagnosis: 'Bateria agotada', purchasePrice: 50, targetPrice: 150, currency: 'ARS' };
const refurb = await post('refurbs', acquire); await post('refurbs', acquire, { status: 400 });
let refurbBody = { version: 1, status: 'Ready', grade: 'B', diagnosis: 'Bateria reemplazada', targetPrice: 150, qualityChecks: {}, salePrice: null, buyer: null };
await post(`refurbs/${refurb.id}`, refurbBody, { status: 400 });
await post(`refurbs/${refurb.id}/expenses`, { version: 1, description: 'Bateria QA', amount: 20 });
refurbBody = { ...refurbBody, version: 2, status: 'Repairing' }; await post(`refurbs/${refurb.id}`, refurbBody);
await post(`refurbs/${refurb.id}`, { ...refurbBody, version: 3, status: 'Ready' }, { status: 400 });
refurbBody = { ...refurbBody, version: 3, status: 'Ready', qualityChecks: Object.fromEntries(['Pantalla', 'Batería', 'Carga', 'Cámaras', 'Audio', 'Conectividad'].map(k => [k, true])) }; await post(`refurbs/${refurb.id}`, refurbBody);
await post(`refurbs/${refurb.id}`, { ...refurbBody, version: 4, status: 'Sold', salePrice: 150, buyer: 'Comprador QA' });
await post(`refurbs/${refurb.id}/expenses`, { version: 5, description: 'Gasto tardio', amount: 1 }, { status: 400 });

const now = new Date(), previous = new Date(now.getFullYear(), now.getMonth() - 1, 1), start = new Date(now.getFullYear(), now.getMonth() - 2, 1), end = new Date(now.getFullYear() + 1, now.getMonth(), 1);
const c = await post('contracts', { companyName: 'Empresa QA ' + run, contact: 'Contacto QA', phone: '1100000000', monthlyFee: 1000, extraOrderRate: 100, includedOrders: 1, slaHours: 48, currency: 'ARS', startsOn: start.toISOString().slice(0, 10), endsOn: end.toISOString().slice(0, 10), terms: 'Prueba local sin prorrateo' });
const e1 = await post('equipment', { contractId: c.id, brand: 'Lenovo', model: 'QA', identifier: 'EMP-1-' + run });
const e2 = await post('equipment', { contractId: c.id, brand: 'Lenovo', model: 'QA', identifier: 'EMP-2-' + run });
const batchBody = { contractId: c.id, branchId: branch2.id, equipmentIds: [e1.id, e2.id], issue: 'Mantenimiento por lote', reference: 'LOTE-QA-' + run }; const batchKey = randomUUID();
const batch = await post('business/intake', batchBody, { key: batchKey }); assert.equal(batch.orderIds.length, 2);
assert.deepEqual((await post('business/intake', batchBody, { key: batchKey })).orderIds, batch.orderIds);
await post('business/intake', batchBody, { status: 400 });
const period = previous.toISOString().slice(0, 7); const settlement = await post('settlements', { contractId: c.id, period }); assert.equal(settlement.total, 1000);
await post('settlements', { contractId: c.id, period }, { status: 400 });
await post('settlements', { contractId: c.id, period: now.toISOString().slice(0, 7) }, { status: 400 });
await post(`settlements/${settlement.id}/paid`, { version: 1 });
await post(`settlements/${settlement.id}/paid`, { version: 1 }, { status: 409 });
await call('/api/v1/inventory/' + lot.itemId + '/adjustments', { token: beta, method: 'POST', body: { deltaQuantity: 1 }, status: 409 });
const mainAfter = await call('/api/v2/premium/workspace', { token: admin });
assert.deepEqual(mainAfter, mainBefore);
workspace = await get(); assert.equal(workspace.refurbs.find(r => r.id === refurb.id).salePrice, 150); assert.equal(workspace.settlements.find(s => s.id === settlement.id).status, 'Paid');
console.log(`PASS: ${count} verificaciones HTTP, más cálculos y persistencia para los seis módulos. Importación CSV/XLSX/PDF, idempotencia, concurrencia, trazabilidad, garantías, reventa, lotes empresariales, liquidación y aislamiento. Solo se crearon datos QA en el taller B (${run}).`);
