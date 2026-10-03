import { expect, test } from '@playwright/test'

test('an ad landing keeps campaign data through English signup and waits for consent', async ({ page }) => {
  let tags = 0
  for (const url of ['https://www.googletagmanager.com/**', 'https://connect.facebook.net/**']) {
    await page.route(url, async (route) => { tags++; await route.fulfill({ contentType: 'application/javascript', body: '' }) })
  }
  await page.route('**/api/v1/billing/config', async (route) => {
    const response = await route.fetch()
    const body = await response.json()
    body.data.tracking = { ga4Id: 'G-TEST', googleAdsId: 'AW-123', googleAdsSignupLabel: 'signup', googleAdsPurchaseLabel: 'paid', metaPixelId: '12345' }
    await route.fulfill({ response, json: body })
  })
  await page.goto('/?utm_source=google&utm_campaign=global&gclid=test_click')
  await expect(page.getByRole('heading', { name: 'Más reparaciones. Menos papeleo.' })).toBeVisible()
  await page.getByRole('link', { name: 'English', exact: true }).click()
  await expect(page).toHaveURL(/\/en\?utm_source=google/)
  await expect(page.getByRole('heading', { name: 'More repairs. Less paperwork.' })).toBeVisible()
  await expect(page.locator('html')).toHaveAttribute('lang', 'en')
  expect(tags).toBe(0)
  await page.getByRole('button', { name: 'Accept optional cookies' }).click()
  await expect.poll(() => tags).toBe(2)
  await page.getByRole('link', { name: 'Try for free', exact: true }).click()
  await page.getByLabel('Shop name').fill('Global E2E workshop')
  await page.getByLabel('Your name').fill('Global Owner')
  await page.getByLabel('Email', { exact: true }).fill(`global${Date.now()}@example.com`)
  await page.getByLabel('Password', { exact: true }).fill('Clave12345')
  await page.getByLabel('Country', { exact: true }).selectOption('US')
  await page.getByRole('checkbox').check()
  const request = page.waitForRequest((r) => r.method() === 'POST' && r.url().endsWith('/auth/signup'))
  await page.getByRole('button', { name: 'Create my account' }).click()
  const submitted = (await request).postDataJSON()
  expect(submitted.attribution).toMatchObject({ utmSource: 'google', utmCampaign: 'global', gclid: 'test_click', adConsent: true })
  expect(submitted.attribution.eventId).toMatch(/^trial_/)
  await expect(page.getByText(/Prueba gratis: te quedan 14 días/)).toBeVisible()
  const trialId = await page.evaluate(() => window.fbq?.queue.find((c) => c[1] === 'StartTrial')?.[3])
  expect(trialId).toEqual({ eventID: submitted.attribution.eventId })
})

test('visitors can reject cookies, switch legal languages and reopen their preferences', async ({ page }) => {
  await page.goto('/en/privacy')
  await expect(page.getByRole('heading', { name: 'Privacy policy', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Cookie settings', exact: true }).click()
  await page.getByRole('button', { name: 'Only necessary' }).click()
  await expect(page.locator('#rs-google-tag, #rs-meta-pixel')).toHaveCount(0)
  await page.reload()
  await expect(page.getByRole('complementary', { name: 'Cookie preferences' })).toHaveCount(0)
  await page.getByRole('link', { name: 'Español', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Política de privacidad', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Preferencias de cookies', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Solo necesarias' })).toBeVisible()
})

test('English prices use the international currency and all legal pages are reachable', async ({ page }) => {
  await page.goto('/en/pricing')
  await page.getByLabel('Prices for').selectOption('US')
  await expect(page.getByRole('heading', { name: 'Professional', exact: true })).toBeVisible()
  await expect(page.getByText('$25', { exact: true })).toBeVisible()
  for (const [link, heading] of [['Terms', 'Terms of service'], ['Privacy', 'Privacy policy'], ['Refunds', 'Refund policy']]) {
    await page.getByRole('contentinfo').getByRole('link', { name: link, exact: true }).click()
    await expect(page.getByRole('heading', { name: heading, exact: true })).toBeVisible()
  }
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/en')
  await expect(page.getByRole('heading', { name: 'More repairs. Less paperwork.' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
  await page.screenshot({ path: 'test-results/landing-en-mobile.png', fullPage: true })
})
