import { expect, type Page } from '@playwright/test'

// Users created by the development seed (Seed__Enabled=true).
export const USERS = {
  admin: { email: 'admin@local', password: 'Admin123456' },
  tech: { email: 'tech@local', password: 'Tech123456' },
  reception: { email: 'recepcion@local', password: 'Recepcion123' },
  cashier: { email: 'caja@local', password: 'Caja123456' },
} as const

export async function login(page: Page, who: keyof typeof USERS) {
  const user = USERS[who]
  await page.goto('/login')
  await page.getByLabel('Email').fill(user.email)
  await page.getByLabel('Contraseña').fill(user.password)
  await page.getByRole('button', { name: 'Ingresar' }).click()
  await expect(page.getByRole('navigation', { name: 'Principal' }).first()).toBeVisible()
}

export function uniquePhone() {
  return `11${String(Date.now()).slice(-8)}`
}

/** Registers a customer, a device and an order through the reception flow; leaves the page on the order. */
export async function createOrderViaUi(page: Page, issue = 'No carga, el pin está flojo') {
  await page.goto('/orders/new')
  await page.getByRole('button', { name: '+ Cliente nuevo' }).click()
  await page.getByLabel('Nombre y apellido').fill(`Cliente E2E ${Date.now()}`)
  await page.getByLabel('Teléfono / WhatsApp').fill(uniquePhone())
  await page.getByRole('button', { name: 'Crear cliente y seguir' }).click()
  await page.getByLabel('Marca').fill('Motorola')
  await page.getByLabel('Modelo').fill('Moto G54')
  await page.getByRole('button', { name: 'Agregar equipo y seguir' }).click()
  await page.getByLabel('Falla que reporta el cliente').fill(issue)
  await page.getByRole('button', { name: 'Crear orden' }).click()
  await expect(page.getByText('Orden creada')).toBeVisible()
  const code = ((await page.locator('p.text-4xl').textContent()) ?? '').trim()
  await page.getByRole('button', { name: 'Ver orden' }).click()
  await expect(page.getByRole('heading', { name: code })).toBeVisible()
  return code
}

/** Opens the register if it is closed (payments require an open cash session). */
export async function ensureCashOpen(page: Page) {
  await page.goto('/cash')
  await expect(page.getByText(/La caja está cerrada|Caja abierta · turno/).first()).toBeVisible()
  if (await page.getByText('La caja está cerrada').isVisible()) {
    await page.getByLabel('Efectivo inicial (cambio)').fill('5000')
    await page.getByRole('button', { name: 'Abrir caja' }).click()
    await expect(page.getByText(/Caja abierta · turno/)).toBeVisible()
  }
}

/** Uses the "Pasar a" bar of the order detail. */
export async function moveOrderTo(page: Page, statusLabel: string) {
  await page.getByRole('button', { name: statusLabel, exact: true }).click()
  await page.getByRole('button', { name: 'Confirmar' }).click()
  await page.getByRole('button', { name: 'Listo', exact: true }).click()
  await expect(page.getByText(statusLabel).first()).toBeVisible()
}
