import RefreshIcon from '@mui/icons-material/Refresh'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useCallback, useEffect, useState } from 'react'
import { getHealth, type HealthReport } from '../api/health'

export default function DashboardPage() {
  const [report, setReport] = useState<HealthReport | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const loadHealth = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      setReport(await getHealth())
    } catch {
      setReport(null)
      setError('The SmartStaff API could not be reached.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadHealth()
  }, [loadHealth])

  const healthy = report?.status === 'Healthy'

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h4" component="h1">
          Operations dashboard
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 680 }}>
          Live status from the SmartStaff API. Database status is a real PostgreSQL connection check.
        </Typography>
      </Box>

      <Box>
        <Button
          variant="outlined"
          startIcon={<RefreshIcon />}
          onClick={() => void loadHealth()}
          disabled={loading}
        >
          Refresh status
        </Button>
      </Box>

      {loading && (
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
          <CircularProgress size={22} />
          <Typography>Checking the API and database…</Typography>
        </Stack>
      )}

      {error && <Alert severity="error">{error}</Alert>}

      {report && (
        <Box
          sx={{
            display: 'grid',
            gap: 2,
            gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' },
          }}
        >
          <StatusCard label="Overall" value={report.status} healthy={healthy} />
          <StatusCard label="API" value={report.api} healthy={healthy} />
          <StatusCard label="Database" value={report.database} healthy={report.database === 'Connected'} />
        </Box>
      )}

      {report && (
        <Paper variant="outlined" sx={{ p: 2.5 }}>
          <Typography variant="subtitle2" color="text.secondary">
            Last check
          </Typography>
          <Typography sx={{ mt: 0.5 }}>{new Date(report.checkedAtUtc).toLocaleString()}</Typography>
          {report.databaseError && (
            <Alert severity="warning" sx={{ mt: 2 }}>
              {report.databaseError}
            </Alert>
          )}
        </Paper>
      )}
    </Stack>
  )
}

function StatusCard({
  label,
  value,
  healthy,
}: {
  label: string
  value: string
  healthy: boolean
}) {
  return (
    <Paper variant="outlined" sx={{ p: 2.5 }}>
      <Typography variant="subtitle2" color="text.secondary">
        {label}
      </Typography>
      <Stack direction="row" spacing={1} sx={{ mt: 1.5, alignItems: 'center' }}>
        <Chip size="small" color={healthy ? 'success' : 'error'} label={healthy ? 'Up' : 'Down'} />
        <Typography variant="h6" component="p">
          {value}
        </Typography>
      </Stack>
    </Paper>
  )
}
