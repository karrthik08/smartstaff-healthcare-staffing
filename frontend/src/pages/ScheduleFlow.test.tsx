import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import SchedulePage from './SchedulePage'
import { assignClinician, createShift, listAvailability, listShiftChanges, listShifts, publishShift, requestShiftChange, saveAvailability } from '../api/scheduling'
import { listFacilities, listLocations, listRequisitions } from '../api/staffing'

jest.mock('../api/scheduling', () => ({
  listShifts: jest.fn(),
  createShift: jest.fn(),
  publishShift: jest.fn(),
  assignClinician: jest.fn(),
  confirmAssignment: jest.fn(),
  saveAvailability: jest.fn(),
  listAvailability: jest.fn(),
  requestShiftChange: jest.fn(),
  listShiftChanges: jest.fn(),
  reviewShiftChange: jest.fn(),
}))

jest.mock('../api/staffing', () => ({
  listFacilities: jest.fn(),
  listLocations: jest.fn(),
  listRequisitions: jest.fn(),
}))

const roles = { current: ['FacilityManager'] }

jest.mock('../auth/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'user-1', email: 'frances@smartstaff.test', firstName: 'Frances', lastName: 'Facility', roles: roles.current },
    loading: false,
  }),
}))

const shifts = jest.mocked(listShifts)
const facilities = jest.mocked(listFacilities)
const locations = jest.mocked(listLocations)
const requisitions = jest.mocked(listRequisitions)
const create = jest.mocked(createShift)
const publish = jest.mocked(publishShift)
const assign = jest.mocked(assignClinician)
const availability = jest.mocked(saveAvailability)
const mine = jest.mocked(listAvailability)
const changes = jest.mocked(listShiftChanges)
const change = jest.mocked(requestShiftChange)

describe('schedule page', () => {
  beforeEach(() => {
    roles.current = ['FacilityManager']
    shifts.mockReset()
    facilities.mockReset()
    locations.mockReset()
    requisitions.mockReset()
    create.mockReset()
    publish.mockReset()
    assign.mockReset()
    availability.mockReset()
    mine.mockReset()
    changes.mockReset()
    change.mockReset()
    shifts.mockResolvedValue({ items: [] })
    mine.mockResolvedValue([])
    changes.mockResolvedValue([])
    facilities.mockResolvedValue({ items: [{ id: 'fac-1', name: 'Riverside', facilityType: 'Hospital', description: null, address: '1', city: 'Austin', state: 'TX', zipCode: '78701', contactEmail: 'a@b.c', contactPhone: '555', status: 'Active', createdAt: '', updatedAt: '' }], page: 1, pageSize: 10, total: 1 })
    locations.mockResolvedValue([{ id: 'loc-1', facilityId: 'fac-1', locationName: 'Main', address: '1', city: 'Austin', state: 'TX', zipCode: '78701', isActive: true }])
    requisitions.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
  })

  it('shows an empty schedule and rejects an end time before the start', async () => {
    render(<MemoryRouter><SchedulePage /></MemoryRouter>)
    expect(await screen.findByText('No shifts are scheduled.')).toBeTruthy()
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Start'), '2026-12-16T15:00')
    await user.type(screen.getByLabelText('End'), '2026-12-16T07:00')
    await user.click(screen.getByRole('button', { name: 'Save shift' }))
    expect(await screen.findByText('The shift end must be after the start.')).toBeTruthy()
    expect(create).not.toHaveBeenCalled()
  })

  it('shows a loading state and an API error', async () => {
    shifts.mockImplementation(() => new Promise(() => undefined))
    const { unmount } = render(<MemoryRouter><SchedulePage /></MemoryRouter>)
    expect(await screen.findByText('Loading schedule…')).toBeTruthy()
    unmount()
    shifts.mockRejectedValue(new Error('The schedule could not be loaded.'))
    render(<MemoryRouter><SchedulePage /></MemoryRouter>)
    expect(await screen.findByText('The schedule could not be loaded.')).toBeTruthy()
  })

  it('assigns a clinician from the schedule', async () => {
    shifts.mockResolvedValue({
      items: [{
        id: 'shift-1', facilityId: 'fac-1', facilityName: 'Riverside', timeZoneId: 'America/Chicago', facilityLocationId: 'loc-1',
        locationName: 'Main', jobRequisitionId: 'job-1', startDateTime: '', endDateTime: '', startLocal: '2026-12-16T07:00', endLocal: '2026-12-16T15:00',
        requiredClinicians: 1, assignedClinicians: 0, confirmedClinicians: 0, specialty: 'ICU', status: 'Published',
      }],
    })
    assign.mockResolvedValue({ id: 'as-1', shiftId: 'shift-1', clinicianProfileId: 'pro-1', clinicianName: 'Casey Clinician', assignmentStatus: 'Assigned', confirmedAt: null })
    render(<MemoryRouter><SchedulePage /></MemoryRouter>)
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('Clinician profile'), 'pro-1')
    await user.click(screen.getByRole('button', { name: 'Assign clinician' }))
    expect(assign).toHaveBeenCalledWith('shift-1', 'pro-1')
    expect(await screen.findByText('Clinician assigned.')).toBeTruthy()
  })

  it('validates availability and submits a shift-change request', async () => {
    roles.current = ['Clinician']
    render(<MemoryRouter><SchedulePage /></MemoryRouter>)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Save availability' }))
    expect(await screen.findByText('Enter an availability window that ends after it starts.')).toBeTruthy()
    await user.type(screen.getByLabelText('Available from'), '2026-12-16T07:00')
    await user.type(screen.getByLabelText('Available until'), '2026-12-16T15:00')
    availability.mockResolvedValue({ id: 'av-1', availableFrom: '', availableUntil: '', availabilityType: 'Available', notes: '' })
    await user.click(screen.getByRole('button', { name: 'Save availability' }))
    expect(availability).toHaveBeenCalled()
    await user.type(screen.getByLabelText('Assignment'), 'as-1')
    await user.type(screen.getByLabelText('Change reason'), 'Need coverage')
    change.mockResolvedValue({ id: 'ch-1', shiftAssignmentId: 'as-1', shiftId: 'shift-1', requestType: 'Cancel', reason: 'Need coverage', status: 'Pending' })
    await user.click(screen.getByRole('button', { name: 'Request shift change' }))
    expect(change).toHaveBeenCalledWith({ shiftAssignmentId: 'as-1', requestType: 'Cancel', reason: 'Need coverage' })
    expect(publish).not.toHaveBeenCalled()
  })
})
