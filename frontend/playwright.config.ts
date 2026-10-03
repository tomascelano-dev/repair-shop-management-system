import { defineConfig, devices } from '@playwright/test'

// End-to-end tests run against a real API + database.
// Start the backend first (see README), then: npm run test:e2e
//   E2E_BASE_URL     use an already running frontend instead of starting Vite
//   VITE_PROXY_TARGET backend URL for the Vite dev server (default http://localhost:8080)
//   PW_CHROMIUM_EXECUTABLE use a preinstalled Chromium instead of the Playwright download
const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:5173'
const executablePath = process.env.PW_CHROMIUM_EXECUTABLE || undefined

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL,
    locale: 'es-AR',
    timezoneId: 'America/Argentina/Buenos_Aires',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    launchOptions: executablePath ? { executablePath } : undefined,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: 'npm run dev -- --port 5173 --strictPort',
        url: 'http://localhost:5173',
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
      },
})
