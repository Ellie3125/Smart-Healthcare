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
    <div className="min-h-screen bg-surface py-10 px-4 flex justify-center items-center font-body-md text-on-surface">
      <div className="w-full max-w-3xl bg-surface-container-lowest rounded-2xl border border-surface-container-highest/70 shadow-[0_8px_32px_rgba(15,31,24,0.06)] p-6 sm:p-10">
        {/* Stitch 2-step onboarding indicator */}
        <div className="flex flex-wrap items-center justify-center gap-3 mb-7 pb-6 border-b border-surface-container-low">
          <div className="inline-flex items-center gap-2 rounded-full bg-secondary-container/70 px-3.5 py-1.5 text-xs font-semibold text-[#005231]">
            <span className="w-5 h-5 rounded-full bg-primary text-white flex items-center justify-center text-[11px] font-bold">1</span>
            <span>Thông tin tài khoản</span>
          </div>
          <span className="h-px w-6 bg-outline-variant/60" aria-hidden="true" />
          <div
            className={`inline-flex items-center gap-2 rounded-full px-3.5 py-1.5 text-xs font-semibold transition-colors ${
              createSelf
                ? 'bg-secondary-container/70 text-[#005231]'
                : 'bg-surface-container-low text-on-surface-variant'
            }`}
          >
            <span
              className={`w-5 h-5 rounded-full flex items-center justify-center text-[11px] font-bold ${
                createSelf ? 'bg-primary text-white' : 'bg-surface-container-highest text-on-surface-variant'
              }`}
            >
              2
            </span>
            <span>Hồ sơ y tế bản thân</span>
          </div>
        </div>

        <div className="text-center mb-8">
          <div className="w-12 h-12 rounded-2xl bg-surface-container-low flex items-center justify-center text-primary mx-auto mb-3 shadow-sm">
            <span className="material-symbols-outlined text-[28px]">person_add</span>
          </div>
          <h1 className="text-2xl font-bold text-on-surface tracking-tight">Đăng Ký Tài Khoản Bệnh Nhân</h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Tạo tài khoản quản lý hồ sơ khám chữa bệnh cho bản thân và người thân trong gia đình
          </p>
        </div>

        {errorMsg && (
          <div className="mb-6 p-4 rounded-xl bg-error-container flex items-start gap-3 text-sm text-[#93000a]">
            <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
            <div className="flex-1">{errorMsg}</div>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Section 1: Thông tin tài khoản */}
          <div>
            <h2 className="text-xs font-bold uppercase tracking-wider text-primary border-b border-surface-container-low pb-2 mb-4">
              1. Thông Tin Tài Khoản Đăng Nhập
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="reg-username">
                  Tên đăng nhập <span className="text-error">*</span>
                </label>
                <input
                  id="reg-username"
                  type="text"
                  value={userName}
                  onChange={(e) => setUserName(e.target.value)}
                  placeholder="Ví dụ: nguyenvanan"
                  className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
                    fieldErrors.userName ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                  } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                />
                {fieldErrors.userName && <p className="text-xs text-error mt-1">{fieldErrors.userName}</p>}
              </div>

              <div>
                <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="reg-email">
                  Địa chỉ Email
                </label>
                <input
                  id="reg-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="name@example.com"
                  className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
                    fieldErrors.email ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                  } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                />
                {fieldErrors.email && <p className="text-xs text-error mt-1">{fieldErrors.email}</p>}
              </div>

              <div>
                <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="reg-phone">
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
                  className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
                    fieldErrors.phoneNumber ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                  } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                />
                {fieldErrors.phoneNumber && <p className="text-xs text-error mt-1">{fieldErrors.phoneNumber}</p>}
              </div>

              <div>
                <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="reg-pwd">
                  Mật khẩu <span className="text-error">*</span>
                </label>
                <input
                  id="reg-pwd"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Tối thiểu 6 ký tự"
                  className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
                    fieldErrors.password ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                  } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                />
                {fieldErrors.password && <p className="text-xs text-error mt-1">{fieldErrors.password}</p>}
              </div>

              <div className="sm:col-span-2">
                <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="reg-confirm">
                  Xác nhận lại mật khẩu <span className="text-error">*</span>
                </label>
                <input
                  id="reg-confirm"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="Nhập lại đúng mật khẩu phía trên"
                  className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
                    fieldErrors.confirmPassword ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                  } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                />
                {fieldErrors.confirmPassword && (
                  <p className="text-xs text-error mt-1">{fieldErrors.confirmPassword}</p>
                )}
              </div>
            </div>
          </div>

          {/* Section 2: Tạo hồ sơ bản thân */}
          <div>
            <div className="flex items-center justify-between border-b border-surface-container-low pb-2 mb-4">
              <h2 className="text-xs font-bold uppercase tracking-wider text-primary">
                2. Hồ Sơ Bệnh Nhân Bản Thân (Self)
              </h2>
              <label className="flex items-center gap-2 cursor-pointer text-xs font-medium text-on-surface">
                <input
                  type="checkbox"
                  checked={createSelf}
                  onChange={(e) => setCreateSelf(e.target.checked)}
                  className="rounded border-outline-variant text-primary focus:ring-primary"
                />
                <span>Tạo hồ sơ ngay</span>
              </label>
            </div>

            {createSelf ? (
              <div className="space-y-4 bg-surface-container-low/75 p-5 rounded-2xl">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="sm:col-span-2">
                    <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="self-fullname">
                      Họ và tên đầy đủ <span className="text-error">*</span>
                    </label>
                    <input
                      id="self-fullname"
                      type="text"
                      value={fullName}
                      onChange={(e) => setFullName(e.target.value)}
                      placeholder="NGUYỄN VĂN AN"
                      className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white transition-colors ${
                        fieldErrors.fullName ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                      } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                    />
                    {fieldErrors.fullName && <p className="text-xs text-error mt-1">{fieldErrors.fullName}</p>}
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="self-dob">
                      Ngày sinh <span className="text-error">*</span>
                    </label>
                    <input
                      id="self-dob"
                      type="date"
                      value={dateOfBirth}
                      onChange={(e) => setDateOfBirth(e.target.value)}
                      className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white transition-colors ${
                        fieldErrors.dateOfBirth ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                      } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                    />
                    {fieldErrors.dateOfBirth && <p className="text-xs text-error mt-1">{fieldErrors.dateOfBirth}</p>}
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="self-gender">
                      Giới tính <span className="text-error">*</span>
                    </label>
                    <select
                      id="self-gender"
                      value={gender}
                      onChange={(e) => setGender(e.target.value as 'Male' | 'Female' | 'Other')}
                      className="w-full h-11 px-3.5 rounded-xl border border-outline-variant/75 bg-white text-sm focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                    >
                      <option value="Male">Nam</option>
                      <option value="Female">Nữ</option>
                      <option value="Other">Khác</option>
                    </select>
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="self-cccd">
                      Số CCCD / Định danh (để trống nếu chưa có)
                    </label>
                    <input
                      id="self-cccd"
                      type="text"
                      maxLength={12}
                      value={nationalId}
                      onChange={(e) => setNationalId(e.target.value)}
                      placeholder="12 chữ số"
                      className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white transition-colors ${
                        fieldErrors.nationalId ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                      } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                    />
                    {fieldErrors.nationalId && <p className="text-xs text-error mt-1">{fieldErrors.nationalId}</p>}
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="self-phone">
                      Số điện thoại hồ sơ <span className="text-error">*</span>
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
                      className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white transition-colors ${
                        fieldErrors.selfPhoneNumber ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                      } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                    />
                    {fieldErrors.selfPhoneNumber && (
                      <p className="text-xs text-error mt-1">{fieldErrors.selfPhoneNumber}</p>
                    )}
                  </div>

                  <div className="sm:col-span-2">
                    <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="self-address">
                      Địa chỉ liên hệ / Thường trú <span className="text-error">*</span>
                    </label>
                    <input
                      id="self-address"
                      type="text"
                      value={address}
                      onChange={(e) => setAddress(e.target.value)}
                      placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/TP"
                      className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white transition-colors ${
                        fieldErrors.address ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
                      } focus:outline-none focus:ring-2 focus:ring-primary/20`}
                    />
                    {fieldErrors.address && <p className="text-xs text-error mt-1">{fieldErrors.address}</p>}
                  </div>
                </div>

                <div className="p-3.5 bg-surface-container-lowest rounded-xl text-xs text-on-surface-variant flex items-start gap-2.5 shadow-sm">
                  <span className="material-symbols-outlined text-[18px] text-primary shrink-0 mt-0.5">info</span>
                  <p>
                    <strong className="text-on-surface">Quy định bảo mật &amp; xác thực (FR01):</strong> Hệ thống không yêu cầu và không hỗ trợ tải ảnh giấy tờ tùy thân. Khi đến khám tại cơ sở y tế, vui lòng mang theo bản gốc thẻ CCCD để tiếp tân đối chiếu tại quầy.
                  </p>
                </div>
              </div>
            ) : (
              <p className="text-xs text-on-surface-variant italic">
                Bạn có thể bổ sung hồ sơ bản thân hoặc người thân sau khi đăng ký thành công.
              </p>
            )}
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full h-12 rounded-xl bg-primary text-white font-semibold text-sm hover:bg-[#005231] transition-colors flex items-center justify-center gap-2 shadow-[0_2px_10px_rgba(0,106,65,0.22)] cursor-pointer disabled:opacity-50"
          >
            {isSubmitting ? (
              <>
                <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
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

        <div className="mt-6 pt-5 border-t border-surface-container-low text-center text-sm text-on-surface-variant">
          Đã có tài khoản bệnh nhân?{' '}
          <Link to="/login" className="font-semibold text-primary hover:underline">
            Đăng nhập ngay
          </Link>
        </div>
      </div>
    </div>
  );
};
