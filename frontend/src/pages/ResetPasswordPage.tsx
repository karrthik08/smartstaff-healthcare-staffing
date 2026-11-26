import { Alert, Button, Stack, TextField } from '@mui/material'
import { useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { resetPassword } from '../api/auth'
import { apiErrorMessage } from '../api/client'
import AuthCard from './AuthCard'

export default function ResetPasswordPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const [email, setEmail] = useState(params.get('email') ?? '')
  const [token, setToken] = useState(params.get('token') ?? '')
  const [password, setPassword] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (!email.includes('@') || token.length === 0 || password.length < 12) {
      setError('Enter the email, reset token, and a new password of at least 12 characters.')
      return
    }
    setLoading(true)
    setError(null)
    try {
      setMessage(await resetPassword(email, token, password))
      window.setTimeout(() => navigate('/login', { state: { notice: 'Password reset. Sign in with the new password.' } }), 600)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The reset link is invalid or has expired.'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthCard title="Reset password">
      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      <Stack component="form" spacing={2} onSubmit={onSubmit}>
        <TextField label="Email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
        <TextField label="Reset token" value={token} onChange={(event) => setToken(event.target.value)} required />
        <TextField label="New password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} required />
        <Button type="submit" variant="contained" disabled={loading}>{loading ? 'Resetting…' : 'Reset password'}</Button>
      </Stack>
    </AuthCard>
  )
}
