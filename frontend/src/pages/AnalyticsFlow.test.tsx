import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { analyticsCredentials, analyticsOverview, analyticsRecruitment, analyticsScheduling, analyticsTimesheets, exportAnalytics } from '../api/analytics'
import { useAuth } from '../auth/useAuth'
import AnalyticsPage from './AnalyticsPage'

jest.mock('../auth/useAuth', () => ({ useAuth: jest.fn() }))
jest.mock('../api/staffing', () => ({ listFacilities: jest.fn().mockResolvedValue({ items: [{ id: 'fac-1', name: 'Riverside General' }] }) }))
jest.mock('../api/analytics', () => ({
  analyticsOverview: jest.fn(),
  analyticsRecruitment: jest.fn(),
  analyticsScheduling: jest.fn(),
  analyticsTimesheets: jest.fn(),
  analyticsCredentials: jest.fn(),
  exportAnalytics: jest.fn(),
}))

const auth = jest.mocked(useAuth)
const overview = jest.mocked(analyticsOverview)
const recruitment = jest.mocked(analyticsRecruitment)
const scheduling = jest.mocked(analyticsScheduling)
const timesheets = jest.mocked(analyticsTimesheets)
const credentials = jest.mocked(analyticsCredentials)
const exporter = jest.mocked(exportAnalytics)

const summary = {
  openRequisitions: 2,
  publishedPositions: 1,
  filledPositions: 1,
  placements: 1,
  activeApplications: 3,
  scheduledShifts: 4,
  staffingCoveragePercent: 50,
  scheduledHours: 8,
  workedHours: 7.5,
  pendingApprovals: 1,
  expiringCredentials: 0,
}

beforeEach(() => {
  auth.mockReturnValue({ user: { id: '1', email: 'a@smartstaff.test', firstName: 'Ada', lastName: 'Admin', roles: ['Administrator'] }, login: jest.fn(), logout: jest.fn(), register: jest.fn(), refresh: jest.fn() } as never)
  overview.mockResolvedValue(summary)
  recruitment.mockResolvedValue({ applicationsByStatus: [{ label: 'Applied', value: 3 }], averageDaysToPlacement: 4, placementConversionPercent: 25, trend: [{ label: '2026-12-01', value: 1 }] })
  scheduling.mockResolvedValue({ confirmedAssignments: 2, unfilledShifts: 1, cancellations: 0, clinicianAvailability: 1, trend: [] })
  timesheets.mockResolvedValue({ draft: 1, submitted: 1, approved: 2, rejected: 0, averageApprovalHours: 5 })
  credentials.mockResolvedValue({ verified: 1, pendingVerification: 0, expiring: 0, expired: 0, byStatus: [{ label: 'Verified', value: 1 }] })
  exporter.mockResolvedValue(new Blob(['section,label']))
})

it('renders dashboard metrics and applies a facility filter', async () => {
  render(<MemoryRouter><AnalyticsPage /></MemoryRouter>)
  expect(await screen.findByText('Organization analytics')).toBeTruthy()
  expect(await screen.findByText(/4 days to placement/)).toBeTruthy()
  expect(screen.getByText('Open requisitions')).toBeTruthy()
  expect(screen.getByText('2')).toBeTruthy()
  const user = userEvent.setup()
  await user.click(screen.getByRole('combobox', { name: 'Facility' }))
  await user.click(await screen.findByRole('option', { name: 'Riverside General' }))
  await user.click(screen.getByRole('button', { name: 'Apply filters' }))
  expect(overview).toHaveBeenLastCalledWith(expect.objectContaining({ facilityId: 'fac-1' }))
})

it('exports an authorized report', async () => {
  render(<MemoryRouter><AnalyticsPage /></MemoryRouter>)
  expect(await screen.findByText(/4 days to placement/)).toBeTruthy()
  await userEvent.setup().click(screen.getByRole('button', { name: 'Export recruitment' }))
  expect(exporter).toHaveBeenCalledWith('recruitment', expect.any(Object))
})

it('shows an error when analytics cannot load', async () => {
  overview.mockRejectedValue(new Error('offline'))
  render(<MemoryRouter><AnalyticsPage /></MemoryRouter>)
  expect(await screen.findByText('offline')).toBeTruthy()
})
