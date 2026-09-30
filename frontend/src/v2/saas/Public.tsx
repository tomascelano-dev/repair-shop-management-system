import { useEffect, useState, type CSSProperties, type ReactNode } from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import {
  ArrowRight,
  CalendarCheck,
  CheckCircle2,
  MapPin,
  Phone,
  Search,
  Star,
} from "lucide-react";
import {
  request,
  TOKEN_KEY,
  USER_KEY,
  money,
  fullDate,
  statusNames,
} from "../api";
import { Brand, Status, Field, ErrorBox, Loading } from "../ui";
import { time } from "./session";
import "./saas.css";

// Header shared by the customer-facing pages, using the shop's logo and color.
function ShopShell({
  shop,
  embed,
  children,
}: {
  shop: any;
  embed?: boolean;
  children: ReactNode;
}) {
  const style = { "--shop": shop?.primaryColor || "#117f76" } as CSSProperties;
  return (
    <div className={`public-page ${embed ? "embed" : ""}`} style={style}>
      {!embed && (
        <header className="public-header">
          {shop?.logoDataUrl ? (
            <img src={shop.logoDataUrl} alt={shop.displayName} />
          ) : (
            <strong>{shop?.displayName || "Taller"}</strong>
          )}
          {shop && (
            <div className="public-contact">
              {shop.phone && (
                <span>
                  <Phone size={14} />
                  {shop.phone}
                </span>
              )}
              {shop.address && (
                <span>
                  <MapPin size={14} />
                  {shop.address}
                  {shop.city ? `, ${shop.city}` : ""}
                </span>
              )}
            </div>
          )}
        </header>
      )}
      <main>{children}</main>
      {!embed && <footer>Gestionado con RepairShop</footer>}
    </div>
  );
}

function useShop(slug?: string) {
  const [shop, setShop] = useState<any>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    request(`/api/public/shops/${slug}`)
      .then(setShop)
      .catch((e) => setError(e.message));
  }, [slug]);
  return { shop, error };
}

