import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export const RequirePatient: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { isAuthenticated, user } = useAuth();

  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace />;
  }

  if (user.role !== 'Patient') {
    return (
      <div className="min-h-screen flex items-center justify-center bg-surface p-4 text-center">
        <div className="max-w-md bg-white p-6 rounded-xl border border-outline-variant shadow-sm">
          <span className="material-symbols-outlined text-[48px] text-tertiary mb-3">gpp_maybe</span>
          <h1 className="text-xl font-bold text-on-surface mb-2">Quyền truy cập không hợp lệ</h1>
          <p className="text-on-surface-variant text-sm mb-4">
            Cổng web này chỉ dành cho tài khoản Bệnh nhân. Tài khoản nhân viên y tế vui lòng truy cập đúng cổng nội bộ.
          </p>
          <a
            href="/login"
            className="inline-block px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-secondary transition-colors"
          >
            Đăng nhập lại
          </a>
        </div>
      </div>
    );
  }

  return <>{children}</>;
};
