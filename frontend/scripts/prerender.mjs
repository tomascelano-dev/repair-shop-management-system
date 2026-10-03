// Writes dist/<page>.html for the public pages (see src/prerender.tsx). The web server serves them for
// /precios, /terminos, /privacidad and /reembolsos; everything else keeps getting the SPA shell (index.html).
import { mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import { build } from 'vite'
import { dirname } from 'node:path'

const SHELL_ROOT = '<div id="root" class="h-full"></div>'
const escape = (s) => s.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;')

await build({ logLevel: 'warn', build: { ssr: 'src/prerender.tsx', outDir: 'dist-ssr', emptyOutDir: true } })
const { PUBLIC_PAGES, render, CONTACT_MARKER, SELLER_MARKER } = await import(new URL('../dist-ssr/prerender.js', import.meta.url).href)

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

for (const page of PUBLIC_PAGES) {
  const markup = fillServerSide(render(page), page.locale)
  if (!markup.includes(page.expect)) throw new Error(`${page.path}: the pre-rendered page does not contain "${page.expect}"`)
  const html = shell
    .replace(/<html lang="[^"]*"/, `<html lang="${page.locale}"`)
    .replace(SHELL_ROOT, `<div id="root" class="h-full">${markup}</div>`)
    .replace(/<title>.*?<\/title>/, `<title>${escape(page.title)}</title>`)
    .replace(/<meta name="description" content="[^"]*" \/>/, `<meta name="description" content="${escape(page.description)}" />`)
  await mkdir(dirname(`dist/${page.file}`), { recursive: true })
  await writeFile(`dist/${page.file}`, html)
  console.log(`prerendered ${page.path} -> dist/${page.file} (${markup.length} chars)`)
}

await rm('dist-ssr', { recursive: true, force: true })
