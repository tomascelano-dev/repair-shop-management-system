import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Bell, Download } from "lucide-react";
import { toast } from "sonner";
import { request, money, fullDate, date } from "../api";
import { ErrorBox, Loading } from "../ui";
import { Section, Table, Metrics } from "../premium/shared";
import { useSession, download } from "./session";

const voucherNames: Record<number, string> = {
  1: "Factura A",
  6: "Factura B",
  11: "Factura C",
  3: "Nota de crédito A",
  8: "Nota de crédito B",
  13: "Nota de crédito C",
};
const dayInput = (d: Date) => d.toISOString().slice(0, 10);

export default function Reports() {
  const { has } = useSession();
  const [from, setFrom] = useState(() =>
    dayInput(new Date(Date.now() - 30 * 86400000)),
  );
  const [to, setTo] = useState(() => dayInput(new Date()));
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  const range = `from=${from}T00:00:00Z&to=${to}T23:59:59Z`;
  useEffect(() => {
    setD(null);
    request(`/api/saas/reports?${range}`)
      .then(setD)
      .catch((e) => setError(e.message));
  }, [range]);
  const exp = (kind: string) =>
    download(
      `/api/saas/reports/export?kind=${kind}&${range}`,
      `${kind}.csv`,
    ).catch((e) => toast.error(e.message));
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>Reportes</h1>
          <p>
            Órdenes, cobros, ventas, facturación, satisfacción y productividad.
          </p>
        </div>
        <div className="heading-actions">
          <input
            type="date"
            className="inline-search"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            aria-label="Desde"
          />
          <input
            type="date"
            className="inline-search"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            aria-label="Hasta"
          />
        </div>
      </div>
      <ErrorBox message={error} />
      {!d ? (
        !error && <Loading />
      ) : (
        <div className="settings-stack">
          <Metrics
            items={[
              {
                label: "Órdenes recibidas",
                value: d.ordersCreated,
                hint: `${d.delivered} entregadas`,
              },
              {
                label: "Cobrado en órdenes",
                value:
                  d.payments
                    .map((p: any) => money(p.total, p.currency))
                    .join(" · ") || money(0),
                hint: "Pagos registrados",
              },
              {
                label: "Ventas de mostrador",
                value:
                  d.sales
                    .map((s: any) => money(s.total, s.currency))
                    .join(" · ") || money(0),
                hint: `${d.sales.reduce((s: number, x: any) => s + x.count, 0)} ventas`,
              },
              {
                label: "NPS",
                value: d.satisfaction.responses ? d.satisfaction.nps : "—",
                hint: d.satisfaction.responses
                  ? `${d.satisfaction.responses} respuestas · promedio ${d.satisfaction.average}/10`
                  : "Sin encuestas respondidas",
              },
            ]}
          />
          <div className="two-cols">
            <Section
              title="Productividad por técnico"
              text="Órdenes terminadas en la app del técnico."
            >
              <Table
                headers={["Técnico", "Órdenes", "Horas", "Promedio"]}
                rows={d.technicians.map((t: any) => [
                  t.displayName || "—",
                  t.orders,
                  (t.minutes / 60).toFixed(1),
                  `${Math.round(t.minutes / Math.max(1, t.orders))} min`,
                ])}
                empty={
                  has("technician")
                    ? "Todavía no hay trabajos cronometrados."
                    : "Disponible con la app del técnico (plan Estándar)."
                }
              />
            </Section>
            <Section
              title="Facturación"
              text="Comprobantes emitidos, sin rechazados."
            >
              <Table
                headers={["Comprobante", "Cantidad", "Total"]}
                rows={d.invoices.map((i: any) => [
                  voucherNames[i.voucherType] || `Tipo ${i.voucherType}`,
                  i.count,
                  money(i.total, i.currency),
                ])}
                empty="Sin comprobantes en el período."
              />
            </Section>
          </div>
          <Section title="Lo que dicen tus clientes">
            <Table
              headers={["Fecha", "Puntaje", "Comentario", ""]}
              rows={d.satisfaction.comments.map((c: any) => [
                date(c.answeredAtUtc),
                `${c.score}/10`,
                c.comment,
                <Link className="text-link" to={`/orders/${c.orderId}`}>
                  Ver orden
                </Link>,
              ])}
              empty={
                has("surveys")
                  ? "Todavía no hay comentarios."
                  : "Disponible con encuestas (plan Estándar)."
              }
            />
          </Section>
          <Section
            title="Exportar"
            text="Archivos CSV compatibles con Excel y Google Sheets."
          >
            <div className="row-actions padded-grid">
              <button
                className="button secondary small"
                onClick={() => exp("orders")}
              >
                <Download size={15} />
                Órdenes
              </button>
              <button
                className="button secondary small"
                onClick={() => exp("cash")}
              >
                <Download size={15} />
                Movimientos de caja
              </button>
              <button
                className="button secondary small"
                onClick={() => exp("invoices")}
              >
                <Download size={15} />
                Facturas
              </button>
            </div>
          </Section>
        </div>
      )}
    </>
  );
}

export function AlertsBell() {
  const { me, reload } = useSession();
  const nav = useNavigate();
  const [open, setOpen] = useState(false);
  const [rows, setRows] = useState<any[] | null>(null);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    request("/api/saas/alerts")
      .then(setRows)
      .catch(() => setRows([]));
    const close = (e: MouseEvent) =>
      !ref.current?.contains(e.target as Node) && setOpen(false);
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open]);
  const unread = me?.unreadAlerts || 0;
  return (
    <div className="alerts" ref={ref}>
      <button
        className="icon-button"
        aria-label={`Alertas${unread ? `, ${unread} sin leer` : ""}`}
        onClick={() => setOpen(!open)}
      >
        <Bell size={19} />
        {unread > 0 && (
          <span className="alerts-badge">{unread > 9 ? "9+" : unread}</span>
        )}
      </button>
      {open && (
        <div className="alerts-panel">
          <header>
            <strong>Alertas</strong>
            {unread > 0 && (
              <button
                className="text-link"
                onClick={async () => {
                  await request("/api/saas/alerts/read", "POST", {});
                  await reload();
                  setRows(
                    (r) =>
                      r?.map((x) => ({
                        ...x,
                        readAtUtc: x.readAtUtc || new Date().toISOString(),
                      })) || null,
                  );
                }}
              >
                Marcar todas como leídas
              </button>
            )}
          </header>
          {!rows ? (
            <Loading />
          ) : rows.length === 0 ? (
            <p className="alerts-empty">No hay alertas.</p>
          ) : (
            <ul>
              {rows.map((a) => (
                <li key={a.id} className={a.readAtUtc ? "" : "unread"}>
                  <button
                    onClick={() => {
                      setOpen(false);
                      if (a.link) nav(a.link);
                    }}
                  >
                    <span>{a.message}</span>
                    <small>{fullDate(a.createdAtUtc)}</small>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
