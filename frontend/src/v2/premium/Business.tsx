import { useState } from "react";
import { Building2, ArrowUpRight } from "lucide-react";
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
  isOpen,
  currencies,
  money,
  date,
} from "./shared";

export default function Business() {
  const { d, admin, edit, mutate, orderFilter } = usePremium();
  const [tab, setTab] = useState("Empresas"),
    [selectedId, setSelectedId] = useState(""),
    [currency, setCurrency] = useState("ARS");
  const selected =
    d.contracts.find((c) => c.id === selectedId) || d.contracts[0];
  const overdue = d.assignments.filter(
    (a) =>
      a.dueAtUtc &&
      new Date(a.dueAtUtc) < new Date() &&
      d.orders.some((o) => o.id === a.orderId && isOpen(o)),
  );
  const companyEquipment = d.equipment.filter(
    (e) => e.contractId === selected?.id,
  );
  const companyOrders = d.assignments.filter(
    (a) => a.contractId === selected?.id,
  );
  function contract() {
    const now = new Date(),
      next = new Date();
    next.setFullYear(next.getFullYear() + 1);
    edit({
      title: "Crear contrato empresarial",
      subtitle:
        "Abono mensual más una tarifa por orden entregada que exceda el cupo. Repuestos y presupuestos se gestionan por separado.",
      initial: {
        currency: "ARS",
        monthlyFee: 0,
        extraOrderRate: 0,
        includedOrders: 5,
        slaHours: 72,
        startsOn: now.toISOString().slice(0, 10),
        endsOn: next.toISOString().slice(0, 10),
      },
      fields: [
        { name: "companyName", label: "Empresa", max: 120 },
        { name: "contact", label: "Persona de contacto", max: 120 },
        {
          name: "phone",
          label: "Teléfono de referencia",
          max: 40,
          hint: "Solo se guarda como dato de contacto.",
        },
        {
          name: "currency",
          label: "Moneda del contrato",
          type: "select",
          options: currencies,
        },
        { name: "monthlyFee", label: "Abono mensual", type: "number" },
        {
          name: "includedOrders",
          label: "Órdenes incluidas por mes",
          type: "number",
          step: "1",
          max: 10000,
        },
        {
          name: "extraOrderRate",
          label: "Tarifa por orden adicional",
          type: "number",
        },
        {
          name: "slaHours",
          label: "Plazo de atención (horas corridas)",
          type: "number",
          step: "1",
          min: 1,
          max: 8760,
        },
        { name: "startsOn", label: "Inicio de vigencia", type: "date" },
        { name: "endsOn", label: "Fin de vigencia", type: "date" },
        {
          name: "terms",
          label: "Condiciones del servicio",
          type: "textarea",
          required: false,
          max: 2000,
        },
      ],
      save: (v) => mutate("contracts", v),
    });
  }
  function equipment() {
    if (!selected) return;
    edit({
      title: `Agregar equipo · ${selected.companyName}`,
      fields: [
        { name: "brand", label: "Marca", max: 60 },
        { name: "model", label: "Modelo", max: 60 },
        {
          name: "identifier",
          label: "Serie / identificador de activo",
          max: 80,
        },
      ],
      save: (v) => mutate("equipment", { ...v, contractId: selected.id }),
    });
  }
  function batch() {
    if (!selected) return;
    const free = companyEquipment.filter(
      (e) =>
        !d.assignments.some(
          (a) =>
            a.equipmentId === e.id &&
            d.orders.some((o) => o.id === a.orderId && isOpen(o)),
        ),
    );
    edit({
      title: "Recibir equipos por lote",
      subtitle:
        "Se crea una orden por equipo, con la empresa, la sucursal y la fecha límite del contrato.",
      initial: {
        branchId: d.branches[0]?.id,
        reference: `LOTE-${new Date().toISOString().slice(0, 10)}`,
      },
      fields: [
        {
          name: "branchId",
          label: "Sucursal de recepción",
          type: "select",
          options: options(d.branches, (b) => b.name),
        },
        { name: "reference", label: "Referencia del lote", max: 100 },
        {
          name: "equipmentIds",
          label: "Equipos a recibir",
          type: "checks",
          options: options(free, (e) => `${e.label} · ${e.identifier}`),
          hint: free.length
            ? "Hasta 30 equipos. Los que ya tienen una orden abierta no aparecen."
            : "No hay equipos disponibles. Agregá equipos o completá sus órdenes abiertas.",
        },
        {
          name: "issue",
          label: "Trabajo solicitado para el lote",
          type: "textarea",
          max: 500,
        },
      ],
      submit: "Crear órdenes del lote",
      save: (v) => mutate("business/intake", { ...v, contractId: selected.id }),
    });
  }
  function settle() {
    const previous = new Date();
    previous.setDate(1);
    previous.setMonth(previous.getMonth() - 1);
    edit({
      title: "Liquidar período mensual",
      subtitle:
        "Se cuentan órdenes entregadas en el mes, usando horario argentino. La liquidación queda fijada y no vuelve a calcularse.",
      initial: {
        contractId: selected?.id,
        period: previous.toISOString().slice(0, 7),
      },
      fields: [
        {
          name: "contractId",
          label: "Contrato",
          type: "select",
          options: options(d.contracts, (c) => c.companyName),
          span: true,
        },
        { name: "period", label: "Mes cerrado", type: "month" },
      ],
      submit: "Generar liquidación",
      save: (v) => mutate("settlements", v),
    });
  }
  return (
    <>
      <Metrics
        items={[
          {
            label: "Contratos activos",
            value: d.contracts.filter(
              (c) =>
                c.status === "Active" &&
                new Date(c.startsAtUtc) <= new Date() &&
                new Date(c.endsAtUtc) > new Date(),
            ).length,
            hint: "Empresas con servicio vigente",
          },
          {
            label: "Equipos de empresas",
            value: d.equipment.length,
            hint: "Inventario de activos de los clientes",
          },
          {
            label: "Órdenes fuera de plazo",
            value: overdue.length,
            hint: "Plazos internos en horas corridas",
          },
        ]}
      />
      <div className="tabs premium-tabs">
        {["Empresas", "Sucursales", "Liquidaciones"].map((t) => (
          <button
            className={tab === t ? "active" : ""}
            key={t}
            onClick={() => setTab(t)}
          >
            {t}
          </button>
        ))}
      </div>
      {tab === "Empresas" && (
        <>
          <Section
            title="Contratos de servicio"
            text="Administrá los equipos y el trabajo recurrente de cada empresa."
            action={
              admin && <AddButton onClick={contract}>Nueva empresa</AddButton>
            }
          >
            <div className="contract-grid">
              {d.contracts.map((c) => (
                <button
                  className={`contract-card ${selected?.id === c.id ? "selected" : ""}`}
                  key={c.id}
                  onClick={() => setSelectedId(c.id)}
                >
                  <div>
                    <Building2 size={21} />
                    <Chip value={c.status} />
                  </div>
                  <h3>{c.companyName}</h3>
                  <p>{money(c.monthlyFee, c.currency)} / mes</p>
                  <small>
                    {c.includedOrders} órdenes incluidas · plazo {c.slaHours} h
                  </small>
                  <span>
                    Ver equipos y actividad <ArrowUpRight size={15} />
                  </span>
                </button>
              ))}
            </div>
            {!d.contracts.length && (
              <div className="premium-empty">
                Creá un contrato para gestionar equipos y recepciones
                empresariales.
              </div>
            )}
          </Section>
          {selected && (
            <>
              <Section
                title={selected.companyName}
                text={`${selected.contact} · ${selected.phone} · ${date(selected.startsAtUtc)} a ${date(new Date(new Date(selected.endsAtUtc).getTime() - 86400000).toISOString())}`}
                action={
                  <div className="premium-actions">
                    {admin && (
                      <button
                        className="button secondary small"
                        onClick={() =>
                          edit({
                            title: "Estado del contrato",
                            initial: selected,
                            fields: [
                              {
                                name: "status",
                                label: "Estado",
                                type: "select",
                                options: [
                                  { value: "Active", label: "Activo" },
                                  { value: "Paused", label: "Pausado" },
                                  { value: "Closed", label: "Cerrado" },
                                ],
                              },
                            ],
                            save: (v) =>
                              mutate(`contracts/${selected.id}/status`, {
                                ...v,
                                version: selected.version,
                              }),
                          })
                        }
                      >
                        Cambiar estado
                      </button>
                    )}
                    <AddButton
                      onClick={batch}
                      disabled={
                        selected.status !== "Active" || !companyEquipment.length
                      }
                    >
                      Recibir lote
                    </AddButton>
                  </div>
                }
              >
                {selected.terms && (
                  <p className="premium-note">{selected.terms}</p>
                )}
                <div className="premium-toolbar">
                  <strong>Equipos registrados</strong>
                  {admin && (
                    <button className="premium-link" onClick={equipment}>
                      + Agregar equipo
                    </button>
                  )}
                </div>
                <Table
                  headers={["Equipo", "Serie / activo", "Situación"]}
                  rows={companyEquipment.map((e) => {
                    const active = d.assignments.find(
                      (a) =>
                        a.equipmentId === e.id &&
                        d.orders.some((o) => o.id === a.orderId && isOpen(o)),
                    );
                    return [
                      e.label,
                      e.identifier,
                      active ? (
                        <OrderLink d={d} id={active.orderId} />
                      ) : (
                        "Disponible para recepción"
                      ),
                    ];
                  })}
                />
              </Section>
              <Section
                title="Órdenes de la empresa"
                text="El ingreso por lote aparece también en el tablero habitual del taller."
              >
                <Table
                  headers={[
                    "Orden",
                    "Equipo",
                    "Lote",
                    "Sucursal",
                    "Fecha límite",
                    "Estado",
                  ]}
                  rows={companyOrders.map((a) => {
                    const o = d.orders.find((o) => o.id === a.orderId);
                    const late = overdue.some((x) => x.id === a.id);
                    return [
                      <OrderLink d={d} id={a.orderId} />,
                      o?.deviceLabel,
                      a.batchReference,
                      d.branches.find((b) => b.id === a.branchId)?.name,
                      <span className={late ? "premium-negative" : ""}>
                        {a.dueAtUtc
                          ? new Date(a.dueAtUtc).toLocaleString("es-AR")
                          : "—"}
                        {late && " · Vencido"}
                      </span>,
                      <Link
                        className="premium-link"
                        to={`/orders/${a.orderId}`}
                      >
                        {isOpen(o || {}) ? "Ver seguimiento" : "Finalizada"}
                      </Link>,
                    ];
                  })}
                />
              </Section>
            </>
          )}
        </>
      )}
      {tab === "Sucursales" && (
        <>
          <Section
            title="Operación por sucursal"
            text="Resultados calculados de las órdenes. Las liquidaciones empresariales se muestran en su pestaña."
            action={
              admin && (
                <AddButton
                  onClick={() =>
                    edit({
                      title: "Crear sucursal",
                      fields: [
                        { name: "name", label: "Nombre", max: 100 },
                        {
                          name: "address",
                          label: "Dirección",
                          required: false,
                        },
                      ],
                      save: (v) => mutate("branches", v),
                    })
                  }
                >
                  Nueva sucursal
                </AddButton>
              )
            }
          >
            <div className="premium-toolbar">
              <select
                aria-label="Moneda del resultado por sucursal"
                value={currency}
                onChange={(e) => setCurrency(e.target.value)}
              >
                {currencies.map((c) => (
                  <option value={c.value} key={c.value}>
                    {c.label}
                  </option>
                ))}
              </select>
              {admin && (
                <button
                  className="button secondary small"
                  onClick={() =>
                    edit({
                      title: "Asignar orden a una sucursal",
                      initial: { orderId: orderFilter },
                      fields: [
                        {
                          name: "orderId",
                          label: "Orden abierta",
                          type: "select",
                          options: orderOptions(d, isOpen),
                          span: true,
                        },
                        {
                          name: "branchId",
                          label: "Sucursal",
                          type: "select",
                          options: options(d.branches, (b) => b.name),
                        },
                      ],
                      save: (v) => mutate("branches/assign", v),
                    })
                  }
                >
                  Asignar orden
                </button>
              )}
            </div>
            <Table
              headers={[
                "Sucursal",
                "Órdenes abiertas",
                "Unidades en stock",
                "Ingreso neto",
                "Resultado calculado",
              ]}
              rows={d.branches.map((b) => {
                const ids = d.assignments
                  .filter((a) => a.branchId === b.id)
                  .map((a) => a.orderId);
                const results = d.profitability.filter(
                  (r) => ids.includes(r.orderId) && r.currency === currency,
                );
                return [
                  <Detail main={b.name} sub={b.address} />,
                  d.orders.filter((o) => ids.includes(o.id) && isOpen(o))
                    .length,
                  d.lots
                    .filter((l) => l.branchId === b.id)
                    .reduce((s, l) => s + l.quantity, 0),
                  money(
                    results.reduce((s, r) => s + r.revenue, 0),
                    currency,
                  ),
                  money(
                    results.reduce((s, r) => s + r.net, 0),
                    currency,
                  ),
                ];
              })}
            />
          </Section>
          <div className="premium-note">
            Las transferencias de repuestos se hacen desde{" "}
            <Link className="premium-link" to="/premium/stock">
              Stock avanzado
            </Link>
            . Las sucursales comparten el taller y sus usuarios; la separación
            entre clientes de RepairShop se mantiene por cuenta.
          </div>
        </>
      )}
      {tab === "Liquidaciones" && (
        <>
          <Section
            title="Liquidación de abonos"
            text="Abono mensual + órdenes adicionales entregadas. No incluye automáticamente repuestos, impuestos ni cobros de las órdenes."
            action={
              admin && <AddButton onClick={settle}>Liquidar mes</AddButton>
            }
          >
            <Table
              headers={[
                "Empresa / período",
                "Órdenes entregadas",
                "Adicionales",
                "Abono",
                "Total",
                "Estado",
                "Acción",
              ]}
              rows={d.settlements.map((s) => [
                <Detail
                  main={
                    d.contracts.find((c) => c.id === s.contractId)?.companyName
                  }
                  sub={s.period}
                />,
                s.completedOrders,
                `${s.extraOrders} × ${money(s.extraRate, s.currency)}`,
                money(s.monthlyFee, s.currency),
                money(s.total, s.currency),
                <Chip value={s.status} />,
                admin && s.status === "Pending" ? (
                  <button
                    className="button secondary small"
                    onClick={() =>
                      edit({
                        title: "Registrar cobro de liquidación",
                        subtitle: `${s.period} · ${money(s.total, s.currency)}. Confirmá cuando hayas recibido el pago completo.`,
                        fields: [],
                        submit: "Marcar como cobrada",
                        save: () =>
                          mutate(`settlements/${s.id}/paid`, {
                            version: s.version,
                          }),
                      })
                    }
                  >
                    Registrar cobro
                  </button>
                ) : (
                  date(s.paidAtUtc)
                ),
              ])}
            />
          </Section>
          <div className="premium-note">
            Se liquidan meses cerrados, sin prorrateo del abono. Los
            presupuestos de reparaciones y estas liquidaciones llevan registros
            separados; definí en el contrato qué servicios incluye el abono. No
            se emiten comprobantes fiscales.
          </div>
        </>
      )}
    </>
  );
}
