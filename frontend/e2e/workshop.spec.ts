import { expect, test } from '@playwright/test'
import { login, uniquePhone } from './helpers'

test('reception registers a repair and the customer follows it online', async ({ page, browser }) => {
  await login(page, 'reception')
  await page.goto('/orders/new')

  await page.getByRole('button', { name: '+ Cliente nuevo' }).click()
  const name = `Cliente E2E ${Date.now()}`
  await page.getByLabel('Nombre y apellido').fill(name)
  await page.getByLabel('Teléfono / WhatsApp').fill(uniquePhone())
  await page.getByRole('button', { name: 'Crear cliente y seguir' }).click()

  await page.getByLabel('Marca').fill('Samsung')
  await page.getByLabel('Modelo').fill('Galaxy A54')
  await page.getByRole('button', { name: 'Agregar equipo y seguir' }).click()

  await page.getByLabel('Falla que reporta el cliente').fill('Pantalla rota, no da imagen')
  await page.getByRole('button', { name: 'Crear orden' }).click()

  await expect(page.getByText('Orden creada')).toBeVisible()
  const code = (await page.locator('p.text-4xl').textContent())?.trim() ?? ''
  expect(code).toMatch(/^#\d+$/)
  const trackingUrl = await page.getByRole('link', { name: /\/t\// }).getAttribute('href')
  expect(trackingUrl).toBeTruthy()

  await page.getByRole('button', { name: 'Ver orden' }).click()
  await expect(page.getByRole('heading', { name: new RegExp(code) })).toBeVisible()
  await expect(page.getByText('Recibido').first()).toBeVisible()

  // The public portal needs no login.
  const anonymous = await browser.newContext()
  const portal = await anonymous.newPage()
  await portal.goto(new URL(trackingUrl!).pathname)
  await expect(portal.getByText(code)).toBeVisible()
  await expect(portal.getByText('Samsung Galaxy A54')).toBeVisible()
  await expect(portal.getByText('Pantalla rota, no da imagen')).toBeVisible()
  await anonymous.close()
})

test('technicians only see the workshop sections', async ({ page }) => {
  await login(page, 'tech')
  const nav = page.getByRole('navigation', { name: 'Principal' }).first()
  await expect(nav.getByRole('link', { name: 'Tablero' })).toBeVisible()
  await expect(nav.getByRole('link', { name: 'Configuración' })).toHaveCount(0)
  await expect(nav.getByRole('link', { name: 'Punto de venta' })).toHaveCount(0)

  await page.goto('/settings')
  await expect(page.getByText('No tenés permiso para ver esta sección.')).toBeVisible()
})
