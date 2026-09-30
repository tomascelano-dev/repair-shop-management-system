import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  Check,
  Copy,
  ExternalLink,
  ImagePlus,
  Link2,
  Plus,
  Printer,
  Save,
  Trash2,
  Wallet,
  X,
} from "lucide-react";
import { toast } from "sonner";
import {
  request,
  code,
  money,
  fullDate,
  date,
  statusCodes,
  statusNames,
  quoteNames,
  checkNames,
  TOKEN_KEY,
  USER_KEY,
} from "./api";
import { Status, Field, ErrorBox, Loading, Modal, Empty } from "./ui";

const checkLabels: Record<string, string> = {
  ok: "Funciona",
  fail: "Presenta falla",
  untested: "Sin probar",
  na: "No aplica",
};
export default function OrderDetail({ onChanged }: { onChanged: () => void }) {
  const { id } = useParams();
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [tab, setTab] = useState("Resumen");
  const [dialog, setDialog] = useState("");
  const [link, setLink] = useState("");
  const [photos, setPhotos] = useState<{ id: string; url: string }[]>([]);
  const load = useCallback(async () => {
    try {
      setD(await request(`/api/v2/orders/${id}`));
      setError("");
    } catch (e) {
      setError((e as Error).message);
    }
  }, [id]);
  useEffect(() => {
    setD(null);
    setLink("");
    load();
  }, [load]);
  useEffect(() => {
    if (busy || dialog || !["Resumen", "Cobros", "Historial"].includes(tab))
      return;
    const timer = setInterval(load, 15000);
    window.addEventListener("focus", load);
    return () => {
      clearInterval(timer);
      window.removeEventListener("focus", load);
    };
  }, [load, tab, busy, dialog]);
  useEffect(() => {
    if (!d) return;
    let active = true;
    const urls: string[] = [];
    Promise.all(
      d.photos.map(async (p: any) => {
        const r = await fetch(`/api/v2/orders/${id}/photos/${p.id}`, {
          headers: {
            Authorization: `Bearer ${localStorage.getItem(TOKEN_KEY)}`,
          },
        });
        if (!r.ok) throw Error("No se pudo cargar una foto");
        const url = URL.createObjectURL(await r.blob());
        urls.push(url);
        return { id: p.id, url };
      }),
    )
      .then((p) => {
        if (active) setPhotos(p);
        else urls.forEach(URL.revokeObjectURL);
      })
      .catch(() => toast.error("No se pudieron cargar las fotos"));
    return () => {
      active = false;
      urls.forEach(URL.revokeObjectURL);
    };
  }, [d?.photos.map((p: any) => p.id).join(","), id]);
  async function action(path: string, body: any, method = "POST") {
    setBusy(true);
    setError("");
    try {
      const r = await request(`/api/v2/orders/${id}/${path}`, method, {
        version: d.workflow.version,
        ...body,
      });
      await load();
      onChanged();
      toast.success("Cambios guardados");
      return r;
    } catch (e) {
      const message = (e as Error).message;
      setError(message);
      toast.error(message);
      throw e;
    } finally {
      setBusy(false);
    }
  }
  async function stage(status: string) {
    try {
      await action("stage", { status });
    } catch {}
  }
  if (!d)
    return (
      <>
        <ErrorBox message={error} />
        <Loading />
      </>
    );
  const w = d.workflow,
    o = d.order,
    q = d.quotes[0],
    status = typeof o.status === "number" ? statusCodes[o.status] : o.status,
    closed = ["Delivered", "Cancelled"].includes(status),
    admin = JSON.parse(localStorage.getItem(USER_KEY) || "{}").role === "Admin";
  const paid =
      d.payments.reduce((s: number, p: any) => s + p.amount, 0) -
      d.refunds.reduce((s: number, p: any) => s + p.amount, 0),
    due = q ? q.total - paid : 0,
    currency = q?.currency || "ARS";
  const next: Record<string, { status: string; label: string }[]> = {
    Received: [{ status: "Diagnosing", label: "Iniciar diagnóstico" }],
    AwaitingApproval:
      q?.status === "Accepted"
        ? [
            { status: "InProgress", label: "Iniciar reparación" },
            { status: "WaitingParts", label: "Esperar repuesto" },
          ]
        : [],
    WaitingParts: [{ status: "InProgress", label: "Reanudar reparación" }],
    InProgress: [
      { status: "QualityCheck", label: "Pasar a control de calidad" },
      { status: "WaitingParts", label: "Esperar repuesto" },
    ],
    QualityCheck: [
      { status: "Ready", label: "Marcar listo para retirar" },
      { status: "InProgress", label: "Volver a reparación" },
    ],
    Ready: [{ status: "InProgress", label: "Volver a reparación" }],
  };
  return (
    <>
      <Link to="/orders" className="back-link">
        <ArrowLeft size={15} />
        Todas las órdenes
      </Link>
      <div className="page-heading order-heading">
        <div>
          <span className="eyebrow">
            {code(w.number)} {w.isDemo && "· DEMO"}
          </span>
          <h1>{w.deviceLabel}</h1>
          <p>
            {w.customerName} <span className="separator">/</span> Ingresó el{" "}
            {date(o.createdAtUtc)}
          </p>
        </div>
        <div className="heading-actions">
          <Status value={status} />
          <button
            className="button secondary small"
            onClick={() => window.print()}
          >
            <Printer size={16} />
            Comprobante
          </button>
        </div>
      </div>
      <ErrorBox message={error} />
      <div className="premium-context-bar" style={{ marginBottom: 18 }}>
        <Link to={`/premium/stock?order=${id}`}>Repuestos y reservas</Link>
        <Link to={`/premium/profit?order=${id}`}>Costos y rentabilidad</Link>
        <Link to={`/premium/warranties?order=${id}`}>Garantías</Link>
        <Link to={`/premium/business?order=${id}`}>Sucursal y empresa</Link>
      </div>
      <div className="order-summary-strip">
        <div>
          <small>Cliente</small>
          <strong>{w.customerName}</strong>
          <span>{w.customerPhone}</span>
        </div>
        <div>
          <small>Presupuesto vigente</small>
          <strong>{q ? money(q.total, currency) : "Por definir"}</strong>
          <span>
            {q
              ? `Versión ${q.revision} · ${quoteNames[q.status] || q.status}`
              : "Completá el diagnóstico"}
          </span>
        </div>
        <div>
          <small>Cobrado neto</small>
          <strong>{money(paid, currency)}</strong>
          <span>{d.payments.length} cobros registrados</span>
        </div>
        <div>
          <small>Saldo presupuestado</small>
          <strong className={due > 0 ? "amber-text" : "green-text"}>
            {money(due, currency)}
          </strong>
          <span>
            {q?.status === "Accepted"
              ? "Presupuesto aprobado"
              : "Sujeto a aprobación"}
          </span>
        </div>
      </div>
      <div className="action-bar">
        <span>
          <span className="live-dot" />
          {w.handedOverAtUtc
            ? `Retirado por ${w.deliveredTo}`
            : closed
              ? "Orden cancelada"
              : q?.status === "Accepted"
                ? "Presupuesto aprobado por el cliente"
                : "Seguimiento de la reparación"}
        </span>
        <div>
          {(next[status] || []).map((n, i) => (
            <button
              disabled={busy}
              key={n.status}
              className={`button small ${i === 0 ? "primary" : "secondary"}`}
              onClick={() => stage(n.status)}
            >
              {n.label}
              <ArrowRight size={15} />
            </button>
          ))}
          {["Ready", "Cancelled"].includes(status) && !w.handedOverAtUtc && (
            <button
              className="button primary small"
              onClick={() => setDialog("handover")}
            >
              Registrar entrega
              <Check size={16} />
            </button>
          )}
          {!closed && (
            <button
              className="button subtle small"
              onClick={() => setDialog("cancel")}
            >
              Cancelar orden
            </button>
          )}
        </div>
      </div>
      <div className="tabs">
        {["Resumen", "Diagnóstico", "Presupuesto", "Cobros", "Historial"].map(
          (t) => (
            <button
              key={t}
              className={tab === t ? "active" : ""}
              onClick={() => setTab(t)}
            >
              {t}
              {t === "Cobros" && <span>{d.payments.length}</span>}
            </button>
          ),
        )}
      </div>
      {tab === "Resumen" && (
        <div className="detail-grid">
          <div className="stack">
            <section className="panel padded">
              <h3>El equipo al ingresar</h3>
              <dl className="detail-list">
                <div>
                  <dt>Falla informada</dt>
                  <dd>{o.issueDescription}</dd>
                </div>
                <div>
                  <dt>IMEI / serie</dt>
                  <dd>{w.identifier || "Sin registrar"}</dd>
                </div>
                <div>
                  <dt>Estado físico</dt>
                  <dd>{w.condition || "Sin observaciones"}</dd>
                </div>
                <div>
                  <dt>Accesorios</dt>
                  <dd>{w.accessories || "Sin accesorios"}</dd>
                </div>
                <div>
                  <dt>Prioridad</dt>
                  <dd>{w.priority}</dd>
                </div>
              </dl>
              <h4>Checklist de ingreso</h4>
              <div className="check-summary">
                {checkNames.map((k) => (
                  <div key={k}>
                    <span>{k}</span>
                    <b className={`check-${d.intakeChecks[k] || "untested"}`}>
                      {checkLabels[d.intakeChecks[k] || "untested"]}
                    </b>
                  </div>
                ))}
              </div>
            </section>
            <section className="panel padded">
              <div className="panel-heading compact">
                <div>
                  <h3>Fotos del equipo</h3>
                  <p>Hasta 8 fotos · JPG o PNG · 3 MB por foto</p>
                </div>
                {!closed && (
                  <label className="button secondary small upload-button">
                    <ImagePlus size={16} />
                    Agregar
                    <input
                      type="file"
                      accept="image/jpeg,image/png"
                      disabled={busy || photos.length >= 8}
                      onChange={async (e) => {
                        const f = e.target.files?.[0];
                        if (!f) return;
                        if (f.size > 3 * 1024 * 1024) {
                          toast.error("La foto supera los 3 MB");
                          return;
                        }
                        const reader = new FileReader();
                        reader.onload = async () => {
                          try {
                            await action("photos", { dataUrl: reader.result });
                          } catch {}
                        };
                        reader.readAsDataURL(f);
                        e.target.value = "";
                      }}
                    />
                  </label>
                )}
              </div>
              {photos.length ? (
                <div className="photo-grid">
                  {photos.map((p) => (
                    <a href={p.url} target="_blank" rel="noreferrer" key={p.id}>
                      <img src={p.url} alt="Estado del equipo" />
                    </a>
                  ))}
                </div>
              ) : (
                <div className="photo-empty">
                  <ImagePlus size={30} />
                  <p>Documentá el estado del equipo</p>
                  <small>Las fotos son internas y sólo las ve el taller.</small>
                </div>
              )}
            </section>
          </div>
          <div className="stack">
            <section className="panel padded portal-card">
              <span className="feature-icon">
                <Link2 size={22} />
              </span>
              <h3>El cliente también puede seguirlo</h3>
              <p>
                Un enlace para ver el estado y aprobar el presupuesto vigente.
              </p>
              <button
                className="button primary full"
                disabled={busy}
                onClick={async () => {
                  try {
                    const r = await action("portal", {});
                    setLink(`${window.location.origin}/portal#${r.token}`);
                  } catch {}
                }}
              >
                {w.portalTokenHash
                  ? "Renovar enlace del portal"
                  : "Crear enlace del portal"}
                <ExternalLink size={16} />
              </button>
              {link && (
                <div className="portal-link-box">
                  <p>Enlace creado para esta computadora.</p>
                  <a
                    className="button secondary full"
                    href={link}
                    target="_blank"
                    rel="noreferrer"
                  >
                    Abrir como cliente
                    <ExternalLink size={15} />
                  </a>
                  <button
                    className="text-link"
                    onClick={async () => {
                      try {
                        await navigator.clipboard.writeText(link);
                        toast.success("Enlace copiado");
                      } catch {
                        toast.error(
                          "No se pudo copiar. Abrí el portal y copiá su dirección.",
                        );
                      }
                    }}
                  >
                    <Copy size={15} />
                    Copiar enlace
                  </button>
                </div>
              )}
              {w.portalTokenHash && (
                <button
                  className="button subtle full"
                  disabled={busy}
                  onClick={async () => {
                    try {
                      await action("portal", {}, "DELETE");
                      setLink("");
                    } catch {}
                  }}
                >
                  Revocar acceso
                </button>
              )}
              <small>
                Al renovar, el enlace anterior deja de funcionar. Vigencia: 30
                días. En esta versión local se abre desde esta PC.
              </small>
            </section>
            <section className="panel padded">
              <h3>Próximo paso</h3>
              <p className="muted">
                {status === "Received"
                  ? "Iniciá el diagnóstico y registrá lo que encontrás."
                  : status === "Diagnosing"
                    ? "Prepará un presupuesto detallado para tu cliente."
                    : status === "AwaitingApproval" && q?.status !== "Accepted"
                      ? "Compartí el portal para registrar la decisión del cliente."
                      : status === "QualityCheck"
                        ? "Completá las pruebas finales en la pestaña Diagnóstico."
                        : status === "Ready"
                          ? "Verificá el saldo y registrá quién retira el equipo."
                          : closed
                            ? "La historia de esta reparación queda disponible para consultar."
                            : "Actualizá el diagnóstico y avanzá cuando el equipo esté listo."}
              </p>
              {["Diagnosing", "AwaitingApproval"].includes(status) && (
                <button
                  className="text-link"
                  onClick={() => setTab("Presupuesto")}
                >
                  Ir al presupuesto
                  <ArrowRight size={15} />
                </button>
              )}
            </section>
          </div>
        </div>
      )}
      {tab === "Diagnóstico" && (
        <Diagnosis
          key={w.version}
          w={w}
          checks={d.qualityChecks}
          closed={closed}
          busy={busy}
          onSave={(b) => action("diagnosis", b, "PUT")}
        />
      )}
      {tab === "Presupuesto" && (
        <Quotes
          d={d}
          busy={busy}
          canEdit={[
            "Diagnosing",
            "AwaitingApproval",
            "InProgress",
            "WaitingParts",
          ].includes(status)}
          onSave={(b) => action("quotes", b)}
        />
      )}
      {tab === "Cobros" && (
        <div className="detail-grid">
          <section className="panel padded">
            <div className="panel-heading compact">
              <div>
                <h3>Cobros y devoluciones</h3>
                <p>Importes registrados para esta orden</p>
              </div>
              {!closed && q?.status === "Accepted" && due > 0 && (
                <button
                  className="button primary small"
                  onClick={() => setDialog("payment")}
                >
                  <Plus size={16} />
                  Registrar cobro
                </button>
              )}
            </div>
            {!d.payments.length ? (
              <Empty
                title="Aún no hay cobros"
                text="Podés registrar una seña o el pago completo cuando el presupuesto esté aprobado."
              />
            ) : (
              <div className="ledger">
                {d.payments.map((p: any) => (
                  <div key={p.id}>
                    <span className="ledger-icon">
                      <Wallet size={19} />
                    </span>
                    <div>
                      <strong>
                        {["Efectivo", "Transferencia", "Tarjeta", "Otro"][
                          p.method
                        ] || p.method}
                      </strong>
                      <small>
                        {fullDate(p.createdAtUtc)} ·{" "}
                        {p.reference || "Sin referencia"}
                      </small>
                    </div>
                    <b>{money(p.amount, p.currency)}</b>
                    {admin && (
                      <button
                        className="text-link"
                        onClick={() => setDialog(`refund:${p.id}`)}
                      >
                        Devolver
                      </button>
                    )}
                  </div>
                ))}
                {d.refunds.map((r: any) => (
                  <div key={r.id}>
                    <span className="ledger-icon refund">
                      <ArrowLeft size={19} />
                    </span>
                    <div>
                      <strong>Devolución</strong>
                      <small>
                        {r.reason} · {fullDate(r.createdAtUtc)}
                      </small>
                    </div>
                    <b className="red-text">−{money(r.amount, currency)}</b>
                  </div>
                ))}
              </div>
            )}
          </section>
          <section className="panel padded payment-summary">
            <h3>Resumen de la cuenta</h3>
            <div>
              <span>Presupuesto</span>
              <b>{money(q?.total || 0, currency)}</b>
            </div>
            <div>
              <span>Cobrado neto</span>
              <b>{money(paid, currency)}</b>
            </div>
            <div className="total">
              <span>Saldo</span>
              <b>{money(due, currency)}</b>
            </div>
            <small>
              Moneda: {currency}. Los cobros son registros internos, no facturas
              fiscales.
            </small>
          </section>
        </div>
      )}
      {tab === "Historial" && (
        <section className="panel padded">
          <h3>Historia de esta reparación</h3>
          <div className="timeline">
            {[
              ...d.notes.map((n: any) => ({
                text: n.body,
                time: n.createdAtUtc,
              })),
              ...d.quotes
                .filter((q: any) => q.decidedAtUtc)
                .map((q: any) => ({
                  text: `Presupuesto v${q.revision}: ${quoteNames[q.status]} por ${q.decisionBy}`,
                  time: q.decidedAtUtc,
                })),
            ]
              .sort((a, b) => b.time.localeCompare(a.time))
              .map((n, i) => (
                <div key={i}>
                  <span />
                  <article>
                    <strong>{n.text}</strong>
                    <small>{fullDate(n.time)}</small>
                  </article>
                </div>
              ))}
          </div>
        </section>
      )}
      {dialog && (
        <OperationDialog
          kind={dialog}
          d={d}
          due={due}
          currency={currency}
          admin={admin}
          busy={busy}
          onClose={() => !busy && setDialog("")}
          onSave={async (path, b) => {
            await action(path, b);
            setDialog("");
          }}
        />
      )}
      <section className="print-sheet">
        <h1>RepairShop · Comprobante de orden</h1>
        <p>
          {code(w.number)} · {fullDate(o.createdAtUtc)}
        </p>
        <h2>{w.deviceLabel}</h2>
        <p>
          {w.customerName} · {w.customerPhone} · Serie:{" "}
          {w.identifier || "Sin registrar"}
        </p>
        <p>
          <b>Falla:</b> {o.issueDescription}
        </p>
        <p>
          <b>Condición:</b> {w.condition}. <b>Accesorios:</b> {w.accessories}
        </p>
        <p>
          <b>Estado:</b> {statusNames[status]}
        </p>
        {q && (
          <>
            <h3>
              Presupuesto v{q.revision} · {quoteNames[q.status]}
            </h3>
            {q.lines.map((l: any, i: number) => (
              <p key={i}>
                {l.quantity} × {l.description} —{" "}
                {money(l.quantity * l.unitPrice, currency)}
              </p>
            ))}
            <p>
              Total: {money(q.total, currency)} · Cobrado:{" "}
              {money(paid, currency)} · Saldo: {money(due, currency)}
            </p>
            <p>
              Garantía del trabajo: {q.warrantyDays} días desde la entrega.{" "}
              {q.terms}
            </p>
          </>
        )}
        {w.handedOverAtUtc && (
          <p>
            Retirado por {w.deliveredTo} el {fullDate(w.handedOverAtUtc)}
          </p>
        )}
        <p>Comprobante interno. No válido como factura fiscal.</p>
      </section>
    </>
  );
}

