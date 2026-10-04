import { Link } from 'react-router-dom'
import { PricingSection } from './PricingPage'
import { PUBLIC_ROUTES, useLang } from './lang'
import { PublicLayout } from './PublicLayout'

interface Copy {
  title: string
  eyebrow: string
  heading: string
  lead: string
  cta: string
  seePrices: string
  ctaNote: string
  featuresTitle: string
  features: { title: string; text: string }[]
  stepsTitle: string
  steps: { title: string; text: string }[]
  pricingTitle: string
  faqTitle: string
  faq: { q: string; a: string }[]
  closingTitle: string
  closingText: string
}

const COPY: Record<'es' | 'en', Copy> = {
  es: {
    title: 'RepairShop · Software para servicio técnico',
    eyebrow: 'Software para servicio técnico de celulares, computadoras y electrónica',
    heading: 'Tu taller de reparaciones, ordenado y sin papeles',
    lead: 'Registra cada equipo, envía presupuestos que el cliente aprueba desde el celular, avisa por WhatsApp cuando está listo y controla el stock y la caja. Todo en un solo sistema.',
    cta: 'Prueba gratis 14 días',
    seePrices: 'Ver precios',
    ctaNote: 'Sin tarjeta. Usuarios ilimitados. Cancela cuando quieras.',
    featuresTitle: 'Todo lo que pasa en el mostrador y en el banco de trabajo',
    features: [
      {
        title: 'Órdenes de trabajo completas',
        text: 'Ingreso con fotos, accesorios, patrón de desbloqueo y firma del cliente. Cada equipo con su número, su historial y su garantía.',
      },
      {
        title: 'Presupuestos que se aprueban solos',
        text: 'El cliente recibe un link, ve el presupuesto y lo aprueba o rechaza desde el celular, sin llamadas ni mensajes de ida y vuelta.',
      },
      {
        title: 'Avisos por WhatsApp, email y SMS',
        text: 'Mensajes automáticos cuando el equipo ingresa, cuando hay presupuesto y cuando está listo para retirar.',
      },
      {
        title: 'Tablero del taller',
        text: 'Mira de un vistazo qué está en diagnóstico, qué espera repuestos y qué está listo, y quién está trabajando en cada equipo.',
      },
      {
        title: 'Punto de venta, caja e inventario',
        text: 'Vende accesorios y repuestos con lector de código de barras, controla el stock y cierra la caja todos los días.',
      },
      {
        title: 'Varias sucursales y reportes',
        text: 'Maneja más de un local con su propio stock, transfiere entre sucursales y exporta los reportes a Excel.',
      },
    ],
    stepsTitle: 'Empiezas a trabajar el mismo día',
    steps: [
      { title: 'Crea tu cuenta', text: 'Nombre del taller, tu email y una contraseña. No pedimos tarjeta.' },
      { title: 'Carga tu primera orden', text: 'Los mensajes a clientes y las plantillas ya vienen listos. Invita a tu equipo sin costo extra.' },
      { title: 'Elige un plan cuando quieras', text: 'Tienes 14 días con todos los módulos. Si no eliges un plan, la cuenta queda en modo consulta.' },
    ],
    pricingTitle: 'Precios simples, sin costo por usuario',
    faqTitle: 'Preguntas frecuentes',
    faq: [
      {
        q: '¿Necesito instalar algo?',
        a: 'No. Funciona en el navegador de la computadora, la tablet o el celular, y se puede instalar como aplicación desde el navegador.',
      },
      {
        q: '¿Qué pasa cuando termina la prueba gratis?',
        a: 'Si eliges un plan, sigues trabajando sin perder nada. Si no, tus datos quedan guardados en modo consulta hasta que te suscribas.',
      },
      {
        q: '¿Cómo se paga?',
        a: 'En Argentina, en pesos con Mercado Pago. En el resto de los países, en dólares con tarjeta a través de Paddle, que suma los impuestos de tu país.',
      },
      {
        q: '¿Puedo cancelar cuando quiera?',
        a: 'Sí, desde la sección Suscripción. Y si no te sirve, te devolvemos el primer pago dentro de los 14 días.',
      },
      {
        q: '¿Mis datos están seguros?',
        a: 'Cada taller tiene sus datos separados, las conexiones van cifradas y hacemos copias de seguridad todos los días.',
      },
    ],
    closingTitle: 'Prueba RepairShop en tu taller',
    closingText: '14 días gratis con todos los módulos, sin tarjeta.',
  },
  en: {
    title: 'RepairShop · Repair shop management software',
    eyebrow: 'Software for phone, computer and electronics repair shops',
    heading: 'Run your repair shop without the paperwork',
    lead: 'Check in every device, send quotes your customers approve from their phone, notify them on WhatsApp when it is ready, and keep stock and cash under control. All in one system.',
    cta: 'Start your 14-day free trial',
    seePrices: 'See pricing',
    ctaNote: 'No credit card. Unlimited users. Cancel anytime.',
    featuresTitle: 'Everything that happens at the counter and on the bench',
    features: [
      {
        title: 'Complete repair orders',
        text: 'Check-in with photos, accessories, unlock pattern and customer signature. Every device with its number, history and warranty.',
      },
      {
        title: 'Quotes customers approve themselves',
        text: 'Your customer gets a link, sees the quote and approves or declines it from their phone, without back-and-forth calls.',
      },
      {
        title: 'WhatsApp, email and SMS notifications',
        text: 'Automatic messages when a device is checked in, when the quote is ready and when it can be picked up.',
      },
      {
        title: 'Workshop board',
        text: 'See at a glance what is being diagnosed, what is waiting for parts and what is ready, and who is working on each device.',
      },
      {
        title: 'Point of sale, cash register and inventory',
        text: 'Sell accessories and parts with a barcode scanner, keep stock under control and close the register every day.',
      },
      {
        title: 'Multiple branches and reports',
        text: 'Run more than one store with its own stock, transfer between branches and export reports to Excel.',
      },
    ],
    stepsTitle: 'Start working the same day',
    steps: [
      { title: 'Create your account', text: 'Shop name, your email and a password. No credit card required.' },
      { title: 'Enter your first repair', text: 'Customer messages and templates come ready to use. Invite your team at no extra cost.' },
      { title: 'Pick a plan whenever you are ready', text: 'You get 14 days with every module. If you do not pick a plan, the account becomes read-only.' },
    ],
    pricingTitle: 'Simple pricing, no per-user fees',
    faqTitle: 'Frequently asked questions',
    faq: [
      {
        q: 'Do I need to install anything?',
        a: 'No. It runs in the browser on your computer, tablet or phone, and you can install it as an app from the browser.',
      },
      {
        q: 'What happens when the free trial ends?',
        a: 'If you pick a plan, you keep working without losing anything. If not, your data stays available in read-only mode until you subscribe.',
      },
      {
        q: 'How do I pay?',
        a: 'By card in US dollars through Paddle, which adds the taxes that apply in your country. In Argentina, in pesos through Mercado Pago.',
      },
      {
        q: 'Can I cancel anytime?',
        a: 'Yes, from the Subscription section. And if it is not for you, we refund your first payment within 14 days.',
      },
      {
        q: 'Is the app available in English?',
        a: 'The app is in Spanish today. The website, signup and billing are available in English.',
      },
    ],
    closingTitle: 'Try RepairShop in your shop',
    closingText: '14 days free with every module, no credit card.',
  },
}

