import { defineStore } from 'pinia'

export interface AuthUser {
  codusuario: number
  nome: string
  role: string
  permissions?: string[]
}

const TOKEN_KEY = 'mdw_token'
const USER_KEY = 'mdw_user'
const SESSION_MAX_AGE_SECONDS = 30 * 60

function getTokenCookie() {
  return useCookie<string | null>(TOKEN_KEY, {
    maxAge: SESSION_MAX_AGE_SECONDS,
    path: '/',
    sameSite: 'lax'
  })
}

function getUserCookie() {
  return useCookie<AuthUser | null>(USER_KEY, {
    maxAge: SESSION_MAX_AGE_SECONDS,
    path: '/',
    sameSite: 'lax'
  })
}

function readStoredUser(value: string | null): AuthUser | null {
  if (!value) return null
  try {
    return JSON.parse(value) as AuthUser
  } catch {
    return null
  }
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: '' as string,
    user: null as AuthUser | null
  }),
  getters: {
    isAuthenticated: (s) => !!s.token,
    isAdmin: (s) => String(s.user?.role || '').toLowerCase() === 'admin',
    can: (s) => (domain: string, action: string) => {
      if (String(s.user?.role || '').toLowerCase() === 'admin') return true
      const key = `${domain}.${action}`.toLowerCase()
      return (s.user?.permissions || []).some((permission) => permission.toLowerCase() === key)
    }
  },
  actions: {
    loadFromStorage() {
      const tokenCookie = getTokenCookie()
      const userCookie = getUserCookie()
      this.token = tokenCookie.value || ''
      this.user = userCookie.value || null

      if (import.meta.client) {
        const localToken = localStorage.getItem(TOKEN_KEY) || ''
        const localUser = readStoredUser(localStorage.getItem(USER_KEY))

        if (!this.token && localToken) this.token = localToken
        if (!this.user && localUser) this.user = localUser

        this.persistSession()
      }
    },
    setSession(token: string, user: AuthUser) {
      this.token = token
      this.user = user
      this.persistSession()
    },
    persistSession() {
      const tokenCookie = getTokenCookie()
      const userCookie = getUserCookie()
      tokenCookie.value = this.token || null
      userCookie.value = this.user

      if (import.meta.client) {
        if (this.token && this.user) {
          localStorage.setItem(TOKEN_KEY, this.token)
          localStorage.setItem(USER_KEY, JSON.stringify(this.user))
        } else {
          localStorage.removeItem(TOKEN_KEY)
          localStorage.removeItem(USER_KEY)
        }
      }
    },
    logout() {
      this.token = ''
      this.user = null
      this.persistSession()
    }
  }
})
