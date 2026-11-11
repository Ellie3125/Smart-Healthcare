import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { ProfilesPage } from './pages/ProfilesPage';
import { EditProfilePage } from './pages/EditProfilePage';
import { AddRelativePage } from './pages/AddRelativePage';
import { VerificationStatusPage } from './pages/VerificationStatusPage';
import { PatientLayout } from './layouts/PatientLayout';
import { RequirePatientAuth } from './components/RequirePatientAuth';
import { useAuth } from './hooks/useAuth';

export const App: React.FC = () => {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-[#F7FAF8]">
        <div className="flex flex-col items-center gap-3">
          <div className="w-8 h-8 border-3 border-[#006a41] border-t-transparent rounded-full animate-spin"></div>
          <span className="text-sm font-medium text-[#5E6F66]">Đang khởi tạo hệ thống...</span>
        </div>
      </div>
    );
  }

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route
        element={
          <RequirePatientAuth>
            <PatientLayout />
          </RequirePatientAuth>
        }
      >
        <Route path="/profiles" element={<ProfilesPage />} />
        <Route path="/profiles/new" element={<EditProfilePage />} />
        <Route path="/profiles/:id/edit" element={<EditProfilePage />} />
        <Route path="/profiles/add-relative" element={<AddRelativePage />} />
        <Route path="/profiles/:id/verification" element={<VerificationStatusPage />} />
      </Route>

      <Route
        path="/"
        element={<Navigate to={isAuthenticated ? '/profiles' : '/login'} replace />}
      />
      <Route
        path="*"
        element={<Navigate to={isAuthenticated ? '/profiles' : '/login'} replace />}
      />
    </Routes>
  );
};
