import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Minus, Plus, Printer, ReceiptText, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { request, money, fullDate } from "../api";
import { Field, ErrorBox, Loading, Modal } from "../ui";
import {
  Section,
  Table,
  Metrics,
  Detail,
  Editor,
  type EditSpec,
  currencies,
} from "../premium/shared";
import { useSession, methodNames, methodOptions } from "./session";
import { IssueInvoiceDialog } from "./Invoices";

const kindNames: Record<string, string> = {
  Income: "Ingreso",
  Expense: "Egreso",
  Sale: "Venta",
  OrderPayment: "Cobro de orden",
  Refund: "Devolución",
  AccountPayment: "Pago de cuenta",
  Void: "Anulación",
};

export default function Cash() {
  const [tab, setTab] = useState("Caja");
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>Caja</h1>
          <p>
            Apertura y cierre, ventas de mostrador, cobros de órdenes y cuentas
            corrientes.
          </p>
        </div>
      </div>
      <div className="tabs">
        {["Caja", "Mostrador", "Ventas", "Cuentas corrientes"].map((t) => (
          <button
            key={t}
            className={tab === t ? "active" : ""}
            onClick={() => setTab(t)}
          >
            {t}
          </button>
        ))}
      </div>
      {tab === "Caja" && <Register />}
      {tab === "Mostrador" && <Counter onDone={() => setTab("Ventas")} />}
      {tab === "Ventas" && <Sales />}
      {tab === "Cuentas corrientes" && <Accounts />}
    </>
  );
}

function useCash() {
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  const load = useCallback(
    () =>
      request("/api/saas/cash")
        .then(setD)
        .catch((e) => setError(e.message)),
    [],
  );
  useEffect(() => {
    load();
  }, [load]);
  return { d, error, load };
}

