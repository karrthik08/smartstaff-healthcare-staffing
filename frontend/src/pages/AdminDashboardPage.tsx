import { Alert, Checkbox, FormControlLabel, Stack, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { listAuditLogs, listUsers, updateUserRoles, type AuditEntry, type UserSummary } from '../api/auth'
import { apiErrorMessage } from '../api/client'
import RoleDashboardPage from './RoleDashboardPage'

const roles = ['Administrator', 'StaffingCoordinator', 'Clinician', 'HospitalAdministrator']

export default function AdminDashboardPage() {
  const [users, setUsers] = useState<UserSummary[]>([])
  const [logs, setLogs] = useState<AuditEntry[]>([])
  const [error, setError] = useState<string | null>(null)

  async function load() {
    const [nextUsers, nextLogs] = await Promise.all([listUsers(), listAuditLogs()])
    setUsers(nextUsers)
    setLogs(nextLogs)
  }

  useEffect(() => {
    load().catch((caught) => setError(apiErrorMessage(caught, 'Administrator data is unavailable.')))
  }, [])

  async function toggle(user: UserSummary, role: string, checked: boolean) {
    const next = checked ? [...user.roles, role] : user.roles.filter((item) => item !== role)
    try {
      const updated = await updateUserRoles(user.id, next)
      setUsers((current) => current.map((item) => (item.id === updated.id ? updated : item)))
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The role change was rejected.'))
    }
  }

  return (
    <Stack spacing={3}>
      <RoleDashboardPage workspace="administrator" />
      {error && <Alert severity="error">{error}</Alert>}
      <Typography variant="h5" component="h2">Users</Typography>
      {users.map((user) => (
        <Stack key={user.id} spacing={0.5}>
          <Typography>{user.firstName} {user.lastName} — {user.email}</Typography>
          <Stack direction="row" sx={{ flexWrap: 'wrap' }}>
            {roles.map((role) => (
              <FormControlLabel
                key={role}
                control={<Checkbox checked={user.roles.includes(role)} onChange={(event) => toggle(user, role, event.target.checked)} />}
                label={role}
              />
            ))}
          </Stack>
        </Stack>
      ))}
      <Typography variant="h5" component="h2">Audit log</Typography>
      {logs.map((entry) => (
        <Typography key={entry.id} variant="body2">
          {new Date(entry.createdAt).toLocaleString()} — {entry.action} — {entry.succeeded ? 'succeeded' : 'failed'}
        </Typography>
      ))}
    </Stack>
  )
}
