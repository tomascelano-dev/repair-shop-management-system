import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { Toaster } from 'sonner'
import type { AxiosError } from 'axios'
import App from './App'
import { SessionProvider } from './auth/session'
import './styles.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 15_000,
      refetchOnWindowFocus: true,
      // Don't retry what will fail again (validation, permissions, not found).
      retry: (count, error) => {
        const status = (error as AxiosError)?.response?.status
        if (status && status >= 400 && status < 500 && status !== 408 && status !== 429) return false
        return count < 2
      },
    },
    mutations: { retry: false },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <SessionProvider>
          <App />
        </SessionProvider>
      </BrowserRouter>
      <Toaster richColors position="top-right" closeButton />
    </QueryClientProvider>
  </StrictMode>
)

// Offline shell + install prompt (production only, so dev hot reload is never cached).
if (import.meta.env.PROD && 'serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js').catch(() => undefined)
  })
}
