import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { StaffRole } from '../types/auth';

interface RequireRoleProps {
  allow: StaffRole[];
  children: React.ReactElement;
}

export const RequireRole: React.FC<RequireRoleProps> = ({ allow, children }) => {
  const { user, isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background text-[#5E6F66]">
        <div className="flex items-center gap-2">
          <span className="material-symbols-outlined animate-spin text-[#006a41]">progress_activity</span>
          <span>Đang tải thông tin...</span>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace />;
  }

  if (!allow.includes(user.role as StaffRole)) {
    return <Navigate to="/forbidden" replace />;
  }

  return children;
};

