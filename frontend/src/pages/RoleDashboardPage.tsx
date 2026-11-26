import { Alert, CircularProgress, Stack, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { getDashboard, type DashboardResponse } from '../api/auth'
import { apiErrorMessage } from '../api/client'

export default function RoleDashboardPage({ workspace }: { workspace: string }) {
  const [data, setData] = useState<DashboardResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    getDashboard(workspace)
      .then((response) => {
        if (active) setData(response)
      })
      .catch((caught) => {
        if (active) setError(apiErrorMessage(caught, 'This workspace is not available.'))
      })
    return () => {
      active = false
    }
  }, [workspace])

  if (error) return <Alert severity="error">{error}</Alert>
  if (!data) return <CircularProgress />
  return (
    <Stack spacing={1.5}>
      <Typography variant="h4" component="h1">{data.title}</Typography>
      <Typography>{data.message}</Typography>
      <Typography variant="body2" color="text.secondary">
        Reported {new Date(data.generatedAtUtc).toLocaleString()}
      </Typography>
    </Stack>
  )
}
