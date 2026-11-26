import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { navigation } from '../auth/roles'
import FacilitiesPage from './FacilitiesPage'
import RequisitionsPage from './RequisitionsPage'
import { createRequisition, listFacilities, listLocations, listRequisitions } from '../api/staffing'

jest.mock('../api/staffing', () => ({
  listFacilities: jest.fn(),
  createFacility: jest.fn(),
  listLocations: jest.fn(),
  listRequisitions: jest.fn(),
  createRequisition: jest.fn(),
}))

const roles = { current: ['Administrator'] }

jest.mock('../auth/useAuth', () => ({
  useAuth: () => ({
    user: { id: 'user-1', email: 'a@smartstaff.test', firstName: 'Ada', lastName: 'Admin', roles: roles.current },
    loading: false,
  }),
}))

const mockedFacilities = jest.mocked(listFacilities)
const mockedLocations = jest.mocked(listLocations)
const mockedRequisitions = jest.mocked(listRequisitions)
const mockedCreate = jest.mocked(createRequisition)

describe('staffing pages', () => {
  beforeEach(() => {
    roles.current = ['Administrator']
    mockedFacilities.mockReset()
    mockedLocations.mockReset()
    mockedRequisitions.mockReset()
    mockedCreate.mockReset()
    mockedFacilities.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    mockedLocations.mockResolvedValue([])
    mockedRequisitions.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
  })

  it('requires a facility name before saving', async () => {
    render(<MemoryRouter><FacilitiesPage /></MemoryRouter>)
    expect(await screen.findByText('No facilities are available for this account.')).toBeTruthy()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Save facility' }))
    expect(await screen.findByText('Enter the facility name, address, city, two-letter state, and contact email.')).toBeTruthy()
  })

  it('rejects a requisition whose maximum pay is below the minimum', async () => {
    mockedFacilities.mockResolvedValue({
      items: [{ id: 'fac-1', name: 'Lakeside', facilityType: 'Hospital', description: null, address: '1', city: 'Austin', state: 'TX', zipCode: '78701', contactEmail: 'a@b.c', contactPhone: '555', status: 'Active', createdAt: '', updatedAt: '' }],
      page: 1,
      pageSize: 20,
      total: 1,
    })
    mockedLocations.mockResolvedValue([{ id: 'loc-1', facilityId: 'fac-1', locationName: 'East', address: '1', city: 'Austin', state: 'TX', zipCode: '78701', isActive: true }])
    render(<MemoryRouter><RequisitionsPage /></MemoryRouter>)
    const user = userEvent.setup()
    await user.click(await screen.findByLabelText('Facility'))
    await user.click(await screen.findByRole('option', { name: 'Lakeside' }))
    await user.click(screen.getByLabelText('Location'))
    await user.click(await screen.findByRole('option', { name: 'East' }))
    await user.type(screen.getByLabelText('Job title'), 'Nurse')
    await user.type(screen.getByLabelText('Job specialty'), 'ICU')
    await user.clear(screen.getByLabelText('Minimum pay'))
    await user.type(screen.getByLabelText('Minimum pay'), '80')
    await user.clear(screen.getByLabelText('Maximum pay'))
    await user.type(screen.getByLabelText('Maximum pay'), '40')
    await user.click(screen.getByRole('button', { name: 'Save requisition' }))
    expect(await screen.findByText('The maximum pay rate must be at least the minimum pay rate.')).toBeTruthy()
    expect(mockedCreate).not.toHaveBeenCalled()
  })

  it('searches jobs with the selected filters', async () => {
    roles.current = ['Clinician']
    mockedRequisitions.mockResolvedValue({
      items: [{
        id: 'job-1', facilityId: 'fac-1', facilityName: 'Lakeside', locationId: 'loc-1', locationName: 'East', city: 'Austin', state: 'TX',
        jobTitle: 'Registered Nurse', description: 'Nights', specialty: 'ICU', employmentType: 'FullTime', shiftType: 'Night',
        requiredExperience: 2, requiredCertifications: 'RN', openPositions: 2, payRateMin: 40, payRateMax: 55, startDate: null, endDate: null,
        status: 'Published', createdBy: 'u', assignedRecruiterId: null, createdAt: '', updatedAt: '',
      }],
      page: 1, pageSize: 20, total: 1,
    })
    render(<MemoryRouter><RequisitionsPage /></MemoryRouter>)
    expect(await screen.findByRole('heading', { name: 'Available jobs' })).toBeTruthy()
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Specialty'), 'ICU')
    await user.click(screen.getByRole('button', { name: 'Search' }))
    expect(mockedRequisitions).toHaveBeenLastCalledWith(expect.objectContaining({ specialty: 'ICU' }))
    expect(await screen.findByText('Registered Nurse')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Save requisition' })).toBeNull()
  })

  it('shows facility navigation only to facility roles', () => {
    expect(navigation.some((item) => item.label === 'Facilities' && item.roles.includes('Administrator'))).toBe(true)
    expect(navigation.some((item) => item.label === 'Jobs' && item.roles.includes('Clinician') && !item.roles.includes('Administrator'))).toBe(true)
    expect(navigation.some((item) => item.label === 'Requisitions' && item.roles.includes('Recruiter'))).toBe(true)
  })
})
