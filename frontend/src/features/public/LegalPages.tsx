import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { billingApi } from '../../api/endpoints'
import { contactFallback, sellerSentence } from './legalText'
import { PUBLIC_ROUTES, useLang } from './lang'
import { PublicLayout } from './PublicLayout'

const UPDATED = { es: '3 de octubre de 2026', en: 'October 3, 2026' }

function useLegalConfig() {
  return useQuery({ queryKey: ['billing-config'], queryFn: billingApi.config, staleTime: 5 * 60_000 }).data
}

function Contact() {
  const lang = useLang()
  const email = useLegalConfig()?.contactEmail
  if (email)
    return (
      <a href={`mailto:${email}`} className="text-brand-700 underline">
        {email}
      </a>
    )
  return (
    <a href="https://techxto.ar" target="_blank" rel="noreferrer" className="text-brand-700 underline">
      {contactFallback(lang)}
    </a>
  )
}

function Seller() {
  const lang = useLang()
  const name = useLegalConfig()?.legalName
  return name ? <>{sellerSentence(lang, name)}</> : null
}

function PageLink({ page, children }: { page: 'refunds' | 'privacy'; children: ReactNode }) {
  return (
    <Link to={PUBLIC_ROUTES[useLang()][page]} className="text-brand-700 underline">
      {children}
    </Link>
  )
}

function LegalPage({ title, children }: { title: string; children: ReactNode }) {
  const lang = useLang()
  return (
    <PublicLayout title={`${title} · RepairShop`}>
      <main className="mx-auto max-w-3xl px-4 pb-6">
        <article className="space-y-3 rounded-2xl bg-white p-6 text-sm leading-relaxed text-slate-700 shadow-sm ring-1 ring-slate-200 sm:p-8 [&_h2]:pt-3 [&_h2]:font-semibold [&_h2]:text-slate-900">
          <p className="text-xs uppercase tracking-wide text-slate-500">
            {lang === 'en' ? 'Last updated' : 'Actualizado el'} {UPDATED[lang]}
          </p>
          <h1 className="text-2xl font-bold text-slate-900">{title}</h1>
          {children}
        </article>
      </main>
    </PublicLayout>
  )
}

export function TermsPage() {
  return useLang() === 'en' ? <TermsEn /> : <TermsEs />
}

export function PrivacyPage() {
  return useLang() === 'en' ? <PrivacyEn /> : <PrivacyEs />
}

export function RefundPage() {
  return useLang() === 'en' ? <RefundEn /> : <RefundEs />
}

// ---- Español -----------------------------------------------------------------------------------------------------

function TermsEs() {
  return (
    <LegalPage title="Términos del servicio">
      <p>
        RepairShop es un software de gestión para talleres de reparación que se ofrece por suscripción. Al crear una cuenta, aceptas estos términos en nombre
        propio y del taller que registras.
        <Seller />
      </p>
      <h2>1. Cuenta y uso</h2>
      <p>
        Eres responsable de la veracidad de los datos cargados, de mantener la confidencialidad de tus contraseñas y de las acciones que realicen los usuarios
        que invites. No puedes usar el servicio para actividades ilícitas ni para enviar comunicaciones no solicitadas.
      </p>
      <h2>2. Prueba gratis, planes y pagos</h2>
      <p>
        Cada cuenta nueva incluye una prueba gratis con todos los módulos y sin tarjeta. Al terminar, la cuenta queda en modo consulta hasta que elijas un plan
        mensual. En Argentina el abono se cobra en pesos con Mercado Pago. En el resto de los países lo cobra en dólares Paddle.com, que actúa como revendedor
        autorizado (merchant of record) y agrega los impuestos que correspondan en tu país. El abono se cobra por mes adelantado y puedes cancelarlo cuando
        quieras desde la sección Suscripción; la cancelación rige al final del período pagado. Los reintegros se rigen por la{' '}
        <PageLink page="refunds">Política de reembolsos</PageLink>.
      </p>
      <h2>3. Cambios en el servicio</h2>
      <p>Podemos incorporar, modificar o retirar funciones. Te avisaremos con anticipación cuando un cambio afecte funciones que usas o el precio de tu plan.</p>
      <h2>4. Tus datos</h2>
      <p>
        Los datos que cargas (clientes, equipos, órdenes, comprobantes) son de tu taller. Los tratamos según la{' '}
        <PageLink page="privacy">Política de privacidad</PageLink>, que también explica las cookies de medición del sitio público, y puedes pedir una copia o su
        eliminación en cualquier momento.
      </p>
      <h2>5. Facturación electrónica</h2>
      <p>
        La emisión de comprobantes ante ARCA (Argentina) se realiza con el certificado y el punto de venta de cada taller. Cada taller es responsable de sus
        obligaciones fiscales y de revisar los comprobantes que emite.
      </p>
      <h2>6. Disponibilidad y responsabilidad</h2>
      <p>
        Trabajamos para que el servicio esté disponible y realizamos copias de seguridad diarias, pero no garantizamos un funcionamiento ininterrumpido. En la
        medida que permita la ley, nuestra responsabilidad se limita al monto abonado en los últimos tres meses.
      </p>
      <h2>7. Cambios y contacto</h2>
      <p>
        Si modificamos estos términos, lo informaremos dentro del servicio. Para consultas, escríbenos a <Contact />. Estos términos se rigen por las leyes de
        la República Argentina, sin perjuicio de los derechos que te otorguen las normas de protección al consumidor de tu país.
      </p>
    </LegalPage>
  )
}

