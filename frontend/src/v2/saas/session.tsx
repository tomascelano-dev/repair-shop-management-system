import { createContext, useContext } from "react";
import { TOKEN_KEY } from "../api";

export type Me = {
  user: { id: string; displayName: string; email: string; role: string };
  profile: Record<string, any>;
  subscription: {
    plan: string;
    status: string;
    trialEndsAtUtc: string;
    periodEndsAtUtc?: string;
    modules: string[];
    readOnly: boolean;
    reason?: string;
  };
  unreadAlerts: number;
  allModules: { id: string; name: string }[];
};

export type Session = {
  me: Me | null;
  reload: () => Promise<void>;
  has: (module: string) => boolean;
  admin: boolean;
};

export const SessionContext = createContext<Session>({
  me: null,
  reload: async () => {},
  has: () => true,
  admin: false,
});
export const useSession = () => useContext(SessionContext);

export const planNames: Record<string, string> = {
  Basic: "Básico",
  Standard: "Estándar",
  Pro: "Profesional",
};
export const subscriptionNames: Record<string, string> = {
  Trialing: "Prueba gratis",
  Active: "Activa",
  PastDue: "Pago pendiente",
  Cancelled: "Cancelada",
};
export const methodNames: Record<string, string> = {
  Cash: "Efectivo",
  Card: "Tarjeta",
  Transfer: "Transferencia",
  MercadoPago: "Mercado Pago",
  Account: "Cuenta corriente",
};
export const methodOptions = Object.entries(methodNames).map(
  ([value, label]) => ({ value, label }),
);

export const daysLeft = (iso?: string) =>
  iso
    ? Math.max(0, Math.ceil((new Date(iso).getTime() - Date.now()) / 86400000))
    : 0;

// Authenticated file download (CSV exports need the bearer token, so a plain link is not enough).
export async function download(path: string, fallbackName: string) {
  const response = await fetch(path, {
    headers: { Authorization: `Bearer ${localStorage.getItem(TOKEN_KEY)}` },
  });
  if (!response.ok) {
    const data = await response.json().catch(() => ({}));
    throw new Error(data.detail || "No se pudo descargar el archivo.");
  }
  const name =
    /filename=([^;]+)/
      .exec(response.headers.get("content-disposition") || "")?.[1]
      ?.replace(/"/g, "") || fallbackName;
  const url = URL.createObjectURL(await response.blob());
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export const readFile = (file: File, as: "text" | "dataUrl") =>
  new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(new Error("No se pudo leer el archivo."));
    if (as === "text") reader.readAsText(file);
    else reader.readAsDataURL(file);
  });

// datetime-local inputs work in the browser's zone; the API stores UTC.
export const toLocalInput = (iso?: string) => {
  const d = iso ? new Date(iso) : new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};
export const fromLocalInput = (value: string) => new Date(value).toISOString();
export const time = (iso: string) =>
  new Date(iso).toLocaleTimeString("es-AR", {
    hour: "2-digit",
    minute: "2-digit",
  });
