import { expect, test } from '@playwright/test'

// A visitor signs up from the pricing page, works during the trial and picks a plan.
// In development the subscription is activated by the simulated provider (no real charge).
test('a new shop signs up, gets the trial and picks a plan', async ({ page }) => {
  await page.goto('/precios')
  await expect(page.getByRole('heading', { name: 'El sistema para tu servicio técnico' })).toBeVisible()
  await expect(page.getByText('Estándar').first()).toBeVisible()
  await page.screenshot({ path: 'test-results/precios.png', fullPage: true })

  await page.getByRole('link', { name: 'Prueba gratis', exact: true }).click()
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

// Paddle reviews the domain before approving the account: it must show the product and prices and link these pages.
test('the bare domain shows the landing page, which links terms, privacy and refund policy', async ({ page }) => {
  await page.goto('/')
  await expect(page).toHaveURL(/\/$/)
  await expect(page.getByRole('heading', { name: 'Tu taller de reparaciones, ordenado y sin papeles' })).toBeVisible()
  await expect(page.getByText('Profesional').first()).toBeVisible()
  await page.screenshot({ path: 'test-results/inicio.png', fullPage: true })
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

test('the English website mirrors the Spanish one and links back to it', async ({ page }) => {
  await page.goto('/en')
  await expect(page.getByRole('heading', { name: 'Run your repair shop without the paperwork' })).toBeVisible()
  await expect(page.getByText('Professional').first()).toBeVisible()
  await page.screenshot({ path: 'test-results/en.png', fullPage: true })

  await page.getByRole('contentinfo').getByRole('link', { name: 'Refunds' }).click()
  await expect(page).toHaveURL(/\/en\/refunds$/)
  await expect(page.getByRole('heading', { name: 'Refund Policy' })).toBeVisible()

  await page.getByRole('link', { name: 'Español' }).click()
  await expect(page).toHaveURL(/\/reembolsos$/)
  await expect(page.getByRole('heading', { name: 'Política de reembolsos' })).toBeVisible()
})

// A visitor from an ad: the campaign parameters travel with the signup so the shop can be traced to the campaign.
test('a signup from an ad carries its campaign', async ({ page }) => {
  await page.goto('/en?utm_source=google&utm_medium=cpc&utm_campaign=e2e-campaign&gclid=e2e-click')
  await page.getByRole('link', { name: 'Start your 14-day free trial' }).first().click()
  await expect(page).toHaveURL(/\/en\/signup$/)
  await page.getByLabel('Shop name').fill('Ad Shop E2E')
  await page.getByLabel('Your name').fill('Owner E2E')
  await page.getByLabel('Email').fill(`ads${Date.now()}@example.com`)
  await page.getByLabel('Password').fill('Clave12345')
  await page.getByLabel('Country').selectOption('US')
  await page.getByRole('checkbox').check()
  await page.screenshot({ path: 'test-results/en-signup.png', fullPage: true })

  const request = page.waitForRequest((r) => r.url().endsWith('/auth/signup') && r.method() === 'POST')
  await page.getByRole('button', { name: 'Create my account' }).click()
  const body = (await request).postDataJSON()
  expect(body.attribution).toMatchObject({ utmSource: 'google', utmMedium: 'cpc', utmCampaign: 'e2e-campaign', gclid: 'e2e-click', landingPath: '/en' })
  expect(body.attribution.eventId).toBeTruthy()
  await expect(page.getByText(/Prueba gratis: te quedan 14 días/)).toBeVisible()
})
