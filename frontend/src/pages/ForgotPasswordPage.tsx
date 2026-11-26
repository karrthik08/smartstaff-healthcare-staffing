import { Alert, Button, Link as MuiLink, Stack, TextField } from '@mui/material'
import { useState, type FormEvent } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import { forgotPassword } from '../api/auth'
import { apiErrorMessage } from '../api/client'
import AuthCard from './AuthCard'

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (!email.includes('@')) {
      setError('Enter the email address for the account.')
      return
    }
    setLoading(true)
    setError(null)
    try {
      setMessage(await forgotPassword(email))
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The request could not be completed.'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthCard title="Forgot password">
      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      <Stack component="form" spacing={2} onSubmit={onSubmit}>
        <TextField label="Email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
        <Button type="submit" variant="contained" disabled={loading}>{loading ? 'Sending…' : 'Create reset instructions'}</Button>
      </Stack>
      <MuiLink component={RouterLink} to="/login">Back to sign in</MuiLink>
    </AuthCard>
  )
}
