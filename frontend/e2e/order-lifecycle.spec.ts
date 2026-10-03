import { expect, test } from '@playwright/test'
import { createOrderViaUi, ensureCashOpen, login, moveOrderTo } from './helpers'

const QA_ITEMS = ['Enciende', 'Pantalla', 'Táctil', 'Cámaras', 'Audio / parlantes', 'Micrófono', 'Botones', 'Carga', 'WiFi / señal / Bluetooth', 'Face ID / huella']

test('an order goes from reception to delivery: quote, QA, payment and warranty', async ({ page }) => {
  test.setTimeout(120_000)
  await login(page, 'admin')
  await ensureCashOpen(page)
  const code = await createOrderViaUi(page)
  const orderUrl = page.url()

  await moveOrderTo(page, 'En diagnóstico')

  // Starting the repair without an approved quote is blocked and leads to the quote tab.
  await page.getByRole('button', { name: /En reparación/ }).click()
  await expect(page.getByRole('tab', { name: /^Presupuestos/, selected: true })).toBeVisible()

  await page.getByRole('button', { name: '+ Armar presupuesto' }).click()
  await page.getByLabel('Precio unit.').first().fill('25000')
  await page.getByRole('button', { name: 'Guardar borrador' }).click()
  await expect(page.getByText('Versión 1')).toBeVisible()
  await page.getByRole('button', { name: 'Aprobado', exact: true }).click()
  await page.getByRole('button', { name: 'Aprobar', exact: true }).click()
  await expect(page.getByText('Aprobado', { exact: true }).first()).toBeVisible()

  await moveOrderTo(page, 'En reparación')

  // Quality control before "ready".
  await page.getByRole('tab', { name: /^Checklists/ }).click()
  for (const item of QA_ITEMS) await page.getByRole('group', { name: item }).getByRole('button', { name: 'OK' }).click()
  await page.getByRole('button', { name: 'Aprobar control' }).click()
  await expect(page.getByText(/^Aprobado/).first()).toBeVisible()

  await moveOrderTo(page, 'Listo para retirar')

  // Pay the balance, then deliver.
  await page.getByRole('tab', { name: /^Pagos/ }).click()
  await page.getByRole('button', { name: 'Registrar pago / seña' }).click()
  await page.getByLabel('Medio de pago').selectOption('Transfer')
  await page.getByRole('button', { name: /^Registrar \$/ }).click()
  await expect(page.getByText('Pago registrado')).toBeVisible()

  await moveOrderTo(page, 'Entregado')
  await expect(page.getByRole('button', { name: 'Garantía', exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Reingreso por garantía' })).toBeVisible()

  // History keeps every step.
  await page.goto(orderUrl + '?tab=history')
  await page.getByRole('tab', { name: /^Historial/ }).click()
  for (const label of ['En diagnóstico', 'En reparación', 'Listo para retirar', 'Entregado']) await expect(page.getByText(label).first()).toBeVisible()
  expect(code).toMatch(/^#\d+$/)
})
