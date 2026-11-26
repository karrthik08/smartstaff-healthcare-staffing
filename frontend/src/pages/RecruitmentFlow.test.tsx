import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { navigation } from '../auth/roles'
import ApplicationDetailPage from './ApplicationDetailPage'
import ApplicationsPage from './ApplicationsPage'
import ClinicianProfilePage from './ClinicianProfilePage'
import RequisitionDetailPage from './RequisitionDetailPage'
import { applyToJob, changeApplicationStatus, getApplication, getMyProfile, listApplications, listMyApplications, saveMyProfile } from '../api/recruitment'
import { getRequisition } from '../api/staffing'

jest.mock('../api/recruitment', () => ({
  getMyProfile: jest.fn(),
  saveMyProfile: jest.fn(),
  addCredential: jest.fn(),
  applyToJob: jest.fn(),
  listMyApplications: jest.fn(),
  listApplications: jest.fn(),
  getApplication: jest.fn(),
  changeApplicationStatus: jest.fn(),
}))

jest.mock('../api/staffing', () => ({
  getRequisition: jest.fn(),
}))

const roles = { current: ['Clinician'] }

jest.mock('../auth/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'user-1', email: 'casey@smartstaff.test', firstName: 'Casey', lastName: 'Clinician', roles: roles.current },
    loading: false,
  }),
}))

const profile = jest.mocked(getMyProfile)
const save = jest.mocked(saveMyProfile)
const requisition = jest.mocked(getRequisition)
const apply = jest.mocked(applyToJob)
const mine = jest.mocked(listMyApplications)
const all = jest.mocked(listApplications)
const detail = jest.mocked(getApplication)
const status = jest.mocked(changeApplicationStatus)

describe('recruitment pages', () => {
  beforeEach(() => {
    roles.current = ['Clinician']
    profile.mockReset()
    save.mockReset()
    requisition.mockReset()
    apply.mockReset()
    mine.mockReset()
    all.mockReset()
    detail.mockReset()
    status.mockReset()
    profile.mockRejectedValue(new Error('missing'))
    mine.mockResolvedValue({ items: [], page: 1, pageSize: 10, total: 0 })
    all.mockResolvedValue({ items: [], page: 1, pageSize: 10, total: 0 })
  })

  it('requires a name and specialty before saving a profile', async () => {
    render(<MemoryRouter><ClinicianProfilePage /></MemoryRouter>)
    expect(await screen.findByText('Your profile is not complete yet.')).toBeTruthy()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Save profile' }))
    expect(await screen.findByText('Enter your name, phone, title, and specialty.')).toBeTruthy()
    expect(save).not.toHaveBeenCalled()
  })

  it('lets a clinician apply to a published job', async () => {
    requisition.mockResolvedValue({
      id: 'job-1', facilityId: 'fac-1', facilityName: 'Riverside General', locationId: 'loc-1', locationName: 'Main Campus',
      city: 'Austin', state: 'TX', jobTitle: 'Registered Nurse', description: 'Nights', specialty: 'ICU',
      employmentType: 'FullTime', shiftType: 'Night', requiredExperience: 1, requiredCertifications: 'RN',
      openPositions: 2, payRateMin: 42, payRateMax: 58, startDate: null, endDate: null, status: 'Published',
      createdBy: 'admin', assignedRecruiterId: null, createdAt: '', updatedAt: '',
    })
    const application = {
      id: 'app-1', jobRequisitionId: 'job-1', jobTitle: 'Registered Nurse', specialty: 'ICU', facilityId: 'fac-1',
      facilityName: 'Riverside General', clinicianProfileId: 'pro-1', clinicianName: 'Casey Clinician',
      status: 'Applied', notes: '', appliedAt: '', history: [],
    }
    apply.mockResolvedValue(application)
    detail.mockResolvedValue(application)
    render(
      <MemoryRouter initialEntries={['/requisitions/job-1']}>
        <Routes>
          <Route path="/requisitions/:id" element={<RequisitionDetailPage />} />
          <Route path="/applications/:id" element={<ApplicationDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Apply' }))
    expect(apply).toHaveBeenCalledWith('job-1')
  })

  it('shows an empty application list and hides recruiter controls', async () => {
    render(<MemoryRouter><ApplicationsPage /></MemoryRouter>)
    expect(await screen.findByText('No applications match this view.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Update application status' })).toBeNull()
    expect(navigation.some((link) => link.label === 'Applications' && link.roles.includes('Clinician'))).toBe(true)
    expect(navigation.some((link) => link.label === 'Candidates' && link.roles.includes('Clinician'))).toBe(false)
  })

  it('updates a candidate status from the pipeline', async () => {
    roles.current = ['Recruiter']
    all.mockResolvedValue({
      items: [{
        id: 'app-1', jobRequisitionId: 'job-1', jobTitle: 'Registered Nurse', specialty: 'ICU', facilityId: 'fac-1',
        facilityName: 'Riverside General', clinicianProfileId: 'pro-1', clinicianName: 'Casey Clinician',
        status: 'Applied', notes: '', appliedAt: '', history: [],
      }],
      page: 1, pageSize: 10, total: 1,
    })
    detail.mockResolvedValue({
      id: 'app-1', jobRequisitionId: 'job-1', jobTitle: 'Registered Nurse', specialty: 'ICU', facilityId: 'fac-1',
      facilityName: 'Riverside General', clinicianProfileId: 'pro-1', clinicianName: 'Casey Clinician',
      status: 'Applied', notes: '', appliedAt: '',
      history: [{ id: 'h1', previousStatus: null, newStatus: 'Applied', changedAt: '', reason: 'Application submitted' }],
    })
    status.mockResolvedValue({
      id: 'app-1', jobRequisitionId: 'job-1', jobTitle: 'Registered Nurse', specialty: 'ICU', facilityId: 'fac-1',
      facilityName: 'Riverside General', clinicianProfileId: 'pro-1', clinicianName: 'Casey Clinician',
      status: 'UnderReview', notes: 'Reviewed', appliedAt: '', history: [],
    })
    render(
      <MemoryRouter initialEntries={['/applications/app-1']}>
        <Routes><Route path="/applications/:id" element={<ApplicationDetailPage />} /></Routes>
      </MemoryRouter>,
    )
    const user = userEvent.setup()
    expect(await screen.findByText('Status Applied')).toBeTruthy()
    await user.click(screen.getByLabelText('Status'))
    await user.click(await screen.findByRole('option', { name: 'UnderReview' }))
    await user.click(screen.getByRole('button', { name: 'Update application status' }))
    expect(status).toHaveBeenCalledWith('app-1', 'UnderReview', '', '')
    expect(await screen.findByText('Application updated.')).toBeTruthy()
  })
})
