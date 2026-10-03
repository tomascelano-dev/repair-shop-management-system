import { expect, test } from '@playwright/test'

// A visitor signs up from the pricing page, works during the trial and picks a plan.
// In development the subscription is activated by the simulated provider (no real charge).
test('a new shop signs up, gets the trial and picks a plan', async ({ page }) => {
  await page.goto('/precios')
  await expect(page.getByRole('heading', { name: 'El sistema para tu servicio técnico' })).toBeVisible()
  await expect(page.getByText('Estándar').first()).toBeVisible()
  await page.screenshot({ path: 'test-results/precios.png', fullPage: true })

  await page.getByRole('link', { name: 'Probar gratis' }).click()
  await page.getByLabel('Nombre del taller').fill('Taller E2E')
  await page.getByLabel('Tu nombre').fill('Dueño E2E')
  await page.getByLabel('Email').fill(`dueno${Date.now()}@example.com`)
  await page.getByLabel('Contraseña').fill('Clave12345')
  await page.getByLabel('País').selectOption('AR')
  await page.getByRole('checkbox').check()
  await page.screenshot({ path: 'test-results/registro.png', fullPage: true })
  await page.getByRole('button', { name: 'Crear mi cuenta' }).click()

  await expect(page.getByText(/Prueba gratis: te quedan 14 días/)).toBeVisible()
  await expect(page.getByText(/Confirmá tu email/)).toBeVisible()
  await page.screenshot({ path: 'test-results/inicio-prueba.png', fullPage: true })

  await page.getByRole('link', { name: 'Suscripción' }).first().click()
  await expect(page.getByRole('heading', { name: 'Suscripción' })).toBeVisible()
  await page.screenshot({ path: 'test-results/suscripcion.png', fullPage: true })

  await page.getByRole('button', { name: 'Elegir este plan' }).nth(1).click()
  await expect(page.getByText('Plan actualizado.')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Plan actual' })).toBeVisible()
  await expect(page.getByText(/Prueba gratis: te quedan/)).toHaveCount(0)
})

// Paddle reviews these pages before approving the account: they must be public and linked from the pricing page.
test('terms, privacy and refund policy are public and linked from the pricing page', async ({ page }) => {
  await page.goto('/precios')
  const footer = page.getByRole('contentinfo')
  for (const [link, heading] of [
    ['Términos', 'Términos del servicio'],
    ['Privacidad', 'Política de privacidad'],
    ['Reembolsos', 'Política de reembolsos'],
  ]) {
    await footer.getByRole('link', { name: link }).click()
    await expect(page.getByRole('heading', { name: heading })).toBeVisible()
  }
  await expect(page.getByText(/dentro de los 14 días corridos/)).toBeVisible()
  await page.screenshot({ path: 'test-results/reembolsos.png', fullPage: true })
})
