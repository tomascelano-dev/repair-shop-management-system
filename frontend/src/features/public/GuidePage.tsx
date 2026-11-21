import { Link, useLocation } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { billingApi } from '../../api/endpoints'
import { GUIDE_SECTIONS, publicPath } from '../../lib/publicLocale'
import { GUIDES, type Guide } from './guides'
import { PublicLayout } from './PublicLayout'

const linkClass = 'font-semibold text-brand-700 underline'

/** A long-tail guide (features/public/guides.ts), picked by the current path. Spanish only. */
export function GuidePage() {
  const { pathname } = useLocation()
  const guide = Object.values(GUIDES).find((g) => publicPath('es', g.section) === pathname) ?? GUIDES['guide-phones']
  const config = useQuery({ queryKey: ['billing-config'], queryFn: billingApi.config, staleTime: 5 * 60_000 })
  const days = config.data?.trialDays ?? 14
  return (
    <PublicLayout title={guide.heading} documentTitle={guide.title}>
      <main className="mx-auto max-w-3xl px-4 pb-12">
        <article>
          <header className="py-10 md:py-14">
            <p className="text-sm font-semibold uppercase tracking-wide text-brand-700">{guide.eyebrow}</p>
            <h1 className="mt-3 text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">{guide.heading}</h1>
            <p className="mt-5 text-lg leading-relaxed text-slate-600">{guide.intro}</p>
            <CallToAction days={days} />
          </header>
          {guide.blocks.map((block) => (
            <section key={block.heading} className="mt-10">
              <h2 className="text-xl font-bold tracking-tight text-slate-900">{block.heading}</h2>
              {block.paragraphs?.map((p) => <p key={p} className="mt-3 leading-relaxed text-slate-700">{p}</p>)}
              {block.items ? (
                <ul className="mt-3 list-disc space-y-2 pl-6 leading-relaxed text-slate-700">
                  {block.items.map((item) => <li key={item}>{item}</li>)}
                </ul>
              ) : null}
              {block.related ? (
                <p className="mt-3 text-sm text-slate-600">
                  Más detalle: <Link to={publicPath('es', block.related)} className={linkClass}>{GUIDES[block.related].linkText}</Link>
                </p>
              ) : null}
            </section>
          ))}
          <section className="mt-14">
            <h2 className="text-2xl font-bold tracking-tight text-slate-900">Preguntas frecuentes</h2>
            <div className="mt-5 space-y-3">
              {guide.faq.map(([q, a]) => (
                <details key={q} className="rounded-xl bg-white p-5 ring-1 ring-slate-200">
                  <summary className="cursor-pointer font-semibold text-slate-900">{q}</summary>
                  <p className="mt-3 text-sm leading-relaxed text-slate-600">{a}</p>
                </details>
              ))}
            </div>
          </section>
          <section className="mt-12 rounded-2xl bg-white p-6 ring-1 ring-slate-200">
            <h2 className="text-xl font-bold tracking-tight text-slate-900">Pruébalo en tu taller</h2>
            <p className="mt-2 leading-relaxed text-slate-600">
              {days} días gratis con todos los módulos, sin tarjeta y con usuarios ilimitados. Después eliges el plan según la cantidad de sucursales.
            </p>
            <CallToAction days={days} />
          </section>
        </article>
        <OtherGuides current={guide} />
      </main>
    </PublicLayout>
  )
}

function CallToAction({ days }: { days: number }) {
  return (
    <div className="mt-6 flex flex-wrap gap-4">
      <Link to={publicPath('es', 'signup')} className="rounded-lg bg-brand-600 px-5 py-3 font-semibold text-white hover:bg-brand-700">Probar {days} días gratis</Link>
      <Link to={publicPath('es', 'pricing')} className="rounded-lg border border-slate-300 px-5 py-3 font-semibold text-slate-700">Ver planes y precios</Link>
    </div>
  )
}

function OtherGuides({ current }: { current: Guide }) {
  return (
    <nav aria-label="Otras guías" className="mt-12 border-t border-slate-200 pt-8">
      <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">También te puede servir</h2>
      <ul className="mt-3 space-y-2">
        {GUIDE_SECTIONS.filter((s) => s !== current.section).map((s) => (
          <li key={s}><Link to={publicPath('es', s)} className={linkClass}>{GUIDES[s].linkText}</Link></li>
        ))}
      </ul>
    </nav>
  )
}
