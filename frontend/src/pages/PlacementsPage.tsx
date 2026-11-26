import { Alert, Stack, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { listPlacements, type Placement } from '../api/recruitment'
import { apiErrorMessage } from '../api/client'

export default function PlacementsPage() {
  const [items, setItems] = useState<Placement[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    listPlacements()
      .then((page) => setItems(page.items))
      .catch((caught) => setError(apiErrorMessage(caught, 'Placements could not be loaded.')))
  }, [])

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">Placements</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {items?.length === 0 && <Typography>No placements are recorded yet.</Typography>}
      {items?.map((item) => (
        <Typography key={item.id}>{item.clinicianName} · {item.jobTitle} · {item.facilityName} · ${item.agreedPayRate} · {item.status}</Typography>
      ))}
      {!items && !error && <Typography>Loading placements…</Typography>}
    </Stack>
  )
}
