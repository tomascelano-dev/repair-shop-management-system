import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  Check,
  Smartphone,
  UserRound,
  ClipboardCheck,
} from "lucide-react";
import { checkNames, request } from "./api";
import { Modal, Field, ErrorBox } from "./ui";

export default function Intake({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: () => void;
}) {
  const nav = useNavigate();
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [customers, setCustomers] = useState<any[]>([]);
  const [devices, setDevices] = useState<any[]>([]);
  const [form, setForm] = useState({
    customerId: "",
    deviceId: "",
    customerName: "",
    phone: "",
    email: "",
    brand: "Apple",
    model: "",
    identifier: "",
    issue: "",
    condition: "",
    accessories: "Sin accesorios",
    priority: "Normal",
    checks: Object.fromEntries(checkNames.map((k) => [k, "untested"])),
  });
  const set = (key: string, value: any) =>
    setForm((f) => ({ ...f, [key]: value }));
  useEffect(() => {
    request("/api/v1/customers?take=200")
      .then((d) => setCustomers(Array.isArray(d) ? d : d.items || []))
      .catch(() => {});
  }, []);
  async function chooseCustomer(id: string) {
    set("customerId", id);
    set("deviceId", "");
    setDevices([]);
    const c = customers.find((x) => x.id === id);
    if (c) {
      set("customerName", c.fullName);
      set("phone", c.phone);
      try {
        const list = await request(`/api/v1/devices/by-customer/${id}`);
        setDevices(Array.isArray(list) ? list : list.items || []);
      } catch {}
    }
  }
  async function submit(e: FormEvent) {
    e.preventDefault();
    setError("");
    if (step < 2) {
      setStep(step + 1);
      return;
    }
    setBusy(true);
    try {
      const result = await request("/api/v2/intake", "POST", {
        ...form,
        customerId: form.customerId || null,
        deviceId: form.deviceId || null,
      });
      onCreated();
      onClose();
      nav(`/orders/${result.id}`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title="Recibir un equipo"
      subtitle="Una nueva reparación, con todo en su lugar."
      onClose={() => !busy && onClose()}
      wide
    >
      <div className="stepper">
        {[
          { t: "Cliente", I: UserRound },
          { t: "Equipo", I: Smartphone },
          { t: "Recepción", I: ClipboardCheck },
        ].map(({ t, I }, i) => (
          <div
            className={step === i ? "active" : step > i ? "complete" : ""}
            key={t}
          >
            <span>{step > i ? <Check size={16} /> : <I size={16} />}</span>
            {t}
          </div>
        ))}
      </div>
      <form onSubmit={submit}>
        <div className="dialog-body">
          <ErrorBox message={error} />
          {step === 0 && (
            <>
              <Field label="Buscar un cliente existente">
                <select
                  value={form.customerId}
                  onChange={(e) => chooseCustomer(e.target.value)}
                >
                  <option value="">Crear un cliente nuevo</option>
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.fullName} · {c.phone}
                    </option>
                  ))}
                </select>
              </Field>
              <div className="form-grid">
                <Field label="Nombre y apellido">
                  <input
                    autoFocus
                    required
                    minLength={3}
                    maxLength={120}
                    value={form.customerName}
                    readOnly={!!form.customerId}
                    onChange={(e) => set("customerName", e.target.value)}
                    placeholder="Ej. Ana González"
                  />
                </Field>
                <Field label="Teléfono">
                  <input
                    required
                    minLength={6}
                    maxLength={40}
                    type="tel"
                    value={form.phone}
                    readOnly={!!form.customerId}
                    onChange={(e) => set("phone", e.target.value)}
                    placeholder="11 5555 1234"
                  />
                </Field>
              </div>
              <Field
                label="Email (opcional)"
                hint="Para enviarle el presupuesto, los avisos de estado y el link de seguimiento."
              >
                <input
                  type="email"
                  maxLength={160}
                  value={form.email}
                  onChange={(e) => set("email", e.target.value)}
                  placeholder="cliente@email.com"
                />
              </Field>
              <div className="info-box">
                El comprobante conservará los datos del cliente y del equipo al
                momento de recibirlo.
              </div>
            </>
          )}
          {step === 1 && (
            <>
              {devices.length > 0 && (
                <Field label="Equipo del cliente">
                  <select
                    value={form.deviceId}
                    onChange={(e) => {
                      set("deviceId", e.target.value);
                      const d = devices.find((d) => d.id === e.target.value);
                      if (d) {
                        set("brand", d.brand);
                        set("model", d.model);
                        set("identifier", d.serialNumber || "");
                      }
                    }}
                  >
                    <option value="">Registrar otro equipo</option>
                    {devices.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.brand} {d.model} · {d.serialNumber || "Sin serial"}
                      </option>
                    ))}
                  </select>
                </Field>
              )}
              <div className="form-grid">
                <Field label="Marca">
                  <input
                    required
                    minLength={2}
                    maxLength={60}
                    readOnly={!!form.deviceId}
                    value={form.brand}
                    onChange={(e) => set("brand", e.target.value)}
                  />
                </Field>
                <Field label="Modelo">
                  <input
                    autoFocus
                    required
                    minLength={2}
                    maxLength={60}
                    readOnly={!!form.deviceId}
                    value={form.model}
                    onChange={(e) => set("model", e.target.value)}
                    placeholder="Ej. iPhone 13"
                  />
                </Field>
                <Field
                  label="IMEI o número de serie"
                  hint="Opcional si no se puede leer."
                >
                  <input
                    maxLength={80}
                    readOnly={!!form.deviceId}
                    value={form.identifier}
                    onChange={(e) => set("identifier", e.target.value)}
                    placeholder="Ingresá o escaneá el identificador"
                  />
                </Field>
                <Field label="Prioridad">
                  <select
                    value={form.priority}
                    onChange={(e) => set("priority", e.target.value)}
                  >
                    <option>Normal</option>
                    <option>Alta</option>
                  </select>
                </Field>
              </div>
              <Field label="Falla informada por el cliente">
                <textarea
                  required
                  minLength={5}
                  maxLength={500}
                  value={form.issue}
                  onChange={(e) => set("issue", e.target.value)}
                  placeholder="¿Qué le pasa al equipo?"
                />
              </Field>
            </>
          )}
          {step === 2 && (
            <>
              <div className="intake-summary">
                <Smartphone size={26} />
                <div>
                  <strong>
                    {form.brand} {form.model}
                  </strong>
                  <p>
                    {form.customerName} · {form.phone}
                  </p>
                </div>
              </div>
              <div className="form-grid">
                <Field label="Estado físico">
                  <textarea
                    maxLength={500}
                    value={form.condition}
                    onChange={(e) => set("condition", e.target.value)}
                    placeholder="Golpes, rayas, pantalla rota…"
                  />
                </Field>
                <Field label="Accesorios recibidos">
                  <textarea
                    maxLength={200}
                    value={form.accessories}
                    onChange={(e) => set("accessories", e.target.value)}
                  />
                </Field>
              </div>
              <h4>Revisión de ingreso</h4>
              <div className="checks">
                {checkNames.map((k) => (
                  <label key={k}>
                    <span>{k}</span>
                    <select
                      value={form.checks[k]}
                      onChange={(e) =>
                        set("checks", { ...form.checks, [k]: e.target.value })
                      }
                    >
                      <option value="untested">Sin probar</option>
                      <option value="ok">Funciona</option>
                      <option value="fail">Presenta falla</option>
                      <option value="na">No aplica</option>
                    </select>
                  </label>
                ))}
              </div>
              <p className="muted text-sm">
                Podés agregar fotos desde la ficha de la orden una vez creada.
              </p>
            </>
          )}
        </div>
        <footer className="dialog-footer">
          <button
            type="button"
            className="button secondary"
            disabled={busy}
            onClick={() => (step ? setStep(step - 1) : onClose())}
          >
            <ArrowLeft size={16} />
            {step ? "Anterior" : "Cancelar"}
          </button>
          <button className="button primary" disabled={busy}>
            {busy
              ? "Guardando…"
              : step === 2
                ? "Crear orden de trabajo"
                : "Continuar"}
            {step === 2 ? <Check size={16} /> : <ArrowRight size={16} />}
          </button>
        </footer>
      </form>
    </Modal>
  );
}
