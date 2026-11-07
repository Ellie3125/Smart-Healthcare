import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { ForbiddenPage } from './pages/ForbiddenPage';
import { StaffLayout } from './layouts/StaffLayout';
import { RequireRole } from './components/RequireRole';
import { ReceptionPage } from './pages/reception/ReceptionPage';
import { NursePage } from './pages/nurse/NursePage';
import { DoctorPage } from './pages/doctor/DoctorPage';
import { RoomPage } from './pages/room/RoomPage';
import { AdminPage } from './pages/admin/AdminPage';
import { useAuth } from './hooks/useAuth';

export const App: React.FC = () => {
  const { isAuthenticated, user } = useAuth();

  const getDefaultRoute = () => {
    if (!isAuthenticated || !user) return '/login';
    switch (user.role) {
      case 'Receptionist':
        return '/reception';
      case 'Nurse':
        return '/nurse';
      case 'Doctor':
        return '/doctor';
      case 'RoomAccount':
        return '/room';
      case 'Admin':
        return '/admin';
      default:
        return '/forbidden';
    }
  };

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forbidden" element={<ForbiddenPage />} />

      {/* Staff Protected Routes */}
      <Route element={<StaffLayout />}>
        <Route
          path="/reception/*"
          element={
            <RequireRole allow={['Receptionist', 'Admin']}>
              <ReceptionPage />
            </RequireRole>
          }
        />
        <Route
          path="/nurse/*"
          element={
            <RequireRole allow={['Nurse', 'Admin']}>
              <NursePage />
            </RequireRole>
          }
        />
        <Route
          path="/doctor/*"
          element={
            <RequireRole allow={['Doctor', 'Admin']}>
              <DoctorPage />
            </RequireRole>
          }
        />
        <Route
          path="/room/*"
          element={
            <RequireRole allow={['RoomAccount', 'Admin']}>
              <RoomPage />
            </RequireRole>
          }
        />
        <Route
          path="/admin/*"
          element={
            <RequireRole allow={['Admin']}>
              <AdminPage />
            </RequireRole>
          }
        />
      </Route>

      <Route path="/" element={<Navigate to={getDefaultRoute()} replace />} />
      <Route path="*" element={<Navigate to={getDefaultRoute()} replace />} />
    </Routes>
  );
};

