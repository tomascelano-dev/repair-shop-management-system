import { useState } from "react";
import { Link } from "react-router-dom";
import {
  usePremium,
  Metrics,
  Section,
  Table,
  Detail,
  AddButton,
  Chip,
  OrderLink,
  options,
  orderOptions,
  currencies,
  money,
  date,
  code,
  total,
} from "./shared";

export function Profitability() {
  const { d, admin, edit, mutate, orderFilter } = usePremium();
  const [currency, setCurrency] = useState("ARS"),
    [closed, setClosed] = useState(false);
  const rows = d.profitability.filter(
    (r) =>
      r.currency === currency &&
      (!orderFilter || r.orderId === orderFilter) &&
      (!closed || r.status === "Delivered"),
  );
  function expense() {
    edit({
      title: "Registrar costo de una reparación",
      subtitle:
        "Para mano de obra, el importe se calcula como minutos / 60 × costo por hora.",
      initial: {
        orderId: orderFilter,
        kind: "Labor",
        minutes: 60,
        hourlyRate: 5000,
        amount: 0,
        currency,
      },
      fields: [
        {
          name: "orderId",
          label: "Orden de trabajo",
          type: "select",
          options: orderOptions(d),
          span: true,
        },
        {
          name: "kind",
          label: "Tipo de costo",
          type: "select",
          options: [
            { value: "Labor", label: "Mano de obra por tiempo" },
            ...(admin
              ? [
                  { value: "Commission", label: "Comisión de cobro" },
                  { value: "Other", label: "Otro gasto" },
                ]
              : []),
          ],
        },
        {
          name: "currency",
          label: "Moneda de la orden",
          type: "select",
          options: currencies,
        },
        {
          name: "description",
          label: "Trabajo o concepto",
          span: true,
          max: 300,
        },
        {
          name: "minutes",
          label: "Minutos trabajados",
          type: "number",
          step: "1",
          min: 0,
          max: 1440,
          hint: "Solo se usa para mano de obra.",
        },
        {
          name: "hourlyRate",
          label: "Costo por hora",
          type: "number",
          hint: "Solo se usa para mano de obra.",
        },
        {
          name: "amount",
          label: "Importe de comisión u otro gasto",
          type: "number",
          hint: "Para mano de obra se calcula automáticamente.",
        },
      ],
      save: (v) => mutate("expenses", v),
    });
  }
  return (
    <>
      <div className="premium-toolbar">
        <select
          aria-label="Moneda del informe"
          value={currency}
          onChange={(e) => setCurrency(e.target.value)}
        >
          {currencies.map((c) => (
            <option key={c.value} value={c.value}>
              {c.label}
            </option>
          ))}
        </select>
        <label className="premium-check">
          <input
            type="checkbox"
            checked={closed}
            onChange={(e) => setClosed(e.target.checked)}
          />
          Solo reparaciones entregadas
        </label>
        <AddButton onClick={expense}>Registrar costo / tiempo</AddButton>
      </div>
      <Metrics
        items={[
          {
            label: `Presupuestado neto · ${currency}`,
            value: money(total(rows, "revenue"), currency),
            hint: "Aprobado menos devoluciones de dinero",
          },
          {
            label: `Costos registrados / estimados · ${currency}`,
            value: money(
              rows.reduce(
                (s, r) => s + r.parts + r.labor + r.other + r.warranty,
                0,
              ),
              currency,
            ),
            hint: "Repuestos, trabajo, gastos y garantías",
          },
          {
            label: `Resultado calculado · ${currency}`,
            value: money(total(rows, "net"), currency),
            hint: "Depende de completar todos los costos",
          },
        ]}
      />
      <Section
        title="Resultado por reparación"
        text="Las órdenes abiertas muestran una proyección. Revisá la fuente del costo antes de interpretar el margen."
      >
        <Table
          headers={[
            "Orden / equipo",
            "Ingreso neto",
            "Repuestos",
            "Mano de obra",
            "Gastos / garantía",
            "Resultado",
            "Margen",
          ]}
          rows={rows.map((r) => [
            <Detail
              main={<OrderLink d={d} id={r.orderId} />}
              sub={
                <>
                  {r.deviceLabel}
                  <br />
                  {r.status === "Delivered"
                    ? "Entregada"
                    : "En curso · proyección"}
                </>
              }
            />,
            <Detail
              main={money(r.revenue, currency)}
              sub={`Cobrado neto: ${money(r.collected, currency)}`}
            />,
            <Detail main={money(r.parts, currency)} sub={r.partsSource} />,
            <Detail
              main={money(r.labor, currency)}
              sub={`${r.minutes} min · ${r.laborSource}`}
            />,
            <Detail
              main={money(r.other + r.warranty, currency)}
              sub={`Garantías: ${money(r.warranty, currency)}`}
            />,
            <Detail
              main={
                <span
                  className={
                    r.net < 0 ? "premium-negative" : "premium-positive"
                  }
                >
                  {money(r.net, currency)}
                </span>
              }
              sub={
                r.hasQuote
                  ? `Precio para margen 30 %: ${money(r.suggestedPrice, currency)}`
                  : "Sin presupuesto aprobado"
              }
            />,
            r.hasQuote ? `${r.margin}%` : "—",
          ])}
        />
      </Section>
      <div className="premium-note">
        Si existen consumos de stock, se usa su costo histórico; en caso
        contrario, el costo estimado del presupuesto. Las horas registradas
        reemplazan el costo manual de mano de obra. Los costos de garantía se
        cargan en Garantías para evitar duplicarlos aquí. No incluye impuestos
        ni gastos generales del negocio.
      </div>
      <Section title="Costos y tiempos registrados">
        <Table
          headers={["Fecha", "Orden", "Tipo", "Concepto", "Tiempo", "Importe"]}
          rows={d.expenses
            .filter(
              (e) =>
                e.currency === currency &&
                (!orderFilter || e.orderId === orderFilter),
            )
            .map((e) => [
              date(e.createdAtUtc),
              <OrderLink d={d} id={e.orderId} />,
              <Chip value={e.kind} />,
              e.description,
              e.kind === "Labor"
                ? `${e.minutes} min × ${money(e.hourlyRate, e.currency)}/h`
                : "—",
              money(e.amount, e.currency),
            ])}
        />
      </Section>
    </>
  );
}

