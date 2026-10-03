import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { billingApi } from '../../api/endpoints'
import { PublicLayout } from './PublicLayout'
import { publicPath, usePublicLocale } from '../../lib/publicLocale'

const UPDATED = '3 de octubre de 2026'

function useLegalConfig() {
  return useQuery({ queryKey: ['billing-config'], queryFn: billingApi.config, staleTime: 5 * 60_000 }).data
}

function Contact() {
  const en = usePublicLocale() === 'en'
  const email = useLegalConfig()?.contactEmail
  if (email)
    return (
      <a href={`mailto:${email}`} className="text-brand-700 underline">
        {email}
      </a>
    )
  return (
    <a href="https://techxto.ar" target="_blank" rel="noreferrer" className="text-brand-700 underline">
      {en ? 'the contact channels at techxto.ar' : 'los canales publicados en techxto.ar'}
    </a>
  )
}

function Seller() {
  const en = usePublicLocale() === 'en'
  const name = useLegalConfig()?.legalName
  return name ? <>{en ? ` The service is provided by ${name}.` : ` El servicio lo presta ${name}.`}</> : null
}

function RefundLink() {
  const locale = usePublicLocale()
  return (
    <Link to={publicPath(locale, 'refunds')} className="text-brand-700 underline">
      {locale === 'en' ? 'Refund policy' : 'Política de reembolsos'}
    </Link>
  )
}

function LegalPage({ title, children }: { title: string; children: ReactNode }) {
  const en = usePublicLocale() === 'en'
  return (
    <PublicLayout title={title}>
      <main className="mx-auto max-w-3xl px-4 pb-6">
        <article className="space-y-3 rounded-2xl bg-white p-6 text-sm leading-relaxed text-slate-700 shadow-sm ring-1 ring-slate-200 sm:p-8 [&_h2]:pt-3 [&_h2]:font-semibold [&_h2]:text-slate-900">
          <p className="text-xs uppercase tracking-wide text-slate-500">{en ? 'Updated October 3, 2026' : `Actualizado el ${UPDATED}`} </p>
          <h1 className="text-2xl font-bold text-slate-900">{title}</h1>
          {children}
        </article>
      </main>
    </PublicLayout>
  )
}

export function TermsPage() {
  const locale = usePublicLocale()
  if (locale === 'en') return <EnglishLegalPage page="terms" />
  return (
    <LegalPage title="Términos del servicio">
      <p>
        RepairShop es un software de gestión para talleres de reparación que se ofrece por suscripción. Al crear una cuenta, aceptas estos términos en nombre
        propio y del taller que registras.
        <Seller />
      </p>
      <h2>1. Cuenta y uso</h2>
      <p>
        Eres responsable de la veracidad de los datos cargados, de mantener la confidencialidad de tus contraseñas y de las acciones que realicen los usuarios que
        invites. No puedes usar el servicio para actividades ilícitas ni para enviar comunicaciones no solicitadas.
      </p>
      <h2>2. Prueba gratis, planes y pagos</h2>
      <p>
        Cada cuenta nueva incluye una prueba gratis con todos los módulos y sin tarjeta. Al terminar, la cuenta queda en modo consulta hasta que elijas un plan
        mensual. En Argentina el abono se cobra en pesos con Mercado Pago. En el resto de los países lo cobra en dólares Paddle.com, que actúa como revendedor
        autorizado (merchant of record) y agrega los impuestos que correspondan en tu país. El abono se cobra por mes adelantado y puedes cancelarlo cuando
        quieras desde la sección Suscripción; la cancelación rige al final del período pagado. Los reintegros se rigen por la <RefundLink />.
      </p>
      <h2>3. Cambios en el servicio</h2>
      <p>
        Podemos incorporar, modificar o retirar funciones. Te avisaremos con anticipación cuando un cambio afecte funciones que usas o el precio de tu plan.
      </p>
      <h2>4. Tus datos</h2>
      <p>
        Los datos que cargas (clientes, equipos, órdenes, comprobantes) son de tu taller. Los tratamos según la Política de privacidad y puedes pedir una copia o
        su eliminación en cualquier momento.
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
        Si modificamos estos términos, lo informaremos dentro del servicio. Para consultas, contáctanos por <Contact />. Estos términos se rigen por las leyes de
        la República Argentina, sin perjuicio de los derechos que te otorguen las normas de protección al consumidor de tu país.
      </p>
    </LegalPage>
  )
}

