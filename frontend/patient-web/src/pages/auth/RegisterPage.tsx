import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { RegisterPatientRequest } from '../../types/patient';
import { mockRegisterPatient } from '../../api/mockPatientApi';

export const RegisterPage: React.FC = () => {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterPatientRequest>({
    defaultValues: {
      gender: 'Male',
    },
  });

  const onSubmit = async (data: RegisterPatientRequest) => {
    setIsSubmitting(true);
    setErrorMsg(null);

    try {
      // Gọi qua mock/API đăng ký
      const authResponse = await mockRegisterPatient(data);
      login(authResponse);
      navigate('/profile');
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } }; message?: string };
      setErrorMsg(errorObj.response?.data?.detail || errorObj.message || 'Đăng ký không thành công. Vui lòng thử lại.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="bg-background font-body-md text-on-surface antialiased min-h-screen flex flex-col justify-center items-center py-8">
      <main className="w-full max-w-2xl mx-auto px-4">
        <div className="w-full rounded-[12px] bg-white border border-[#DDE7E1] shadow-[0_4px_24px_-4px_rgba(28,43,36,0.06)] overflow-hidden p-6 sm:p-10">
          {/* Header */}
          <div className="flex items-center gap-3 mb-6 pb-4 border-b border-[#DDE7E1]">
            <div className="w-10 h-10 rounded-lg bg-[#E8F5EE] border border-[#DDE7E1] flex items-center justify-center">
              <span className="material-symbols-outlined text-[#006a41] text-[24px]">person_add</span>
            </div>
            <div>
              <h1 className="font-headline-sm text-headline-sm font-bold text-[#1C2B24]">
                Đăng ký tài khoản Bệnh nhân
              </h1>
              <p className="font-label-sm text-label-sm text-[#5E6F66]">
                Tạo tài khoản để đặt lịch khám và quản lý hồ sơ y bạ điện tử gia đình
              </p>
            </div>
          </div>

          {/* Pending verification info notice */}
          <div className="mb-6 p-4 rounded-lg bg-surface-container-low border border-primary/20 flex items-start gap-3">
            <span className="material-symbols-outlined text-primary text-[22px] shrink-0 mt-0.5">info</span>
            <div className="text-sm text-on-surface-variant leading-relaxed">
              <strong className="text-primary font-semibold block mb-0.5">Lưu ý về xác minh hồ sơ:</strong>
              Hồ sơ mới tạo ở trạng thái <strong>Chờ xác minh (Pending)</strong>. Bạn có thể đặt lịch, thanh toán và xem vé; chỉ xem được kết quả xét nghiệm và lịch sử sức khỏe sau khi tiếp tân đối chiếu giấy tờ gốc tại quầy.
            </div>
          </div>

          {/* Error Banner */}
          {errorMsg && (
            <div className="mb-5 p-3.5 rounded-lg bg-[#FDE8E8] border border-[#F8B4B4] flex items-start gap-3 text-[#D32F2F]">
              <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
              <p className="font-body-md text-body-md font-medium flex-1">{errorMsg}</p>
            </div>
          )}

          {/* Registration Form */}
          <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
            {/* Account Info Section */}
            <div className="bg-[#F7FAF8] p-4 rounded-lg border border-[#DDE7E1] space-y-4">
              <div className="text-xs font-bold text-[#006a41] uppercase tracking-wider">Thông tin tài khoản đăng nhập</div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block font-label-md text-label-md font-medium text-[#1C2B24] mb-1" htmlFor="userName">
                    Tên đăng nhập / Email <span className="text-[#D32F2F]">*</span>
                  </label>
                  <input
                    {...register('userName', { required: 'Vui lòng nhập tên đăng nhập hoặc email' })}
                    className="w-full h-11 px-3 rounded-lg border border-[#DDE7E1] bg-white text-on-surface text-sm focus:border-primary focus:ring-2 focus:ring-primary/20 outline-none"
                    placeholder="VD: nguyenminhanh@gmail.com"
                    id="userName"
                    type="text"
                  />
                  {errors.userName && <p className="text-xs text-[#D32F2F] mt-1">{errors.userName.message}</p>}
                </div>

                <div>
                  <label className="block font-label-md text-label-md font-medium text-[#1C2B24] mb-1" htmlFor="password">
                    Mật khẩu khởi tạo <span className="text-[#D32F2F]">*</span>
                  </label>
                  <input
                    {...register('password', {
                      required: 'Vui lòng nhập mật khẩu',
                      minLength: { value: 6, message: 'Mật khẩu tối thiểu 6 ký tự' },
                    })}
                    className="w-full h-11 px-3 rounded-lg border border-[#DDE7E1] bg-white text-on-surface text-sm focus:border-primary focus:ring-2 focus:ring-primary/20 outline-none"
                    placeholder="Ít nhất 6 ký tự"
                    id="password"
                    type="password"
                  />
                  {errors.password && <p className="text-xs text-[#D32F2F] mt-1">{errors.password.message}</p>}
                </div>
              </div>
            </div>

            {/* Patient Personal Info Section */}
            <div className="bg-[#F7FAF8] p-4 rounded-lg border border-[#DDE7E1] space-y-4">
              <div className="text-xs font-bold text-[#006a41] uppercase tracking-wider">Hồ sơ sức khỏe cá nhân (Bản thân)</div>

              {/* FullName */}
              <div>
                <label className="block font-label-md text-label-md font-medium text-[#1C2B24] mb-1" htmlFor="fullName">
                  Họ và tên đầy đủ <span className="text-[#D32F2F]">*</span>
                </label>
                <input
                  {...register('fullName', { required: 'Vui lòng nhập họ tên đầy đủ theo giấy tờ' })}
                  className="w-full h-11 px-3 rounded-lg border border-[#DDE7E1] bg-white text-on-surface text-sm focus:border-primary focus:ring-2 focus:ring-primary/20 outline-none"
                  placeholder="VD: Nguyễn Minh Anh"
                  id="fullName"
                  type="text"
                />
                {errors.fullName && <p className="text-xs text-[#D32F2F] mt-1">{errors.fullName.message}</p>}
              </div>

              {/* DOB & Gender */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block font-label-md text-label-md font-medium text-[#1C2B24] mb-1" htmlFor="dateOfBirth">
                    Ngày sinh <span className="text-[#D32F2F]">*</span>
                  </label>
                  <input
                    {...register('dateOfBirth', { required: 'Vui lòng chọn ngày sinh' })}
                    className="w-full h-11 px-3 rounded-lg border border-[#DDE7E1] bg-white text-on-surface text-sm focus:border-primary focus:ring-2 focus:ring-primary/20 outline-none"
                    id="dateOfBirth"
                    type="date"
                  />
                  {errors.dateOfBirth && <p className="text-xs text-[#D32F2F] mt-1">{errors.dateOfBirth.message}</p>}
                </div>

                <div>
                  <label className="block font-label-md text-label-md font-medium text-[#1C2B24] mb-1">
                    Giới tính <span className="text-[#D32F2F]">*</span>
                  </label>
                  <div className="grid grid-cols-3 gap-2 h-11">
                    {(['Male', 'Female', 'Other'] as const).map((g) => (
                      <label
                        key={g}
                        className="cursor-pointer border border-[#DDE7E1] rounded-lg bg-white flex items-center justify-center text-sm font-medium hover:bg-surface transition-colors has-[:checked]:border-primary has-[:checked]:bg-[#E8F5EE] has-[:checked]:text-primary"
                      >
                        <input {...register('gender')} type="radio" value={g} className="hidden" />
                        <span>{g === 'Male' ? 'Nam' : g === 'Female' ? 'Nữ' : 'Khác'}</span>
                      </label>
                    ))}
                  </div>
                </div>
              </div>

              {/* Phone & CCCD (CCCD is optional) */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block font-label-md text-label-md font-medium text-[#1C2B24] mb-1" htmlFor="phoneNumber">
                    Số điện thoại liên hệ <span className="text-[#D32F2F]">*</span>
                  </label>
                  <input
                    {...register('phoneNumber', { required: 'Vui lòng nhập số điện thoại' })}
                    className="w-full h-11 px-3 rounded-lg border border-[#DDE7E1] bg-white text-on-surface text-sm focus:border-primary focus:ring-2 focus:ring-primary/20 outline-none"
                    placeholder="VD: 0912 345 678"
                    id="phoneNumber"
                    type="tel"
                  />
                  {errors.phoneNumber && <p className="text-xs text-[#D32F2F] mt-1">{errors.phoneNumber.message}</p>}
                </div>

                <div>
                  <div className="flex items-center justify-between mb-1">
                    <label className="block font-label-md text-label-md font-medium text-[#1C2B24]" htmlFor="nationalId">
                      Số CCCD / Định danh
                    </label>
                    <span className="text-xs text-[#5E6F66]">(Không bắt buộc)</span>
                  </div>
                  <input
                    {...register('nationalId')}
                    className="w-full h-11 px-3 rounded-lg border border-[#DDE7E1] bg-white text-on-surface text-sm focus:border-primary focus:ring-2 focus:ring-primary/20 outline-none font-data-tabular"
                    placeholder="12 chữ số trên CCCD gắn chip (nếu có)"
                    id="nationalId"
                    type="text"
                  />
                  <p className="text-[11px] text-[#5E6F66] mt-0.5">Không yêu cầu tải ảnh giấy tờ; đối chiếu tại quầy.</p>
                </div>
              </div>

              {/* Address */}
              <div>
                <label className="block font-label-md text-label-md font-medium text-[#1C2B24] mb-1" htmlFor="address">
                  Địa chỉ thường trú / liên hệ <span className="text-[#D32F2F]">*</span>
                </label>
                <input
                  {...register('address', { required: 'Vui lòng nhập địa chỉ cư trú' })}
                  className="w-full h-11 px-3 rounded-lg border border-[#DDE7E1] bg-white text-on-surface text-sm focus:border-primary focus:ring-2 focus:ring-primary/20 outline-none"
                  placeholder="Số nhà, tên đường, phường/xã, quận/huyện, tỉnh/thành phố"
                  id="address"
                  type="text"
                />
                {errors.address && <p className="text-xs text-[#D32F2F] mt-1">{errors.address.message}</p>}
              </div>
            </div>

            {/* Actions */}
            <div className="pt-3 flex flex-col sm:flex-row items-center justify-between gap-3">
              <Link
                to="/login"
                className="w-full sm:w-auto px-6 h-11 rounded-lg border border-[#DDE7E1] text-[#1C2B24] hover:bg-[#F1F4F2] flex items-center justify-center font-medium text-sm transition-colors"
              >
                Quay lại Đăng nhập
              </Link>
              <button
                disabled={isSubmitting}
                className="w-full sm:w-auto min-w-[200px] h-11 rounded-lg bg-[#006a41] hover:bg-[#005231] text-white font-semibold text-sm flex items-center justify-center gap-2 shadow-sm transition-all disabled:opacity-60"
                type="submit"
              >
                {isSubmitting ? (
                  <span>Đang xử lý...</span>
                ) : (
                  <>
                    <span className="material-symbols-outlined text-[18px]">how_to_reg</span>
                    <span>Hoàn tất Đăng ký</span>
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      </main>
    </div>
  );
};
