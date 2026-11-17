import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export const ForbiddenPage: React.FC = () => {
  const { user, logout } = useAuth();

  return (
    <div className="min-h-screen bg-surface flex items-center justify-center p-4">
      <div className="max-w-md w-full bg-surface-container-lowest rounded-2xl p-8 text-center shadow-[0_12px_32px_rgba(15,31,24,0.06)]">
        <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-error-container flex items-center justify-center text-error">
          <span className="material-symbols-outlined text-[36px]">block</span>
        </div>
        <h1 className="font-headline-md text-xl font-extrabold tracking-tight text-on-surface mb-2">
          403 - Truy cập bị từ chối
        </h1>
        <p className="font-body-md text-sm text-on-surface-variant mb-6 leading-relaxed">
          Tài khoản <strong className="text-on-surface">{user?.userName || 'hiện tại'}</strong> (vai trò: <code className="rounded bg-surface-container-low px-1.5 py-0.5 text-xs">{user?.role}</code>) không có quyền truy cập vào phân hệ này.
        </p>
        <div className="flex flex-col gap-2.5">
          <Link
            to="/login"
            className="w-full h-[42px] rounded-xl bg-primary text-white font-semibold text-sm flex items-center justify-center hover:bg-[#005231] transition-colors"
          >
            Quay lại trang Đăng nhập
          </Link>
          <button
            onClick={logout}
            className="w-full h-[42px] rounded-xl bg-surface-container-low text-on-surface font-semibold text-sm hover:bg-surface-container-high transition-colors"
          >
            Đăng xuất
          </button>
        </div>
      </div>
    </div>
  );
};

