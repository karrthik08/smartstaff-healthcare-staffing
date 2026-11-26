import { Alert, Button, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { changeApplicationStatus, createPlacement, getApplication, type JobApplication } from '../api/recruitment'
import { apiErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const statuses = ['Applied', 'UnderReview', 'Screening', 'SubmittedToFacility', 'Interview', 'Offer', 'Rejected', 'Withdrawn']

export default function ApplicationDetailPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const [item, setItem] = useState<JobApplication | null>(null)
  const [status, setStatus] = useState('Applied')
  const [reason, setReason] = useState('')
  const [notes, setNotes] = useState('')
  const [pay, setPay] = useState('50')
  const [startDate, setStartDate] = useState('2026-12-01')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const clinician = user?.roles.includes('Clinician') && !user.roles.includes('Administrator')
  const canReview = !!user && user.roles.some((role) => ['Administrator', 'Recruiter', 'FacilityManager', 'HospitalAdministrator'].includes(role))

  useEffect(() => {
    getApplication(id)
      .then((application) => {
        setItem(application)
        setStatus(application.status === 'Placed' ? 'Offer' : application.status)
        setNotes(application.notes)
      })
      .catch((caught) => setError(apiErrorMessage(caught, 'This application is not available.')))
  }, [id])

  async function saveStatus() {
    try {
      const updated = await changeApplicationStatus(id, clinician ? 'Withdrawn' : status, reason, notes)
      setItem(updated)
      setMessage('Application updated.')
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The status change was rejected.'))
    }
  }

  async function place() {
    try {
      await createPlacement({ jobApplicationId: id, startDate, agreedPayRate: Number(pay) })
      setItem(await getApplication(id))
      setMessage('Placement recorded.')
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The placement could not be created.'))
    }
  }

  if (!item && !error) return <Typography>Loading application…</Typography>
  if (!item) return <Alert severity="error">{error}</Alert>

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">{item.jobTitle}</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      <Typography>{item.clinicianName} · {item.facilityName} · {item.specialty}</Typography>
      <Typography>Status {item.status}</Typography>
      <Stack spacing={0.5}>
        {item.history.map((entry) => (
          <Typography key={entry.id} variant="body2">{entry.previousStatus ?? 'Start'} → {entry.newStatus}{entry.reason ? ` · ${entry.reason}` : ''}</Typography>
        ))}
      </Stack>
      {clinician && !['Placed', 'Rejected', 'Withdrawn'].includes(item.status) && (
        <Button variant="outlined" onClick={saveStatus}>Withdraw application</Button>
      )}
      {canReview && item.status !== 'Placed' && (
        <Stack spacing={2}>
          <TextField label="Recruitment notes" value={notes} onChange={(event) => setNotes(event.target.value)} multiline />
          <TextField label="Status" select value={status} onChange={(event) => setStatus(event.target.value)}>
            {statuses.map((value) => <MenuItem key={value} value={value}>{value}</MenuItem>)}
          </TextField>
          <TextField label="Reason" value={reason} onChange={(event) => setReason(event.target.value)} />
          <Button variant="contained" onClick={saveStatus} sx={{ alignSelf: 'flex-start' }}>Update application status</Button>
        </Stack>
      )}
      {canReview && item.status === 'Offer' && (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
          <TextField label="Start date" type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField label="Agreed pay" type="number" value={pay} onChange={(event) => setPay(event.target.value)} />
          <Button variant="contained" onClick={place} sx={{ alignSelf: 'center' }}>Create placement</Button>
        </Stack>
      )}
    </Stack>
  )
}
