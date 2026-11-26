export const roleLabels: Record<string, string> = {
  Administrator: 'Administrator',
  StaffingCoordinator: 'Staffing coordinator',
  Clinician: 'Clinician',
  HospitalAdministrator: 'Hospital administrator',
}

export function primaryPath(roles: string[]) {
  if (roles.includes('Administrator')) return '/dashboard/admin'
  if (roles.includes('HospitalAdministrator')) return '/dashboard/hospital'
  if (roles.includes('StaffingCoordinator')) return '/dashboard/coordinator'
  return '/dashboard/clinician'
}

export const navigation = [
  { to: '/dashboard/admin', label: 'Administrator', roles: ['Administrator'] },
  { to: '/dashboard/coordinator', label: 'Coordinator', roles: ['Administrator', 'StaffingCoordinator'] },
  { to: '/dashboard/clinician', label: 'Clinician', roles: ['Administrator', 'Clinician'] },
  { to: '/dashboard/hospital', label: 'Hospital', roles: ['Administrator', 'HospitalAdministrator'] },
  { to: '/profile', label: 'Profile', roles: ['Administrator', 'StaffingCoordinator', 'Clinician', 'HospitalAdministrator'] },
  { to: '/health', label: 'System status', roles: ['Administrator', 'StaffingCoordinator', 'Clinician', 'HospitalAdministrator'] },
]
