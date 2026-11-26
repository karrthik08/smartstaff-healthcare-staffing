import { CssBaseline, ThemeProvider } from '@mui/material'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import ProtectedRoute from './auth/ProtectedRoute'
import AppShell from './layout/AppShell'
import AdminDashboardPage from './pages/AdminDashboardPage'
import DashboardPage from './pages/DashboardPage'
import ApplicationDetailPage from './pages/ApplicationDetailPage'
import ApplicationsPage from './pages/ApplicationsPage'
import CandidatesPage from './pages/CandidatesPage'
import ClinicianProfilePage from './pages/ClinicianProfilePage'
import FacilitiesPage from './pages/FacilitiesPage'
import PlacementsPage from './pages/PlacementsPage'
import ForgotPasswordPage from './pages/ForgotPasswordPage'
import LoginPage from './pages/LoginPage'
import ProfilePage from './pages/ProfilePage'
import RegisterPage from './pages/RegisterPage'
import RequisitionDetailPage from './pages/RequisitionDetailPage'
import RequisitionsPage from './pages/RequisitionsPage'
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
            <Route path="/dashboard/recruiter" element={<ProtectedRoute roles={['Administrator', 'Recruiter', 'StaffingCoordinator', 'Staffing Coordinator']}><RoleDashboardPage workspace="recruiter" /></ProtectedRoute>} />
            <Route path="/dashboard/coordinator" element={<Navigate to="/dashboard/recruiter" replace />} />
            <Route path="/dashboard/clinician" element={<ProtectedRoute roles={['Administrator', 'Clinician']}><RoleDashboardPage workspace="clinician" /></ProtectedRoute>} />
            <Route path="/dashboard/facility" element={<ProtectedRoute roles={['Administrator', 'FacilityManager', 'HospitalAdministrator', 'Hospital Administrator']}><RoleDashboardPage workspace="facility" /></ProtectedRoute>} />
            <Route path="/dashboard/hospital" element={<Navigate to="/dashboard/facility" replace />} />
            <Route path="/facilities" element={<ProtectedRoute roles={['Administrator', 'FacilityManager', 'HospitalAdministrator', 'Hospital Administrator', 'Recruiter', 'StaffingCoordinator', 'Staffing Coordinator']}><FacilitiesPage /></ProtectedRoute>} />
            <Route path="/requisitions" element={<ProtectedRoute><RequisitionsPage /></ProtectedRoute>} />
            <Route path="/requisitions/:id" element={<ProtectedRoute><RequisitionDetailPage /></ProtectedRoute>} />
            <Route path="/clinician-profile" element={<ProtectedRoute roles={['Clinician', 'Administrator']}><ClinicianProfilePage /></ProtectedRoute>} />
            <Route path="/candidates" element={<ProtectedRoute roles={['Administrator', 'Recruiter', 'StaffingCoordinator', 'Staffing Coordinator', 'FacilityManager', 'HospitalAdministrator', 'Hospital Administrator']}><CandidatesPage /></ProtectedRoute>} />
            <Route path="/applications" element={<ProtectedRoute><ApplicationsPage /></ProtectedRoute>} />
            <Route path="/applications/:id" element={<ProtectedRoute><ApplicationDetailPage /></ProtectedRoute>} />
            <Route path="/placements" element={<ProtectedRoute><PlacementsPage /></ProtectedRoute>} />
            <Route path="/profile" element={<ProtectedRoute><ProfilePage /></ProtectedRoute>} />
          </Route>
        </Routes>
      </AuthProvider>
    </ThemeProvider>
  )
}
