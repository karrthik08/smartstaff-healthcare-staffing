import { Alert, Button, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { useEffect, useState, type FormEvent } from 'react'
import { Link as RouterLink, useNavigate } from 'react-router-dom'
import { createRequisition, listFacilities, listLocations, listRequisitions, type Facility, type FacilityLocation, type Requisition, type RequisitionInput } from '../api/staffing'
import { apiErrorMessage } from '../api/client'
import { useAuth } from '../auth/useAuth'

const emptyRequest: RequisitionInput = {
  facilityId: '',
  locationId: '',
  jobTitle: '',
  description: '',
  specialty: '',
  employmentType: 'FullTime',
  shiftType: 'Day',
  requiredExperience: 0,
  requiredCertifications: '',
  openPositions: 1,
  payRateMin: 0,
  payRateMax: 0,
  startDate: '',
  endDate: '',
}

export default function RequisitionsPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const clinicianOnly = !!user && user.roles.includes('Clinician') && !user.roles.some((role) => ['Administrator', 'Recruiter', 'FacilityManager', 'StaffingCoordinator', 'Staffing Coordinator', 'HospitalAdministrator', 'Hospital Administrator'].includes(role))
  const canCreate = !!user && user.roles.some((role) => ['Administrator', 'FacilityManager', 'HospitalAdministrator', 'Hospital Administrator'].includes(role))
  const [items, setItems] = useState<Requisition[]>([])
  const [facilities, setFacilities] = useState<Facility[]>([])
  const [locations, setLocations] = useState<FacilityLocation[]>([])
  const [filters, setFilters] = useState({ search: '', specialty: '', city: '', shiftType: '', employmentType: '' })
  const [form, setForm] = useState(emptyRequest)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  async function load(next = filters) {
    setLoading(true)
    try {
      const page = await listRequisitions({
        search: next.search || undefined,
        specialty: next.specialty || undefined,
        city: next.city || undefined,
        shiftType: next.shiftType || undefined,
        employmentType: next.employmentType || undefined,
        page: 1,
        pageSize: 20,
      })
      setItems(page.items)
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Requisitions could not be loaded.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    listRequisitions({ page: 1, pageSize: 20 })
      .then((page) => {
        if (!active) return
        setItems(page.items)
        setLoading(false)
      })
      .catch((caught) => {
        if (!active) return
        setError(apiErrorMessage(caught, 'Requisitions could not be loaded.'))
        setLoading(false)
      })
    if (canCreate) {
      listFacilities()
        .then((page) => {
          if (active) setFacilities(page.items)
        })
        .catch(() => undefined)
    }
    return () => {
      active = false
    }
  }, [canCreate])

  useEffect(() => {
    if (!form.facilityId) return undefined
    let active = true
    listLocations(form.facilityId)
      .then((items) => {
        if (active) setLocations(items)
      })
      .catch(() => {
        if (active) setLocations([])
      })
    return () => {
      active = false
    }
  }, [form.facilityId])

  async function onCreate(event: FormEvent) {
    event.preventDefault()
    if (!form.jobTitle.trim() || !form.specialty.trim() || !form.facilityId || !form.locationId) {
      setError('Enter the job title, specialty, facility, and location.')
      return
    }
    if (Number(form.payRateMax) < Number(form.payRateMin)) {
      setError('The maximum pay rate must be at least the minimum pay rate.')
      return
    }
    try {
      const created = await createRequisition(form)
      setForm(emptyRequest)
      setError(null)
      navigate(`/requisitions/${created.id}`)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The requisition could not be saved.'))
    }
  }

  return (
    <Stack spacing={3}>
      <Typography variant="h4" component="h1">{clinicianOnly ? 'Available jobs' : 'Requisitions'}</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
        <TextField label="Search" value={filters.search} onChange={(event) => setFilters({ ...filters, search: event.target.value })} fullWidth />
        <TextField label="Specialty" value={filters.specialty} onChange={(event) => setFilters({ ...filters, specialty: event.target.value })} />
        <TextField label="City" value={filters.city} onChange={(event) => setFilters({ ...filters, city: event.target.value })} />
        <TextField label="Shift" select value={filters.shiftType} onChange={(event) => setFilters({ ...filters, shiftType: event.target.value })} sx={{ minWidth: 140 }}>
          <MenuItem value="">Any shift</MenuItem>
          {['Day', 'Night', 'Evening', 'Rotating', 'Weekend'].map((shift) => <MenuItem key={shift} value={shift}>{shift}</MenuItem>)}
        </TextField>
        <TextField label="Employment" select value={filters.employmentType} onChange={(event) => setFilters({ ...filters, employmentType: event.target.value })} sx={{ minWidth: 160 }}>
          <MenuItem value="">Any type</MenuItem>
          {['FullTime', 'PartTime', 'Contract', 'PerDiem'].map((type) => <MenuItem key={type} value={type}>{type}</MenuItem>)}
        </TextField>
        <Button variant="contained" onClick={() => load()} sx={{ alignSelf: { md: 'center' } }}>Search</Button>
      </Stack>
      {loading && <Typography>Loading requisitions…</Typography>}
      {!loading && items.length === 0 && <Typography>No requisitions match this search.</Typography>}
      {items.map((item) => (
        <Stack key={item.id} component={RouterLink} to={`/requisitions/${item.id}`} spacing={0.5} sx={{ border: 1, borderColor: 'divider', borderRadius: 2, p: 2, color: 'inherit', textDecoration: 'none' }}>
          <Typography variant="h6">{item.jobTitle}</Typography>
          <Typography>{item.facilityName} · {item.city}, {item.state} · {item.specialty}</Typography>
          <Typography variant="body2">{item.shiftType} · {item.employmentType} · {item.openPositions} open · {item.status} · ${item.payRateMin}–${item.payRateMax}</Typography>
        </Stack>
      ))}
      {canCreate && (
        <Stack component="form" spacing={2} onSubmit={onCreate}>
          <Typography variant="h5" component="h2">New staffing request</Typography>
          <TextField label="Facility" select value={form.facilityId} onChange={(event) => setForm({ ...form, facilityId: event.target.value, locationId: '' })}>
            <MenuItem value="">Select a facility</MenuItem>
            {facilities.map((facility) => <MenuItem key={facility.id} value={facility.id}>{facility.name}</MenuItem>)}
          </TextField>
          <TextField label="Location" select value={form.locationId} onChange={(event) => setForm({ ...form, locationId: event.target.value })}>
            <MenuItem value="">Select a location</MenuItem>
            {(form.facilityId ? locations : []).map((location) => <MenuItem key={location.id} value={location.id}>{location.locationName}</MenuItem>)}
          </TextField>
          <TextField label="Job title" value={form.jobTitle} onChange={(event) => setForm({ ...form, jobTitle: event.target.value })} />
          <TextField label="Job specialty" value={form.specialty} onChange={(event) => setForm({ ...form, specialty: event.target.value })} />
          <TextField label="Description" value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} multiline minRows={3} />
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField label="Minimum pay" type="number" value={form.payRateMin} onChange={(event) => setForm({ ...form, payRateMin: Number(event.target.value) })} fullWidth />
            <TextField label="Maximum pay" type="number" value={form.payRateMax} onChange={(event) => setForm({ ...form, payRateMax: Number(event.target.value) })} fullWidth />
            <TextField label="Open positions" type="number" value={form.openPositions} onChange={(event) => setForm({ ...form, openPositions: Number(event.target.value) })} fullWidth />
          </Stack>
          <Button type="submit" variant="contained">Save requisition</Button>
        </Stack>
      )}
    </Stack>
  )
}
