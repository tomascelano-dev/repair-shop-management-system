// Writes dist/<page>.html for the public pages (see src/prerender.tsx). The web server serves them for
// /precios, /terminos, /privacidad and /reembolsos; everything else keeps getting the SPA shell (index.html).
import { readFile, rm, writeFile } from 'node:fs/promises'
import { build } from 'vite'

const SHELL_ROOT = '<div id="root" class="h-full"></div>'
const escape = (s) => s.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;')

await build({ logLevel: 'warn', build: { ssr: 'src/prerender.tsx', outDir: 'dist-ssr', emptyOutDir: true } })
const { PUBLIC_PAGES, render } = await import(new URL('../dist-ssr/prerender.js', import.meta.url).href)

const shell = await readFile('dist/index.html', 'utf8')
if (!shell.includes(SHELL_ROOT)) throw new Error(`dist/index.html has no ${SHELL_ROOT}`)

for (const page of PUBLIC_PAGES) {
  const markup = render(page)
  if (!markup.includes(page.expect)) throw new Error(`${page.path}: the pre-rendered page does not contain "${page.expect}"`)
  const html = shell
    .replace(SHELL_ROOT, `<div id="root" class="h-full">${markup}</div>`)
    .replace(/<title>.*?<\/title>/, `<title>${escape(page.title)}</title>`)
    .replace(/<meta name="description" content="[^"]*" \/>/, `<meta name="description" content="${escape(page.description)}" />`)
  await writeFile(`dist/${page.file}`, html)
  console.log(`prerendered ${page.path} -> dist/${page.file} (${markup.length} chars)`)
}

await rm('dist-ssr', { recursive: true, force: true })