function Diagnosis({
  w,
  checks,
  closed,
  busy,
  onSave,
}: {
  w: any;
  checks: any;
  closed: boolean;
  busy: boolean;
  onSave: (b: any) => Promise<any>;
}) {
  const [diagnosis, setDiagnosis] = useState(w.diagnosis);
  const [laborCost, setCost] = useState(w.laborCost);
  const [quality, setQuality] = useState(checks);
  return (
    <form
      className="detail-grid"
      onSubmit={async (e) => {
        e.preventDefault();
        try {
          await onSave({
            diagnosis,
            laborCost: Number(laborCost),
            qualityChecks: quality,
          });
        } catch {}
      }}
    >
      <section className="panel padded">
        <h3>Diagnóstico técnico</h3>
        <p className="muted">Observaciones internas y trabajo a realizar.</p>
        <Field label="Hallazgos y reparación">
          <textarea
            className="large-textarea"
            maxLength={1200}
            value={diagnosis}
            disabled={closed}
            onChange={(e) => setDiagnosis(e.target.value)}
            placeholder="Describí las pruebas, la falla encontrada y el trabajo realizado…"
          />
        </Field>
        <Field
          label="Costo interno de mano de obra"
          hint="En la moneda del presupuesto. No se muestra al cliente."
        >
          <input
            type="number"
            min="0"
            max="100000000"
            step="0.01"
            value={laborCost}
            disabled={closed}
            onChange={(e) => setCost(e.target.value)}
          />
        </Field>
        {!closed && (
          <button className="button primary" disabled={busy}>
            <Save size={16} />
            Guardar diagnóstico y pruebas
          </button>
        )}
      </section>
      <section className="panel padded">
        <span className="eyebrow">ANTES DE ENTREGAR</span>
        <h3>Control de calidad</h3>
        <p className="muted">
          Todas las pruebas deben funcionar o no aplicar para marcar el equipo
          como listo.
        </p>
        <div className="checks">
          {checkNames.map((k) => (
            <label key={k}>
              <span>{k}</span>
              <select
                value={quality[k] || "untested"}
                disabled={closed}
                onChange={(e) =>
                  setQuality({ ...quality, [k]: e.target.value })
                }
              >
                {Object.entries(checkLabels).map(([v, l]) => (
                  <option key={v} value={v}>
                    {l}
                  </option>
                ))}
              </select>
            </label>
          ))}
        </div>
      </section>
    </form>
  );
}

