import { useCallback, useEffect, useState } from "react";
import { Percent, Plus, Upload } from "lucide-react";
import { toast } from "sonner";
import { request, money } from "../api";
import { ErrorBox, Loading, Modal, Field } from "../ui";
import {
  Section,
  Table,
  Metrics,
  Detail,
  Editor,
  type EditSpec,
  type Row,
  currencies,
} from "../premium/shared";
import { useSession, readFile } from "./session";
import { UpgradeNotice } from "./Settings";

export default function Services() {
  const { admin, has } = useSession();
  const [rows, setRows] = useState<any[] | null>(null);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("");
  const [form, setForm] = useState<EditSpec | null>(null);
  const [importing, setImporting] = useState(false);
  const load = useCallback(
    () =>
      request("/api/saas/catalog")
        .then(setRows)
        .catch((e) => setError(e.message)),
    [],
  );
  useEffect(() => {
    if (has("catalog")) load();
  }, [load]);
  if (!has("catalog")) return <UpgradeNotice module="Catálogo de servicios" />;
  if (!rows) return error ? <ErrorBox message={error} /> : <Loading />;
  const categories = [
    ...new Set(rows.map((r) => r.category).filter(Boolean)),
  ].sort();
  const filtered = rows.filter(
    (r) =>
      (!category || r.category === category) &&
      `${r.code} ${r.name}`.toLowerCase().includes(search.toLowerCase()),
  );
  const margin = (r: Row) =>
    r.price > 0 ? Math.round(((r.price - r.estimatedCost) / r.price) * 100) : 0;
  const edit = (r?: Row) =>
    setForm({
      title: r ? `Editar ${r.name}` : "Nuevo servicio",
      fields: [
        { name: "code", label: "Código", required: true },
        { name: "name", label: "Nombre", required: true },
        {
          name: "category",
          label: "Categoría",
          required: false,
          hint: "Ej.: Pantallas, Baterías, Consolas.",
        },
        {
          name: "currency",
          label: "Moneda",
          type: "select",
          options: currencies,
        },
        { name: "price", label: "Precio al cliente", type: "number" },
        { name: "estimatedCost", label: "Costo estimado", type: "number" },
        {
          name: "estimatedMinutes",
          label: "Tiempo estimado (min)",
          type: "number",
          step: "1",
        },
        {
          name: "warrantyDays",
          label: "Garantía (días)",
          type: "number",
          step: "1",
          max: 730,
        },
        { name: "active", label: "Activo", type: "checkbox" },
      ],
      initial: r || {
        currency: "ARS",
        price: 0,
        estimatedCost: 0,
        estimatedMinutes: 60,
        warrantyDays: 90,
        active: true,
      },
      save: async (b) => {
        await request(
          r ? `/api/saas/catalog/${r.id}` : "/api/saas/catalog",
          r ? "PUT" : "POST",
          { ...b, version: r?.version || 0 },
        );
        toast.success("Servicio guardado");
        await load();
      },
    });
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>Servicios y precios</h1>
          <p>
            Tu lista de reparaciones con precio, costo, tiempo y garantía. Se
            usa en presupuestos y en el mostrador.
          </p>
        </div>
        {admin && (
          <div className="heading-actions">
            <button
              className="button secondary small"
              onClick={() => setImporting(true)}
            >
              <Upload size={15} />
              Importar CSV
            </button>
            <button
              className="button secondary small"
              onClick={() =>
                setForm({
                  title: "Actualizar precios en bloque",
                  subtitle:
                    "Aplica un porcentaje a los servicios activos. Ideal para acompañar la inflación o el dólar.",
                  fields: [
                    {
                      name: "category",
                      label: "Categoría",
                      type: "select",
                      required: false,
                      options: categories.map((c) => ({ value: c, label: c })),
                      hint: "Vacío = todas.",
                    },
                    {
                      name: "currency",
                      label: "Moneda",
                      type: "select",
                      options: currencies,
                    },
                    {
                      name: "percent",
                      label: "Ajuste (%)",
                      type: "number",
                      min: -90,
                      max: 500,
                      step: "0.1",
                      hint: "Negativo para bajar.",
                    },
                    {
                      name: "rounding",
                      label: "Redondeo",
                      type: "select",
                      options: [
                        { value: "none", label: "Sin redondeo" },
                        { value: "10", label: "A 10" },
                        { value: "100", label: "A 100" },
                        { value: "1000", label: "A 1.000" },
                      ],
                    },
                  ],
                  initial: { currency: "ARS", rounding: "100", percent: 10 },
                  submit: "Aplicar",
                  save: async (b) => {
                    const r = await request(
                      "/api/saas/catalog/bulk-price",
                      "POST",
                      b,
                    );
                    toast.success(`${r.updated} precios actualizados`);
                    await load();
                  },
                })
              }
            >
              <Percent size={15} />
              Ajustar precios
            </button>
            <button className="button primary small" onClick={() => edit()}>
              <Plus size={16} />
              Nuevo servicio
            </button>
          </div>
        )}
      </div>
      <Metrics
        items={[
          {
            label: "Servicios activos",
            value: rows.filter((r) => r.active).length,
            hint: `${categories.length} categorías`,
          },
          {
            label: "Margen promedio",
            value: `${rows.length ? Math.round(rows.reduce((s, r) => s + margin(r), 0) / rows.length) : 0} %`,
            hint: "Precio contra costo estimado",
          },
          {
            label: "Tiempo promedio",
            value: `${rows.length ? Math.round(rows.reduce((s, r) => s + r.estimatedMinutes, 0) / rows.length) : 0} min`,
            hint: "Por reparación",
          },
        ]}
      />
      <Section
        title="Servicios"
        action={
          <div className="row-actions">
            <input
              className="inline-search"
              placeholder="Buscar…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <select
              className="inline-search"
              value={category}
              onChange={(e) => setCategory(e.target.value)}
            >
              <option value="">Todas las categorías</option>
              {categories.map((c) => (
                <option key={c}>{c}</option>
              ))}
            </select>
          </div>
        }
      >
        <Table
          headers={[
            "Código",
            "Servicio",
            "Precio",
            "Costo",
            "Margen",
            "Tiempo",
            "Garantía",
            "",
          ]}
          rows={filtered.map((r) => [
            <code>{r.code}</code>,
            <Detail
              main={r.name}
              sub={[r.category, r.active ? "" : "Inactivo"]
                .filter(Boolean)
                .join(" · ")}
            />,
            money(r.price, r.currency),
            money(r.estimatedCost, r.currency),
            `${margin(r)} %`,
            `${r.estimatedMinutes} min`,
            `${r.warrantyDays} días`,
            admin ? (
              <button className="text-link" onClick={() => edit(r)}>
                Editar
              </button>
            ) : null,
          ])}
          empty="Cargá tus servicios o importalos desde una planilla."
        />
      </Section>
      {form && <Editor spec={form} onClose={() => setForm(null)} />}
      {importing && (
        <ImportDialog onClose={() => setImporting(false)} onDone={load} />
      )}
    </>
  );
}

