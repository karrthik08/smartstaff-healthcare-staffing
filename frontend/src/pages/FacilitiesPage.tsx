import { Alert, Button, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { useEffect, useState, type FormEvent } from 'react'
import { assignFacilityMember, createFacility, createLocation, listFacilities, listLocations, type Facility, type FacilityInput, type FacilityLocation } from '../api/staffing'
import { apiErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const emptyFacility: FacilityInput = {
  name: '',
  facilityType: 'Hospital',
  description: '',
  address: '',
  city: '',
  state: '',
  zipCode: '',
  contactEmail: '',
  contactPhone: '',
  status: 'Active',
}

export default function FacilitiesPage() {
  const { user } = useAuth()
  const canCreate = user?.roles.includes('Administrator') ?? false
  const [facilities, setFacilities] = useState<Facility[]>([])
  const [form, setForm] = useState(emptyFacility)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [locations, setLocations] = useState<Record<string, FacilityLocation[]>>({})
  const [locationName, setLocationName] = useState<Record<string, string>>({})
  const [memberId, setMemberId] = useState<Record<string, string>>({})

  async function load() {
    setLoading(true)
    try {
      const page = await listFacilities()
      setFacilities(page.items)
      const nextLocations = await Promise.all(page.items.map(async (facility) => [facility.id, await listLocations(facility.id)] as const))
      setLocations(Object.fromEntries(nextLocations))
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Facilities could not be loaded.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load().catch(() => undefined)
  }, [])

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (!form.name.trim() || !form.address.trim() || !form.city.trim() || form.state.trim().length !== 2 || !form.contactEmail.trim()) {
      setError('Enter the facility name, address, city, two-letter state, and contact email.')
      return
    }
    try {
      await createFacility({ ...form, state: form.state.toUpperCase() })
      setForm(emptyFacility)
      setMessage('Facility saved.')
      setError(null)
      await load()
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The facility could not be saved.'))
    }
  }

  return (
    <Stack spacing={3}>
      <Typography variant="h4" component="h1">Facilities</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      {loading && <Typography>Loading facilities…</Typography>}
      {!loading && facilities.length === 0 && <Typography>No facilities are available for this account.</Typography>}
      {facilities.map((facility) => (
        <Stack key={facility.id} spacing={1} sx={{ border: 1, borderColor: 'divider', borderRadius: 2, p: 2 }}>
          <Typography variant="h6">{facility.name}</Typography>
          <Typography>{facility.facilityType} · {facility.city}, {facility.state} · {facility.status}</Typography>
          <Typography variant="body2">{facility.contactEmail} · {facility.contactPhone}</Typography>
          {(locations[facility.id] ?? []).map((location) => (
            <Typography key={location.id} variant="body2">{location.locationName} · {location.city}, {location.state}</Typography>
          ))}
          {(canCreate || user?.roles.includes('FacilityManager')) && (
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
              <TextField label="Location name" value={locationName[facility.id] ?? ''} onChange={(event) => setLocationName({ ...locationName, [facility.id]: event.target.value })} fullWidth />
              <Button variant="outlined" onClick={async () => {
                const name = (locationName[facility.id] ?? '').trim()
                if (!name) {
                  setError('Enter a location name.')
                  return
                }
                try {
                  await createLocation(facility.id, { locationName: name, address: facility.address, city: facility.city, state: facility.state, zipCode: facility.zipCode, isActive: true })
                  setLocationName({ ...locationName, [facility.id]: '' })
                  setMessage('Location saved.')
                  await load()
                } catch (caught) {
                  setError(apiErrorMessage(caught, 'The location could not be saved.'))
                }
              }}>Add location</Button>
            </Stack>
          )}
          {canCreate && (
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
              <TextField label="Member email" value={memberId[facility.id] ?? ''} onChange={(event) => setMemberId({ ...memberId, [facility.id]: event.target.value })} fullWidth />
              <Button variant="outlined" onClick={async () => {
                const userId = (memberId[facility.id] ?? '').trim()
                if (!userId) {
                  setError('Enter the account email to assign.')
                  return
                }
                try {
                  await assignFacilityMember(facility.id, userId, 'Manager')
                  setMemberId({ ...memberId, [facility.id]: '' })
                  setMessage('Facility manager assigned.')
                  await load()
                } catch (caught) {
                  setError(apiErrorMessage(caught, 'The membership could not be saved.'))
                }
              }}>Assign manager</Button>
            </Stack>
          )}
        </Stack>
      ))}
      {canCreate && (
        <Stack component="form" spacing={2} onSubmit={onSubmit}>
          <Typography variant="h5" component="h2">New facility</Typography>
          <TextField label="Facility name" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
          <TextField label="Type" select value={form.facilityType} onChange={(event) => setForm({ ...form, facilityType: event.target.value })}>
            {['Hospital', 'Clinic', 'LongTermCare', 'Outpatient', 'Other'].map((type) => <MenuItem key={type} value={type}>{type}</MenuItem>)}
          </TextField>
          <TextField label="Description" value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} />
          <TextField label="Address" value={form.address} onChange={(event) => setForm({ ...form, address: event.target.value })} />
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField label="City" value={form.city} onChange={(event) => setForm({ ...form, city: event.target.value })} fullWidth />
            <TextField label="State" value={form.state} onChange={(event) => setForm({ ...form, state: event.target.value })} sx={{ maxWidth: { sm: 120 } }} />
            <TextField label="ZIP code" value={form.zipCode} onChange={(event) => setForm({ ...form, zipCode: event.target.value })} sx={{ maxWidth: { sm: 160 } }} />
          </Stack>
          <TextField label="Contact email" type="email" value={form.contactEmail} onChange={(event) => setForm({ ...form, contactEmail: event.target.value })} />
          <TextField label="Contact phone" value={form.contactPhone} onChange={(event) => setForm({ ...form, contactPhone: event.target.value })} />
          <Button type="submit" variant="contained">Save facility</Button>
        </Stack>
      )}
    </Stack>
  )
}
