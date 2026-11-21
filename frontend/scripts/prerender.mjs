// Writes dist/<page>.html for the public pages (see src/prerender.tsx). The web server serves them for
// /precios, /terminos, /privacidad and /reembolsos; everything else keeps getting the SPA shell (index.html).
import { mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import { build } from 'vite'
import { dirname } from 'node:path'

const SHELL_ROOT = '<div id="root" class="h-full"></div>'
const escape = (s) => s.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;')

await build({ logLevel: 'warn', build: { ssr: 'src/prerender.tsx', outDir: 'dist-ssr', emptyOutDir: true } })
const { PUBLIC_PAGES, SEO_PRICES, render, CONTACT_MARKER, SELLER_MARKER } = await import(new URL('../dist-ssr/prerender.js', import.meta.url).href)

// ===== SEO =====
// Absolute URLs (canonical, hreflang, Open Graph, sitemap) need the public domain, which only the server knows:
// Caddy fills [[ . ]] with PUBLIC_ORIGIN (e.g. https://app.techxto.ar). Without it those tags are left out.
const ORIGIN_OPEN = '[[with env "PUBLIC_ORIGIN"]]'
const ORIGIN_CLOSE = '[[end]]'
const url = (path) => `[[.]]${path === '/' ? '/' : path}`
const OG_LOCALE = { es: 'es_AR', en: 'en_US' }

function structuredData(page) {
  const app = {
    '@type': 'SoftwareApplication',
    name: 'RepairShop',
    applicationCategory: 'BusinessApplication',
    applicationSubCategory: page.locale === 'en' ? 'Repair shop management software' : 'Sistema para servicio técnico',
    operatingSystem: 'Web',
    inLanguage: page.locale,
    description: page.description,
    url: '__URL__',
    offers: {
      '@type': 'AggregateOffer',
      priceCurrency: SEO_PRICES.currency,
      lowPrice: String(SEO_PRICES.low),
      highPrice: String(SEO_PRICES.high),
      offerCount: String(SEO_PRICES.count),
    },
  }
  const json = JSON.stringify({ '@context': 'https://schema.org', ...app })
  // Go templates use [[ ]]: the JSON must not contain them (it has no nested arrays).
  if (json.includes('[[') || json.includes(']]')) throw new Error('structured data collides with the template delimiters')
  return json.replace('"__URL__"', `"${url(page.path)}"`)
}

function seoHead(page) {
  const alternates = Object.entries(page.alternates)
    .map(([lang, path]) => `<link rel="alternate" hreflang="${lang}" href="${url(path)}" />`)
    .join('') + `<link rel="alternate" hreflang="x-default" href="${url(page.alternates.es)}" />`
  const withOrigin = [
    `<link rel="canonical" href="${url(page.path)}" />`,
    alternates,
    `<meta property="og:url" content="${url(page.path)}" />`,
    `<meta property="og:image" content="[[.]]/og.png" />`,
    `<meta property="og:image:width" content="1200" /><meta property="og:image:height" content="630" />`,
    `<meta name="twitter:image" content="[[.]]/og.png" />`,
    page.section === 'home' || page.section === 'pricing' ? `<script type="application/ld+json">${structuredData(page)}</script>` : '',
  ].join('')
  return [
    '<meta name="robots" content="index, follow, max-image-preview:large" />',
    `<meta property="og:type" content="website" />`,
    `<meta property="og:site_name" content="RepairShop" />`,
    `<meta property="og:locale" content="${OG_LOCALE[page.locale]}" />`,
    `<meta property="og:title" content="${escape(page.title)}" />`,
    `<meta property="og:description" content="${escape(page.description)}" />`,
    `<meta name="twitter:card" content="summary_large_image" />`,
    ORIGIN_OPEN + withOrigin + ORIGIN_CLOSE,
  ].join('\n    ')
}

const NOINDEX = '<meta name="robots" content="noindex, nofollow" />'

// The web server (Caddy `templates`, delimiters [[ ]]) fills in CONTACT_EMAIL and LEGAL_NAME from its environment
// on every request, falling back to the same text the app shows when they are not set.
const contactLink = new RegExp(`<a href="mailto:${CONTACT_MARKER}"([^>]*)>${CONTACT_MARKER}</a>`, 'g')
const contactTemplate =
  `[[with env "CONTACT_EMAIL"]]<a href="mailto:[[html .]]"$1>[[html .]]</a>` +
  `[[else]]<a href="https://techxto.ar" target="_blank" rel="noreferrer"$1>los canales publicados en techxto.ar</a>[[end]]`
const sellerText = ` El servicio lo presta ${SELLER_MARKER}.`
const sellerTemplate = `[[with env "LEGAL_NAME"]] El servicio lo presta [[html .]].[[end]]`

function fillServerSide(markup, locale) {
  const englishContact = contactTemplate.replace('los canales publicados en techxto.ar', 'the contact channels at techxto.ar')
  const out = markup.replace(contactLink, locale === 'en' ? englishContact : contactTemplate)
    .replaceAll(sellerText, sellerTemplate)
    .replaceAll(` The service is provided by ${SELLER_MARKER}.`, `[[with env "LEGAL_NAME"]] The service is provided by [[html .]].[[end]]`)
  if (out.includes(CONTACT_MARKER) || out.includes(SELLER_MARKER)) throw new Error('a contact or seller marker was left in the page')
  return out
}

const shell = await readFile('dist/index.html', 'utf8')
if (!shell.includes(SHELL_ROOT)) throw new Error(`dist/index.html has no ${SHELL_ROOT}`)
// The SPA shell (the app, the customer tracking portal /t/...) is never indexed; only the pre-rendered pages are.
if (!shell.includes(NOINDEX)) throw new Error(`dist/index.html must carry ${NOINDEX}`)

for (const page of PUBLIC_PAGES) {
  const markup = fillServerSide(render(page), page.locale)
  if (!markup.includes(page.expect)) throw new Error(`${page.path}: the pre-rendered page does not contain "${page.expect}"`)
  const html = shell
    .replace(/<html lang="[^"]*"/, `<html lang="${page.locale}"`)
    .replace(SHELL_ROOT, `<div id="root" class="h-full">${markup}</div>`)
    .replace(/<title>.*?<\/title>/, `<title>${escape(page.title)}</title>`)
    .replace(/<meta name="description" content="[^"]*" \/>/, `<meta name="description" content="${escape(page.description)}" />`)
    .replace(NOINDEX, seoHead(page))
  if (html.includes(NOINDEX)) throw new Error(`${page.path}: the public page kept the noindex of the app shell`)
  await mkdir(dirname(`dist/${page.file}`), { recursive: true })
  await writeFile(`dist/${page.file}`, html)
  console.log(`prerendered ${page.path} -> dist/${page.file} (${markup.length} chars)`)
}

// Sitemap of the public pages, each with its other-language version (filled with the domain by Caddy).
const today = new Date().toISOString().slice(0, 10)
const priority = { home: '1.0', pricing: '0.9' }
const entries = PUBLIC_PAGES.map((page) => [
  '  <url>',
  `    <loc>${url(page.path)}</loc>`,
  `    <lastmod>${today}</lastmod>`,
  `    <priority>${priority[page.section] ?? '0.3'}</priority>`,
  ...Object.entries(page.alternates).map(([lang, path]) => `    <xhtml:link rel="alternate" hreflang="${lang}" href="${url(path)}" />`),
  `    <xhtml:link rel="alternate" hreflang="x-default" href="${url(page.alternates.es)}" />`,
  '  </url>',
].join('\n'))
await writeFile('dist/sitemap.xml', [
  '<?xml version="1.0" encoding="UTF-8"?>',
  '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">',
  ORIGIN_OPEN,
  ...entries,
  ORIGIN_CLOSE,
  '</urlset>',
  '',
].join('\n'))
console.log(`sitemap.xml: ${PUBLIC_PAGES.length} pages`)

// robots.txt: everything public may be crawled (the app pages carry noindex); the API is not a page.
await writeFile('dist/robots.txt', [
  'User-agent: *',
  'Allow: /',
  'Disallow: /api/',
  '',
  `${ORIGIN_OPEN}Sitemap: [[.]]/sitemap.xml${ORIGIN_CLOSE}`,
  '',
].join('\n'))

await rm('dist-ssr', { recursive: true, force: true })