function Register() {
  const { admin } = useSession();
  const { d, error, load } = useCash();
  const [form, setForm] = useState<EditSpec | null>(null);
  if (!d) return error ? <ErrorBox message={error} /> : <Loading />;
  const s = d.session;
  const user = (id: string) =>
    d.users.find((u: any) => u.id === id)?.displayName || "—";
  const save = (path: string, ok: string) => async (body: any) => {
    const r = await request(`/api/saas/${path}`, "POST", body);
    toast.success(ok);
    await load();
    return r;
  };
  return (
    <div className="settings-stack">
      {!s ? (
        <section className="panel padded cash-closed">
          <h3>La caja está cerrada</h3>
          <p>Abrila con el efectivo inicial para registrar ventas y cobros.</p>
          <button
            className="button primary"
            onClick={() =>
              setForm({
                title: "Abrir caja",
                fields: [
                  {
                    name: "openingCash",
                    label: "Efectivo inicial en pesos",
                    type: "number",
                  },
                  {
                    name: "openingCashUsd",
                    label: "Efectivo inicial en dólares",
                    type: "number",
                  },
                  {
                    name: "notes",
                    label: "Notas",
                    type: "textarea",
                    required: false,
                  },
                ],
                initial: { openingCash: 0, openingCashUsd: 0 },
                submit: "Abrir caja",
                save: save("cash/open", "Caja abierta"),
              })
            }
          >
            Abrir caja
          </button>
        </section>
      ) : (
        <>
          <Metrics
            items={[
              {
                label: "Efectivo esperado",
                value: money(d.expected.cash),
                hint: `Inicial ${money(s.openingCash)}`,
              },
              {
                label: "Dólares esperados",
                value: money(d.expected.cashUsd, "USD"),
                hint: `Inicial ${money(s.openingCashUsd, "USD")}`,
              },
              {
                label: "Movimientos",
                value: d.movements.length,
                hint: `Abierta por ${user(s.openedBy)}`,
              },
              {
                label: "Apertura",
                value: fullDate(s.createdAtUtc),
                hint: s.notes || "Sin notas",
              },
            ]}
          />
          <Section
            title="Por medio de pago"
            action={
              <div className="row-actions">
                <button
                  className="button secondary small"
                  onClick={() =>
                    setForm({
                      title: "Registrar movimiento",
                      fields: [
                        {
                          name: "kind",
                          label: "Tipo",
                          type: "select",
                          options: [
                            { value: "Income", label: "Ingreso" },
                            ...(admin
                              ? [
                                  {
                                    value: "Expense",
                                    label: "Egreso (gasto, retiro)",
                                  },
                                ]
                              : []),
                          ],
                        },
                        {
                          name: "method",
                          label: "Medio",
                          type: "select",
                          options: methodOptions.filter(
                            (m) => m.value !== "Account",
                          ),
                        },
                        {
                          name: "amount",
                          label: "Importe",
                          type: "number",
                          required: true,
                        },
                        {
                          name: "currency",
                          label: "Moneda",
                          type: "select",
                          options: currencies,
                        },
                        {
                          name: "description",
                          label: "Detalle",
                          required: true,
                          span: true,
                        },
                      ],
                      initial: {
                        kind: "Income",
                        method: "Cash",
                        currency: "ARS",
                      },
                      save: save("cash/movements", "Movimiento registrado"),
                    })
                  }
                >
                  Ingreso / egreso
                </button>
                <button
                  className="button primary small"
                  onClick={() =>
                    setForm({
                      title: "Cerrar caja",
                      subtitle: `Esperado: ${money(d.expected.cash)} y ${money(d.expected.cashUsd, "USD")} en efectivo.`,
                      fields: [
                        {
                          name: "countedCash",
                          label: "Efectivo contado en pesos",
                          type: "number",
                          required: true,
                        },
                        {
                          name: "countedCashUsd",
                          label: "Efectivo contado en dólares",
                          type: "number",
                        },
                        {
                          name: "notes",
                          label: "Notas",
                          type: "textarea",
                          required: false,
                        },
                      ],
                      initial: {
                        countedCash: d.expected.cash,
                        countedCashUsd: d.expected.cashUsd,
                      },
                      submit: "Cerrar caja",
                      save: async (b) => {
                        const r = await save(
                          "cash/close",
                          "Caja cerrada",
                        )({ ...b, version: s.version });
                        if (r.difference !== 0)
                          toast.warning(
                            `Diferencia en pesos: ${money(r.difference)}`,
                          );
                      },
                    })
                  }
                >
                  Cerrar caja
                </button>
              </div>
            }
          >
            <Table
              headers={["Medio", "Moneda", "Movimientos", "Total"]}
              rows={d.summary.map((x: any) => [
                methodNames[x.method] || x.method,
                x.currency,
                x.count,
                money(x.total, x.currency),
              ])}
              empty="Sin movimientos todavía."
            />
          </Section>
          <Section title="Movimientos de la caja">
            <Table
              headers={[
                "Hora",
                "Tipo",
                "Detalle",
                "Medio",
                "Importe",
                "Usuario",
              ]}
              rows={d.movements.map((m: any) => [
                fullDate(m.createdAtUtc),
                kindNames[m.kind] || m.kind,
                m.description,
                methodNames[m.method] || m.method,
                <b className={m.amount < 0 ? "red-text" : ""}>
                  {money(m.amount, m.currency)}
                </b>,
                user(m.actorId),
              ])}
            />
          </Section>
        </>
      )}
      <Section title="Cierres anteriores">
        <Table
          headers={["Cierre", "Esperado", "Contado", "Diferencia", "Cerró"]}
          rows={d.history.map((h: any) => {
            const diff = h.countedCash - h.expectedCash;
            return [
              fullDate(h.closedAtUtc),
              money(h.expectedCash),
              money(h.countedCash),
              <b
                className={
                  diff < 0 ? "red-text" : diff > 0 ? "amber-text" : "green-text"
                }
              >
                {money(diff)}
              </b>,
              user(h.closedBy),
            ];
          })}
          empty="Todavía no se cerró ninguna caja."
        />
      </Section>
      {form && <Editor spec={form} onClose={() => setForm(null)} />}
    </div>
  );
}

type Line = {
  key: string;
  description: string;
  quantity: number;
  unitPrice: number;
  itemId?: string;
  serviceId?: string;
};

