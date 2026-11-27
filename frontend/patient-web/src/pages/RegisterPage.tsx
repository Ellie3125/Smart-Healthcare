import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { apiClient } from '../api/client';
import { getVNDateISO } from '../shared/lib/datetime';
import { LoginResponse } from '../types/auth';

type RegisterField =
  | 'userName'
  | 'email'
  | 'phoneNumber'
  | 'password'
  | 'confirmPassword'
  | 'fullName'
  | 'dateOfBirth'
  | 'nationalId'
  | 'selfPhoneNumber'
  | 'address';

interface RegisterFormValues {
  userName: string;
  email: string;
  phoneNumber: string;
  password: string;
  confirmPassword: string;
  createSelf: boolean;
  fullName: string;
  dateOfBirth: string;
  nationalId: string;
  selfPhoneNumber: string;
  address: string;
}

const VIETNAM_PHONE_REGEX = /^(0(3|5|7|8|9)\d{8}|02\d{8,9}|\+84((3|5|7|8|9)\d{8}|2\d{8,9}))$/;
const SELF_FIELDS: RegisterField[] = ['fullName', 'dateOfBirth', 'nationalId', 'selfPhoneNumber', 'address'];
const ALL_FIELDS: RegisterField[] = [
  'userName',
  'email',
  'phoneNumber',
  'password',
  'confirmPassword',
  ...SELF_FIELDS,
];

function isValidVietnamesePhone(rawPhone: string): boolean {
  const trimmed = rawPhone.trim();
  if (!trimmed || trimmed.length > 32) return false;
  const compact = trimmed.replace(/[\s.-]/g, '');
  return VIETNAM_PHONE_REGEX.test(compact);
}

function isValidCalendarDateISO(raw: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(raw)) return false;
  const [y, m, d] = raw.split('-').map(Number);
  const parsed = new Date(Date.UTC(y, m - 1, d));
  return parsed.getUTCFullYear() === y && parsed.getUTCMonth() === m - 1 && parsed.getUTCDate() === d;
}

function subtractYearsISO(isoDate: string, years: number): string {
  const [y, m, d] = isoDate.split('-').map(Number);
  const targetYear = y - years;
  const maxDayInMonth = new Date(Date.UTC(targetYear, m, 0)).getUTCDate();
  const clampedDay = Math.min(d, maxDayInMonth);
  return `${String(targetYear).padStart(4, '0')}-${String(m).padStart(2, '0')}-${String(clampedDay).padStart(2, '0')}`;
}