export function Tracking() {
  const { slug } = useParams();
  const [params, setParams] = useSearchParams();
  const embed = params.get("embed") === "1";
  const { shop, error: shopError } = useShop(slug);
  const [codeValue, setCode] = useState(params.get("codigo") || "");
  const [result, setResult] = useState<any>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function find(value = codeValue) {
    if (!value.trim()) return;
    setBusy(true);
    setError("");
    try {
      setResult(
        await request(
          `/api/public/shops/${slug}/track?code=${encodeURIComponent(value.trim())}`,
        ),
      );
      if (!embed) setParams({ codigo: value.trim() });
    } catch (e) {
      setResult(null);
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  useEffect(() => {
    if (params.get("codigo")) find(params.get("codigo")!);
  }, [slug]);
  const steps = ["Received", "Diagnosing", "InProgress", "Ready", "Delivered"];
  const current = result
    ? Math.max(
        0,
        steps.indexOf(
          ["AwaitingApproval"].includes(result.status)
            ? "Diagnosing"
            : ["WaitingParts", "QualityCheck"].includes(result.status)
              ? "InProgress"
              : result.status,
        ),
      )
    : 0;
  return (
    <ShopShell shop={shop} embed={embed}>
      <ErrorBox message={shopError} />
      <section className="public-card">
        <span className="eyebrow">SEGUIMIENTO DE REPARACIÓN</span>
        <h1>¿Cómo va tu equipo?</h1>
        <p>Ingresá el código que figura en tu comprobante.</p>
        <form
          className="public-search"
          onSubmit={(e) => {
            e.preventDefault();
            find();
          }}
        >
          <input
            aria-label="Código de seguimiento"
            placeholder="Ej.: K7M2QX9A"
            value={codeValue}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            maxLength={12}
          />
          <button className="button primary shop-bg" disabled={busy}>
            <Search size={16} />
            {busy ? "Buscando…" : "Consultar"}
          </button>
        </form>
        <ErrorBox message={error} />
      </section>
      {result && (
        <section className="public-card">
          <div className="track-head">
            <div>
              <small>{result.number}</small>
              <h2>{result.deviceLabel}</h2>
              <p>
                Hola{result.customer ? `, ${result.customer}` : ""}. Última
                actualización: {fullDate(result.updatedAtUtc)}.
              </p>
            </div>
            <Status value={result.status} />
          </div>
          {result.status === "Cancelled" ? (
            <div className="info-box">
              La orden fue cancelada. Consultá con el taller.
            </div>
          ) : (
            <ol className="track-steps">
              {steps.map((s, i) => (
                <li key={s} className={i <= current ? "done" : ""}>
                  <i />
                  <span>{statusNames[s]}</span>
                </li>
              ))}
            </ol>
          )}
          {result.status === "AwaitingApproval" && (
            <div className="info-box">
              El presupuesto está esperando tu aprobación. Revisá el enlace que
              te envió el taller.
            </div>
          )}
          {result.quote && (
            <p className="muted-line">
              Presupuesto: {money(result.quote.total, result.quote.currency)}
            </p>
          )}
          {result.history?.length > 0 && (
            <ul className="track-history">
              {result.history.map((h: any, i: number) => (
                <li key={i}>
                  <span>{statusNames[h.status] || h.status}</span>
                  <small>{fullDate(h.changedAtUtc)}</small>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
      {shop?.onlineBooking && !embed && (
        <Link className="public-cta" to={`/turnos/${slug}`}>
          <CalendarCheck size={18} />
          Reservar un turno
          <ArrowRight size={16} />
        </Link>
      )}
    </ShopShell>
  );
}

const pad = (n: number) => String(n).padStart(2, "0");
const isoDay = (d: Date) =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

export function Booking() {
  const { slug } = useParams();
  const { shop, error: shopError } = useShop(slug);
  const days = Array.from({ length: 14 }, (_, i) => {
    const d = new Date();
    d.setDate(d.getDate() + i);
    return d;
  }).filter((d) => shop?.hours?.days?.includes(d.getDay()) ?? true);
  const [day, setDay] = useState("");
  const [slots, setSlots] = useState<string[] | null>(null);
  const [slot, setSlot] = useState("");
  const [form, setForm] = useState({
    customerName: "",
    phone: "",
    email: "",
    deviceLabel: "",
    reason: "",
  });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState<string>("");
  useEffect(() => {
    if (!day) return;
    setSlots(null);
    setSlot("");
    request(`/api/public/shops/${slug}/slots?date=${day}`)
      .then(setSlots)
      .catch((e) => setError(e.message));
  }, [day, slug]);
  useEffect(() => {
    if (!day && days.length) setDay(isoDay(days[0]));
  }, [shop]);
  if (shop && !shop.onlineBooking)
    return (
      <ShopShell shop={shop}>
        <section className="public-card">
          <h1>Turnos no disponibles</h1>
          <p>Este taller no toma turnos online. Comunicate por teléfono.</p>
        </section>
      </ShopShell>
    );
  return (
    <ShopShell shop={shop}>
      <ErrorBox message={shopError} />
      {done ? (
        <section className="public-card center">
          <CheckCircle2 size={42} className="shop-color" />
          <h1>¡Turno reservado!</h1>
          <p>
            Te esperamos el{" "}
            {new Date(done).toLocaleDateString("es-AR", {
              weekday: "long",
              day: "numeric",
              month: "long",
            })}{" "}
            a las {time(done)}.
            {form.email && " Te enviamos la confirmación por email."}
          </p>
        </section>
      ) : (
        <section className="public-card">
          <span className="eyebrow">TURNOS ONLINE</span>
          <h1>Reservá tu turno</h1>
          <p>Elegí el día y el horario que te quedan mejor.</p>
          <div className="day-strip">
            {days.map((d) => (
              <button
                key={isoDay(d)}
                className={day === isoDay(d) ? "active" : ""}
                onClick={() => setDay(isoDay(d))}
              >
                <small>
                  {d.toLocaleDateString("es-AR", { weekday: "short" })}
                </small>
                <strong>{d.getDate()}</strong>
              </button>
            ))}
          </div>
          {slots === null ? (
            day && <Loading />
          ) : slots.length === 0 ? (
            <div className="info-box">No quedan horarios libres ese día.</div>
          ) : (
            <div className="slot-grid">
              {slots.map((s) => (
                <button
                  key={s}
                  className={slot === s ? "active" : ""}
                  onClick={() => setSlot(s)}
                >
                  {time(s)}
                </button>
              ))}
            </div>
          )}
          {slot && (
            <form
              className="booking-form"
              onSubmit={async (e) => {
                e.preventDefault();
                setBusy(true);
                setError("");
                try {
                  const r = await request(
                    `/api/public/shops/${slug}/appointments`,
                    "POST",
                    { ...form, startsAtUtc: slot },
                  );
                  setDone(r.startsAtUtc);
                } catch (e) {
                  setError((e as Error).message);
                } finally {
                  setBusy(false);
                }
              }}
            >
              <ErrorBox message={error} />
              <div className="form-grid">
                <Field label="Nombre y apellido">
                  <input
                    required
                    minLength={3}
                    value={form.customerName}
                    onChange={(e) =>
                      setForm({ ...form, customerName: e.target.value })
                    }
                  />
                </Field>
                <Field label="Teléfono">
                  <input
                    required
                    minLength={6}
                    value={form.phone}
                    onChange={(e) =>
                      setForm({ ...form, phone: e.target.value })
                    }
                  />
                </Field>
                <Field label="Email (opcional)">
                  <input
                    type="email"
                    value={form.email}
                    onChange={(e) =>
                      setForm({ ...form, email: e.target.value })
                    }
                  />
                </Field>
                <Field label="Equipo">
                  <input
                    placeholder="Ej.: iPhone 13"
                    value={form.deviceLabel}
                    onChange={(e) =>
                      setForm({ ...form, deviceLabel: e.target.value })
                    }
                  />
                </Field>
                <div className="span-two">
                  <Field label="¿Qué le pasa?">
                    <textarea
                      required
                      minLength={3}
                      rows={3}
                      value={form.reason}
                      onChange={(e) =>
                        setForm({ ...form, reason: e.target.value })
                      }
                    />
                  </Field>
                </div>
              </div>
              <button className="button primary full shop-bg" disabled={busy}>
                {busy ? "Reservando…" : `Confirmar turno de las ${time(slot)}`}
              </button>
            </form>
          )}
        </section>
      )}
    </ShopShell>
  );
}

export function Survey() {
  const { token } = useParams();
  const [d, setD] = useState<any>(null);
  const [score, setScore] = useState<number | null>(null);
  const [comment, setComment] = useState("");
  const [error, setError] = useState("");
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    request(`/api/public/surveys/${token}`)
      .then(setD)
      .catch((e) => setError(e.message));
  }, [token]);
  const shop = d && {
    displayName: d.shopName,
    logoDataUrl: d.logoDataUrl,
    primaryColor: d.primaryColor,
  };
  return (
    <ShopShell shop={shop}>
      <ErrorBox message={error} />
      {!d && !error && <Loading />}
      {d &&
        (sent || d.answered ? (
          <section className="public-card center">
            <Star size={40} className="shop-color" />
            <h1>¡Gracias por tu opinión!</h1>
            <p>Nos ayuda a mejorar cada reparación.</p>
          </section>
        ) : (
          <section className="public-card">
            <span className="eyebrow">ENCUESTA</span>
            <h1>¿Cómo fue tu experiencia?</h1>
            <p>
              Reparamos tu {d.device} en {d.shopName}. ¿Qué tan probable es que
              nos recomiendes?
            </p>
            <div className="nps-scale">
              {Array.from({ length: 11 }, (_, i) => (
                <button
                  key={i}
                  className={score === i ? "active" : ""}
                  onClick={() => setScore(i)}
                  aria-label={`${i} de 10`}
                >
                  {i}
                </button>
              ))}
            </div>
            <div className="nps-legend">
              <small>Nada probable</small>
              <small>Muy probable</small>
            </div>
            <Field label="Comentario (opcional)">
              <textarea
                rows={3}
                maxLength={1000}
                value={comment}
                onChange={(e) => setComment(e.target.value)}
              />
            </Field>
            <button
              className="button primary full shop-bg"
              disabled={score === null || busy}
              onClick={async () => {
                setBusy(true);
                setError("");
                try {
                  await request(`/api/public/surveys/${token}`, "POST", {
                    score,
                    comment,
                  });
                  setSent(true);
                } catch (e) {
                  setError((e as Error).message);
                } finally {
                  setBusy(false);
                }
              }}
            >
              Enviar respuesta
            </button>
          </section>
        ))}
    </ShopShell>
  );
}

export function Signup() {
  const nav = useNavigate();
  const [form, setForm] = useState({
    shopName: "",
    ownerName: "",
    email: "",
    password: "",
    phone: "",
    city: "",
  });
  const [accept, setAccept] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const set = (k: string, v: string) => setForm({ ...form, [k]: v });
  return (
    <div className="login-screen">
      <div className="login-story">
        <Brand />
        <div>
          <span className="eyebrow light">14 DÍAS GRATIS</span>
          <h1>
            Tu taller,
            <br />
            en línea hoy.
          </h1>
          <p>
            Órdenes, presupuestos con firma, caja, factura electrónica ARCA,
            turnos y avisos automáticos. Sin tarjeta para empezar.
          </p>
          <div className="story-card">
            <span className="story-icon">
              <CheckCircle2 />
            </span>
            <div>
              <strong>Todos los módulos durante la prueba</strong>
              <small>Después elegís el plan que te sirve</small>
            </div>
          </div>
        </div>
        <small>Diseñado para quienes reparan.</small>
      </div>
      <div className="login-panel">
        <div className="login-box">
          <span className="eyebrow">CREÁ TU CUENTA</span>
          <h2>Empezá tu prueba gratis</h2>
          <p>En un minuto tenés tu taller funcionando.</p>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              setBusy(true);
              setError("");
              try {
                const d = await request("/api/saas/signup", "POST", form);
                localStorage.setItem(TOKEN_KEY, d.accessToken);
                localStorage.setItem(USER_KEY, JSON.stringify(d.user));
                nav("/");
                location.reload();
              } catch (e) {
                setError((e as Error).message);
              } finally {
                setBusy(false);
              }
            }}
          >
            <ErrorBox message={error} />
            <Field label="Nombre del taller">
              <input
                required
                minLength={2}
                value={form.shopName}
                onChange={(e) => set("shopName", e.target.value)}
              />
            </Field>
            <Field label="Tu nombre">
              <input
                required
                minLength={2}
                autoComplete="name"
                value={form.ownerName}
                onChange={(e) => set("ownerName", e.target.value)}
              />
            </Field>
            <Field label="Email">
              <input
                required
                type="email"
                autoComplete="username"
                value={form.email}
                onChange={(e) => set("email", e.target.value)}
              />
            </Field>
            <Field label="Contraseña" hint="Al menos 10 caracteres.">
              <input
                required
                type="password"
                minLength={10}
                autoComplete="new-password"
                value={form.password}
                onChange={(e) => set("password", e.target.value)}
              />
            </Field>
            <div className="form-grid">
              <Field label="Teléfono">
                <input
                  value={form.phone}
                  onChange={(e) => set("phone", e.target.value)}
                />
              </Field>
              <Field label="Ciudad">
                <input
                  value={form.city}
                  onChange={(e) => set("city", e.target.value)}
                />
              </Field>
            </div>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={accept}
                onChange={(e) => setAccept(e.target.checked)}
              />
              Acepto los términos del servicio y la política de privacidad.
            </label>
            <button className="button primary full" disabled={busy || !accept}>
              {busy ? "Creando tu taller…" : "Crear mi taller"}
              <ArrowRight size={17} />
            </button>
          </form>
          <p className="login-switch">
            ¿Ya tenés cuenta? <Link to="/login">Iniciá sesión</Link>
          </p>
        </div>
      </div>
    </div>
  );
}
