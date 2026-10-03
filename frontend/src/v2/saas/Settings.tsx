import { useCallback, useEffect, useState } from "react";
import {
  Link,
  NavLink,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import {
  Check,
  Copy,
  CreditCard,
  KeyRound,
  Plug,
  Trash2,
  Upload,
} from "lucide-react";
import { toast } from "sonner";
import { request, money, fullDate, date } from "../api";
import { Field, ErrorBox, Loading, Modal } from "../ui";
import { Section, Table, Metrics, Detail, Chip } from "../premium/shared";
import {
  useSession,
  planNames,
  subscriptionNames,
  daysLeft,
  readFile,
} from "./session";

const tabs = [
  { id: "taller", name: "Taller" },
  { id: "usuarios", name: "Usuarios" },
  { id: "plan", name: "Plan y pago" },
  { id: "facturacion", name: "Factura electrónica" },
  { id: "integraciones", name: "Integraciones" },
  { id: "mensajes", name: "Mensajes enviados" },
];

export default function Settings() {
  const { tab = "taller" } = useParams();
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>Configuración</h1>
          <p>Datos del comercio, equipo, plan, ARCA e integraciones.</p>
        </div>
      </div>
      <div className="tabs">
        {tabs.map((t) => (
          <NavLink
            key={t.id}
            to={`/settings/${t.id}`}
            className={({ isActive }) => (isActive ? "active" : "")}
          >
            {t.name}
          </NavLink>
        ))}
      </div>
      {tab === "taller" && <ShopTab />}
      {tab === "usuarios" && <UsersTab />}
      {tab === "plan" && <PlanTab />}
      {tab === "facturacion" && <FiscalTab />}
      {tab === "integraciones" && <IntegrationsTab />}
      {tab === "mensajes" && <MessagesTab />}
    </>
  );
}

const dayNames = ["Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb"];

