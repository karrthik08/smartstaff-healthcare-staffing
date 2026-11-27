#!/usr/bin/env node
// Seeds fictional demo accounts on an isolated API. Refuses the development API on port 5080.
// Passwords come from the environment and are written only to .local/demo-credentials.txt.
import { chmodSync, mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const base = process.env.DEMO_BASE_URL ?? ''
const adminEmail = process.env.DEMO_ADMIN_EMAIL ?? ''
const adminPassword = process.env.DEMO_ADMIN_PASSWORD ?? ''
const password = process.env.DEMO_PASSWORD ?? ''

if (!base || !adminEmail || !adminPassword || !password) {
  console.error('Set DEMO_BASE_URL, DEMO_ADMIN_EMAIL, DEMO_ADMIN_PASSWORD, and DEMO_PASSWORD.')
  process.exit(1)
}

let parsed
try {
  parsed = new URL(base)
} catch {
  console.error('DEMO_BASE_URL is not a URL.')
  process.exit(1)
}

if (!['127.0.0.1', 'localhost'].includes(parsed.hostname) || parsed.port === '5080') {
  console.error('Demo seeding is limited to the isolated local API. Port 5080 is the development API and is refused.')
  process.exit(1)
}

const stamp = new Date().toISOString().replace(/[-:.TZ]/g, '').slice(0, 14)
const people = {
  manager: { email: `demo-manager-${stamp}@smartstaff.invalid`, firstName: 'Morgan', lastName: 'Manager', roles: ['FacilityManager'] },
  recruiter: { email: `demo-recruiter-${stamp}@smartstaff.invalid`, firstName: 'Riley', lastName: 'Recruiter', roles: ['Recruiter'] },
  clinician: { email: `demo-clinician-${stamp}@smartstaff.invalid`, firstName: 'Casey', lastName: 'Demo', roles: ['Clinician'] },
}

const jar = {}

function remember(response) {
  const lines = typeof response.headers.getSetCookie === 'function' ? response.headers.getSetCookie() : []
  for (const line of lines) {
    const pair = line.split(';')[0]
    const index = pair.indexOf('=')
    if (index > 0) jar[pair.slice(0, index)] = pair.slice(index + 1)
  }
}

async function call(method, path, body, token, extra = {}) {
  const headers = { ...extra }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers.Authorization = `Bearer ${token}`
  const response = await fetch(`${base}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  remember(response)
  const text = await response.text()
  let payload = null
  if (text) {
    try {
      payload = JSON.parse(text)
    } catch {
      payload = text
    }
  }
  return { status: response.status, payload }
}

function assert(result, expected, label) {
  const ok = Array.isArray(expected) ? expected.includes(result.status) : result.status === expected
  if (!ok) {
    const detail = typeof result.payload === 'string' ? result.payload : JSON.stringify(result.payload)
    throw new Error(`${label} returned ${result.status}: ${detail}`)
  }
  return result.payload
}

async function login(email, secret) {
  const result = await call('POST', '/api/auth/login', { email, password: secret })
  const payload = assert(result, 200, `login ${email}`)
  return payload.accessToken
}

async function userId(adminToken, email) {
  const result = await call('GET', `/api/admin/users?search=${encodeURIComponent(email)}&pageSize=20`, undefined, adminToken)
  const page = assert(result, 200, `find ${email}`)
  const match = (page.items ?? []).find((item) => item.email === email)
  if (!match) throw new Error(`No account found for ${email}`)
  return match.id
}

async function ensureAccount(adminToken, person) {
  const created = await call('POST', '/api/auth/register', {
    email: person.email,
    password,
    firstName: person.firstName,
    lastName: person.lastName,
    phoneNumber: '555-0100',
    role: 'Clinician',
  })
  if (![201, 200, 409].includes(created.status)) {
    assert(created, [201, 200], `register ${person.email}`)
  }
  const id = await userId(adminToken, person.email)
  if (person.roles[0] !== 'Clinician') {
    assert(await call('PUT', `/api/admin/users/${id}/roles`, { roles: person.roles }, adminToken), 200, `roles ${person.email}`)
  }
  return id
}

const admin = await login(adminEmail, adminPassword)
const ids = {
  manager: await ensureAccount(admin, people.manager),
  recruiter: await ensureAccount(admin, people.recruiter),
  clinician: await ensureAccount(admin, people.clinician),
}
const manager = await login(people.manager.email, password)
const recruiter = await login(people.recruiter.email, password)
const clinician = await login(people.clinician.email, password)

assert(await call('POST', '/api/facilities', {
  name: 'Unauthorized attempt',
  facilityType: 'Hospital',
  description: 'Must fail',
  address: '1 Demo Street',
  city: 'Austin',
  state: 'TX',
  zipCode: '78701',
  contactEmail: 'none@smartstaff.invalid',
  contactPhone: '555-0101',
  status: 'Active',
  timeZoneId: 'America/Chicago',
}, clinician), 403, 'clinician facility create')

const facility = assert(await call('POST', '/api/facilities', {
  name: `Demo General ${stamp}`,
  facilityType: 'Hospital',
  description: 'Fictional demonstration facility',
  address: '100 Demonstration Avenue',
  city: 'Austin',
  state: 'TX',
  zipCode: '78701',
  contactEmail: 'demo-facility@smartstaff.invalid',
  contactPhone: '555-0102',
  status: 'Active',
  timeZoneId: 'America/Chicago',
}, admin), 201, 'create facility')

const location = assert(await call('POST', `/api/facilities/${facility.id}/locations`, {
  locationName: 'Demo Unit',
  address: '100 Demonstration Avenue',
  city: 'Austin',
  state: 'TX',
  zipCode: '78701',
  isActive: true,
}, admin), 201, 'create location')

assert(await call('POST', `/api/facilities/${facility.id}/members`, {
  userId: ids.manager,
  membershipRole: 'Manager',
}, admin), 201, 'assign manager')

const requisition = assert(await call('POST', '/api/requisitions', {
  facilityId: facility.id,
  locationId: location.id,
  jobTitle: 'Registered Nurse',
  description: 'Fictional demonstration requisition',
  specialty: 'ICU',
  employmentType: 'Contract',
  shiftType: 'Day',
  requiredExperience: 1,
  requiredCertifications: 'RN',
  openPositions: 1,
  payRateMin: 50,
  payRateMax: 70,
  startDate: '2027-06-01',
  endDate: '2027-08-31',
}, admin), 201, 'create requisition')

assert(await call('PATCH', `/api/requisitions/${requisition.id}/assign-recruiter`, { recruiterId: ids.recruiter }, admin), 200, 'assign recruiter')
assert(await call('PATCH', `/api/requisitions/${requisition.id}/status`, { status: 'Open' }, admin), 200, 'open requisition')
assert(await call('PATCH', `/api/requisitions/${requisition.id}/status`, { status: 'Published' }, admin), 200, 'publish requisition')

const profile = assert(await call('PUT', '/api/clinicians/me', {
  firstName: 'Casey',
  lastName: 'Demo',
  phoneNumber: '555-0100',
  professionalTitle: 'Registered Nurse',
  specialty: 'ICU',
  yearsOfExperience: 3,
  preferredLocations: 'Austin',
  employmentPreference: 'Contract',
  availabilityDate: '2027-06-01',
}, clinician), 200, 'clinician profile')

const application = assert(await call('POST', '/api/applications', { jobRequisitionId: requisition.id }, clinician), 201, 'apply')
for (const status of ['UnderReview', 'Screening', 'SubmittedToFacility']) {
  assert(await call('PATCH', `/api/applications/${application.id}/status`, { status, reason: status }, recruiter), 200, `status ${status}`)
}
for (const status of ['Interview', 'Offer']) {
  assert(await call('PATCH', `/api/applications/${application.id}/status`, { status, reason: status }, manager), 200, `status ${status}`)
}
const placement = assert(await call('POST', '/api/placements', {
  jobApplicationId: application.id,
  startDate: '2027-06-01',
  agreedPayRate: 62,
}, recruiter), 201, 'placement')

const shift = assert(await call('POST', '/api/shifts', {
  facilityId: facility.id,
  facilityLocationId: location.id,
  jobRequisitionId: requisition.id,
  startLocal: '2027-06-02T07:00:00',
  endLocal: '2027-06-02T15:00:00',
  requiredClinicians: 1,
  specialty: 'ICU',
}, manager), 201, 'create shift')
assert(await call('PATCH', `/api/shifts/${shift.id}/publish`, undefined, manager), 200, 'publish shift')
const assignment = assert(await call('POST', `/api/shifts/${shift.id}/assignments`, { clinicianProfileId: profile.id }, manager), 201, 'assign shift')
assert(await call('PATCH', `/api/shifts/assignments/${assignment.id}/confirm`, undefined, clinician), 200, 'confirm shift')

const clockIn = assert(await call('POST', `/api/attendance/${assignment.id}/clock-in`, { local: '2027-06-02T07:00:00' }, clinician), 201, 'clock in')
if (!String(clockIn.clockInTime).includes('12:00')) {
  throw new Error(`Expected Chicago 07:00 to be stored as 12:00 UTC, received ${clockIn.clockInTime}`)
}
assert(await call('POST', `/api/attendance/${assignment.id}/clock-out`, { local: '2027-06-02T15:00:00' }, clinician), 200, 'clock out')

const timesheet = assert(await call('POST', '/api/timesheets', {
  facilityId: facility.id,
  periodStart: '2027-05-31',
  periodEnd: '2027-06-06',
}, clinician), 201, 'create timesheet')
assert(await call('POST', `/api/timesheets/${timesheet.id}/entries`, {
  shiftAssignmentId: assignment.id,
  startTime: '2027-06-02T12:00:00Z',
  endTime: '2027-06-02T20:00:00Z',
  breakMinutes: 30,
  notes: 'Demonstration entry',
}, clinician), 200, 'timesheet entry')
assert(await call('POST', `/api/timesheets/${timesheet.id}/submit`, undefined, clinician), 200, 'submit timesheet')
assert(await call('POST', `/api/timesheets/${timesheet.id}/approve`, undefined, clinician), 403, 'clinician approve')
const approved = assert(await call('POST', `/api/timesheets/${timesheet.id}/approve`, undefined, manager), 200, 'approve timesheet')

let notifications = null
for (let attempt = 0; attempt < 15; attempt += 1) {
  notifications = assert(await call('GET', '/api/notifications?pageSize=20', undefined, clinician), 200, 'notifications')
  const rows = notifications.items ?? notifications.Items ?? []
  if (rows.length > 0) {
    notifications = { ...notifications, items: rows }
    break
  }
  await new Promise((resolve) => setTimeout(resolve, 2000))
}
if ((notifications.items ?? []).length === 0) {
  throw new Error(`Clinician notifications were empty after waiting. Last response: ${JSON.stringify(notifications)}`)
}

const dashboards = {
  administrator: (await call('GET', '/api/dashboards/administrator', undefined, admin)).status,
  facilityManager: (await call('GET', '/api/dashboards/facility', undefined, manager)).status,
  recruiter: (await call('GET', '/api/dashboards/recruiter', undefined, recruiter)).status,
  clinician: (await call('GET', '/api/dashboards/clinician', undefined, clinician)).status,
}
for (const [role, status] of Object.entries(dashboards)) {
  if (status !== 200) throw new Error(`${role} dashboard returned ${status}`)
}

const analytics = assert(await call('GET', `/api/analytics/overview?facilityId=${facility.id}`, undefined, admin), 200, 'analytics')
if (Number(analytics.placements) < 1 || Number(analytics.scheduledShifts) < 1) {
  throw new Error(`Analytics did not reflect the demo records: ${JSON.stringify(analytics)}`)
}

const stale = clinician
const logout = await fetch(`${base}/api/auth/logout`, {
  method: 'POST',
  headers: {
    Cookie: `smartstaff.refresh=${jar['smartstaff.refresh'] ?? ''}; smartstaff.csrf=${jar['smartstaff.csrf'] ?? ''}`,
    'X-CSRF-TOKEN': jar['smartstaff.csrf'] ?? '',
  },
})
if (logout.status !== 204) throw new Error(`logout returned ${logout.status}`)
const replay = await call('GET', '/api/auth/me', undefined, stale)
if (replay.status !== 401) throw new Error(`old access token returned ${replay.status}`)

const summary = {
  facilityId: facility.id,
  locationId: location.id,
  requisitionId: requisition.id,
  applicationId: application.id,
  placementId: placement.id,
  shiftId: shift.id,
  assignmentId: assignment.id,
  timesheetId: timesheet.id,
  timesheetStatus: approved.status,
  notificationCount: (notifications.items ?? []).length,
  analyticsPlacements: analytics.placements,
  analyticsShifts: analytics.scheduledShifts,
  logoutInvalidatedAccessToken: true,
}
summary.managerEmail = people.manager.email
summary.recruiterEmail = people.recruiter.email
summary.clinicianEmail = people.clinician.email
summary.dashboards = dashboards
const credentialDir = join(dirname(fileURLToPath(import.meta.url)), '..', '.local')
mkdirSync(credentialDir, { recursive: true })
const credentialFile = join(credentialDir, 'demo-credentials.txt')
writeFileSync(credentialFile, [
  'SmartStaff local demonstration',
  'Site: http://127.0.0.1:8080',
  'This file is gitignored. Do not commit it or paste it into a chat.',
  '',
  'Administrator',
  `Email: ${adminEmail}`,
  `Password: ${adminPassword}`,
  '',
  'Facility manager',
  `Email: ${people.manager.email}`,
  `Password: ${password}`,
  '',
  'Recruiter',
  `Email: ${people.recruiter.email}`,
  `Password: ${password}`,
  '',
  'Clinician',
  `Email: ${people.clinician.email}`,
  `Password: ${password}`,
  '',
].join('\n'), { mode: 0o600 })
chmodSync(credentialFile, 0o600)
console.error('Demo credentials saved to .local/demo-credentials.txt')
console.log(JSON.stringify(summary))