export function PrivacyPage() {
  const locale = usePublicLocale()
  if (locale === 'en') return <EnglishLegalPage page="privacy" />
  return (
    <LegalPage title="Política de privacidad">
      <p>
        Esta política explica qué datos trata RepairShop y cómo los protege, de acuerdo con la Ley 25.326 de Protección de Datos Personales de Argentina y, para
        personas en la Unión Europea, el Reglamento General de Protección de Datos (RGPD).
      </p>
      <h2>1. Qué datos tratamos</h2>
      <p>
        Datos de la cuenta (nombre, email, país y datos del taller) y los datos que el taller carga sobre sus clientes, equipos, órdenes, pagos y comprobantes.
        También registramos datos técnicos de uso, como accesos y errores, para operar y proteger el servicio.
      </p>
      <h2>2. Para qué los usamos</h2>
      <p>
        Para prestar el servicio, enviar los avisos que el taller configura, cobrar la suscripción, brindar soporte y cumplir obligaciones legales. No vendemos
        datos personales.
      </p>
      <h2>3. Rol del taller</h2>
      <p>
        Respecto de los datos de sus clientes, cada taller es el responsable y RepairShop actúa como encargado del tratamiento: los procesamos solo para prestar
        el servicio al taller.
      </p>
      <h2>4. Con quién los compartimos</h2>
      <p>
        Con proveedores necesarios para operar: alojamiento, envío de emails, SMS y WhatsApp, cobro de la suscripción (Mercado Pago en Argentina y Paddle en el
        resto de los países) y ARCA para la facturación electrónica. Cada uno recibe solo lo necesario para su función. Algunos de ellos procesan datos fuera de
        tu país, con las garantías que exige la normativa aplicable.
      </p>
      <h2>5. Seguridad y conservación</h2>
      <p>
        Usamos conexiones cifradas, contraseñas protegidas, separación de datos por taller y copias de seguridad diarias. Conservamos los datos mientras la cuenta
        esté activa y el tiempo que exijan las normas fiscales.
      </p>
      <h2>Cookies y medición de campañas</h2>
      <p>Las cookies de sesión son necesarias para usar la aplicación. Solo con tu consentimiento cargamos Google Analytics, Google Ads y Meta Pixel. Si lo aceptas, guardamos el origen de la campaña y comunicamos el inicio de la prueba y el primer pago a estas plataformas. Meta también puede recibir estos eventos desde nuestro servidor, junto con identificadores de campaña, dirección IP, navegador y datos de coincidencia como el email transformado con hash. Un hash no convierte estos datos en anónimos. Puedes rechazar las cookies opcionales o cambiar tu elección desde Preferencias de cookies; para retirar también el consentimiento de tu cuenta, hazlo con la sesión iniciada.</p>
      <h2>6. Tus derechos</h2>
      <p>
        Puedes acceder, rectificar, actualizar, exportar o pedir la eliminación de tus datos, u oponerte a su tratamiento, escribiéndonos por <Contact />. En
        Argentina, la Agencia de Acceso a la Información Pública atiende denuncias sobre el incumplimiento de la Ley 25.326; en la Unión Europea puedes reclamar
        ante la autoridad de protección de datos de tu país.
      </p>
    </LegalPage>
  )
}

