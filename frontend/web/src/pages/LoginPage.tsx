import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { getRoleHomePath } from '../layouts/menus';
import type { ProblemDetails } from '../types/auth';
import axios from 'axios';

type LoginTab = 'patient' | 'staff';

export function LoginPage() {
  const { isAuthenticated, user, loginAsStaff, isLoading } = useAuth();
  const navigate = useNavigate();

  const [tab, setTab] = useState<LoginTab>('staff');
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Đã đăng nhập → chuyển thẳng về trang chủ của role
  if (isAuthenticated && user) {
    return <Navigate to={getRoleHomePath(user.role)} replace />;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!userName.trim() || !password.trim()) {
      setErrorMsg('Vui lòng nhập tên đăng nhập và mật khẩu.');
      return;
    }

    if (tab === 'patient') {
      setErrorMsg('Bệnh nhân vui lòng sử dụng ứng dụng di động Smart Healthcare để đăng nhập.');
      return;
    }

    try {
      await loginAsStaff(userName.trim(), password);
      // AuthContext đã lưu user, giờ điều hướng
      // user sẽ được set sau loginAsStaff, cần đọc lại
      const stored = localStorage.getItem('current_user');
      const u = stored ? JSON.parse(stored) : null;
      if (u) {
        navigate(getRoleHomePath(u.role), { replace: true });
      }
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const status = err.response?.status;
        const problem = err.response?.data as ProblemDetails | undefined;

        if (status === 403) {
          setErrorMsg('Tài khoản bệnh nhân không có quyền truy cập kênh nhân viên. Vui lòng dùng ứng dụng di động.');
        } else if (status === 401) {
          setErrorMsg('Tên đăng nhập hoặc mật khẩu không chính xác. Vui lòng kiểm tra lại.');
        } else {
          setErrorMsg(problem?.detail ?? 'Đã xảy ra lỗi, vui lòng thử lại sau.');
        }
      } else {
        setErrorMsg('Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng.');
      }
    }
  };

  return (
    <div className="min-h-screen bg-[#F7FAF8] flex flex-col justify-center items-center py-8 px-4">
      <div className="w-full max-w-5xl rounded-xl bg-white border border-[#DDE7E1] shadow-[0_4px_24px_-4px_rgba(28,43,36,0.08)] overflow-hidden grid grid-cols-1 lg:grid-cols-12">

        {/* ── Left Panel (desktop only) ── */}
        <aside className="hidden lg:flex lg:col-span-5 bg-[#E8F5EE] p-10 flex-col justify-between border-r border-[#DDE7E1]">
          <div>
            {/* Brand */}
            <div className="flex items-center gap-3">
              <div className="relative w-11 h-11 rounded-lg bg-white border border-[#DDE7E1] flex items-center justify-center shadow-sm">
                <svg className="w-7 h-7 text-[#006a41]" fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.2" viewBox="0 0 32 32">
                  <circle cx="16" cy="16" r="14" className="stroke-[#006a41] stroke-2" />
                  <path d="M16 9v14" className="stroke-[#D32F2F] stroke-[3]" />
                  <path d="M9 16h14" className="stroke-[#D32F2F] stroke-[3]" />
                </svg>
              </div>
              <div>
                <span className="block text-[18px] font-bold text-[#1C2B24] tracking-tight">Smart Healthcare</span>
                <span className="block text-[12px] text-[#5E6F66]">Phòng khám Đa khoa Tiêu chuẩn Quốc tế</span>
              </div>
            </div>

            {/* Tagline */}
            <div className="mt-8">
              <h2 className="text-[20px] font-semibold text-[#1C2B24] leading-snug">
                Đặt lịch khám nhanh, theo dõi hành trình khám rõ ràng
              </h2>
              <p className="mt-2 text-[14px] text-[#5E6F66]">
                Hệ thống quản lý y tế số đồng bộ, kết nối liên tục giữa người bệnh và đội ngũ chuyên gia bác sĩ.
              </p>
            </div>

            {/* Features */}
            <ul className="mt-6 space-y-3.5">
              {[
                'Tiếp nhận bệnh nhân thông minh không chờ đợi',
                'Tra cứu kết quả xét nghiệm & chẩn đoán hình ảnh tức thì',
                'Đồng bộ hồ sơ bệnh án điện tử an toàn, bảo mật',
              ].map((text) => (
                <li key={text} className="flex items-start gap-3">
                  <span className="w-5 h-5 rounded-full bg-white border border-[#DDE7E1] flex items-center justify-center shrink-0 mt-0.5 text-[#006a41]">
                    <span className="material-symbols-outlined text-[14px]">check</span>
                  </span>
                  <span className="text-[14px] text-[#1C2B24] font-medium leading-tight">{text}</span>
                </li>
              ))}
            </ul>
          </div>

          {/* Emergency Line */}
          <div className="mt-8 pt-5 border-t border-[#DDE7E1]/80 flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-full bg-white flex items-center justify-center text-[#D32F2F] border border-[#DDE7E1]">
                <span className="material-symbols-outlined text-[18px]">e911_emergency</span>
              </div>
              <div>
                <span className="block text-[12px] text-[#5E6F66]">Cấp cứu & Hỗ trợ y tế</span>
                <span className="text-[14px] font-bold text-[#1C2B24]">1900 6868</span>
              </div>
            </div>
            <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold bg-white text-[#006a41] border border-[#DDE7E1]">
              24/7 Phục vụ
            </span>
          </div>
        </aside>

        {/* ── Right Panel: Form ── */}
        <section className="lg:col-span-7 bg-white p-6 sm:p-10 lg:p-12 flex flex-col justify-center">
          {/* Mobile brand */}
          <div className="flex lg:hidden items-center gap-2.5 mb-6 pb-4 border-b border-[#DDE7E1]">
            <div className="w-9 h-9 rounded-lg bg-[#E8F5EE] border border-[#DDE7E1] flex items-center justify-center">
              <span className="material-symbols-outlined text-[#006a41] text-[22px]">local_hospital</span>
            </div>
            <div>
              <span className="block text-[15px] font-bold text-[#1C2B24]">Smart Healthcare</span>
              <span className="block text-[11px] text-[#5E6F66]">Phòng khám Đa khoa Tiêu chuẩn Quốc tế</span>
            </div>
          </div>

          {/* Title */}
          <div className="mb-5">
            <h1 className="text-[24px] font-semibold text-[#1C2B24] tracking-tight">Đăng nhập</h1>
            <p className="text-[14px] text-[#5E6F66] mt-1">Dành cho Bệnh nhân và Nhân viên y tế Smart Healthcare</p>
          </div>

          {/* Tab Switcher */}
          <div className="mb-5 bg-[#F1F4F2] p-1 rounded-lg flex items-center gap-1 border border-[#DDE7E1]">
            {(['patient', 'staff'] as const).map((t) => (
              <button
                key={t}
                type="button"
                onClick={() => { setTab(t); setErrorMsg(null); }}
                className={[
                  'flex-1 py-2 px-3 text-center rounded-[6px] text-[14px] font-medium transition-all duration-150',
                  tab === t
                    ? 'bg-white text-[#1C2B24] shadow-sm'
                    : 'text-[#5E6F66] hover:text-[#1C2B24]',
                ].join(' ')}
              >
                <span className="inline-flex items-center justify-center gap-1.5">
                  <span className={`material-symbols-outlined text-[16px] ${tab === t ? 'text-[#006a41]' : ''}`}>
                    {t === 'patient' ? 'person' : 'medical_services'}
                  </span>
                  {t === 'patient' ? 'Bệnh nhân' : 'Nhân viên y tế'}
                </span>
              </button>
            ))}
          </div>

          {/* Error Banner */}
          {errorMsg && (
            <div className="mb-5 p-3.5 rounded-lg bg-[#FDE8E8] border border-[#F8B4B4] flex items-start gap-3 text-[#D32F2F]">
              <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
              <p className="flex-1 text-[14px] font-medium leading-tight">{errorMsg}</p>
              <button
                type="button"
                onClick={() => setErrorMsg(null)}
                aria-label="Đóng thông báo"
                className="text-[#D32F2F] hover:opacity-80 p-0.5 rounded"
              >
                <span className="material-symbols-outlined text-[18px]">close</span>
              </button>
            </div>
          )}

          {/* Form */}
          <form className="space-y-4" noValidate onSubmit={handleSubmit}>
            {/* Username */}
            <div>
              <label className="block text-[14px] font-medium text-[#1C2B24] mb-1.5" htmlFor="identity">
                Tên đăng nhập <span className="text-[#D32F2F]">*</span>
              </label>
              <div className="relative">
                <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[#006a41] text-[20px] pointer-events-none">
                  contact_mail
                </span>
                <input
                  id="identity"
                  name="identity"
                  type="text"
                  autoComplete="username"
                  required
                  value={userName}
                  onChange={(e) => setUserName(e.target.value)}
                  placeholder={tab === 'patient' ? 'Số điện thoại hoặc email' : 'Tên đăng nhập nhân viên'}
                  className="w-full h-11 pl-10 pr-3 rounded-lg border border-[#DDE7E1] bg-white text-[#1C2B24] text-[14px] placeholder:text-[#5E6F66]/60 focus:outline-none focus:border-[#006a41] focus:ring-2 focus:ring-[#006a41]/20 transition-all duration-150"
                />
              </div>
            </div>

            {/* Password */}
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <label className="block text-[14px] font-medium text-[#1C2B24]" htmlFor="password">
                  Mật khẩu <span className="text-[#D32F2F]">*</span>
                </label>
                <a href="#" className="text-[12px] font-semibold text-[#006a41] hover:underline">
                  Quên mật khẩu?
                </a>
              </div>
              <div className="relative">
                <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[#006a41] text-[20px] pointer-events-none">
                  lock
                </span>
                <input
                  id="password"
                  name="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Nhập mật khẩu của bạn"
                  className="w-full h-11 pl-10 pr-11 rounded-lg border border-[#DDE7E1] bg-white text-[#1C2B24] text-[14px] placeholder:text-[#5E6F66]/60 focus:outline-none focus:border-[#006a41] focus:ring-2 focus:ring-[#006a41]/20 transition-all duration-150"
                />
                <button
                  type="button"
                  aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  onClick={() => setShowPassword((v) => !v)}
                  className="absolute right-2 top-1/2 -translate-y-1/2 p-1.5 text-[#5E6F66] hover:text-[#1C2B24] rounded focus:outline-none"
                >
                  <span className="material-symbols-outlined text-[20px]">
                    {showPassword ? 'visibility_off' : 'visibility'}
                  </span>
                </button>
              </div>
            </div>

            {/* Role badge */}
            <div className="flex items-center justify-between pt-1">
              <span className="text-[12px] text-[#5E6F66]">
                Cổng: {tab === 'patient' ? 'Bệnh nhân' : 'Nhân viên y tế'}
              </span>
            </div>

            {/* Submit */}
            <div className="pt-2">
              <button
                type="submit"
                disabled={isLoading}
                className="w-full h-11 rounded-lg bg-[#006a41] text-white text-[14px] font-semibold flex items-center justify-center gap-2 hover:bg-[#005231] active:scale-[0.99] transition-all duration-150 shadow-sm focus:outline-none focus:ring-2 focus:ring-[#006a41] focus:ring-offset-2 disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {isLoading ? (
                  <>
                    <span className="material-symbols-outlined text-[18px] animate-spin">progress_activity</span>
                    Đang đăng nhập…
                  </>
                ) : (
                  <>
                    <span>Đăng nhập</span>
                    <span className="material-symbols-outlined text-[18px]">arrow_forward</span>
                  </>
                )}
              </button>
            </div>
          </form>

          {/* Footer */}
          <div className="mt-8 pt-5 border-t border-[#DDE7E1] flex flex-col sm:flex-row items-center justify-between gap-3 text-center sm:text-left">
            <div className="flex items-center gap-2 text-[#5E6F66]">
              <span className="material-symbols-outlined text-[16px] text-[#006a41]">support_agent</span>
              <span className="text-[12px]">Hỗ trợ kỹ thuật: hotro@smarthealthcare.vn</span>
            </div>
            <a href="tel:19006868" className="text-[12px] font-semibold text-[#006a41] hover:underline flex items-center gap-1">
              <span className="material-symbols-outlined text-[15px]">call</span>
              1900 6868
            </a>
          </div>
        </section>
      </div>
    </div>
  );
}

