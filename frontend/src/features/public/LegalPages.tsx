import type { ReactNode } from 'react'
import { PublicFooter, PublicHeader } from './PricingPage'

const UPDATED = '3 de octubre de 2026'

function Contact() {
  return (
    <a href="https://techxto.ar" target="_blank" rel="noreferrer" className="text-brand-700 underline">
      los canales publicados en techxto.ar
    </a>
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
        quieras desde la sección Suscripción; la cancelación rige al final del período pagado y no se reintegran períodos parciales.
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