function Quotes({
  d,
  busy,
  canEdit,
  onSave,
}: {
  d: any;
  busy: boolean;
  canEdit: boolean;
  onSave: (b: any) => Promise<any>;
}) {
  const q = d.quotes[0];
  const [edit, setEdit] = useState(!q);
  const [lines, setLines] = useState<any[]>(
    q?.lines || [{ description: "", quantity: 1, unitPrice: 0, unitCost: 0 }],
  );
  const [currency, setCurrency] = useState(q?.currency || "ARS");
  const [terms, setTerms] = useState(
    q?.terms ||
      "Incluye instalación y pruebas. Garantía sobre el trabajo realizado.",
  );
  const [validDays, setValid] = useState(7);
  const [warrantyDays, setWarranty] = useState(q?.warrantyDays ?? 90);
  const total = lines.reduce(
      (s, l) => s + Number(l.quantity) * Number(l.unitPrice),
      0,
    ),
    cost =
      lines.reduce((s, l) => s + Number(l.quantity) * Number(l.unitCost), 0) +
      d.workflow.laborCost;
  function change(i: number, k: string, v: any) {
    setLines(lines.map((l, n) => (n === i ? { ...l, [k]: v } : l)));
  }
  return (
    <div className="stack">
      {q && (
        <section className="panel padded">
          <div className="panel-heading compact">
            <div>
              <h3>
                Presupuesto v{q.revision}
                <span className={`quote-badge q-${q.status}`}>
                  {quoteNames[q.status]}
                </span>
              </h3>
              <p>
                Vigente hasta {date(q.expiresAtUtc)} · Garantía:{" "}
                {q.warrantyDays} días
              </p>
            </div>
            {canEdit && !edit && (
              <button
                className="button secondary small"
                onClick={() => setEdit(true)}
              >
                <Plus size={16} />
                Crear nueva versión
              </button>
            )}
          </div>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>DETALLE</th>
                  <th>CANT.</th>
                  <th>PRECIO UNIT.</th>
                  <th>SUBTOTAL</th>
                </tr>
              </thead>
              <tbody>
                {q.lines.map((l: any, i: number) => (
                  <tr key={i}>
                    <td>{l.description}</td>
                    <td>{l.quantity}</td>
                    <td>{money(l.unitPrice, q.currency)}</td>
                    <td>{money(l.quantity * l.unitPrice, q.currency)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="quote-total">
            <span>Total · {q.currency}</span>
            <strong>{money(q.total, q.currency)}</strong>
          </div>
          <p>{q.terms}</p>
          {q.decidedAtUtc && (
            <div className="info-box">
              {quoteNames[q.status]} por {q.decisionBy} ·{" "}
              {fullDate(q.decidedAtUtc)}
            </div>
          )}
        </section>
      )}
      {edit && canEdit && (
        <form
          className="panel padded"
          onSubmit={async (e) => {
            e.preventDefault();
            try {
              await onSave({
                lines: lines.map((l) => ({
                  ...l,
                  quantity: Number(l.quantity),
                  unitPrice: Number(l.unitPrice),
                  unitCost: Number(l.unitCost),
                })),
                currency,
                terms,
                validDays: Number(validDays),
                warrantyDays: Number(warrantyDays),
              });
              setEdit(false);
            } catch {}
          }}
        >
          <div className="panel-heading compact">
            <div>
              <h3>
                {q ? "Nueva versión del presupuesto" : "Preparar presupuesto"}
              </h3>
              <p>
                El precio publicado queda registrado. Cada cambio genera una
                nueva versión.
              </p>
            </div>
            {q && (
              <button
                type="button"
                className="icon-button"
                aria-label="Cerrar editor"
                onClick={() => setEdit(false)}
              >
                <X size={18} />
              </button>
            )}
          </div>
          <div className="quote-editor">
            {lines.map((l, i) => (
              <div className="quote-line" key={i}>
                <Field label="Trabajo o repuesto">
                  <input
                    required
                    maxLength={200}
                    value={l.description}
                    onChange={(e) => change(i, "description", e.target.value)}
                    placeholder="Ej. Reemplazo de pantalla"
                  />
                </Field>
                <Field label="Cantidad">
                  <input
                    required
                    type="number"
                    min="1"
                    max="1000"
                    value={l.quantity}
                    onChange={(e) => change(i, "quantity", e.target.value)}
                  />
                </Field>
                <Field label="Precio unitario">
                  <input
                    required
                    type="number"
                    min="0"
                    max="100000000"
                    step="0.01"
                    value={l.unitPrice}
                    onChange={(e) => change(i, "unitPrice", e.target.value)}
                  />
                </Field>
                <Field label="Costo interno">
                  <input
                    required
                    type="number"
                    min="0"
                    max="100000000"
                    step="0.01"
                    value={l.unitCost}
                    onChange={(e) => change(i, "unitCost", e.target.value)}
                  />
                </Field>
                <button
                  type="button"
                  className="icon-button"
                  disabled={lines.length === 1}
                  onClick={() => setLines(lines.filter((_, n) => n !== i))}
                  aria-label={`Quitar ítem ${i + 1}`}
                >
                  <Trash2 size={17} />
                </button>
              </div>
            ))}
          </div>
          <button
            type="button"
            className="text-link"
            disabled={lines.length >= 30}
            onClick={() =>
              setLines([
                ...lines,
                { description: "", quantity: 1, unitPrice: 0, unitCost: 0 },
              ])
            }
          >
            <Plus size={15} />
            Agregar ítem
          </button>
          <div className="form-grid three">
            <Field label="Moneda">
              <select
                value={currency}
                onChange={(e) => setCurrency(e.target.value)}
              >
                <option>ARS</option>
                <option>USD</option>
              </select>
            </Field>
            <Field label="Vigencia (días)">
              <input
                type="number"
                min="1"
                max="90"
                required
                value={validDays}
                onChange={(e) => setValid(Number(e.target.value))}
              />
            </Field>
            <Field label="Garantía (días)">
              <input
                type="number"
                min="0"
                max="730"
                required
                value={warrantyDays}
                onChange={(e) => setWarranty(Number(e.target.value))}
              />
            </Field>
          </div>
          <Field label="Condiciones que verá el cliente">
            <textarea
              maxLength={2000}
              value={terms}
              onChange={(e) => setTerms(e.target.value)}
            />
          </Field>
          <div className="quote-totals">
            <div>
              <small>Costo estimado + mano de obra</small>
              <b>{money(cost, currency)}</b>
            </div>
            <div>
              <small>Margen estimado</small>
              <b>{money(total - cost, currency)}</b>
            </div>
            <div>
              <small>Total al cliente</small>
              <strong>{money(total, currency)}</strong>
            </div>
          </div>
          <div className="form-actions">
            <small>
              La nueva versión reemplaza la propuesta vigente y requiere
              aprobación.
            </small>
            <button className="button primary" disabled={busy}>
              Publicar presupuesto
              <ArrowRight size={16} />
            </button>
          </div>
        </form>
      )}
      {!q && !canEdit && (
        <Empty
          title="Primero, el diagnóstico"
          text="Iniciá el diagnóstico de esta orden para preparar el presupuesto."
        />
      )}
      {d.quotes.length > 1 && (
        <section className="panel padded">
          <h3>Versiones anteriores</h3>
          {d.quotes.slice(1).map((x: any) => (
            <div className="revision-row" key={x.id}>
              <b>v{x.revision}</b>
              <span>{fullDate(x.createdAtUtc)}</span>
              <span>{quoteNames[x.status]}</span>
              <strong>{money(x.total, x.currency)}</strong>
            </div>
          ))}
        </section>
      )}
    </div>
  );
}

function OperationDialog({
  kind,
  d,
  due,
  currency,
  admin,
  busy,
  onClose,
  onSave,
}: {
  kind: string;
  d: any;
  due: number;
  currency: string;
  admin: boolean;
  busy: boolean;
  onClose: () => void;
  onSave: (path: string, b: any) => Promise<void>;
}) {
  const refund = kind.startsWith("refund:"),
    payment = refund
      ? d.payments.find((p: any) => p.id === kind.split(":")[1])
      : null,
    limit = refund
      ? payment.amount -
        d.refunds
          .filter((r: any) => r.paymentId === payment.id)
          .reduce((s: number, r: any) => s + r.amount, 0)
      : due;
  const [amount, setAmount] = useState(limit);
  const [reason, setReason] = useState("");
  const [recipient, setRecipient] = useState(d.workflow.customerName);
  const [method, setMethod] = useState(1);
  const [reference, setReference] = useState("");
  const [error, setError] = useState("");
  const title = refund
    ? "Registrar devolución"
    : kind === "payment"
      ? "Registrar cobro"
      : kind === "handover"
        ? "Entregar el equipo"
        : "Cancelar la orden";
  return (
    <Modal
      title={title}
      subtitle={
        kind === "payment" ? `Saldo: ${money(due, currency)}` : undefined
      }
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setError("");
          try {
            await onSave(
              refund
                ? "refunds"
                : kind === "payment"
                  ? "payments"
                  : kind === "handover"
                    ? "handover"
                    : "stage",
              refund
                ? { paymentId: payment.id, amount: Number(amount), reason }
                : kind === "payment"
                  ? { amount: Number(amount), currency, method, reference }
                  : kind === "handover"
                    ? { recipient, debtReason: reason }
                    : { status: "Cancelled", reason },
            );
          } catch (e) {
            setError((e as Error).message);
          }
        }}
      >
        <div className="dialog-body">
          <ErrorBox message={error} />
          {(refund || kind === "payment") && (
            <Field label={`Importe · ${currency}`}>
              <input
                autoFocus
                required
                type="number"
                min="0.01"
                max={limit}
                step="0.01"
                value={amount}
                onChange={(e) => setAmount(Number(e.target.value))}
              />
            </Field>
          )}
          {kind === "payment" && (
            <>
              <Field label="Medio de pago">
                <select
                  value={method}
                  onChange={(e) => setMethod(Number(e.target.value))}
                >
                  {["Efectivo", "Transferencia", "Tarjeta", "Otro"].map(
                    (x, i) => (
                      <option key={x} value={i}>
                        {x}
                      </option>
                    ),
                  )}
                </select>
              </Field>
              <Field label="Referencia (opcional)">
                <input
                  maxLength={120}
                  value={reference}
                  onChange={(e) => setReference(e.target.value)}
                />
              </Field>
            </>
          )}
          {kind === "handover" && (
            <>
              <Field label="Nombre de quien retira">
                <input
                  required
                  minLength={3}
                  maxLength={120}
                  value={recipient}
                  onChange={(e) => setRecipient(e.target.value)}
                />
              </Field>
              {due > 0 && (
                <div className="info-box warning">
                  Hay {money(due, currency)} pendientes.
                  {admin
                    ? " Justificá la entrega con deuda."
                    : " Un administrador debe autorizar esta entrega."}
                </div>
              )}
            </>
          )}
          {(refund ||
            kind === "cancel" ||
            (kind === "handover" && due > 0)) && (
            <Field
              label={
                kind === "handover"
                  ? "Justificación de entrega con deuda"
                  : "Motivo"
              }
            >
              <textarea
                required
                minLength={3}
                maxLength={300}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </Field>
          )}
        </div>
        <div className="dialog-footer">
          <button type="button" className="button secondary" onClick={onClose}>
            Volver
          </button>
          <button
            className="button primary"
            disabled={busy || (kind === "handover" && due > 0 && !admin)}
          >
            {busy ? "Guardando…" : "Confirmar"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
