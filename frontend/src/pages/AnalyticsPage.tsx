import { Alert, Button, Card, CardContent, CircularProgress, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { lazy, Suspense, useEffect, useState } from 'react'
import { analyticsCredentials, analyticsOverview, analyticsRecruitment, analyticsScheduling, analyticsTimesheets, exportAnalytics, type AnalyticsFilters, type AnalyticsOverview, type MetricPoint } from '../api/analytics'
import { apiErrorMessage } from '../api/client'
import { listFacilities, type Facility } from '../api/staffing'
import { useAuth } from '../auth/useAuth'

type Charts = {
  applications: MetricPoint[]
  recruitmentTrend: MetricPoint[]
  scheduleTrend: MetricPoint[]
  timesheets: MetricPoint[]
  credentials: MetricPoint[]
  extra: string
}

const emptyCharts: Charts = { applications: [], recruitmentTrend: [], scheduleTrend: [], timesheets: [], credentials: [], extra: '' }
const AnalyticsCharts = lazy(() => import('./AnalyticsCharts'))

export default function AnalyticsPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const admin = roles.includes('Administrator')
  const facility = roles.some((role) => ['FacilityManager', 'HospitalAdministrator', 'Hospital Administrator'].includes(role))
  const recruiter = roles.some((role) => ['Recruiter', 'StaffingCoordinator', 'Staffing Coordinator'].includes(role))
  const [filters, setFilters] = useState<AnalyticsFilters>({})
  const [draft, setDraft] = useState<AnalyticsFilters>({})
  const [overview, setOverview] = useState<AnalyticsOverview | null>(null)
  const [charts, setCharts] = useState<Charts>(emptyCharts)
  const [facilities, setFacilities] = useState<Facility[]>([])
  const [error, setError] = useState<string | null>(null)
  const [exportError, setExportError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    Promise.all([
      analyticsOverview(filters),
      analyticsRecruitment(filters),
      analyticsScheduling(filters),
      analyticsTimesheets(filters),
      analyticsCredentials(filters),
    ]).then(([summary, recruitment, scheduling, timesheets, credentials]) => {
      if (!active) return
      setOverview(summary)
      setCharts({
        applications: recruitment.applicationsByStatus,
        recruitmentTrend: recruitment.trend,
        scheduleTrend: scheduling.trend,
        timesheets: [
          { label: 'Draft', value: timesheets.draft },
          { label: 'Submitted', value: timesheets.submitted },
          { label: 'Approved', value: timesheets.approved },
          { label: 'Rejected', value: timesheets.rejected },
        ],
        credentials: credentials.byStatus,
        extra: `${recruitment.averageDaysToPlacement} days to placement · ${recruitment.placementConversionPercent}% conversion · ${scheduling.confirmedAssignments} confirmed · ${scheduling.unfilledShifts} unfilled · ${scheduling.cancellations} cancelled · ${scheduling.clinicianAvailability} available windows · ${timesheets.averageApprovalHours}h approval · ${credentials.verified} verified · ${credentials.pendingVerification} pending · ${credentials.expired} expired`,
      })
      setError(null)
    }).catch((caught) => {
      if (active) setError(apiErrorMessage(caught, 'Analytics could not be loaded.'))
    })
    if (admin || facility) {
      listFacilities().then((page) => {
        if (active) setFacilities(page.items)
      }).catch(() => undefined)
    }
    return () => {
      active = false
    }
  }, [filters, admin, facility])

  const title = admin ? 'Organization analytics' : facility ? 'Facility staffing' : recruiter ? 'Assigned recruitment' : 'My staffing'

  async function download(report: string) {
    try {
      const blob = await exportAnalytics(report, filters)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `smartstaff-${report}.csv`
      link.click()
      URL.revokeObjectURL(url)
      setExportError(null)
    } catch (caught) {
      setExportError(apiErrorMessage(caught, 'The report could not be exported.'))
    }
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">{title}</Typography>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1}>
        <TextField label="From" type="date" value={draft.from ?? ''} onChange={(event) => setDraft({ ...draft, from: event.target.value })} slotProps={{ inputLabel: { shrink: true } }} />
        <TextField label="To" type="date" value={draft.to ?? ''} onChange={(event) => setDraft({ ...draft, to: event.target.value })} slotProps={{ inputLabel: { shrink: true } }} />
        {(admin || facility) && (
          <TextField select label="Facility" value={draft.facilityId ?? ''} onChange={(event) => setDraft({ ...draft, facilityId: event.target.value })} sx={{ minWidth: 200 }}>
            <MenuItem value="">All authorized facilities</MenuItem>
            {facilities.map((item) => <MenuItem key={item.id} value={item.id}>{item.name}</MenuItem>)}
          </TextField>
        )}
        <Button variant="contained" onClick={() => setFilters(draft)}>Apply filters</Button>
      </Stack>
      {error && <Alert severity="error">{error}</Alert>}
      {exportError && <Alert severity="error">{exportError}</Alert>}
      {!overview && !error && <Typography>Loading analytics…</Typography>}
      {overview && (
        <Stack spacing={2}>
          <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 1.5 }}>
            <Kpi label="Open requisitions" value={overview.openRequisitions} />
            <Kpi label="Published positions" value={overview.publishedPositions} />
            <Kpi label="Filled positions" value={overview.filledPositions} />
            <Kpi label="Placements" value={overview.placements} />
            <Kpi label="Active applications" value={overview.activeApplications} />
            <Kpi label="Scheduled shifts" value={overview.scheduledShifts} />
            <Kpi label="Coverage %" value={overview.staffingCoveragePercent} />
            <Kpi label="Scheduled hours" value={overview.scheduledHours} />
            <Kpi label="Worked hours" value={overview.workedHours} />
            <Kpi label="Pending approvals" value={overview.pendingApprovals} />
            <Kpi label="Expiring credentials" value={overview.expiringCredentials} />
          </Stack>
          <Typography>{charts.extra}</Typography>
          <Suspense fallback={<CircularProgress aria-label="Loading charts" />}>
            <AnalyticsCharts charts={[
              { title: 'Applications by status', data: charts.applications, kind: 'bar' },
              { title: 'Recruitment activity', data: charts.recruitmentTrend, kind: 'line' },
              { title: 'Shift activity', data: charts.scheduleTrend, kind: 'line' },
              { title: 'Timesheet status', data: charts.timesheets, kind: 'bar' },
              { title: 'Credential status', data: charts.credentials, kind: 'bar' },
            ]} />
          </Suspense>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
            {['recruitment', 'scheduling', 'attendance', 'timesheets', 'credentials'].map((report) => (
              <Button key={report} variant="outlined" onClick={() => void download(report)}>Export {report}</Button>
            ))}
          </Stack>
        </Stack>
      )}
    </Stack>
  )
}

function Kpi({ label, value }: { label: string; value: number }) {
  return (
    <Card sx={{ minWidth: 160, flex: '1 1 160px' }}>
      <CardContent>
        <Typography variant="body2" color="text.secondary">{label}</Typography>
        <Typography variant="h5">{value}</Typography>
      </CardContent>
    </Card>
  )
}

