import { Button, Stack, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { primaryPath } from '../auth/roles'

export default function UnauthorizedPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  return (
    <Stack spacing={2} sx={{ maxWidth: 560 }}>
      <Typography variant="h4" component="h1">Unauthorized</Typography>
      <Typography>Your account does not have access to that page.</Typography>
      <Button variant="contained" onClick={() => navigate(user ? primaryPath(user.roles) : '/login')} sx={{ alignSelf: 'flex-start' }}>
        Go to your workspace
      </Button>
    </Stack>
  )
}
