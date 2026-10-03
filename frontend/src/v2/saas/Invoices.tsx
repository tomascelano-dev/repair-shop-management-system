import { useCallback, useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { Download, Printer } from "lucide-react";
import QRCode from "qrcode";
import { toast } from "sonner";
import { request, money, date } from "../api";
import { Field, ErrorBox, Loading, Modal } from "../ui";
import { Section, Table, Metrics, Detail, Editor } from "../premium/shared";
import { useSession, download } from "./session";
import { UpgradeNotice } from "./Settings";

export const docTypes = [
  { value: 99, label: "Consumidor final sin identificar" },
  { value: 96, label: "DNI" },
  { value: 80, label: "CUIT" },
  { value: 86, label: "CUIL" },
];
export const taxConditions: Record<string, string> = {
  ConsumidorFinal: "Consumidor final",
  ResponsableInscripto: "Responsable inscripto",
  Monotributo: "Monotributo",
  Exento: "Exento",
};
const statusNames: Record<string, string> = {
  Authorized: "Autorizada",
  Simulated: "Interna (sin CAE)",
  Rejected: "Rechazada",
};

export function IssueInvoiceDialog({
  sourceType,
  sourceId,
  defaultName,
  defaults,
  onClose,
}: {
  sourceType: "Order" | "Sale";
  sourceId: string;
  defaultName?: string;
  defaults?: { docType?: number; docNumber?: string; taxCondition?: string };
  onClose: (invoice?: any) => void;
}) {
  const [f, setF] = useState({
    customerName: defaultName || "",
    docType: defaults?.docType || 99,
    docNumber: defaults?.docNumber || "",
    taxCondition: defaults?.taxCondition || "ConsumidorFinal",
  });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <Modal
      title="Emitir factura"
      subtitle="El tipo (A, B o C) se define según tu condición y la del cliente."
      onClose={() => !busy && onClose()}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            const invoice = await request("/api/saas/invoices", "POST", {
              sourceType,
              sourceId,
              ...f,
            });
            if (invoice.status === "Rejected") {
              setError(
                `ARCA rechazó el comprobante: ${invoice.providerMessage}`,
              );
              return;
            }
            toast.success(
              `${invoice.voucherName} ${invoice.formatted} emitida`,
            );
            window.open(`/print/invoice/${invoice.id}`, "_blank");
            onClose(invoice);
          } catch (e) {
            setError((e as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="dialog-body">
          <ErrorBox message={error} />
          <Field label="Nombre o razón social">
            <input
              value={f.customerName}
              onChange={(e) => setF({ ...f, customerName: e.target.value })}
              placeholder="Consumidor final"
            />
          </Field>
          <div className="form-grid">
            <Field label="Documento">
              <select
                value={f.docType}
                onChange={(e) =>
                  setF({ ...f, docType: Number(e.target.value) })
                }
              >
                {docTypes.map((d) => (
                  <option key={d.value} value={d.value}>
                    {d.label}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Número">
              <input
                inputMode="numeric"
                disabled={f.docType === 99}
                value={f.docNumber}
                onChange={(e) => setF({ ...f, docNumber: e.target.value })}
              />
            </Field>
          </div>
          <Field label="Condición frente al IVA del cliente">
            <select
              value={f.taxCondition}
              onChange={(e) => setF({ ...f, taxCondition: e.target.value })}
            >
              {Object.entries(taxConditions).map(([v, l]) => (
                <option key={v} value={v}>
                  {l}
                </option>
              ))}
            </select>
          </Field>
        </div>
        <div className="dialog-footer">
          <button
            type="button"
            className="button secondary"
            onClick={() => onClose()}
          >
            Cancelar
          </button>
          <button className="button primary" disabled={busy}>
            {busy ? "Autorizando…" : "Emitir"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

export default function Invoices() {
  const { admin, has } = useSession();
  const [params] = useSearchParams();
  const [rows, setRows] = useState<any[] | null>(null);
  const [error, setError] = useState("");
  const [credit, setCredit] = useState<any>(null);
  const load = useCallback(
    () =>
      request("/api/saas/invoices")
        .then(setRows)
        .catch((e) => setError(e.message)),
    [],
  );
  useEffect(() => {
    if (has("invoicing")) load();
  }, [load]);
  useEffect(() => {
    const open = params.get("open");
    if (open) window.open(`/print/invoice/${open}`, "_blank");
  }, []);
  if (!has("invoicing")) return <UpgradeNotice module="Facturación ARCA" />;
  if (!rows) return error ? <ErrorBox message={error} /> : <Loading />;
  const month = rows.filter(
    (r) =>
      new Date(r.createdAtUtc).getMonth() === new Date().getMonth() &&
      r.status !== "Rejected",
  );
  const signed = (r: any) =>
    [3, 8, 13].includes(r.voucherType) ? -r.total : r.total;
  const cancelled = new Set(
    rows
      .filter((r) => r.cancelsInvoiceId && r.status !== "Rejected")
      .map((r) => r.cancelsInvoiceId),
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>Facturación</h1>
          <p>
            Facturas y notas de crédito electrónicas con CAE. Emitilas desde una
            orden o una venta.
          </p>
        </div>
        <div className="heading-actions">
          <Link className="button secondary small" to="/settings/facturacion">
            Configurar ARCA
          </Link>
          <button
            className="button secondary small"
            onClick={() =>
              download(
                "/api/saas/reports/export?kind=invoices",
                "facturas.csv",
              ).catch((e) => toast.error(e.message))
            }
          >
            <Download size={15} />
            Exportar CSV
          </button>
        </div>
      </div>
      <Metrics
        items={[
          {
            label: "Emitidas este mes",
            value: month.length,
            hint: "Facturas y notas de crédito",
          },
          {
            label: "Facturado este mes",
            value: money(
              month
                .filter((r) => r.currency === "ARS")
                .reduce((s, r) => s + signed(r), 0),
            ),
            hint: "Neto de notas de crédito, en pesos",
          },
          {
            label: "Con CAE",
            value: rows.filter((r) => r.status === "Authorized").length,
            hint: "Autorizadas por ARCA",
          },
          {
            label: "Internas",
            value: rows.filter((r) => r.status === "Simulated").length,
            hint: "Sin validez fiscal",
          },
        ]}
      />
      <Section title="Comprobantes">
        <Table
          headers={[
            "Fecha",
            "Comprobante",
            "Cliente",
            "Total",
            "CAE",
            "Estado",
            "",
          ]}
          rows={rows.map((r) => [
            date(r.createdAtUtc),
            <Detail main={r.voucherName} sub={r.formatted} />,
            <Detail
              main={r.customerName || "Consumidor final"}
              sub={r.docType === 99 ? "" : r.docNumber}
            />,
            money(signed(r), r.currency),
            r.cae ? (
              <Detail main={r.cae} sub={`Vence ${date(r.caeDueDate)}`} />
            ) : (
              "—"
            ),
            <Detail
              main={statusNames[r.status] || r.status}
              sub={
                cancelled.has(r.id)
                  ? "Anulada con nota de crédito"
                  : r.status === "Rejected"
                    ? r.providerMessage
                    : ""
              }
            />,
            <span className="row-actions">
              {r.status !== "Rejected" && (
                <a
                  className="text-link"
                  href={`/print/invoice/${r.id}`}
                  target="_blank"
                  rel="noreferrer"
                >
                  <Printer size={14} />
                  Ver
                </a>
              )}
              {admin &&
                r.status !== "Rejected" &&
                ![3, 8, 13].includes(r.voucherType) &&
                !cancelled.has(r.id) && (
                  <button
                    className="text-link danger"
                    onClick={() => setCredit(r)}
                  >
                    Nota de crédito
                  </button>
                )}
            </span>,
          ])}
          empty="Todavía no emitiste comprobantes."
        />
      </Section>
      {credit && (
        <Editor
          spec={{
            title: `Anular ${credit.voucherName} ${credit.formatted}`,
            subtitle: "Se emite una nota de crédito por el total.",
            fields: [
              { name: "reason", label: "Motivo", required: true, span: true },
            ],
            submit: "Emitir nota de crédito",
            save: async (b) => {
              const r = await request(
                `/api/saas/invoices/${credit.id}/credit-note`,
                "POST",
                b,
              );
              if (r.status === "Rejected")
                throw new Error(
                  `ARCA rechazó la nota de crédito: ${r.providerMessage}`,
                );
              toast.success(`${r.voucherName} ${r.formatted} emitida`);
              await load();
            },
          }}
          onClose={() => setCredit(null)}
        />
      )}
    </>
  );
}

const conditionNames: Record<string, string> = {
  ...taxConditions,
  ResponsableInscripto: "IVA Responsable inscripto",
  Monotributo: "Responsable monotributo",
  Exento: "IVA exento",
};

export function InvoicePrint() {
  const { id } = useParams();
  const [d, setD] = useState<any>(null);
  const [qr, setQr] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    request(`/api/saas/invoices/${id}`)
      .then(async (r) => {
        setD(r);
        if (r.invoice.qr)
          setQr(
            await QRCode.toDataURL(
              `https://www.afip.gob.ar/fe/qr/?p=${r.invoice.qr}`,
              { margin: 1, width: 180 },
            ),
          );
      })
      .catch((e) => setError(e.message));
  }, [id]);
  if (!d)
    return (
      <div className="print-page">
        {error ? <ErrorBox message={error} /> : <Loading />}
      </div>
    );
  const { invoice: i, shop } = d;
  const letter = i.voucherName.split(" ").pop();
  const showsVat = [1, 3].includes(i.voucherType);
  return (
    <div className="print-page">
      <div className="print-toolbar">
        <button className="button primary small" onClick={() => window.print()}>
          <Printer size={15} />
          Imprimir / PDF
        </button>
      </div>
      <article className="invoice-sheet">
        <header>
          <div>
            {shop.logoDataUrl && <img src={shop.logoDataUrl} alt="" />}
            <h2>{shop.legalName || shop.displayName}</h2>
            <p>
              {shop.address}
              {shop.city ? `, ${shop.city}` : ""}
            </p>
            <p>{conditionNames[shop.taxCondition] || shop.taxCondition}</p>
          </div>
          <div className="invoice-letter">
            <b>{letter}</b>
            <small>Cód. {String(i.voucherType).padStart(2, "0")}</small>
          </div>
          <div>
            <h2>{i.voucherName.replace(/ [ABC]$/, "")}</h2>
            <p>N° {i.formatted}</p>
            <p>Fecha: {date(i.createdAtUtc)}</p>
            <p>CUIT: {shop.taxId || "—"}</p>
          </div>
        </header>
        <section className="invoice-client">
          <p>
            <b>Cliente:</b> {i.customerName || "Consumidor final"}
          </p>
          <p>
            <b>
              {i.docType === 99
                ? "Documento"
                : docTypes.find((t) => t.value === i.docType)?.label ||
                  "Documento"}
              :
            </b>{" "}
            {i.docType === 99 ? "Sin identificar" : i.docNumber}
          </p>
          <p>
            <b>Condición IVA:</b>{" "}
            {taxConditions[i.customerTaxCondition] || i.customerTaxCondition}
          </p>
          {i.currency === "USD" && (
            <p>
              <b>Moneda:</b> Dólares · Cotización {i.exchangeRate}
            </p>
          )}
        </section>
        <table>
          <thead>
            <tr>
              <th>Descripción</th>
              <th>Cant.</th>
              <th>Precio</th>
              <th>Subtotal</th>
            </tr>
          </thead>
          <tbody>
            {i.lines.map((l: any, n: number) => (
              <tr key={n}>
                <td>{l.description}</td>
                <td>{l.quantity}</td>
                <td>{money(l.unitPrice, i.currency)}</td>
                <td>{money(l.quantity * l.unitPrice, i.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <dl className="invoice-totals">
          {showsVat && (
            <>
              <div>
                <dt>Neto gravado</dt>
                <dd>{money(i.net, i.currency)}</dd>
              </div>
              <div>
                <dt>IVA</dt>
                <dd>{money(i.vat, i.currency)}</dd>
              </div>
            </>
          )}
          <div className="grand">
            <dt>Total</dt>
            <dd>{money(i.total, i.currency)}</dd>
          </div>
          {!showsVat && i.vat > 0 && (
            <div className="transparency">
              <dt>IVA contenido (Ley 27.743)</dt>
              <dd>{money(i.vat, i.currency)}</dd>
            </div>
          )}
        </dl>
        <footer>
          {qr && <img src={qr} alt="Código QR de ARCA" />}
          <div>
            {i.status === "Authorized" ? (
              <>
                <p>
                  <b>CAE:</b> {i.cae}
                </p>
                <p>
                  <b>Vencimiento CAE:</b> {date(i.caeDueDate)}
                </p>
                {i.environment === "Homologacion" && (
                  <p className="warn">
                    Comprobante de homologación, sin validez fiscal.
                  </p>
                )}
              </>
            ) : (
              <p className="warn">Comprobante interno sin validez fiscal.</p>
            )}
            {shop.receiptFooter && (
              <p className="pre-line">{shop.receiptFooter}</p>
            )}
          </div>
        </footer>
      </article>
    </div>
  );
}
