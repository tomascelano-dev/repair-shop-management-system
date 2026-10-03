import axios, { AxiosError, type AxiosRequestConfig, type AxiosResponse } from 'axios'
import type { ApiResponse, LoginResponse, Page, ProblemDetails } from './types'

// '/api/v1' works with the Vite dev proxy and with a reverse proxy in production.
// Set VITE_API_BASE (e.g. https://api.mitaller.com/api/v1) when the API lives on another origin.
export const API_BASE: string = import.meta.env.VITE_API_BASE ?? '/api/v1'

const REFRESH_HEADER = { 'X-RS-Refresh': '1' }

/**
 * Session tokens. The access token lives only in memory (never in localStorage), so an XSS can't read a
 * long-lived credential. The refresh token is an httpOnly cookie handled by the browser.
 */
let accessToken: string | null = null
let sessionListener: ((session: LoginResponse | null) => void) | null = null

export function setAccessToken(token: string | null) {
  accessToken = token
}

export function getAccessToken() {
  return accessToken
}

export function onSessionChange(listener: (session: LoginResponse | null) => void) {
  sessionListener = listener
}

// Non-sensitive hint that this browser had a session, so anonymous visitors (login page, public tracking
// portal) don't fire a refresh request that can only fail. The credential itself stays in the httpOnly cookie.
const SESSION_HINT = 'rs.session'

export function hasSessionHint(): boolean {
  try {
    return localStorage.getItem(SESSION_HINT) === '1'
  } catch {
    return true
  }
}

// The web server reads the same hint as a cookie: browsers without it get the pre-rendered pricing page at the bare
// domain (visitors, crawlers and Paddle's reviewer), the others get the app.
const SESSION_COOKIE = 'rs_app'

export function setSessionHint(active: boolean) {
  try {
    if (active) localStorage.setItem(SESSION_HINT, '1')
    else localStorage.removeItem(SESSION_HINT)
  } catch {
    // storage blocked: we'll just try the refresh next time
  }
  const secure = location.protocol === 'https:' ? '; Secure' : ''
  document.cookie = active
    ? `${SESSION_COOKIE}=1; Path=/; Max-Age=31536000; SameSite=Lax${secure}`
    : `${SESSION_COOKIE}=; Path=/; Max-Age=0; SameSite=Lax${secure}`
}

function correlationId() {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

export const http = axios.create({ baseURL: API_BASE, withCredentials: true })

http.interceptors.request.use((config) => {
  config.headers = config.headers ?? {}
  if (accessToken && !config.headers.Authorization) config.headers.Authorization = `Bearer ${accessToken}`
  if (!config.headers['X-Correlation-Id']) config.headers['X-Correlation-Id'] = correlationId()
  return config
})

// One refresh at a time: concurrent 401s wait for the same promise.
let refreshing: Promise<LoginResponse | null> | null = null

export function refreshSession(): Promise<LoginResponse | null> {
  if (!refreshing) {
    refreshing = axios
      .post<ApiResponse<LoginResponse>>(`${API_BASE}/auth/refresh`, null, { withCredentials: true, headers: REFRESH_HEADER })
      .then((res) => {
        const session = res.data.data
        accessToken = session.accessToken
        setSessionHint(true)
        sessionListener?.(session)
        return session
      })
      .catch(() => {
        accessToken = null
        setSessionHint(false)
        sessionListener?.(null)
        return null
      })
      .finally(() => {
        setTimeout(() => (refreshing = null), 0)
      })
  }
  return refreshing
}

http.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined
    const url = original?.url ?? ''
    const isAuthCall = url.includes('/auth/login') || url.includes('/auth/refresh')

    if (error.response?.status === 401 && original && !original._retried && !isAuthCall) {
      original._retried = true
      const session = await refreshSession()
      if (session) {
        original.headers = { ...(original.headers ?? {}), Authorization: `Bearer ${session.accessToken}` }
        return http(original)
      }
    }
    return Promise.reject(error)
  }
)

export const authHeaders = REFRESH_HEADER

// ===== Helpers that unwrap ApiResponse<T> =====

export async function get<T>(url: string, params?: object): Promise<T> {
  const res = await http.get<ApiResponse<T>>(url, { params: clean(params) })
  return res.data.data
}

export async function getPage<T>(url: string, params?: object): Promise<Page<T>> {
  const res = await http.get<ApiResponse<T[]>>(url, { params: clean(params) })
  const total = Number(res.headers['x-total-count'] ?? res.data.data.length)
  return { items: res.data.data, total }
}

export async function post<T>(url: string, body?: unknown, options?: { idempotencyKey?: string; headers?: Record<string, string> }): Promise<T> {
  const headers: Record<string, string> = { ...(options?.headers ?? {}) }
  if (options?.idempotencyKey) headers['Idempotency-Key'] = options.idempotencyKey
  const res = await http.post<ApiResponse<T>>(url, body ?? {}, { headers })
  return res.data?.data
}

export async function put<T>(url: string, body?: unknown): Promise<T> {
  const res = await http.put<ApiResponse<T>>(url, body ?? {})
  return res.data?.data
}

export async function del(url: string): Promise<void> {
  await http.delete(url)
}

export async function upload<T>(url: string, file: File, fields?: Record<string, string>, params?: object): Promise<T> {
  const form = new FormData()
  form.append('file', file)
  for (const [k, v] of Object.entries(fields ?? {})) form.append(k, v)
  const res = await http.post<ApiResponse<T>>(url, form, { params: clean(params) })
  return res.data.data
}

/** Downloads a binary (PDF/XLSX) with the bearer token and returns a blob URL. */
export async function fetchBlob(url: string, params?: object): Promise<{ blobUrl: string; fileName?: string; res: AxiosResponse<Blob> }> {
  const res = await http.get<Blob>(url, { responseType: 'blob', params: clean(params) })
  const disposition = String(res.headers['content-disposition'] ?? '')
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)
  return { blobUrl: URL.createObjectURL(res.data), fileName: match ? decodeURIComponent(match[1]) : undefined, res }
}

export function newIdempotencyKey() {
  return correlationId()
}

function clean(params?: object) {
  if (!params) return undefined
  const out: Record<string, unknown> = {}
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === '') continue
    out[k] = v
  }
  return out
}

// ===== Errors =====

export function toProblem(err: unknown): ProblemDetails {
  const e = err as AxiosError<ProblemDetails>
  const data = e?.response?.data
  if (data && typeof data === 'object' && ('title' in data || 'detail' in data || 'errors' in data)) return { ...data, status: e.response?.status }
  if (e?.response?.status === 429) return { status: 429, title: 'Demasiadas solicitudes', detail: 'Esperá un momento y volvé a intentar.' }
  if (e?.code === 'ERR_NETWORK') return { title: 'Sin conexión', detail: 'No se pudo conectar con el servidor. Revisá tu conexión.' }
  return { title: 'Error inesperado', detail: e?.message, status: e?.response?.status }
}

/** One human-readable sentence for toasts. */
export function errorMessage(err: unknown): string {
  const p = toProblem(err)
  if (p.errors) {
    const first = Object.values(p.errors).flat()[0]
    if (first) return first
  }
  return p.detail || p.title || 'Ocurrió un error.'
}

/** Field errors keyed by camelCase field name (as returned by the API). */
export function fieldErrors(err: unknown): Record<string, string> {
  const p = toProblem(err)
  const out: Record<string, string> = {}
  for (const [k, v] of Object.entries(p.errors ?? {})) if (v?.length) out[k] = v[0]
  return out
}
