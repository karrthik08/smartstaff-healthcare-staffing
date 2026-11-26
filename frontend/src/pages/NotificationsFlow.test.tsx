import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import NotificationBell from '../layout/NotificationBell'
import NotificationsPage from './NotificationsPage'
import { listNotifications, markRead, notificationPreferences, saveNotificationPreferences, unreadCount } from '../api/notifications'

jest.mock('../api/notifications', () => ({
  listNotifications: jest.fn(),
  unreadCount: jest.fn(),
  markRead: jest.fn(),
  markAllRead: jest.fn(),
  notificationPreferences: jest.fn(),
  saveNotificationPreferences: jest.fn(),
  notificationHref: (item: { relatedEntityType: string }) => item.relatedEntityType === 'Timesheet' ? '/attendance' : '/schedule',
}))

const count = jest.mocked(unreadCount)
const list = jest.mocked(listNotifications)
const read = jest.mocked(markRead)
const preferences = jest.mocked(notificationPreferences)
const save = jest.mocked(saveNotificationPreferences)

const item = {
  id: 'note-1',
  notificationType: 'ShiftPublished',
  title: 'New shift published',
  message: 'ICU was published.',
  relatedEntityType: 'Shift',
  relatedEntityId: 'shift-1',
  isRead: false,
  createdAt: '2026-12-16T13:00:00Z',
}

describe('notifications', () => {
  beforeEach(() => {
    count.mockReset()
    list.mockReset()
    read.mockReset()
    preferences.mockReset()
    save.mockReset()
    count.mockResolvedValue(1)
    list.mockResolvedValue({ items: [item], unreadCount: 1 })
    preferences.mockResolvedValue({ emailEnabled: true, inAppEnabled: true, shiftRemindersEnabled: true, applicationUpdatesEnabled: true, timesheetUpdatesEnabled: true })
    read.mockResolvedValue({ ...item, isRead: true })
    save.mockResolvedValue({ emailEnabled: false, inAppEnabled: true, shiftRemindersEnabled: true, applicationUpdatesEnabled: true, timesheetUpdatesEnabled: true })
  })

  it('shows the unread count and marks a notification read', async () => {
    render(<MemoryRouter><NotificationBell /></MemoryRouter>)
    expect(await screen.findByLabelText('Notifications')).toBeTruthy()
    const user = userEvent.setup()
    await user.click(screen.getByLabelText('Notifications'))
    expect(await screen.findByText('New shift published')).toBeTruthy()
    await user.click(screen.getByText('New shift published'))
    expect(read).toHaveBeenCalledWith('note-1')
  })

  it('shows an error when notifications cannot load', async () => {
    list.mockRejectedValue(new Error('offline'))
    render(<MemoryRouter><NotificationsPage /></MemoryRouter>)
    expect(await screen.findByText('offline')).toBeTruthy()
  })

  it('saves notification preferences', async () => {
    render(<MemoryRouter><NotificationsPage /></MemoryRouter>)
    expect(await screen.findByText(/New shift published/)).toBeTruthy()
    const user = userEvent.setup()
    await user.click(screen.getByLabelText('Email'))
    await user.click(screen.getByRole('button', { name: 'Save preferences' }))
    expect(save).toHaveBeenCalledWith(expect.objectContaining({ emailEnabled: false }))
    expect(await screen.findByText('Preferences saved.')).toBeTruthy()
  })
})
