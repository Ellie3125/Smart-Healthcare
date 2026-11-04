import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export function ForbiddenPage() {
  const { user } = useAuth();

  return (
    <div className="min-h-screen bg-[#F7FAF8] flex flex-col items-center justify-center px-4">
      <div className="w-full max-w-md bg-white rounded-xl border border-[#DDE7E1] shadow-sm p-8 text-center">
        <div className="w-16 h-16 rounded-full bg-[#FDE8E8] flex items-center justify-center mx-auto mb-4">
          <span className="material-symbols-outlined text-[#D32F2F] text-[32px]">block</span>
        </div>
        <h1 className="text-[22px] font-semibold text-[#1C2B24] mb-2">Không có quyền truy cập</h1>
        <p className="text-[14px] text-[#5E6F66] mb-6">
          Tài khoản <span className="font-semibold text-[#1C2B24]">{user?.userName}</span> không
          có quyền truy cập trang này.
        </p>
        <div className="flex flex-col gap-3">
          <Link
            to="/"
            className="inline-flex items-center justify-center gap-2 py-2.5 px-4 rounded-lg bg-[#006a41] text-white text-[14px] font-medium hover:bg-[#005231] transition-colors"
          >
            <span className="material-symbols-outlined text-[18px]">home</span>
            Về trang chủ
          </Link>
          <Link
            to="/login"
            className="inline-flex items-center justify-center gap-2 py-2.5 px-4 rounded-lg border border-[#DDE7E1] text-[#5E6F66] text-[14px] font-medium hover:bg-[#F1F4F2] transition-colors"
          >
            <span className="material-symbols-outlined text-[18px]">logout</span>
            Đăng nhập tài khoản khác
          </Link>
        </div>
      </div>
    </div>
  );
}

