import { useEffect, useState, useCallback } from "react";
import {
  Link,
  NavLink,
  Navigate,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from "react-router-dom";
import {
  LayoutDashboard,
  Wrench,
  Users,
  Boxes,
  Plus,
  Search,
  LogOut,
  ArrowUpRight,
  ArrowRight,
  Clock3,
  CheckCircle2,
  CircleDollarSign,
  ChevronRight,
  LayoutGrid,
  List,
  Menu,
  X,
  SlidersHorizontal,
  RefreshCw,
  ShieldCheck,
  Wallet,
  ReceiptText,
  CalendarDays,
  ClipboardList,
  HardHat,
  BarChart3,
  Settings as SettingsIcon,
  Lock,
} from "lucide-react";
import { Toaster, toast } from "sonner";
import {
  request,
  TOKEN_KEY,
  USER_KEY,
  money,
  date,
  code,
  statusNames,
  type BoardData,
  type Order,
} from "./api";
import { Brand, Status, Field, ErrorBox, Empty, Loading } from "./ui";
import Intake from "./Intake";
import OrderDetail from "./OrderDetail";
import Portal from "./Portal";
import Catalog from "./Catalog";
import Premium, { modules } from "./premium/Premium";
import {
  SessionContext,
  type Me,
  planNames,
  subscriptionNames,
  daysLeft,
} from "./saas/session";
import { Tracking, Booking, Survey, Signup } from "./saas/Public";
import Settings, { SimulatedCheckout } from "./saas/Settings";
import { Privacy, Terms } from "./saas/Legal";
import Cash from "./saas/Cash";
import Invoices, { InvoicePrint } from "./saas/Invoices";
import Agenda from "./saas/Agenda";
import Services from "./saas/Services";
import Tech from "./saas/Tech";
import Reports, { AlertsBell } from "./saas/Reports";
import { OrderTicket, SaleTicket } from "./saas/Print";
import { OnboardingCard, SubscriptionBanner } from "./saas/Widgets";
import "./v2.css";
import "./saas/saas.css";

const operations = [
  {
    to: "/agenda",
    name: "Agenda y turnos",
    icon: CalendarDays,
    module: "agenda",
  },
  { to: "/cash", name: "Caja y mostrador", icon: Wallet, module: "cash" },
  {
    to: "/invoices",
    name: "Facturación",
    icon: ReceiptText,
    module: "invoicing",
  },
  {
    to: "/services",
    name: "Servicios y precios",
    icon: ClipboardList,
    module: "catalog",
  },
  { to: "/tech", name: "App del técnico", icon: HardHat, module: "technician" },
  { to: "/reports", name: "Reportes", icon: BarChart3, module: "" },
];
const adminOnly = ["/cash", "/invoices"];
const titles: [string, string][] = [
  ["/orders/", "Detalle de orden"],
  ["/orders", "Órdenes de trabajo"],
  ["/customers", "Clientes"],
  ["/agenda", "Agenda y turnos"],
  ["/cash", "Caja y mostrador"],
  ["/invoices", "Facturación"],
  ["/services", "Servicios y precios"],
  ["/tech", "App del técnico"],
  ["/reports", "Reportes"],
  ["/settings", "Configuración"],
  ["/billing", "Suscripción"],
];

// Pages customers open without an account: tracking, online booking, surveys and the approval portal.
function PublicApp() {
  return (
    <>
      <Routes>
        <Route path="/portal" element={<Portal />} />
        <Route path="/signup" element={<Signup />} />
        <Route path="/seguimiento/:slug" element={<Tracking />} />
        <Route path="/turnos/:slug" element={<Booking />} />
        <Route path="/encuesta/:token" element={<Survey />} />
        <Route path="/terminos" element={<Terms />} />
        <Route path="/privacidad" element={<Privacy />} />
      </Routes>
      <Toaster richColors />
    </>
  );
}
const publicPaths = [
  "/portal",
  "/signup",
  "/seguimiento/",
  "/turnos/",
  "/encuesta/",
  "/terminos",
  "/privacidad",
];

export default function App() {
  const location = useLocation();
  const nav = useNavigate();
  const [token, setToken] = useState(localStorage.getItem(TOKEN_KEY));
  const [intake, setIntake] = useState(false);
  const [mobile, setMobile] = useState(false);
  const [data, setData] = useState<BoardData>({
    orders: [],
    quotes: [],
    receipts: [],
    refunds: [],
  });
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState("");
  const [me, setMe] = useState<Me | null>(null);
  const loadMe = useCallback(async () => {
    try {
      setMe(await request<Me>("/api/saas/me"));
    } catch {
      /* the session keeps working with the defaults; the 401 handler covers expired tokens */
    }
  }, []);
  const load = useCallback(async () => {
    try {
      setData(await request("/api/v2/orders"));
      setError("");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }, []);
  useEffect(() => {
    if (token) {
      load();
      loadMe();
      const timer = setInterval(() => {
        load();
        loadMe();
      }, 20000);
      return () => clearInterval(timer);
    }
  }, [token, load, loadMe]);
  if (
    publicPaths.some(
      (p) =>
        location.pathname === p ||
        (p.endsWith("/") && location.pathname.startsWith(p)),
    )
  )
    return <PublicApp />;
  if (!token)
    return (
      <>
        <Login
          onLogin={() => {
            setToken(localStorage.getItem(TOKEN_KEY));
            nav("/");
          }}
        />
        <Toaster richColors />
      </>
    );
  const user = JSON.parse(localStorage.getItem(USER_KEY) || "{}");
  const admin = (me?.user.role || user.role) === "Admin";
  const has = (m: string) => !me || !m || me.subscription.modules.includes(m);
  const session = { me, reload: loadMe, has, admin };
  const open = data.orders.filter(
    (o) => !["Delivered", "Cancelled"].includes(o.status),
  ).length;
  if (
    location.pathname.startsWith("/print/") ||
    location.pathname.startsWith("/billing/")
  )
    return (
      <SessionContext.Provider value={session}>
        <Routes>
          <Route path="/print/invoice/:id" element={<InvoicePrint />} />
          <Route path="/print/order/:id" element={<OrderTicket />} />
          <Route path="/print/sale/:id" element={<SaleTicket />} />
          <Route path="/billing/simulated" element={<SimulatedCheckout />} />
        </Routes>
        <Toaster richColors />
      </SessionContext.Provider>
    );
  const shopName = me?.profile.displayName || "Mi taller";
  const sub = me?.subscription;
  return (
    <SessionContext.Provider value={session}>
      <div className="workshop-app">
        <aside className={`sidebar ${mobile ? "is-open" : ""}`}>
          <Link to="/" aria-label="Inicio">
            <Brand />
          </Link>
          <button
            className="mobile-close icon-button"
            onClick={() => setMobile(false)}
            aria-label="Cerrar menú"
          >
            <X />
          </button>
          <Link
            to="/settings/taller"
            className="workspace-switch"
            onClick={() => setMobile(false)}
          >
            {me?.profile.logoDataUrl ? (
              <img
                className="workspace-avatar"
                src={me.profile.logoDataUrl}
                alt=""
              />
            ) : (
              <span className="workspace-avatar">
                {shopName.slice(0, 1).toUpperCase()}
              </span>
            )}
            <div>
              <strong>{shopName}</strong>
              <small>
                {sub
                  ? `Plan ${planNames[sub.plan] || sub.plan}`
                  : "Espacio de trabajo"}
              </small>
            </div>
            <ChevronRight size={14} />
          </Link>
          <small className="nav-caption">OPERACIÓN</small>
          <nav onClick={() => setMobile(false)}>
            <NavLink to="/" end>
              <LayoutDashboard size={19} />
              Vista general
            </NavLink>
            <NavLink to="/orders">
              <Wrench size={19} />
              Órdenes de trabajo<span className="nav-count">{open}</span>
            </NavLink>
            <NavLink to="/customers">
              <Users size={19} />
              Clientes
            </NavLink>
            {operations
              .filter((m) => admin || !adminOnly.includes(m.to))
              .map((m) => (
                <NavLink
                  key={m.to}
                  to={has(m.module) ? m.to : "/settings/plan"}
                  className={({ isActive }) =>
                    has(m.module) ? (isActive ? "active" : "") : "locked"
                  }
                >
                  <m.icon size={18} />
                  {m.name}
                  {!has(m.module) && <Lock size={13} className="nav-lock" />}
                </NavLink>
              ))}
            <small className="nav-caption premium-nav-caption">
              GESTIÓN DEL NEGOCIO
            </small>
            <NavLink to="/premium" end>
              <Boxes size={18} />
              Resumen de módulos
            </NavLink>
            {modules.map((m) => (
              <NavLink
                key={m.id}
                to={has(m.id) ? `/premium/${m.id}` : "/settings/plan"}
                className={({ isActive }) =>
                  has(m.id) ? (isActive ? "active" : "") : "locked"
                }
              >
                <m.icon size={18} />
                {m.name}
                {!has(m.id) && <Lock size={13} className="nav-lock" />}
              </NavLink>
            ))}
            <small className="nav-caption premium-nav-caption">CUENTA</small>
            <NavLink to="/settings">
              <SettingsIcon size={18} />
              Configuración
            </NavLink>
          </nav>
          <div className="sidebar-bottom">
            {sub && (
              <Link
                to="/settings/plan"
                className="local-card"
                onClick={() => setMobile(false)}
              >
                <span className="live-dot" />
                <strong>
                  Plan {planNames[sub.plan] || sub.plan} ·{" "}
                  {subscriptionNames[sub.status] || sub.status}
                </strong>
                <p>
                  {sub.status === "Trialing"
                    ? `Te quedan ${daysLeft(sub.trialEndsAtUtc)} días de prueba con todos los módulos.`
                    : sub.readOnly
                      ? "Cuenta en modo solo lectura."
                      : "Ver plan, pagos y módulos incluidos."}
                </p>
              </Link>
            )}
            <div className="user-card">
              <span className="avatar">
                {(user.displayName || "A").slice(0, 1)}
              </span>
              <div>
                <strong>{user.displayName || "Administrador"}</strong>
                <small>
                  {user.role === "Admin" ? "Administrador" : "Técnico"}
                </small>
              </div>
              <button
                className="icon-button"
                title="Cerrar sesión"
                onClick={() => {
                  localStorage.removeItem(TOKEN_KEY);
                  localStorage.removeItem(USER_KEY);
                  setToken(null);
                }}
              >
                <LogOut size={17} />
              </button>
            </div>
          </div>
        </aside>
        {mobile && (
          <div className="sidebar-overlay" onClick={() => setMobile(false)} />
        )}
        <div className="workspace-main">
          <header className="topbar">
            <div className="breadcrumb">
              <button
                className="icon-button menu-toggle"
                aria-label="Abrir menú"
                onClick={() => setMobile(true)}
              >
                <Menu />
              </button>
              <span>{shopName}</span>
              <ChevronRight size={14} />
              <strong>
                {titles.find(([p]) => location.pathname.startsWith(p))?.[1] ||
                  (location.pathname.startsWith("/premium")
                    ? modules.find((m) => location.pathname.endsWith(m.id))
                        ?.name || "Módulos premium"
                    : "Vista general")}
              </strong>
            </div>
            <div className="topbar-right">
              <span className="today">
                {new Date().toLocaleDateString("es-AR", {
                  day: "numeric",
                  month: "long",
                })}
              </span>
              <AlertsBell />
              <button
                className="button primary small"
                onClick={() => setIntake(true)}
              >
                <Plus size={17} />
                Nueva recepción
              </button>
            </div>
          </header>
          <SubscriptionBanner />
          <main className="page">
            <ErrorBox message={error} />
            {location.pathname === "/" && <OnboardingCard />}
            <Routes>
              <Route
                path="/"
                element={
                  busy ? (
                    <Loading />
                  ) : (
                    <Dashboard data={data} onNew={() => setIntake(true)} />
                  )
                }
              />
              <Route
                path="/orders"
                element={
                  busy ? (
                    <Loading />
                  ) : (
                    <Orders data={data} onNew={() => setIntake(true)} />
                  )
                }
              />
              <Route
                path="/orders/:id"
                element={<OrderDetail onChanged={load} />}
              />
              <Route path="/customers" element={<Catalog type="customers" />} />
              <Route
                path="/inventory"
                element={<Navigate to="/premium/stock" replace />}
              />
              <Route path="/premium" element={<Premium />} />
              <Route path="/premium/:module" element={<Premium />} />
              <Route path="/agenda" element={<Agenda />} />
              <Route path="/cash" element={<Cash />} />
              <Route path="/invoices" element={<Invoices />} />
              <Route path="/services" element={<Services />} />
              <Route path="/tech" element={<Tech />} />
              <Route path="/reports" element={<Reports />} />
              <Route
                path="/settings"
                element={<Navigate to="/settings/taller" replace />}
              />
              <Route path="/settings/:tab" element={<Settings />} />
              <Route
                path="*"
                element={
                  <Dashboard data={data} onNew={() => setIntake(true)} />
                }
              />
            </Routes>
            <footer className="page-footer">
              <span>RepairShop</span>
              <span>
                <ShieldCheck size={13} />
                Tus datos, con copias de seguridad diarias
              </span>
              <button
                onClick={() => {
                  load();
                  toast.success("Datos actualizados");
                }}
              >
                <RefreshCw size={12} />
                Actualizar
              </button>
            </footer>
          </main>
        </div>
        {intake && <Intake onClose={() => setIntake(false)} onCreated={load} />}
        <Toaster richColors position="bottom-right" />
      </div>
    </SessionContext.Provider>
  );
}

const localDemo = ["localhost", "127.0.0.1"].includes(window.location.hostname);

function Login({ onLogin }: { onLogin: () => void }) {
  const [email, setEmail] = useState(localDemo ? "admin@local" : "");
  const [password, setPassword] = useState(localDemo ? "Admin12345" : "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  return (
    <div className="login-screen">
      <div className="login-story">
        <Brand />
        <div>
          <span className="eyebrow light">GESTIÓN PARA TALLERES</span>
          <h1>
            Tu taller.
            <br />
            Todo en orden.
          </h1>
          <p>
            Desde el primer diagnóstico hasta la entrega.
            <br />
            Cada equipo, cada decisión, en un solo lugar.
          </p>
          <div className="story-card">
            <span className="story-icon">
              <CheckCircle2 />
            </span>
            <div>
              <strong>Más claridad. Menos pendientes.</strong>
              <small>Recepción · Presupuestos · Seguimiento</small>
            </div>
          </div>
        </div>
        <small>Diseñado para quienes reparan.</small>
      </div>
      <div className="login-panel">
        <div className="login-box">
          <span className="eyebrow">BIENVENIDO A TU ESPACIO</span>
          <h2>Entrá a tu taller</h2>
          <p>Continuá donde dejaste tus reparaciones.</p>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              setBusy(true);
              setError("");
              try {
                const d = await request("/api/v1/auth/login", "POST", {
                  email,
                  password,
                });
                localStorage.setItem(TOKEN_KEY, d.accessToken);
                localStorage.setItem(USER_KEY, JSON.stringify(d.user));
                onLogin();
              } catch (e) {
                setError((e as Error).message);
              } finally {
                setBusy(false);
              }
            }}
          >
            <ErrorBox message={error} />
            <Field label="Correo electrónico">
              <input
                required
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </Field>
            <Field label="Contraseña">
              <input
                required
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </Field>
            <button className="button primary full" disabled={busy}>
              {busy ? "Ingresando…" : "Ingresar al taller"}
              <ArrowRight size={17} />
            </button>
          </form>
          <p className="login-switch">
            ¿Todavía no tenés cuenta?{" "}
            <Link to="/signup">Probalo gratis 14 días</Link>
          </p>
          {localDemo && (
            <div className="login-demo">
              <span className="live-dot" />
              Demostración local
              <p>
                Admin: <b>admin@local</b> · <b>Admin12345</b>
              </p>
              <p>
                Técnico: <b>tech@local</b> · <b>Tech123456</b>
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function Dashboard({ data, onNew }: { data: BoardData; onNew: () => void }) {
  const active = data.orders.filter(
    (o) => !["Delivered", "Cancelled"].includes(o.status),
  );
  const pending = data.orders.filter((o) => o.status === "AwaitingApproval");
  const ready = data.orders.filter((o) => o.status === "Ready");
  const ars =
    data.receipts
      .filter((p) => p.currency === "ARS")
      .reduce((sum, p) => sum + p.amount, 0) -
    data.refunds.reduce((sum, r) => {
      const q = data.quotes.find((q) => q.orderId === r.orderId);
      return sum + (q?.currency === "ARS" ? r.amount : 0);
    }, 0);
  const stats = [
    {
      label: "Órdenes abiertas",
      value: active.length,
      sub: "Equipos en proceso",
      Icon: Wrench,
      color: "blue",
    },
    {
      label: "Por aprobar",
      value: pending.length,
      sub: "Esperando a tu cliente",
      Icon: Clock3,
      color: "amber",
    },
    {
      label: "Listos para retirar",
      value: ready.length,
      sub: "Reparación finalizada",
      Icon: CheckCircle2,
      color: "green",
    },
    {
      label: "Cobrado neto · ARS",
      value: money(ars),
      sub: "Cobros menos devoluciones · total",
      Icon: CircleDollarSign,
      color: "purple",
    },
  ];
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">VISTA GENERAL</span>
          <h1>
            El taller, en orden<span className="accent">.</span>
          </h1>
          <p>Esto es lo que está pasando con tus reparaciones.</p>
        </div>
        <button className="button secondary" onClick={onNew}>
          <Plus size={18} />
          Recibir un equipo
        </button>
      </div>
      <div className="stats-grid">
        {stats.map((s) => (
          <div className="stat-card" key={s.label}>
            <div>
              <span>{s.label}</span>
              <span className={`stat-icon ${s.color}`}>
                <s.Icon size={18} />
              </span>
            </div>
            <strong>{s.value}</strong>
            <small>{s.sub}</small>
          </div>
        ))}
      </div>
      <div className="dashboard-split">
        <section className="panel flow-panel">
          <div className="panel-heading">
            <div>
              <h3>Flujo del taller</h3>
              <p>Distribución de las órdenes abiertas</p>
            </div>
            <span className="soft-label">{active.length} en curso</span>
          </div>
          <div className="flow-stages">
            {[
              "Received",
              "Diagnosing",
              "AwaitingApproval",
              "InProgress",
              "QualityCheck",
              "WaitingParts",
              "Ready",
            ].map((s) => {
              const n = active.filter((o) => o.status === s).length;
              return (
                <Link to={`/orders?status=${s}`} key={s}>
                  <span className={`flow-dot s-${s}`} />
                  <span>{statusNames[s]}</span>
                  <div className="flow-track">
                    <i
                      style={{
                        width: `${Math.max(n ? 8 : 0, (n / Math.max(active.length, 1)) * 100)}%`,
                      }}
                    />
                  </div>
                  <strong>{n}</strong>
                </Link>
              );
            })}
          </div>
        </section>
        <section className="attention-panel">
          <div className="attention-symbol">
            <Clock3 size={24} />
          </div>
          <h3>El próximo paso cuenta.</h3>
          <p>
            {pending.length
              ? `Tenés ${pending.length} presupuesto${pending.length > 1 ? "s" : ""} esperando respuesta. Compartí el enlace para que el cliente confirme.`
              : "Tus presupuestos están al día. Revisá el tablero y elegí el próximo equipo."}
          </p>
          <Link to="/orders?status=AwaitingApproval">
            Ver presupuestos pendientes
            <ArrowUpRight size={18} />
          </Link>
          <div className="attention-bottom">
            <CheckCircle2 size={17} />
            <span>
              {ready.length} equipo{ready.length !== 1 ? "s" : ""} listo
              {ready.length !== 1 ? "s" : ""} para retirar
            </span>
          </div>
        </section>
      </div>
      <section className="panel">
        <div className="panel-heading">
          <div>
            <h3>Últimas órdenes</h3>
            <p>Lo más reciente de tu mesa de trabajo</p>
          </div>
          <Link className="text-link" to="/orders">
            Ver todas
            <ArrowRight size={16} />
          </Link>
        </div>
        <OrderTable orders={data.orders.slice(0, 6)} data={data} />
      </section>
    </>
  );
}

export function OrderTable({
  orders,
  data,
}: {
  orders: Order[];
  data: BoardData;
}) {
  const nav = useNavigate();
  return orders.length ? (
    <div className="table-scroll">
      <table className="orders-table">
        <thead>
          <tr>
            <th>ORDEN / EQUIPO</th>
            <th>CLIENTE</th>
            <th>ESTADO</th>
            <th>PRESUPUESTO</th>
            <th>INGRESO</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {orders.map((o) => {
            const q = data.quotes
              .filter((q) => q.orderId === o.id)
              .sort((a, b) => b.revision - a.revision)[0];
            return (
              <tr key={o.id} onClick={() => nav(`/orders/${o.id}`)}>
                <td>
                  <Link className="device-cell" to={`/orders/${o.id}`}>
                    <span className="device-icon">
                      <Wrench size={17} />
                    </span>
                    <span>
                      <strong>{o.deviceLabel}</strong>
                      <small>
                        {code(o.number)} {o.isDemo && <em>DEMO</em>}
                        {o.priority === "Alta" && (
                          <b className="priority-mini">ALTA</b>
                        )}
                      </small>
                    </span>
                  </Link>
                </td>
                <td>
                  <span className="cell-title">{o.customerName}</span>
                  <small>{o.customerPhone}</small>
                </td>
                <td>
                  <Status value={o.status} />
                </td>
                <td>
                  <strong className="amount">
                    {q ? money(q.total, q.currency) : "Por definir"}
                  </strong>
                  <small>{q?.currency || "Sin presupuesto"}</small>
                </td>
                <td>
                  <span>{date(o.createdAtUtc)}</span>
                </td>
                <td>
                  <ChevronRight size={17} className="muted" />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  ) : (
    <Empty
      title="Todavía no hay órdenes"
      text="Recibí tu primer equipo para empezar a organizar el taller."
    />
  );
}

function Orders({ data, onNew }: { data: BoardData; onNew: () => void }) {
  const location = useLocation();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState(
    new URLSearchParams(location.search).get("status") || "",
  );
  const [board, setBoard] = useState(false);
  useEffect(() => {
    setStatus(new URLSearchParams(location.search).get("status") || "");
  }, [location.search]);
  const orders = data.orders.filter(
    (o) =>
      (!status || o.status === status) &&
      `${o.customerName} ${o.deviceLabel} ${o.identifier || ""} ${code(o.number)}`
        .toLowerCase()
        .includes(search.toLowerCase()),
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">TU MESA DE TRABAJO</span>
          <h1>Órdenes de trabajo</h1>
          <p>Cada reparación, desde el ingreso hasta la entrega.</p>
        </div>
        <button className="button primary" onClick={onNew}>
          <Plus size={18} />
          Nueva recepción
        </button>
      </div>
      <div className="toolbar">
        <div className="search-input">
          <Search size={18} />
          <input
            aria-label="Buscar órdenes"
            placeholder="Buscar equipo, cliente, IMEI u orden…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <div className="filter-select">
          <SlidersHorizontal size={17} />
          <select
            aria-label="Filtrar por estado"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="">Todos los estados</option>
            {Object.entries(statusNames).map(([k, v]) => (
              <option value={k} key={k}>
                {v}
              </option>
            ))}
          </select>
        </div>
        <div className="view-toggle">
          <button
            className={!board ? "selected" : ""}
            onClick={() => setBoard(false)}
            aria-label="Vista de lista"
          >
            <List size={18} />
          </button>
          <button
            className={board ? "selected" : ""}
            onClick={() => setBoard(true)}
            aria-label="Vista de tablero"
          >
            <LayoutGrid size={18} />
          </button>
        </div>
      </div>
      <p className="result-count">{orders.length} órdenes encontradas</p>
      {board ? (
        <div className="kanban">
          {(status
            ? [status]
            : [
                "Received",
                "Diagnosing",
                "AwaitingApproval",
                "InProgress",
                "WaitingParts",
                "QualityCheck",
                "Ready",
                "Delivered",
                "Cancelled",
              ]
          ).map((s) => (
            <section key={s}>
              <header>
                <Status value={s} />
                <span>{orders.filter((o) => o.status === s).length}</span>
              </header>
              {orders
                .filter((o) => o.status === s)
                .map((o) => (
                  <Link
                    className="kanban-card"
                    to={`/orders/${o.id}`}
                    key={o.id}
                  >
                    <span>
                      {code(o.number)}
                      {o.priority === "Alta" && <b>Prioridad alta</b>}
                    </span>
                    <h4>{o.deviceLabel}</h4>
                    <p>{o.issueDescription}</p>
                    <footer>
                      {o.customerName}
                      <ChevronRight size={15} />
                    </footer>
                  </Link>
                ))}
            </section>
          ))}
        </div>
      ) : (
        <section className="panel">
          <OrderTable orders={orders} data={data} />
        </section>
      )}
    </>
  );
}