/** Landing page for visitors and ad campaigns (the bare domain in Spanish, /en in English). */
export function LandingPage({ initialCountry }: { initialCountry?: string } = {}) {
  const lang = useLang()
  const t = COPY[lang]
  const routes = PUBLIC_ROUTES[lang]

  return (
    <PublicLayout title={t.title}>
      <main>
        <section className="mx-auto max-w-4xl px-4 pb-12 pt-8 text-center sm:pt-14">
          <p className="text-sm font-medium text-brand-700">{t.eyebrow}</p>
          <h1 className="mt-3 text-3xl font-bold tracking-tight text-slate-900 sm:text-5xl">{t.heading}</h1>
          <p className="mx-auto mt-4 max-w-2xl text-lg text-slate-600">{t.lead}</p>
          <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
            <Link to={routes.signup} className="rounded-lg bg-brand-600 px-5 py-3 font-medium text-white shadow-sm hover:bg-brand-700">
              {t.cta}
            </Link>
            <Link to={routes.pricing} className="rounded-lg bg-white px-5 py-3 font-medium text-slate-900 shadow-sm ring-1 ring-slate-200 hover:bg-slate-50">
              {t.seePrices}
            </Link>
          </div>
          <p className="mt-3 text-sm text-slate-500">{t.ctaNote}</p>
        </section>

        <section className="bg-white py-14">
          <div className="mx-auto max-w-6xl px-4">
            <h2 className="text-center text-2xl font-bold text-slate-900">{t.featuresTitle}</h2>
            <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {t.features.map((f) => (
                <div key={f.title} className="rounded-2xl bg-slate-50 p-5 ring-1 ring-slate-200">
                  <h3 className="font-semibold text-slate-900">{f.title}</h3>
                  <p className="mt-2 text-sm text-slate-600">{f.text}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-5xl px-4 py-14">
          <h2 className="text-center text-2xl font-bold text-slate-900">{t.stepsTitle}</h2>
          <ol className="mt-8 grid gap-4 sm:grid-cols-3">
            {t.steps.map((s, i) => (
              <li key={s.title} className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <span className="flex h-8 w-8 items-center justify-center rounded-full bg-brand-600 text-sm font-bold text-white">{i + 1}</span>
                <h3 className="mt-3 font-semibold text-slate-900">{s.title}</h3>
                <p className="mt-1 text-sm text-slate-600">{s.text}</p>
              </li>
            ))}
          </ol>
        </section>

        <section className="mx-auto max-w-6xl px-4 pb-14">
          <h2 className="pb-2 text-center text-2xl font-bold text-slate-900">{t.pricingTitle}</h2>
          <PricingSection lang={lang} initialCountry={initialCountry} />
        </section>

        <section className="bg-white py-14">
          <div className="mx-auto max-w-3xl px-4">
            <h2 className="text-center text-2xl font-bold text-slate-900">{t.faqTitle}</h2>
            <dl className="mt-8 space-y-5">
              {t.faq.map((f) => (
                <div key={f.q}>
                  <dt className="font-semibold text-slate-900">{f.q}</dt>
                  <dd className="mt-1 text-sm text-slate-600">{f.a}</dd>
                </div>
              ))}
            </dl>
          </div>
        </section>

        <section className="mx-auto max-w-4xl px-4 py-14 text-center">
          <h2 className="text-2xl font-bold text-slate-900">{t.closingTitle}</h2>
          <p className="mt-2 text-slate-600">{t.closingText}</p>
          <Link to={routes.signup} className="mt-6 inline-block rounded-lg bg-brand-600 px-5 py-3 font-medium text-white shadow-sm hover:bg-brand-700">
            {t.cta}
          </Link>
        </section>
      </main>
    </PublicLayout>
  )
}
