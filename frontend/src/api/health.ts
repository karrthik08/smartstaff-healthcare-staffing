import axios from 'axios'
import { apiClient } from './client'

export type HealthReport = {
  status: string
  api: string
  database: string
  checkedAtUtc: string
  databaseError: string | null
}

export async function getHealth(): Promise<HealthReport> {
  try {
    const response = await apiClient.get<HealthReport>('/api/health')
    return response.data
  } catch (error) {
    if (axios.isAxiosError<HealthReport>(error) && error.response?.data?.status) {
      return error.response.data
    }

    throw error
  }
}
