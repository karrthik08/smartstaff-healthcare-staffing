import { Alert, Button, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { listFacilities, listLocations, listRequisitions } from '../api/staffing'
import { assignClinician, confirmAssignment, createShift, listAvailability, listShiftChanges, listShifts, publishShift, requestShiftChange, reviewShiftChange, saveAvailability, type Shift, type ShiftChange } from '../api/scheduling'
import { apiErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export default function SchedulePage() {
  const { user } = useAuth()
  const clinician = !!user?.roles.includes('Clinician') && !user.roles.some((role) => ['Administrator', 'Recruiter', 'FacilityManager'].includes(role))
  const canCreate = !!user?.roles.some((role) => ['Administrator', 'FacilityManager', 'HospitalAdministrator'].includes(role))
  const canReview = canCreate || !!user?.roles.includes('Recruiter')
  const [shifts, setShifts] = useState<Shift[] | null>(null)
  const [changes, setChanges] = useState<ShiftChange[]>([])
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [facilityId, setFacilityId] = useState('')
  const [locationId, setLocationId] = useState('')
  const [requisitionId, setRequisitionId] = useState('')
  const [specialty, setSpecialty] = useState('ICU')
  const [startLocal, setStartLocal] = useState('')
  const [endLocal, setEndLocal] = useState('')
  const [assignmentId, setAssignmentId] = useState('')
  const [availableFrom, setAvailableFrom] = useState('')
  const [availableUntil, setAvailableUntil] = useState('')
  const [reason, setReason] = useState('')
  const [facilities, setFacilities] = useState<{ id: string; name: string }[]>([])
  const [locations, setLocations] = useState<{ id: string; locationName: string }[]>([])
  const [requisitions, setRequisitions] = useState<{ id: string; jobTitle: string }[]>([])

  useEffect(() => {
    let active = true
    listShifts({ pageSize: 20 })
      .then((page) => {
        if (active) setShifts(page.items)
      })
      .catch((caught) => {
        if (active) setError(apiErrorMessage(caught, 'The schedule could not be loaded.'))
      })
    if (canReview) {
      listShiftChanges().then((items) => {
        if (active) setChanges(items)
      }).catch(() => undefined)
    }
    if (clinician) {
      listAvailability().catch(() => undefined)
    }
    if (canCreate) {
      listFacilities().then((page) => {
        if (active) setFacilities(page.items)
      }).catch(() => undefined)
    }
    return () => {
      active = false
    }
  }, [canCreate, canReview, clinician])

  useEffect(() => {
    if (!facilityId) return undefined
    let active = true
    listLocations(facilityId).then((items) => {
      if (active) setLocations(items)
    }).catch(() => undefined)
    listRequisitions({ facilityId, pageSize: 20 }).then((page) => {
      if (active) setRequisitions(page.items)
    }).catch(() => undefined)
    return () => {
      active = false
    }
  }, [facilityId])

  async function reload() {
    setShifts((await listShifts({ pageSize: 20 })).items)
    if (canReview) setChanges(await listShiftChanges())
  }

  async function onCreate() {
    if (!startLocal || !endLocal || endLocal <= startLocal) {
      setError('The shift end must be after the start.')
      return
    }
    if (!facilityId || !locationId || !requisitionId || !specialty.trim()) {
      setError('Choose a facility, location, requisition, and specialty.')
      return
    }
    try {
      const created = await createShift({
        facilityId,
        facilityLocationId: locationId,
        jobRequisitionId: requisitionId,
        startLocal,
        endLocal,
        requiredClinicians: 1,
        specialty,
      })
      await publishShift(created.id)
      setMessage('Shift published.')
      setError(null)
      await reload()
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The shift could not be saved.'))
    }
  }

  async function onAssign(shiftId: string) {
    try {
      await assignClinician(shiftId, assignmentId.trim())
      setMessage('Clinician assigned.')
      setError(null)
      await reload()
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The assignment was rejected.'))
    }
  }

  async function onAvailability() {
    if (!availableFrom || !availableUntil || availableUntil <= availableFrom) {
      setError('Enter an availability window that ends after it starts.')
      return
    }
    try {
      await saveAvailability({ availableFrom, availableUntil, availabilityType: 'Available', notes: '' })
      setMessage('Availability saved.')
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Availability could not be saved.'))
    }
  }

  const openShifts = (shifts ?? []).filter((shift) => shift.assignedClinicians < shift.requiredClinicians && shift.status !== 'Cancelled')
  const filledShifts = (shifts ?? []).filter((shift) => shift.assignedClinicians >= shift.requiredClinicians)

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">{clinician ? 'My schedule' : 'Scheduling'}</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      {!shifts && !error && <Typography>Loading schedule…</Typography>}
      {shifts?.length === 0 && <Typography>No shifts are scheduled.</Typography>}
      <Typography>Open shifts {openShifts.length} · Filled shifts {filledShifts.length}</Typography>
      {shifts?.map((shift) => (
        <Stack key={shift.id} spacing={0.5} sx={{ border: 1, borderColor: 'divider', borderRadius: 2, p: 2 }}>
          <Typography>{shift.specialty} · {shift.facilityName} · {shift.locationName}</Typography>
          <Typography>{shift.startLocal} – {shift.endLocal} {shift.timeZoneId}</Typography>
          <Typography>{shift.assignedClinicians} of {shift.requiredClinicians} assigned · {shift.confirmedClinicians} confirmed · {shift.status}{shift.assignedClinicians < shift.requiredClinicians ? ' · Shortage' : ' · Covered'}</Typography>
          {canReview && shift.status !== 'Cancelled' && (
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
              <TextField label="Clinician profile" value={assignmentId} onChange={(event) => setAssignmentId(event.target.value)} />
              <Button variant="contained" onClick={() => onAssign(shift.id)} sx={{ alignSelf: 'center' }}>Assign clinician</Button>
            </Stack>
          )}
        </Stack>
      ))}
      {canCreate && (
        <Stack spacing={2}>
          <Typography variant="h5" component="h2">New shift</Typography>
          <TextField label="Facility" select value={facilityId} onChange={(event) => setFacilityId(event.target.value)}>
            {facilities.map((facility) => <MenuItem key={facility.id} value={facility.id}>{facility.name}</MenuItem>)}
          </TextField>
          <TextField label="Location" select value={locationId} onChange={(event) => setLocationId(event.target.value)}>
            {locations.map((location) => <MenuItem key={location.id} value={location.id}>{location.locationName}</MenuItem>)}
          </TextField>
          <TextField label="Requisition" select value={requisitionId} onChange={(event) => setRequisitionId(event.target.value)}>
            {requisitions.map((requisition) => <MenuItem key={requisition.id} value={requisition.id}>{requisition.jobTitle}</MenuItem>)}
          </TextField>
          <TextField label="Shift specialty" value={specialty} onChange={(event) => setSpecialty(event.target.value)} />
          <TextField label="Start" type="datetime-local" value={startLocal} onChange={(event) => setStartLocal(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField label="End" type="datetime-local" value={endLocal} onChange={(event) => setEndLocal(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <Button variant="contained" onClick={onCreate} sx={{ alignSelf: 'flex-start' }}>Save shift</Button>
        </Stack>
      )}
      {clinician && (
        <Stack spacing={2}>
          <Typography variant="h5" component="h2">My availability</Typography>
          <TextField label="Available from" type="datetime-local" value={availableFrom} onChange={(event) => setAvailableFrom(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField label="Available until" type="datetime-local" value={availableUntil} onChange={(event) => setAvailableUntil(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <Button variant="outlined" onClick={onAvailability} sx={{ alignSelf: 'flex-start' }}>Save availability</Button>
          <TextField label="Assignment" value={assignmentId} onChange={(event) => setAssignmentId(event.target.value)} />
          <TextField label="Change reason" value={reason} onChange={(event) => setReason(event.target.value)} />
          <Button variant="outlined" onClick={async () => {
            if (!reason.trim() || !assignmentId.trim()) {
              setError('Enter the assignment and a reason for the change.')
              return
            }
            try {
              await requestShiftChange({ shiftAssignmentId: assignmentId.trim(), requestType: 'Cancel', reason })
              setMessage('Shift change requested.')
              setError(null)
            } catch (caught) {
              setError(apiErrorMessage(caught, 'The change request was rejected.'))
            }
          }} sx={{ alignSelf: 'flex-start' }}>Request shift change</Button>
          <Button variant="contained" onClick={async () => {
            try {
              await confirmAssignment(assignmentId.trim())
              setMessage('Assignment confirmed.')
              setError(null)
              await reload()
            } catch (caught) {
              setError(apiErrorMessage(caught, 'The assignment could not be confirmed.'))
            }
          }} sx={{ alignSelf: 'flex-start' }}>Confirm assignment</Button>
        </Stack>
      )}
      {canReview && changes.filter((item) => item.status === 'Pending').map((item) => (
        <Stack key={item.id} direction={{ xs: 'column', sm: 'row' }} spacing={1}>
          <Typography sx={{ flexGrow: 1 }}>{item.requestType} · {item.reason}</Typography>
          <Button onClick={() => reviewShiftChange(item.id, 'Approved').then(() => reload())}>Approve request</Button>
          <Button onClick={() => reviewShiftChange(item.id, 'Rejected').then(() => reload())}>Reject request</Button>
        </Stack>
      ))}
    </Stack>
  )
}
