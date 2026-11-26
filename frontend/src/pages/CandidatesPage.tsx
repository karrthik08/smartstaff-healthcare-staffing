import { Alert, Button, Stack, TextField, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { listClinicians, type ClinicianProfile } from '../api/recruitment'
import { apiErrorMessage } from '../api/client'

export default function CandidatesPage() {
  const [specialty, setSpecialty] = useState('')
  const [location, setLocation] = useState('')
  const [experience, setExperience] = useState('')
  const [items, setItems] = useState<ClinicianProfile[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  function load() {
    const params: Record<string, string | number> = {}
    if (specialty) params.specialty = specialty
    if (location) params.location = location
    if (experience) params.minExperience = Number(experience)
    listClinicians(params)
      .then((page) => setItems(page.items))
      .catch((caught) => setError(apiErrorMessage(caught, 'Candidates could not be loaded.')))
  }

  useEffect(() => {
    let active = true
    listClinicians({})
      .then((page) => {
        if (active) setItems(page.items)
      })
      .catch((caught) => {
        if (active) setError(apiErrorMessage(caught, 'Candidates could not be loaded.'))
      })
    return () => {
      active = false
    }
  }, [])

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">Candidates</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
        <TextField label="Specialty" value={specialty} onChange={(event) => setSpecialty(event.target.value)} />
        <TextField label="Location" value={location} onChange={(event) => setLocation(event.target.value)} />
        <TextField label="Minimum experience" type="number" value={experience} onChange={(event) => setExperience(event.target.value)} />
        <Button variant="contained" onClick={load} sx={{ alignSelf: 'center' }}>Search candidates</Button>
      </Stack>
      {items?.length === 0 && <Typography>No candidates match these filters.</Typography>}
      {items?.map((item) => (
        <Typography key={item.id}>{item.firstName} {item.lastName} · {item.specialty} · {item.yearsOfExperience} years · {item.preferredLocations || 'Location open'}</Typography>
      ))}
      {!items && !error && <Typography>Loading candidates…</Typography>}
    </Stack>
  )
}
