import { createContext, useContext, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { Plus } from "lucide-react";
import { Field, Modal, ErrorBox } from "../ui";
import { code, money, date } from "../api";

export type Row = Record<string, any>;
export type Workspace = Record<string, Row[]>;
export type Option = { value: string; label: string };
export type InputField = {
  name: string;
  label: string;
  type?: string;
  required?: boolean;
  options?: Option[];
  hint?: string;
  min?: number;
  max?: number;
  step?: string;
  span?: boolean;
};
export type EditSpec = {
  title: string;
  subtitle?: string;
  fields: InputField[];
  initial?: Row;
  submit?: string;
  save: (data: Row) => Promise<unknown>;
};
export type PremiumContextValue = {
  d: Workspace;
  admin: boolean;
  mutate: (path: string, body: Row) => Promise<any>;
  edit: (spec: EditSpec) => void;
  orderFilter: string;
};
export const PremiumContext = createContext<PremiumContextValue>(null!);
export const usePremium = () => useContext(PremiumContext);
export const options = (rows: Row[], label: (r: Row) => string): Option[] =>
  rows.map((r) => ({ value: r.id, label: label(r) }));
export const currencies = [
  { value: "ARS", label: "Pesos argentinos · ARS" },
  { value: "USD", label: "Dólares · USD" },
];
export const gradeOptions = ["A", "B", "C"].map((value) => ({
  value,
  label: `Grado ${value}`,
}));
export const labels: Record<string, string> = {
  Reserved: "Reservado",
  Consumed: "Consumido",
  Released: "Liberado",
  Open: "Abierta",
  SupplierReview: "En proveedor",
  Resolved: "Resuelta",
  Rejected: "Rechazada",
  Received: "Recibido",
  Repairing: "En reparación",
  Ready: "Listo para vender",
  Sold: "Vendido",
  Active: "Activo",
  Paused: "Pausado",
  Closed: "Cerrado",
  Paid: "Cobrado",
  Pending: "Pendiente",
  Labor: "Mano de obra",
  Commission: "Comisión",
  Other: "Otro costo",
  Receipt: "Compra",
  Opening: "Stock inicial",
  Reservation: "Reserva",
  Release: "Liberación",
  Consumption: "Consumo",
  Transfer: "Transferencia",
  TransferIn: "Entrada",
  TransferOut: "Salida",
  Adjustment: "Ajuste",
};
export const isOpen = (o: Row) =>
  !["Delivered", "Cancelled"].includes(o.status);
export const total = (rows: Row[], key: string) =>
  rows.reduce((s, r) => s + (Number(r[key]) || 0), 0);
export const available = (d: Workspace, lot: Row) =>
  lot.quantity -
  d.reservations
    .filter((r) => r.lotId === lot.id && r.status === "Reserved")
    .reduce((s, r) => s + r.quantity, 0);
export const orderOptions = (d: Workspace, predicate = (_: Row) => true) =>
  options(
    d.orders.filter(predicate),
    (o) => `${code(o.number)} · ${o.customerName} · ${o.deviceLabel}`,
  );
export function Chip({ value }: { value: string }) {
  return (
    <span
      className={`premium-chip ${["Resolved", "Ready", "Paid", "Consumed", "Active"].includes(value) ? "good" : ["Open", "Reserved", "SupplierReview", "Pending"].includes(value) ? "warn" : ""}`}
    >
      {labels[value] || value}
    </span>
  );
}
export function OrderLink({ d, id }: { d: Workspace; id: string }) {
  const o = d.orders.find((o) => o.id === id);
  return (
    <Link className="premium-link" to={`/orders/${id}`}>
      {o ? code(o.number) : "Ver orden"}
    </Link>
  );
}
export function Metrics({
  items,
}: {
  items: { label: string; value: ReactNode; hint: string }[];
}) {
  return (
    <div className="premium-metrics">
      {items.map((i) => (
        <section key={i.label}>
          <small>{i.label}</small>
          <strong>{i.value}</strong>
          <span>{i.hint}</span>
        </section>
      ))}
    </div>
  );
}
export function Section({
  title,
  text,
  action,
  children,
}: {
  title: string;
  text?: string;
  action?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="panel premium-section">
      <div className="panel-heading">
        <div>
          <h3>{title}</h3>
          {text && <p>{text}</p>}
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}
export function Table({
  headers,
  rows,
  empty = "Todavía no hay registros. Creá el primero para comenzar.",
}: {
  headers: string[];
  rows: ReactNode[][];
  empty?: string;
}) {
  return rows.length ? (
    <div className="table-scroll">
      <table className="premium-table">
        <thead>
          <tr>
            {headers.map((h) => (
              <th key={h}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((cells, i) => (
            <tr key={i}>
              {cells.map((cell, j) => (
                <td key={j}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <div className="premium-empty">{empty}</div>
  );
}
export function AddButton({
  children,
  onClick,
  disabled,
}: {
  children: ReactNode;
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <button
      className="button primary small"
      onClick={onClick}
      disabled={disabled}
    >
      <Plus size={16} />
      {children}
    </button>
  );
}
export function Detail({ main, sub }: { main: ReactNode; sub?: ReactNode }) {
  return (
    <div className="premium-detail">
      <strong>{main}</strong>
      {sub && <small>{sub}</small>}
    </div>
  );
}

export function Editor({
  spec,
  onClose,
}: {
  spec: EditSpec;
  onClose: () => void;
}) {
  const [values, setValues] = useState<Row>(() =>
    Object.fromEntries(
      spec.fields.map((f) => [
        f.name,
        spec.initial?.[f.name] ??
          (f.type === "checks" ? [] : f.type === "checkbox" ? false : ""),
      ]),
    ),
  );
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const set = (name: string, value: any) =>
    setValues((v) => ({ ...v, [name]: value }));
  return (
    <Modal
      title={spec.title}
      subtitle={spec.subtitle}
      onClose={() => !busy && onClose()}
      wide
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await spec.save(values);
            onClose();
          } catch (e) {
            setError((e as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="dialog-body">
          <ErrorBox message={error} />
          <div className="form-grid premium-form">
            {spec.fields.map((f) => (
              <div
                key={f.name}
                className={
                  f.span || f.type === "textarea" || f.type === "checks"
                    ? "span-two"
                    : ""
                }
              >
                {f.type === "checks" ? (
                  <fieldset className="premium-checks">
                    <legend>{f.label}</legend>
                    {f.options?.map((o) => (
                      <label key={o.value}>
                        <input
                          type="checkbox"
                          checked={(values[f.name] as string[]).includes(
                            o.value,
                          )}
                          onChange={(e) =>
                            set(
                              f.name,
                              e.target.checked
                                ? [...values[f.name], o.value]
                                : values[f.name].filter(
                                    (v: string) => v !== o.value,
                                  ),
                            )
                          }
                        />
                        {o.label}
                      </label>
                    ))}
                    {f.hint && <small>{f.hint}</small>}
                  </fieldset>
                ) : f.type === "checkbox" ? (
                  <label className="premium-check">
                    <input
                      type="checkbox"
                      checked={!!values[f.name]}
                      onChange={(e) => set(f.name, e.target.checked)}
                    />
                    {f.label}
                  </label>
                ) : (
                  <Field label={f.label} hint={f.hint}>
                    {f.type === "select" ? (
                      <select
                        required={f.required !== false}
                        value={values[f.name]}
                        onChange={(e) => set(f.name, e.target.value)}
                      >
                        <option value="">Seleccionar…</option>
                        {f.options?.map((o) => (
                          <option value={o.value} key={o.value}>
                            {o.label}
                          </option>
                        ))}
                      </select>
                    ) : f.type === "textarea" ? (
                      <textarea
                        rows={3}
                        maxLength={f.max || 1000}
                        required={f.required !== false}
                        value={values[f.name]}
                        onChange={(e) => set(f.name, e.target.value)}
                      />
                    ) : (
                      <input
                        type={f.type || "text"}
                        required={f.required !== false}
                        min={f.min ?? (f.type === "number" ? 0 : undefined)}
                        max={f.max}
                        maxLength={
                          f.type === "text" || !f.type
                            ? f.max || 200
                            : undefined
                        }
                        step={
                          f.step || (f.type === "number" ? "0.01" : undefined)
                        }
                        value={values[f.name]}
                        onChange={(e) =>
                          set(
                            f.name,
                            f.type === "number"
                              ? e.target.value === ""
                                ? ""
                                : Number(e.target.value)
                              : e.target.value,
                          )
                        }
                      />
                    )}
                  </Field>
                )}
              </div>
            ))}
          </div>
        </div>
        <div className="dialog-footer">
          <button
            type="button"
            className="button secondary"
            onClick={onClose}
            disabled={busy}
          >
            Cancelar
          </button>
          <button className="button primary" disabled={busy}>
            {busy ? "Guardando…" : spec.submit || "Guardar"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

export function receiveEditor(ctx: PremiumContextValue, offer?: Row) {
  const { d, edit, mutate } = ctx;
  const match = offer && d.items.find((i) => i.sku === offer.sku);
  edit({
    title: "Recibir repuestos",
    subtitle:
      "La recepción suma stock, registra el lote y actualiza el costo del catálogo.",
    initial: {
      branchId: d.branches[0]?.id,
      itemId: match?.id || "",
      sku: offer?.sku || "",
      name: offer?.description || "",
      supplier: offer?.supplier || "",
      lotCode: `LOTE-${new Date().toISOString().slice(0, 10)}`,
      quantity: 1,
      unitCost: offer?.unitCost || 0,
      currency: offer?.currency || "ARS",
    },
    fields: [
      {
        name: "branchId",
        label: "Sucursal",
        type: "select",
        options: options(d.branches, (b) => b.name),
      },
      {
        name: "itemId",
        label: "Repuesto existente",
        type: "select",
        required: false,
        options: options(
          d.items.filter((i) => i.isActive),
          (i) => `${i.sku} · ${i.name}`,
        ),
        hint: "Dejá vacío para crear el SKU indicado abajo.",
      },
      {
        name: "sku",
        label: "SKU para repuesto nuevo",
        required: false,
        max: 60,
      },
      {
        name: "name",
        label: "Nombre para repuesto nuevo",
        required: false,
        max: 120,
      },
      { name: "supplier", label: "Proveedor", required: false },
      { name: "lotCode", label: "Código de lote", max: 80 },
      {
        name: "serial",
        label: "Número de serie",
        required: false,
        hint: "Si tiene serie, la cantidad debe ser 1.",
        max: 100,
      },
      {
        name: "quantity",
        label: "Cantidad recibida",
        type: "number",
        min: 1,
        max: 10000,
        step: "1",
      },
      { name: "unitCost", label: "Costo por unidad", type: "number" },
      {
        name: "currency",
        label: "Moneda",
        type: "select",
        options: currencies,
      },
    ],
    save: (v) =>
      mutate("stock/receive", {
        ...v,
        itemId: v.itemId || null,
        offerId: offer?.id || null,
        serial: v.serial || null,
      }),
  });
}
export { money, code, date };
