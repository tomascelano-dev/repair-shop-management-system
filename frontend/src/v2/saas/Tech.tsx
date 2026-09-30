import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  CheckCircle2,
  Home,
  MapPin,
  Pause,
  Phone,
  Play,
  UserPlus,
} from "lucide-react";
import { toast } from "sonner";
import { request, code, fullDate } from "../api";
import { ErrorBox, Loading, Status } from "../ui";
import { Editor, type EditSpec, type Row } from "../premium/shared";
import { useSession, time } from "./session";
import { UpgradeNotice } from "./Settings";

const stateNames: Record<string, string> = {
  Assigned: "Asignada",
  Working: "Trabajando",
  Paused: "En pausa",
  Finished: "Terminada",
};

function useTick(active: boolean) {
  const [, setTick] = useState(0);
  useEffect(() => {
    if (!active) return;
    const t = setInterval(() => setTick((x) => x + 1), 30000);
    return () => clearInterval(t);
  }, [active]);
}

const minutes = (a: Row) =>
  a.workedMinutes +
  (a.runningSinceUtc
    ? Math.ceil((Date.now() - new Date(a.runningSinceUtc).getTime()) / 60000)
    : 0);
const hm = (m: number) =>
  `${Math.floor(m / 60)} h ${String(m % 60).padStart(2, "0")} min`;

