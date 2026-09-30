import { useEffect, useRef, type ReactNode } from "react";
import { X, Wrench, LoaderCircle } from "lucide-react";
import { statusNames } from "./api";

export function Brand() {
  return (
    <div className="brand">
      <span className="brandmark">
        <Wrench size={22} />
      </span>
      <span>
        repair<span className="brand-light">shop</span>
        <small>WORKSHOP MANAGEMENT</small>
      </span>
    </div>
  );
}
export function Status({ value }: { value: string }) {
  return (
    <span className={`status s-${value}`}>
      <i />
      {statusNames[value] || value}
    </span>
  );
}
export function Modal({
  title,
  subtitle,
  onClose,
  children,
  wide = false,
}: {
  title: string;
  subtitle?: string;
  onClose: () => void;
  children: ReactNode;
  wide?: boolean;
}) {
  const dialogRef = useRef<HTMLElement>(null);
  const closeRef = useRef(onClose);
  closeRef.current = onClose;
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    const oldOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    if (!dialogRef.current?.contains(document.activeElement))
      dialogRef.current?.focus();
    const keydown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        closeRef.current();
      }
      if (e.key !== "Tab") return;
      const elements = Array.from(
        dialogRef.current?.querySelectorAll<HTMLElement>(
          "button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href]",
        ) || [],
      ).filter((el) => el.getClientRects().length > 0);
      const first = elements[0],
        last = elements[elements.length - 1];
      if (
        e.shiftKey &&
        (document.activeElement === first ||
          document.activeElement === dialogRef.current)
      ) {
        e.preventDefault();
        last?.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first?.focus();
      }
    };
    document.addEventListener("keydown", keydown);
    return () => {
      document.body.style.overflow = oldOverflow;
      document.removeEventListener("keydown", keydown);
      previous?.focus();
    };
  }, []);
  return (
    <div className="modal-scrim" onClick={onClose}>
      <section
        ref={dialogRef}
        tabIndex={-1}
        className={`dialog ${wide ? "wide" : ""}`}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(e) => e.stopPropagation()}
      >
        <header>
          <div>
            <h2>{title}</h2>
            {subtitle && <p>{subtitle}</p>}
          </div>
          <button
            type="button"
            className="icon-button"
            aria-label="Cerrar"
            onClick={onClose}
          >
            <X size={21} />
          </button>
        </header>
        {children}
      </section>
    </div>
  );
}
export function Field({
  label,
  children,
  hint,
}: {
  label: string;
  children: ReactNode;
  hint?: string;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      {hint && <small>{hint}</small>}
    </label>
  );
}
export function Empty({
  title,
  text,
  children,
}: {
  title: string;
  text: string;
  children?: ReactNode;
}) {
  return (
    <div className="empty">
      <Wrench size={30} />
      <h3>{title}</h3>
      <p>{text}</p>
      {children}
    </div>
  );
}
export function Loading() {
  return (
    <div className="loading">
      <LoaderCircle className="spin" size={25} /> Cargando tu taller…
    </div>
  );
}
export function ErrorBox({ message }: { message: string }) {
  return message ? (
    <div className="error-box" role="alert">
      {message}
    </div>
  ) : null;
}
