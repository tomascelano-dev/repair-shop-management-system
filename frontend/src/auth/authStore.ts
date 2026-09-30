import type { UserResponse } from '../api/types'

const TOKEN_KEY = 'repairshop.token'
const USER_KEY = 'repairshop.user'

export const authStore = {
  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY)
  },
  set(token: string, user: UserResponse) {
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(USER_KEY, JSON.stringify(user))
  },
  clear() {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  },
  getUser(): UserResponse | null {
    const raw = localStorage.getItem(USER_KEY)
    if (!raw) return null
    try {
      return JSON.parse(raw) as UserResponse
    } catch {
      return null
    }
  }
}
