import { Link } from "react-router-dom";
import { Brand } from "../ui";

// Contact shown on the legal pages. Leave empty to point to the public site instead.
const CONTACT_EMAIL = "";
const UPDATED = "30 de septiembre de 2026";

function Contact() {
  return CONTACT_EMAIL ? (
    <a href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>
  ) : (
    <a href="https://techxto.ar" target="_blank" rel="noreferrer">
      los canales publicados en techxto.ar
    </a>
  );
}

function LegalPage({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="legal-page">
      <header>
        <Link to="/" aria-label="Inicio">
          <Brand />
        </Link>
      </header>
      <article className="panel padded">
        <span className="eyebrow">Actualizado el {UPDATED}</span>
        <h1>{title}</h1>
        {children}
        <p className="legal-links">
          <Link to="/terminos">Términos del servicio</Link> ·{" "}
          <Link to="/privacidad">Política de privacidad</Link> ·{" "}
          <Link to="/signup">Crear cuenta</Link>
        </p>
      </article>
    </div>
  );
}

export function Terms() {
  return (
    <LegalPage title="Términos del servicio">
      <p>
        RepairShop es un software de gestión para talleres de reparación que se
        ofrece por suscripción. Al crear una cuenta, aceptás estos términos en
        nombre propio y del taller que registrás.
      </p>
      <h2>1. Cuenta y uso</h2>
      <p>
        Sos responsable de la veracidad de los datos cargados, de mantener la
        confidencialidad de tus contraseñas y de las acciones que realicen los
        usuarios que invites. No podés usar el servicio para actividades
        ilícitas ni para enviar comunicaciones no solicitadas.
      </p>
      <h2>2. Prueba gratis y planes</h2>
      <p>
        Cada cuenta nueva incluye un período de prueba gratis con todos los
        módulos. Al finalizar, podés elegir un plan mensual. El abono se debita
        por mes adelantado y podés cancelarlo cuando quieras desde
        Configuración; la cancelación rige al final del período pagado.
      </p>
      <h2>3. Servicio en beta</h2>
      <p>
        El servicio se encuentra en etapa beta. Podemos incorporar, modificar o
        retirar funciones, y te avisaremos con anticipación cuando un cambio
        afecte funciones que usás.
      </p>
      <h2>4. Tus datos</h2>
      <p>
        Los datos que cargás (clientes, equipos, órdenes, comprobantes) son de
        tu taller. Los tratamos según la{" "}
        <Link to="/privacidad">Política de privacidad</Link> y podés pedir una
        copia o su eliminación en cualquier momento.
      </p>
      <h2>5. Facturación electrónica</h2>
      <p>
        La emisión de comprobantes ante ARCA se realiza con el certificado y el
        punto de venta de cada taller. Cada taller es responsable de sus
        obligaciones fiscales y de revisar los comprobantes que emite.
      </p>
      <h2>6. Disponibilidad y responsabilidad</h2>
      <p>
        Trabajamos para que el servicio esté disponible y realizamos copias de
        seguridad diarias, pero no garantizamos un funcionamiento
        ininterrumpido. En la medida que permita la ley, nuestra responsabilidad
        se limita al monto abonado en los últimos tres meses.
      </p>
      <h2>7. Cambios y contacto</h2>
      <p>
        Si modificamos estos términos, lo informaremos dentro del servicio. Para
        consultas, contactanos por <Contact />. Estos términos se rigen por las
        leyes de la República Argentina.
      </p>
    </LegalPage>
  );
}

export function Privacy() {
  return (
    <LegalPage title="Política de privacidad">
      <p>
        Esta política explica qué datos trata RepairShop y cómo los protege, de
        acuerdo con la Ley 25.326 de Protección de Datos Personales.
      </p>
      <h2>1. Qué datos tratamos</h2>
      <p>
        Datos de la cuenta (nombre, email, teléfono y datos del taller) y los
        datos que el taller carga sobre sus clientes, equipos, órdenes, pagos y
        comprobantes. También registramos datos técnicos de uso, como accesos y
        errores, para operar y proteger el servicio.
      </p>
      <h2>2. Para qué los usamos</h2>
      <p>
        Para prestar el servicio, enviar los avisos que el taller configura,
        cobrar la suscripción, brindar soporte y cumplir obligaciones legales.
        No vendemos datos personales ni los usamos para publicidad de terceros.
      </p>
      <h2>3. Rol del taller</h2>
      <p>
        Respecto de los datos de sus clientes, cada taller es el responsable y
        RepairShop actúa como encargado del tratamiento: los procesamos solo
        para prestar el servicio al taller.
      </p>
      <h2>4. Con quién los compartimos</h2>
      <p>
        Con proveedores necesarios para operar: alojamiento, envío de emails y
        SMS, procesamiento de pagos (Mercado Pago) y ARCA para la facturación
        electrónica. Cada uno recibe solo lo necesario para su función.
      </p>
      <h2>5. Seguridad y conservación</h2>
      <p>
        Usamos conexiones cifradas, contraseñas protegidas, separación de datos
        por taller y copias de seguridad diarias. Conservamos los datos mientras
        la cuenta esté activa y el tiempo que exijan las normas fiscales.
      </p>
      <h2>6. Tus derechos</h2>
      <p>
        Podés acceder, rectificar, actualizar o pedir la eliminación de tus
        datos escribiéndonos por <Contact />. La Agencia de Acceso a la
        Información Pública, órgano de control de la Ley 25.326, atiende
        denuncias y reclamos sobre el incumplimiento de esta normativa.
      </p>
    </LegalPage>
  );
}