export function RefundPage() {
  const locale = usePublicLocale()
  if (locale === 'en') return <EnglishLegalPage page="refund" />
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
        Escríbenos por <Contact /> desde el email de tu cuenta. Fuera de Argentina el pago lo procesa Paddle.com, que hace el reintegro; también puedes pedirlo
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

function EnglishLegalPage({ page }: { page: 'terms' | 'privacy' | 'refund' }) {
  if (page === 'terms') return (
    <LegalPage title="Terms of service">
      <p>RepairShop is subscription software for repair shop management. By creating an account, you accept these terms on behalf of yourself and the shop you register.<Seller /></p>
      <h2>1. Account and use</h2>
      <p>You are responsible for accurate account information, keeping your passwords confidential and the actions of users you invite. You may not use the service for unlawful activities or unsolicited communications.</p>
      <h2>2. Free trial, plans and payments</h2>
      <p>New accounts include a free trial with all modules and no card required. After the trial, your account becomes read-only until you choose a monthly plan. In Argentina, Mercado Pago charges in Argentine pesos. Elsewhere, Paddle.com acts as our authorized reseller and merchant of record, charging in US dollars plus applicable taxes. Subscriptions are billed monthly in advance. You can cancel from the Subscription page at any time; cancellation takes effect at the end of the paid period. Refunds follow our <RefundLink />.</p>
      <h2>3. Changes to the service</h2>
      <p>We may add, change or remove features. We will notify you in advance of changes that affect features you use or your plan price.</p>
      <h2>4. Your data</h2>
      <p>Your shop owns the customer, device, order and document data it enters. We process it according to our Privacy policy. You may request a copy or deletion at any time.</p>
      <h2>5. Electronic invoicing</h2>
      <p>ARCA invoicing in Argentina uses each shop’s own certificate and point of sale. Each shop is responsible for its tax obligations and for checking the documents it issues.</p>
      <h2>6. Availability and liability</h2>
      <p>We work to keep the service available and make daily backups, but do not guarantee uninterrupted operation. To the extent permitted by law, our liability is limited to the amount paid in the preceding three months.</p>
      <h2>7. Changes and contact</h2>
      <p>We will announce changes to these terms within the service. Contact us through <Contact />. These terms are governed by the laws of Argentina, without limiting the consumer rights granted by the laws of your country.</p>
    </LegalPage>
  )
  if (page === 'privacy') return (
    <LegalPage title="Privacy policy">
      <p>This policy explains how RepairShop processes and protects personal data under Argentina’s Personal Data Protection Law 25,326 and, for people in the European Union, the General Data Protection Regulation (GDPR).</p>
      <h2>1. Data we process</h2>
      <p>Account details, including name, email, country and shop information, and the customer, device, order, payment and document data entered by your shop. We also log technical information, including sign-ins and errors, to operate and protect the service.</p>
      <h2>2. How we use it</h2>
      <p>To provide the service, deliver notifications configured by your shop, collect subscription payments, provide support and meet legal obligations. We do not sell personal data.</p>
      <h2>3. Your shop’s role</h2>
      <p>Your shop is the controller of its customers’ data. RepairShop acts as a processor and uses that data only to provide the service to your shop.</p>
      <h2>4. Service providers</h2>
      <p>We share necessary data with hosting, email, SMS and WhatsApp providers, Mercado Pago in Argentina, Paddle elsewhere, and ARCA for electronic invoicing. Some providers process data outside your country, subject to safeguards required by applicable law.</p>
      <h2>5. Security and retention</h2>
      <p>We use encrypted connections, protected passwords, separation between shops and daily backups. We retain data while your account is active and for the periods required by tax rules.</p>
      <h2>Cookies and campaign measurement</h2>
      <p>Session cookies are necessary to use the app. Google Analytics, Google Ads and Meta Pixel load only with your consent. If you agree, we store campaign attribution and report trial signups and first subscription payments to these platforms. Meta may also receive these events from our server, along with campaign identifiers, IP address, browser information and hashed matching data such as your email. Hashing does not make these data anonymous. You can refuse optional cookies or change your choice in Cookie settings. To withdraw consent for your account as well, change your choice while signed in.</p>
      <h2>6. Your rights</h2>
      <p>You can request access, correction, updates, export or deletion of your data, or object to processing, through <Contact />. In Argentina, the Agency for Access to Public Information handles complaints under Law 25,326. In the EU, you may complain to your country’s data protection authority.</p>
    </LegalPage>
  )
  return (
    <LegalPage title="Refund policy">
      <p>Every new account includes a free trial with all modules and no card required, so you can evaluate RepairShop before paying.</p>
      <h2>1. Refund within 14 days</h2>
      <p>You can request a full refund of your first subscription payment within 14 calendar days of the charge, without giving a reason. The same applies to the price difference paid when upgrading to a more expensive plan.</p>
      <h2>2. After 14 days</h2>
      <p>You can cancel anytime from the Subscription page. Cancellation takes effect at the end of the paid period, and you retain access until then. There are no further charges. Partial periods are not refunded unless your country’s law requires otherwise.</p>
      <h2>3. Incorrect charges</h2>
      <p>If you are charged twice for the same period or after cancellation has taken effect, we refund the full incorrect charge.</p>
      <h2>4. How to request a refund</h2>
      <p>Contact <Contact /> using your account email. Outside Argentina, Paddle.com processes payments and refunds; you may also request a refund at <a href="https://paddle.net" className="text-brand-700 underline">paddle.net</a>. In Argentina, refunds use Mercado Pago, and you may also exercise your right to withdraw within 10 days of contracting.</p>
      <h2>5. Timing</h2>
      <p>We respond within five business days. After approval, funds return to the original payment method according to your bank or card provider’s processing times.</p>
    </LegalPage>
  )
}
