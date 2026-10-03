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
  Wallet,
  ReceiptText,
  CalendarDays,
  ClipboardList,
  HardHat,
  BarChart3,
  Settings as SettingsIcon,
  Lock,
} from "lucide-react";
import { Toaster } from "sonner";
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
import { SessionContext, type Me, planNames, daysLeft } from "./saas/session";
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
  { to: "/agenda", name: "Agenda", icon: CalendarDays, module: "agenda" },
  { to: "/cash", name: "Caja", icon: Wallet, module: "cash" },
  {
    to: "/invoices",
    name: "Facturación",
    icon: ReceiptText,
    module: "invoicing",
  },
];
const business = [
  {
    to: "/services",
    name: "Servicios y precios",
    icon: ClipboardList,
    module: "catalog",
  },
  { to: "/reports", name: "Reportes", icon: BarChart3, module: "" },
  { to: "/tech", name: "App del técnico", icon: HardHat, module: "technician" },
];
const adminOnly = ["/cash", "/invoices"];

function ModuleLink({
  to,
  name,
  icon: Icon,
  unlocked,
}: {
  to: string;
  name: string;
  icon: typeof Wrench;
  unlocked: boolean;
}) {
  return (
    <NavLink
      to={unlocked ? to : "/settings/plan"}
      className={({ isActive }) =>
        unlocked ? (isActive ? "active" : "") : "locked"
      }
    >
      <Icon size={18} />
      {name}
      {!unlocked && <Lock size={13} className="nav-lock" />}
    </NavLink>
  );
}
const titles: [string, string][] = [
  ["/orders/", "Detalle de orden"],
  ["/orders", "Órdenes"],
  ["/customers", "Clientes"],
  ["/agenda", "Agenda"],
  ["/cash", "Caja"],
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
                  ? sub.status === "Trialing"
                    ? `Prueba · ${daysLeft(sub.trialEndsAtUtc)} días`
                    : `Plan ${planNames[sub.plan] || sub.plan}`
                  : "Espacio de trabajo"}
              </small>
            </div>
            <ChevronRight size={14} />
          </Link>
          <small className="nav-caption">TALLER</small>
          <nav onClick={() => setMobile(false)}>
            <NavLink to="/" end>
              <LayoutDashboard size={19} />
              Inicio
            </NavLink>
            <NavLink to="/orders">
              <Wrench size={19} />
              Órdenes<span className="nav-count">{open}</span>
            </NavLink>
            <NavLink to="/customers">
              <Users size={19} />
              Clientes
            </NavLink>
            {operations
              .filter((m) => admin || !adminOnly.includes(m.to))
              .map((m) => (
                <ModuleLink key={m.to} {...m} unlocked={has(m.module)} />
              ))}
            <small className="nav-caption premium-nav-caption">NEGOCIO</small>
            {business
              .filter((m) => admin || !adminOnly.includes(m.to))
              .map((m) => (
                <ModuleLink key={m.to} {...m} unlocked={has(m.module)} />
              ))}
            <NavLink to="/premium">
              <Boxes size={18} />
              Módulos
            </NavLink>
            <small className="nav-caption premium-nav-caption">CUENTA</small>
            <NavLink to="/settings">
              <SettingsIcon size={18} />
              Configuración
            </NavLink>
          </nav>
          <div className="sidebar-bottom">
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
              <strong>
                {titles.find(([p]) => location.pathname.startsWith(p))?.[1] ||
                  (location.pathname.startsWith("/premium")
                    ? modules.find((m) => location.pathname.endsWith(m.id))
                        ?.name || "Módulos"
                    : "Inicio")}
              </strong>
            </div>
            <div className="topbar-right">
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
                element={busy ? <Loading /> : <Dashboard data={data} />}
              />
              <Route
                path="/orders"
                element={busy ? <Loading /> : <Orders data={data} />}
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
              <Route path="*" element={<Dashboard data={data} />} />
            </Routes>
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

function Dashboard({ data }: { data: BoardData }) {
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
      to: "/orders",
      Icon: Wrench,
      color: "blue",
    },
    {
      label: "Por aprobar",
      value: pending.length,
      to: "/orders?status=AwaitingApproval",
      Icon: Clock3,
      color: "amber",
    },
    {
      label: "Listos para retirar",
      value: ready.length,
      to: "/orders?status=Ready",
      Icon: CheckCircle2,
      color: "green",
    },
    {
      label: "Cobrado",
      value: money(ars),
      to: "/reports",
      Icon: CircleDollarSign,
      color: "purple",
    },
  ];
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>Inicio</h1>
        </div>
      </div>
      <div className="stats-grid">
        {stats.map((s) => (
          <Link className="stat-card" key={s.label} to={s.to}>
            <div>
              <span>{s.label}</span>
              <span className={`stat-icon ${s.color}`}>
                <s.Icon size={18} />
              </span>
            </div>
            <strong>{s.value}</strong>
          </Link>
        ))}
      </div>
      <div className="dashboard-split">
        <section className="panel flow-panel">
          <div className="panel-heading">
            <div>
              <h3>Órdenes por estado</h3>
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
      </div>
      <section className="panel">
        <div className="panel-heading">
          <div>
            <h3>Últimas órdenes</h3>
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

function Orders({ data }: { data: BoardData }) {
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
          <h1>Órdenes</h1>
        </div>
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
