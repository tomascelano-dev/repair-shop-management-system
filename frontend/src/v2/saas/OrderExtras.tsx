import { useCallback, useEffect, useState } from "react";
import {
  Copy,
  ExternalLink,
  Printer,
  ReceiptText,
  Send,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";
import { request, money, fullDate } from "../api";
import { Field, ErrorBox, Loading } from "../ui";
import { Table, Detail } from "../premium/shared";
import { useSession } from "./session";
import { IssueInvoiceDialog, docTypes, taxConditions } from "./Invoices";

const outboxStatus: Record<string, string> = {
  Pending: "Pendiente",
  Sent: "Enviado",
  Failed: "Falló",
  Processing: "Enviando",
};
const invoiceStatus: Record<string, string> = {
  Authorized: "Con CAE",
  Simulated: "Interna",
  Rejected: "Rechazada",
};
const voucherNames: Record<number, string> = {
  1: "Factura A",
  6: "Factura B",
  11: "Factura C",
  3: "NC A",
  8: "NC B",
  13: "NC C",
};

async function copy(text: string) {
  try {
    await navigator.clipboard.writeText(text);
    toast.success("Copiado");
  } catch {
    toast.error("No se pudo copiar.");
  }
}

export function OrderOnline({
  orderId,
  version,
  customerName,
  due,
  currency,
  accepted,
  closed,
  onChanged,
}: {
  orderId: string;
  version: number;
  customerName: string;
  due: number;
  currency: string;
  accepted: boolean;
  closed: boolean;
  onChanged: () => void;
}) {
  const { has, admin } = useSession();
  const [d, setD] = useState<any>(null);
  const [contact, setContact] = useState<any>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [link, setLink] = useState("");
  const [invoice, setInvoice] = useState(false);
  const load = useCallback(
    () =>
      request(`/api/saas/orders/${orderId}`)
        .then((r) => {
          setD(r);
          setContact(
            (c: any) =>
              c || {
                email: r.email || r.contact?.email || "",
                docType: r.contact?.docType || 99,
                docNumber: r.contact?.docNumber || "",
                taxCondition: r.contact?.taxCondition || "ConsumidorFinal",
                address: r.contact?.address || "",
              },
          );
        })
        .catch((e) => setError(e.message)),
    [orderId],
  );
  useEffect(() => {
    load();
  }, [load, version]);
  if (!d) return error ? <ErrorBox message={error} /> : <Loading />;
  const run = async (fn: () => Promise<any>) => {
    setBusy(true);
    try {
      return await fn();
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="detail-grid">
      <div className="stack">
        <section className="panel padded">
          <h3>Seguimiento online</h3>
          <p className="muted-line">
            El cliente consulta el estado con este código, sin usuario ni
            contraseña.
          </p>
          <div className="tracking-code">
            <strong>{d.code}</strong>
            <button
              className="icon-button"
              onClick={() => copy(d.trackUrl)}
              aria-label="Copiar enlace de seguimiento"
            >
              <Copy size={16} />
            </button>
            <a
              className="icon-button"
              href={d.trackUrl}
              target="_blank"
              rel="noreferrer"
              aria-label="Abrir seguimiento"
            >
              <ExternalLink size={16} />
            </a>
          </div>
          <div className="form-actions">
            <a
              className="button secondary small"
              href={`/print/order/${orderId}`}
              target="_blank"
              rel="noreferrer"
            >
              <Printer size={15} />
              Ticket de recepción
            </a>
            {has("portal") && !closed && (
              <button
                className="button primary small"
                disabled={busy}
                onClick={() =>
                  run(async () => {
                    const r = await request(
                      `/api/saas/orders/${orderId}/send-portal`,
                      "POST",
                      { version },
                    );
                    setLink(`${window.location.origin}/portal#${r.token}`);
                    toast.success(
                      r.sentTo.length
                        ? `Enlace enviado a ${r.sentTo.join(" y ")}`
                        : "Enlace creado. Cargá un email para enviarlo.",
                    );
                    onChanged();
                    load();
                  })
                }
              >
                <Send size={15} />
                Enviar presupuesto al cliente
              </button>
            )}
          </div>
          {link && (
            <div className="portal-link-box">
              <a
                className="text-link"
                href={link}
                target="_blank"
                rel="noreferrer"
              >
                Abrir como cliente
              </a>
              <button className="text-link" onClick={() => copy(link)}>
                <Copy size={14} />
                Copiar enlace
              </button>
            </div>
          )}
        </section>
        <section className="panel padded">
          <h3>Datos para avisos y factura</h3>
          <form
            className="stack"
            onSubmit={(e) => {
              e.preventDefault();
              run(async () => {
                await request(
                  `/api/saas/orders/${orderId}/contact`,
                  "PUT",
                  contact,
                );
                toast.success("Datos del cliente guardados");
                load();
              });
            }}
          >
            <Field label="Email">
              <input
                type="email"
                value={contact.email}
                onChange={(e) =>
                  setContact({ ...contact, email: e.target.value })
                }
              />
            </Field>
            <div className="form-grid">
              <Field label="Documento">
                <select
                  value={contact.docType}
                  onChange={(e) =>
                    setContact({ ...contact, docType: Number(e.target.value) })
                  }
                >
                  {docTypes.map((t) => (
                    <option key={t.value} value={t.value}>
                      {t.label}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Número">
                <input
                  inputMode="numeric"
                  disabled={contact.docType === 99}
                  value={contact.docNumber}
                  onChange={(e) =>
                    setContact({ ...contact, docNumber: e.target.value })
                  }
                />
              </Field>
            </div>
            <Field label="Condición frente al IVA">
              <select
                value={contact.taxCondition}
                onChange={(e) =>
                  setContact({ ...contact, taxCondition: e.target.value })
                }
              >
                {Object.entries(taxConditions).map(([v, l]) => (
                  <option key={v} value={v}>
                    {l}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Domicilio">
              <input
                value={contact.address}
                onChange={(e) =>
                  setContact({ ...contact, address: e.target.value })
                }
              />
            </Field>
            <button className="button secondary small" disabled={busy}>
              Guardar datos
            </button>
          </form>
        </section>
      </div>
      <div className="stack">
        {has("invoicing") && (
          <section className="panel padded">
            <div className="panel-heading compact">
              <h3>Facturas</h3>
              {accepted && (
                <button
                  className="button primary small"
                  onClick={() => setInvoice(true)}
                >
                  <ReceiptText size={15} />
                  Facturar
                </button>
              )}
            </div>
            {d.invoices.length ? (
              <Table
                headers={["Comprobante", "Total", "Estado", ""]}
                rows={d.invoices.map((i: any) => [
                  <Detail
                    main={voucherNames[i.voucherType]}
                    sub={`${String(i.pointOfSale).padStart(5, "0")}-${String(i.number).padStart(8, "0")}`}
                  />,
                  money(i.total, i.currency),
                  invoiceStatus[i.status] || i.status,
                  i.status !== "Rejected" ? (
                    <a
                      className="text-link"
                      href={`/print/invoice/${i.id}`}
                      target="_blank"
                      rel="noreferrer"
                    >
                      Ver
                    </a>
                  ) : null,
                ])}
              />
            ) : (
              <p className="muted-line">
                {accepted
                  ? "Todavía no se facturó."
                  : "Se factura con el presupuesto aprobado."}
              </p>
            )}
          </section>
        )}
        {has("cash") && admin && accepted && due > 0 && !closed && (
          <section className="panel padded">
            <h3>Saldo a cuenta corriente</h3>
            <p className="muted-line">
              Entregá el equipo y dejá {money(due, currency)} en la cuenta del
              cliente.
            </p>
            <button
              className="button secondary small"
              disabled={busy}
              onClick={() =>
                confirm(
                  `¿Pasar ${money(due, currency)} a la cuenta corriente de ${customerName}?`,
                ) &&
                run(async () => {
                  await request(
                    `/api/saas/accounts/orders/${orderId}`,
                    "POST",
                    { version },
                  );
                  toast.success("Saldo pasado a cuenta corriente");
                  onChanged();
                })
              }
            >
              Pasar saldo a cuenta
            </button>
          </section>
        )}
        {d.signatures.length > 0 && (
          <section className="panel padded">
            <h3>Decisiones del cliente</h3>
            <ul className="signature-list">
              {d.signatures.map((s: any) => (
                <li key={s.id}>
                  {s.signatureDataUrl && (
                    <img
                      src={s.signatureDataUrl}
                      alt={`Firma de ${s.signerName}`}
                    />
                  )}
                  <Detail
                    main={`${s.accepted ? "Aprobó" : "Rechazó"}: ${s.signerName}`}
                    sub={`${fullDate(s.createdAtUtc)} · IP ${s.ipAddress || "—"}`}
                  />
                </li>
              ))}
            </ul>
          </section>
        )}
        {d.survey && (
          <section className="panel padded">
            <h3>Encuesta</h3>
            {d.survey.answeredAtUtc ? (
              <Detail
                main={`${d.survey.score}/10`}
                sub={d.survey.comment || "Sin comentario"}
              />
            ) : (
              <p className="muted-line">
                Enviada el {fullDate(d.survey.createdAtUtc)}. Sin respuesta
                todavía.
              </p>
            )}
          </section>
        )}
        <section className="panel padded">
          <h3>Mensajes al cliente</h3>
          <Table
            headers={["Fecha", "Canal", "Asunto", "Estado"]}
            rows={d.messages.map((m: any) => [
              fullDate(m.createdAtUtc),
              m.channel,
              m.title,
              <Detail
                main={outboxStatus[m.status] || m.status}
                sub={m.lastError}
              />,
            ])}
            empty={
              d.email
                ? "Todavía no se enviaron mensajes."
                : "Cargá el email del cliente para enviarle avisos."
            }
          />
        </section>
      </div>
      {invoice && (
        <IssueInvoiceDialog
          sourceType="Order"
          sourceId={orderId}
          defaultName={customerName}
          defaults={contact}
          onClose={() => {
            setInvoice(false);
            load();
            onChanged();
          }}
        />
      )}
    </div>
  );
}

const markKinds: Record<string, { label: string; color: string }> = {
  scratch: { label: "Rayón", color: "#d97706" },
  crack: { label: "Rotura / fisura", color: "#dc2626" },
  dent: { label: "Golpe / abolladura", color: "#7c3aed" },
  missing: { label: "Falta pieza", color: "#2563eb" },
  other: { label: "Otro", color: "#475569" },
};
const templates: Record<string, { label: string; shapes: JSX.Element }> = {
  phone: {
    label: "Celular",
    shapes: (
      <>
        <rect x="8" y="6" width="36" height="88" rx="6" />
        <rect x="56" y="6" width="36" height="88" rx="6" />
        <rect x="11" y="12" width="30" height="74" rx="2" className="screen" />
        <circle cx="66" cy="16" r="5" />
        <text x="26" y="99">
          Frente
        </text>
        <text x="74" y="99">
          Dorso
        </text>
      </>
    ),
  },
  tablet: {
    label: "Tablet",
    shapes: (
      <>
        <rect x="4" y="12" width="44" height="74" rx="5" />
        <rect x="52" y="12" width="44" height="74" rx="5" />
        <rect x="8" y="17" width="36" height="64" rx="2" className="screen" />
        <circle cx="60" cy="20" r="3" />
        <text x="26" y="94">
          Frente
        </text>
        <text x="74" y="94">
          Dorso
        </text>
      </>
    ),
  },
  laptop: {
    label: "Notebook",
    shapes: (
      <>
        <rect x="14" y="8" width="72" height="46" rx="3" />
        <rect x="18" y="12" width="64" height="38" rx="1" className="screen" />
        <path d="M6 58h88l-6 18H12z" />
        <rect x="38" y="64" width="24" height="8" rx="1" />
        <text x="50" y="88">
          Tapa, pantalla y base
        </text>
      </>
    ),
  },
  console: {
    label: "Consola / joystick",
    shapes: (
      <>
        <rect x="10" y="10" width="80" height="34" rx="4" />
        <path d="M22 62c-10 0-14 22-6 26 6 3 12-6 16-10h36c4 4 10 13 16 10 8-4 4-26-6-26z" />
        <circle cx="36" cy="70" r="4" />
        <circle cx="64" cy="70" r="4" />
        <text x="50" y="97">
          Consola y control
        </text>
      </>
    ),
  },
  watch: {
    label: "Reloj",
    shapes: (
      <>
        <rect x="36" y="4" width="28" height="22" rx="4" />
        <rect x="30" y="26" width="40" height="46" rx="10" />
        <rect x="35" y="31" width="30" height="36" rx="6" className="screen" />
        <rect x="36" y="72" width="28" height="22" rx="4" />
      </>
    ),
  },
};

export function DamageDiagram({
  orderId,
  closed,
}: {
  orderId: string;
  closed: boolean;
}) {
  const { has } = useSession();
  const [d, setD] = useState<any>(null);
  const [kind, setKind] = useState("scratch");
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => {
    if (has("diagram"))
      request(`/api/saas/diagram/${orderId}`)
        .then(setD)
        .catch((e) => setError(e.message));
  }, [orderId]);
  if (!has("diagram"))
    return (
      <div className="info-box">
        El diagrama de daños está incluido desde el plan Básico. Revisá tu plan
        en Configuración.
      </div>
    );
  if (!d) return error ? <ErrorBox message={error} /> : <Loading />;
  const change = (patch: any) => {
    setD({ ...d, ...patch });
    setDirty(true);
  };
  return (
    <div className="detail-grid">
      <section className="panel padded">
        <div className="panel-heading compact">
          <h3>Estado físico al ingresar</h3>
          <select
            value={d.template}
            disabled={closed}
            onChange={(e) => change({ template: e.target.value })}
          >
            {Object.entries(templates).map(([k, t]) => (
              <option key={k} value={k}>
                {t.label}
              </option>
            ))}
          </select>
        </div>
        <div className="kind-picker">
          {Object.entries(markKinds).map(([k, m]) => (
            <button
              key={k}
              className={kind === k ? "active" : ""}
              onClick={() => setKind(k)}
            >
              <i style={{ background: m.color }} />
              {m.label}
            </button>
          ))}
        </div>
        <svg
          className="diagram"
          viewBox="0 0 100 100"
          role="img"
          aria-label="Diagrama del equipo. Tocá para marcar un daño."
          onClick={(e) => {
            if (closed || d.marks.length >= 40) return;
            const r = (
              e.currentTarget as SVGSVGElement
            ).getBoundingClientRect();
            const x = Math.round(((e.clientX - r.left) / r.width) * 1000) / 10;
            const y = Math.round(((e.clientY - r.top) / r.height) * 1000) / 10;
            change({ marks: [...d.marks, { x, y, kind, note: "" }] });
          }}
        >
          <g className="outline">{templates[d.template]?.shapes}</g>
          {d.marks.map((m: any, i: number) => (
            <g key={i}>
              <circle
                cx={m.x}
                cy={m.y}
                r="2.6"
                fill={markKinds[m.kind]?.color}
              />
              <text x={m.x} y={m.y + 1} className="mark-number">
                {i + 1}
              </text>
            </g>
          ))}
        </svg>
        <p className="muted-line">
          Tocá el dibujo para marcar cada daño. Queda en el ticket y en el
          historial de la orden.
        </p>
      </section>
      <section className="panel padded">
        <h3>Marcas</h3>
        {d.marks.length === 0 ? (
          <p className="muted-line">Sin daños marcados.</p>
        ) : (
          <ol className="mark-list">
            {d.marks.map((m: any, i: number) => (
              <li key={i}>
                <i style={{ background: markKinds[m.kind]?.color }}>{i + 1}</i>
                <span>{markKinds[m.kind]?.label}</span>
                <input
                  placeholder="Detalle (opcional)"
                  value={m.note}
                  disabled={closed}
                  maxLength={200}
                  onChange={(e) =>
                    change({
                      marks: d.marks.map((x: any, j: number) =>
                        j === i ? { ...x, note: e.target.value } : x,
                      ),
                    })
                  }
                />
                {!closed && (
                  <button
                    className="icon-button"
                    aria-label="Quitar marca"
                    onClick={() =>
                      change({
                        marks: d.marks.filter((_: any, j: number) => j !== i),
                      })
                    }
                  >
                    <Trash2 size={14} />
                  </button>
                )}
              </li>
            ))}
          </ol>
        )}
        <button
          className="button primary small"
          disabled={!dirty || busy || closed}
          onClick={async () => {
            setBusy(true);
            try {
              const r = await request(`/api/saas/diagram/${orderId}`, "PUT", {
                version: d.version,
                template: d.template,
                marks: d.marks,
              });
              setD({ ...d, version: r.version });
              setDirty(false);
              toast.success("Diagrama guardado");
            } catch (e) {
              toast.error((e as Error).message);
            } finally {
              setBusy(false);
            }
          }}
        >
          Guardar diagrama
        </button>
      </section>
    </div>
  );
}

export { markKinds, templates };
