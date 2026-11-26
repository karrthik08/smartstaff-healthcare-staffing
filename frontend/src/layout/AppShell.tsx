import LogoutIcon from '@mui/icons-material/Logout'
import { AppBar, Box, Button, Chip, Drawer, List, ListItemButton, ListItemText, Toolbar, Typography } from '@mui/material'
import { Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { navigation, roleLabels } from '../auth/roles'

export default function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const links = user ? navigation.filter((item) => item.roles.some((role) => user.roles.includes(role))) : []

  async function onLogout() {
    try {
      await logout()
    } finally {
      navigate('/login', { replace: true })
    }
  }

  return (
    <Box sx={{ display: 'flex', minHeight: '100%' }}>
      <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>SmartStaff</Typography>
          {user && (
            <>
              <Typography sx={{ mr: 1.5, minWidth: 0 }} noWrap>
                {user.firstName} {user.lastName}
              </Typography>
              <Chip label={user.roles.map((role) => roleLabels[role] ?? role).join(', ')} size="small" sx={{ mr: 2, color: 'inherit', borderColor: 'rgba(255,255,255,0.4)' }} variant="outlined" />
              <Button color="inherit" startIcon={<LogoutIcon />} onClick={onLogout}>Sign out</Button>
            </>
          )}
        </Toolbar>
      </AppBar>
      {user && (
        <Drawer variant="permanent" sx={{ width: 240, flexShrink: 0, '& .MuiDrawer-paper': { width: 240, boxSizing: 'border-box' } }}>
          <Toolbar />
          <List>
            {links.map((link) => (
              <ListItemButton key={`${link.to}-${link.label}`} onClick={() => navigate(link.to)}>
                <ListItemText primary={link.label} />
              </ListItemButton>
            ))}
          </List>
        </Drawer>
      )}
      <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  )
}
