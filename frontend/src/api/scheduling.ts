import { apiClient } from './client'

export type Shift = {
  id: string
  facilityId: string
  facilityName: string
  timeZoneId: string
  facilityLocationId: string
  locationName: string
  jobRequisitionId: string
  startDateTime: string
  endDateTime: string
  startLocal: string
  endLocal: string
  requiredClinicians: number
  assignedClinicians: number
  confirmedClinicians: number
  specialty: string
  status: string
}

export type Assignment = {
  id: string
  shiftId: string
  clinicianProfileId: string
  clinicianName: string
  assignmentStatus: string
  confirmedAt: string | null
}

export type Availability = {
  id: string
  availableFrom: string
  availableUntil: string
  availabilityType: string
  notes: string
}

export type ShiftChange = {
  id: string
  shiftAssignmentId: string
  shiftId: string
  requestType: string
  reason: string
  status: string
}

export async function listShifts(params: Record<string, string | number> = {}) {
  const response = await apiClient.get<{ items: Shift[] }>('/api/shifts', { params })
  return response.data
}

export async function createShift(body: {
  facilityId: string
  facilityLocationId: string
  jobRequisitionId: string
  startLocal: string
  endLocal: string
  requiredClinicians: number
  specialty: string
}) {
  const response = await apiClient.post<Shift>('/api/shifts', body)
  return response.data
}

export async function publishShift(id: string) {
  const response = await apiClient.patch<Shift>(`/api/shifts/${id}/publish`)
  return response.data
}

export async function assignClinician(shiftId: string, clinicianProfileId: string) {
  const response = await apiClient.post<Assignment>(`/api/shifts/${shiftId}/assignments`, { clinicianProfileId })
  return response.data
}

export async function confirmAssignment(id: string) {
  const response = await apiClient.patch<Assignment>(`/api/shifts/assignments/${id}/confirm`)
  return response.data
}

export async function saveAvailability(body: { availableFrom: string; availableUntil: string; availabilityType: string; notes: string }) {
  const response = await apiClient.post<Availability>('/api/availability', body)
  return response.data
}

export async function listAvailability() {
  const response = await apiClient.get<Availability[]>('/api/availability/me')
  return response.data
}

export async function requestShiftChange(body: { shiftAssignmentId: string; requestType: string; reason: string }) {
  const response = await apiClient.post<ShiftChange>('/api/shift-changes', body)
  return response.data
}

export async function listShiftChanges() {
  const response = await apiClient.get<ShiftChange[]>('/api/shift-changes')
  return response.data
}

export async function reviewShiftChange(id: string, status: string) {
  const response = await apiClient.patch<ShiftChange>(`/api/shift-changes/${id}`, { status })
  return response.data
}