function PrivacyEs() {
  return (
    <LegalPage title="Política de privacidad">
      <p>
        Esta política explica qué datos trata RepairShop y cómo los protege, de acuerdo con la Ley 25.326 de Protección de Datos Personales de Argentina y, para
        personas en la Unión Europea, el Reglamento General de Protección de Datos (RGPD).
      </p>
      <h2>1. Qué datos tratamos</h2>
      <p>
        Datos de la cuenta (nombre, email, país y datos del taller) y los datos que el taller carga sobre sus clientes, equipos, órdenes, pagos y comprobantes.
        También registramos datos técnicos de uso, como accesos y errores, para operar y proteger el servicio. Al crear la cuenta guardamos además la dirección
        IP, el navegador y, si llegaste desde un anuncio o un enlace de campaña, sus parámetros (fuente, campaña e identificadores de clic de Google y Meta).
      </p>
      <h2>2. Para qué los usamos</h2>
      <p>
        Para prestar el servicio, enviar los avisos que el taller configura, cobrar la suscripción, brindar soporte y cumplir obligaciones legales. Si aceptas
        las cookies de publicidad, también para medir qué anuncios traen nuevos talleres. No vendemos datos personales.
      </p>
      <h2>3. Rol del taller</h2>
      <p>
        Respecto de los datos de sus clientes, cada taller es el responsable y RepairShop actúa como encargado del tratamiento: los procesamos solo para prestar
        el servicio al taller.
      </p>
      <h2>4. Con quién los compartimos</h2>
      <p>
        Con proveedores necesarios para operar: alojamiento, envío de emails, SMS y WhatsApp, cobro de la suscripción (Mercado Pago en Argentina y Paddle en el
        resto de los países) y ARCA para la facturación electrónica. Si aceptas las cookies de publicidad, Google (Google Analytics y Google Ads) y Meta (píxel
        y API de conversiones) reciben qué páginas del sitio público visitas y cuándo creas una cuenta o contratas un plan; a Meta le enviamos además tu email y
        tu país cifrados con SHA-256, tu IP y tu navegador, para que pueda atribuir la contratación al anuncio. Cada proveedor recibe solo lo necesario para su
        función. Algunos procesan datos fuera de tu país, con las garantías que exige la normativa aplicable.
      </p>
      <h2 id="cookies">5. Cookies</h2>
      <p>
        Usamos cookies técnicas, necesarias para iniciar sesión y recordar tu elección sobre cookies. En el sitio público (inicio, precios, registro y páginas
        legales) usamos además cookies de Google Analytics, Google Ads y el píxel de Meta para medir nuestros anuncios. En la Unión Europea, el Espacio
        Económico Europeo, el Reino Unido y Suiza solo se activan si las aceptas; en el resto de los países se activan salvo que las rechaces. Puedes cambiar tu
        elección cuando quieras desde el enlace Cookies al pie de la página.
      </p>
      <h2>6. Seguridad y conservación</h2>
      <p>
        Usamos conexiones cifradas, contraseñas protegidas, separación de datos por taller y copias de seguridad diarias. Conservamos los datos mientras la cuenta
        esté activa y el tiempo que exijan las normas fiscales.
      </p>
      <h2>7. Tus derechos</h2>
      <p>
        Puedes acceder, rectificar, actualizar, exportar o pedir la eliminación de tus datos, u oponerte a su tratamiento, escribiéndonos a <Contact />. En
        Argentina, la Agencia de Acceso a la Información Pública atiende denuncias sobre el incumplimiento de la Ley 25.326; en la Unión Europea puedes reclamar
        ante la autoridad de protección de datos de tu país.
      </p>
    </LegalPage>
  )
}

