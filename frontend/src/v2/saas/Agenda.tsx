import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  CalendarPlus,
  ChevronLeft,
  ChevronRight,
  Home,
  Repeat,
  Wrench,
} from "lucide-react";
import { toast } from "sonner";
import { request } from "../api";
import { ErrorBox, Loading } from "../ui";
import { Metrics, Editor, type EditSpec, type Row } from "../premium/shared";
import { toLocalInput, fromLocalInput, time, useSession } from "./session";
import { UpgradeNotice } from "./Settings";

const statusNames: Record<string, string> = {
  Booked: "Reservado",
  Confirmed: "Confirmado",
  Done: "Atendido",
  NoShow: "No vino",
  Cancelled: "Cancelado",
  Converted: "Orden creada",
};

const startOfWeek = (d: Date) => {
  const x = new Date(d);
  x.setHours(0, 0, 0, 0);
  x.setDate(x.getDate() - ((x.getDay() + 6) % 7));
  return x;
};

export default function Agenda() {
  const { has, me } = useSession();
  const nav = useNavigate();
  const [week, setWeek] = useState(() => startOfWeek(new Date()));
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  const [form, setForm] = useState<EditSpec | null>(null);
  const [customers, setCustomers] = useState<any[]>([]);
  const end = new Date(week);
  end.setDate(end.getDate() + 7);
  const load = useCallback(
    () =>
      request(
        `/api/saas/appointments?from=${week.toISOString()}&to=${end.toISOString()}`,
      )
        .then(setD)
        .catch((e) => setError(e.message)),
    [week.getTime()],
  );
  useEffect(() => {
    if (has("agenda")) load();
  }, [load]);
  useEffect(() => {
    request("/api/v1/customers?take=200")
      .then((r) => setCustomers(Array.isArray(r) ? r : r.items || []))
      .catch(() => {});
  }, []);
  if (!has("agenda")) return <UpgradeNotice module="Agenda y turnos" />;
  if (!d) return error ? <ErrorBox message={error} /> : <Loading />;
  const techOptions = [
    ...d.technicians.map((t: any) => ({ value: t.id, label: t.displayName })),
  ];
  const tech = (id?: string) =>
    d.technicians.find((t: any) => t.id === id)?.displayName;
  const edit = (a?: Row, start?: Date) =>
    setForm({
      title: a ? "Editar turno" : "Nuevo turno",
      fields: [
        {
          name: "customerId",
          label: "Cliente existente",
          type: "select",
          required: false,
          hint: "Dejalo vacío para un cliente nuevo.",
          options: [
            ...customers.map((c) => ({
              value: c.id,
              label: `${c.fullName} · ${c.phone}`,
            })),
          ],
        },
        { name: "customerName", label: "Nombre", required: false },
        { name: "phone", label: "Teléfono", required: false },
        { name: "email", label: "Email", type: "email", required: false },
        { name: "deviceLabel", label: "Equipo", required: false },
        {
          name: "startsAt",
          label: "Fecha y hora",
          type: "datetime-local",
          required: true,
        },
        {
          name: "durationMinutes",
          label: "Duración (min)",
          type: "number",
          min: 10,
          max: 600,
          step: "5",
        },
        {
          name: "technicianId",
          label: "Técnico",
          type: "select",
          required: false,
          options: techOptions,
        },
        {
          name: "kind",
          label: "Tipo",
          type: "select",
          options: [
            { value: "Workshop", label: "En el taller" },
            { value: "Field", label: "A domicilio" },
          ],
        },
        { name: "address", label: "Dirección (a domicilio)", required: false },
        {
          name: "recurrenceMonths",
          label: "Mantenimiento preventivo: repetir cada (meses)",
          type: "number",
          min: 0,
          max: 24,
          step: "1",
          hint: "0 = no se repite. Al marcarlo atendido se agenda el próximo.",
        },
        { name: "reason", label: "Motivo", type: "textarea", required: true },
      ],
      initial: a
        ? {
            ...a,
            customerId: a.customerId || "",
            technicianId: a.technicianId || "",
            startsAt: toLocalInput(a.startsAtUtc),
          }
        : {
            kind: "Workshop",
            durationMinutes: me?.profile.openingHours?.slotMinutes || 30,
            recurrenceMonths: 0,
            startsAt: toLocalInput(start?.toISOString()),
            customerId: "",
            technicianId: "",
          },
      save: async ({ startsAt, ...b }) => {
        const c = customers.find((x) => x.id === b.customerId);
        const body = {
          ...b,
          version: a?.version || 0,
          customerId: b.customerId || null,
          technicianId: b.technicianId || null,
          customerName: b.customerName || c?.fullName,
          phone: b.phone || c?.phone,
          startsAtUtc: fromLocalInput(startsAt),
        };
        await request(
          a ? `/api/saas/appointments/${a.id}` : "/api/saas/appointments",
          a ? "PUT" : "POST",
          body,
        );
        toast.success(a ? "Turno actualizado" : "Turno agendado");
        await load();
      },
    });
  const setStatus = async (a: Row, status: string) => {
    try {
      const r = await request(`/api/saas/appointments/${a.id}/status`, "POST", {
        version: a.version,
        status,
      });
      toast.success(
        r.next
          ? "Turno cerrado. Se agendó el próximo mantenimiento."
          : `Turno: ${statusNames[status].toLowerCase()}`,
      );
      load();
    } catch (e) {
      toast.error((e as Error).message);
    }
  };
  const convert = (a: Row) =>
    setForm({
      title: "Recibir equipo",
      subtitle: `Crea la orden de trabajo de ${a.customerName}.`,
      fields: [
        { name: "brand", label: "Marca", required: true },
        { name: "model", label: "Modelo", required: true },
        { name: "identifier", label: "IMEI / serie", required: false },
        { name: "issue", label: "Falla", type: "textarea", required: true },
        { name: "condition", label: "Estado al ingresar", required: false },
        { name: "accessories", label: "Accesorios", required: false },
      ],
      initial: { issue: a.reason, model: a.deviceLabel },
      submit: "Crear orden",
      save: async (b) => {
        const r = await request(
          `/api/saas/appointments/${a.id}/convert`,
          "POST",
          { ...b, version: a.version },
        );
        toast.success("Orden creada");
        nav(`/orders/${r.orderId}`);
      },
    });
  const days = Array.from({ length: 7 }, (_, i) => {
    const x = new Date(week);
    x.setDate(x.getDate() + i);
    return x;
  });
  const today = new Date().toDateString();
  const move = (n: number) => {
    const x = new Date(week);
    x.setDate(x.getDate() + n * 7);
    setD(null);
    setWeek(x);
  };
  const active = d.appointments.filter(
    (a: any) => !["Cancelled"].includes(a.status),
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">AGENDA</span>
          <h1>Turnos y visitas</h1>
          <p>
            Turnos del taller, servicios a domicilio y mantenimientos
            preventivos.
          </p>
        </div>
        <div className="heading-actions">
          <button className="button primary small" onClick={() => edit()}>
            <CalendarPlus size={16} />
            Nuevo turno
          </button>
        </div>
      </div>
      <Metrics
        items={[
          {
            label: "Turnos esta semana",
            value: active.length,
            hint: `${active.filter((a: any) => a.source === "Online").length} reservados online`,
          },
          {
            label: "A domicilio",
            value: active.filter((a: any) => a.kind === "Field").length,
            hint: "Visitas de técnicos",
          },
          {
            label: "Preventivos próximos",
            value: d.preventiveDueSoon,
            hint: "En los próximos 30 días",
          },
          {
            label: "Convertidos en orden",
            value: d.appointments.filter((a: any) => a.status === "Converted")
              .length,
            hint: "Esta semana",
          },
        ]}
      />
      <div className="week-nav">
        <button
          className="icon-button"
          onClick={() => move(-1)}
          aria-label="Semana anterior"
        >
          <ChevronLeft />
        </button>
        <strong>
          {days[0].toLocaleDateString("es-AR", {
            day: "numeric",
            month: "short",
          })}{" "}
          al{" "}
          {days[6].toLocaleDateString("es-AR", {
            day: "numeric",
            month: "short",
          })}
        </strong>
        <button
          className="icon-button"
          onClick={() => move(1)}
          aria-label="Semana siguiente"
        >
          <ChevronRight />
        </button>
        <button
          className="button subtle small"
          onClick={() => {
            setD(null);
            setWeek(startOfWeek(new Date()));
          }}
        >
          Hoy
        </button>
      </div>
      <div className="week-grid">
        {days.map((day) => {
          const list = d.appointments.filter(
            (a: any) =>
              new Date(a.startsAtUtc).toDateString() === day.toDateString(),
          );
          return (
            <section
              key={day.toISOString()}
              className={`panel day-col ${day.toDateString() === today ? "today" : ""}`}
            >
              <header>
                <small>
                  {day.toLocaleDateString("es-AR", { weekday: "short" })}
                </small>
                <strong>{day.getDate()}</strong>
                <button
                  className="icon-button"
                  aria-label="Agregar turno este día"
                  onClick={() => {
                    const s = new Date(day);
                    const [h, m] = (
                      me?.profile.openingHours?.from || "09:00"
                    ).split(":");
                    s.setHours(Number(h), Number(m));
                    edit(undefined, s);
                  }}
                >
                  +
                </button>
              </header>
              {list.map((a: any) => (
                <article key={a.id} className={`appt a-${a.status}`}>
                  <button
                    className="appt-main"
                    onClick={() =>
                      !["Converted", "Done"].includes(a.status) && edit(a)
                    }
                  >
                    <b>{time(a.startsAtUtc)}</b>
                    <span>{a.customerName}</span>
                    <small>
                      {a.kind === "Field" ? (
                        <Home size={12} />
                      ) : (
                        <Wrench size={12} />
                      )}
                      {a.deviceLabel || a.reason}
                      {a.recurrenceMonths > 0 && <Repeat size={12} />}
                    </small>
                    {tech(a.technicianId) && (
                      <small>{tech(a.technicianId)}</small>
                    )}
                    <em>{statusNames[a.status]}</em>
                  </button>
                  {["Booked", "Confirmed"].includes(a.status) && (
                    <div className="appt-actions">
                      {a.status === "Booked" && (
                        <button onClick={() => setStatus(a, "Confirmed")}>
                          Confirmar
                        </button>
                      )}
                      {a.kind === "Workshop" && (
                        <button onClick={() => convert(a)}>Recibir</button>
                      )}
                      <button onClick={() => setStatus(a, "Done")}>
                        Atendido
                      </button>
                      <button onClick={() => setStatus(a, "NoShow")}>
                        No vino
                      </button>
                      <button
                        onClick={() =>
                          confirm("¿Cancelar el turno?") &&
                          setStatus(a, "Cancelled")
                        }
                      >
                        Cancelar
                      </button>
                    </div>
                  )}
                </article>
              ))}
            </section>
          );
        })}
      </div>
      {form && <Editor spec={form} onClose={() => setForm(null)} />}
    </>
  );
}
