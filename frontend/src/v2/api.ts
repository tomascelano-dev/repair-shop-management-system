export const TOKEN_KEY = "repairshop.v2.token";
export const USER_KEY = "repairshop.v2.user";

export async function request<T = any>(
  path: string,
  method = "GET",
  body?: unknown,
  portalToken?: string,
  key = crypto.randomUUID(),
): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    "Idempotency-Key": key,
  };
  const token = localStorage.getItem(TOKEN_KEY);
  if (token && !portalToken) headers.Authorization = `Bearer ${token}`;
  if (portalToken) headers["X-Portal-Token"] = portalToken;
  const options = {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  };
  let response: Response;
  try {
    response = await fetch(path, options);
  } catch {
    const replaySafe =
      method === "GET" ||
      (path.startsWith("/api/v2/") && !path.endsWith("/portal"));
    if (!replaySafe)
      throw new Error(
        "Se interrumpió la conexión. Actualizá los datos para comprobar si se guardó antes de volver a intentarlo.",
      );
    response = await fetch(path, options);
  }
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    if (
      response.status === 401 &&
      !portalToken &&
      !path.includes("/auth/login")
    ) {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
      location.assign("/login");
    }
    throw new Error(
      data.detail ||
        Object.values(data.errors || {})
          .flat()
          .join(" · ") ||
        data.title ||
        "No pudimos completar la operación.",
    );
  }
  return data.data ?? data;
}

export const money = (value: number, currency = "ARS") =>
  new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency,
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(value || 0);
export const date = (value?: string) =>
  value
    ? new Date(value).toLocaleDateString("es-AR", {
        day: "2-digit",
        month: "short",
      })
    : "—";
export const fullDate = (value?: string) =>
  value
    ? new Date(value).toLocaleString("es-AR", {
        dateStyle: "short",
        timeStyle: "short",
      })
    : "—";
export const code = (n: number) => `OT-${String(n).padStart(4, "0")}`;
export const statusNames: Record<string, string> = {
  Received: "Recibido",
  Diagnosing: "En diagnóstico",
  AwaitingApproval: "Por aprobar",
  WaitingParts: "Espera repuesto",
  InProgress: "En reparación",
  QualityCheck: "Control de calidad",
  Ready: "Listo para retirar",
  Delivered: "Entregado",
  Cancelled: "Cancelado",
};
export const statusCodes = [
  "Received",
  "Diagnosing",
  "InProgress",
  "Ready",
  "Delivered",
  "Cancelled",
  "AwaitingApproval",
  "WaitingParts",
  "QualityCheck",
];
export const checkNames = [
  "Pantalla y táctil",
  "Cámaras",
  "Audio y micrófono",
  "Carga",
  "Botones",
  "Biometría",
];
export const quoteNames: Record<string, string> = {
  Sent: "Esperando respuesta",
  Accepted: "Aprobado",
  Rejected: "Rechazado",
  Superseded: "Reemplazado",
};
export interface Order {
  id: string;
  number: number;
  customerName: string;
  customerPhone: string;
  deviceLabel: string;
  identifier: string;
  issueDescription: string;
  status: string;
  priority: string;
  version: number;
  isDemo: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  handedOverAtUtc?: string;
}
export interface BoardData {
  orders: Order[];
  quotes: any[];
  receipts: any[];
  refunds: any[];
}
