import { Navigate, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { useAuth } from '../contexts/AuthContext';

interface RequireRoleProps {
  readonly children: ReactNode;
  /** Danh sách role được phép. Nếu bỏ trống, chỉ cần authenticated. */
  readonly allowedRoles?: readonly string[];
}

/**
 * Wrapper bảo vệ route theo role.
 * Chặn thật ở Backend; component này chỉ để trải nghiệm người dùng (theo kiến trúc).
 */
export function RequireRole({ children, allowedRoles }: RequireRoleProps) {
  const { isAuthenticated, user } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (allowedRoles && allowedRoles.length > 0 && user) {
    const hasRole = allowedRoles.includes(user.role);
    if (!hasRole) {
      return <Navigate to="/forbidden" replace />;
    }
  }

  return <>{children}</>;
}