function Counter({ onDone }: { onDone: () => void }) {
  const { has } = useSession();
  const [items, setItems] = useState<any[]>([]);
  const [services, setServices] = useState<any[]>([]);
  const [customers, setCustomers] = useState<any[]>([]);
  const [search, setSearch] = useState("");
  const [lines, setLines] = useState<Line[]>([]);
  const [customerId, setCustomerId] = useState("");
  const [discount, setDiscount] = useState(0);
  const [currency, setCurrency] = useState("ARS");
  const [method, setMethod] = useState("Cash");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [sold, setSold] = useState<any>(null);
  useEffect(() => {
    request("/api/v1/inventory?take=200")
      .then((d) => setItems(Array.isArray(d) ? d : d.items || []))
      .catch(() => {});
    request("/api/v1/customers?take=200")
      .then((d) => setCustomers(Array.isArray(d) ? d : d.items || []))
      .catch(() => {});
    if (has("catalog"))
      request("/api/saas/catalog")
        .then(setServices)
        .catch(() => {});
  }, []);
  const results = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return [];
    return [
      ...services
        .filter(
          (s) => s.active && `${s.code} ${s.name}`.toLowerCase().includes(q),
        )
        .map((s) => ({ kind: "service", ...s })),
      ...items
        .filter(
          (i) => i.isActive && `${i.sku} ${i.name}`.toLowerCase().includes(q),
        )
        .map((i) => ({ kind: "item", ...i })),
    ].slice(0, 8);
  }, [search, items, services]);
  const subtotal = lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
  const add = (l: Omit<Line, "key">) => {
    setLines((ls) => [...ls, { ...l, key: crypto.randomUUID() }]);
    setSearch("");
  };
  const update = (key: string, patch: Partial<Line>) =>
    setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  if (sold)
    return (
      <section className="panel padded cash-closed">
        <h3>Venta #{sold.number} registrada</h3>
        <p>Total {money(sold.total, currency)}.</p>
        <div className="form-actions">
          <a
            className="button secondary"
            href={`/print/sale/${sold.id}`}
            target="_blank"
            rel="noreferrer"
          >
            <Printer size={16} />
            Imprimir ticket
          </a>
          {has("invoicing") && (
            <button
              className="button secondary"
              onClick={() => setSold({ ...sold, invoice: true })}
            >
              <ReceiptText size={16} />
              Facturar
            </button>
          )}
          <button
            className="button primary"
            onClick={() => {
              setSold(null);
              setLines([]);
              setDiscount(0);
              setCustomerId("");
            }}
          >
            Nueva venta
          </button>
          <button className="button subtle" onClick={onDone}>
            Ver ventas
          </button>
        </div>
        {sold.invoice && (
          <IssueInvoiceDialog
            sourceType="Sale"
            sourceId={sold.id}
            defaultName={customers.find((c) => c.id === customerId)?.fullName}
            onClose={() => setSold({ ...sold, invoice: false })}
          />
        )}
      </section>
    );
  return (
    <div className="pos-grid">
      <Section
        title="Productos y servicios"
        text="Buscá por código, SKU o nombre, o agregá un ítem libre."
      >
        <div className="padded-grid stack">
          <input
            className="pos-search"
            autoFocus
            placeholder="Buscar repuesto o servicio…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {results.length > 0 && (
            <div className="pos-results">
              {results.map((r: any) => (
                <button
                  key={r.id}
                  onClick={() =>
                    r.kind === "service"
                      ? add({
                          description: r.name,
                          quantity: 1,
                          unitPrice: r.price,
                          serviceId: r.id,
                        })
                      : add({
                          description: r.name,
                          quantity: 1,
                          unitPrice: 0,
                          itemId: r.id,
                        })
                  }
                >
                  <Detail
                    main={r.name}
                    sub={
                      r.kind === "service"
                        ? `Servicio ${r.code}`
                        : `Repuesto ${r.sku} · stock ${r.quantityOnHand}`
                    }
                  />
                  <span>
                    {r.kind === "service"
                      ? money(r.price, r.currency)
                      : "Precio a definir"}
                  </span>
                </button>
              ))}
            </div>
          )}
          <button
            className="text-link"
            onClick={() =>
              add({ description: search || "Ítem", quantity: 1, unitPrice: 0 })
            }
          >
            <Plus size={14} />
            Agregar ítem libre
          </button>
          {lines.length === 0 ? (
            <div className="premium-empty">
              Agregá productos para empezar la venta.
            </div>
          ) : (
            <div className="pos-lines">
              {lines.map((l) => (
                <div key={l.key}>
                  <input
                    value={l.description}
                    onChange={(e) =>
                      update(l.key, { description: e.target.value })
                    }
                    aria-label="Descripción"
                  />
                  <span className="qty">
                    <button
                      onClick={() =>
                        update(l.key, { quantity: Math.max(1, l.quantity - 1) })
                      }
                      aria-label="Menos"
                    >
                      <Minus size={13} />
                    </button>
                    <b>{l.quantity}</b>
                    <button
                      onClick={() =>
                        update(l.key, { quantity: l.quantity + 1 })
                      }
                      aria-label="Más"
                    >
                      <Plus size={13} />
                    </button>
                  </span>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    value={l.unitPrice}
                    onChange={(e) =>
                      update(l.key, { unitPrice: Number(e.target.value) })
                    }
                    aria-label="Precio unitario"
                  />
                  <b>{money(l.quantity * l.unitPrice, currency)}</b>
                  <button
                    className="icon-button"
                    onClick={() =>
                      setLines((ls) => ls.filter((x) => x.key !== l.key))
                    }
                    aria-label="Quitar"
                  >
                    <Trash2 size={15} />
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      </Section>
      <Section title="Cobro">
        <div className="padded-grid stack">
          <ErrorBox message={error} />
          <Field label="Cliente">
            <select
              value={customerId}
              onChange={(e) => setCustomerId(e.target.value)}
            >
              <option value="">Consumidor final</option>
              {customers.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.fullName} · {c.phone}
                </option>
              ))}
            </select>
          </Field>
          <div className="form-grid">
            <Field label="Moneda">
              <select
                value={currency}
                onChange={(e) => setCurrency(e.target.value)}
              >
                <option value="ARS">ARS</option>
                <option value="USD">USD</option>
              </select>
            </Field>
            <Field label="Medio de pago">
              <select
                value={method}
                onChange={(e) => setMethod(e.target.value)}
              >
                {methodOptions.map((m) => (
                  <option key={m.value} value={m.value}>
                    {m.label}
                  </option>
                ))}
              </select>
            </Field>
          </div>
          <Field label="Descuento">
            <input
              type="number"
              min={0}
              step="0.01"
              value={discount}
              onChange={(e) => setDiscount(Number(e.target.value))}
            />
          </Field>
          <dl className="pos-total">
            <div>
              <dt>Subtotal</dt>
              <dd>{money(subtotal, currency)}</dd>
            </div>
            <div>
              <dt>Descuento</dt>
              <dd>-{money(discount, currency)}</dd>
            </div>
            <div className="grand">
              <dt>Total</dt>
              <dd>{money(subtotal - discount, currency)}</dd>
            </div>
          </dl>
          <button
            className="button primary full"
            disabled={busy || !lines.length}
            onClick={async () => {
              setBusy(true);
              setError("");
              try {
                const r = await request("/api/saas/sales", "POST", {
                  customerId: customerId || null,
                  lines: lines.map(({ key, ...l }) => l),
                  discount,
                  currency,
                  method,
                });
                toast.success("Venta registrada");
                setSold(r);
              } catch (e) {
                setError((e as Error).message);
              } finally {
                setBusy(false);
              }
            }}
          >
            {busy
              ? "Registrando…"
              : `Cobrar ${money(subtotal - discount, currency)}`}
          </button>
        </div>
      </Section>
    </div>
  );
}

