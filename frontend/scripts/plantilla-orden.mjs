// Regenerates the free order template (lead magnet for /plantilla-orden-de-servicio-tecnico) from
// scripts/plantilla-orden.html: an A4 PDF to download and a PNG preview for the page. Run by hand after editing
// the HTML (the outputs are committed):  node scripts/plantilla-orden.mjs
import { chromium } from '@playwright/test'
import { readFile, mkdir } from 'node:fs/promises'

const html = await readFile(new URL('./plantilla-orden.html', import.meta.url), 'utf8')
const out = new URL('../public/plantillas/', import.meta.url)
await mkdir(out, { recursive: true })

const browser = await chromium.launch({ executablePath: process.env.PW_CHROMIUM_EXECUTABLE || undefined })
const page = await browser.newPage({ viewport: { width: 794, height: 1123 }, deviceScaleFactor: 1.5 })
await page.setContent(html, { waitUntil: 'load' })
await page.pdf({ path: new URL('orden-de-servicio-tecnico.pdf', out).pathname.replace(/^\/(\w:)/, '$1'), format: 'A4', printBackground: true, scale: 0.95, // one A4 sheet

  margin: { top: '10mm', right: '10mm', bottom: '10mm', left: '10mm' } })
// Preview of the top of the sheet (what a visitor needs to recognize it).
await page.addStyleTag({ content: 'body { padding: 10mm; }' })
await page.screenshot({ path: new URL('orden-de-servicio-tecnico.png', out).pathname.replace(/^\/(\w:)/, '$1'), fullPage: true, type: 'png' })
const pages = await page.evaluate(() => document.body.scrollHeight)
await browser.close()
console.log(`plantilla: PDF y PNG en public/plantillas/ (alto ${pages}px)`)
