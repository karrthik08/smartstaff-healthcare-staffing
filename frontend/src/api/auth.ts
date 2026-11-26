import { apiClient, refreshSession } from './client'
import { setAccessToken } from '../auth/session'

export type AuthUser = {
  id: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
  phoneNumber?: string | null
}

type SessionResponse = {
  accessToken: string
  expiresAt: string
  user: AuthUser
}

export type RegisterInput = {
  email: string
  password: string
  firstName: string
  lastName: string
  phoneNumber?: string
}

export async function login(email: string, password: string) {
  const response = await apiClient.post<SessionResponse>('/api/auth/login', { email, password })
  setAccessToken(response.data.accessToken)
  return response.data.user
}

export async function registerAccount(input: RegisterInput) {
  await apiClient.post('/api/auth/register', input)
}

export async function logout() {
  await apiClient.post('/api/auth/logout')
  setAccessToken(null)
}

export async function restoreSession() {
  const session = await refreshSession()
  if (!session) {
    throw new Error('Sign in again.')
  }
  return session.user
}

export async function currentUser() {
  const response = await apiClient.get<AuthUser>('/api/auth/me')
  return response.data
}

export async function forgotPassword(email: string) {
  const response = await apiClient.post<{ message: string }>('/api/auth/forgot-password', { email })
  return response.data.message
}

export async function resetPassword(email: string, token: string, newPassword: string) {
  const response = await apiClient.post<{ message: string }>('/api/auth/reset-password', {
    email,
    token,
    newPassword,
  })
  return response.data.message
}

export async function changePassword(currentPassword: string, newPassword: string) {
  const response = await apiClient.post<{ message: string }>('/api/profile/password', {
    currentPassword,
    newPassword,
  })
  return response.data.message
}

export type DashboardResponse = {
  workspace: string
  title: string
  message: string
  generatedAtUtc: string
}

export async function getDashboard(workspace: string) {
  const response = await apiClient.get<DashboardResponse>(`/api/dashboards/${workspace}`)
  return response.data
}

export async function updateProfile(input: { firstName: string; lastName: string; phoneNumber?: string }) {
  const response = await apiClient.put<AuthUser>('/api/profile', input)
  return response.data
}

export type UserSummary = {
  id: string
  email: string
  firstName: string
  lastName: string
  locked: boolean
  active: boolean
  roles: string[]
}

export type UserPage = {
  items: UserSummary[]
  page: number
  pageSize: number
  total: number
}

export async function listUsers(query: { search?: string; role?: string; active?: string; page?: number; pageSize?: number } = {}) {
  const response = await apiClient.get<UserPage>('/api/admin/users', { params: query })
  return response.data
}

export async function updateUserRoles(id: string, roles: string[]) {
  const response = await apiClient.put<UserSummary>(`/api/admin/users/${id}/roles`, { roles })
  return response.data
}

export async function updateUserStatus(id: string, active: boolean) {
  const response = await apiClient.put<UserSummary>(`/api/admin/users/${id}/status`, { active })
  return response.data
}

export type AuditEntry = {
  id: string
  action: string
  userId: string | null
  succeeded: boolean
  detail: string | null
  createdAt: string
}

export async function listAuditLogs() {
  const response = await apiClient.get<AuditEntry[]>('/api/admin/audit-logs')
  return response.data
}
