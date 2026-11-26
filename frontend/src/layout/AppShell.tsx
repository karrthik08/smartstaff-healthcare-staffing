import LocalHospitalIcon from '@mui/icons-material/LocalHospital'
import {
  AppBar,
  Box,
  Drawer,
  List,
  ListItemButton,
  ListItemText,
  Toolbar,
  Typography,
} from '@mui/material'
import { Route, Routes } from 'react-router-dom'
import DashboardPage from '../pages/DashboardPage'

const drawerWidth = 248

export default function AppShell() {
  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="fixed" elevation={0} sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
        <Toolbar>
          <LocalHospitalIcon sx={{ mr: 1.5 }} />
          <Box>
            <Typography variant="h6" component="div" sx={{ lineHeight: 1.2 }}>
              SmartStaff
            </Typography>
            <Typography variant="caption" sx={{ opacity: 0.85 }}>
              Healthcare staffing
            </Typography>
          </Box>
        </Toolbar>
      </AppBar>

      <Drawer
        variant="permanent"
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: { width: drawerWidth, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        <List>
          <ListItemButton selected>
            <ListItemText primary="Dashboard" />
          </ListItemButton>
        </List>
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, p: { xs: 2, md: 4 } }}>
        <Toolbar />
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="*" element={<DashboardPage />} />
        </Routes>
      </Box>
    </Box>
  )
}
