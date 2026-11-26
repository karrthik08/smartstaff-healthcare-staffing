import { CssBaseline, ThemeProvider } from '@mui/material'
import { lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import ProtectedRoute from './auth/ProtectedRoute'
import AppShell from './layout/AppShell'
import { theme } from './theme'

const AdminDashboardPage = lazy(() => import('./pages/AdminDashboardPage'))
const DashboardPage = lazy(() => import('./pages/DashboardPage'))
const ApplicationDetailPage = lazy(() => import('./pages/ApplicationDetailPage'))
const ApplicationsPage = lazy(() => import('./pages/ApplicationsPage'))
const CandidatesPage = lazy(() => import('./pages/CandidatesPage'))
const ClinicianProfilePage = lazy(() => import('./pages/ClinicianProfilePage'))
const FacilitiesPage = lazy(() => import('./pages/FacilitiesPage'))
const PlacementsPage = lazy(() => import('./pages/PlacementsPage'))
const ForgotPasswordPage = lazy(() => import('./pages/ForgotPasswordPage'))
const LoginPage = lazy(() => import('./pages/LoginPage'))
const ProfilePage = lazy(() => import('./pages/ProfilePage'))
const RegisterPage = lazy(() => import('./pages/RegisterPage'))
const RequisitionDetailPage = lazy(() => import('./pages/RequisitionDetailPage'))
const RequisitionsPage = lazy(() => import('./pages/RequisitionsPage'))
const ResetPasswordPage = lazy(() => import('./pages/ResetPasswordPage'))
const RoleDashboardPage = lazy(() => import('./pages/RoleDashboardPage'))
const SchedulePage = lazy(() => import('./pages/SchedulePage'))
const AttendancePage = lazy(() => import('./pages/AttendancePage'))
const NotificationsPage = lazy(() => import('./pages/NotificationsPage'))
const AnalyticsPage = lazy(() => import('./pages/AnalyticsPage'))
const UnauthorizedPage = lazy(() => import('./pages/UnauthorizedPage'))

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
            <Route path="/schedule" element={<ProtectedRoute><SchedulePage /></ProtectedRoute>} />
            <Route path="/attendance" element={<ProtectedRoute><AttendancePage /></ProtectedRoute>} />
            <Route path="/notifications" element={<ProtectedRoute><NotificationsPage /></ProtectedRoute>} />
            <Route path="/analytics" element={<ProtectedRoute><AnalyticsPage /></ProtectedRoute>} />
            <Route path="/profile" element={<ProtectedRoute><ProfilePage /></ProtectedRoute>} />
          </Route>
        </Routes>
      </AuthProvider>
    </ThemeProvider>
  )
}
