import { render, screen } from '@testing-library/react'
import { getHealth } from '../api/health'
import DashboardPage from './DashboardPage'

jest.mock('../api/health', () => ({
  getHealth: jest.fn(),
}))

const mockedGetHealth = jest.mocked(getHealth)

describe('DashboardPage', () => {
  beforeEach(() => {
    mockedGetHealth.mockReset()
  })

  it('shows the API and database status returned by the backend', async () => {
    mockedGetHealth.mockResolvedValue({
      status: 'Healthy',
      api: 'SmartStaff.API',
      database: 'Connected',
      checkedAtUtc: '2026-11-25T18:00:00.000Z',
      databaseError: null,
    })

    render(<DashboardPage />)

    expect(await screen.findByText('Connected')).toBeTruthy()
    expect(screen.getByText('SmartStaff.API')).toBeTruthy()
    expect(screen.getByText('Healthy')).toBeTruthy()
  })

  it('shows an error when the API cannot be reached', async () => {
    mockedGetHealth.mockRejectedValue(new Error('network down'))

    render(<DashboardPage />)

    expect(await screen.findByText('The SmartStaff API could not be reached.')).toBeTruthy()
  })
})
