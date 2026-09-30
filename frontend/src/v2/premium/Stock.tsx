import { useState } from "react";
import { Link } from "react-router-dom";
import { Search, ArrowRightLeft } from "lucide-react";
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
  isOpen,
  available,
  receiveEditor,
  money,
  date,
  type Row,
} from "./shared";

export default function Stock() {
  const ctx = usePremium(),
    { d, admin, edit, mutate, orderFilter } = ctx;
  const [branch, setBranch] = useState(""),
    [search, setSearch] = useState("");
  const item = (id: string) => d.items.find((i) => i.id === id),
    branchName = (id: string) =>
      d.branches.find((b) => b.id === id)?.name || "—";
  const lots = d.lots.filter(
    (l) =>
      (!branch || branch === l.branchId) &&
      `${item(l.itemId)?.name} ${item(l.itemId)?.sku} ${l.lotCode} ${l.serial || ""}`
        .toLowerCase()
        .includes(search.toLowerCase()),
  );
  const alerts = d.minimums
    .map((m): Row => ({
      ...m,
      free: d.lots
        .filter((l) => l.branchId === m.branchId && l.itemId === m.itemId)
        .reduce((s, l) => s + available(d, l), 0),
    }))
    .filter((m) => m.free < m.minimum && (!branch || m.branchId === branch));
  const reservations = d.reservations.filter(
    (r) => !orderFilter || r.orderId === orderFilter,
  );
  function minimum() {
    edit({
      title: "Definir stock mínimo",
      subtitle:
        "El aviso se calcula sobre unidades disponibles, descontando las reservas.",
      initial: { branchId: branch || d.branches[0]?.id, minimum: 2 },
      fields: [
        {
          name: "itemId",
          label: "Repuesto",
          type: "select",
          options: options(d.items, (i) => `${i.sku} · ${i.name}`),
        },
        {
          name: "branchId",
          label: "Sucursal",
          type: "select",
          options: options(d.branches, (b) => b.name),
        },
        {
          name: "minimum",
          label: "Unidades mínimas",
          type: "number",
          min: 0,
          max: 10000,
          step: "1",
        },
      ],
      save: (v) => mutate("stock/minimum", v),
    });
  }
  return (
    <>
      <Metrics
        items={[
          {
            label: "Unidades físicas",
            value: lots.reduce((s, l) => s + l.quantity, 0),
            hint: "En los lotes del filtro actual",
          },
          {
            label: "Disponibles",
            value: lots.reduce((s, l) => s + available(d, l), 0),
            hint: "Stock físico menos reservas",
          },
          {
            label: "Alertas de reposición",
            value: alerts.length,
            hint: "Repuestos debajo del mínimo",
          },
        ]}
      />
      {!!alerts.length && (
        <div className="premium-alert">
          <strong>Hay repuestos para reponer</strong>
          {alerts.map((a) => (
            <p key={a.id}>
              {item(a.itemId)?.name} · {branchName(a.branchId)}: {a.free}{" "}
              disponibles / mínimo {a.minimum}
            </p>
          ))}
          <Link className="premium-link" to="/premium/purchases">
            Consultar precios de proveedores →
          </Link>
        </div>
      )}
      <Section
        title="Lotes y disponibilidad"
        text="Cada consumo conserva el costo del lote y queda vinculado a la reparación."
        action={
          admin && (
            <div className="premium-actions">
              <button className="button secondary small" onClick={minimum}>
                Configurar mínimo
              </button>
              <AddButton onClick={() => receiveEditor(ctx)}>
                Recibir repuestos
              </AddButton>
            </div>
          )
        }
      >
        <div className="premium-toolbar">
          <label className="premium-search">
            <Search size={17} />
            <input
              aria-label="Buscar stock"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Repuesto, SKU, lote o serie…"
            />
          </label>
          <select
            aria-label="Filtrar sucursal de stock"
            value={branch}
            onChange={(e) => setBranch(e.target.value)}
          >
            <option value="">Todas las sucursales</option>
            {d.branches.map((b) => (
              <option value={b.id} key={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </div>
        <Table
          headers={[
            "Repuesto / lote",
            "Sucursal",
            "Físico",
            "Reservado",
            "Disponible",
            "Costo histórico",
            "Acciones",
          ]}
          rows={lots.map((l) => [
            <Detail
              main={item(l.itemId)?.name || "Repuesto"}
              sub={`${l.lotCode}${l.serial ? ` · Serie ${l.serial}` : ""} · ${l.supplier}`}
            />,
            branchName(l.branchId),
            l.quantity,
            l.quantity - available(d, l),
            <b>{available(d, l)}</b>,
            <Detail main={money(l.unitCost, l.currency)} sub={l.currency} />,
            <div className="premium-actions">
              <button
                className="button secondary small"
                disabled={available(d, l) < 1}
                onClick={() =>
                  edit({
                    title: "Reservar para una reparación",
                    subtitle: `${item(l.itemId)?.name} · ${available(d, l)} unidades disponibles`,
                    initial: { orderId: orderFilter, quantity: 1 },
                    fields: [
                      {
                        name: "orderId",
                        label: "Orden de trabajo",
                        type: "select",
                        options: orderOptions(d, isOpen),
                        span: true,
                      },
                      {
                        name: "quantity",
                        label: "Unidades a reservar",
                        type: "number",
                        min: 1,
                        max: available(d, l),
                        step: "1",
                      },
                    ],
                    save: (v) => mutate("stock/reserve", { ...v, lotId: l.id }),
                  })
                }
              >
                Reservar
              </button>
              {admin && (
                <>
                  <button
                    className="premium-link"
                    disabled={available(d, l) < 1 || d.branches.length < 2}
                    onClick={() =>
                      edit({
                        title: "Transferir entre sucursales",
                        subtitle:
                          "Solo se pueden trasladar unidades disponibles. El costo y el lote se conservan.",
                        initial: { quantity: 1 },
                        fields: [
                          {
                            name: "branchId",
                            label: "Sucursal de destino",
                            type: "select",
                            options: options(
                              d.branches.filter((b) => b.id !== l.branchId),
                              (b) => b.name,
                            ),
                          },
                          {
                            name: "quantity",
                            label: "Cantidad",
                            type: "number",
                            min: 1,
                            max: available(d, l),
                            step: "1",
                          },
                        ],
                        save: (v) =>
                          mutate("stock/transfer", {
                            ...v,
                            lotId: l.id,
                            version: l.version,
                          }),
                      })
                    }
                  >
                    <ArrowRightLeft size={14} />
                    Transferir
                  </button>
                  <button
                    className="premium-link"
                    onClick={() =>
                      edit({
                        title: "Ajustar lote",
                        subtitle:
                          "Usá un número positivo para sumar y negativo para descontar. El motivo queda en el historial.",
                        fields: [
                          {
                            name: "delta",
                            label: "Variación de unidades",
                            type: "number",
                            min: -available(d, l),
                            max: 10000,
                            step: "1",
                          },
                          {
                            name: "reason",
                            label: "Motivo del ajuste",
                            max: 300,
                          },
                        ],
                        save: (v) =>
                          mutate("stock/adjust", {
                            ...v,
                            lotId: l.id,
                            version: l.version,
                          }),
                      })
                    }
                  >
                    Ajustar
                  </button>
                </>
              )}
            </div>,
          ])}
        />
      </Section>
      <Section
        title="Reservas y consumos"
        text={
          orderFilter
            ? "Movimientos de la orden seleccionada."
            : "Reservar aparta el repuesto. Consumir descuenta stock y registra el costo en la orden."
        }
      >
        <Table
          headers={[
            "Orden",
            "Repuesto",
            "Lote / serie",
            "Cantidad",
            "Estado",
            "Acción",
          ]}
          rows={reservations.map((r) => {
            const lot = d.lots.find((l) => l.id === r.lotId);
            return [
              <OrderLink d={d} id={r.orderId} />,
              item(lot?.itemId)?.name,
              <Detail main={lot?.lotCode} sub={lot?.serial || lot?.supplier} />,
              r.quantity,
              <Chip value={r.status} />,
              r.status === "Reserved" && (
                <div className="premium-actions">
                  <button
                    className="button primary small"
                    onClick={() =>
                      edit({
                        title: "Confirmar consumo del repuesto",
                        subtitle: `${r.quantity} × ${item(lot?.itemId)?.name}. Se descontará del stock y se registrará en la reparación.`,
                        fields: [],
                        submit: "Consumir repuesto",
                        save: () =>
                          mutate(`stock/reservations/${r.id}`, {
                            version: r.version,
                            action: "consume",
                          }),
                      })
                    }
                  >
                    Consumir
                  </button>
                  <button
                    className="premium-link"
                    onClick={() =>
                      edit({
                        title: "Liberar reserva",
                        subtitle:
                          "El repuesto volverá a estar disponible para otras reparaciones.",
                        fields: [],
                        submit: "Liberar reserva",
                        save: () =>
                          mutate(`stock/reservations/${r.id}`, {
                            version: r.version,
                            action: "release",
                          }),
                      })
                    }
                  >
                    Liberar
                  </button>
                </div>
              ),
            ];
          })}
        />
      </Section>
      <Section
        title="Historial de stock"
        text="Últimos 300 movimientos del taller."
      >
        <Table
          headers={[
            "Fecha",
            "Movimiento",
            "Repuesto / lote",
            "Cantidad",
            "Motivo",
            "Orden",
          ]}
          rows={d.movements
            .filter((m) => !orderFilter || m.orderId === orderFilter)
            .map((m) => {
              const l = d.lots.find((l) => l.id === m.lotId);
              return [
                date(m.createdAtUtc),
                <Chip value={m.kind} />,
                <Detail main={item(l?.itemId)?.name} sub={l?.lotCode} />,
                m.quantity,
                m.reason,
                m.orderId ? <OrderLink d={d} id={m.orderId} /> : "—",
              ];
            })}
        />
      </Section>
    </>
  );
}
