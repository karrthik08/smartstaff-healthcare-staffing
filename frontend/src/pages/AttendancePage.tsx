import { Alert, Button, Stack, TextField, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { apiErrorMessage } from '../api/client'
import { addTimesheetEntry, approveTimesheet, clockIn, clockOut, createTimesheet, listAttendance, listTimesheets, rejectTimesheet, requestAdjustment, submitTimesheet, timesheetHistory, timesheetSummary, type ApprovalHistory, type Attendance, type Timesheet, type TimesheetSummary } from '../api/timekeeping'
import { useAuth } from '../auth/useAuth'

export default function AttendancePage() {
  const { user } = useAuth()
  const clinician = !!user?.roles.includes('Clinician') && !user.roles.some((role) => ['Administrator', 'Recruiter', 'FacilityManager'].includes(role))
  const canApprove = !!user?.roles.some((role) => ['Administrator', 'FacilityManager', 'HospitalAdministrator'].includes(role))
  const staff = canApprove || !!user?.roles.includes('Recruiter')
  const [attendance, setAttendance] = useState<Attendance[] | null>(null)
  const [timesheets, setTimesheets] = useState<Timesheet[] | null>(null)
  const [summary, setSummary] = useState<TimesheetSummary | null>(null)
  const [history, setHistory] = useState<ApprovalHistory[]>([])
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [assignmentId, setAssignmentId] = useState('')
  const [clockInAt, setClockInAt] = useState('')
  const [clockOutAt, setClockOutAt] = useState('')
  const [facilityId, setFacilityId] = useState('')
  const [periodStart, setPeriodStart] = useState('')
  const [periodEnd, setPeriodEnd] = useState('')
  const [timesheetId, setTimesheetId] = useState('')
  const [entryAssignmentId, setEntryAssignmentId] = useState('')
  const [startTime, setStartTime] = useState('')
  const [endTime, setEndTime] = useState('')
  const [breakMinutes, setBreakMinutes] = useState('0')
  const [reason, setReason] = useState('')
  const [entryId, setEntryId] = useState('')
  const [adjustedHours, setAdjustedHours] = useState('')

  useEffect(() => {
    let active = true
    listAttendance()
      .then((items) => {
        if (active) setAttendance(items)
      })
      .catch((caught) => {
        if (active) setError(apiErrorMessage(caught, 'Attendance could not be loaded.'))
      })
    listTimesheets()
      .then((page) => {
        if (active) setTimesheets(page.items)
      })
      .catch((caught) => {
        if (active) setError(apiErrorMessage(caught, 'Attendance could not be loaded.'))
      })
    if (staff) {
      timesheetSummary().then((value) => {
        if (active) setSummary(value)
      }).catch(() => undefined)
    }
    return () => {
      active = false
    }
  }, [staff])

  async function reload() {
    setAttendance(await listAttendance())
    setTimesheets((await listTimesheets()).items)
    if (staff) setSummary(await timesheetSummary())
  }

  async function run(action: () => Promise<void>, success: string) {
    try {
      await action()
      setMessage(success)
      setError(null)
      await reload()
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The attendance request was rejected.'))
    }
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">{clinician ? 'My attendance' : 'Attendance and timesheets'}</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      {!attendance && !timesheets && !error && <Typography>Loading attendance…</Typography>}
      {summary && (
        <Typography>
          Submitted {summary.submitted} · Approved {summary.approved} · Rejected {summary.rejected} · Open attendance {summary.openAttendance} · Missing timesheets {summary.missingTimesheets}
        </Typography>
      )}
      {summary && summary.missingTimesheets > 0 && <Alert severity="warning">{summary.missingTimesheets} completed shift{summary.missingTimesheets === 1 ? '' : 's'} still {summary.missingTimesheets === 1 ? 'needs' : 'need'} a timesheet.</Alert>}
      {attendance?.length === 0 && timesheets?.length === 0 && <Typography>No attendance or timesheets yet.</Typography>}
      {attendance?.map((record) => (
        <Stack key={record.id} spacing={0.5} sx={{ border: 1, borderColor: 'divider', borderRadius: 2, p: 2 }}>
          <Typography>{record.clinicianName} · {record.attendanceStatus}</Typography>
          <Typography>{record.clockInLocal}{record.clockOutLocal ? ` – ${record.clockOutLocal}` : ' · still clocked in'} {record.timeZoneId}</Typography>
        </Stack>
      ))}
      {timesheets?.map((sheet) => (
        <Stack key={sheet.id} spacing={1} sx={{ border: 1, borderColor: 'divider', borderRadius: 2, p: 2 }}>
          <Typography>{sheet.clinicianName} · {sheet.facilityName}</Typography>
          <Typography>{sheet.periodStart} – {sheet.periodEnd} · {sheet.status}</Typography>
          <Typography>Regular {sheet.totalRegularHours} · Overtime {sheet.totalOvertimeHours}</Typography>
          {sheet.entries.map((entry) => (
            <Typography key={entry.id}>Entry {entry.id} · {entry.regularHours} regular · {entry.overtimeHours} overtime · break {entry.breakMinutes}</Typography>
          ))}
          {canApprove && sheet.status === 'Submitted' && (
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
              <Button variant="contained" onClick={() => run(() => approveTimesheet(sheet.id).then(() => undefined), 'Timesheet approved.')}>Approve timesheet</Button>
              <TextField label="Rejection reason" value={reason} onChange={(event) => setReason(event.target.value)} />
              <Button onClick={() => {
                if (!reason.trim()) {
                  setError('A rejection reason is required.')
                  return
                }
                void run(() => rejectTimesheet(sheet.id, reason).then(() => undefined), 'Timesheet rejected.')
              }}>Reject timesheet</Button>
            </Stack>
          )}
          {staff && (
            <Button onClick={() => timesheetHistory(sheet.id).then(setHistory).catch((caught) => setError(apiErrorMessage(caught, 'Approval history could not be loaded.')))}>View approval history</Button>
          )}
        </Stack>
      ))}
      {history.map((item) => (
        <Typography key={item.id}>{item.previousStatus} → {item.newStatus} · {item.comments}</Typography>
      ))}
      {clinician && (
        <Stack spacing={2}>
          <Typography variant="h5" component="h2">Clock in and out</Typography>
          <TextField label="Assignment" value={assignmentId} onChange={(event) => setAssignmentId(event.target.value)} />
          <TextField label="Clock in time" type="datetime-local" value={clockInAt} onChange={(event) => setClockInAt(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} helperText="Facility local time. The server stores UTC and rejects a missing or repeated daylight-saving hour." />
          <TextField label="Clock out time" type="datetime-local" value={clockOutAt} onChange={(event) => setClockOutAt(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} helperText="Facility local time. The server stores UTC and rejects a missing or repeated daylight-saving hour." />
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
            <Button variant="contained" onClick={() => {
              if (!assignmentId.trim() || !clockInAt) {
                setError('Enter the assignment and clock-in time.')
                return
              }
              void run(() => clockIn(assignmentId.trim(), clockInAt).then(() => undefined), 'Clocked in.')
            }}>Clock in</Button>
            <Button variant="outlined" onClick={() => {
              if (!clockInAt || !clockOutAt || clockOutAt <= clockInAt) {
                setError('Enter a clock-out after the clock-in.')
                return
              }
              void run(() => clockOut(assignmentId.trim(), clockOutAt).then(() => undefined), 'Clocked out.')
            }}>Clock out</Button>
          </Stack>
          <Typography variant="h5" component="h2">Timesheet</Typography>
          <TextField label="Facility" value={facilityId} onChange={(event) => setFacilityId(event.target.value)} />
          <TextField label="Period start" type="date" value={periodStart} onChange={(event) => setPeriodStart(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField label="Period end" type="date" value={periodEnd} onChange={(event) => setPeriodEnd(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <Button variant="contained" onClick={() => run(async () => {
            const created = await createTimesheet({ facilityId: facilityId.trim(), periodStart, periodEnd })
            setTimesheetId(created.id)
          }, 'Timesheet saved.')} sx={{ alignSelf: 'flex-start' }}>Create timesheet</Button>
          <TextField label="Timesheet" value={timesheetId} onChange={(event) => setTimesheetId(event.target.value)} />
          <TextField label="Entry assignment" value={entryAssignmentId} onChange={(event) => setEntryAssignmentId(event.target.value)} />
          <TextField label="Start" type="datetime-local" value={startTime} onChange={(event) => setStartTime(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} helperText="UTC. Timesheet hours are sent with a UTC offset." />
          <TextField label="End" type="datetime-local" value={endTime} onChange={(event) => setEndTime(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} helperText="UTC. Timesheet hours are sent with a UTC offset." />
          <TextField label="Break minutes" value={breakMinutes} onChange={(event) => setBreakMinutes(event.target.value)} />
          <Button variant="outlined" onClick={() => {
            if (!startTime || !endTime || endTime <= startTime) {
              setError('Enter hours that end after they start.')
              return
            }
            void run(() => addTimesheetEntry(timesheetId.trim(), {
              shiftAssignmentId: entryAssignmentId.trim(),
              startTime,
              endTime,
              breakMinutes: Number(breakMinutes) || 0,
              notes: '',
            }).then((sheet) => {
              setTimesheetId(sheet.id)
            }), 'Hours recorded.')
          }} sx={{ alignSelf: 'flex-start' }}>Add hours</Button>
          <Button variant="contained" onClick={() => run(() => submitTimesheet(timesheetId.trim()).then(() => undefined), 'Timesheet submitted.')} sx={{ alignSelf: 'flex-start' }}>Submit timesheet</Button>
          <Typography variant="h5" component="h2">Correct an approved entry</Typography>
          <TextField label="Entry" value={entryId} onChange={(event) => setEntryId(event.target.value)} />
          <TextField label="Adjusted hours" value={adjustedHours} onChange={(event) => setAdjustedHours(event.target.value)} />
          <TextField label="Adjustment reason" value={reason} onChange={(event) => setReason(event.target.value)} />
          <Button variant="outlined" onClick={() => run(() => requestAdjustment({ timesheetEntryId: entryId.trim(), adjustedHours: Number(adjustedHours), reason }).then(() => undefined), 'Adjustment requested.')} sx={{ alignSelf: 'flex-start' }}>Request adjustment</Button>
        </Stack>
      )}
    </Stack>
  )
}