function validateRegisterField(
  field: RegisterField,
  values: RegisterFormValues,
  touchedFields: Partial<Record<RegisterField, boolean>>,
): string | undefined {
  switch (field) {
    case 'userName': {
      const trimmed = values.userName.trim();
      if (!trimmed) return 'Tên đăng nhập không được để trống.';
      if (/\s/.test(values.userName)) return 'Tên đăng nhập không được chứa khoảng trắng.';
      if (trimmed.length < 3) return 'Tên đăng nhập phải có ít nhất 3 ký tự.';
      if (trimmed.length > 200) return 'Tên đăng nhập tối đa 200 ký tự.';
      return undefined;
    }
    case 'email': {
      const trimmed = values.email.trim();
      if (trimmed) {
        if (trimmed.length > 256 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)) {
          return 'Định dạng email không hợp lệ.';
        }
        return undefined;
      }
      if (!values.phoneNumber.trim() && touchedFields.phoneNumber) {
        return 'Vui lòng cung cấp email hoặc số điện thoại để đăng ký.';
      }
      return undefined;
    }
    case 'phoneNumber': {
      const trimmed = values.phoneNumber.trim();
      if (trimmed) {
        if (!isValidVietnamesePhone(trimmed)) {
          return 'Số điện thoại không đúng định dạng Việt Nam.';
        }
        return undefined;
      }
      if (!values.email.trim() && touchedFields.email) {
        return 'Vui lòng cung cấp email hoặc số điện thoại để đăng ký.';
      }
      return undefined;
    }
    case 'password': {
      if (!values.password) return 'Mật khẩu không được để trống.';
      if (values.password.length < 6) return 'Mật khẩu phải có ít nhất 6 ký tự.';
      return undefined;
    }
    case 'confirmPassword': {
      if (!values.confirmPassword) return 'Vui lòng xác nhận lại mật khẩu.';
      if (values.password !== values.confirmPassword) return 'Mật khẩu xác nhận không khớp.';
      return undefined;
    }
    case 'fullName': {
      if (!values.createSelf) return undefined;
      const trimmed = values.fullName.trim();
      if (!trimmed) return 'Họ và tên không được để trống.';
      if (trimmed.length > 200) return 'Họ và tên tối đa 200 ký tự.';
      return undefined;
    }
    case 'dateOfBirth': {
      if (!values.createSelf) return undefined;
      if (!values.dateOfBirth) return 'Ngày sinh không được để trống.';
      if (!isValidCalendarDateISO(values.dateOfBirth)) return 'Ngày sinh không hợp lệ.';
      const todayISO = getVNDateISO();
      const minDobISO = subtractYearsISO(todayISO, 120);
      if (values.dateOfBirth > todayISO) return 'Ngày sinh không thể ở tương lai.';
      if (values.dateOfBirth < minDobISO) return 'Ngày sinh không được quá 120 tuổi.';
      return undefined;
    }
    case 'nationalId': {
      if (!values.createSelf) return undefined;
      const trimmed = values.nationalId.trim();
      if (trimmed && !/^[0-9]{12}$/.test(trimmed)) {
        return 'Số CCCD/định danh cá nhân phải gồm đúng 12 chữ số.';
      }
      return undefined;
    }
    case 'selfPhoneNumber': {
      if (!values.createSelf) return undefined;
      const trimmed = values.selfPhoneNumber.trim();
      if (!trimmed) return 'Số điện thoại liên hệ của hồ sơ bệnh nhân không được để trống.';
      if (!isValidVietnamesePhone(trimmed)) {
        return 'Số điện thoại hồ sơ không đúng định dạng Việt Nam.';
      }
      return undefined;
    }
    case 'address': {
      if (!values.createSelf) return undefined;
      const trimmed = values.address.trim();
      if (!trimmed) return 'Địa chỉ thường trú không được để trống.';
      if (trimmed.length > 200) return 'Địa chỉ thường trú tối đa 200 ký tự.';
      return undefined;
    }
  }
}

