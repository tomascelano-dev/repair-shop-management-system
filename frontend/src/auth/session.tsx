import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { authApi } from '../api/endpoints'
import { hasSessionHint, onSessionChange, refreshSession, setAccessToken, setSessionHint } from '../api/http'
import type { LoginResponse, Permission, Role, ShopAccess, UserResponse } from '../api/types'

export type SessionState =
  | { status: 'loading' }
  | { status: 'anonymous' }
  | { status: 'authenticated'; user: UserResponse; shops: ShopAccess[]; permissions: Permission[] }

interface SessionContextValue {
  state: SessionState
  user: UserResponse | null
  shops: ShopAccess[]
  role: Role | null
  can: (permission: Permission) => boolean
  login: (email: string, password: string) => Promise<void>
  acceptSession: (session: LoginResponse) => void
  logout: () => Promise<void>
  switchShop: (shopId: string) => Promise<void>
}

const SessionContext = createContext<SessionContextValue | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<SessionState>({ status: 'loading' })
  const queryClient = useQueryClient()

  const apply = useCallback((session: LoginResponse | null) => {
    setSessionHint(!!session)
    if (!session) {
      setAccessToken(null)
      setState({ status: 'anonymous' })
      return
    }
    setAccessToken(session.accessToken)
    setState({ status: 'authenticated', user: session.user, shops: session.shops, permissions: session.permissions })
  }, [])

  // Restore the session from the refresh cookie on load; keep it fresh before the access token expires.
  useEffect(() => {
    onSessionChange((s) => apply(s))
    if (hasSessionHint()) void refreshSession().then((s) => apply(s))
    else apply(null)
  }, [apply])

  useEffect(() => {
    if (state.status !== 'authenticated') return
    const id = window.setInterval(() => void refreshSession(), 10 * 60 * 1000)
    return () => window.clearInterval(id)
  }, [state.status])

  const login = useCallback(
    async (email: string, password: string) => {
      const session = await authApi.login(email, password)
      queryClient.clear()
      apply(session)
    },
    [apply, queryClient]
  )

  const logout = useCallback(async () => {
    await authApi.logout()
    queryClient.clear()
    apply(null)
  }, [apply, queryClient])

  const switchShop = useCallback(
    async (shopId: string) => {
      const session = await authApi.switchShop(shopId)
      queryClient.clear()
      apply(session)
    },
    [apply, queryClient]
  )

  const value = useMemo<SessionContextValue>(() => {
    const auth = state.status === 'authenticated' ? state : null
    return {
      state,
      user: auth?.user ?? null,
      shops: auth?.shops ?? [],
      role: auth?.user.role ?? null,
      can: (p) => !!auth?.permissions.includes(p),
      login,
      acceptSession: (s) => {
        queryClient.clear()
        apply(s)
      },
      logout,
      switchShop,
    }
  }, [state, login, logout, switchShop, apply, queryClient])

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useSession() {
  const ctx = useContext(SessionContext)
  if (!ctx) throw new Error('useSession must be used inside SessionProvider')
  return ctx
}
