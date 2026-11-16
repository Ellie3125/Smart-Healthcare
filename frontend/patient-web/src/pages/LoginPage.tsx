import React, { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { apiClient } from '../api/client';
import { LoginResponse } from '../types/auth';

export const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();

  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const from = (location.state as { from?: { pathname: string } })?.from?.pathname || '/profiles';

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!userName.trim() || !password.trim()) {
      setErrorMsg('Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.');
      return;
    }

    setIsSubmitting(true);
    setErrorMsg(null);

    try {
      const res = await apiClient.post<LoginResponse>('/auth/patient/login', {
        userName: userName.trim(),
        password: password.trim(),
      });

      if (res.data.role !== 'Patient') {
        setErrorMsg('Tài khoản này là nhân viên y tế. Vui lòng đăng nhập tại staff.smarthealthcare.io.vn');
        return;
      }

      login(res.data);
      navigate(from, { replace: true });
    } catch (err: unknown) {
      const errorObj = err as { response?: { status?: number; data?: { detail?: string; title?: string } } };
      if (errorObj.response?.data?.detail) {
        setErrorMsg(errorObj.response.data.detail);
      } else if (errorObj.response?.status === 401) {
        setErrorMsg('Tên đăng nhập hoặc mật khẩu không chính xác.');
      } else if (errorObj.response?.status === 403) {
        setErrorMsg('Tài khoản của bạn không có quyền truy cập kênh bệnh nhân.');
      } else {
        setErrorMsg('Không thể kết nối đến máy chủ. Vui lòng thử lại sau.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-surface grid lg:grid-cols-12 font-body-md text-on-surface">
      {/* Left Brand Showcase matching Stitch */}
      <aside className="hidden lg:flex lg:col-span-6 bg-gradient-to-br from-[#006a41] via-[#028654] to-[#005231] text-white p-10 xl:p-14 flex-col justify-between relative overflow-hidden">
        <div className="absolute -top-24 -right-24 w-72 h-72 rounded-full bg-white/10 blur-3xl pointer-events-none" />
        <div className="absolute -bottom-24 -left-24 w-80 h-80 rounded-full bg-[#9ef5c0]/15 blur-3xl pointer-events-none" />

        <div className="relative z-10 flex items-center gap-3">
          <div className="w-11 h-11 rounded-xl bg-white/15 backdrop-blur-md flex items-center justify-center">
            <span className="material-symbols-outlined text-[26px] text-white">local_hospital</span>
          </div>
          <div>
            <p className="font-headline-sm text-lg font-bold tracking-tight">Smart Healthcare</p>
            <p className="text-xs text-[#9ef5c0]">Phòng khám Ngoại trú Chuẩn Quốc tế</p>
          </div>
        </div>

        <div className="relative z-10 space-y-6 my-auto py-8 max-w-lg">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-white/15 px-3 py-1 text-xs font-semibold text-[#9ef5c0]">
            <span className="material-symbols-outlined text-[15px]">verified</span>
            Cổng Bệnh Nhân &amp; Người Thân
          </span>
          <h2 className="text-3xl xl:text-4xl font-bold leading-tight tracking-tight">
            Chăm sóc sức khỏe chủ động, minh bạch mọi hành trình khám
          </h2>
          <p className="text-sm text-white/85 leading-relaxed">
            Đặt lịch khám đúng chuyên khoa, nhận vé khám số QR, theo dõi số thứ tự theo thời gian thực và tra cứu kết quả y bạ điện tử mọi lúc.
          </p>

          <div className="grid gap-3 pt-2">
            {[
              { icon: 'calendar_month', title: 'Đặt lịch chuyên khoa & giữ chỗ tức thì', desc: 'Chọn ngày khám và khung giờ phù hợp không cần chờ đợi tại quầy.' },
              { icon: 'qr_code_2', title: 'Vé khám điện tử & theo dõi hàng chờ', desc: 'Cập nhật tiến trình tiếp đón, sinh hiệu và thứ tự vào phòng theo thời gian thực.' },
              { icon: 'clinical_notes', title: 'Lưu trữ bệnh án & đơn thuốc đồng bộ', desc: 'Quản lý hồ sơ cho bản thân và các thành viên gia đình trên cùng một tài khoản.' },
            ].map((item) => (
              <div key={item.title} className="flex items-start gap-3 rounded-2xl bg-white/10 backdrop-blur-sm p-3.5">
                <div className="w-9 h-9 rounded-xl bg-white/15 flex items-center justify-center shrink-0">
                  <span className="material-symbols-outlined text-[20px] text-[#9ef5c0]">{item.icon}</span>
                </div>
                <div>
                  <p className="text-sm font-semibold text-white">{item.title}</p>
                  <p className="text-xs text-white/75 mt-0.5">{item.desc}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="relative z-10 flex flex-wrap items-center gap-4 text-xs text-white/80">
          <span className="inline-flex items-center gap-1.5">
            <span className="material-symbols-outlined text-[16px] text-[#9ef5c0]">health_and_safety</span>
            GP Bộ Y Tế: 08842/BYT-CCHN
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span className="material-symbols-outlined text-[16px] text-[#9ef5c0]">call</span>
            CSKH 24/7: 1900 6868
          </span>
        </div>
      </aside>

      {/* Right White Card Panel */}
      <div className="lg:col-span-6 flex flex-col justify-center items-center p-4 sm:p-8 lg:p-12">
        <div className="w-full max-w-md bg-surface-container-lowest rounded-2xl border border-surface-container-highest/70 shadow-[0_8px_32px_rgba(15,31,24,0.06)] p-6 sm:p-9">
          <div className="text-center mb-7">
            <div className="w-14 h-14 rounded-2xl bg-surface-container-low flex items-center justify-center text-primary mx-auto mb-3.5 shadow-sm">
              <span className="material-symbols-outlined text-[30px]">local_hospital</span>
            </div>
            <h1 className="text-2xl font-bold text-on-surface tracking-tight">Đăng nhập Smart Healthcare</h1>
            <p className="text-sm text-on-surface-variant mt-1">
              Cổng dịch vụ y tế trực tuyến cho bệnh nhân &amp; người thân
            </p>
          </div>

          {errorMsg && (
            <div className="mb-5 p-4 rounded-xl bg-error-container border border-[#ffb4ab]/70 flex items-start gap-3 text-sm text-[#93000a]">
              <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
              <div className="flex-1 leading-relaxed">{errorMsg}</div>
            </div>
          )}

          <form onSubmit={handleLogin} className="space-y-4">
            <div>
              <label className="block text-sm font-semibold text-on-surface mb-1.5" htmlFor="userName">
                Tên đăng nhập
              </label>
              <div className="relative">
                <span className="material-symbols-outlined absolute left-3.5 top-1/2 -translate-y-1/2 text-on-surface-variant text-[20px] pointer-events-none w-5 h-5 flex items-center justify-center overflow-hidden shrink-0 select-none">person</span>
                <input
                  id="userName"
                  type="text"
                  value={userName}
                  onChange={(e) => setUserName(e.target.value)}
                  placeholder="Ví dụ: patient01"
                  className="w-full h-11 pl-11 pr-3.5 rounded-xl border border-outline-variant/75 bg-surface-container-low/55 text-sm text-on-surface placeholder:text-outline transition-colors focus:outline-none focus:bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
                  required
                />
              </div>
            </div>

            <div>
              <div className="flex items-center justify-between mb-1.5">
                <label className="block text-sm font-semibold text-on-surface" htmlFor="password">
                  Mật khẩu
                </label>
              </div>
              <div className="relative">
                <span className="material-symbols-outlined absolute left-3.5 top-1/2 -translate-y-1/2 text-on-surface-variant text-[20px] pointer-events-none w-5 h-5 flex items-center justify-center overflow-hidden shrink-0 select-none">lock</span>
                <input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Nhập mật khẩu của bạn"
                  className="w-full h-11 pl-11 pr-12 rounded-xl border border-outline-variant/75 bg-surface-container-low/55 text-sm text-on-surface placeholder:text-outline transition-colors focus:outline-none focus:bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 w-8 h-8 flex items-center justify-center text-on-surface-variant hover:text-on-surface rounded-lg"
                  tabIndex={-1}
                  aria-label="Hiện hoặc ẩn mật khẩu"
                >
                  <span className="material-symbols-outlined text-[20px] w-5 h-5 flex items-center justify-center overflow-hidden select-none">
                    {showPassword ? 'visibility_off' : 'visibility'}
                  </span>
                </button>
              </div>
            </div>

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full h-11 rounded-xl bg-primary text-white font-semibold text-sm hover:bg-[#005231] transition-colors flex items-center justify-center gap-2 shadow-[0_2px_8px_rgba(0,106,65,0.22)] focus:outline-none focus:ring-2 focus:ring-primary/40 disabled:opacity-50 cursor-pointer"
            >
              {isSubmitting ? (
                <>
                  <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  <span>Đang xử lý...</span>
                </>
              ) : (
                <>
                  <span className="material-symbols-outlined text-[20px]">login</span>
                  <span>Đăng nhập</span>
                </>
              )}
            </button>
          </form>

          <div className="mt-6 pt-5 border-t border-surface-container-low text-center text-sm text-on-surface-variant">
            Chưa có tài khoản bệnh nhân?{' '}
            <Link to="/register" className="font-semibold text-primary hover:underline">
              Đăng ký ngay
            </Link>
          </div>

          <div className="mt-4 p-3 bg-surface-container-low rounded-xl text-xs text-on-surface-variant leading-relaxed text-center">
            Dành riêng cho Bệnh nhân và Người thân. Nhân viên y tế vui lòng truy cập cổng nội bộ riêng.
          </div>
        </div>
      </div>
    </div>
  );
};
