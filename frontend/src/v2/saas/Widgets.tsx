import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { ArrowRight, Eraser, Sparkles, X } from "lucide-react";
import { request } from "../api";
import { useSession, planNames, daysLeft } from "./session";

// Finger or mouse signature captured as a PNG data URL.
export function SignaturePad({
  onChange,
}: {
  onChange: (dataUrl: string) => void;
}) {
  const ref = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const [empty, setEmpty] = useState(true);
  useEffect(() => {
    const c = ref.current!;
    const ratio = window.devicePixelRatio || 1;
    c.width = c.offsetWidth * ratio;
    c.height = c.offsetHeight * ratio;
    const ctx = c.getContext("2d")!;
    ctx.scale(ratio, ratio);
    ctx.lineWidth = 2.2;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    ctx.strokeStyle = "#16233a";
  }, []);
  const point = (e: React.PointerEvent) => {
    const r = ref.current!.getBoundingClientRect();
    return [e.clientX - r.left, e.clientY - r.top];
  };
  return (
    <div className="signature-pad">
      <canvas
        ref={ref}
        aria-label="Recuadro para firmar"
        onPointerDown={(e) => {
          drawing.current = true;
          ref.current!.setPointerCapture(e.pointerId);
          const ctx = ref.current!.getContext("2d")!;
          const [x, y] = point(e);
          ctx.beginPath();
          ctx.moveTo(x, y);
        }}
        onPointerMove={(e) => {
          if (!drawing.current) return;
          const ctx = ref.current!.getContext("2d")!;
          const [x, y] = point(e);
          ctx.lineTo(x, y);
          ctx.stroke();
        }}
        onPointerUp={() => {
          drawing.current = false;
          setEmpty(false);
          onChange(ref.current!.toDataURL("image/png"));
        }}
      />
      <div>
        <small>
          {empty ? "Firmá con el dedo o el mouse" : "Firma registrada"}
        </small>
        <button
          type="button"
          className="text-link"
          onClick={() => {
            const c = ref.current!;
            c.getContext("2d")!.clearRect(0, 0, c.width, c.height);
            setEmpty(true);
            onChange("");
          }}
        >
          <Eraser size={14} />
          Borrar
        </button>
      </div>
    </div>
  );
}

const steps = [
  {
    title: "Completá los datos y el logo del taller",
    text: "Salen en tickets, facturas, emails y en tu página de seguimiento.",
    to: "/settings/taller",
  },
  {
    title: "Cargá tus servicios y precios",
    text: "Importalos desde Excel y ajustalos en bloque cuando cambian los costos.",
    to: "/services",
  },
  {
    title: "Sumá a tu equipo",
    text: "Cada técnico con su usuario y su app para cronometrar trabajos.",
    to: "/settings/usuarios",
  },
  {
    title: "Configurá la factura electrónica",
    text: "Subí el certificado de ARCA y emití facturas A, B o C con CAE.",
    to: "/settings/facturacion",
  },
  {
    title: "Abrí la caja y activá los turnos online",
    text: "Cobrá en el mostrador y compartí tu link de turnos.",
    to: "/cash",
  },
];

export function OnboardingCard() {
  const { me, admin, reload } = useSession();
  const [step, setStep] = useState<number>(me?.profile.onboardingStep || 0);
  if (!me || !admin || me.profile.onboardingCompleted) return null;
  const save = async (next: number, completed = false) => {
    setStep(next);
    await request("/api/saas/onboarding", "POST", { step: next, completed });
    if (completed) await reload();
  };
  const current = steps[Math.min(step, steps.length - 1)];
  return (
    <section className="panel onboarding">
      <header>
        <span className="feature-icon">
          <Sparkles size={20} />
        </span>
        <div>
          <strong>
            Primeros pasos · {Math.min(step, steps.length)} de {steps.length}
          </strong>
          <div className="onboarding-bar">
            <i
              style={{
                width: `${(Math.min(step, steps.length) / steps.length) * 100}%`,
              }}
            />
          </div>
        </div>
        <button
          className="icon-button"
          aria-label="Ocultar la guía"
          onClick={() => save(step, true)}
        >
          <X size={17} />
        </button>
      </header>
      <div className="onboarding-step">
        <div>
          <h3>{current.title}</h3>
          <p>{current.text}</p>
        </div>
        <div className="row-actions">
          <Link className="button secondary small" to={current.to}>
            Ir ahora
            <ArrowRight size={15} />
          </Link>
          <button
            className="button primary small"
            onClick={() =>
              step + 1 >= steps.length
                ? save(steps.length, true)
                : save(step + 1)
            }
          >
            {step + 1 >= steps.length ? "Terminar" : "Hecho, siguiente"}
          </button>
        </div>
      </div>
    </section>
  );
}

// Trial countdown or read-only warning at the top of every page.
export function SubscriptionBanner() {
  const { me } = useSession();
  if (!me) return null;
  const s = me.subscription;
  if (s.readOnly)
    return (
      <div className="sub-banner danger">
        <span>{s.reason || "Tu cuenta está en modo solo lectura."}</span>
        <Link to="/settings/plan">Regularizar</Link>
      </div>
    );
  if (s.status === "Trialing") {
    const left = daysLeft(s.trialEndsAtUtc);
    return (
      <div className={`sub-banner ${left <= 3 ? "warn" : ""}`}>
        <span>
          Prueba gratis del plan {planNames[s.plan] || s.plan}:{" "}
          {left === 1 ? "queda 1 día" : `quedan ${left} días`}.
        </span>
        <Link to="/settings/plan">Elegir plan</Link>
      </div>
    );
  }
  if (s.status === "PastDue")
    return (
      <div className="sub-banner warn">
        <span>
          No pudimos cobrar tu abono. Revisá el medio de pago para no perder
          acceso.
        </span>
        <Link to="/settings/plan">Ver pago</Link>
      </div>
    );
  return null;
}