export function ShopTab({ onSaved }: { onSaved?: () => void }) {
  const { reload, admin } = useSession();
  const [p, setP] = useState<any>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    request("/api/saas/profile")
      .then(setP)
      .catch((e) => setError(e.message));
  }, []);
  if (!p) return error ? <ErrorBox message={error} /> : <Loading />;
  const set = (k: string, v: any) => setP({ ...p, [k]: v });
  const hours = p.openingHours;
  const origin = window.location.origin;
  const widget = `<div id="repairshop-tracking"></div>\n<script src="${origin}/widget.js" data-shop="${p.slug}" async></script>`;
  return (
    <form
      className="settings-stack"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        setError("");
        try {
          setP(await request("/api/saas/profile", "PUT", p));
          await reload();
          toast.success("Datos del taller guardados");
          onSaved?.();
        } catch (e) {
          setError((e as Error).message);
        } finally {
          setBusy(false);
        }
      }}
    >
      <ErrorBox message={error} />
      <fieldset disabled={!admin} className="plain-fieldset settings-stack">
        <Section
          title="Datos del comercio"
          text="Aparecen en comprobantes, facturas, emails y páginas públicas."
        >
          <div className="form-grid padded-grid">
            <Field label="Nombre comercial">
              <input
                required
                value={p.displayName}
                onChange={(e) => set("displayName", e.target.value)}
              />
            </Field>
            <Field label="Razón social">
              <input
                value={p.legalName}
                onChange={(e) => set("legalName", e.target.value)}
              />
            </Field>
            <Field label="CUIT" hint="11 dígitos, sin guiones.">
              <input
                inputMode="numeric"
                value={p.taxId}
                onChange={(e) => set("taxId", e.target.value)}
              />
            </Field>
            <Field label="Condición frente al IVA">
              <select
                value={p.taxCondition}
                onChange={(e) => set("taxCondition", e.target.value)}
              >
                <option value="Monotributo">Monotributo</option>
                <option value="ResponsableInscripto">
                  Responsable inscripto
                </option>
                <option value="Exento">Exento</option>
              </select>
            </Field>
            <Field label="Email">
              <input
                type="email"
                value={p.email}
                onChange={(e) => set("email", e.target.value)}
              />
            </Field>
            <Field label="Teléfono">
              <input
                value={p.phone}
                onChange={(e) => set("phone", e.target.value)}
              />
            </Field>
            <Field label="Dirección">
              <input
                value={p.address}
                onChange={(e) => set("address", e.target.value)}
              />
            </Field>
            <Field label="Ciudad">
              <input
                value={p.city}
                onChange={(e) => set("city", e.target.value)}
              />
            </Field>
            <Field label="Sitio web">
              <input
                value={p.website}
                onChange={(e) => set("website", e.target.value)}
              />
            </Field>
          </div>
        </Section>
        <Section
          title="Marca"
          text="Tu logo y color en el portal, el seguimiento, los turnos y los comprobantes."
        >
          <div className="brand-editor">
            <div
              className="logo-preview"
              style={{ borderColor: p.primaryColor }}
            >
              {p.logoDataUrl ? (
                <img src={p.logoDataUrl} alt="Logo" />
              ) : (
                <span>Sin logo</span>
              )}
            </div>
            <div className="stack">
              <label className="button secondary small file-button">
                <Upload size={15} />
                Subir logo (PNG, JPG o SVG)
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/svg+xml"
                  onChange={async (e) => {
                    const f = e.target.files?.[0];
                    if (!f) return;
                    if (f.size > 280_000)
                      return toast.error("El logo debe pesar menos de 280 KB.");
                    set("logoDataUrl", await readFile(f, "dataUrl"));
                  }}
                />
              </label>
              {p.logoDataUrl && (
                <button
                  type="button"
                  className="button subtle small"
                  onClick={() => set("logoDataUrl", "")}
                >
                  Quitar logo
                </button>
              )}
              <Field label="Color principal">
                <input
                  type="color"
                  value={p.primaryColor}
                  onChange={(e) => set("primaryColor", e.target.value)}
                />
              </Field>
            </div>
            <div className="span-two">
              <Field
                label="Pie de comprobantes"
                hint="Condiciones, garantía, horarios o redes."
              >
                <textarea
                  rows={3}
                  maxLength={600}
                  value={p.receiptFooter}
                  onChange={(e) => set("receiptFooter", e.target.value)}
                />
              </Field>
            </div>
          </div>
        </Section>
        <Section
          title="Atención y turnos online"
          text="El horario define los turnos que tus clientes pueden reservar."
        >
          <div className="padded-grid stack">
            <div className="day-toggles">
              {dayNames.map((n, i) => (
                <label
                  key={n}
                  className={hours.days.includes(i) ? "active" : ""}
                >
                  <input
                    type="checkbox"
                    checked={hours.days.includes(i)}
                    onChange={(e) =>
                      set("openingHours", {
                        ...hours,
                        days: e.target.checked
                          ? [...hours.days, i]
                          : hours.days.filter((d: number) => d !== i),
                      })
                    }
                  />
                  {n}
                </label>
              ))}
            </div>
            <div className="form-grid three">
              <Field label="Desde">
                <input
                  type="time"
                  value={hours.from}
                  onChange={(e) =>
                    set("openingHours", { ...hours, from: e.target.value })
                  }
                />
              </Field>
              <Field label="Hasta">
                <input
                  type="time"
                  value={hours.to}
                  onChange={(e) =>
                    set("openingHours", { ...hours, to: e.target.value })
                  }
                />
              </Field>
              <Field label="Duración del turno (min)">
                <input
                  type="number"
                  min={10}
                  max={240}
                  value={hours.slotMinutes}
                  onChange={(e) =>
                    set("openingHours", {
                      ...hours,
                      slotMinutes: Number(e.target.value),
                    })
                  }
                />
              </Field>
            </div>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={p.onlineBookingEnabled}
                onChange={(e) => set("onlineBookingEnabled", e.target.checked)}
              />
              Tomar turnos online
            </label>
            <PublicLink
              label="Página de turnos"
              url={`${origin}/turnos/${p.slug}`}
            />
            <PublicLink
              label="Seguimiento de reparaciones"
              url={`${origin}/seguimiento/${p.slug}`}
            />
            <details className="field advanced">
              <summary>Mostrar el seguimiento en tu sitio web</summary>
              <pre className="code-box">{widget}</pre>
              <button
                type="button"
                className="text-link"
                onClick={() => copy(widget)}
              >
                <Copy size={14} />
                Copiar código
              </button>
            </details>
          </div>
        </Section>
        <Section
          title="Avisos a clientes"
          text="Se envían solos cuando cambia el estado de la orden."
        >
          <div className="padded-grid stack">
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={p.notifyEmail}
                onChange={(e) => set("notifyEmail", e.target.checked)}
              />
              Enviar emails (recepción, presupuesto, listo para retirar, turnos)
            </label>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={p.notifySms}
                onChange={(e) => set("notifySms", e.target.checked)}
              />
              Enviar SMS cuando el equipo está listo (plan Estándar o superior)
            </label>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={p.surveysEnabled}
                onChange={(e) => set("surveysEnabled", e.target.checked)}
              />
              Pedir una encuesta de satisfacción al entregar
            </label>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={p.requireSignature}
                onChange={(e) => set("requireSignature", e.target.checked)}
              />
              Exigir firma del cliente para aprobar presupuestos
            </label>
            <div className="form-grid">
              <Field
                label="Recordar retiro después de (días)"
                hint="0 desactiva el recordatorio."
              >
                <input
                  type="number"
                  min={0}
                  max={60}
                  value={p.pickupReminderDays}
                  onChange={(e) =>
                    set("pickupReminderDays", Number(e.target.value))
                  }
                />
              </Field>
              <Field
                label="Resumen semanal para"
                hint="Email que recibe el resumen de cada lunes."
              >
                <input
                  type="email"
                  value={p.weeklySummaryEmail}
                  onChange={(e) => set("weeklySummaryEmail", e.target.value)}
                />
              </Field>
            </div>
          </div>
        </Section>
      </fieldset>
      {admin ? (
        <div className="sticky-save">
          <button className="button primary" disabled={busy}>
            <Check size={16} />
            {busy ? "Guardando…" : "Guardar cambios"}
          </button>
        </div>
      ) : (
        <div className="info-box">
          Solo un administrador puede cambiar estos datos.
        </div>
      )}
    </form>
  );
}

