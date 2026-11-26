import { CircularProgress, Stack } from '@mui/material'
import { Navigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from './useAuth'

export default function ProtectedRoute({ roles, children }: { roles?: string[]; children: ReactNode }) {
  const { user, loading } = useAuth()
  if (loading) {
    return (
      <Stack sx={{ py: 6, alignItems: 'center' }}>
        <CircularProgress />
      </Stack>
    )
  }
  if (!user) return <Navigate to="/login" replace />
  if (roles && !roles.some((role) => user.roles.includes(role))) return <Navigate to="/unauthorized" replace />
  return children
}
