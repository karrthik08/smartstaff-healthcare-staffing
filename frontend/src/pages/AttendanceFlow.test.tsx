import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import AttendancePage from './AttendancePage'
import { addTimesheetEntry, approveTimesheet, clockIn, listAttendance, listTimesheets, rejectTimesheet, submitTimesheet, timesheetSummary } from '../api/timekeeping'

jest.mock('../api/timekeeping', () => ({
  listAttendance: jest.fn(),
  listTimesheets: jest.fn(),
  timesheetSummary: jest.fn(),
  clockIn: jest.fn(),
  clockOut: jest.fn(),
  createTimesheet: jest.fn(),
  addTimesheetEntry: jest.fn(),
  submitTimesheet: jest.fn(),
  approveTimesheet: jest.fn(),
  rejectTimesheet: jest.fn(),
  timesheetHistory: jest.fn(),
}))

const roles = { current: ['Clinician'] }

jest.mock('../auth/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'user-1', email: 'casey@smartstaff.test', firstName: 'Casey', lastName: 'Clinician', roles: roles.current },
    loading: false,
  }),
}))

const attendance = jest.mocked(listAttendance)
const timesheets = jest.mocked(listTimesheets)
const summary = jest.mocked(timesheetSummary)
const punch = jest.mocked(clockIn)
const hours = jest.mocked(addTimesheetEntry)
const submit = jest.mocked(submitTimesheet)
const approve = jest.mocked(approveTimesheet)
const reject = jest.mocked(rejectTimesheet)

const sheet = {
  id: 'sheet-1',
  clinicianProfileId: 'pro-1',
  clinicianName: 'Casey Clinician',
  facilityId: 'fac-1',
  facilityName: 'Riverside',
  periodStart: '2026-12-14',
  periodEnd: '2026-12-20',
  totalRegularHours: 7.5,
  totalOvertimeHours: 0,
  status: 'Submitted',
  rejectionReason: null,
  entries: [],
}

describe('attendance page', () => {
  beforeEach(() => {
    roles.current = ['Clinician']
    attendance.mockReset()
    timesheets.mockReset()
    summary.mockReset()
    punch.mockReset()
    hours.mockReset()
    submit.mockReset()
    approve.mockReset()
    reject.mockReset()
    attendance.mockResolvedValue([])
    timesheets.mockResolvedValue({ items: [] })
    summary.mockResolvedValue({ draft: 0, submitted: 1, approved: 0, rejected: 0, openAttendance: 0, missingTimesheets: 1 })
  })

  it('shows an attendance error after loading starts', async () => {
    attendance.mockImplementation(() => new Promise(() => undefined))
    timesheets.mockRejectedValue(new Error('offline'))
    render(<MemoryRouter><AttendancePage /></MemoryRouter>)
    expect(screen.getByText('Loading attendance…')).toBeTruthy()
    expect(await screen.findByText('offline')).toBeTruthy()
  })

  it('rejects a clock-out that is not after clock-in', async () => {
    render(<MemoryRouter><AttendancePage /></MemoryRouter>)
    expect(await screen.findByText('No attendance or timesheets yet.')).toBeTruthy()
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Assignment'), 'assignment-1')
    await user.type(screen.getByLabelText('Clock in time'), '2026-12-16T15:00')
    await user.type(screen.getByLabelText('Clock out time'), '2026-12-16T07:00')
    await user.click(screen.getByRole('button', { name: 'Clock out' }))
    expect(await screen.findByText('Enter a clock-out after the clock-in.')).toBeTruthy()
    expect(punch).not.toHaveBeenCalled()
  })

  it('records hours and submits the timesheet', async () => {
    hours.mockResolvedValue(sheet)
    submit.mockResolvedValue({ ...sheet, status: 'Submitted' })
    render(<MemoryRouter><AttendancePage /></MemoryRouter>)
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('Timesheet'), 'sheet-1')
    await user.type(screen.getByLabelText('Entry assignment'), 'assignment-1')
    await user.type(screen.getByLabelText('Start'), '2026-12-16T13:00')
    await user.type(screen.getByLabelText('End'), '2026-12-16T21:00')
    await user.click(screen.getByRole('button', { name: 'Add hours' }))
    expect(await screen.findByText('Hours recorded.')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Submit timesheet' }))
    expect(submit).toHaveBeenCalledWith('sheet-1')
  })

  it('approves a submitted timesheet and requires a rejection reason', async () => {
    roles.current = ['FacilityManager']
    timesheets.mockResolvedValue({ items: [sheet] })
    approve.mockResolvedValue({ ...sheet, status: 'Approved' })
    render(<MemoryRouter><AttendancePage /></MemoryRouter>)
    expect(await screen.findByText(/Submitted 1/)).toBeTruthy()
    expect(screen.getByText(/· Submitted/)).toBeTruthy()
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Reject timesheet' }))
    expect(await screen.findByText('A rejection reason is required.')).toBeTruthy()
    expect(reject).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Approve timesheet' }))
    expect(approve).toHaveBeenCalledWith('sheet-1')
    expect(await screen.findByText('Timesheet approved.')).toBeTruthy()
  })

  it('shows a missing timesheet alert to a recruiter', async () => {
    roles.current = ['Recruiter']
    render(<MemoryRouter><AttendancePage /></MemoryRouter>)
    expect(await screen.findByText('1 completed shift still needs a timesheet.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Approve timesheet' })).toBeNull()
  })
})
