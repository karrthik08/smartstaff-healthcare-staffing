import { apiClient } from './client'

export type MetricPoint = { label: string; value: number }
export type ReportRow = { label: string; status: string; value: string; occurredOn: string }

export type AnalyticsOverview = {
  openRequisitions: number
  publishedPositions: number
  filledPositions: number
  placements: number
  activeApplications: number
  scheduledShifts: number
  staffingCoveragePercent: number
  scheduledHours: number
  workedHours: number
  pendingApprovals: number
  expiringCredentials: number
}

export type AnalyticsFilters = {
  from?: string
  to?: string
  facilityId?: string
}

function params(filters: AnalyticsFilters) {
  return {
    from: filters.from || undefined,
    to: filters.to || undefined,
    facilityId: filters.facilityId || undefined,
  }
}

export async function analyticsOverview(filters: AnalyticsFilters) {
  const response = await apiClient.get<AnalyticsOverview>('/api/analytics/overview', { params: params(filters) })
  return response.data
}

export async function analyticsRecruitment(filters: AnalyticsFilters) {
  const response = await apiClient.get<{ applicationsByStatus: MetricPoint[]; averageDaysToPlacement: number; placementConversionPercent: number; trend: MetricPoint[] }>('/api/analytics/recruitment', { params: params(filters) })
  return response.data
}

export async function analyticsScheduling(filters: AnalyticsFilters) {
  const response = await apiClient.get<{ confirmedAssignments: number; unfilledShifts: number; cancellations: number; clinicianAvailability: number; trend: MetricPoint[] }>('/api/analytics/scheduling', { params: params(filters) })
  return response.data
}

export async function analyticsTimesheets(filters: AnalyticsFilters) {
  const response = await apiClient.get<{ draft: number; submitted: number; approved: number; rejected: number; averageApprovalHours: number }>('/api/analytics/timesheets', { params: params(filters) })
  return response.data
}

export async function analyticsCredentials(filters: AnalyticsFilters) {
  const response = await apiClient.get<{ verified: number; pendingVerification: number; expiring: number; expired: number; byStatus: MetricPoint[] }>('/api/analytics/credentials', { params: params(filters) })
  return response.data
}

export async function exportAnalytics(report: string, filters: AnalyticsFilters) {
  const response = await apiClient.get<Blob>('/api/analytics/export', { params: { report, ...params(filters), pageSize: 50 }, responseType: 'blob' })
  return response.data
}