function RefundEs() {
  return (
    <LegalPage title="Política de reembolsos">
      <p>
        Cada cuenta nueva tiene una prueba gratis con todos los módulos y sin tarjeta, para que puedas evaluar RepairShop antes de pagar. Si igual contratas un
        plan y no te sirve, estas son las reglas.
      </p>
      <h2>1. Reintegro dentro de los 14 días</h2>
      <p>
        Puedes pedir el reintegro total del primer pago de tu suscripción dentro de los 14 días corridos desde ese cobro, sin dar explicaciones. Lo mismo vale
        para la diferencia que pagas al pasar a un plan más caro.
      </p>
      <h2>2. Después de los 14 días</h2>
      <p>
        Puedes cancelar cuando quieras desde la sección Suscripción. La cancelación rige al final del período ya pagado, sigues usando el servicio hasta esa
        fecha y no se vuelve a cobrar. No reintegramos períodos parciales, salvo que la ley de tu país disponga otra cosa.
      </p>
      <h2>3. Cobros por error</h2>
      <p>Si te cobramos dos veces el mismo período, o después de que cancelaste, te devolvemos el importe completo.</p>
      <h2>4. Cómo pedirlo</h2>
      <p>
        Escríbenos a <Contact /> desde el email de tu cuenta. Fuera de Argentina el pago lo procesa Paddle.com, que hace el reintegro; también puedes pedirlo
        directamente en{' '}
        <a href="https://paddle.net" target="_blank" rel="noreferrer" className="text-brand-700 underline">
          paddle.net
        </a>
        . En Argentina el reintegro se hace por Mercado Pago, y también puedes ejercer el derecho de arrepentimiento dentro de los 10 días de la contratación.
      </p>
      <h2>5. Plazos</h2>
      <p>
        Respondemos los pedidos dentro de los 5 días hábiles. Una vez aprobado, el dinero vuelve al mismo medio de pago en el plazo que maneje tu banco o tarjeta.
      </p>
    </LegalPage>
  )
}

// ---- English -----------------------------------------------------------------------------------------------------

function TermsEn() {
  return (
    <LegalPage title="Terms of Service">
      <p>
        RepairShop is management software for repair shops, offered as a subscription. By creating an account you accept these terms on your own behalf and on
        behalf of the shop you register.
        <Seller />
      </p>
      <h2>1. Account and use</h2>
      <p>
        You are responsible for the accuracy of the data you enter, for keeping your passwords confidential and for the actions of the users you invite. You may
        not use the service for unlawful activities or to send unsolicited communications.
      </p>
      <h2>2. Free trial, plans and payments</h2>
      <p>
        Every new account includes a free trial with every module and no credit card. When it ends, the account becomes read-only until you choose a monthly
        plan. Outside Argentina the subscription is charged in US dollars by Paddle.com, which acts as authorized reseller (merchant of record) and adds the
        taxes that apply in your country; in Argentina it is charged in pesos through Mercado Pago. The subscription is charged monthly in advance and you can
        cancel it at any time from the Subscription section; cancellation takes effect at the end of the paid period. Refunds follow the{' '}
        <PageLink page="refunds">Refund Policy</PageLink>.
      </p>
      <h2>3. Changes to the service</h2>
      <p>We may add, change or remove features. We will give you notice in advance when a change affects features you use or the price of your plan.</p>
      <h2>4. Your data</h2>
      <p>
        The data you enter (customers, devices, orders, receipts) belongs to your shop. We handle it according to the{' '}
        <PageLink page="privacy">Privacy Policy</PageLink>, which also explains the measurement cookies of the public website, and you can request a copy or
        its deletion at any time.
      </p>
      <h2>5. Electronic invoicing</h2>
      <p>
        Receipts issued through ARCA (Argentina) use each shop's own certificate and point of sale. Each shop is responsible for its tax obligations and for
        reviewing the receipts it issues.
      </p>
      <h2>6. Availability and liability</h2>
      <p>
        We work to keep the service available and make daily backups, but we do not guarantee uninterrupted operation. To the extent permitted by law, our
        liability is limited to the amount paid in the last three months.
      </p>
      <h2>7. Changes and contact</h2>
      <p>
        If we change these terms, we will announce it inside the service. For questions, write to <Contact />. These terms are governed by the laws of the
        Argentine Republic, without prejudice to the rights granted to you by the consumer protection laws of your country.
      </p>
    </LegalPage>
  )
}