function Sales() {
  const { admin, has } = useSession();
  const [rows, setRows] = useState<any[] | null>(null);
  const [error, setError] = useState("");
  const [invoice, setInvoice] = useState<any>(null);
  const [voiding, setVoiding] = useState<any>(null);
  const load = useCallback(
    () =>
      request("/api/saas/sales")
        .then(setRows)
        .catch((e) => setError(e.message)),
    [],
  );
  useEffect(() => {
    load();
  }, [load]);
  if (!rows) return error ? <ErrorBox message={error} /> : <Loading />;
  return (
    <Section title="Ventas de mostrador">
      <Table
        headers={[
          "#",
          "Fecha",
          "Cliente",
          "Detalle",
          "Medio",
          "Total",
          "Estado",
          "",
        ]}
        rows={rows.map((s) => [
          s.number,
          fullDate(s.createdAtUtc),
          s.customerName,
          s.lines
            .map((l: any) => `${l.quantity} × ${l.description}`)
            .join(", "),
          methodNames[s.method] || s.method,
          money(s.total, s.currency),
          s.status === "Voided" ? (
            <Detail main="Anulada" sub={s.voidReason} />
          ) : s.invoiceId ? (
            "Facturada"
          ) : (
            "Cobrada"
          ),
          <span className="row-actions">
            <a
              className="text-link"
              href={`/print/sale/${s.id}`}
              target="_blank"
              rel="noreferrer"
            >
              Ticket
            </a>
            {s.status === "Completed" && !s.invoiceId && has("invoicing") && (
              <button className="text-link" onClick={() => setInvoice(s)}>
                Facturar
              </button>
            )}
            {s.invoiceId && (
              <Link className="text-link" to={`/invoices?open=${s.invoiceId}`}>
                Ver factura
              </Link>
            )}
            {admin && s.status === "Completed" && !s.invoiceId && (
              <button
                className="text-link danger"
                onClick={() => setVoiding(s)}
              >
                Anular
              </button>
            )}
          </span>,
        ])}
        empty="Todavía no hay ventas."
      />
      {invoice && (
        <IssueInvoiceDialog
          sourceType="Sale"
          sourceId={invoice.id}
          defaultName={invoice.customerName}
          onClose={() => {
            setInvoice(null);
            load();
          }}
        />
      )}
      {voiding && (
        <Editor
          spec={{
            title: `Anular venta #${voiding.number}`,
            subtitle:
              "Devuelve el stock y registra el egreso en la caja abierta.",
            fields: [
              { name: "reason", label: "Motivo", required: true, span: true },
            ],
            submit: "Anular venta",
            save: async (b) => {
              await request(`/api/saas/sales/${voiding.id}/void`, "POST", b);
              toast.success("Venta anulada");
              await load();
            },
          }}
          onClose={() => setVoiding(null)}
        />
      )}
    </Section>
  );
}

