import { useCallback, useEffect, useState } from "react";
import {
  CheckCircle2,
  Clock3,
  Printer,
  ShieldCheck,
  Wrench,
} from "lucide-react";
import { request, code, money, date, fullDate, quoteNames } from "./api";
import { Brand, Status, Field, ErrorBox, Loading } from "./ui";

export default function Portal() {
  const token = window.location.hash.slice(1);
  const [d, setD] = useState<any>(null);
  const [error, setError] = useState("");
  const [name, setName] = useState("");
  const [agreed, setAgreed] = useState(false);
  const [busy, setBusy] = useState(false);
  const load = useCallback(async () => {
    try {
      setD(await request("/api/v2/portal", "GET", undefined, token));
      setError("");
    } catch (e) {
      setError((e as Error).message);
      setD(null);
    }
  }, [token]);
  useEffect(() => {
    load();
    const t = setInterval(load, 20000);
    return () => clearInterval(t);
  }, [load]);
  const q = d?.quote;
  async function decide(accept: boolean) {
    setBusy(true);
    setError("");
    try {
      await request(
        "/api/v2/portal/decision",
        "POST",
        { quoteId: q.id, accept, name },
        token,
      );
      await load();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="portal-page">
      <header>
        <Brand />
        <span className="soft-label">Portal del cliente</span>
      </header>
      <main>
        <ErrorBox message={error} />
        {!d && !error && <Loading />}
        {d && (
          <>
            <div className="portal-welcome">
              <span className="eyebrow">{d.shopName}</span>
              <h1>Tu equipo, paso a paso.</h1>
              <p>
                Hola, {d.customerName.split(" ")[0]}. Acá podés seguir tu
                reparación.
              </p>
            </div>
            <section className="panel padded">
              <div className="portal-device">
                <span className="feature-icon">
                  <Wrench size={24} />
                </span>
                <div>
                  <small>{code(d.number)}</small>
                  <h2>{d.deviceLabel}</h2>
                </div>
                <Status value={d.status} />
              </div>
              {d.handedOverAtUtc && (
                <p>Equipo retirado el {fullDate(d.handedOverAtUtc)}.</p>
              )}
            </section>
            {q ? (
              <section className="panel padded">
                <div className="panel-heading compact">
                  <div>
                    <span className="eyebrow">
                      PRESUPUESTO · VERSIÓN {q.revision}
                    </span>
                    <h2>Esto necesita tu equipo</h2>
                  </div>
                  <span className={`quote-badge q-${q.status}`}>
                    {quoteNames[q.status]}
                  </span>
                </div>
                <div className="public-quote-lines">
                  {q.lines.map((l: any, i: number) => (
                    <div key={i}>
                      <span>
                        <strong>{l.description}</strong>
                        <small>
                          {l.quantity} × {money(l.unitPrice, q.currency)}
                        </small>
                      </span>
                      <b>{money(l.quantity * l.unitPrice, q.currency)}</b>
                    </div>
                  ))}
                </div>
                <div className="quote-total">
                  <span>Total · {q.currency}</span>
                  <strong>{money(q.total, q.currency)}</strong>
                </div>
                <div className="portal-terms">
                  <p>
                    <Clock3 size={16} />
                    Válido hasta {date(q.expiresAtUtc)}
                  </p>
                  <p>
                    <ShieldCheck size={16} />
                    Garantía del trabajo: {q.warrantyDays} días desde la entrega
                  </p>
                  <p>{q.terms}</p>
                </div>
                {q.status === "Sent" &&
                new Date(q.expiresAtUtc) > new Date() &&
                !["Cancelled", "Delivered"].includes(d.status) ? (
                  <form
                    className="portal-approval"
                    onSubmit={(e) => {
                      e.preventDefault();
                      if (agreed) decide(true);
                    }}
                  >
                    <h3>¿Avanzamos con la reparación?</h3>
                    <Field label="Tu nombre y apellido">
                      <input
                        required
                        minLength={3}
                        maxLength={120}
                        value={name}
                        onChange={(e) => setName(e.target.value)}
                        placeholder="Nombre de quien autoriza"
                      />
                    </Field>
                    <label className="checkbox-label">
                      <input
                        type="checkbox"
                        checked={agreed}
                        onChange={(e) => setAgreed(e.target.checked)}
                      />
                      Leí el detalle, el importe y las condiciones del
                      presupuesto.
                    </label>
                    <div className="form-actions">
                      <button
                        type="button"
                        className="button secondary"
                        disabled={busy || name.trim().length < 3}
                        onClick={() => decide(false)}
                      >
                        No aprobar
                      </button>
                      <button
                        className="button primary"
                        disabled={busy || !agreed}
                      >
                        <CheckCircle2 size={17} />
                        {busy ? "Registrando…" : "Aprobar presupuesto"}
                      </button>
                    </div>
                    <small>
                      Tu decisión quedará registrada con nombre, fecha y versión
                      del presupuesto.
                    </small>
                  </form>
                ) : (
                  <div className="info-box">
                    {q.status === "Sent"
                      ? "La propuesta no está disponible para aprobación. Consultá con el taller."
                      : `${quoteNames[q.status]} por ${q.decisionBy || "el cliente"}${q.decidedAtUtc ? " · " + fullDate(q.decidedAtUtc) : ""}.`}
                  </div>
                )}
                {q.status === "Accepted" && (
                  <div className="portal-balance">
                    <span>
                      Pagado: <b>{money(d.paid, q.currency)}</b>
                    </span>
                    <span>
                      Saldo: <b>{money(q.total - d.paid, q.currency)}</b>
                    </span>
                  </div>
                )}
              </section>
            ) : (
              <section className="panel padded">
                <h3>Estamos revisando tu equipo</h3>
                <p>
                  Cuando el taller prepare el presupuesto, lo vas a encontrar
                  acá.
                </p>
              </section>
            )}
            <button
              className="button secondary portal-print"
              onClick={() => window.print()}
            >
              <Printer size={17} />
              Imprimir / guardar PDF
            </button>
          </>
        )}
      </main>
      <footer>RepairShop · Información compartida por tu taller</footer>
    </div>
  );
}
