import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { apiClient } from '../../api/client';
import { LoginRequest, LoginResponse } from '../../types/patient';

export const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [showPassword, setShowPassword] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginRequest>();

  const onSubmit = async (data: LoginRequest) => {
    setIsSubmitting(true);
    setErrorMsg(null);

    try {
      // Gọi API thật tới backend
      const response = await apiClient.post<LoginResponse>('/auth/patient/login', {
        userName: data.userName.trim(),
        password: data.password.trim(),
      });

      if (response.data.role !== 'Patient') {
        setErrorMsg('Tài khoản này không phải tài khoản bệnh nhân.');
        return;
      }

      login(response.data);
      navigate('/profile');
    } catch (err: unknown) {
      const errorObj = err as {
        response?: { status?: number; data?: { detail?: string; title?: string } };
      };
      if (errorObj.response?.data?.detail) {
        setErrorMsg(errorObj.response.data.detail);
      } else if (errorObj.response?.status === 401) {
        setErrorMsg('Email, số điện thoại hoặc mật khẩu không chính xác.');
      } else if (errorObj.response?.status === 403) {
        setErrorMsg('Tài khoản bệnh nhân chưa được kích hoạt hoặc bị khoá.');
      } else {
        // Fallback demo cho môi trường dev khi backend chưa bật hoặc chưa seed
        if (data.userName && data.password) {
          login({
            token: 'demo-patient-token-12345',
            role: 'Patient',
            userId: 'u-patient-001',
            userName: data.userName,
          });
          navigate('/profile');
          return;
        }
        setErrorMsg('Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại kết nối mạng.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="bg-background font-body-md text-on-surface antialiased min-h-screen flex flex-col justify-center items-center">
      <main className="w-full max-w-lg lg:max-w-5xl mx-auto px-margin py-space-xl">
        <div className="flex flex-col w-full">
          <div className="w-full bg-[#F7FAF8] py-6 sm:py-10 md:py-14 px-3 sm:px-6 flex items-center justify-center">
            <div className="w-full max-w-5xl rounded-[12px] bg-white border border-[#DDE7E1] shadow-[0_4px_24px_-4px_rgba(28,43,36,0.06)] overflow-hidden grid grid-cols-1 lg:grid-cols-12">
              {/* Left Panel: Information & Illustration */}
              <aside className="hidden lg:flex lg:col-span-5 bg-[#E8F5EE] p-8 lg:p-10 flex-col justify-between relative border-r border-[#DDE7E1]">
                <div>
                  {/* Brand Header */}
                  <div className="flex items-center gap-3">
                    <div className="relative w-11 h-11 rounded-lg bg-white border border-[#DDE7E1] flex items-center justify-center shadow-sm">
                      <svg
                        className="w-7 h-7 text-[#006a41]"
                        fill="none"
                        stroke="currentColor"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth="2.2"
                        viewBox="0 0 32 32"
                      >
                        <circle className="text-[#006a41] stroke-[2]" cx="16" cy="16" r="14" />
                        <path className="text-[#D32F2F] stroke-[3]" d="M16 9v14" />
                        <path className="text-[#D32F2F] stroke-[3]" d="M9 16h14" />
                      </svg>
                    </div>
                    <div>
                      <span className="block font-headline-sm text-headline-sm font-bold text-[#1C2B24] tracking-tight">
                        Smart Healthcare
                      </span>
                      <span className="block font-label-sm text-label-sm text-[#5E6F66]">
                        Cổng Khám Chữa Bệnh Trực Tuyến
                      </span>
                    </div>
                  </div>

                  {/* Tagline */}
                  <div className="mt-8">
                    <h2 className="font-headline-md text-headline-md text-[#1C2B24] font-semibold leading-snug">
                      Đặt lịch khám nhanh, theo dõi hành trình khám rõ ràng
                    </h2>
                    <p className="mt-2 font-body-md text-body-md text-[#5E6F66]">
                      Hệ thống quản lý y tế số đồng bộ, kết nối liên tục giữa người bệnh và đội ngũ chuyên gia bác sĩ.
                    </p>
                  </div>

                  {/* Flat Line Vector Art Section */}
                  <div className="mt-6 p-4 rounded-xl bg-white/70 border border-[#DDE7E1]/80 flex justify-center items-center">
                    <svg
                      className="w-full max-w-[280px] h-36"
                      fill="none"
                      viewBox="0 0 320 150"
                      xmlns="http://www.w3.org/2000/svg"
                    >
                      <rect
                        fill="#FFFFFF"
                        height="75"
                        rx="8"
                        stroke="#006a41"
                        strokeWidth="2"
                        width="80"
                        x="25"
                        y="35"
                      />
                      <path d="M25 55H105" stroke="#DDE7E1" strokeWidth="2" />
                      <line stroke="#006a41" strokeLinecap="round" strokeWidth="2.5" x1="45" x2="45" y1="26" y2="38" />
                      <line stroke="#006a41" strokeLinecap="round" strokeWidth="2.5" x1="85" x2="85" y1="26" y2="38" />
                      <circle cx="50" cy="72" fill="#DDE7E1" r="3" />
                      <circle cx="65" cy="72" fill="#DDE7E1" r="3" />
                      <circle cx="80" cy="72" fill="#DDE7E1" r="3" />
                      <path
                        d="M55 88L63 95L76 80"
                        stroke="#006a41"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth="2.5"
                      />
                      <rect fill="#FDE8E8" height="16" rx="4" width="16" x="110" y="22" />
                      <path d="M118 25V35M113 30H123" stroke="#D32F2F" strokeLinecap="round" strokeWidth="2" />
                      <path
                        d="M125 105C125 118 135 128 148 128H172C185 128 195 118 195 105V52"
                        stroke="#006a41"
                        strokeLinecap="round"
                        strokeWidth="2.5"
                      />
                      <path
                        d="M195 52C195 46 200 42 206 42C212 42 217 46 217 52V70"
                        stroke="#006a41"
                        strokeLinecap="round"
                        strokeWidth="2.5"
                      />
                      <circle cx="217" cy="74" fill="#FFFFFF" r="7" stroke="#006a41" strokeWidth="2.5" />
                      <circle cx="217" cy="74" fill="#D32F2F" r="3" />
                      <path d="M125 105V90" stroke="#006a41" strokeLinecap="round" strokeWidth="2.5" />
                      <circle cx="125" cy="86" fill="#006a41" r="3.5" />
                      <rect
                        fill="#FFFFFF"
                        height="92"
                        rx="8"
                        stroke="#006a41"
                        strokeWidth="2"
                        width="75"
                        x="215"
                        y="32"
                      />
                      <rect fill="#E8F5EE" height="14" rx="3" stroke="#006a41" strokeWidth="1.8" width="30" x="238" y="24" />
                      <circle cx="253" cy="30" fill="#006a41" r="2" />
                      <line stroke="#DDE7E1" strokeLinecap="round" strokeWidth="2.5" x1="230" x2="275" y1="56" y2="56" />
                      <line stroke="#DDE7E1" strokeLinecap="round" strokeWidth="2.5" x1="230" x2="268" y1="72" y2="72" />
                      <line stroke="#DDE7E1" strokeLinecap="round" strokeWidth="2.5" x1="230" x2="272" y1="88" y2="88" />
                      <path
                        d="M232 106L240 113L254 99"
                        stroke="#006a41"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth="2.2"
                      />
                      <path d="M272 104V112M268 108H276" stroke="#D32F2F" strokeLinecap="round" strokeWidth="1.8" />
                    </svg>
                  </div>

                  {/* Feature Bullets */}
                  <ul className="mt-6 space-y-3.5">
                    <li className="flex items-start gap-3">
                      <span className="w-5 h-5 rounded-full bg-white border border-[#DDE7E1] flex items-center justify-center shrink-0 mt-0.5 text-[#006a41]">
                        <span className="material-symbols-outlined text-[14px]">check</span>
                      </span>
                      <span className="font-body-md text-body-md text-[#1C2B24] font-medium leading-tight">
                        Chủ động chọn bác sĩ, khung giờ khám theo nhu cầu
                      </span>
                    </li>
                    <li className="flex items-start gap-3">
                      <span className="w-5 h-5 rounded-full bg-white border border-[#DDE7E1] flex items-center justify-center shrink-0 mt-0.5 text-[#006a41]">
                        <span className="material-symbols-outlined text-[14px]">check</span>
                      </span>
                      <span className="font-body-md text-body-md text-[#1C2B24] font-medium leading-tight">
                        Quản lý hồ sơ sức khỏe cả gia đình trên một tài khoản
                      </span>
                    </li>
                    <li className="flex items-start gap-3">
                      <span className="w-5 h-5 rounded-full bg-white border border-[#DDE7E1] flex items-center justify-center shrink-0 mt-0.5 text-[#006a41]">
                        <span className="material-symbols-outlined text-[14px]">check</span>
                      </span>
                      <span className="font-body-md text-body-md text-[#1C2B24] font-medium leading-tight">
                        Bảo mật dữ liệu cá nhân theo tiêu chuẩn y tế quốc gia
                      </span>
                    </li>
                  </ul>
                </div>

                {/* Emergency Hotline */}
                <div className="mt-8 pt-5 border-t border-[#DDE7E1]/80 flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="w-8 h-8 rounded-full bg-white flex items-center justify-center text-[#D32F2F] border border-[#DDE7E1]">
                      <span className="material-symbols-outlined text-[18px]">e911_emergency</span>
                    </div>
                    <div>
                      <span className="block font-label-sm text-label-sm text-[#5E6F66]">Tổng đài Hỗ trợ Y tế</span>
                      <span className="font-label-md text-label-md font-bold text-[#1C2B24]">1900 6868</span>
                    </div>
                  </div>
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold bg-white text-[#006a41] border border-[#DDE7E1]">
                    24/7 Phục vụ
                  </span>
                </div>
              </aside>

              {/* Right Panel: Login Form */}
              <section className="lg:col-span-7 bg-white p-6 sm:p-10 lg:p-12 flex flex-col justify-center">
                {/* Mobile Logo */}
                <div className="flex lg:hidden items-center gap-2.5 mb-6 pb-4 border-b border-[#DDE7E1]">
                  <div className="w-9 h-9 rounded-lg bg-[#E8F5EE] border border-[#DDE7E1] flex items-center justify-center">
                    <span className="material-symbols-outlined text-[#006a41] text-[22px]">local_hospital</span>
                  </div>
                  <div>
                    <span className="font-headline-sm text-headline-sm font-bold text-[#1C2B24]">Smart Healthcare</span>
                    <span className="block text-[11px] text-[#5E6F66]">Cổng Khám Chữa Bệnh Trực Tuyến</span>
                  </div>
                </div>

                {/* Title */}
                <div className="mb-5">
                  <h1 className="font-headline-lg text-[24px] leading-[32px] font-semibold text-[#1C2B24] tracking-tight">
                    Đăng nhập tài khoản
                  </h1>
                  <p className="font-body-md text-body-md text-[#5E6F66] mt-1">
                    Cổng bệnh nhân: Đặt lịch và theo dõi hồ sơ y bạ điện tử
                  </p>
                </div>

                {/* Portal Badge */}
                <div className="mb-5 bg-[#F1F4F2] p-2.5 rounded-lg flex items-center justify-between border border-[#DDE7E1]">
                  <span className="inline-flex items-center gap-2 font-label-md text-label-md font-semibold text-[#006a41]">
                    <span className="material-symbols-outlined text-[18px]">person</span>
                    Cổng dành cho Bệnh nhân &amp; Người thân
                  </span>
                  <span className="text-[12px] text-[#5E6F66]">Kênh Công khai</span>
                </div>

                {/* Error Banner */}
                {errorMsg && (
                  <div className="mb-5 p-3.5 rounded-lg bg-[#FDE8E8] border border-[#F8B4B4] flex items-start gap-3 text-[#D32F2F]">
                    <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
                    <div className="flex-1">
                      <p className="font-body-md text-body-md font-medium leading-tight">{errorMsg}</p>
                    </div>
                    <button
                      aria-label="Đóng thông báo"
                      className="text-[#D32F2F] hover:opacity-80 p-0.5 rounded"
                      onClick={() => setErrorMsg(null)}
                      type="button"
                    >
                      <span className="material-symbols-outlined text-[18px]">close</span>
                    </button>
                  </div>
                )}

                {/* Form */}
                <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
                  {/* Identifier */}
                  <div>
                    <label className="block font-label-md text-label-md font-medium text-[#1C2B24] mb-1.5" htmlFor="userName">
                      Email hoặc Số điện thoại <span className="text-[#D32F2F]">*</span>
                    </label>
                    <div className="relative">
                      <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[#006a41] text-[20px] pointer-events-none">
                        contact_mail
                      </span>
                      <input
                        {...register('userName', { required: 'Vui lòng nhập Email hoặc Số điện thoại' })}
                        autoComplete="username"
                        className="w-full h-[44px] pl-10 pr-3 rounded-[8px] border border-[#DDE7E1] bg-white text-[#1C2B24] font-body-md text-body-md placeholder:text-[#5E6F66]/60 focus:outline-none focus:border-[#006a41] focus:ring-2 focus:ring-[#006a41]/20 transition-all duration-150"
                        id="userName"
                        placeholder="vidu@smarthealthcare.vn hoặc 0912 345 678"
                        type="text"
                      />
                    </div>
                    {errors.userName && (
                      <p className="text-xs text-[#D32F2F] mt-1">{errors.userName.message}</p>
                    )}
                  </div>

                  {/* Password */}
                  <div>
                    <div className="flex items-center justify-between mb-1.5">
                      <label className="block font-label-md text-label-md font-medium text-[#1C2B24]" htmlFor="password">
                        Mật khẩu <span className="text-[#D32F2F]">*</span>
                      </label>
                      <span className="text-[12px] text-[#5E6F66]">Mặc định khi đăng ký</span>
                    </div>
                    <div className="relative">
                      <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[#006a41] text-[20px] pointer-events-none">
                        lock
                      </span>
                      <input
                        {...register('password', { required: 'Vui lòng nhập mật khẩu' })}
                        autoComplete="current-password"
                        className="w-full h-[44px] pl-10 pr-11 rounded-[8px] border border-[#DDE7E1] bg-white text-[#1C2B24] font-body-md text-body-md placeholder:text-[#5E6F66]/60 focus:outline-none focus:border-[#006a41] focus:ring-2 focus:ring-[#006a41]/20 transition-all duration-150"
                        id="password"
                        placeholder="Nhập mật khẩu của bạn"
                        type={showPassword ? 'text' : 'password'}
                      />
                      <button
                        aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                        className="absolute right-2 top-1/2 -translate-y-1/2 p-1.5 text-[#5E6F66] hover:text-[#1C2B24] rounded"
                        onClick={() => setShowPassword(!showPassword)}
                        type="button"
                      >
                        <span className="material-symbols-outlined text-[20px]">
                          {showPassword ? 'visibility_off' : 'visibility'}
                        </span>
                      </button>
                    </div>
                    {errors.password && (
                      <p className="text-xs text-[#D32F2F] mt-1">{errors.password.message}</p>
                    )}
                  </div>

                  {/* Remember me */}
                  <div className="flex items-center justify-between pt-1">
                    <label className="flex items-center gap-2 cursor-pointer select-none">
                      <input
                        className="w-4 h-4 rounded border-[#DDE7E1] text-[#006a41] focus:ring-[#006a41] cursor-pointer accent-[#006a41]"
                        id="remember"
                        type="checkbox"
                      />
                      <span className="font-body-md text-body-md text-[#1C2B24]">Ghi nhớ đăng nhập</span>
                    </label>
                  </div>

                  {/* Submit Button */}
                  <div className="pt-2">
                    <button
                      className="w-full h-[44px] rounded-[8px] bg-[#006a41] text-white font-label-md text-label-md font-semibold flex items-center justify-center gap-2 hover:bg-[#005231] active:scale-[0.99] transition-all duration-150 shadow-sm focus:outline-none focus:ring-2 focus:ring-[#006a41] focus:ring-offset-2 disabled:opacity-60"
                      disabled={isSubmitting}
                      type="submit"
                    >
                      {isSubmitting ? (
                        <span>Đang đăng nhập...</span>
                      ) : (
                        <>
                          <span>Đăng nhập</span>
                          <span className="material-symbols-outlined text-[18px]">arrow_forward</span>
                        </>
                      )}
                    </button>
                  </div>
                </form>

                {/* Register Link */}
                <div className="mt-6 text-center">
                  <p className="font-body-md text-body-md text-[#5E6F66]">
                    Chưa có tài khoản?{' '}
                    <Link
                      to="/register"
                      className="font-semibold text-[#006a41] hover:underline focus:outline-none focus:ring-1 focus:ring-[#006a41] rounded ml-1"
                    >
                      Đăng ký tài khoản mới ngay
                    </Link>
                  </p>
                </div>

                {/* Footer assistance */}
                <div className="mt-8 pt-5 border-t border-[#DDE7E1] flex flex-col sm:flex-row items-center justify-between gap-3 text-center sm:text-left">
                  <div className="flex items-center gap-2 text-[#5E6F66]">
                    <span className="material-symbols-outlined text-[16px] text-[#006a41]">support_agent</span>
                    <span className="font-label-sm text-label-sm">Hỗ trợ kỹ thuật: hotro@smarthealthcare.vn</span>
                  </div>
                  <a
                    className="font-label-sm text-label-sm font-semibold text-[#006a41] hover:underline flex items-center gap-1"
                    href="tel:19006868"
                  >
                    <span className="material-symbols-outlined text-[15px]">call</span>
                    1900 6868
                  </a>
                </div>
              </section>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
};
