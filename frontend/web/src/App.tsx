import { Navigate } from 'react-router-dom';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { RequireRole } from './components/RequireRole';
import { StaffLayout } from './layouts/StaffLayout';
import { getRoleHomePath } from './layouts/menus';
import { LoginPage } from './pages/LoginPage';
import { ForbiddenPage } from './pages/ForbiddenPage';
import {
  ReceptionHomePage,
  ReceptionCheckInPage,
  ReceptionAppointmentsPage,
} from './pages/reception/ReceptionPages';
import {
  NurseHomePage,
  NurseVitalsPage,
  NurseQueuePage,
} from './pages/nurse/NursePages';
import {
  DoctorHomePage,
  DoctorQueuePage,
  DoctorRecordsPage,
} from './pages/doctor/DoctorPages';
import {
  RoomHomePage,
  RoomTicketsPage,
  RoomUploadPage,
} from './pages/room/RoomPages';
import {
  AdminHomePage,
  AdminUsersPage,
  AdminCatalogPage,
} from './pages/admin/AdminPages';

/** Chuyển hướng từ / về trang chủ theo role */
function RootRedirect() {
  const { isAuthenticated, user } = useAuth();
  if (!isAuthenticated || !user) return <Navigate to="/login" replace />;
  return <Navigate to={getRoleHomePath(user.role)} replace />;
}

function AppRoutes() {
  return (
    <Routes>
      {/* Public */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forbidden" element={<ForbiddenPage />} />
      <Route path="/" element={<RootRedirect />} />

      {/* Lễ tân */}
      <Route
        path="/reception/*"
        element={
          <RequireRole allowedRoles={['Receptionist']}>
            <StaffLayout>
              <Routes>
                <Route index element={<ReceptionHomePage />} />
                <Route path="check-in" element={<ReceptionCheckInPage />} />
                <Route path="appointments" element={<ReceptionAppointmentsPage />} />
              </Routes>
            </StaffLayout>
          </RequireRole>
        }
      />

      {/* Điều dưỡng */}
      <Route
        path="/nurse/*"
        element={
          <RequireRole allowedRoles={['Nurse']}>
            <StaffLayout>
              <Routes>
                <Route index element={<NurseHomePage />} />
                <Route path="vitals" element={<NurseVitalsPage />} />
                <Route path="queue" element={<NurseQueuePage />} />
              </Routes>
            </StaffLayout>
          </RequireRole>
        }
      />

      {/* Bác sĩ */}
      <Route
        path="/doctor/*"
        element={
          <RequireRole allowedRoles={['Doctor']}>
            <StaffLayout>
              <Routes>
                <Route index element={<DoctorHomePage />} />
                <Route path="queue" element={<DoctorQueuePage />} />
                <Route path="records" element={<DoctorRecordsPage />} />
              </Routes>
            </StaffLayout>
          </RequireRole>
        }
      />

      {/* Phòng CLS */}
      <Route
        path="/room/*"
        element={
          <RequireRole allowedRoles={['RoomAccount']}>
            <StaffLayout>
              <Routes>
                <Route index element={<RoomHomePage />} />
                <Route path="tickets" element={<RoomTicketsPage />} />
                <Route path="upload" element={<RoomUploadPage />} />
              </Routes>
            </StaffLayout>
          </RequireRole>
        }
      />

      {/* Quản trị */}
      <Route
        path="/admin/*"
        element={
          <RequireRole allowedRoles={['Admin']}>
            <StaffLayout>
              <Routes>
                <Route index element={<AdminHomePage />} />
                <Route path="users" element={<AdminUsersPage />} />
                <Route path="catalog" element={<AdminCatalogPage />} />
              </Routes>
            </StaffLayout>
          </RequireRole>
        }
      />

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  );
}

