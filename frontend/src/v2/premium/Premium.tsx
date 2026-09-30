import { useCallback, useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import {
  ShoppingCart,
  PackageCheck,
  CircleDollarSign,
  ShieldCheck,
  Smartphone,
  Building2,
  ArrowUpRight,
  RefreshCw,
} from "lucide-react";
import { toast } from "sonner";
import { request, USER_KEY } from "../api";
import { ErrorBox, Loading } from "../ui";
import {
  PremiumContext,
  Editor,
  type EditSpec,
  type Workspace,
} from "./shared";
import Purchases from "./Purchases";
import Stock from "./Stock";
import { Profitability, Warranties } from "./ProfitWarranty";
import Resale from "./Resale";
import Business from "./Business";
import "./premium.css";

export const modules = [
  {
    id: "purchases",
    name: "Compras y precios",
    description: "Listas de proveedores, equivalencias y recepción de compras.",
    icon: ShoppingCart,
    color: "blue",
  },
  {
    id: "stock",
    name: "Stock avanzado",
    description: "Lotes, reservas por orden, consumos y transferencias.",
    icon: PackageCheck,
    color: "green",
  },
  {
    id: "profit",
    name: "Rentabilidad",
    description: "Costos, tiempos técnicos y resultado de cada reparación.",
    icon: CircleDollarSign,
    color: "purple",
  },
  {
    id: "warranties",
    name: "Garantías y calidad",
    description: "Devoluciones, reclamos a proveedores y costo de retrabajos.",
    icon: ShieldCheck,
    color: "amber",
  },
  {
    id: "resale",
    name: "Reacondicionados",
    description: "Compras, canjes, preparación y margen de reventa.",
    icon: Smartphone,
    color: "blue",
  },
  {
    id: "business",
    name: "Empresas y sucursales",
    description: "Contratos, activos, recepción por lote y abonos mensuales.",
    icon: Building2,
    color: "green",
  },
];

export default function Premium() {
  const { module } = useParams();
  const [query] = useSearchParams();
  const [d, setD] = useState<Workspace | null>(null),
    [error, setError] = useState(""),
    [refreshing, setRefreshing] = useState(false),
    [form, setForm] = useState<EditSpec | null>(null);
  const admin =
    JSON.parse(localStorage.getItem(USER_KEY) || "{}").role === "Admin";
  const load = useCallback(async () => {
    const next = await request<Workspace>("/api/v2/premium/workspace");
    setD(next);
    setError("");
  }, []);
  useEffect(() => {
    setForm(null);
    load().catch((e) => setError(e.message));
  }, [load, module]);
  async function mutate(path: string, body: any) {
    try {
      const result = await request(`/api/v2/premium/${path}`, "POST", body);
      await load();
      toast.success("Cambios guardados");
      return result;
    } catch (e) {
      toast.error((e as Error).message);
      throw e;
    }
  }
  const info = modules.find((m) => m.id === module);
  if (!d)
    return (
      <>
        <ErrorBox message={error} />
        {error ? (
          <button
            className="button secondary"
            onClick={() => load().catch((e) => setError(e.message))}
          >
            Reintentar
          </button>
        ) : (
          <Loading />
        )}
      </>
    );
  return (
    <PremiumContext.Provider
      value={{
        d,
        admin,
        mutate,
        edit: setForm,
        orderFilter: query.get("order") || "",
      }}
    >
      <div className="premium-page">
        <div className="page-heading">
          <div>
            <Link to="/premium" className="eyebrow">
              MÓDULOS PREMIUM
            </Link>
            <h1>
              {info?.name || "Más control sobre tu negocio"}
              <span className="accent">.</span>
            </h1>
            <p>
              {info?.description ||
                "Comprá mejor, cuidá tus márgenes y hacé crecer la operación."}
            </p>
          </div>
          <button
            className="button secondary small"
            disabled={refreshing}
            onClick={async () => {
              setRefreshing(true);
              try {
                await load();
              } catch (e) {
                setError((e as Error).message);
              } finally {
                setRefreshing(false);
              }
            }}
          >
            <RefreshCw size={16} className={refreshing ? "spin" : ""} />
            Actualizar
          </button>
        </div>
        <ErrorBox message={error} />
        {query.get("order") && (
          <div className="premium-context-bar">
            <span>Trabajando con una orden seleccionada</span>
            <Link to={`/orders/${query.get("order")}`}>
              Volver a la orden →
            </Link>
            <Link to={`/premium/${module}`}>Ver todas</Link>
          </div>
        )}
        {!info && (
          <>
            <div className="premium-welcome">
              <span className="live-dot" />
              <div>
                <strong>Herramientas para hacer crecer el taller</strong>
                <p>
                  Cada módulo trabaja con tus órdenes, clientes y stock en
                  tiempo real.
                </p>
              </div>
            </div>
            <div className="module-grid">
              {modules.map((m) => (
                <Link
                  key={m.id}
                  to={`/premium/${m.id}`}
                  className="module-card"
                >
                  <span className={`stat-icon ${m.color}`}>
                    <m.icon size={24} />
                  </span>
                  <h2>{m.name}</h2>
                  <p>{m.description}</p>
                  <div>
                    <span>Abrir módulo</span>
                    <ArrowUpRight size={19} />
                  </div>
                </Link>
              ))}
            </div>
            <div className="premium-journey">
              <strong>Probá el circuito completo</strong>
              <p>
                Recibí un repuesto en Compras, reservalo para una orden en Stock
                y consumilo cuando lo instales. Después registrá el tiempo de
                trabajo y revisá el resultado en Rentabilidad.
              </p>
            </div>
          </>
        )}
        {module === "purchases" && <Purchases />}
        {module === "stock" && <Stock />}
        {module === "profit" && <Profitability />}
        {module === "warranties" && <Warranties />}
        {module === "resale" && <Resale />}
        {module === "business" && <Business />}
        {form && <Editor spec={form} onClose={() => setForm(null)} />}
      </div>
    </PremiumContext.Provider>
  );
}