function PrivacyEn() {
  return (
    <LegalPage title="Privacy Policy">
      <p>
        This policy explains what data RepairShop processes and how it is protected, in accordance with Argentina's Personal Data Protection Law 25,326 and, for
        people in the European Union, the General Data Protection Regulation (GDPR).
      </p>
      <h2>1. What data we process</h2>
      <p>
        Account data (name, email, country and shop details) and the data each shop enters about its customers, devices, orders, payments and receipts. We also
        record technical usage data, such as sign-ins and errors, to run and protect the service. When you create an account we also store the IP address, the
        browser and, if you arrived from an ad or a campaign link, its parameters (source, campaign and Google and Meta click ids).
      </p>
      <h2>2. What we use it for</h2>
      <p>
        To provide the service, send the notifications each shop sets up, charge the subscription, provide support and meet legal obligations. If you accept
        advertising cookies, also to measure which ads bring new shops. We do not sell personal data.
      </p>
      <h2>3. The shop's role</h2>
      <p>
        For the data about its customers, each shop is the controller and RepairShop acts as processor: we process it only to provide the service to the shop.
      </p>
      <h2>4. Who we share it with</h2>
      <p>
        With providers we need to operate: hosting, email, SMS and WhatsApp delivery, subscription billing (Paddle outside Argentina and Mercado Pago in
        Argentina) and ARCA for electronic invoicing. If you accept advertising cookies, Google (Google Analytics and Google Ads) and Meta (pixel and Conversions
        API) receive which pages of the public website you visit and when you create an account or subscribe; we also send Meta your email and country hashed
        with SHA-256, your IP address and your browser, so it can attribute the subscription to the ad. Each provider receives only what it needs for its
        function. Some of them process data outside your country, with the safeguards required by applicable law.
      </p>
      <h2 id="cookies">5. Cookies</h2>
      <p>
        We use technical cookies needed to sign in and to remember your cookie choice. On the public website (home, pricing, signup and legal pages) we also use
        Google Analytics, Google Ads and Meta pixel cookies to measure our ads. In the European Union, the European Economic Area, the United Kingdom and
        Switzerland they are only turned on if you accept them; in other countries they are on unless you reject them. You can change your choice at any time
        from the Cookies link at the bottom of the page.
      </p>
      <h2>6. Security and retention</h2>
      <p>
        We use encrypted connections, protected passwords, per-shop data separation and daily backups. We keep the data while the account is active and for as
        long as tax rules require.
      </p>
      <h2>7. Your rights</h2>
      <p>
        You can access, correct, update, export or request the deletion of your data, or object to its processing, by writing to <Contact />. In Argentina, the
        Agency for Access to Public Information handles complaints about breaches of Law 25,326; in the European Union you can complain to the data protection
        authority of your country.
      </p>
    </LegalPage>
  )
}

function RefundEn() {
  return (
    <LegalPage title="Refund Policy">
      <p>
        Every new account gets a free trial with every module and no credit card, so you can evaluate RepairShop before paying. If you subscribe anyway and it is
        not for you, these are the rules.
      </p>
      <h2>1. Refund within 14 days</h2>
      <p>
        You can request a full refund of the first payment of your subscription within 14 calendar days of that charge, no questions asked. The same applies to
        the difference you pay when moving to a more expensive plan.
      </p>
      <h2>2. After 14 days</h2>
      <p>
        You can cancel at any time from the Subscription section. Cancellation takes effect at the end of the period already paid: you keep using the service
        until then and you are not charged again. We do not refund partial periods, unless the law of your country says otherwise.
      </p>
      <h2>3. Charges in error</h2>
      <p>If we charge you twice for the same period, or after you canceled, we refund the full amount.</p>
      <h2>4. How to request it</h2>
      <p>
        Write to <Contact /> from your account's email. Outside Argentina the payment is processed by Paddle.com, which issues the refund; you can also request
        it directly at{' '}
        <a href="https://paddle.net" target="_blank" rel="noreferrer" className="text-brand-700 underline">
          paddle.net
        </a>
        . In Argentina refunds are made through Mercado Pago, and you can also exercise the right of withdrawal within 10 days of subscribing.
      </p>
      <h2>5. Timing</h2>
      <p>
        We answer requests within 5 business days. Once approved, the money goes back to the same payment method within the time your bank or card issuer
        takes.
      </p>
    </LegalPage>
  )
}
