import LogoutIcon from '@mui/icons-material/Logout'
import MenuIcon from '@mui/icons-material/Menu'
import { AppBar, Box, Button, Chip, CircularProgress, Drawer, IconButton, List, ListItemButton, ListItemText, Toolbar, Typography, useMediaQuery, useTheme } from '@mui/material'
import { Suspense, useState } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import NotificationBell from './NotificationBell'
import { navigation, roleLabels } from '../auth/roles'

export default function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const theme = useTheme()
  const permanentNav = useMediaQuery(theme.breakpoints.up('md'))
  const [mobileNav, setMobileNav] = useState(false)
  const links = user ? navigation.filter((item) => item.roles.some((role) => user.roles.includes(role))) : []

  async function onLogout() {
    try {
      await logout()
    } finally {
      navigate('/login', { replace: true })
    }
  }

  function openPage(path: string) {
    setMobileNav(false)
    navigate(path)
  }

  const drawer = (
    <Box component="nav" aria-label="Primary">
      <Toolbar />
      <List>
        {links.map((link) => (
          <ListItemButton
            key={`${link.to}-${link.label}`}
            selected={location.pathname === link.to}
            aria-current={location.pathname === link.to ? 'page' : undefined}
            onClick={() => openPage(link.to)}
          >
            <ListItemText primary={link.label} />
          </ListItemButton>
        ))}
      </List>
    </Box>
  )

  return (
    <Box sx={{ display: 'flex', minHeight: '100%' }}>
      <Box component="a" href="#main-content" sx={{ position: 'absolute', left: -9999, '&:focus': { left: 8, top: 8, zIndex: (t) => t.zIndex.tooltip, background: '#fff', color: '#111', p: 1 } }}>
        Skip to content
      </Box>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar>
          {user && !permanentNav && (
            <IconButton color="inherit" edge="start" aria-label="Open navigation" onClick={() => setMobileNav(true)} sx={{ mr: 1 }}>
              <MenuIcon />
            </IconButton>
          )}
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>SmartStaff</Typography>
          {user && (
            <>
              <Typography sx={{ mr: 1.5, minWidth: 0, display: { xs: 'none', sm: 'block' } }} noWrap>
                {user.firstName} {user.lastName}
              </Typography>
              <Chip label={user.roles.map((role) => roleLabels[role] ?? role).join(', ')} size="small" sx={{ mr: 2, display: { xs: 'none', md: 'inline-flex' }, color: 'inherit', borderColor: 'rgba(255,255,255,0.4)' }} variant="outlined" />
              <NotificationBell />
              <Button color="inherit" startIcon={<LogoutIcon />} onClick={onLogout}>Sign out</Button>
            </>
          )}
        </Toolbar>
      </AppBar>
      {user && permanentNav && (
        <Drawer variant="permanent" sx={{ width: 240, flexShrink: 0, '& .MuiDrawer-paper': { width: 240, boxSizing: 'border-box' } }}>
          {drawer}
        </Drawer>
      )}
      {user && !permanentNav && (
        <Drawer variant="temporary" open={mobileNav} onClose={() => setMobileNav(false)} ModalProps={{ keepMounted: true }} sx={{ '& .MuiDrawer-paper': { width: 240, boxSizing: 'border-box' } }}>
          {drawer}
        </Drawer>
      )}
      <Box component="main" id="main-content" sx={{ flexGrow: 1, p: { xs: 2, md: 3 }, minWidth: 0 }}>
        <Toolbar />
        <Suspense fallback={<CircularProgress aria-label="Loading page" />}>
          <Outlet />
        </Suspense>
      </Box>
    </Box>
  )
}
