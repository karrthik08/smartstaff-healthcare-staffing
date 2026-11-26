import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { getDashboard, listAuditLogs, listUsers } from '../api/auth'
import AdminDashboardPage from './AdminDashboardPage'

jest.mock('../api/auth', () => ({
  getDashboard: jest.fn(),
  listUsers: jest.fn(),
  listAuditLogs: jest.fn(),
  updateUserRoles: jest.fn(),
  updateUserStatus: jest.fn(),
}))

const mockedUsers = jest.mocked(listUsers)
const mockedLogs = jest.mocked(listAuditLogs)
const mockedDashboard = jest.mocked(getDashboard)

describe('administrator user management', () => {
  beforeEach(() => {
    mockedUsers.mockReset()
    mockedLogs.mockReset()
    mockedDashboard.mockReset()
    mockedDashboard.mockResolvedValue({
      workspace: 'administrator',
      title: 'Administrator workspace',
      message: 'Manage users.',
      generatedAtUtc: '2026-11-26T06:00:00.000Z',
    })
    mockedLogs.mockResolvedValue([])
    mockedUsers.mockResolvedValue({
      items: [{
        id: 'user-1',
        email: 'casey@smartstaff.test',
        firstName: 'Casey',
        lastName: 'Searchable',
        locked: false,
        active: true,
        roles: ['Clinician'],
      }],
      page: 1,
      pageSize: 10,
      total: 1,
    })
  })

  it('searches users and shows account status', async () => {
    render(<AdminDashboardPage />)
    expect(await screen.findByText(/casey@smartstaff.test/)).toBeTruthy()
    expect(screen.getByRole('switch', { name: 'Active' })).toBeTruthy()
    expect(screen.getByRole('checkbox', { name: 'Recruiter' })).toBeTruthy()

    const user = userEvent.setup()
    await user.type(screen.getByLabelText(/^search/i), 'Casey')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    expect(mockedUsers).toHaveBeenCalledWith(expect.objectContaining({ search: 'Casey', page: 1 }))
  })
})
