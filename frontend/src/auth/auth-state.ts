import { createContext } from 'react'
import type { AuthUser, RegisterInput } from '../api/auth'

export type AuthContextValue = {
  user: AuthUser | null
  loading: boolean
  sessionNotice: string | null
  login: (email: string, password: string) => Promise<AuthUser>
  register: (input: RegisterInput) => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