async function copy(text: string) {
  try {
    await navigator.clipboard.writeText(text);
    toast.success("Copiado");
  } catch {
    toast.error("No se pudo copiar.");
  }
}

function PublicLink({ label, url }: { label: string; url: string }) {
  return (
    <div className="public-link">
      <span>{label}</span>
      <a href={url} target="_blank" rel="noreferrer">
        {url}
      </a>
      <button
        type="button"
        className="icon-button"
        onClick={() => copy(url)}
        aria-label="Copiar enlace"
      >
        <Copy size={15} />
      </button>
    </div>
  );
}

function UsersTab() {
  const { admin } = useSession();
  const [users, setUsers] = useState<any[] | null>(null);
  const [modal, setModal] = useState<any>(null);
  const [error, setError] = useState("");
  const load = useCallback(
    () =>
      request("/api/saas/users")
        .then(setUsers)
        .catch((e) => setError(e.message)),
    [],
  );
  useEffect(() => {
    load();
  }, [load]);
  if (!users) return error ? <ErrorBox message={error} /> : <Loading />;
  return (
    <Section
      title="Equipo"
      text="Los técnicos ven las órdenes y su app de trabajo; los administradores, además, caja, facturación y configuración."
      action={
        admin && (
          <button
            className="button primary small"
            onClick={() => setModal({ kind: "new" })}
          >
            Agregar usuario
          </button>
        )
      }
    >
      <Table
        headers={["Nombre", "Email", "Rol", "Alta", ""]}
        rows={users.map((u) => [
          u.displayName,
          u.email,
          u.role === "Admin" ? "Administrador" : "Técnico",
          date(u.createdAtUtc),
          admin ? (
            <button
              className="text-link"
              onClick={() => setModal({ kind: "password", user: u })}
            >
              <KeyRound size={14} />
              Cambiar contraseña
            </button>
          ) : null,
        ])}
      />
      {modal && (
        <UserDialog
          modal={modal}
          onClose={() => setModal(null)}
          onSaved={load}
        />
      )}
    </Section>
  );
}

