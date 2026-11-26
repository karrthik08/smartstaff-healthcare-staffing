export const roleLabels: Record<string, string> = {
  Administrator: 'Administrator',
  Recruiter: 'Recruiter',
  Clinician: 'Clinician',
  FacilityManager: 'Facility manager',
  StaffingCoordinator: 'Recruiter',
  'Staffing Coordinator': 'Recruiter',
  HospitalAdministrator: 'Facility manager',
  'Hospital Administrator': 'Facility manager',
}

const recruiterRoles = ['Administrator', 'Recruiter', 'StaffingCoordinator', 'Staffing Coordinator']
const facilityRoles = ['Administrator', 'FacilityManager', 'HospitalAdministrator', 'Hospital Administrator']
const clinicianRoles = ['Administrator', 'Clinician']
const anyRole = [...new Set([...recruiterRoles, ...facilityRoles, ...clinicianRoles])]

export function primaryPath(roles: string[]) {
  if (roles.includes('Administrator')) return '/dashboard/admin'
  if (facilityRoles.some((role) => roles.includes(role))) return '/dashboard/facility'
  if (recruiterRoles.some((role) => roles.includes(role))) return '/dashboard/recruiter'
  return '/dashboard/clinician'
}

export const navigation = [
  { to: '/dashboard/admin', label: 'Administrator', roles: ['Administrator'] },
  { to: '/dashboard/recruiter', label: 'Recruiter', roles: recruiterRoles },
  { to: '/dashboard/clinician', label: 'Clinician', roles: clinicianRoles },
  { to: '/dashboard/facility', label: 'Facility', roles: facilityRoles },
  { to: '/facilities', label: 'Facilities', roles: facilityRoles },
  { to: '/requisitions', label: 'Requisitions', roles: [...new Set([...recruiterRoles, ...facilityRoles])] },
  { to: '/requisitions', label: 'Jobs', roles: ['Clinician'] },
  { to: '/clinician-profile', label: 'My profile', roles: ['Clinician'] },
  { to: '/candidates', label: 'Candidates', roles: [...new Set([...recruiterRoles, ...facilityRoles])] },
  { to: '/applications', label: 'Applications', roles: anyRole },
  { to: '/placements', label: 'Placements', roles: anyRole },
  { to: '/schedule', label: 'Schedule', roles: anyRole },
  { to: '/profile', label: 'Profile', roles: anyRole },
  { to: '/health', label: 'System status', roles: anyRole },
]

export const assignableRoles = ['Administrator', 'Recruiter', 'Clinician', 'FacilityManager'] as const

export function hasAssignableRole(roles: string[], role: (typeof assignableRoles)[number]) {
  if (role === 'Recruiter') return recruiterRoles.some((name) => roles.includes(name))
  if (role === 'FacilityManager') return facilityRoles.filter((name) => name !== 'Administrator').some((name) => roles.includes(name))
  return roles.includes(role)
}
