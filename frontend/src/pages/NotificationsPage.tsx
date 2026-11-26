import { Alert, Button, Checkbox, FormControlLabel, Stack, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import { apiErrorMessage } from '../api/client'
import { listNotifications, markAllRead, markRead, notificationHref, notificationPreferences, saveNotificationPreferences, type AppNotification, type NotificationPreference } from '../api/notifications'

const initialPreference: NotificationPreference = {
  emailEnabled: true,
  inAppEnabled: true,
  shiftRemindersEnabled: true,
  applicationUpdatesEnabled: true,
  timesheetUpdatesEnabled: true,
}

export default function NotificationsPage() {
  const [items, setItems] = useState<AppNotification[] | null>(null)
  const [preference, setPreference] = useState<NotificationPreference>(initialPreference)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    listNotifications()
      .then((page) => {
        if (active) setItems(page.items)
      })
      .catch((caught) => {
        if (active) setError(apiErrorMessage(caught, 'Notifications could not be loaded.'))
      })
    notificationPreferences().then((value) => {
      if (active) setPreference(value)
    }).catch(() => undefined)
    return () => {
      active = false
    }
  }, [])

  function toggle(key: keyof NotificationPreference) {
    setPreference((current) => ({ ...current, [key]: !current[key] }))
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">Notifications</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      {!items && !error && <Typography>Loading notifications…</Typography>}
      {items?.length === 0 && <Typography>No notifications.</Typography>}
      <Button variant="outlined" onClick={() => markAllRead().then(async () => {
        setItems((await listNotifications()).items)
        setMessage('Notifications marked read.')
      }).catch((caught) => setError(apiErrorMessage(caught, 'Notifications could not be updated.')))} sx={{ alignSelf: 'flex-start' }}>Mark all read</Button>
      {items?.map((item) => (
        <Stack key={item.id} spacing={0.5} sx={{ border: 1, borderColor: 'divider', borderRadius: 2, p: 2, bgcolor: item.isRead ? 'transparent' : 'action.hover' }}>
          <Typography sx={{ fontWeight: item.isRead ? 400 : 700 }}>{item.title} · {item.isRead ? 'Read' : 'Unread'}</Typography>
          <Typography>{item.message}</Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
            <Button component={RouterLink} to={notificationHref(item)}>Open</Button>
            {!item.isRead && <Button onClick={() => markRead(item.id).then(() => setItems((current) => current?.map((row) => row.id === item.id ? { ...row, isRead: true } : row) ?? current))}>Mark read</Button>}
          </Stack>
        </Stack>
      ))}
      <Typography variant="h5" component="h2">Notification preferences</Typography>
      <FormControlLabel control={<Checkbox checked={preference.emailEnabled} onChange={() => toggle('emailEnabled')} />} label="Email" />
      <FormControlLabel control={<Checkbox checked={preference.inAppEnabled} onChange={() => toggle('inAppEnabled')} />} label="In-app" />
      <FormControlLabel control={<Checkbox checked={preference.shiftRemindersEnabled} onChange={() => toggle('shiftRemindersEnabled')} />} label="Shift reminders" />
      <FormControlLabel control={<Checkbox checked={preference.applicationUpdatesEnabled} onChange={() => toggle('applicationUpdatesEnabled')} />} label="Application updates" />
      <FormControlLabel control={<Checkbox checked={preference.timesheetUpdatesEnabled} onChange={() => toggle('timesheetUpdatesEnabled')} />} label="Timesheet updates" />
      <Button variant="contained" onClick={() => saveNotificationPreferences(preference).then(() => {
        setMessage('Preferences saved.')
        setError(null)
      }).catch((caught) => setError(apiErrorMessage(caught, 'Preferences could not be saved.')))} sx={{ alignSelf: 'flex-start' }}>Save preferences</Button>
    </Stack>
  )
}
