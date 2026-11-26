import { apiClient } from './client'

export type Page<T> = { items: T[]; page: number; pageSize: number; total: number }

export type Credential = {
  id: string
  credentialType: string
  credentialName: string
  issuingOrganization: string
  credentialNumber: string | null
  issueDate: string
  expirationDate: string | null
  verificationStatus: string
  verifiedAt: string | null
}

export type ClinicianProfile = {
  id: string
  userId: string
  firstName: string
  lastName: string
  phoneNumber: string
  professionalTitle: string
  specialty: string
  yearsOfExperience: number
  preferredLocations: string
  employmentPreference: string
  availabilityDate: string | null
  profileStatus: string
  credentials: Credential[]
}

export type JobApplication = {
  id: string
  jobRequisitionId: string
  jobTitle: string
  specialty: string
  facilityId: string
  facilityName: string
  clinicianProfileId: string
  clinicianName: string
  status: string
  notes: string
  appliedAt: string
  history: { id: string; previousStatus: string | null; newStatus: string; changedAt: string; reason: string }[]
}

export type Placement = {
  id: string
  jobApplicationId: string
  facilityId: string
  facilityName: string
  clinicianName: string
  jobTitle: string
  startDate: string
  endDate: string | null
  agreedPayRate: number
  status: string
}

export type RecruitmentSummary = {
  applications: number
  activePlacements: number
  openRequisitions: number
  pipeline: { status: string; count: number }[]
}

export async function getMyProfile() {
  const response = await apiClient.get<ClinicianProfile>('/api/clinicians/me')
  return response.data
}

export async function saveMyProfile(body: Omit<ClinicianProfile, 'id' | 'userId' | 'profileStatus' | 'credentials'>) {
  const response = await apiClient.put<ClinicianProfile>('/api/clinicians/me', body)
  return response.data
}

export async function listClinicians(params: Record<string, string | number>) {
  const response = await apiClient.get<Page<ClinicianProfile>>('/api/clinicians', { params })
  return response.data
}

export async function addCredential(body: {
  credentialType: string
  credentialName: string
  issuingOrganization: string
  credentialNumber: string
  issueDate: string
  expirationDate?: string | null
}) {
  const response = await apiClient.post<Credential>('/api/clinicians/me/credentials', body)
  return response.data
}

export async function deleteCredential(id: string) {
  await apiClient.delete(`/api/clinicians/me/credentials/${id}`)
}

export async function applyToJob(jobRequisitionId: string) {
  const response = await apiClient.post<JobApplication>('/api/applications', { jobRequisitionId })
  return response.data
}

export async function listMyApplications(params: Record<string, string | number> = {}) {
  const response = await apiClient.get<Page<JobApplication>>('/api/applications/me', { params })
  return response.data
}

export async function listApplications(params: Record<string, string | number> = {}) {
  const response = await apiClient.get<Page<JobApplication>>('/api/applications', { params })
  return response.data
}

export async function getApplication(id: string) {
  const response = await apiClient.get<JobApplication>(`/api/applications/${id}`)
  return response.data
}

export async function changeApplicationStatus(id: string, status: string, reason: string, notes: string) {
  const response = await apiClient.patch<JobApplication>(`/api/applications/${id}/status`, { status, reason, notes })
  return response.data
}

export async function recruitmentSummary() {
  const response = await apiClient.get<RecruitmentSummary>('/api/applications/summary')
  return response.data
}

export async function createPlacement(body: { jobApplicationId: string; startDate: string; endDate?: string | null; agreedPayRate: number }) {
  const response = await apiClient.post<Placement>('/api/placements', body)
  return response.data
}

export async function listPlacements(params: Record<string, string | number> = {}) {
  const response = await apiClient.get<Page<Placement>>('/api/placements', { params })
  return response.data
}
