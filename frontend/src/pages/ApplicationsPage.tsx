import { Alert, Button, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import { listApplications, listMyApplications, type JobApplication } from '../api/recruitment'
import { apiErrorMessage } from '../api/client'
import { useAuth } from '../auth/useAuth'

const statuses = ['', 'Applied', 'UnderReview', 'Screening', 'SubmittedToFacility', 'Interview', 'Offer', 'Placed', 'Rejected', 'Withdrawn']

export default function ApplicationsPage() {
  const { user } = useAuth()
  const clinician = user?.roles.includes('Clinician') && !user.roles.some((role) => ['Administrator', 'Recruiter', 'FacilityManager'].includes(role))
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const [items, setItems] = useState<JobApplication[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  function load(nextStatus = status, nextSearch = search) {
    const params: Record<string, string> = {}
    if (nextStatus) params.status = nextStatus
    if (nextSearch) params.search = nextSearch
    const request = clinician ? listMyApplications(params) : listApplications(params)
    request.then((page) => setItems(page.items)).catch((caught) => setError(apiErrorMessage(caught, 'Applications could not be loaded.')))
  }

  useEffect(() => {
    let active = true
    const request = clinician ? listMyApplications({}) : listApplications({})
    request
      .then((page) => {
        if (active) setItems(page.items)
      })
      .catch((caught) => {
        if (active) setError(apiErrorMessage(caught, 'Applications could not be loaded.'))
      })
    return () => {
      active = false
    }
  }, [clinician])

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">{clinician ? 'My applications' : 'Application pipeline'}</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
        <TextField label="Search" value={search} onChange={(event) => setSearch(event.target.value)} />
        <TextField label="Status" select value={status} onChange={(event) => setStatus(event.target.value)} sx={{ minWidth: 220 }}>
          {statuses.map((value) => <MenuItem key={value || 'all'} value={value}>{value || 'All statuses'}</MenuItem>)}
        </TextField>
        <Button variant="contained" onClick={() => load()} sx={{ alignSelf: 'center' }}>Filter</Button>
      </Stack>
      {items?.length === 0 && <Typography>No applications match this view.</Typography>}
      {items?.map((item) => (
        <Button key={item.id} component={RouterLink} to={`/applications/${item.id}`} sx={{ justifyContent: 'space-between' }}>
          {item.jobTitle} · {item.clinicianName} · {item.facilityName} · {item.status}
        </Button>
      ))}
      {!items && !error && <Typography>Loading applications…</Typography>}
    </Stack>
  )
}