export function Warranties() {
  const { d, admin, edit, mutate, orderFilter } = usePremium();
  const [status, setStatus] = useState("");
  const rows = d.warranties.filter(
    (w) =>
      (!orderFilter || w.orderId === orderFilter) &&
      (!status || w.status === status),
  );
  const failures = Object.entries(
    d.warranties.reduce<Record<string, number>>((a, w) => {
      a[w.failureCode] = (a[w.failureCode] || 0) + 1;
      return a;
    }, {}),
  ).sort((a, b) => b[1] - a[1]);
  function open() {
    edit({
      title: "Registrar devolución en garantía",
      subtitle:
        "Se vincula con la reparación original. La vigencia se calcula desde la entrega.",
      initial: { orderId: orderFilter },
      fields: [
        {
          name: "orderId",
          label: "Reparación entregada",
          type: "select",
          options: orderOptions(d, (o) => o.status === "Delivered"),
          span: true,
        },
        {
          name: "reservationId",
          label: "Repuesto instalado (opcional)",
          required: false,
          type: "select",
          options: options(
            d.reservations.filter((r) => r.status === "Consumed"),
            (r) => {
              const l = d.lots.find((l) => l.id === r.lotId);
              const o = d.orders.find((o) => o.id === r.orderId);
              return `${code(o?.number || 0)} · ${d.items.find((i) => i.id === l?.itemId)?.name} · ${l?.lotCode}`;
            },
          ),
          span: true,
        },
        {
          name: "failureCode",
          label: "Categoría de falla",
          hint: "Ej.: BATERÍA, PANTALLA, CARGA. Agrupa fallas repetidas.",
          max: 80,
        },
        {
          name: "supplier",
          label: "Proveedor si no hay lote",
          required: false,
          max: 120,
        },
        { name: "problem", label: "Problema informado", type: "textarea" },
      ],
      save: (v) =>
        mutate("warranties", { ...v, reservationId: v.reservationId || null }),
    });
  }
  return (
    <>
      <Metrics
        items={[
          {
            label: "Casos abiertos",
            value: d.warranties.filter((w) =>
              ["Open", "SupplierReview"].includes(w.status),
            ).length,
            hint: "Pendientes de resolución",
          },
          {
            label: "Costo neto · ARS",
            value: money(
              d.warranties
                .filter((w) => w.currency === "ARS")
                .reduce((s, w) => s + w.cost - w.recovered, 0),
            ),
            hint: "Costos menos recuperos del proveedor",
          },
          {
            label: "Categoría más recurrente",
            value: failures[0]?.[0] || "Sin casos",
            hint: failures[0]
              ? `${failures[0][1]} casos registrados`
              : "Se calcula al registrar devoluciones",
          },
        ]}
      />
      <Section
        title="Garantías y reclamos"
        text="El historial de la reparación conserva la apertura y cada actualización del caso."
        action={<AddButton onClick={open}>Nueva garantía</AddButton>}
      >
        <div className="premium-toolbar">
          <select
            aria-label="Filtrar garantías"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="">Todos los estados</option>
            {["Open", "SupplierReview", "Resolved", "Rejected"].map((s) => (
              <option value={s} key={s}>
                {
                  {
                    Open: "Abierta",
                    SupplierReview: "En proveedor",
                    Resolved: "Resuelta",
                    Rejected: "Rechazada",
                  }[s]
                }
              </option>
            ))}
          </select>
        </div>
        <Table
          headers={[
            "Orden / falla",
            "Vigencia al ingreso",
            "Proveedor / reclamo",
            "Costo neto",
            "Estado",
            "Gestión",
          ]}
          rows={rows.map((w) => [
            <Detail
              main={<OrderLink d={d} id={w.orderId} />}
              sub={
                <>
                  {w.failureCode}
                  <br />
                  {w.problem}
                </>
              }
            />,
            <Detail
              main={
                w.coveredAtIntake ? "Dentro de garantía" : "Fuera de garantía"
              }
              sub={`Fin de cobertura: ${date(w.warrantyEndsAtUtc)}`}
            />,
            <Detail
              main={w.supplier || "Trabajo del taller"}
              sub={w.supplierClaim || "Sin reclamo al proveedor"}
            />,
            <Detail
              main={money(w.cost - w.recovered, w.currency)}
              sub={`Recuperado: ${money(w.recovered, w.currency)}`}
            />,
            <Chip value={w.status} />,
            admin ? (
              <button
                className="button secondary small"
                onClick={() =>
                  edit({
                    title: "Gestionar garantía",
                    subtitle: `${w.failureCode} · ${w.problem}`,
                    initial: w,
                    fields: [
                      {
                        name: "status",
                        label: "Estado",
                        type: "select",
                        options: [
                          { value: "Open", label: "Abierta" },
                          {
                            value: "SupplierReview",
                            label: "En revisión del proveedor",
                          },
                          { value: "Resolved", label: "Resuelta" },
                          { value: "Rejected", label: "Rechazada" },
                        ],
                      },
                      {
                        name: "supplierClaim",
                        label: "Referencia / seguimiento del proveedor",
                        required: false,
                        max: 500,
                      },
                      {
                        name: "cost",
                        label: `Costo acumulado (${w.currency})`,
                        type: "number",
                      },
                      {
                        name: "recovered",
                        label: `Recuperado del proveedor (${w.currency})`,
                        type: "number",
                      },
                      {
                        name: "resolution",
                        label: "Resolución o seguimiento",
                        type: "textarea",
                        required: false,
                      },
                    ],
                    save: (v) =>
                      mutate(`warranties/${w.id}`, {
                        ...v,
                        version: w.version,
                      }),
                  })
                }
              >
                Gestionar
              </button>
            ) : (
              w.resolution || "—"
            ),
          ])}
        />
      </Section>
      <div className="premium-note">
        Los controles de entrega se completan en el diagnóstico de cada orden.{" "}
        <Link className="premium-link" to="/orders">
          Ver órdenes →
        </Link>{" "}
        Los costos de cada garantía se descuentan automáticamente en
        Rentabilidad.
      </div>
    </>
  );
}
