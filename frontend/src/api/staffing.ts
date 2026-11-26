import { apiClient } from './client'

export type Facility = {
  id: string
  name: string
  facilityType: string
  description: string | null
  address: string
  city: string
  state: string
  zipCode: string
  contactEmail: string
  contactPhone: string
  status: string
  createdAt: string
  updatedAt: string
}

export type FacilityLocation = {
  id: string
  facilityId: string
  locationName: string
  address: string
  city: string
  state: string
  zipCode: string
  isActive: boolean
}

export type Requisition = {
  id: string
  facilityId: string
  facilityName: string
  locationId: string
  locationName: string
  city: string
  state: string
  jobTitle: string
  description: string
  specialty: string
  employmentType: string
  shiftType: string
  requiredExperience: number
  requiredCertifications: string
  openPositions: number
  payRateMin: number
  payRateMax: number
  startDate: string | null
  endDate: string | null
  status: string
  createdBy: string
  assignedRecruiterId: string | null
  createdAt: string
  updatedAt: string
}

export type Page<T> = { items: T[]; page: number; pageSize: number; total: number }

export type FacilityInput = {
  name: string
  facilityType: string
  description: string
  address: string
  city: string
  state: string
  zipCode: string
  contactEmail: string
  contactPhone: string
  status: string
}

export type RequisitionInput = {
  facilityId: string
  locationId: string
  jobTitle: string
  description: string
  specialty: string
  employmentType: string
  shiftType: string
  requiredExperience: number
  requiredCertifications: string
  openPositions: number
  payRateMin: number
  payRateMax: number
  startDate: string
  endDate: string
}

export async function listFacilities(search = '') {
  const response = await apiClient.get<Page<Facility>>('/api/facilities', { params: { search, page: 1, pageSize: 20 } })
  return response.data
}

export async function createFacility(input: FacilityInput) {
  const response = await apiClient.post<Facility>('/api/facilities', input)
  return response.data
}

export async function updateFacility(id: string, input: FacilityInput) {
  const response = await apiClient.put<Facility>(`/api/facilities/${id}`, input)
  return response.data
}

export async function listLocations(facilityId: string) {
  const response = await apiClient.get<FacilityLocation[]>(`/api/facilities/${facilityId}/locations`)
  return response.data
}

export async function createLocation(facilityId: string, input: Omit<FacilityLocation, 'id' | 'facilityId'>) {
  const response = await apiClient.post<FacilityLocation>(`/api/facilities/${facilityId}/locations`, input)
  return response.data
}

export async function assignFacilityMember(facilityId: string, userId: string, membershipRole: string) {
  const response = await apiClient.post(`/api/facilities/${facilityId}/members`, { userId, membershipRole })
  return response.data
}

export async function listRequisitions(params: Record<string, string | number | boolean | undefined>) {
  const response = await apiClient.get<Page<Requisition>>('/api/requisitions', { params })
  return response.data
}

export async function getRequisition(id: string) {
  const response = await apiClient.get<Requisition>(`/api/requisitions/${id}`)
  return response.data
}

export async function createRequisition(input: RequisitionInput) {
  const response = await apiClient.post<Requisition>('/api/requisitions', {
    ...input,
    startDate: input.startDate || null,
    endDate: input.endDate || null,
  })
  return response.data
}

export async function updateRequisition(id: string, input: RequisitionInput) {
  const response = await apiClient.put<Requisition>(`/api/requisitions/${id}`, {
    ...input,
    startDate: input.startDate || null,
    endDate: input.endDate || null,
  })
  return response.data
}

export async function changeRequisitionStatus(id: string, status: string) {
  const response = await apiClient.patch<Requisition>(`/api/requisitions/${id}/status`, { status })
  return response.data
}

export async function assignRecruiter(id: string, recruiterId: string) {
  const response = await apiClient.patch<Requisition>(`/api/requisitions/${id}/assign-recruiter`, { recruiterId })
  return response.data
}
