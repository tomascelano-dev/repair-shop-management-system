import { useState } from "react";
import { Upload, Download, Trash2, Search } from "lucide-react";
import { toast } from "sonner";
import { request } from "../api";
import { Modal, Field, ErrorBox } from "../ui";
import {
  usePremium,
  Metrics,
  Section,
  Table,
  Detail,
  AddButton,
  currencies,
  options,
  receiveEditor,
  money,
  date,
  type Row,
} from "./shared";

export default function Purchases() {
  const ctx = usePremium(),
    { d, admin, edit, mutate } = ctx;
  const [search, setSearch] = useState(""),
    [importing, setImporting] = useState(false);
  const filtered = d.offers.filter((p) =>
    `${p.supplier} ${p.sku} ${p.description} ${p.compatibility} ${p.quality}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  const groupKey = (p: Row) =>
    [
      p.compatibility.trim().toLowerCase(),
      p.quality.trim().toLowerCase(),
      p.currency,
    ].join("|");
  const groups = d.offers
    .filter((p) => p.compatibility && p.quality)
    .reduce<Record<string, Row[]>>((groups, p) => {
      (groups[groupKey(p)] ||= []).push(p);
      return groups;
    }, {});
  function addOffer() {
    edit({
      title: "Cargar oferta de proveedor",
      initial: { currency: "ARS", unitCost: 0 },
      fields: [
        { name: "supplier", label: "Proveedor", max: 120 },
        { name: "sku", label: "Código del proveedor", max: 80 },
        { name: "description", label: "Descripción", span: true },
        {
          name: "compatibility",
          label: "Modelo y componente",
          required: false,
          hint: "Ejemplo: iPhone 13 · pantalla. Usá el mismo texto para equivalentes.",
        },
        {
          name: "quality",
          label: "Calidad",
          required: false,
          hint: "Ejemplo: OLED compatible, original o premium.",
        },
        { name: "unitCost", label: "Costo unitario", type: "number" },
        {
          name: "currency",
          label: "Moneda",
          type: "select",
          options: currencies,
        },
      ],
      save: (v) => mutate("prices", { rows: [v], source: "Carga manual" }),
    });
  }
  return (
    <>
      <Metrics
        items={[
          {
            label: "Ofertas disponibles",
            value: d.offers.length,
            hint: "Precios guardados de tus proveedores",
          },
          {
            label: "Proveedores",
            value: new Set(d.offers.map((p) => p.supplier)).size,
            hint: "Listas centralizadas",
          },
          {
            label: "Comparaciones posibles",
            value: Object.values(groups).filter((g) => g && g.length > 1)
              .length,
            hint: "Mismo componente, calidad y moneda",
          },
        ]}
      />
      <Section
        title="Listas de proveedores"
        text="Revisá las equivalencias declaradas antes de comprar. Los precios conservan su moneda."
        action={
          admin && (
            <div className="premium-actions">
              <button
                className="button secondary small"
                onClick={() => setImporting(true)}
              >
                <Upload size={16} />
                Importar lista
              </button>
              <AddButton onClick={addOffer}>Nueva oferta</AddButton>
            </div>
          )
        }
      >
        <div className="premium-toolbar">
          <label className="premium-search">
            <Search size={17} />
            <input
              aria-label="Buscar ofertas"
              placeholder="Proveedor, repuesto, modelo o código…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
        </div>
        <Table
          headers={[
            "Repuesto",
            "Proveedor",
            "Equivalencia",
            "Costo",
            "Actualización",
            "Acciones",
          ]}
          rows={filtered.map((p) => {
            const same = groups[groupKey(p)] || [];
            const best =
              same.length > 1 &&
              p.unitCost === Math.min(...same.map((x) => x.unitCost));
            return [
              <Detail main={p.description} sub={p.sku} />,
              p.supplier,
              <Detail
                main={p.compatibility || "Sin equivalencia"}
                sub={p.quality || "Calidad sin indicar"}
              />,
              <Detail
                main={money(p.unitCost, p.currency)}
                sub={
                  best ? (
                    <span className="premium-best">Menor precio del grupo</span>
                  ) : (
                    p.currency
                  )
                }
              />,
              <Detail main={date(p.updatedAtUtc)} sub={p.source} />,
              admin && (
                <div className="premium-actions">
                  <button
                    className="button secondary small"
                    onClick={() => receiveEditor(ctx, p)}
                  >
                    Recibir compra
                  </button>
                  <button
                    className="premium-link"
                    onClick={() =>
                      edit({
                        title: "Actualizar costo del catálogo",
                        subtitle:
                          "Cambia el costo de referencia. Los lotes ya recibidos conservan su costo histórico.",
                        fields: [
                          {
                            name: "itemId",
                            label: "Repuesto equivalente del catálogo",
                            type: "select",
                            options: options(
                              d.items,
                              (i) => `${i.sku} · ${i.name}`,
                            ),
                          },
                        ],
                        save: (v) =>
                          mutate("prices/apply-cost", { ...v, offerId: p.id }),
                      })
                    }
                  >
                    Aplicar costo
                  </button>
                </div>
              ),
            ];
          })}
        />
      </Section>
      <div className="premium-note">
        La comparación agrupa únicamente ofertas con el mismo modelo/componente,
        calidad y moneda. ARS y USD se muestran por separado; la recepción
        registra el precio confirmado.
      </div>
      {importing && <ImportDialog onClose={() => setImporting(false)} />}
    </>
  );
}

function ImportDialog({ onClose }: { onClose: () => void }) {
  const { mutate } = usePremium();
  const [supplier, setSupplier] = useState(""),
    [currency, setCurrency] = useState("ARS"),
    [file, setFile] = useState<File | null>(null),
    [rows, setRows] = useState<Row[]>([]),
    [warnings, setWarnings] = useState<string[]>([]),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const change = (index: number, key: string, value: any) =>
    setRows((r) =>
      r.map((row, i) => (i === index ? { ...row, [key]: value } : row)),
    );
  function sample() {
    const content =
      "codigo;descripcion;compatibilidad;calidad;costo;moneda\nDIS-IP13;Pantalla iPhone 13 OLED;iPhone 13 · pantalla;OLED compatible;49000;ARS\nBAT-IP11;Batería iPhone 11;iPhone 11 · batería;Premium;17500;ARS\n";
    const url = URL.createObjectURL(
      new Blob(["\uFEFF", content], { type: "text/csv;charset=utf-8" }),
    );
    const a = document.createElement("a");
    a.href = url;
    a.download = "ejemplo-proveedor.csv";
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  async function preview() {
    setBusy(true);
    setError("");
    try {
      if (!file) throw Error("Elegí un archivo.");
      if (file.size > 5_000_000)
        throw Error("El archivo debe pesar hasta 5 MB.");
      const base64 = await new Promise<string>((resolve, reject) => {
        const r = new FileReader();
        r.onload = () => resolve(String(r.result).split(",")[1]);
        r.onerror = () => reject(Error("No se pudo leer el archivo."));
        r.readAsDataURL(file);
      });
      const p = await request("/api/v2/premium/prices/preview", "POST", {
        fileName: file.name,
        base64,
        supplier,
        currency,
      });
      setRows(p.rows);
      setWarnings(p.warnings);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title="Importar lista de precios"
      subtitle="Excel (.xlsx), CSV o PDF con texto · hasta 500 productos y 5 MB."
      wide
      onClose={() => !busy && onClose()}
    >
      <div className="dialog-body">
        <ErrorBox message={error} />
        {!rows.length ? (
          <>
            <div className="form-grid">
              <Field label="Proveedor">
                <input
                  value={supplier}
                  onChange={(e) => setSupplier(e.target.value)}
                  maxLength={120}
                />
              </Field>
              <Field label="Moneda predeterminada">
                <select
                  value={currency}
                  onChange={(e) => setCurrency(e.target.value)}
                >
                  {currencies.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Archivo de la lista">
                <input
                  type="file"
                  accept=".xlsx,.csv,.pdf,.txt"
                  onChange={(e) => setFile(e.target.files?.[0] || null)}
                />
              </Field>
            </div>
            <p className="premium-note">
              Excel/CSV: columnas codigo, descripcion, compatibilidad, calidad,
              costo y moneda. Para Excel se lee la primera hoja. Los PDF
              escaneados no se pueden leer en esta versión.
            </p>
            <button className="button secondary small" onClick={sample}>
              <Download size={16} />
              Descargar ejemplo CSV
            </button>
          </>
        ) : (
          <>
            <div className="premium-note">
              {warnings.map((w) => (
                <p key={w}>{w}</p>
              ))}
              <strong>{rows.length} ofertas por confirmar.</strong> Podés
              corregirlas o quitar filas antes de guardar.
            </div>
            <div className="table-scroll premium-import">
              <table>
                <thead>
                  <tr>
                    {[
                      "Proveedor",
                      "Código",
                      "Descripción",
                      "Compatibilidad",
                      "Calidad",
                      "Costo",
                      "Moneda",
                      "",
                    ].map((v, i) => (
                      <th key={i}>{v}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r, i) => (
                    <tr key={i}>
                      {[
                        "supplier",
                        "sku",
                        "description",
                        "compatibility",
                        "quality",
                        "unitCost",
                        "currency",
                      ].map((k) => (
                        <td key={k}>
                          {k === "currency" ? (
                            <select
                              aria-label={`Moneda fila ${i + 1}`}
                              value={r[k]}
                              onChange={(e) => change(i, k, e.target.value)}
                            >
                              <option>ARS</option>
                              <option>USD</option>
                            </select>
                          ) : (
                            <input
                              aria-label={`${k} fila ${i + 1}`}
                              type={k === "unitCost" ? "number" : "text"}
                              min={0}
                              step="0.01"
                              value={r[k]}
                              onChange={(e) =>
                                change(
                                  i,
                                  k,
                                  k === "unitCost"
                                    ? Number(e.target.value)
                                    : e.target.value,
                                )
                              }
                            />
                          )}
                        </td>
                      ))}
                      <td>
                        <button
                          aria-label={`Quitar fila ${i + 1}`}
                          onClick={() =>
                            setRows(rows.filter((_, n) => n !== i))
                          }
                        >
                          <Trash2 size={16} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
      <div className="dialog-footer">
        <button
          className="button secondary"
          onClick={rows.length ? () => setRows([]) : onClose}
          disabled={busy}
        >
          {rows.length ? "Elegir otro archivo" : "Cancelar"}
        </button>
        <button
          className="button primary"
          disabled={
            busy || supplier.trim().length < 2 || (!file && !rows.length)
          }
          onClick={
            rows.length
              ? async () => {
                  setBusy(true);
                  setError("");
                  try {
                    await mutate("prices", {
                      rows,
                      source: file?.name || "Importación",
                    });
                    toast.success(`${rows.length} ofertas importadas`);
                    onClose();
                  } catch (e) {
                    setError((e as Error).message);
                  } finally {
                    setBusy(false);
                  }
                }
              : preview
          }
        >
          {busy
            ? "Procesando…"
            : rows.length
              ? "Confirmar importación"
              : "Revisar archivo"}
        </button>
      </div>
    </Modal>
  );
}
