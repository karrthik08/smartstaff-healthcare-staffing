import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  login as loginRequest,
  logout as logoutRequest,
  registerAccount,
  restoreSession,
  type AuthUser,
  type RegisterInput,
} from '../api/auth'
import { apiErrorMessage } from '../api/client'
import { setAccessToken } from './session'

type AuthContextValue = {
  user: AuthUser | null
  loading: boolean
  sessionNotice: string | null
  login: (email: string, password: string) => Promise<AuthUser>
  register: (input: RegisterInput) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate()
  const [user, setUser] = useState<AuthUser | null>(null)
  const [loading, setLoading] = useState(true)
  const [sessionNotice, setSessionNotice] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    restoreSession()
      .then((restored) => {
        if (active) setUser(restored)
      })
      .catch(() => {
        setAccessToken(null)
        if (active) setUser(null)
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    const onExpired = () => {
      setSessionNotice('Your session expired. Sign in again.')
      setUser(null)
      navigate('/login', { replace: true })
    }
    window.addEventListener('smartstaff:session-expired', onExpired)
    return () => window.removeEventListener('smartstaff:session-expired', onExpired)
  }, [navigate])

  const value = useMemo<AuthContextValue>(() => ({
    user,
    loading,
    sessionNotice,
    async login(email, password) {
      try {
        const next = await loginRequest(email, password)
        setSessionNotice(null)
        setUser(next)
        return next
      } catch (error) {
        throw new Error(apiErrorMessage(error, 'Sign-in failed.'))
      }
    },
    async register(input) {
      try {
        await registerAccount(input)
      } catch (error) {
        throw new Error(apiErrorMessage(error, 'Registration failed.'))
      }
    },
    async logout() {
      try {
        await logoutRequest()
      } finally {
        setAccessToken(null)
        setUser(null)
      }
    },
  }), [loading, sessionNotice, user])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const value = useContext(AuthContext)
  if (!value) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return value
}
