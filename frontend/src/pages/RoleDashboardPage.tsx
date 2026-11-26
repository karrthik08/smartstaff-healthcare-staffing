import { Alert, CircularProgress, Stack, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { getDashboard, type DashboardResponse } from '../api/auth'
import { apiErrorMessage } from '../api/client'
import { recruitmentSummary, type RecruitmentSummary } from '../api/recruitment'

export default function RoleDashboardPage({ workspace }: { workspace: string }) {
  const [data, setData] = useState<DashboardResponse | null>(null)
  const [summary, setSummary] = useState<RecruitmentSummary | null>(null)
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
    recruitmentSummary().then((response) => {
      if (active) setSummary(response)
    }).catch(() => undefined)
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
      {summary && (
        <Typography>
          {summary.applications} applications · {summary.activePlacements} active placements · {summary.openRequisitions} published jobs
          {summary.pipeline.length > 0 ? ` · ${summary.pipeline.map((item) => `${item.status} ${item.count}`).join(', ')}` : ''}
        </Typography>
      )}
    </Stack>
  )
}
