import { Alert, Button, Link as MuiLink, Stack, TextField } from '@mui/material'
import { useState, type FormEvent } from 'react'
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { primaryPath } from '../auth/roles'
import AuthCard from './AuthCard'

export default function LoginPage() {
  const { login, sessionNotice } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const notice = sessionNotice ?? (location.state as { notice?: string } | null)?.notice
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (!email.includes('@') || password.length === 0) {
      setError('Enter the email and password for your account.')
      return
    }
    setLoading(true)
    setError(null)
    try {
      const user = await login(email, password)
      navigate(primaryPath(user.roles), { replace: true })
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Sign-in failed.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthCard title="Sign in">
      {notice && <Alert severity="info">{notice}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      <Stack component="form" spacing={2} onSubmit={onSubmit}>
        <TextField label="Email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="username" required />
        <TextField label="Password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" required />
        <Button type="submit" variant="contained" disabled={loading}>{loading ? 'Signing in…' : 'Sign in'}</Button>
      </Stack>
      <MuiLink component={RouterLink} to="/forgot-password">Forgot password</MuiLink>
      <MuiLink component={RouterLink} to="/register">Create a clinician account</MuiLink>
    </AuthCard>
  )
}
