import { expect, test } from '@playwright/test'
import { login } from './helpers'

test('cashier opens the register and sells with a barcode, giving change', async ({ page }) => {
  await login(page, 'cashier')

  await page.goto('/cash')
  await expect(page.getByText(/La caja está cerrada|Caja abierta · turno/).first()).toBeVisible()
  if (await page.getByText('La caja está cerrada').isVisible()) {
    await page.getByLabel('Efectivo inicial (cambio)').fill('10000')
    await page.getByRole('button', { name: 'Abrir caja' }).click()
  }
  await expect(page.getByText(/Caja abierta · turno #\d+/)).toBeVisible()

  await page.goto('/pos')
  const search = page.getByRole('searchbox')
  await search.fill('7798000000031')
  await search.press('Enter')
  const cart = page.getByRole('list', { name: 'Carrito' })
  await expect(cart).toContainText('Vidrio templado (universal)')
  await expect(search).toHaveValue('')

  await page.getByRole('button', { name: 'Cobrar (F4)' }).click()
  const amount = page.getByLabel('Monto')
  await amount.fill('5000')
  await expect(page.getByText('Vuelto', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Confirmar cobro' }).click()

  await expect(page.getByText(/Venta V-\d+ registrada/)).toBeVisible()
  await expect(page.getByText(/1\.000,00/).first()).toBeVisible()
  await page.getByRole('button', { name: 'Nueva venta' }).click()

  // The sale shows up in the register movements and in the sales history.
  await page.goto('/cash')
  await expect(page.getByText(/Venta V-\d+/).first()).toBeVisible()
  await page.goto('/sales')
  await expect(page.getByRole('cell', { name: /V-\d+/ }).first()).toBeVisible()
})
