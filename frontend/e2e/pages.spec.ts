import { expect, test, type Page } from '@playwright/test'
import { login } from './helpers'

// Visits every section as an administrator and fails on runtime errors or failing API calls.
const ROUTES = [
  '/',
  '/orders',
  '/orders/board',
  '/orders/new',
  '/customers',
  '/pos',
  '/sales',
  '/cash',
  '/invoices',
  '/invoices?tab=rates',
  '/inventory',
  '/inventory?lowStock=1',
  '/purchases',
  '/transfers',
  '/reports?r=revenue',
  '/reports?r=margins',
  '/reports?r=repair-times',
  '/reports?r=quotes',
  '/reports?r=top-issues',
  '/reports?r=technicians',
  '/reports?r=warranty',
  '/reports?r=feedback',
  '/reports?r=inventory',
  '/messages',
  '/settings?tab=general',
  '/settings?tab=operation',
  '/settings?tab=templates',
  '/settings?tab=integrations',
  '/settings?tab=users',
  '/settings?tab=branches',
  '/settings?tab=audit',
  '/profile',
]

function watch(page: Page) {
  const problems: string[] = []
  page.on('console', (msg) => {
    if (msg.type() === 'error') problems.push(`console: ${msg.text()}`)
  })
  page.on('pageerror', (err) => problems.push(`page error: ${err.message}`))
  page.on('response', (res) => {
    if (res.url().includes('/api/') && res.status() >= 400 && !res.url().includes('/auth/refresh')) problems.push(`${res.status()} ${res.request().method()} ${res.url()}`)
  })
  return problems
}

test('every section loads without errors', async ({ page }) => {
  test.setTimeout(180_000)
  const problems = watch(page)
  await login(page, 'admin')

  for (const route of ROUTES) {
    await page.goto(route)
    await expect(page.locator('main h1').first()).toBeVisible()
    await page.waitForLoadState('networkidle')
    if (process.env.E2E_SCREENSHOTS) await page.screenshot({ path: `${process.env.E2E_SCREENSHOTS}/${route.replace(/[/?=&]+/g, '_') || 'home'}.png`, fullPage: true })
  }

  // Detail pages reached from the lists.
  await page.goto('/orders')
  await page.locator('tbody tr').first().click()
  await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}/)
  await page.waitForLoadState('networkidle')
  for (const tab of ['Presupuestos', 'Repuestos', 'Pagos', 'Controles', 'Notas', 'Fotos', 'Mensajes', 'Historial', 'Sugerencias']) {
    const t = page.getByRole('tab', { name: new RegExp(`^${tab}`) })
    if (await t.count()) {
      await t.first().click()
      await page.waitForLoadState('networkidle')
    }
  }
  if (process.env.E2E_SCREENSHOTS) await page.screenshot({ path: `${process.env.E2E_SCREENSHOTS}/order_detail.png`, fullPage: true })

  await page.goto('/customers')
  await page.locator('tbody tr').first().click()
  await expect(page).toHaveURL(/\/customers\/[0-9a-f-]{36}/)
  await page.waitForLoadState('networkidle')
  if (process.env.E2E_SCREENSHOTS) await page.screenshot({ path: `${process.env.E2E_SCREENSHOTS}/customer_detail.png`, fullPage: true })

  expect(problems).toEqual([])
})
