import { useState } from "react";
import { Smartphone, Search } from "lucide-react";
import { Modal } from "../ui";
import {
  usePremium,
  Metrics,
  Section,
  Table,
  Detail,
  AddButton,
  Chip,
  options,
  currencies,
  gradeOptions,
  money,
  date,
  type Row,
} from "./shared";

const checks = [
  "Pantalla",
  "Batería",
  "Carga",
  "Cámaras",
  "Audio",
  "Conectividad",
];
export default function Resale() {
  const { d, admin, edit, mutate } = usePremium();
  const [search, setSearch] = useState(""),
    [history, setHistory] = useState("");
  const rows = d.refurbs.filter((r) =>
    `${r.model} ${r.identifier} ${r.seller}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  const costs = (r: Row) =>
    r.purchasePrice +
    d.refurbExpenses
      .filter((e) => e.deviceId === r.id)
      .reduce((s, e) => s + e.amount, 0);
  const selected = d.refurbs.find((r) => r.id === history);
  function acquire() {
    edit({
      title: "Ingresar equipo para reventa",
      subtitle:
        "En un canje, el valor reconocido al cliente se registra como costo de adquisición.",
      initial: {
        acquisition: "Buy",
        grade: "B",
        purchasePrice: 0,
        targetPrice: 0,
        currency: "ARS",
        branchId: d.branches[0]?.id,
      },
      fields: [
        { name: "model", label: "Marca y modelo", max: 120 },
        { name: "identifier", label: "IMEI o número de serie", max: 80 },
        { name: "seller", label: "Vendedor / titular del canje", max: 120 },
        {
          name: "acquisition",
          label: "Tipo de ingreso",
          type: "select",
          options: [
            { value: "Buy", label: "Compra de usado" },
            { value: "TradeIn", label: "Equipo recibido en canje" },
          ],
        },
        {
          name: "grade",
          label: "Grado estético",
          type: "select",
          options: gradeOptions,
        },
        {
          name: "branchId",
          label: "Sucursal",
          type: "select",
          options: options(d.branches, (b) => b.name),
        },
        {
          name: "purchasePrice",
          label: "Precio de compra / valor del canje",
          type: "number",
        },
        {
          name: "targetPrice",
          label: "Precio de venta objetivo",
          type: "number",
        },
        {
          name: "currency",
          label: "Moneda",
          type: "select",
          options: currencies,
        },
        {
          name: "diagnosis",
          label: "Diagnóstico inicial",
          type: "textarea",
          required: false,
        },
      ],
      save: (v) => mutate("refurbs", v),
    });
  }
  function manage(r: Row) {
    const quality = JSON.parse(r.qualityChecksJson || "{}");
    const transitions: Record<string, string[]> = {
      Received: ["Received", "Repairing"],
      Repairing: ["Repairing", "Ready"],
      Ready: ["Ready", "Repairing", "Sold"],
    };
    edit({
      title: "Preparación y venta",
      subtitle: `${r.model} · ${r.identifier}. Inversión registrada: ${money(costs(r), r.currency)}.`,
      initial: {
        ...r,
        quality: checks.filter((c) => quality[c]),
        salePrice: r.targetPrice,
      },
      fields: [
        {
          name: "status",
          label: "Estado del equipo",
          type: "select",
          options: (transitions[r.status] || []).map((s) => ({
            value: s,
            label: (
              {
                Received: "Recibido",
                Repairing: "En reparación",
                Ready: "Listo para vender",
                Sold: "Registrar venta",
              } as Record<string, string>
            )[s],
          })),
        },
        {
          name: "grade",
          label: "Grado estético",
          type: "select",
          options: gradeOptions,
        },
        {
          name: "targetPrice",
          label: `Precio objetivo (${r.currency})`,
          type: "number",
        },
        {
          name: "diagnosis",
          label: "Diagnóstico y trabajo realizado",
          type: "textarea",
        },
        {
          name: "quality",
          label: "Control de calidad",
          type: "checks",
          options: checks.map((value) => ({
            value,
            label: `${value}: verificado`,
          })),
          hint: "Los seis controles son obligatorios para dejarlo listo o venderlo.",
        },
        {
          name: "salePrice",
          label: `Importe de venta (${r.currency})`,
          type: "number",
          hint: "Se usa al elegir Registrar venta.",
        },
        {
          name: "buyer",
          label: "Comprador",
          required: false,
          hint: "Obligatorio al registrar la venta.",
          max: 120,
        },
      ],
      save: (v) =>
        mutate(`refurbs/${r.id}`, {
          ...v,
          version: r.version,
          qualityChecks: Object.fromEntries(
            checks.map((c) => [c, v.quality.includes(c)]),
          ),
          buyer: v.buyer || null,
        }),
    });
  }
  return (
    <>
      <Metrics
        items={[
          {
            label: "Equipos en preparación",
            value: d.refurbs.filter((r) =>
              ["Received", "Repairing"].includes(r.status),
            ).length,
            hint: "Compras y canjes pendientes de acondicionar",
          },
          {
            label: "Listos para vender",
            value: d.refurbs.filter((r) => r.status === "Ready").length,
            hint: "Con control de calidad completo",
          },
          {
            label: "Margen de ventas · ARS",
            value: money(
              d.refurbs
                .filter((r) => r.status === "Sold" && r.currency === "ARS")
                .reduce((s, r) => s + r.salePrice - costs(r), 0),
            ),
            hint: "Ventas menos adquisición y gastos registrados",
          },
        ]}
      />
      <Section
        title="Reacondicionados y canjes"
        text="Cada IMEI o serie conserva su inversión, controles e historial."
        action={
          admin && <AddButton onClick={acquire}>Ingresar equipo</AddButton>
        }
      >
        <div className="premium-toolbar">
          <label className="premium-search">
            <Search size={17} />
            <input
              aria-label="Buscar equipos para reventa"
              placeholder="Modelo, IMEI, serie o vendedor…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
        </div>
        <div className="refurb-grid">
          {rows.map((r) => (
            <article key={r.id} className="refurb-card">
              <div className="refurb-heading">
                <span className="refurb-icon">
                  <Smartphone size={25} />
                </span>
                <Chip value={r.status} />
              </div>
              <h3>{r.model}</h3>
              <p className="refurb-identifier">{r.identifier}</p>
              <div className="refurb-tags">
                <span>Grado {r.grade}</span>
                <span>{r.acquisition === "TradeIn" ? "Canje" : "Compra"}</span>
                <span>{d.branches.find((b) => b.id === r.branchId)?.name}</span>
              </div>
              <p className="refurb-diagnosis">
                {r.diagnosis || "Diagnóstico pendiente"}
              </p>
              <dl>
                <div>
                  <dt>Adquisición</dt>
                  <dd>{money(r.purchasePrice, r.currency)}</dd>
                </div>
                <div>
                  <dt>Inversión total</dt>
                  <dd>{money(costs(r), r.currency)}</dd>
                </div>
                <div>
                  <dt>
                    {r.status === "Sold" ? "Venta realizada" : "Venta objetivo"}
                  </dt>
                  <dd>{money(r.salePrice ?? r.targetPrice, r.currency)}</dd>
                </div>
                <div className="refurb-result">
                  <dt>
                    {r.status === "Sold"
                      ? "Margen realizado"
                      : "Margen previsto"}
                  </dt>
                  <dd>
                    {money(
                      (r.salePrice ?? r.targetPrice) - costs(r),
                      r.currency,
                    )}
                  </dd>
                </div>
              </dl>
              <div className="premium-actions">
                {admin && r.status !== "Sold" && (
                  <>
                    <button
                      className="button primary small"
                      onClick={() => manage(r)}
                    >
                      Gestionar equipo
                    </button>
                    <button
                      className="button secondary small"
                      onClick={() =>
                        edit({
                          title: "Sumar inversión al equipo",
                          subtitle: `${r.model} · Importes en ${r.currency}.`,
                          fields: [
                            {
                              name: "description",
                              label: "Repuesto, trabajo o gasto",
                              max: 300,
                            },
                            { name: "amount", label: "Costo", type: "number" },
                          ],
                          save: (v) =>
                            mutate(`refurbs/${r.id}/expenses`, {
                              ...v,
                              version: r.version,
                            }),
                        })
                      }
                    >
                      Sumar costo
                    </button>
                  </>
                )}
                <button
                  className="premium-link"
                  onClick={() => setHistory(r.id)}
                >
                  Historial
                </button>
              </div>
              {r.buyer && (
                <small>
                  Vendido a {r.buyer} · {date(r.soldAtUtc)}
                </small>
              )}
            </article>
          ))}
        </div>
        {!rows.length && (
          <div className="premium-empty">
            Ingresá un equipo comprado o recibido en canje para comenzar.
          </div>
        )}
      </Section>
      {selected && (
        <Modal
          title="Historial del equipo"
          subtitle={`${selected.model} · ${selected.identifier}`}
          wide
          onClose={() => setHistory("")}
        >
          <div className="dialog-body">
            <Table
              headers={["Fecha", "Movimiento"]}
              rows={d.refurbEvents
                .filter((e) => e.deviceId === selected.id)
                .map((e) => [date(e.createdAtUtc), e.message])}
            />
            <h4>Detalle de inversión</h4>
            <Table
              headers={["Concepto", "Importe"]}
              rows={[
                [
                  <Detail
                    main={
                      selected.acquisition === "TradeIn"
                        ? "Valor del canje"
                        : "Compra del equipo"
                    }
                    sub={selected.seller}
                  />,
                  money(selected.purchasePrice, selected.currency),
                ],
                ...d.refurbExpenses
                  .filter((e) => e.deviceId === selected.id)
                  .map((e) => [
                    e.description,
                    money(e.amount, selected.currency),
                  ]),
              ]}
            />
          </div>
        </Modal>
      )}
    </>
  );
}
