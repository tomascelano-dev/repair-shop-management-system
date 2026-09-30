import React, { createContext, useContext, useMemo, useState } from 'react'
import { post, toProblemDetails } from '../api/client'
import type { LoginRequest, LoginResponse, ProblemDetails, UserResponse } from '../api/types'
import { authStore } from './authStore'

type AuthState = {
  user: UserResponse | null
  token: string | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<{ ok: true } | { ok: false; error: ProblemDetails }>
  logout: () => void
}

const AuthContext = createContext<AuthState | undefined>(undefined)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserResponse | null>(() => authStore.getUser())
  const [token, setToken] = useState<string | null>(() => authStore.getToken())

  const login = async (email: string, password: string) => {
    try {
      const body: LoginRequest = { email, password }
      const res = await post<LoginResponse>('/auth/login', body)
      authStore.set(res.accessToken, res.user)
      setUser(res.user)
      setToken(res.accessToken)
      return { ok: true as const }
    } catch (err) {
      return { ok: false as const, error: toProblemDetails(err) }
    }
  }

  const logout = () => {
    authStore.clear()
    setUser(null)
    setToken(null)
  }

  const value = useMemo<AuthState>(
    () => ({ user, token, isAuthenticated: !!token, login, logout }),
    [user, token]
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
