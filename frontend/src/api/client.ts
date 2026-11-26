import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { getAccessToken, setAccessToken } from '../auth/session'

const baseURL = import.meta.env.VITE_API_URL ?? ''

export const apiClient = axios.create({
  baseURL,
  timeout: 8000,
  withCredentials: true,
})

function readCookie(name: string) {
  const prefix = `${name}=`
  const match = document.cookie.split('; ').find((row) => row.startsWith(prefix))
  return match ? decodeURIComponent(match.slice(prefix.length)) : null
}

function applyAuth(config: InternalAxiosRequestConfig) {
  const token = getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  const csrf = readCookie('smartstaff.csrf')
  if (csrf) {
    config.headers['X-CSRF-TOKEN'] = csrf
  }

  return config
}

let refreshPromise: Promise<{ accessToken: string; user: AuthUserShape } | null> | null = null
let notifyRefreshFailure = false

type AuthUserShape = {
  id: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
}

export function refreshSession(notifyOnFailure = false) {
  if (notifyOnFailure) {
    notifyRefreshFailure = true
  }

  if (!refreshPromise) {
    refreshPromise = axios
      .post<{ accessToken: string; user: AuthUserShape }>(
        '/api/auth/refresh',
        {},
        {
          baseURL,
          withCredentials: true,
          headers: { 'X-CSRF-TOKEN': readCookie('smartstaff.csrf') ?? '' },
        },
      )
      .then((response) => {
        setAccessToken(response.data.accessToken)
        return response.data
      })
      .catch(() => {
        setAccessToken(null)
        if (notifyRefreshFailure) {
          window.dispatchEvent(new Event('smartstaff:session-expired'))
        }
        return null
      })
      .finally(() => {
        refreshPromise = null
        notifyRefreshFailure = false
      })
  }

  return refreshPromise
}

apiClient.interceptors.request.use(applyAuth)

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as (InternalAxiosRequestConfig & { retried?: boolean }) | undefined
    const url = config?.url ?? ''
    const status = error.response?.status
    if (
      config &&
      !config.retried &&
      status === 401 &&
      !url.includes('/api/auth/login') &&
      !url.includes('/api/auth/refresh') &&
      !url.includes('/api/auth/register')
    ) {
      config.retried = true
      const session = await refreshSession(true)
      if (session) {
        config.headers.Authorization = `Bearer ${session.accessToken}`
        return apiClient(config)
      }
    }

    return Promise.reject(error)
  },
)

export function apiErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && !axios.isAxiosError(error) && error.message) {
    return error.message
  }

  if (axios.isAxiosError(error)) {
    const data = error.response?.data
    if (data && typeof data === 'object') {
      if ('detail' in data && typeof data.detail === 'string' && data.detail) {
        return data.detail
      }
      if ('errors' in data && data.errors && typeof data.errors === 'object') {
        return Object.values(data.errors as Record<string, string[]>)
          .flat()
          .join(' ')
      }
    }
  }

  return fallback
}
