import { apiClient } from './client'

export type AppNotification = {
  id: string
  notificationType: string
  title: string
  message: string
  relatedEntityType: string
  relatedEntityId: string | null
  isRead: boolean
  createdAt: string
}

export type NotificationPreference = {
  emailEnabled: boolean
  inAppEnabled: boolean
  shiftRemindersEnabled: boolean
  applicationUpdatesEnabled: boolean
  timesheetUpdatesEnabled: boolean
}

export function notificationHref(item: Pick<AppNotification, 'relatedEntityType' | 'relatedEntityId'>) {
  if (item.relatedEntityType === 'JobApplication' && item.relatedEntityId) return `/applications/${item.relatedEntityId}`
  if (item.relatedEntityType === 'Placement') return '/placements'
  if (item.relatedEntityType === 'Timesheet') return '/attendance'
  if (item.relatedEntityType === 'Credential') return '/clinician-profile'
  if (item.relatedEntityType === 'Shift' || item.relatedEntityType === 'ShiftAssignment') return '/schedule'
  return '/notifications'
}

export async function listNotifications() {
  const response = await apiClient.get<{ items: AppNotification[]; unreadCount: number }>('/api/notifications')
  return response.data
}

export async function unreadCount() {
  const response = await apiClient.get<{ count: number }>('/api/notifications/unread-count')
  return response.data.count
}

export async function markRead(id: string) {
  const response = await apiClient.patch<AppNotification>(`/api/notifications/${id}/read`)
  return response.data
}

export async function markAllRead() {
  await apiClient.patch('/api/notifications/read-all')
}

export async function notificationPreferences() {
  const response = await apiClient.get<NotificationPreference>('/api/notifications/preferences')
  return response.data
}

export async function saveNotificationPreferences(body: NotificationPreference) {
  const response = await apiClient.put<NotificationPreference>('/api/notifications/preferences', body)
  return response.data
}