function ImportDialog({
  onClose,
  onDone,
}: {
  onClose: () => void;
  onDone: () => void;
}) {
  const [csv, setCsv] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <Modal
      title="Importar servicios"
      subtitle="Columnas: codigo; nombre; categoria; precio; costo; moneda; minutos; garantia. Si el código existe, se actualiza."
      onClose={onClose}
      wide
    >
      <div className="dialog-body">
        <ErrorBox message={error} />
        <Field label="Archivo CSV (separado por ; , o tabulación)">
          <input
            type="file"
            accept=".csv,.txt,text/csv"
            onChange={async (e) =>
              e.target.files?.[0] &&
              setCsv(await readFile(e.target.files[0], "text"))
            }
          />
        </Field>
        <Field label="O pegá las filas desde Excel">
          <textarea
            rows={8}
            value={csv}
            onChange={(e) => setCsv(e.target.value)}
            placeholder={
              "codigo;nombre;categoria;precio;costo;moneda;minutos;garantia\nMOD-IP13;Cambio de módulo iPhone 13;Pantallas;185000;120000;ARS;90;90"
            }
          />
        </Field>
      </div>
      <div className="dialog-footer">
        <button className="button secondary" onClick={onClose}>
          Cancelar
        </button>
        <button
          className="button primary"
          disabled={busy || !csv.trim()}
          onClick={async () => {
            setBusy(true);
            setError("");
            try {
              const r = await request("/api/saas/catalog/import", "POST", {
                csv,
              });
              toast.success(`${r.created} creados y ${r.updated} actualizados`);
              onDone();
              onClose();
            } catch (e) {
              setError((e as Error).message);
            } finally {
              setBusy(false);
            }
          }}
        >
          {busy ? "Importando…" : "Importar"}
        </button>
      </div>
    </Modal>
  );
}
