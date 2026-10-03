import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { billingApi } from '../../api/endpoints'
import { PublicFooter, PublicHeader } from './PricingPage'

const UPDATED = '3 de octubre de 2026'

function useLegalConfig() {
  return useQuery({ queryKey: ['billing-config'], queryFn: billingApi.config, staleTime: 5 * 60_000 }).data
}

function Contact() {
  const email = useLegalConfig()?.contactEmail
  if (email)
    return (
      <a href={`mailto:${email}`} className="text-brand-700 underline">
        {email}
      </a>
    )
  return (
    <a href="https://techxto.ar" target="_blank" rel="noreferrer" className="text-brand-700 underline">
      los canales publicados en techxto.ar
    </a>
  )
}

function Seller() {
  const name = useLegalConfig()?.legalName
  return name ? <>{` El servicio lo presta ${name}.`}</> : null
}

function RefundLink() {
  return (
    <Link to="/reembolsos" className="text-brand-700 underline">
      Política de reembolsos
    </Link>
  )
}

function LegalPage({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="min-h-full bg-slate-50">
      <PublicHeader />
      <main className="mx-auto max-w-3xl px-4 pb-6">
        <article className="space-y-3 rounded-2xl bg-white p-6 text-sm leading-relaxed text-slate-700 shadow-sm ring-1 ring-slate-200 sm:p-8 [&_h2]:pt-3 [&_h2]:font-semibold [&_h2]:text-slate-900">
          <p className="text-xs uppercase tracking-wide text-slate-500">Actualizado el {UPDATED}</p>
          <h1 className="text-2xl font-bold text-slate-900">{title}</h1>
          {children}
        </article>
      </main>
      <PublicFooter />
    </div>
  )
}

export function TermsPage() {
  return (
    <LegalPage title="Términos del servicio">
      <p>
        RepairShop es un software de gestión para talleres de reparación que se ofrece por suscripción. Al crear una cuenta, aceptás estos términos en nombre
        propio y del taller que registrás.
        <Seller />
      </p>
      <h2>1. Cuenta y uso</h2>
      <p>
        Sos responsable de la veracidad de los datos cargados, de mantener la confidencialidad de tus contraseñas y de las acciones que realicen los usuarios que
        invites. No podés usar el servicio para actividades ilícitas ni para enviar comunicaciones no solicitadas.
      </p>
      <h2>2. Prueba gratis, planes y pagos</h2>
      <p>
        Cada cuenta nueva incluye una prueba gratis con todos los módulos y sin tarjeta. Al terminar, la cuenta queda en modo consulta hasta que elijas un plan
        mensual. En Argentina el abono se cobra en pesos con Mercado Pago. En el resto de los países lo cobra en dólares Paddle.com, que actúa como revendedor
        autorizado (merchant of record) y agrega los impuestos que correspondan en tu país. El abono se cobra por mes adelantado y podés cancelarlo cuando
        quieras desde la sección Suscripción; la cancelación rige al final del período pagado. Los reintegros se rigen por la <RefundLink />.
      </p>
      <h2>3. Cambios en el servicio</h2>
      <p>
        Podemos incorporar, modificar o retirar funciones. Te avisaremos con anticipación cuando un cambio afecte funciones que usás o el precio de tu plan.
      </p>
      <h2>4. Tus datos</h2>
      <p>
        Los datos que cargás (clientes, equipos, órdenes, comprobantes) son de tu taller. Los tratamos según la Política de privacidad y podés pedir una copia o
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
        Si modificamos estos términos, lo informaremos dentro del servicio. Para consultas, contactanos por <Contact />. Estos términos se rigen por las leyes de
        la República Argentina, sin perjuicio de los derechos que te otorguen las normas de protección al consumidor de tu país.
      </p>
    </LegalPage>
  )
}

export function PrivacyPage() {
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
      <h2>6. Tus derechos</h2>
      <p>
        Podés acceder, rectificar, actualizar, exportar o pedir la eliminación de tus datos, u oponerte a su tratamiento, escribiéndonos por <Contact />. En
        Argentina, la Agencia de Acceso a la Información Pública atiende denuncias sobre el incumplimiento de la Ley 25.326; en la Unión Europea podés reclamar
        ante la autoridad de protección de datos de tu país.
      </p>
    </LegalPage>
  )
}

export function RefundPage() {
  return (
    <LegalPage title="Política de reembolsos">
      <p>
        Cada cuenta nueva tiene una prueba gratis con todos los módulos y sin tarjeta, para que puedas evaluar RepairShop antes de pagar. Si igual contratás un
        plan y no te sirve, estas son las reglas.
      </p>
      <h2>1. Reintegro dentro de los 14 días</h2>
      <p>
        Podés pedir el reintegro total del primer pago de tu suscripción dentro de los 14 días corridos desde ese cobro, sin dar explicaciones. Lo mismo vale
        para la diferencia que pagás al pasar a un plan más caro.
      </p>
      <h2>2. Después de los 14 días</h2>
      <p>
        Podés cancelar cuando quieras desde la sección Suscripción. La cancelación rige al final del período ya pagado, seguís usando el servicio hasta esa
        fecha y no se vuelve a cobrar. No reintegramos períodos parciales, salvo que la ley de tu país disponga otra cosa.
      </p>
      <h2>3. Cobros por error</h2>
      <p>Si te cobramos dos veces el mismo período, o después de que cancelaste, te devolvemos el importe completo.</p>
      <h2>4. Cómo pedirlo</h2>
      <p>
        Escribinos por <Contact /> desde el email de tu cuenta. Fuera de Argentina el pago lo procesa Paddle.com, que hace el reintegro; también podés pedirlo
        directamente en{' '}
        <a href="https://paddle.net" target="_blank" rel="noreferrer" className="text-brand-700 underline">
          paddle.net
        </a>
        . En Argentina el reintegro se hace por Mercado Pago, y también podés ejercer el derecho de arrepentimiento dentro de los 10 días de la contratación.
      </p>
      <h2>5. Plazos</h2>
      <p>
        Respondemos los pedidos dentro de los 5 días hábiles. Una vez aprobado, el dinero vuelve al mismo medio de pago en el plazo que maneje tu banco o tarjeta.
      </p>
    </LegalPage>
  )
}