const FieldError: React.FC<{ id: string; message?: string }> = ({ id, message }) =>
  message ? (
    <p id={id} role="alert" className="mt-1.5 text-xs text-error flex items-center gap-1 font-medium">
      <span className="material-symbols-outlined text-[15px] shrink-0" aria-hidden="true">error</span>
      <span>{message}</span>
    </p>
  ) : null;

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
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<RegisterField, string>>>({});
  const [touched, setTouched] = useState<Partial<Record<RegisterField, boolean>>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const todayISO = getVNDateISO();

  const currentValues: RegisterFormValues = {
    userName,
    email,
    phoneNumber,
    password,
    confirmPassword,
    createSelf,
    fullName,
    dateOfBirth,
    nationalId,
    selfPhoneNumber,
    address,
  };

  const syncFieldsValidation = (
    fieldsToCheck: RegisterField[],
    nextValues: RegisterFormValues,
    nextTouched: Partial<Record<RegisterField, boolean>>,
  ) => {
    setFieldErrors((prev) => {
      const next = { ...prev };
      for (const f of fieldsToCheck) {
        const err = validateRegisterField(f, nextValues, nextTouched);
        if (err) {
          next[f] = err;
        } else {
          delete next[f];
        }
      }
      return next;
    });
  };

  const handleFieldBlur = (field: RegisterField) => {
    const nextTouched: Partial<Record<RegisterField, boolean>> = { ...touched, [field]: true };
    setTouched(nextTouched);

    const fieldsToCheck: RegisterField[] = [field];
    if (field === 'password' && (nextTouched.confirmPassword || fieldErrors.confirmPassword || confirmPassword)) {
      fieldsToCheck.push('confirmPassword');
    }
    if (field === 'email' && (nextTouched.phoneNumber || fieldErrors.phoneNumber)) {
      fieldsToCheck.push('phoneNumber');
    }
    if (field === 'phoneNumber') {
      if (nextTouched.email || fieldErrors.email) {
        fieldsToCheck.push('email');
      }
      if (!selfPhoneEdited && (nextTouched.selfPhoneNumber || fieldErrors.selfPhoneNumber)) {
        fieldsToCheck.push('selfPhoneNumber');
      }
    }

    syncFieldsValidation(fieldsToCheck, currentValues, nextTouched);
  };

  const handleFieldChange = (field: RegisterField, nextValue: string) => {
    const nextValues: RegisterFormValues = { ...currentValues, [field]: nextValue };
    if (field === 'phoneNumber' && !selfPhoneEdited) {
      nextValues.selfPhoneNumber = nextValue;
    }

    const fieldsToCheck: RegisterField[] = [];
    if (field === 'userName' && /\s/.test(nextValue)) {
      setFieldErrors((prev) => ({
        ...prev,
        userName: 'Tên đăng nhập không được chứa khoảng trắng.',
      }));
      return;
    }

    if (touched[field] || fieldErrors[field]) {
      fieldsToCheck.push(field);
    }
    if (field === 'password' && (touched.confirmPassword || fieldErrors.confirmPassword)) {
      fieldsToCheck.push('confirmPassword');
    }
    if (field === 'email' && (touched.phoneNumber || fieldErrors.phoneNumber)) {
      fieldsToCheck.push('phoneNumber');
    }
    if (field === 'phoneNumber') {
      if (touched.email || fieldErrors.email) {
        fieldsToCheck.push('email');
      }
      if (!selfPhoneEdited && (touched.selfPhoneNumber || fieldErrors.selfPhoneNumber)) {
        fieldsToCheck.push('selfPhoneNumber');
      }
    }

    if (fieldsToCheck.length > 0) {
      syncFieldsValidation(fieldsToCheck, nextValues, touched);
    }
  };

  const inputClass = (hasError: boolean, bgClass = 'bg-surface-container-low/55') =>
    `w-full h-11 px-3.5 rounded-xl border text-sm ${bgClass} focus:bg-white transition-colors focus:outline-none focus:ring-2 ${
      hasError
        ? 'border-error bg-error-container/40 focus:border-error focus:ring-error/20'
        : 'border-outline-variant/75 focus:border-primary focus:ring-primary/20'
    }`;

  const validate = (): boolean => {
    const allTouched: Partial<Record<RegisterField, boolean>> = {};
    for (const f of ALL_FIELDS) {
      if (createSelf || !SELF_FIELDS.includes(f)) {
        allTouched[f] = true;
      }
    }
    setTouched((prev) => ({ ...prev, ...allTouched }));

    const errors: Partial<Record<RegisterField, string>> = {};
    for (const f of ALL_FIELDS) {
      const err = validateRegisterField(f, currentValues, allTouched);
      if (err) {
        errors[f] = err;
      }
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
      navigate('/booking', { replace: true });
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

        <form onSubmit={handleSubmit} className="space-y-6" noValidate>
          {/* Section 1: Thông tin tài khoản */}
          <div>
            <h2 className="text-xs font-bold uppercase tracking-wider text-primary border-b border-surface-container-low pb-2 mb-4">
              1. Thông Tin Tài Khoản Đăng Nhập
            </h2>
            <div className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="reg-username">
                  Tên đăng nhập <span className="text-error">*</span>
                </label>
                <input
                  id="reg-username"
                  type="text"
                  value={userName}
                  onChange={(e) => {
                    const val = e.target.value;
                    setUserName(val);
                    handleFieldChange('userName', val);
                  }}
                  onBlur={() => handleFieldBlur('userName')}
                  onKeyDown={(e) => {
                    if (e.key === ' ') {
                      e.preventDefault();
                      setFieldErrors((prev) => ({
                        ...prev,
                        userName: 'Tên đăng nhập không được chứa khoảng trắng.',
                      }));
                    }
                  }}
                  aria-invalid={Boolean(fieldErrors.userName)}
                  aria-describedby={fieldErrors.userName ? 'reg-username-error' : undefined}
                  placeholder="Ví dụ: nguyenvanan"
                  className={inputClass(Boolean(fieldErrors.userName))}
                />
                <FieldError id="reg-username-error" message={fieldErrors.userName} />
              </div>

              <div>
                <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="reg-email">
                  Địa chỉ Email
                </label>
                <input
                  id="reg-email"
                  type="email"
                  value={email}
                  onChange={(e) => {
                    const val = e.target.value;
                    setEmail(val);
                    handleFieldChange('email', val);
                  }}
                  onBlur={() => handleFieldBlur('email')}
                  aria-invalid={Boolean(fieldErrors.email)}
                  aria-describedby={fieldErrors.email ? 'reg-email-error' : undefined}
                  placeholder="name@example.com"
                  className={inputClass(Boolean(fieldErrors.email))}
                />
                <FieldError id="reg-email-error" message={fieldErrors.email} />
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
                    handleFieldChange('phoneNumber', next);
                  }}
                  onBlur={() => handleFieldBlur('phoneNumber')}
                  aria-invalid={Boolean(fieldErrors.phoneNumber)}
                  aria-describedby={fieldErrors.phoneNumber ? 'reg-phone-error' : undefined}
                  placeholder="0912345678"
                  className={inputClass(Boolean(fieldErrors.phoneNumber))}
                />
                <FieldError id="reg-phone-error" message={fieldErrors.phoneNumber} />
              </div>

              <div>
                <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="reg-pwd">
                  Mật khẩu <span className="text-error">*</span>
                </label>
                <input
                  id="reg-pwd"
                  type="password"
                  value={password}
                  onChange={(e) => {
                    const val = e.target.value;
                    setPassword(val);
                    handleFieldChange('password', val);
                  }}
                  onBlur={() => handleFieldBlur('password')}
                  aria-invalid={Boolean(fieldErrors.password)}
                  aria-describedby={fieldErrors.password ? 'reg-pwd-error' : undefined}
                  placeholder="Tối thiểu 6 ký tự"
                  className={inputClass(Boolean(fieldErrors.password))}
                />
                <FieldError id="reg-pwd-error" message={fieldErrors.password} />
              </div>

              <div>
                <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="reg-confirm">
                  Xác nhận lại mật khẩu <span className="text-error">*</span>
                </label>
                <input
                  id="reg-confirm"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => {
                    const val = e.target.value;
                    setConfirmPassword(val);
                    handleFieldChange('confirmPassword', val);
                  }}
                  onBlur={() => handleFieldBlur('confirmPassword')}
                  aria-invalid={Boolean(fieldErrors.confirmPassword)}
                  aria-describedby={fieldErrors.confirmPassword ? 'reg-confirm-error' : undefined}
                  placeholder="Nhập lại đúng mật khẩu phía trên"
                  className={inputClass(Boolean(fieldErrors.confirmPassword))}
                />
                <FieldError id="reg-confirm-error" message={fieldErrors.confirmPassword} />
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
                  onChange={(e) => {
                    const checked = e.target.checked;
                    setCreateSelf(checked);
                    if (!checked) {
                      setFieldErrors((prev) => {
                        const next = { ...prev };
                        for (const f of SELF_FIELDS) delete next[f];
                        return next;
                      });
                      setTouched((prev) => {
                        const next = { ...prev };
                        for (const f of SELF_FIELDS) delete next[f];
                        return next;
                      });
                    }
                  }}
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
                      onChange={(e) => {
                        const val = e.target.value;
                        setFullName(val);
                        handleFieldChange('fullName', val);
                      }}
                      onBlur={() => handleFieldBlur('fullName')}
                      aria-invalid={Boolean(fieldErrors.fullName)}
                      aria-describedby={fieldErrors.fullName ? 'self-fullname-error' : undefined}
                      placeholder="NGUYỄN VĂN AN"
                      className={inputClass(Boolean(fieldErrors.fullName), 'bg-white')}
                    />
                    <FieldError id="self-fullname-error" message={fieldErrors.fullName} />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="self-dob">
                      Ngày sinh <span className="text-error">*</span>
                    </label>
                    <input
                      id="self-dob"
                      type="date"
                      max={todayISO}
                      value={dateOfBirth}
                      onChange={(e) => {
                        const val = e.target.value;
                        setDateOfBirth(val);
                        handleFieldChange('dateOfBirth', val);
                      }}
                      onBlur={() => handleFieldBlur('dateOfBirth')}
                      aria-invalid={Boolean(fieldErrors.dateOfBirth)}
                      aria-describedby={fieldErrors.dateOfBirth ? 'self-dob-error' : undefined}
                      className={inputClass(Boolean(fieldErrors.dateOfBirth), 'bg-white')}
                    />
                    <FieldError id="self-dob-error" message={fieldErrors.dateOfBirth} />
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
                      onChange={(e) => {
                        const val = e.target.value;
                        setNationalId(val);
                        handleFieldChange('nationalId', val);
                      }}
                      onBlur={() => handleFieldBlur('nationalId')}
                      aria-invalid={Boolean(fieldErrors.nationalId)}
                      aria-describedby={fieldErrors.nationalId ? 'self-cccd-error' : undefined}
                      placeholder="12 chữ số"
                      className={inputClass(Boolean(fieldErrors.nationalId), 'bg-white')}
                    />
                    <FieldError id="self-cccd-error" message={fieldErrors.nationalId} />
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
                        const val = e.target.value;
                        setSelfPhoneEdited(true);
                        setSelfPhoneNumber(val);
                        handleFieldChange('selfPhoneNumber', val);
                      }}
                      onBlur={() => handleFieldBlur('selfPhoneNumber')}
                      aria-invalid={Boolean(fieldErrors.selfPhoneNumber)}
                      aria-describedby={fieldErrors.selfPhoneNumber ? 'self-phone-error' : undefined}
                      placeholder="0912345678"
                      className={inputClass(Boolean(fieldErrors.selfPhoneNumber), 'bg-white')}
                    />
                    <FieldError id="self-phone-error" message={fieldErrors.selfPhoneNumber} />
                  </div>

                  <div className="sm:col-span-2">
                    <label className="block text-xs font-semibold text-on-surface mb-1" htmlFor="self-address">
                      Địa chỉ liên hệ / Thường trú <span className="text-error">*</span>
                    </label>
                    <input
                      id="self-address"
                      type="text"
                      value={address}
                      onChange={(e) => {
                        const val = e.target.value;
                        setAddress(val);
                        handleFieldChange('address', val);
                      }}
                      onBlur={() => handleFieldBlur('address')}
                      aria-invalid={Boolean(fieldErrors.address)}
                      aria-describedby={fieldErrors.address ? 'self-address-error' : undefined}
                      placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/TP"
                      className={inputClass(Boolean(fieldErrors.address), 'bg-white')}
                    />
                    <FieldError id="self-address-error" message={fieldErrors.address} />
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
