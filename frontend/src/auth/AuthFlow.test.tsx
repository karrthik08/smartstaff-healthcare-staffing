import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import App from '../App'
import { getDashboard, listAuditLogs, listUsers, login, logout, registerAccount, restoreSession } from '../api/auth'

jest.mock('../api/auth', () => ({
  login: jest.fn(),
  registerAccount: jest.fn(),
  logout: jest.fn(),
  restoreSession: jest.fn(),
  getDashboard: jest.fn(),
  currentUser: jest.fn(),
  forgotPassword: jest.fn(),
  resetPassword: jest.fn(),
  updateProfile: jest.fn(),
  listUsers: jest.fn(),
  updateUserRoles: jest.fn(),
  updateUserStatus: jest.fn(),
  listAuditLogs: jest.fn(),
  changePassword: jest.fn(),
}))

const mockedLogin = jest.mocked(login)
const mockedLogout = jest.mocked(logout)
const mockedRegister = jest.mocked(registerAccount)
const mockedRestore = jest.mocked(restoreSession)
const mockedDashboard = jest.mocked(getDashboard)
const mockedUsers = jest.mocked(listUsers)
const mockedLogs = jest.mocked(listAuditLogs)

const clinician = {
  id: 'clinician-1',
  email: 'clinician@smartstaff.test',
  firstName: 'Ada',
  lastName: 'Clinician',
  roles: ['Clinician'],
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

async function signIn(user: ReturnType<typeof userEvent.setup>) {
  await screen.findByLabelText(/^email/i)
  await user.type(screen.getByLabelText(/^email/i), clinician.email)
  await user.type(screen.getByLabelText(/^password/i), 'Phase2-Test-Passw0rd!')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('authentication flow', () => {
  beforeEach(() => {
    mockedLogin.mockReset()
    mockedLogout.mockReset()
    mockedRegister.mockReset()
    mockedRestore.mockReset()
    mockedDashboard.mockReset()
    mockedRestore.mockRejectedValue(new Error('no session'))
    mockedLogout.mockResolvedValue()
    mockedUsers.mockResolvedValue({ items: [], page: 1, pageSize: 10, total: 0 })
    mockedLogs.mockResolvedValue([])
    mockedDashboard.mockImplementation(async (workspace: string) => ({
      workspace,
      title: {
        administrator: 'Administrator workspace',
        recruiter: 'Recruiter workspace',
        clinician: 'Clinician workspace',
        facility: 'Facility manager workspace',
      }[workspace] ?? `${workspace} workspace`,
      message: 'Signed in.',
      generatedAtUtc: '2026-11-26T06:00:00.000Z',
    }))
  })

  it('signs in from the login form and opens the clinician workspace', async () => {
    mockedLogin.mockResolvedValue(clinician)
    const user = userEvent.setup()
    renderAt('/login')
    await signIn(user)

    expect(await screen.findByRole('heading', { name: 'Clinician workspace' })).toBeTruthy()
    expect(screen.getByText('Ada Clinician')).toBeTruthy()
    expect(mockedLogin).toHaveBeenCalledWith(clinician.email, 'Phase2-Test-Passw0rd!')
  })

  it('shows the inactive-account message', async () => {
    mockedLogin.mockRejectedValue(new Error('This account is inactive.'))
    const user = userEvent.setup()
    renderAt('/login')
    await signIn(user)
    expect(await screen.findByText('This account is inactive.')).toBeTruthy()
  })

  it('sends each role to its own dashboard and shows that role', async () => {
    const accounts = [
      { firstName: 'Ari', lastName: 'Admin', roles: ['Administrator'], heading: 'Administrator workspace', label: 'Administrator' },
      { firstName: 'Rae', lastName: 'Recruiter', roles: ['Recruiter'], heading: 'Recruiter workspace', label: 'Recruiter' },
      { firstName: 'Finn', lastName: 'Facility', roles: ['FacilityManager'], heading: 'Facility manager workspace', label: 'Facility manager' },
    ]
    for (const account of accounts) {
      mockedLogin.mockResolvedValue({
        id: account.roles[0],
        email: `${account.firstName}@smartstaff.test`,
        firstName: account.firstName,
        lastName: account.lastName,
        roles: account.roles,
      })
      const user = userEvent.setup()
      const view = renderAt('/login')
      await user.type(screen.getByLabelText(/^email/i), `${account.firstName}@smartstaff.test`)
      await user.type(screen.getByLabelText(/^password/i), 'Phase2-Test-Passw0rd!')
      await user.click(screen.getByRole('button', { name: 'Sign in' }))
      expect(await screen.findByRole('heading', { name: account.heading })).toBeTruthy()
      expect(screen.getByText(`${account.firstName} ${account.lastName}`)).toBeTruthy()
      expect(screen.getAllByText(account.label).length).toBeGreaterThan(0)
      view.unmount()
    }
  })

  it('shows an expired-session message and returns to sign-in', async () => {
    mockedLogin.mockResolvedValue(clinician)
    const user = userEvent.setup()
    renderAt('/login')
    await signIn(user)
    await screen.findByRole('heading', { name: 'Clinician workspace' })
    await act(async () => {
      window.dispatchEvent(new Event('smartstaff:session-expired'))
    })
    expect(await screen.findByText('Your session expired. Sign in again.')).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeTruthy()
  })

  it('shows the API error when sign-in is rejected', async () => {
    mockedLogin.mockRejectedValue(new Error('Invalid email or password.'))
    const user = userEvent.setup()
    renderAt('/login')
    await signIn(user)

    expect(await screen.findByText('Invalid email or password.')).toBeTruthy()
  })

  it('registers a clinician account and returns to sign-in', async () => {
    mockedRegister.mockResolvedValue()
    const user = userEvent.setup()
    renderAt('/register')

    expect(await screen.findByText(/Public registration creates a clinician account/)).toBeTruthy()
    await user.type(screen.getByLabelText(/^first name/i), 'Ada')
    await user.type(screen.getByLabelText(/^last name/i), 'Clinician')
    await user.type(screen.getByLabelText(/^email/i), clinician.email)
    await user.type(screen.getByLabelText(/^password/i), 'short')
    await user.click(screen.getByRole('button', { name: 'Create clinician account' }))
    expect(await screen.findByText(/at least 12 characters/)).toBeTruthy()
    expect(mockedRegister).not.toHaveBeenCalled()

    await user.clear(screen.getByLabelText(/^password/i))
    await user.type(screen.getByLabelText(/^password/i), 'Phase2-Test-Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Create clinician account' }))
    expect(await screen.findByText('Clinician account created. Sign in to continue.')).toBeTruthy()
  })

  it('sends an anonymous visitor from a protected page to sign-in', async () => {
    renderAt('/dashboard/clinician')
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeTruthy()
  })

  it('shows clinician navigation and hides administrator navigation', async () => {
    mockedLogin.mockResolvedValue(clinician)
    const user = userEvent.setup()
    renderAt('/login')
    await signIn(user)

    expect(await screen.findByRole('heading', { name: 'Clinician workspace' })).toBeTruthy()
    expect(screen.getByText('Profile')).toBeTruthy()
    expect(screen.queryByText('Administrator')).toBeNull()
    expect(screen.queryByText('Recruiter')).toBeNull()
    expect(screen.queryByText('Facility')).toBeNull()
  })

  it('signs out and returns to the login page', async () => {
    mockedLogin.mockResolvedValue(clinician)
    const user = userEvent.setup()
    renderAt('/login')
    await signIn(user)
    await screen.findByRole('heading', { name: 'Clinician workspace' })

    await user.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeTruthy()
    expect(mockedLogout).toHaveBeenCalled()
  })
})