function UserDialog({
  modal,
  onClose,
  onSaved,
}: {
  modal: any;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [f, setF] = useState({
    displayName: "",
    email: "",
    role: "Tech",
    password: "",
  });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const isNew = modal.kind === "new";
  return (
    <Modal
      title={
        isNew ? "Nuevo usuario" : `Contraseña de ${modal.user.displayName}`
      }
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            if (isNew) await request("/api/saas/users", "POST", f);
            else
              await request(
                `/api/saas/users/${modal.user.id}/password`,
                "POST",
                { password: f.password },
              );
            toast.success(isNew ? "Usuario creado" : "Contraseña actualizada");
            onSaved();
            onClose();
          } catch (e) {
            setError((e as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="dialog-body">
          <ErrorBox message={error} />
          {isNew && (
            <>
              <Field label="Nombre">
                <input
                  required
                  minLength={2}
                  value={f.displayName}
                  onChange={(e) => setF({ ...f, displayName: e.target.value })}
                />
              </Field>
              <Field label="Email">
                <input
                  required
                  type="email"
                  value={f.email}
                  onChange={(e) => setF({ ...f, email: e.target.value })}
                />
              </Field>
              <Field label="Rol">
                <select
                  value={f.role}
                  onChange={(e) => setF({ ...f, role: e.target.value })}
                >
                  <option value="Tech">Técnico</option>
                  <option value="Admin">Administrador</option>
                </select>
              </Field>
            </>
          )}
          <Field
            label={isNew ? "Contraseña inicial" : "Nueva contraseña"}
            hint="Al menos 10 caracteres."
          >
            <input
              required
              type="password"
              minLength={10}
              autoComplete="new-password"
              value={f.password}
              onChange={(e) => setF({ ...f, password: e.target.value })}
            />
          </Field>
        </div>
        <div className="dialog-footer">
          <button type="button" className="button secondary" onClick={onClose}>
            Cancelar
          </button>
          <button className="button primary" disabled={busy}>
            {busy ? "Guardando…" : "Guardar"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

const eventNames: Record<string, string> = {
  TrialStarted: "Prueba iniciada",
  CheckoutStarted: "Pago iniciado",
  Activated: "Suscripción activa",
  PaymentFailed: "Pago rechazado",
  Cancelled: "Cancelada",
  PlanChanged: "Cambio de plan",
  ProviderUpdate: "Aviso de Mercado Pago",
};

function PlanTab() {
  const { reload, admin } = useSession();
  const [params] = useSearchParams();
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState("");
  const load = useCallback(
    () =>
      request("/api/saas/billing")
        .then(setD)
        .catch((e) => setError(e.message)),
    [],
  );
  useEffect(() => {
    load();
    if (params.get("checkout") === "done") {
      toast.success(
        "Recibimos tu suscripción. El estado se actualiza cuando Mercado Pago confirma el pago.",
      );
      reload();
    }
  }, [load]);
  if (!d) return error ? <ErrorBox message={error} /> : <Loading />;
  const s = d.subscription;
  const endDate =
    s.status === "Trialing" ? s.trialEndsAtUtc : s.currentPeriodEndsAtUtc;
  return (
    <div className="settings-stack">
      <ErrorBox message={error} />
      {d.readOnly && <div className="error-box">{d.reason}</div>}
      <Metrics
        items={[
          {
            label: "Plan actual",
            value: planNames[s.plan] || s.plan,
            hint: subscriptionNames[s.status] || s.status,
          },
          {
            label:
              s.status === "Trialing" ? "Prueba gratis" : "Próximo vencimiento",
            value: endDate
              ? s.status === "Trialing"
                ? `${daysLeft(endDate)} días`
                : date(endDate)
              : "—",
            hint: endDate ? fullDate(endDate) : "Sin vencimiento",
          },
          {
            label: "Abono",
            value: money(s.price, s.currency),
            hint: "por mes",
          },
          {
            label: "Cobro",
            value:
              d.provider === "MercadoPago"
                ? "Mercado Pago"
                : "Sin cargo (beta)",
            hint:
              s.payerEmail ||
              (d.provider === "MercadoPago"
                ? "Débito automático mensual"
                : "Durante la beta no se generan cobros"),
          },
        ]}
      />
      <div className="plan-grid">
        {d.plans.map((p: any) => {
          const current = p.id === s.plan && s.status === "Active";
          return (
            <section
              key={p.id}
              className={`panel plan-card ${p.id === "Standard" ? "featured" : ""}`}
            >
              {p.id === "Standard" && (
                <span className="plan-badge">Más elegido</span>
              )}
              <h3>{p.name}</h3>
              <p>{p.description}</p>
              <strong className="plan-price">
                {money(p.price, p.currency)}
                <small>/mes</small>
              </strong>
              <ul>
                {d.catalog.map((m: any) => (
                  <li
                    key={m.id}
                    className={p.modules.includes(m.id) ? "" : "off"}
                  >
                    <Check size={14} />
                    {m.name}
                  </li>
                ))}
              </ul>
              <button
                className={`button ${current ? "secondary" : "primary"} full`}
                disabled={!admin || current || !!busy}
                onClick={async () => {
                  setBusy(p.id);
                  try {
                    const r = await request(
                      "/api/saas/billing/checkout",
                      "POST",
                      { plan: p.id },
                    );
                    window.location.assign(r.url);
                  } catch (e) {
                    toast.error((e as Error).message);
                    setBusy("");
                  }
                }}
              >
                <CreditCard size={16} />
                {current
                  ? "Tu plan"
                  : busy === p.id
                    ? "Abriendo Mercado Pago…"
                    : s.status === "Active"
                      ? "Cambiar a este plan"
                      : "Suscribirme"}
              </button>
            </section>
          );
        })}
      </div>
      {s.pendingPlan && (
        <div className="info-box">
          Hay un pago pendiente de confirmación para el plan{" "}
          {planNames[s.pendingPlan]}.
        </div>
      )}
      <Section title="Historial de la suscripción">
        <Table
          headers={["Fecha", "Evento", "Detalle"]}
          rows={d.events.map((e: any) => [
            fullDate(e.createdAtUtc),
            eventNames[e.kind] || e.kind,
            e.detail,
          ])}
        />
      </Section>
      {admin && s.status !== "Cancelled" && (
        <button
          className="button subtle"
          onClick={async () => {
            if (
              !confirm(
                "¿Cancelar la suscripción? Conservás el acceso hasta el fin del período pago.",
              )
            )
              return;
            try {
              await request("/api/saas/billing/cancel", "POST", {});
              toast.success("Suscripción cancelada");
              load();
              reload();
            } catch (e) {
              toast.error((e as Error).message);
            }
          }}
        >
          Cancelar suscripción
        </button>
      )}
    </div>
  );
}

export function SimulatedCheckout() {
  const [params] = useSearchParams();
  const nav = useNavigate();
  const { reload } = useSession();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  return (
    <div className="checkout-page">
      <section className="panel padded">
        <span className="eyebrow">ACTIVACIÓN DE PLAN</span>
        <h1>Confirmar suscripción</h1>
        <p>
          Durante la beta, la activación de planes no genera cobros. Confirmá
          para activar el plan elegido.
        </p>
        <ErrorBox message={error} />
        <div className="form-actions">
          <Link className="button secondary" to="/settings/plan">
            Volver
          </Link>
          <button
            className="button primary"
            disabled={busy}
            onClick={async () => {
              setBusy(true);
              setError("");
              try {
                await request("/api/saas/billing/simulated/confirm", "POST", {
                  subscriptionId: params.get("subscription"),
                });
                await reload();
                toast.success("Plan activado");
                nav("/settings/plan");
              } catch (e) {
                setError((e as Error).message);
              } finally {
                setBusy(false);
              }
            }}
          >
            Activar plan
          </button>
        </div>
      </section>
    </div>
  );
}

function FiscalTab() {
  const { admin, has } = useSession();
  const [s, setS] = useState<any>(null);
  const [cert, setCert] = useState({ certificatePem: "", privateKeyPem: "" });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (has("invoicing"))
      request("/api/saas/fiscal")
        .then(setS)
        .catch((e) => setError(e.message));
  }, []);
  if (!has("invoicing")) return <UpgradeNotice module="Facturación ARCA" />;
  if (!s) return error ? <ErrorBox message={error} /> : <Loading />;
  const set = (k: string, v: any) => setS({ ...s, [k]: v });
  return (
    <form
      className="settings-stack"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        setError("");
        try {
          setS(await request("/api/saas/fiscal", "PUT", { ...s, ...cert }));
          setCert({ certificatePem: "", privateKeyPem: "" });
          toast.success("Configuración fiscal guardada");
        } catch (e) {
          setError((e as Error).message);
        } finally {
          setBusy(false);
        }
      }}
    >
      <ErrorBox message={error} />
      {s.serverProvider !== "Arca" && (
        <div className="info-box">
          La factura electrónica con ARCA todavía no está disponible en tu
          cuenta. Mientras tanto, los comprobantes se emiten como internos, sin
          validez fiscal.
        </div>
      )}
      <Section
        title="Factura electrónica ARCA"
        text="Facturas A, B y C con CAE y QR. El tipo se elige solo según tu condición y la del cliente."
      >
        <fieldset
          disabled={!admin}
          className="plain-fieldset form-grid padded-grid"
        >
          <Field label="Entorno">
            <select
              value={s.environment}
              onChange={(e) => set("environment", e.target.value)}
            >
              <option value="Homologacion">Homologación (pruebas)</option>
              <option value="Produccion">Producción</option>
            </select>
          </Field>
          <Field
            label="Punto de venta"
            hint="Tipo Web Services, creado en ARCA."
          >
            <input
              type="number"
              min={1}
              max={99998}
              value={s.pointOfSale}
              onChange={(e) => set("pointOfSale", Number(e.target.value))}
            />
          </Field>
          <Field label="Alícuota de IVA por defecto">
            <select
              value={s.defaultVatRate}
              onChange={(e) => set("defaultVatRate", Number(e.target.value))}
            >
              {[21, 10.5, 27, 5, 2.5, 0].map((v) => (
                <option key={v} value={v}>
                  {v} %
                </option>
              ))}
            </select>
          </Field>
          <Field label="Certificado">
            <span className="cert-state">
              {s.hasCertificate
                ? `Cargado · vence ${date(s.certificateExpiresAtUtc)}`
                : "Sin certificado"}
            </span>
          </Field>
          <PemInput
            label="Certificado (.crt)"
            value={cert.certificatePem}
            onChange={(v) => setCert({ ...cert, certificatePem: v })}
          />
          <PemInput
            label="Clave privada (.key)"
            value={cert.privateKeyPem}
            onChange={(v) => setCert({ ...cert, privateKeyPem: v })}
          />
          <label className="checkbox-label span-two">
            <input
              type="checkbox"
              checked={s.enabled}
              onChange={(e) => set("enabled", e.target.checked)}
            />
            Emitir facturas con ARCA (requiere CUIT en los datos del taller y
            certificado)
          </label>
        </fieldset>
      </Section>
      <Section title="Cómo obtener el certificado">
        <ol className="howto">
          <li>
            Generá la clave y el pedido:{" "}
            <code>openssl genrsa -out taller.key 2048</code> y{" "}
            <code>
              openssl req -new -key taller.key -subj "/C=AR/O=TU RAZON
              SOCIAL/CN=repairshop/serialNumber=CUIT 20XXXXXXXXX" -out
              taller.csr
            </code>
          </li>
          <li>
            En ARCA, con clave fiscal:{" "}
            <b>Administración de certificados digitales</b> (en homologación,{" "}
            <b>WSASS</b>). Subí el .csr y descargá el .crt.
          </li>
          <li>
            En <b>Administrador de relaciones</b>, delegá{" "}
            <b>Facturación electrónica (wsfe)</b> al certificado.
          </li>
          <li>
            Creá un punto de venta <b>Web Services</b> en{" "}
            <b>Puntos de venta y domicilios</b>.
          </li>
          <li>Subí acá el .crt y el .key, probá la conexión y activá.</li>
        </ol>
      </Section>
      {admin && (
        <div className="sticky-save">
          <button
            type="button"
            className="button secondary"
            onClick={async () => {
              try {
                const r = await request("/api/saas/fiscal/test", "POST", {});
                toast.success(r.message);
              } catch (e) {
                toast.error((e as Error).message);
              }
            }}
          >
            <Plug size={16} />
            Probar conexión
          </button>
          <button className="button primary" disabled={busy}>
            {busy ? "Guardando…" : "Guardar"}
          </button>
        </div>
      )}
    </form>
  );
}

function PemInput({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <Field
      label={label}
      hint={value ? "Archivo listo para guardar." : "Formato PEM."}
    >
      <input
        type="file"
        accept=".crt,.pem,.key,.cer,text/plain"
        onChange={async (e) => {
          const f = e.target.files?.[0];
          if (f) onChange(await readFile(f, "text"));
        }}
      />
    </Field>
  );
}

export function UpgradeNotice({ module }: { module: string }) {
  return (
    <section className="panel padded upgrade-notice">
      <h3>{module} no está incluido en tu plan</h3>
      <p>Mejorá tu plan para usarlo. El cambio es inmediato.</p>
      <Link className="button primary small" to="/settings/plan">
        Ver planes
      </Link>
    </section>
  );
}

function IntegrationsTab() {
  const { admin, has } = useSession();
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  const [secret, setSecret] = useState<{
    title: string;
    value: string;
    text: string;
  } | null>(null);
  const [hook, setHook] = useState<{ url: string; events: string[] } | null>(
    null,
  );
  const [woo, setWoo] = useState({
    url: "",
    consumerKey: "",
    consumerSecret: "",
  });
  const load = useCallback(
    () =>
      request("/api/saas/integrations")
        .then((r) => {
          setD(r);
          setWoo((w) => ({ ...w, url: r.woo?.wooUrl || w.url }));
        })
        .catch((e) => setError(e.message)),
    [],
  );
  useEffect(() => {
    if (has("api")) load();
  }, [load]);
  if (!has("api"))
    return <UpgradeNotice module="API, webhooks e integraciones" />;
  if (!d) return error ? <ErrorBox message={error} /> : <Loading />;
  const run = async (fn: () => Promise<any>, ok: string) => {
    try {
      const r = await fn();
      toast.success(ok);
      await load();
      return r;
    } catch (e) {
      toast.error((e as Error).message);
    }
  };
  return (
    <div className="settings-stack">
      <Section
        title="Claves de API"
        text="Para conectar tu sitio, un CRM o scripts propios. Enviá la clave en el encabezado X-Api-Key."
        action={
          admin && (
            <button
              className="button primary small"
              onClick={async () => {
                const name = prompt(
                  "Nombre de la clave (por ejemplo, Sitio web)",
                );
                if (!name) return;
                const r = await run(
                  () =>
                    request("/api/saas/integrations/keys", "POST", { name }),
                  "Clave creada",
                );
                if (r)
                  setSecret({
                    title: "Tu nueva clave de API",
                    value: r.key,
                    text: "Copiala ahora: por seguridad no se vuelve a mostrar.",
                  });
              }}
            >
              Crear clave
            </button>
          )
        }
      >
        <Table
          headers={["Nombre", "Prefijo", "Creada", "Último uso", ""]}
          rows={d.keys.map((k: any) => [
            k.name,
            <code>{k.prefix}</code>,
            date(k.createdAtUtc),
            k.revokedAtUtc
              ? "Revocada"
              : k.lastUsedAtUtc
                ? fullDate(k.lastUsedAtUtc)
                : "Nunca",
            admin && !k.revokedAtUtc ? (
              <button
                className="text-link danger"
                onClick={() =>
                  confirm("¿Revocar esta clave?") &&
                  run(
                    () =>
                      request(`/api/saas/integrations/keys/${k.id}`, "DELETE"),
                    "Clave revocada",
                  )
                }
              >
                <Trash2 size={14} />
                Revocar
              </button>
            ) : null,
          ])}
          empty="Todavía no creaste claves."
        />
        <p className="section-note">
          Endpoints: <code>GET /api/ext/v1/orders</code>,{" "}
          <code>/orders/{"{id}"}</code>, <code>/customers</code>,{" "}
          <code>/catalog</code> y <code>POST /api/ext/v1/appointments</code>.
        </p>
      </Section>
      <Section
        title="Webhooks"
        text="Avisamos a tu sistema cada vez que pasa algo. Cada envío va firmado con HMAC-SHA256 en X-RepairShop-Signature."
        action={
          admin && (
            <button
              className="button primary small"
              onClick={() => setHook({ url: "", events: [...d.events] })}
            >
              Agregar webhook
            </button>
          )
        }
      >
        <Table
          headers={["URL", "Eventos", "Estado", ""]}
          rows={d.hooks.map((h: any) => [
            <span className="break">{h.url}</span>,
            h.events === "*"
              ? "Todos"
              : h.events.split(",").length + " eventos",
            h.active ? "Activo" : "Inactivo",
            admin ? (
              <span className="row-actions">
                <button
                  className="text-link"
                  onClick={() =>
                    run(
                      () =>
                        request(
                          `/api/saas/integrations/webhooks/${h.id}/test`,
                          "POST",
                          {},
                        ),
                      "Envío de prueba en cola",
                    )
                  }
                >
                  Probar
                </button>
                <button
                  className="text-link danger"
                  onClick={() =>
                    confirm("¿Eliminar este webhook?") &&
                    run(
                      () =>
                        request(
                          `/api/saas/integrations/webhooks/${h.id}`,
                          "DELETE",
                        ),
                      "Webhook eliminado",
                    )
                  }
                >
                  Eliminar
                </button>
              </span>
            ) : null,
          ])}
          empty="Sin webhooks configurados."
        />
        {d.deliveries.length > 0 && (
          <Table
            headers={["Fecha", "Evento", "Estado", "Respuesta"]}
            rows={d.deliveries.map((x: any) => [
              fullDate(x.createdAtUtc),
              x.event,
              <Chip value={x.status} />,
              x.responseCode ? `HTTP ${x.responseCode}` : x.lastError || "—",
            ])}
          />
        )}
      </Section>
      <Section
        title="WooCommerce"
        text="Publicá tus repuestos y su stock disponible en tu tienda. Los productos nuevos se crean como borrador."
      >
        <form
          className="form-grid padded-grid"
          onSubmit={(e) => {
            e.preventDefault();
            run(
              () => request("/api/saas/integrations/woocommerce", "PUT", woo),
              "WooCommerce configurado",
            );
            setWoo({ ...woo, consumerKey: "", consumerSecret: "" });
          }}
        >
          <fieldset disabled={!admin} className="plain-fieldset contents">
            <Field label="URL de la tienda">
              <input
                type="url"
                placeholder="https://mitienda.com"
                value={woo.url}
                onChange={(e) => setWoo({ ...woo, url: e.target.value })}
              />
            </Field>
            <Field
              label="Consumer key"
              hint={
                d.woo?.hasKeys
                  ? "Guardada. Completá solo para cambiarla."
                  : "WooCommerce → Ajustes → Avanzado → REST API."
              }
            >
              <input
                value={woo.consumerKey}
                onChange={(e) =>
                  setWoo({ ...woo, consumerKey: e.target.value })
                }
              />
            </Field>
            <Field label="Consumer secret">
              <input
                type="password"
                value={woo.consumerSecret}
                onChange={(e) =>
                  setWoo({ ...woo, consumerSecret: e.target.value })
                }
              />
            </Field>
            <div className="form-actions span-two">
              <button className="button secondary small">Guardar</button>
              <button
                type="button"
                className="button primary small"
                disabled={!d.woo?.hasKeys}
                onClick={() =>
                  run(
                    () =>
                      request(
                        "/api/saas/integrations/woocommerce/sync",
                        "POST",
                        {},
                      ),
                    "Sincronización terminada",
                  )
                }
              >
                Sincronizar ahora
              </button>
            </div>
          </fieldset>
          {d.woo?.lastSyncAtUtc && (
            <p className="section-note span-two">
              Última sincronización: {fullDate(d.woo.lastSyncAtUtc)} ·{" "}
              {d.woo.lastSyncResult}
            </p>
          )}
        </form>
      </Section>
      {hook && (
        <Modal title="Nuevo webhook" onClose={() => setHook(null)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const r = await run(
                () => request("/api/saas/integrations/webhooks", "POST", hook),
                "Webhook creado",
              );
              if (r) {
                setHook(null);
                setSecret({
                  title: "Secreto de firma",
                  value: r.secret,
                  text: "Usalo para verificar la firma HMAC de cada envío. No se vuelve a mostrar.",
                });
              }
            }}
          >
            <div className="dialog-body">
              <Field label="URL (https)">
                <input
                  required
                  type="url"
                  value={hook.url}
                  onChange={(e) => setHook({ ...hook, url: e.target.value })}
                />
              </Field>
              <fieldset className="premium-checks">
                <legend>Eventos</legend>
                {d.events.map((ev: string) => (
                  <label key={ev}>
                    <input
                      type="checkbox"
                      checked={hook.events.includes(ev)}
                      onChange={(e) =>
                        setHook({
                          ...hook,
                          events: e.target.checked
                            ? [...hook.events, ev]
                            : hook.events.filter((x) => x !== ev),
                        })
                      }
                    />
                    {ev}
                  </label>
                ))}
              </fieldset>
            </div>
            <div className="dialog-footer">
              <button
                type="button"
                className="button secondary"
                onClick={() => setHook(null)}
              >
                Cancelar
              </button>
              <button className="button primary" disabled={!hook.events.length}>
                Crear
              </button>
            </div>
          </form>
        </Modal>
      )}
      {secret && (
        <Modal title={secret.title} onClose={() => setSecret(null)}>
          <div className="dialog-body">
            <p>{secret.text}</p>
            <pre className="code-box">{secret.value}</pre>
          </div>
          <div className="dialog-footer">
            <button
              className="button secondary"
              onClick={() => copy(secret.value)}
            >
              <Copy size={15} />
              Copiar
            </button>
            <button className="button primary" onClick={() => setSecret(null)}>
              Listo
            </button>
          </div>
        </Modal>
      )}
    </div>
  );
}

const outboxStatus: Record<string, string> = {
  Pending: "Pendiente",
  Sent: "Enviado",
  Failed: "Falló",
  Processing: "Enviando",
};

function MessagesTab() {
  const { admin } = useSession();
  const [rows, setRows] = useState<any[] | null>(null);
  const [open, setOpen] = useState<any>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    if (admin)
      request("/api/saas/notifications")
        .then(setRows)
        .catch((e) => setError(e.message));
  }, [admin]);
  if (!admin)
    return (
      <div className="info-box">
        Solo un administrador ve el registro de mensajes.
      </div>
    );
  if (!rows) return error ? <ErrorBox message={error} /> : <Loading />;
  return (
    <Section
      title="Mensajes a clientes"
      text="Emails y SMS generados por el sistema, con su estado de envío."
    >
      <Table
        headers={["Fecha", "Canal", "Destinatario", "Asunto", "Estado"]}
        rows={rows.map((m) => [
          fullDate(m.createdAtUtc),
          m.channel === "Email" ? "Email" : m.channel,
          m.recipient,
          <button className="text-link" onClick={() => setOpen(m)}>
            {m.title}
          </button>,
          <Detail
            main={outboxStatus[m.status] || m.status}
            sub={
              m.lastError ||
              (m.attemptCount > 1 ? `${m.attemptCount} intentos` : "")
            }
          />,
        ])}
        empty="Todavía no se envió ningún mensaje."
      />
      {open && (
        <Modal
          title={open.title}
          subtitle={`${open.channel} a ${open.recipient}`}
          onClose={() => setOpen(null)}
        >
          <div className="dialog-body">
            <p className="pre-line">{open.body}</p>
          </div>
        </Modal>
      )}
    </Section>
  );
}
