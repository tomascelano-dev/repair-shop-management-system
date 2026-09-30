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
import "./v2.css";

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
      const timer = setInterval(load, 20000);
      return () => clearInterval(timer);
    }
  }, [token, load]);
  if (location.pathname === "/portal")
    return (
      <>
        <Portal />
        <Toaster richColors />
      </>
    );
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
  const open = data.orders.filter(
    (o) => !["Delivered", "Cancelled"].includes(o.status),
  ).length;
  return (
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
        <div className="workspace-switch">
          <span className="workspace-avatar">T</span>
          <div>
            <strong>Mi taller</strong>
            <small>Espacio de trabajo</small>
          </div>
          <ChevronRight size={14} />
        </div>
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
          <NavLink to="/premium" end>
            <Boxes size={19} />
            Módulos premium
          </NavLink>
          <small className="nav-caption premium-nav-caption">
            GESTIÓN DEL NEGOCIO
          </small>
          {modules.map((m) => (
            <NavLink key={m.id} to={`/premium/${m.id}`}>
              <m.icon size={18} />
              {m.name}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <div className="local-card">
            <span className="live-dot" />
            <strong>Versión local · v2</strong>
            <p>
              Los registros marcados como demo son ficticios. Tus cambios se
              guardan.
            </p>
          </div>
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
            <span>Mi taller</span>
            <ChevronRight size={14} />
            <strong>
              {location.pathname.includes("/orders/")
                ? "Detalle de orden"
                : location.pathname.startsWith("/orders")
                  ? "Órdenes de trabajo"
                  : location.pathname === "/inventory"
                    ? "Inventario"
                    : location.pathname === "/customers"
                      ? "Clientes"
                      : location.pathname.startsWith("/premium")
                        ? modules.find((m) => location.pathname.endsWith(m.id))
                            ?.name || "Módulos premium"
                        : "Vista general"}
            </strong>
          </div>
          <div className="topbar-right">
            <span className="today">
              {new Date().toLocaleDateString("es-AR", {
                day: "numeric",
                month: "long",
              })}
            </span>
            <button
              className="button primary small"
              onClick={() => setIntake(true)}
            >
              <Plus size={17} />
              Nueva recepción
            </button>
          </div>
        </header>
        <main className="page">
          <ErrorBox message={error} />
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
            <Route
              path="*"
              element={<Dashboard data={data} onNew={() => setIntake(true)} />}
            />
          </Routes>
          <footer className="page-footer">
            <span>RepairShop v2</span>
            <span>
              <ShieldCheck size={13} />
              Datos guardados en tu instalación local
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
  );
}

function Login({ onLogin }: { onLogin: () => void }) {
  const [email, setEmail] = useState("admin@local");
  const [password, setPassword] = useState("Admin12345");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  return (
    <div className="login-screen">
      <div className="login-story">
        <Brand />
        <div>
          <span className="eyebrow light">REPAIRSHOP V2</span>
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
