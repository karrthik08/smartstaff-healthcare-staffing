import { Alert, Button, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { assignRecruiter, changeRequisitionStatus, getRequisition, type Requisition } from '../api/staffing'
import { applyToJob } from '../api/recruitment'
import { apiErrorMessage } from '../api/client'
import { useAuth } from '../auth/useAuth'

const statuses = ['Draft', 'Open', 'Published', 'OnHold', 'Filled', 'Closed', 'Cancelled']

export default function RequisitionDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const { user } = useAuth()
  const [item, setItem] = useState<Requisition | null>(null)
  const [status, setStatus] = useState('Draft')
  const [recruiterId, setRecruiterId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const isAdmin = user?.roles.includes('Administrator') ?? false
  const isClinician = user?.roles.includes('Clinician') && !isAdmin
  const canManage = !!user && user.roles.some((role) => ['Administrator', 'FacilityManager', 'HospitalAdministrator', 'Hospital Administrator', 'Recruiter', 'StaffingCoordinator', 'Staffing Coordinator'].includes(role))

  useEffect(() => {
    getRequisition(id)
      .then((requisition) => {
        setItem(requisition)
        setStatus(requisition.status)
        setRecruiterId(requisition.assignedRecruiterId ?? '')
      })
      .catch((caught) => setError(apiErrorMessage(caught, 'This requisition is not available.')))
  }, [id])

  async function saveStatus() {
    try {
      const updated = await changeRequisitionStatus(id, status)
      setItem(updated)
      setMessage('Status updated.')
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The status change was rejected.'))
    }
  }

  async function saveRecruiter() {
    try {
      const updated = await assignRecruiter(id, recruiterId.trim())
      setItem(updated)
      setMessage('Recruiter assigned.')
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The recruiter assignment was rejected.'))
    }
  }

  async function apply() {
    try {
      const application = await applyToJob(id)
      navigate(`/applications/${application.id}`)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The application could not be submitted.'))
    }
  }

  if (!item && !error) return <Typography>Loading requisition…</Typography>
  if (!item) return <Alert severity="error">{error}</Alert>

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">{item.jobTitle}</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      <Typography>{item.facilityName} · {item.locationName} · {item.city}, {item.state}</Typography>
      <Typography>{item.description}</Typography>
      <Typography>Specialty {item.specialty} · {item.shiftType} · {item.employmentType}</Typography>
      <Typography>Experience {item.requiredExperience} years · {item.requiredCertifications || 'No certifications listed'}</Typography>
      <Typography>{item.openPositions} open positions · ${item.payRateMin}–${item.payRateMax} · {item.status}</Typography>
      {isClinician && item.status === 'Published' && (
        <Button variant="contained" onClick={apply} sx={{ alignSelf: 'flex-start' }}>Apply</Button>
      )}
      {canManage && (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
          <TextField label="Status" select value={status} onChange={(event) => setStatus(event.target.value)} sx={{ minWidth: 200 }}>
            {statuses.map((value) => <MenuItem key={value} value={value}>{value}</MenuItem>)}
          </TextField>
          <Button variant="contained" onClick={saveStatus} sx={{ alignSelf: 'center' }}>Update status</Button>
        </Stack>
      )}
      {isAdmin && (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
          <TextField label="Recruiter email" value={recruiterId} onChange={(event) => setRecruiterId(event.target.value)} fullWidth />
          <Button variant="outlined" onClick={saveRecruiter} sx={{ alignSelf: 'center' }}>Assign recruiter</Button>
        </Stack>
      )}
    </Stack>
  )
}
