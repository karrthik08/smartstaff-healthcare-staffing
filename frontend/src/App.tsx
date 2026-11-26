import { CssBaseline, ThemeProvider } from '@mui/material'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import ProtectedRoute from './auth/ProtectedRoute'
import AppShell from './layout/AppShell'
import AdminDashboardPage from './pages/AdminDashboardPage'
import DashboardPage from './pages/DashboardPage'
import ForgotPasswordPage from './pages/ForgotPasswordPage'
import LoginPage from './pages/LoginPage'
import ProfilePage from './pages/ProfilePage'
import RegisterPage from './pages/RegisterPage'
import ResetPasswordPage from './pages/ResetPasswordPage'
import RoleDashboardPage from './pages/RoleDashboardPage'
import UnauthorizedPage from './pages/UnauthorizedPage'
import { theme } from './theme'

export default function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <AuthProvider>
        <Routes>
          <Route element={<AppShell />}>
            <Route path="/" element={<Navigate to="/login" replace />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/forgot-password" element={<ForgotPasswordPage />} />
            <Route path="/reset-password" element={<ResetPasswordPage />} />
            <Route path="/unauthorized" element={<UnauthorizedPage />} />
            <Route path="/health" element={<DashboardPage />} />
            <Route path="/dashboard/admin" element={<ProtectedRoute roles={['Administrator']}><AdminDashboardPage /></ProtectedRoute>} />
            <Route path="/dashboard/coordinator" element={<ProtectedRoute roles={['Administrator', 'StaffingCoordinator']}><RoleDashboardPage workspace="coordinator" /></ProtectedRoute>} />
            <Route path="/dashboard/clinician" element={<ProtectedRoute roles={['Administrator', 'Clinician']}><RoleDashboardPage workspace="clinician" /></ProtectedRoute>} />
            <Route path="/dashboard/hospital" element={<ProtectedRoute roles={['Administrator', 'HospitalAdministrator']}><RoleDashboardPage workspace="hospital" /></ProtectedRoute>} />
            <Route path="/profile" element={<ProtectedRoute><ProfilePage /></ProtectedRoute>} />
          </Route>
        </Routes>
      </AuthProvider>
    </ThemeProvider>
  )
}
