import axios, { AxiosError } from 'axios'
import type { ApiResponse, ProblemDetails } from './types'
import { authStore } from '../auth/authStore'

// Default to '/api' (works with Vite proxy).
// For production you can set VITE_API_BASE, e.g. 'https://your-api.com/api'
const baseURL = import.meta.env.VITE_API_BASE ?? '/api'

export const api = axios.create({
  baseURL,
  headers: {
    'Content-Type': 'application/json'
  }
})

api.interceptors.request.use((config) => {
  const token = authStore.getToken()
  if (token) {
    config.headers = config.headers ?? {}
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  (res) => res,
  (error: AxiosError) => {
    const status = error.response?.status
    if (status === 401) {
      // Token invalid/expired => logout hard
      authStore.clear()
      if (location.pathname !== '/login') {
        location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

// Helpers that unwrap ApiResponse<T> => T
export async function get<T>(url: string, params?: any): Promise<T> {
  const res = await api.get<ApiResponse<T>>(url, { params })
  return res.data.data
}

export async function post<T>(url: string, body?: any): Promise<T> {
  const res = await api.post<ApiResponse<T>>(url, body)
  return res.data.data
}

export async function put<T>(url: string, body?: any): Promise<T> {
  const res = await api.put<ApiResponse<T>>(url, body)
  return res.data.data
}

export async function del(url: string): Promise<void> {
  await api.delete(url)
}

export function toProblemDetails(err: unknown): ProblemDetails {
  const e = err as AxiosError<any>
  const data = e?.response?.data
  if (data && typeof data === 'object') return data as ProblemDetails
  return {
    title: e?.message ?? 'Request failed',
    status: e?.response?.status
  }
}

export function problemDetailsToText(pd: ProblemDetails): string {
  const lines: string[] = []
  if (pd.title) lines.push(pd.title)
  if (pd.detail) lines.push(pd.detail)
  if (pd.errors) {
    for (const [k, v] of Object.entries(pd.errors)) {
      lines.push(`${k}: ${v.join(', ')}`)
    }
  }
  return lines.filter(Boolean).join('\n') || 'Request failed'
}

// For endpoints that return 204 No Content (e.g. POST /orders/{id}/status)
export async function postNoContent(url: string, body?: any): Promise<void> {
  await api.post(url, body)
}