export default function Tech() {
  const { has, admin } = useSession();
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  const [tab, setTab] = useState("Mis órdenes");
  const [form, setForm] = useState<EditSpec | null>(null);
  const load = useCallback(
    () =>
      request("/api/saas/tech")
        .then(setD)
        .catch((e) => setError(e.message)),
    [],
  );
  useEffect(() => {
    if (!has("technician")) return;
    load();
    const t = setInterval(load, 30000);
    return () => clearInterval(t);
  }, [load]);
  useTick(!!d?.assignments.some((a: any) => a.state === "Working"));
  if (!has("technician")) return <UpgradeNotice module="App del técnico" />;
  if (!d) return error ? <ErrorBox message={error} /> : <Loading />;
  const name = (id: string) =>
    d.technicians.find((t: any) => t.id === id)?.displayName || "—";
  const mine = d.orders.filter(
    (x: any) => x.assignment && (x.assignment.technicianId === d.me || admin),
  );
  const unassigned = d.orders.filter((x: any) => !x.assignment);
  const timer = async (o: Row, a: Row, action: string, report?: string) => {
    try {
      await request(`/api/saas/tech/orders/${o.id}/timer`, "POST", {
        version: a.version,
        action,
        report,
      });
      toast.success(
        action === "start"
          ? "Trabajo iniciado"
          : action === "pause"
            ? "Trabajo en pausa"
            : "Trabajo terminado",
      );
      load();
    } catch (e) {
      toast.error((e as Error).message);
    }
  };
  const assign = (o: Row, self: boolean) => {
    const save = async (b: Row) => {
      await request(`/api/saas/tech/orders/${o.id}/assign`, "POST", {
        technicianId: b.technicianId || d.me,
        mode: b.mode || "Workshop",
        fieldAddress: b.fieldAddress || "",
        scheduledAtUtc: b.scheduledAt
          ? new Date(b.scheduledAt).toISOString()
          : null,
      });
      toast.success("Orden asignada");
      await load();
    };
    if (self && !admin) return save({}).catch((e) => toast.error(e.message));
    setForm({
      title: `Asignar ${code(o.number)}`,
      subtitle: `${o.deviceLabel} · ${o.customerName}`,
      fields: [
        {
          name: "technicianId",
          label: "Técnico",
          type: "select",
          options: d.technicians.map((t: any) => ({
            value: t.id,
            label: t.displayName,
          })),
        },
        {
          name: "mode",
          label: "Dónde",
          type: "select",
          options: [
            { value: "Workshop", label: "En el taller" },
            { value: "Field", label: "A domicilio" },
          ],
        },
        {
          name: "fieldAddress",
          label: "Dirección (a domicilio)",
          required: false,
        },
        {
          name: "scheduledAt",
          label: "Programada para",
          type: "datetime-local",
          required: false,
        },
      ],
      initial: { technicianId: d.me, mode: "Workshop" },
      save,
    });
  };
  const running = d.assignments.find(
    (a: any) => a.state === "Working" && a.technicianId === d.me,
  );
  const runningOrder =
    running && d.orders.find((x: any) => x.o.id === running.orderId)?.o;
  return (
    <div className="tech-app">
      <div className="page-heading">
        <div>
          <span className="eyebrow">APP DEL TÉCNICO</span>
          <h1>Mi trabajo</h1>
          <p>
            Tus órdenes, tiempos y visitas. Instalala en el celular desde el
            menú del navegador.
          </p>
        </div>
      </div>
      {runningOrder && (
        <section className="panel padded running-card">
          <span className="live-dot" />
          <div>
            <small>Trabajando en {code(runningOrder.number)}</small>
            <strong>{runningOrder.deviceLabel}</strong>
            <span>{hm(minutes(running))}</span>
          </div>
          <button
            className="button secondary small"
            onClick={() => timer(runningOrder, running, "pause")}
          >
            <Pause size={15} />
            Pausar
          </button>
        </section>
      )}
      <div className="tabs">
        {["Mis órdenes", "Sin asignar", "Visitas"].map((t) => (
          <button
            key={t}
            className={tab === t ? "active" : ""}
            onClick={() => setTab(t)}
          >
            {t}
            <span>
              {t === "Mis órdenes"
                ? mine.length
                : t === "Sin asignar"
                  ? unassigned.length
                  : d.field.length}
            </span>
          </button>
        ))}
      </div>
      {tab === "Mis órdenes" && (
        <div className="tech-list">
          {mine.length === 0 && (
            <div className="premium-empty">
              No tenés órdenes asignadas. Tomá una de “Sin asignar”.
            </div>
          )}
          {mine.map(({ o, assignment: a }: any) => (
            <article key={o.id} className="panel tech-card">
              <header>
                <Link to={`/orders/${o.id}`}>
                  <small>
                    {code(o.number)} · Prioridad {o.priority.toLowerCase()}
                  </small>
                  <strong>{o.deviceLabel}</strong>
                </Link>
                <Status value={o.status} />
              </header>
              <p>{o.issueDescription}</p>
              <div className="tech-meta">
                <span>
                  {stateNames[a.state]} · {hm(minutes(a))}
                </span>
                {admin && <span>{name(a.technicianId)}</span>}
                {a.mode === "Field" && (
                  <a
                    href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(a.fieldAddress)}`}
                    target="_blank"
                    rel="noreferrer"
                  >
                    <MapPin size={13} />
                    {a.fieldAddress}
                  </a>
                )}
                {a.scheduledAtUtc && (
                  <span>Programada {fullDate(a.scheduledAtUtc)}</span>
                )}
                <a href={`tel:${o.customerPhone}`}>
                  <Phone size={13} />
                  {o.customerName}
                </a>
              </div>
              <div className="tech-actions">
                {a.state !== "Working" ? (
                  <button
                    className="button primary small"
                    onClick={() => timer(o, a, "start")}
                  >
                    <Play size={15} />
                    {a.state === "Paused" ? "Retomar" : "Empezar"}
                  </button>
                ) : (
                  <button
                    className="button secondary small"
                    onClick={() => timer(o, a, "pause")}
                  >
                    <Pause size={15} />
                    Pausar
                  </button>
                )}
                <button
                  className="button secondary small"
                  onClick={() =>
                    setForm({
                      title: `Terminar ${code(o.number)}`,
                      subtitle: `Tiempo registrado: ${hm(minutes(a))}`,
                      fields: [
                        {
                          name: "report",
                          label: "Informe técnico",
                          type: "textarea",
                          required: false,
                        },
                      ],
                      submit: "Terminar trabajo",
                      save: async (b) => timer(o, a, "finish", b.report),
                    })
                  }
                >
                  <CheckCircle2 size={15} />
                  Terminar
                </button>
                {admin && (
                  <button
                    className="button subtle small"
                    onClick={() => assign(o, false)}
                  >
                    Reasignar
                  </button>
                )}
              </div>
            </article>
          ))}
        </div>
      )}
      {tab === "Sin asignar" && (
        <div className="tech-list">
          {unassigned.length === 0 && (
            <div className="premium-empty">
              Todas las órdenes abiertas tienen técnico.
            </div>
          )}
          {unassigned.map(({ o }: any) => (
            <article key={o.id} className="panel tech-card">
              <header>
                <Link to={`/orders/${o.id}`}>
                  <small>{code(o.number)}</small>
                  <strong>{o.deviceLabel}</strong>
                </Link>
                <Status value={o.status} />
              </header>
              <p>{o.issueDescription}</p>
              <div className="tech-actions">
                <button
                  className="button primary small"
                  onClick={() => assign(o, true)}
                >
                  <UserPlus size={15} />
                  {admin ? "Asignar" : "Tomar orden"}
                </button>
              </div>
            </article>
          ))}
        </div>
      )}
      {tab === "Visitas" && (
        <div className="tech-list">
          {d.field.length === 0 && (
            <div className="premium-empty">
              No hay visitas a domicilio programadas.
            </div>
          )}
          {d.field.map((v: any) => (
            <article key={v.id} className="panel tech-card">
              <header>
                <div>
                  <small>
                    {new Date(v.startsAtUtc).toLocaleDateString("es-AR", {
                      weekday: "long",
                      day: "numeric",
                      month: "short",
                    })}{" "}
                    · {time(v.startsAtUtc)}
                  </small>
                  <strong>{v.customerName}</strong>
                </div>
                <Home size={18} />
              </header>
              <p>{v.reason}</p>
              <div className="tech-meta">
                <a
                  href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(v.address)}`}
                  target="_blank"
                  rel="noreferrer"
                >
                  <MapPin size={13} />
                  {v.address}
                </a>
                <a href={`tel:${v.phone}`}>
                  <Phone size={13} />
                  {v.phone}
                </a>
                {admin && v.technicianId && <span>{name(v.technicianId)}</span>}
              </div>
            </article>
          ))}
        </div>
      )}
      {form && <Editor spec={form} onClose={() => setForm(null)} />}
    </div>
  );
}
