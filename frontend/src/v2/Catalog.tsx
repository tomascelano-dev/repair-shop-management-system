import { useCallback, useEffect, useState } from "react";
import { Boxes, Plus, Search, Users, Pencil, History } from "lucide-react";
import { toast } from "sonner";
import { request, money, fullDate, USER_KEY } from "./api";
import { Field, Modal, ErrorBox, Loading, Empty } from "./ui";
export default function Catalog({ type }: { type: "customers" | "inventory" }) {
  const stock = type === "inventory",
    admin = JSON.parse(localStorage.getItem(USER_KEY) || "{}").role === "Admin";
  const [items, setItems] = useState<any[]>([]),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(true),
    [search, setSearch] = useState(""),
    [modal, setModal] = useState<any>(null),
    [history, setHistory] = useState<any>(null);
  const load = useCallback(async () => {
    try {
      const d = await request(`/api/v1/${type}?take=200`);
      setItems(Array.isArray(d) ? d : d.items || []);
      setError("");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }, [type]);
  useEffect(() => {
    setBusy(true);
    setSearch("");
    load();
  }, [load]);
  const filtered = items.filter((i) =>
    `${i.name || i.fullName} ${i.sku || i.phone}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>{stock ? "Inventario" : "Clientes"}</h1>
          <p>
            {stock
              ? "Consultá existencias y registrá movimientos de stock."
              : "Los datos de tus clientes, siempre a mano."}
          </p>
        </div>
        {(!stock || admin) && (
          <button
            className="button primary"
            onClick={() => setModal({ kind: "create" })}
          >
            <Plus size={17} />
            {stock ? "Agregar repuesto" : "Nuevo cliente"}
          </button>
        )}
      </div>
      <ErrorBox message={error} />
      <div className="catalog-summary">
        <span className="feature-icon">
          {stock ? <Boxes size={23} /> : <Users size={23} />}
        </span>
        <div>
          <strong>{items.length}</strong>
          <span>
            {stock ? "repuestos registrados" : "clientes registrados"}
          </span>
        </div>
        {stock && (
          <span className="soft-label warning">
            {items.filter((i) => i.quantityOnHand < 3).length} con menos de 3
            unidades
          </span>
        )}
      </div>
      <div className="toolbar">
        <div className="search-input">
          <Search size={18} />
          <input
            aria-label={stock ? "Buscar repuestos" : "Buscar clientes"}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={
              stock
                ? "Buscar por nombre o SKU…"
                : "Buscar por nombre o teléfono…"
            }
          />
        </div>
      </div>
      <section className="panel">
        {busy ? (
          <Loading />
        ) : !filtered.length ? (
          <Empty
            title="Sin resultados"
            text="Probá otra búsqueda o agregá un registro."
          />
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>{stock ? "REPUESTO / SKU" : "CLIENTE"}</th>
                  <th>{stock ? "STOCK" : "TELÉFONO"}</th>
                  <th>{stock ? "COSTO UNITARIO" : "NOTAS"}</th>
                  <th>ACCIONES</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((i) => (
                  <tr key={i.id}>
                    <td>
                      <strong>{i.name || i.fullName}</strong>
                      {stock && <small>{i.sku}</small>}
                    </td>
                    <td>
                      {stock ? (
                        <span
                          className={`stock-count ${i.quantityOnHand < 3 ? "low" : ""}`}
                        >
                          {i.quantityOnHand} un.
                        </span>
                      ) : (
                        i.phone
                      )}
                    </td>
                    <td>
                      {stock ? (
                        money(i.unitCost || 0, i.unitCostCurrency || "ARS")
                      ) : (
                        <span className="muted">{i.notes || "—"}</span>
                      )}
                    </td>
                    <td>
                      <div className="row-actions">
                        {(!stock || admin) && (
                          <button
                            className="button secondary small"
                            onClick={() =>
                              setModal({
                                kind: stock ? "adjust" : "edit",
                                item: i,
                              })
                            }
                          >
                            {stock ? <Plus size={14} /> : <Pencil size={14} />}
                            {stock ? "Movimiento" : "Editar"}
                          </button>
                        )}
                        {stock && (
                          <button
                            className="icon-button"
                            title="Historial de movimientos"
                            onClick={async () => {
                              try {
                                setHistory({
                                  item: i,
                                  rows: await request(
                                    `/api/v1/inventory/${i.id}/adjustments`,
                                  ),
                                });
                              } catch (e) {
                                toast.error((e as Error).message);
                              }
                            }}
                          >
                            <History size={18} />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      {modal && (
        <Editor
          stock={stock}
          modal={modal}
          onClose={() => setModal(null)}
          onSaved={() => {
            setModal(null);
            load();
          }}
        />
      )}
      {history && (
        <Modal
          title={`Movimientos · ${history.item.sku}`}
          onClose={() => setHistory(null)}
          wide
        >
          <div className="dialog-body">
            {history.rows.length ? (
              <div className="ledger">
                {history.rows.map((r: any) => (
                  <div key={r.id}>
                    <b
                      className={
                        r.deltaQuantity > 0 ? "green-text" : "red-text"
                      }
                    >
                      {r.deltaQuantity > 0 ? "+" : ""}
                      {r.deltaQuantity}
                    </b>
                    <div>
                      <strong>{r.reason}</strong>
                      <small>{fullDate(r.createdAtUtc)}</small>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p className="muted">
                No hay movimientos registrados para este artículo.
              </p>
            )}
          </div>
        </Modal>
      )}
    </>
  );
}
function Editor({
  stock,
  modal,
  onClose,
  onSaved,
}: {
  stock: boolean;
  modal: any;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [f, setF] = useState<any>({
      fullName: "",
      phone: "",
      notes: "",
      sku: "",
      name: "",
      initialQuantity: 0,
      unitCost: 0,
      unitCostCurrency: "ARS",
      isActive: true,
      deltaQuantity: 1,
      reason: "",
      type: 0,
      ...modal.item,
    }),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  const set = (key: string, value: any) => setF({ ...f, [key]: value });
  const adjust = modal.kind === "adjust";
  return (
    <Modal
      title={
        adjust
          ? `Movimiento · ${f.sku}`
          : stock
            ? "Agregar repuesto"
            : modal.kind === "edit"
              ? "Editar cliente"
              : "Nuevo cliente"
      }
      onClose={() => !busy && onClose()}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await request(
              `/api/v1/${stock ? "inventory" : "customers"}${modal.item ? "/" + modal.item.id : ""}${adjust ? "/adjustments" : ""}`,
              modal.kind === "edit" ? "PUT" : "POST",
              stock
                ? adjust
                  ? {
                      type: 0,
                      deltaQuantity: Number(f.deltaQuantity),
                      reason: f.reason,
                    }
                  : {
                      sku: f.sku,
                      name: f.name,
                      initialQuantity: Number(f.initialQuantity),
                      unitCost: Number(f.unitCost),
                      unitCostCurrency: f.unitCostCurrency,
                      isActive: true,
                    }
                : { fullName: f.fullName, phone: f.phone, notes: f.notes },
            );
            toast.success("Registro guardado");
            onSaved();
          } catch (e) {
            setError((e as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="dialog-body">
          <ErrorBox message={error} />
          {adjust ? (
            <>
              <div className="info-box">
                Stock actual: {f.quantityOnHand}. Usá un número positivo para
                ingresar y negativo para descontar.
              </div>
              <Field label="Cantidad a sumar o restar">
                <input
                  required
                  type="number"
                  min={-f.quantityOnHand}
                  value={f.deltaQuantity}
                  onChange={(e) => set("deltaQuantity", e.target.value)}
                />
              </Field>
              <Field label="Motivo del movimiento">
                <textarea
                  required
                  minLength={3}
                  maxLength={300}
                  value={f.reason}
                  onChange={(e) => set("reason", e.target.value)}
                />
              </Field>
            </>
          ) : stock ? (
            <>
              <Field label="SKU">
                <input
                  required
                  minLength={2}
                  maxLength={60}
                  value={f.sku}
                  onChange={(e) => set("sku", e.target.value)}
                />
              </Field>
              <Field label="Nombre del repuesto">
                <input
                  required
                  minLength={2}
                  maxLength={160}
                  value={f.name}
                  onChange={(e) => set("name", e.target.value)}
                />
              </Field>
              <div className="form-grid">
                <Field label="Stock inicial">
                  <input
                    required
                    type="number"
                    min="0"
                    value={f.initialQuantity}
                    onChange={(e) => set("initialQuantity", e.target.value)}
                  />
                </Field>
                <Field label="Costo unitario">
                  <input
                    required
                    type="number"
                    min="0"
                    step="0.01"
                    value={f.unitCost}
                    onChange={(e) => set("unitCost", e.target.value)}
                  />
                </Field>
              </div>
              <Field label="Moneda">
                <select
                  value={f.unitCostCurrency}
                  onChange={(e) => set("unitCostCurrency", e.target.value)}
                >
                  <option>ARS</option>
                  <option>USD</option>
                </select>
              </Field>
            </>
          ) : (
            <>
              <Field label="Nombre y apellido">
                <input
                  required
                  minLength={3}
                  maxLength={120}
                  value={f.fullName}
                  onChange={(e) => set("fullName", e.target.value)}
                />
              </Field>
              <Field label="Teléfono">
                <input
                  required
                  minLength={6}
                  maxLength={40}
                  type="tel"
                  value={f.phone}
                  onChange={(e) => set("phone", e.target.value)}
                />
              </Field>
              <Field label="Notas internas">
                <textarea
                  maxLength={500}
                  value={f.notes || ""}
                  onChange={(e) => set("notes", e.target.value)}
                />
              </Field>
            </>
          )}
        </div>
        <div className="dialog-footer">
          <button type="button" className="button secondary" onClick={onClose}>
            Cancelar
          </button>
          <button
            className="button primary"
            disabled={busy || (adjust && Number(f.deltaQuantity) === 0)}
          >
            {busy ? "Guardando…" : "Guardar"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