function Accounts() {
  const { admin } = useSession();
  const [rows, setRows] = useState<any[] | null>(null);
  const [customers, setCustomers] = useState<any[]>([]);
  const [open, setOpen] = useState<string>("");
  const [error, setError] = useState("");
  const [form, setForm] = useState<EditSpec | null>(null);
  const load = useCallback(
    () =>
      request("/api/saas/accounts")
        .then(setRows)
        .catch((e) => setError(e.message)),
    [],
  );
  useEffect(() => {
    load();
    request("/api/v1/customers?take=200")
      .then((d) => setCustomers(Array.isArray(d) ? d : d.items || []))
      .catch(() => {});
  }, [load]);
  if (!rows) return error ? <ErrorBox message={error} /> : <Loading />;
  const debt = rows.filter((r) => r.balance > 0);
  return (
    <div className="settings-stack">
      <Metrics
        items={[
          {
            label: "Clientes con saldo",
            value: debt.length,
            hint: "Deben algo al taller",
          },
          {
            label: "A cobrar en pesos",
            value: money(
              debt
                .filter((r) => r.currency === "ARS")
                .reduce((s, r) => s + r.balance, 0),
            ),
            hint: "Suma de saldos",
          },
          {
            label: "A cobrar en dólares",
            value: money(
              debt
                .filter((r) => r.currency === "USD")
                .reduce((s, r) => s + r.balance, 0),
              "USD",
            ),
            hint: "Suma de saldos",
          },
        ]}
      />
      <Section
        title="Cuentas corrientes"
        text="Ventas y saldos de órdenes a cuenta. Los pagos entran a la caja abierta."
        action={
          admin && (
            <button
              className="button secondary small"
              onClick={() =>
                setForm({
                  title: "Cargar deuda a un cliente",
                  fields: [
                    {
                      name: "customerId",
                      label: "Cliente",
                      type: "select",
                      required: true,
                      options: customers.map((c) => ({
                        value: c.id,
                        label: c.fullName,
                      })),
                    },
                    {
                      name: "amount",
                      label: "Importe",
                      type: "number",
                      required: true,
                    },
                    {
                      name: "currency",
                      label: "Moneda",
                      type: "select",
                      options: currencies,
                    },
                    { name: "description", label: "Detalle", required: true },
                  ],
                  initial: { currency: "ARS" },
                  save: async ({ customerId, ...b }) => {
                    await request(
                      `/api/saas/accounts/${customerId}/charges`,
                      "POST",
                      b,
                    );
                    toast.success("Cargo registrado");
                    await load();
                  },
                })
              }
            >
              Cargar deuda
            </button>
          )
        }
      >
        <Table
          headers={[
            "Cliente",
            "Teléfono",
            "Moneda",
            "Saldo",
            "Último movimiento",
            "",
          ]}
          rows={rows.map((r) => [
            r.fullName,
            r.phone,
            r.currency,
            <b className={r.balance > 0 ? "amber-text" : "green-text"}>
              {money(r.balance, r.currency)}
            </b>,
            fullDate(r.last),
            <span className="row-actions">
              <button
                className="text-link"
                onClick={() => setOpen(r.customerId)}
              >
                Ver cuenta
              </button>
              {r.balance > 0 && (
                <button
                  className="text-link"
                  onClick={() =>
                    setForm({
                      title: `Pago de ${r.fullName}`,
                      subtitle: `Saldo: ${money(r.balance, r.currency)}`,
                      fields: [
                        {
                          name: "amount",
                          label: "Importe",
                          type: "number",
                          required: true,
                        },
                        {
                          name: "method",
                          label: "Medio",
                          type: "select",
                          options: methodOptions.filter(
                            (m) => m.value !== "Account",
                          ),
                        },
                        {
                          name: "description",
                          label: "Detalle",
                          required: false,
                        },
                      ],
                      initial: { amount: r.balance, method: "Cash" },
                      save: async (b) => {
                        await request(
                          `/api/saas/accounts/${r.customerId}/payments`,
                          "POST",
                          { ...b, currency: r.currency },
                        );
                        toast.success("Pago registrado");
                        await load();
                      },
                    })
                  }
                >
                  Registrar pago
                </button>
              )}
            </span>,
          ])}
          empty="No hay cuentas corrientes con movimientos."
        />
      </Section>
      {open && <AccountDialog customerId={open} onClose={() => setOpen("")} />}
      {form && <Editor spec={form} onClose={() => setForm(null)} />}
    </div>
  );
}

function AccountDialog({
  customerId,
  onClose,
}: {
  customerId: string;
  onClose: () => void;
}) {
  const [d, setD] = useState<any>(null);
  useEffect(() => {
    request(`/api/saas/accounts/${customerId}`)
      .then(setD)
      .catch((e) => toast.error(e.message));
  }, [customerId]);
  return (
    <Modal
      title={d ? d.customer.fullName : "Cuenta corriente"}
      subtitle={d?.balances
        .map((b: any) => `${b.currency} ${money(b.balance, b.currency)}`)
        .join(" · ")}
      onClose={onClose}
      wide
    >
      <div className="dialog-body">
        {!d ? (
          <Loading />
        ) : (
          <Table
            headers={["Fecha", "Movimiento", "Detalle", "Importe"]}
            rows={d.entries.map((e: any) => [
              fullDate(e.createdAtUtc),
              e.kind === "Charge" ? "Cargo" : "Pago",
              e.description,
              money(e.kind === "Charge" ? e.amount : -e.amount, e.currency),
            ])}
          />
        )}
      </div>
    </Modal>
  );
}
