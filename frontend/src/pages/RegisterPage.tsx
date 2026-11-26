import { Alert, Button, Link as MuiLink, Stack, TextField, Typography } from '@mui/material'
import { useState, type FormEvent } from 'react'
import { Link as RouterLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import AuthCard from './AuthCard'

export default function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', phoneNumber: '', password: '' })
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  function update(field: keyof typeof form, value: string) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (!form.email.includes('@') || form.password.length < 12 || !form.firstName.trim() || !form.lastName.trim()) {
      setError('Use your name, a valid email, and a password of at least 12 characters with upper and lower case, a number, and a symbol.')
      return
    }
    setLoading(true)
    setError(null)
    try {
      await register(form)
      navigate('/login', { state: { notice: 'Clinician account created. Sign in to continue.' } })
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Registration failed.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthCard title="Clinician registration">
      <Typography color="text.secondary">Public registration creates a clinician account. Other roles are assigned by an administrator.</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      <Stack component="form" spacing={2} onSubmit={onSubmit}>
        <TextField label="First name" value={form.firstName} onChange={(event) => update('firstName', event.target.value)} required />
        <TextField label="Last name" value={form.lastName} onChange={(event) => update('lastName', event.target.value)} required />
        <TextField label="Email" type="email" value={form.email} onChange={(event) => update('email', event.target.value)} autoComplete="email" required />
        <TextField label="Phone" value={form.phoneNumber} onChange={(event) => update('phoneNumber', event.target.value)} />
        <TextField label="Password" type="password" value={form.password} onChange={(event) => update('password', event.target.value)} autoComplete="new-password" required />
        <Button type="submit" variant="contained" disabled={loading}>{loading ? 'Creating account…' : 'Create clinician account'}</Button>
      </Stack>
      <MuiLink component={RouterLink} to="/login">Already have an account</MuiLink>
    </AuthCard>
  )
}
