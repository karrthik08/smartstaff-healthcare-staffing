import { ThemeProvider } from '@mui/material'
import { cleanup, render, screen } from '@testing-library/react'
import { axe, toHaveNoViolations } from 'jest-axe'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { theme } from '../theme'
import AdminDashboardPage from './AdminDashboardPage'
import AnalyticsPage from './AnalyticsPage'
import ApplicationsPage from './ApplicationsPage'
import AttendancePage from './AttendancePage'
import ClinicianProfilePage from './ClinicianProfilePage'
import FacilitiesPage from './FacilitiesPage'
import LoginPage from './LoginPage'
import NotificationsPage from './NotificationsPage'
import RegisterPage from './RegisterPage'
import RequisitionsPage from './RequisitionsPage'
import SchedulePage from './SchedulePage'

expect.extend(toHaveNoViolations)

const mockSession = {
  current: {
    id: 'user-1',
    email: 'ada@example.invalid',
    firstName: 'Ada',
    lastName: 'Admin',
    roles: ['Administrator'],
  },
}

jest.mock('../auth/useAuth', () => ({
  useAuth: () => ({
    user: mockSession.current,
    loading: false,
    login: async () => mockSession.current,
    register: async () => mockSession.current,
    sessionNotice: null,
  }),
}))

jest.mock('../api/staffing', () => ({
  listFacilities: async () => ({ items: [] }),
  listLocations: async () => [],
  listRequisitions: async () => ({ items: [] }),
  createFacility: async () => ({}),
  createLocation: async () => ({}),
  assignFacilityMember: async () => ({}),
  createRequisition: async () => ({}),
}))

jest.mock('../api/recruitment', () => ({
  listApplications: async () => ({ items: [] }),
  listMyApplications: async () => ({ items: [] }),
  recruitmentSummary: async () => ({ applications: 0, activePlacements: 0, openRequisitions: 0, pipeline: [] }),
  getMyProfile: async () => {
    throw new Error('none')
  },
  saveMyProfile: async () => ({}),
  addCredential: async () => ({}),
  deleteCredential: async () => undefined,
}))

jest.mock('../api/scheduling', () => ({
  listShifts: async () => ({ items: [] }),
  listShiftChanges: async () => [],
  listAvailability: async () => [],
  createShift: async () => ({}),
  publishShift: async () => ({}),
  assignClinician: async () => ({}),
  confirmAssignment: async () => ({}),
  requestShiftChange: async () => ({}),
  reviewShiftChange: async () => ({}),
  saveAvailability: async () => ({}),
}))

jest.mock('../api/timekeeping', () => ({
  listAttendance: async () => [],
  listTimesheets: async () => ({ items: [] }),
  timesheetSummary: async () => ({ draft: 0, submitted: 0, approved: 0, rejected: 0, openAttendance: 0, missingTimesheets: 0 }),
  clockIn: async () => ({}),
  clockOut: async () => ({}),
  createTimesheet: async () => ({ id: 'sheet' }),
  addTimesheetEntry: async () => ({}),
  submitTimesheet: async () => ({}),
  approveTimesheet: async () => ({}),
  rejectTimesheet: async () => ({}),
  timesheetHistory: async () => [],
  requestAdjustment: async () => ({}),
}))

jest.mock('../api/notifications', () => ({
  listNotifications: async () => ({ items: [], unreadCount: 0 }),
  markAllRead: async () => undefined,
  markRead: async () => undefined,
  notificationHref: () => '/notifications',
  notificationPreferences: async () => ({
    emailEnabled: true,
    inAppEnabled: true,
    shiftRemindersEnabled: true,
    applicationUpdatesEnabled: true,
    timesheetUpdatesEnabled: true,
  }),
  saveNotificationPreferences: async () => undefined,
}))

jest.mock('../api/analytics', () => ({
  analyticsOverview: async () => ({
    openRequisitions: 0,
    publishedPositions: 0,
    filledPositions: 0,
    placements: 0,
    activeApplications: 0,
    scheduledShifts: 0,
    staffingCoveragePercent: 0,
    scheduledHours: 0,
    workedHours: 0,
    pendingApprovals: 0,
    expiringCredentials: 0,
  }),
  analyticsRecruitment: async () => ({ applicationsByStatus: [], trend: [], averageDaysToPlacement: 0, placementConversionPercent: 0 }),
  analyticsScheduling: async () => ({ trend: [], confirmedAssignments: 0, unfilledShifts: 0, cancellations: 0, clinicianAvailability: 0 }),
  analyticsTimesheets: async () => ({ draft: 0, submitted: 0, approved: 0, rejected: 0, averageApprovalHours: 0 }),
  analyticsCredentials: async () => ({ byStatus: [], verified: 0, pendingVerification: 0, expired: 0 }),
  exportAnalytics: async () => new Blob(),
}))

jest.mock('../api/auth', () => ({
  listUsers: async () => ({ items: [], total: 0, page: 1 }),
  listAuditLogs: async () => [],
  updateUserRoles: async () => mockSession.current,
  updateUserStatus: async () => mockSession.current,
  getDashboard: async () => ({ title: 'Administrator', message: 'Workspace ready.', generatedAtUtc: '2026-11-26T12:00:00Z' }),
}))

async function check(node: ReactElement, heading: string) {
  cleanup()
  const { container } = render(
    <MemoryRouter>
      <ThemeProvider theme={theme}>
        <main>{node}</main>
      </ThemeProvider>
    </MemoryRouter>,
  )
  expect(await screen.findByRole('heading', { name: heading })).toBeTruthy()
  expect(await axe(container)).toHaveNoViolations()
}

describe('accessibility', () => {
  it('checks the signed-out and signed-in workspaces', async () => {
    mockSession.current = { ...mockSession.current, roles: [] }
    await check(<LoginPage />, 'Sign in')
    await check(<RegisterPage />, 'Clinician registration')
    mockSession.current = { ...mockSession.current, roles: ['Administrator'] }
    await check(<AdminDashboardPage />, 'Administrator')
    await check(<FacilitiesPage />, 'Facilities')
    await check(<RequisitionsPage />, 'Requisitions')
    await check(<ClinicianProfilePage />, 'Professional profile')
    await check(<ApplicationsPage />, 'Application pipeline')
    await check(<SchedulePage />, 'Scheduling')
    await check(<AttendancePage />, 'Attendance and timesheets')
    await check(<NotificationsPage />, 'Notifications')
    await check(<AnalyticsPage />, 'Organization analytics')
  })
})
