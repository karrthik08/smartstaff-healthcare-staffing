import { Alert, Button, Stack, TextField, Typography } from '@mui/material'
import { useState, type FormEvent } from 'react'
import { changePassword, updateProfile } from '../api/auth'
import { apiErrorMessage } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { roleLabels } from '../auth/roles'

export default function ProfilePage() {
  const { user, loading } = useAuth()
  const [firstName, setFirstName] = useState(user?.firstName ?? '')
  const [lastName, setLastName] = useState(user?.lastName ?? '')
  const [phoneNumber, setPhoneNumber] = useState(user?.phoneNumber ?? '')
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  if (loading || !user) return null

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    setMessage(null)
    try {
      await updateProfile({ firstName, lastName, phoneNumber })
      setMessage('Profile saved.')
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The profile could not be saved.'))
    } finally {
      setSaving(false)
    }
  }

  async function onPassword(event: FormEvent) {
    event.preventDefault()
    if (newPassword.length < 12) {
      setError('Use a new password of at least 12 characters.')
      return
    }
    setSaving(true)
    setError(null)
    setMessage(null)
    try {
      setMessage(await changePassword(currentPassword, newPassword))
      setCurrentPassword('')
      setNewPassword('')
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The password could not be changed.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Stack spacing={2} sx={{ maxWidth: 560 }}>
      <Typography variant="h4" component="h1">Profile</Typography>
      <Typography>{user.email}</Typography>
      <Typography color="text.secondary">{user.roles.map((role) => roleLabels[role] ?? role).join(', ')}</Typography>
      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      <Stack component="form" spacing={2} onSubmit={onSubmit}>
        <TextField label="First name" value={firstName} onChange={(event) => setFirstName(event.target.value)} required />
        <TextField label="Last name" value={lastName} onChange={(event) => setLastName(event.target.value)} required />
        <TextField label="Phone" value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} />
        <Button type="submit" variant="contained" disabled={saving} sx={{ alignSelf: 'flex-start' }}>{saving ? 'Saving…' : 'Save profile'}</Button>
      </Stack>
      <Typography variant="h5" component="h2">Password</Typography>
      <Stack component="form" spacing={2} onSubmit={onPassword}>
        <TextField label="Current password" type="password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} required />
        <TextField label="New password" type="password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} required />
        <Button type="submit" variant="outlined" disabled={saving} sx={{ alignSelf: 'flex-start' }}>Change password</Button>
      </Stack>
    </Stack>
  )
}
