import { useEffect, useState } from "react";
import { useParams, useSearchParams } from "react-router-dom";
import { Printer } from "lucide-react";
import QRCode from "qrcode";
import {
  request,
  code,
  money,
  fullDate,
  statusCodes,
  statusNames,
} from "../api";
import { ErrorBox, Loading } from "../ui";
import { markKinds } from "./OrderExtras";
import { methodNames } from "./session";

function Toolbar() {
  const [params, setParams] = useSearchParams();
  const wide = params.get("formato") === "a4";
  return (
    <div className="print-toolbar">
      <button
        className="button secondary small"
        onClick={() => setParams(wide ? {} : { formato: "a4" })}
      >
        {wide ? "Formato ticket 80 mm" : "Formato hoja A4"}
      </button>
      <button className="button primary small" onClick={() => window.print()}>
        <Printer size={15} />
        Imprimir
      </button>
    </div>
  );
}

function ShopHeader({ shop }: { shop: any }) {
  return (
    <header className="ticket-head">
      {shop.logoDataUrl && <img src={shop.logoDataUrl} alt="" />}
      <strong>{shop.displayName}</strong>
      {shop.legalName && (
        <span>
          {shop.legalName}
          {shop.taxId ? ` · CUIT ${shop.taxId}` : ""}
        </span>
      )}
      <span>{[shop.address, shop.city].filter(Boolean).join(", ")}</span>
      <span>{[shop.phone, shop.email].filter(Boolean).join(" · ")}</span>
    </header>
  );
}

export function OrderTicket() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    Promise.all([
      request(`/api/v2/orders/${id}`),
      request(`/api/saas/orders/${id}`),
      request("/api/saas/profile"),
      request(`/api/saas/diagram/${id}`).catch(() => null),
    ])
      .then(async ([order, extras, shop, diagram]) => {
        const qr = await QRCode.toDataURL(extras.trackUrl, {
          margin: 1,
          width: 160,
        });
        setD({ order, extras, shop, diagram, qr });
      })
      .catch((e) => setError(e.message));
  }, [id]);
  if (!d)
    return (
      <div className="print-page">
        {error ? <ErrorBox message={error} /> : <Loading />}
      </div>
    );
  const { order, extras, shop, diagram, qr } = d;
  const w = order.workflow,
    o = order.order,
    q = order.quotes[0];
  const status =
    typeof o.status === "number" ? statusCodes[o.status] : o.status;
  return (
    <div className="print-page">
      <Toolbar />
      <article
        className={`ticket ${params.get("formato") === "a4" ? "a4" : ""}`}
      >
        <ShopHeader shop={shop} />
        <h1>Orden {code(w.number)}</h1>
        <p className="center">
          {fullDate(o.createdAtUtc)} · {statusNames[status]}
        </p>
        <dl>
          <div>
            <dt>Cliente</dt>
            <dd>{w.customerName}</dd>
          </div>
          <div>
            <dt>Teléfono</dt>
            <dd>{w.customerPhone}</dd>
          </div>
          <div>
            <dt>Equipo</dt>
            <dd>{w.deviceLabel}</dd>
          </div>
          <div>
            <dt>IMEI / serie</dt>
            <dd>{w.identifier || "—"}</dd>
          </div>
          <div>
            <dt>Falla</dt>
            <dd>{o.issueDescription}</dd>
          </div>
          <div>
            <dt>Estado</dt>
            <dd>{w.condition || "—"}</dd>
          </div>
          <div>
            <dt>Accesorios</dt>
            <dd>{w.accessories || "Ninguno"}</dd>
          </div>
        </dl>
        {diagram?.marks?.length > 0 && (
          <>
            <h2>Daños al ingresar</h2>
            <ol className="ticket-marks">
              {diagram.marks.map((m: any, i: number) => (
                <li key={i}>
                  {markKinds[m.kind]?.label}
                  {m.note ? `: ${m.note}` : ""}
                </li>
              ))}
            </ol>
          </>
        )}
        {q && (
          <>
            <h2>Presupuesto v{q.revision}</h2>
            {q.lines.map((l: any, i: number) => (
              <p className="ticket-line" key={i}>
                <span>
                  {l.quantity} × {l.description}
                </span>
                <b>{money(l.quantity * l.unitPrice, q.currency)}</b>
              </p>
            ))}
            <p className="ticket-line total">
              <span>Total</span>
              <b>{money(q.total, q.currency)}</b>
            </p>
            <p>Garantía: {q.warrantyDays} días desde la entrega.</p>
          </>
        )}
        <div className="ticket-qr">
          <img src={qr} alt="QR de seguimiento" />
          <p>
            Seguí tu reparación con el código <b>{extras.code}</b>
            <br />
            <small>{extras.trackUrl.split("?")[0]}</small>
          </p>
        </div>
        {shop.receiptFooter && (
          <p className="pre-line small">{shop.receiptFooter}</p>
        )}
        <div className="ticket-sign">
          <span>Firma del cliente</span>
        </div>
        <p className="center small">
          Comprobante interno. No válido como factura.
        </p>
      </article>
    </div>
  );
}

export function SaleTicket() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    Promise.all([
      request("/api/saas/sales?take=500"),
      request("/api/saas/profile"),
    ])
      .then(([sales, shop]) => {
        const sale = sales.find((s: any) => s.id === id);
        if (!sale) throw new Error("Venta no encontrada.");
        setD({ sale, shop });
      })
      .catch((e) => setError(e.message));
  }, [id]);
  if (!d)
    return (
      <div className="print-page">
        {error ? <ErrorBox message={error} /> : <Loading />}
      </div>
    );
  const { sale: s, shop } = d;
  return (
    <div className="print-page">
      <Toolbar />
      <article
        className={`ticket ${params.get("formato") === "a4" ? "a4" : ""}`}
      >
        <ShopHeader shop={shop} />
        <h1>Venta #{s.number}</h1>
        <p className="center">{fullDate(s.createdAtUtc)}</p>
        <p>Cliente: {s.customerName}</p>
        {s.lines.map((l: any, i: number) => (
          <p className="ticket-line" key={i}>
            <span>
              {l.quantity} × {l.description}
            </span>
            <b>{money(l.quantity * l.unitPrice, s.currency)}</b>
          </p>
        ))}
        {s.discount > 0 && (
          <p className="ticket-line">
            <span>Descuento</span>
            <b>-{money(s.discount, s.currency)}</b>
          </p>
        )}
        <p className="ticket-line total">
          <span>Total</span>
          <b>{money(s.total, s.currency)}</b>
        </p>
        <p>Pago: {methodNames[s.method] || s.method}</p>
        {s.status === "Voided" && (
          <p className="center">
            <b>VENTA ANULADA</b>
          </p>
        )}
        {shop.receiptFooter && (
          <p className="pre-line small">{shop.receiptFooter}</p>
        )}
        <p className="center small">
          Comprobante interno. No válido como factura.
        </p>
      </article>
    </div>
  );
}
