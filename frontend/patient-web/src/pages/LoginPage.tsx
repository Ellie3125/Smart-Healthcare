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
    <div className="min-h-screen bg-[#F7FAF8] flex flex-col justify-center items-center p-4">
      <div className="w-full max-w-md bg-white rounded-2xl border border-[#DDE7E1] shadow-sm p-6 sm:p-8">
        {/* Brand Header */}
        <div className="text-center mb-8">
          <div className="w-14 h-14 rounded-2xl bg-[#E8F5EE] border border-[#DDE7E1] flex items-center justify-center text-[#006a41] mx-auto mb-4 shadow-sm">
            <span className="material-symbols-outlined text-[32px]">local_hospital</span>
          </div>
          <h1 className="text-2xl font-bold text-[#1C2B24] tracking-tight">Smart Healthcare</h1>
          <p className="text-sm text-[#5E6F66] mt-1">Cổng dịch vụ y tế trực tuyến cho bệnh nhân & người thân</p>
        </div>

        {errorMsg && (
          <div className="mb-6 p-4 rounded-xl bg-[#FEE2E2] border border-[#FECACA] flex items-start gap-3 text-sm text-[#B91C1C]">
            <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
            <div className="flex-1 leading-relaxed">{errorMsg}</div>
          </div>
        )}

        <form onSubmit={handleLogin} className="space-y-5">
          <div>
            <label className="block text-sm font-semibold text-[#1C2B24] mb-1.5" htmlFor="userName">
              Tên đăng nhập
            </label>
            <div className="relative">
              <span className="material-symbols-outlined absolute left-3 top-2.5 text-[#5E6F66] text-[20px]">person</span>
              <input
                id="userName"
                type="text"
                value={userName}
                onChange={(e) => setUserName(e.target.value)}
                placeholder="Ví dụ: patient01"
                className="w-full h-11 pl-10 pr-3 rounded-lg border border-[#DDE7E1] bg-white text-sm focus:outline-none focus:ring-2 focus:ring-[#006a41]/20 focus:border-[#006a41]"
                required
              />
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="block text-sm font-semibold text-[#1C2B24]" htmlFor="password">
                Mật khẩu
              </label>
            </div>
            <div className="relative">
              <span className="material-symbols-outlined absolute left-3 top-2.5 text-[#5E6F66] text-[20px]">lock</span>
              <input
                id="password"
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Nhập mật khẩu của bạn"
                className="w-full h-11 pl-10 pr-10 rounded-lg border border-[#DDE7E1] bg-white text-sm focus:outline-none focus:ring-2 focus:ring-[#006a41]/20 focus:border-[#006a41]"
                required
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-3 top-2.5 text-[#5E6F66] hover:text-[#1C2B24]"
                tabIndex={-1}
              >
                <span className="material-symbols-outlined text-[20px]">
                  {showPassword ? 'visibility_off' : 'visibility'}
                </span>
              </button>
            </div>
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full h-11 rounded-lg bg-[#006a41] text-white font-semibold text-sm hover:bg-[#028654] transition-colors flex items-center justify-center gap-2 shadow-sm focus:outline-none focus:ring-2 focus:ring-[#006a41]/40 disabled:opacity-50 cursor-pointer"
          >
            {isSubmitting ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
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

        <div className="mt-6 pt-6 border-t border-[#DDE7E1] text-center text-sm text-[#5E6F66]">
          Chưa có tài khoản bệnh nhân?{' '}
          <Link to="/register" className="font-semibold text-[#006a41] hover:underline">
            Đăng ký ngay
          </Link>
        </div>

        <div className="mt-4 p-3 bg-[#E8F5EE] rounded-lg border border-[#DDE7E1] text-xs text-[#5E6F66] leading-relaxed text-center">
          Dành riêng cho Bệnh nhân và Người thân. Nhân viên y tế vui lòng truy cập cổng riêng.
        </div>
      </div>
    </div>
  );
};
