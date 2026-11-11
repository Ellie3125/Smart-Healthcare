import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export const ForbiddenPage: React.FC = () => {
  const { user, logout } = useAuth();

  return (
    <div className="min-h-screen bg-[#F7FAF8] flex items-center justify-center p-4">
      <div className="max-w-md w-full bg-white rounded-xl border border-[#DDE7E1] p-8 text-center shadow-sm">
        <div className="w-16 h-16 mx-auto mb-4 rounded-full bg-[#FDE8E8] flex items-center justify-center text-[#D32F2F]">
          <span className="material-symbols-outlined text-[36px]">block</span>
        </div>
        <h1 className="font-headline-md text-headline-md font-bold text-[#1C2B24] mb-2">
          403 - Truy cập bị từ chối
        </h1>
        <p className="font-body-md text-body-md text-[#5E6F66] mb-6">
          Tài khoản <strong>{user?.userName || 'hiện tại'}</strong> (vai trò: <code>{user?.role}</code>) không có quyền truy cập vào phân hệ này.
        </p>
        <div className="flex flex-col gap-2">
          <Link
            to="/login"
            className="w-full h-[40px] rounded-lg bg-[#006a41] text-white font-label-md font-medium flex items-center justify-center hover:bg-[#005231] transition-colors"
          >
            Quay lại trang Đăng nhập
          </Link>
          <button
            onClick={logout}
            className="w-full h-[40px] rounded-lg border border-[#DDE7E1] text-[#1C2B24] font-label-md font-medium hover:bg-[#F1F4F2] transition-colors"
          >
            Đăng xuất
          </button>
        </div>
      </div>
    </div>
  );
};

