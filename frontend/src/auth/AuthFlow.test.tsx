import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import App from '../App'
import { getDashboard, login, logout, registerAccount, restoreSession } from '../api/auth'

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
  listAuditLogs: jest.fn(),
}))

const mockedLogin = jest.mocked(login)
const mockedLogout = jest.mocked(logout)
const mockedRegister = jest.mocked(registerAccount)
const mockedRestore = jest.mocked(restoreSession)
const mockedDashboard = jest.mocked(getDashboard)

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
    mockedDashboard.mockResolvedValue({
      workspace: 'clinician',
      title: 'Clinician workspace',
      message: 'Signed in as a clinician.',
      generatedAtUtc: '2026-11-26T06:00:00.000Z',
    })
  })

  it('signs in from the login form and opens the clinician workspace', async () => {
    mockedLogin.mockResolvedValue(clinician)
    const user = userEvent.setup()
    renderAt('/login')
    await signIn(user)

    expect(await screen.findByRole('heading', { name: 'Clinician workspace' })).toBeTruthy()
    expect(mockedLogin).toHaveBeenCalledWith(clinician.email, 'Phase2-Test-Passw0rd!')
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

    expect(screen.getByText(/Public registration creates a clinician account/)).toBeTruthy()
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
    expect(screen.queryByText('Coordinator')).toBeNull()
    expect(screen.queryByText('Hospital')).toBeNull()
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
