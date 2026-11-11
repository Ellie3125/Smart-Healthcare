import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { apiClient } from '../api/client';
import { LoginResponse } from '../types/auth';

export const RegisterPage: React.FC = () => {
  const navigate = useNavigate();
  const { login } = useAuth();

  // Account details
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  // Self profile toggle & details
  const [createSelf, setCreateSelf] = useState(true);
  const [fullName, setFullName] = useState('');
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [gender, setGender] = useState<'Male' | 'Female' | 'Other'>('Male');
  const [nationalId, setNationalId] = useState('');
  const [selfPhoneNumber, setSelfPhoneNumber] = useState('');
  const [selfPhoneEdited, setSelfPhoneEdited] = useState(false);
  const [address, setAddress] = useState('');

  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const validate = (): boolean => {
    const errors: Record<string, string> = {};

    if (!userName.trim()) errors.userName = 'Tên đăng nhập không được để trống.';
    if (userName.length < 3) errors.userName = 'Tên đăng nhập phải có ít nhất 3 ký tự.';
    if (!password) errors.password = 'Mật khẩu không được để trống.';
    if (password.length < 6) errors.password = 'Mật khẩu phải có ít nhất 6 ký tự.';
    if (password !== confirmPassword) errors.confirmPassword = 'Mật khẩu xác nhận không khớp.';

    if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      errors.email = 'Định dạng email không hợp lệ.';
    }

    if (phoneNumber && !/^(0|\+84)[0-9]{9,10}$/.test(phoneNumber.replace(/\s/g, ''))) {
      errors.phoneNumber = 'Số điện thoại không đúng định dạng Việt Nam.';
    }

    if (createSelf) {
      if (!fullName.trim()) errors.fullName = 'Họ và tên không được để trống.';
      if (fullName.trim().length > 200) errors.fullName = 'Họ và tên tối đa 200 ký tự.';
      if (!dateOfBirth) {
        errors.dateOfBirth = 'Ngày sinh không được để trống.';
      } else {
        const dob = new Date(dateOfBirth);
        const today = new Date();
        if (dob > today) {
          errors.dateOfBirth = 'Ngày sinh không thể ở tương lai.';
        }
        const age = today.getFullYear() - dob.getFullYear();
        if (age > 120) {
          errors.dateOfBirth = 'Ngày sinh không được quá 120 tuổi.';
        }
      }

      if (nationalId.trim()) {
        if (!/^[0-9]{12}$/.test(nationalId.trim())) {
          errors.nationalId = 'Số CCCD/định danh cá nhân phải gồm đúng 12 chữ số.';
        }
      }

      const normalizedSelfPhone = selfPhoneNumber.replace(/\s/g, '');
      if (!normalizedSelfPhone) {
        errors.selfPhoneNumber = 'Số điện thoại liên hệ của hồ sơ bệnh nhân không được để trống.';
      } else if (!/^(0|\+84)[0-9]{9,10}$/.test(normalizedSelfPhone)) {
        errors.selfPhoneNumber = 'Số điện thoại hồ sơ không đúng định dạng Việt Nam.';
      }

      if (!address.trim()) errors.address = 'Địa chỉ thường trú không được để trống.';
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) {
      setErrorMsg('Vui lòng kiểm tra lại các trường thông tin có lỗi bên dưới.');
      return;
    }

    setIsSubmitting(true);
    setErrorMsg(null);

    const payload = {
      userName: userName.trim(),
      email: email.trim() || undefined,
      phoneNumber: phoneNumber.trim() || undefined,
      password: password,
      createSelfProfile: createSelf,
      selfProfile: createSelf
        ? {
            fullName: fullName.trim(),
            dateOfBirth: dateOfBirth,
            gender: gender,
            nationalId: nationalId.trim() || undefined,
            phoneNumber: selfPhoneNumber.replace(/\s/g, ''),
            address: address.trim(),
          }
        : undefined,
    };

    try {
      const res = await apiClient.post<LoginResponse>('/auth/patient/register', payload);
      login(res.data);
      navigate('/profiles', { replace: true });
    } catch (err: unknown) {
      const errorObj = err as { response?: { status?: number; data?: { detail?: string; title?: string; errors?: Record<string, string[]> } } };
      if (errorObj.response?.status === 429) {
        setErrorMsg(errorObj.response.data?.detail || 'Bạn đã gửi quá nhiều yêu cầu đăng ký. Vui lòng thử lại sau ít phút.');
      } else if (errorObj.response?.status === 409) {
        setErrorMsg(errorObj.response.data?.detail || 'Tên đăng nhập, email, số điện thoại hoặc CCCD đã được đăng ký.');
      } else if (errorObj.response?.data?.detail) {
        setErrorMsg(errorObj.response.data.detail);
      } else if (errorObj.response?.data?.errors) {
        const firstErr = Object.values(errorObj.response.data.errors).flat()[0];
        setErrorMsg(firstErr || 'Thông tin đăng ký chưa hợp lệ.');
      } else {
        setErrorMsg('Không thể đăng ký tài khoản. Vui lòng thử lại sau.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#F7FAF8] py-8 px-4 flex justify-center items-center">
      <div className="w-full max-w-2xl bg-white rounded-2xl border border-[#DDE7E1] shadow-sm p-6 sm:p-10">
        <div className="text-center mb-8">
          <div className="w-12 h-12 rounded-xl bg-[#E8F5EE] border border-[#DDE7E1] flex items-center justify-center text-[#006a41] mx-auto mb-3 shadow-sm">
            <span className="material-symbols-outlined text-[28px]">person_add</span>
          </div>
          <h1 className="text-2xl font-bold text-[#1C2B24] tracking-tight">Đăng Ký Tài Khoản Bệnh Nhân</h1>
          <p className="text-sm text-[#5E6F66] mt-1">
            Tạo tài khoản quản lý hồ sơ khám chữa bệnh cho bản thân và người thân trong gia đình
          </p>
        </div>

        {errorMsg && (
          <div className="mb-6 p-4 rounded-xl bg-[#FEE2E2] border border-[#FECACA] flex items-start gap-3 text-sm text-[#B91C1C]">
            <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
            <div className="flex-1">{errorMsg}</div>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Section 1: Thông tin tài khoản */}
          <div>
            <h2 className="text-sm font-bold uppercase tracking-wider text-[#006a41] border-b border-[#DDE7E1] pb-2 mb-4">
              1. Thông Tin Tài Khoản Đăng Nhập
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="reg-username">
                  Tên đăng nhập <span className="text-red-500">*</span>
                </label>
                <input
                  id="reg-username"
                  type="text"
                  value={userName}
                  onChange={(e) => setUserName(e.target.value)}
                  placeholder="Ví dụ: nguyenvanan"
                  className={`w-full h-10 px-3 rounded-lg border text-sm ${
                    fieldErrors.userName ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                  } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                />
                {fieldErrors.userName && <p className="text-xs text-red-500 mt-1">{fieldErrors.userName}</p>}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="reg-email">
                  Địa chỉ Email
                </label>
                <input
                  id="reg-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="name@example.com"
                  className={`w-full h-10 px-3 rounded-lg border text-sm ${
                    fieldErrors.email ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                  } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                />
                {fieldErrors.email && <p className="text-xs text-red-500 mt-1">{fieldErrors.email}</p>}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="reg-phone">
                  Số điện thoại liên hệ
                </label>
                <input
                  id="reg-phone"
                  type="tel"
                  value={phoneNumber}
                  onChange={(e) => {
                    const next = e.target.value;
                    setPhoneNumber(next);
                    if (!selfPhoneEdited) {
                      setSelfPhoneNumber(next);
                    }
                  }}
                  placeholder="0912345678"
                  className={`w-full h-10 px-3 rounded-lg border text-sm ${
                    fieldErrors.phoneNumber ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                  } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                />
                {fieldErrors.phoneNumber && <p className="text-xs text-red-500 mt-1">{fieldErrors.phoneNumber}</p>}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="reg-pwd">
                  Mật khẩu <span className="text-red-500">*</span>
                </label>
                <input
                  id="reg-pwd"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Tối thiểu 6 ký tự"
                  className={`w-full h-10 px-3 rounded-lg border text-sm ${
                    fieldErrors.password ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                  } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                />
                {fieldErrors.password && <p className="text-xs text-red-500 mt-1">{fieldErrors.password}</p>}
              </div>

              <div className="sm:col-span-2">
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="reg-confirm">
                  Xác nhận lại mật khẩu <span className="text-red-500">*</span>
                </label>
                <input
                  id="reg-confirm"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="Nhập lại đúng mật khẩu phía trên"
                  className={`w-full h-10 px-3 rounded-lg border text-sm ${
                    fieldErrors.confirmPassword ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                  } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                />
                {fieldErrors.confirmPassword && (
                  <p className="text-xs text-red-500 mt-1">{fieldErrors.confirmPassword}</p>
                )}
              </div>
            </div>
          </div>

          {/* Section 2: Tạo hồ sơ bản thân */}
          <div>
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-2 mb-4">
              <h2 className="text-sm font-bold uppercase tracking-wider text-[#006a41]">
                2. Hồ Sơ Bệnh Nhân Bản Thân (Self)
              </h2>
              <label className="flex items-center gap-2 cursor-pointer text-xs font-medium text-[#1C2B24]">
                <input
                  type="checkbox"
                  checked={createSelf}
                  onChange={(e) => setCreateSelf(e.target.checked)}
                  className="rounded border-[#DDE7E1] text-[#006a41] focus:ring-[#006a41]"
                />
                <span>Tạo hồ sơ ngay</span>
              </label>
            </div>

            {createSelf ? (
              <div className="space-y-4 bg-[#F7FAF8] p-4 rounded-xl border border-[#DDE7E1]">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="sm:col-span-2">
                    <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="self-fullname">
                      Họ và tên đầy đủ <span className="text-red-500">*</span>
                    </label>
                    <input
                      id="self-fullname"
                      type="text"
                      value={fullName}
                      onChange={(e) => setFullName(e.target.value)}
                      placeholder="NGUYỄN VĂN AN"
                      className={`w-full h-10 px-3 rounded-lg border text-sm bg-white ${
                        fieldErrors.fullName ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                      } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                    />
                    {fieldErrors.fullName && <p className="text-xs text-red-500 mt-1">{fieldErrors.fullName}</p>}
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="self-dob">
                      Ngày sinh <span className="text-red-500">*</span>
                    </label>
                    <input
                      id="self-dob"
                      type="date"
                      value={dateOfBirth}
                      onChange={(e) => setDateOfBirth(e.target.value)}
                      className={`w-full h-10 px-3 rounded-lg border text-sm bg-white ${
                        fieldErrors.dateOfBirth ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                      } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                    />
                    {fieldErrors.dateOfBirth && <p className="text-xs text-red-500 mt-1">{fieldErrors.dateOfBirth}</p>}
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                      Giới tính <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={gender}
                      onChange={(e) => setGender(e.target.value as 'Male' | 'Female' | 'Other')}
                      className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] bg-white text-sm focus:outline-none focus:ring-2 focus:ring-[#006a41]/20"
                    >
                      <option value="Male">Nam</option>
                      <option value="Female">Nữ</option>
                      <option value="Other">Khác</option>
                    </select>
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="self-cccd">
                      Số CCCD / Định danh (để trống nếu chưa có)
                    </label>
                    <input
                      id="self-cccd"
                      type="text"
                      maxLength={12}
                      value={nationalId}
                      onChange={(e) => setNationalId(e.target.value)}
                      placeholder="12 chữ số"
                      className={`w-full h-10 px-3 rounded-lg border text-sm bg-white ${
                        fieldErrors.nationalId ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                      } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                    />
                    {fieldErrors.nationalId && <p className="text-xs text-red-500 mt-1">{fieldErrors.nationalId}</p>}
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="self-phone">
                      Số điện thoại hồ sơ <span className="text-red-500">*</span>
                    </label>
                    <input
                      id="self-phone"
                      type="tel"
                      value={selfPhoneNumber}
                      onChange={(e) => {
                        setSelfPhoneEdited(true);
                        setSelfPhoneNumber(e.target.value);
                      }}
                      placeholder="0912345678"
                      className={`w-full h-10 px-3 rounded-lg border text-sm bg-white ${
                        fieldErrors.selfPhoneNumber ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                      } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                    />
                    {fieldErrors.selfPhoneNumber && (
                      <p className="text-xs text-red-500 mt-1">{fieldErrors.selfPhoneNumber}</p>
                    )}
                  </div>

                  <div className="sm:col-span-2">
                    <label className="block text-xs font-semibold text-[#1C2B24] mb-1" htmlFor="self-address">
                      Địa chỉ liên hệ / Thường trú <span className="text-red-500">*</span>
                    </label>
                    <input
                      id="self-address"
                      type="text"
                      value={address}
                      onChange={(e) => setAddress(e.target.value)}
                      placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/TP"
                      className={`w-full h-10 px-3 rounded-lg border text-sm bg-white ${
                        fieldErrors.address ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
                      } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
                    />
                    {fieldErrors.address && <p className="text-xs text-red-500 mt-1">{fieldErrors.address}</p>}
                  </div>
                </div>

                <div className="p-3 bg-white rounded-lg border border-[#DDE7E1] text-xs text-[#5E6F66] flex items-start gap-2">
                  <span className="material-symbols-outlined text-[18px] text-[#006a41] shrink-0 mt-0.5">info</span>
                  <p>
                    <strong>Quy định bảo mật & xác thực (FR01):</strong> Hệ thống không yêu cầu và không hỗ trợ tải ảnh giấy tờ tùy thân. Khi đến khám tại cơ sở y tế, vui lòng mang theo bản gốc thẻ CCCD để tiếp tân đối chiếu tại quầy.
                  </p>
                </div>
              </div>
            ) : (
              <p className="text-xs text-[#5E6F66] italic">
                Bạn có thể bổ sung hồ sơ bản thân hoặc người thân sau khi đăng ký thành công.
              </p>
            )}
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full h-12 rounded-xl bg-[#006a41] text-white font-semibold text-sm hover:bg-[#028654] transition-colors flex items-center justify-center gap-2 shadow-sm cursor-pointer disabled:opacity-50"
          >
            {isSubmitting ? (
              <>
                <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                <span>Đang đăng ký tài khoản...</span>
              </>
            ) : (
              <>
                <span className="material-symbols-outlined text-[20px]">how_to_reg</span>
                <span>Hoàn Tất Đăng Ký Tài Khoản</span>
              </>
            )}
          </button>
        </form>

        <div className="mt-6 pt-6 border-t border-[#DDE7E1] text-center text-sm text-[#5E6F66]">
          Đã có tài khoản bệnh nhân?{' '}
          <Link to="/login" className="font-semibold text-[#006a41] hover:underline">
            Đăng nhập ngay
          </Link>
        </div>
      </div>
    </div>
  );
};
