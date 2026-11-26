import {
  Alert,
  Button,
  Checkbox,
  FormControlLabel,
  MenuItem,
  Pagination,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material'
import { useEffect, useState } from 'react'
import { listAuditLogs, listUsers, updateUserRoles, updateUserStatus, type AuditEntry, type UserSummary } from '../api/auth'
import { apiErrorMessage } from '../api/client'
import { assignableRoles, hasAssignableRole, roleLabels } from '../auth/roles'
import RoleDashboardPage from './RoleDashboardPage'

export default function AdminDashboardPage() {
  const [users, setUsers] = useState<UserSummary[]>([])
  const [logs, setLogs] = useState<AuditEntry[]>([])
  const [search, setSearch] = useState('')
  const [role, setRole] = useState('')
  const [active, setActive] = useState('')
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const pageSize = 10

  async function load(nextPage = page) {
    const result = await listUsers({
      search,
      role: role || undefined,
      active: active || undefined,
      page: nextPage,
      pageSize,
    })
    setUsers(result.items)
    setTotal(result.total)
    setPage(result.page)
    setLogs(await listAuditLogs())
  }

  useEffect(() => {
    load(1).catch((caught) => setError(apiErrorMessage(caught, 'Administrator data is unavailable.')))
  }, [])

  async function toggleRole(user: UserSummary, roleName: (typeof assignableRoles)[number], checked: boolean) {
    const current = assignableRoles.filter((item) => hasAssignableRole(user.roles, item))
    const next = checked ? [...new Set([...current, roleName])] : current.filter((item) => item !== roleName)
    try {
      const updated = await updateUserRoles(user.id, next)
      setUsers((items) => items.map((item) => (item.id === updated.id ? updated : item)))
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The role change was rejected.'))
    }
  }

  async function toggleActive(user: UserSummary, checked: boolean) {
    try {
      const updated = await updateUserStatus(user.id, checked)
      setUsers((items) => items.map((item) => (item.id === updated.id ? updated : item)))
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The account status change was rejected.'))
    }
  }

  return (
    <Stack spacing={3}>
      <RoleDashboardPage workspace="administrator" />
      {error && <Alert severity="error">{error}</Alert>}
      <Typography variant="h5" component="h2">Users</Typography>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
        <TextField label="Search" value={search} onChange={(event) => setSearch(event.target.value)} fullWidth />
        <TextField label="Role" select value={role} onChange={(event) => setRole(event.target.value)} sx={{ minWidth: { md: 180 } }}>
          <MenuItem value="">All roles</MenuItem>
          {assignableRoles.map((item) => <MenuItem key={item} value={item}>{roleLabels[item]}</MenuItem>)}
        </TextField>
        <TextField label="Status" select value={active} onChange={(event) => setActive(event.target.value)} sx={{ minWidth: { md: 160 } }}>
          <MenuItem value="">All statuses</MenuItem>
          <MenuItem value="true">Active</MenuItem>
          <MenuItem value="false">Inactive</MenuItem>
        </TextField>
        <Button variant="contained" onClick={() => load(1).catch((caught) => setError(apiErrorMessage(caught, 'The user search failed.')))} sx={{ alignSelf: { md: 'center' } }}>
          Search
        </Button>
      </Stack>
      {users.map((user) => (
        <Stack key={user.id} spacing={1} sx={{ border: 1, borderColor: 'divider', borderRadius: 2, p: 2 }}>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ justifyContent: 'space-between' }}>
            <Typography>{user.firstName} {user.lastName} — {user.email}</Typography>
            <FormControlLabel
              control={<Switch checked={user.active} onChange={(event) => toggleActive(user, event.target.checked)} />}
              label={user.active ? 'Active' : 'Inactive'}
            />
          </Stack>
          <Stack direction="row" sx={{ flexWrap: 'wrap' }}>
            {assignableRoles.map((item) => (
              <FormControlLabel
                key={item}
                control={<Checkbox checked={hasAssignableRole(user.roles, item)} onChange={(event) => toggleRole(user, item, event.target.checked)} />}
                label={roleLabels[item]}
              />
            ))}
          </Stack>
        </Stack>
      ))}
      <Pagination
        page={page}
        count={Math.max(1, Math.ceil(total / pageSize))}
        onChange={(_event, value) => load(value).catch((caught) => setError(apiErrorMessage(caught, 'The next page could not be loaded.')))}
      />
      <Typography variant="h5" component="h2">Audit log</Typography>
      {logs.map((entry) => (
        <Typography key={entry.id} variant="body2">
          {new Date(entry.createdAt).toLocaleString()} — {entry.action} — {entry.succeeded ? 'succeeded' : 'failed'}
        </Typography>
      ))}
    </Stack>
  )
}
