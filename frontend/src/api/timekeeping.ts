import { apiClient } from './client'

export type Attendance = {
  id: string
  shiftAssignmentId: string
  clinicianProfileId: string
  clinicianName: string
  facilityId: string
  clockInTime: string
  clockOutTime: string | null
  attendanceStatus: string
}

export type TimesheetEntry = {
  id: string
  shiftAssignmentId: string
  workDate: string
  startTime: string
  endTime: string
  breakMinutes: number
  regularHours: number
  overtimeHours: number
  notes: string
}

export type Timesheet = {
  id: string
  clinicianProfileId: string
  clinicianName: string
  facilityId: string
  facilityName: string
  periodStart: string
  periodEnd: string
  totalRegularHours: number
  totalOvertimeHours: number
  status: string
  rejectionReason: string | null
  entries: TimesheetEntry[]
}

export type TimesheetSummary = {
  draft: number
  submitted: number
  approved: number
  rejected: number
  openAttendance: number
  missingTimesheets: number
}

export type ApprovalHistory = {
  id: string
  previousStatus: string
  newStatus: string
  changedAt: string
  comments: string
}

function asUtc(value: string) {
  if (!value) return value
  return value.length === 16 ? `${value}:00Z` : value
}

export async function listAttendance() {
  const response = await apiClient.get<Attendance[]>('/api/attendance')
  return response.data
}

export async function clockIn(assignmentId: string, at: string) {
  const response = await apiClient.post(`/api/attendance/${assignmentId}/clock-in`, { at: asUtc(at) })
  return response.data
}

export async function clockOut(assignmentId: string, at: string) {
  const response = await apiClient.post(`/api/attendance/${assignmentId}/clock-out`, { at: asUtc(at) })
  return response.data
}

export async function listTimesheets(status?: string) {
  const response = await apiClient.get<{ items: Timesheet[] }>('/api/timesheets', { params: { pageSize: 20, status } })
  return response.data
}

export async function timesheetSummary() {
  const response = await apiClient.get<TimesheetSummary>('/api/timesheets/summary')
  return response.data
}

export async function createTimesheet(body: { facilityId: string; periodStart: string; periodEnd: string }) {
  const response = await apiClient.post<Timesheet>('/api/timesheets', body)
  return response.data
}

export async function addTimesheetEntry(timesheetId: string, body: { shiftAssignmentId: string; startTime: string; endTime: string; breakMinutes: number; notes: string }) {
  const response = await apiClient.post<Timesheet>(`/api/timesheets/${timesheetId}/entries`, {
    ...body,
    startTime: asUtc(body.startTime),
    endTime: asUtc(body.endTime),
  })
  return response.data
}

export async function submitTimesheet(id: string) {
  const response = await apiClient.post<Timesheet>(`/api/timesheets/${id}/submit`)
  return response.data
}

export async function approveTimesheet(id: string) {
  const response = await apiClient.post<Timesheet>(`/api/timesheets/${id}/approve`)
  return response.data
}

export async function rejectTimesheet(id: string, reason: string) {
  const response = await apiClient.post<Timesheet>(`/api/timesheets/${id}/reject`, { reason })
  return response.data
}

export async function timesheetHistory(id: string) {
  const response = await apiClient.get<ApprovalHistory[]>(`/api/timesheets/${id}/history`)
  return response.data
}

export async function requestAdjustment(body: { timesheetEntryId: string; adjustedHours: number; reason: string }) {
  const response = await apiClient.post('/api/timesheet-adjustments', body)
  return response.data
}
