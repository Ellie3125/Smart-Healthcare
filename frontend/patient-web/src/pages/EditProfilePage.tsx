import React, { useEffect, useState } from 'react';
import { useNavigate, useParams, Link } from 'react-router-dom';
import { apiClient } from '../api/client';
import { PatientProfileDto, Gender, VerificationStatus } from '../types/patient';

export const EditProfilePage: React.FC = () => {
  const { id } = useParams<{ id?: string }>();
  const navigate = useNavigate();
  const isEditing = !!id;

  const [fullName, setFullName] = useState('');
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [gender, setGender] = useState<Gender>('Male');
  const [nationalId, setNationalId] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [address, setAddress] = useState('');
  const [status, setStatus] = useState<VerificationStatus>('Pending');

  const [isLoading, setIsLoading] = useState(isEditing);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const isVerified = status === 'Verified';

  useEffect(() => {
    if (isEditing && id) {
      const loadProfile = async () => {
        setIsLoading(true);
        try {
          const res = await apiClient.get<PatientProfileDto>(`/api/patient/profiles/${id}`);
          const p = res.data;
          setFullName(p.fullName);
          setDateOfBirth(p.dateOfBirth);
          setGender(p.gender as Gender);
          setNationalId(p.nationalId || '');
          setPhoneNumber(p.phoneNumber);
          setAddress(p.address);
          setStatus(p.verificationStatus);
        } catch {
          setErrorMsg('Không thể tải thông tin hồ sơ bệnh nhân.');
        } finally {
          setIsLoading(false);
        }
      };
      loadProfile();
    }
  }, [id, isEditing]);

  const validate = (): boolean => {
    const errors: Record<string, string> = {};

    if (!isVerified) {
      if (!fullName.trim()) errors.fullName = 'Họ và tên không được để trống.';
      if (fullName.trim().length > 200) errors.fullName = 'Họ và tên tối đa 200 ký tự.';

      if (!dateOfBirth) {
        errors.dateOfBirth = 'Ngày sinh không được để trống.';
      } else {
        const dob = new Date(dateOfBirth);
        const today = new Date();
        if (dob > today) errors.dateOfBirth = 'Ngày sinh không thể ở tương lai.';
        const age = today.getFullYear() - dob.getFullYear();
        if (age > 120) errors.dateOfBirth = 'Ngày sinh không được quá 120 tuổi.';
      }

      if (nationalId.trim() && !/^[0-9]{12}$/.test(nationalId.trim())) {
        errors.nationalId = 'Số CCCD/định danh phải gồm đúng 12 chữ số.';
      }
    }

    if (!phoneNumber.trim()) {
      errors.phoneNumber = 'Số điện thoại không được để trống.';
    } else if (!/^(0|\+84)[0-9]{9,10}$/.test(phoneNumber.replace(/\s/g, ''))) {
      errors.phoneNumber = 'Số điện thoại không đúng định dạng Việt Nam.';
    }

    if (!address.trim()) errors.address = 'Địa chỉ thường trú không được để trống.';

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) {
      setErrorMsg('Vui lòng kiểm tra lại thông tin có lỗi bên dưới.');
      return;
    }

    setIsSubmitting(true);
    setErrorMsg(null);

    try {
      if (isEditing && id) {
        const payload = {
          phoneNumber: phoneNumber.trim(),
          address: address.trim(),
          fullName: fullName.trim(),
          dateOfBirth: dateOfBirth,
          gender: gender,
          nationalId: nationalId.trim() || null,
        };

        await apiClient.put(`/api/patient/profiles/${id}`, payload);
        navigate('/profiles');
      } else {
        const payload = {
          fullName: fullName.trim(),
          dateOfBirth: dateOfBirth,
          gender: gender,
          nationalId: nationalId.trim() || undefined,
          phoneNumber: phoneNumber.trim(),
          address: address.trim(),
          relationship: 'Self',
        };

        await apiClient.post('/api/patient/profiles', payload);
        navigate('/profiles');
      }
    } catch (err: unknown) {
      const errorObj = err as { response?: { status?: number; data?: { detail?: string; title?: string } } };
      if (errorObj.response?.status === 409) {
        setErrorMsg(errorObj.response.data?.detail || 'Số CCCD đã được đăng ký cho hồ sơ khác. Vui lòng liên hệ quầy tiếp tân.');
      } else if (errorObj.response?.data?.detail) {
        setErrorMsg(errorObj.response.data.detail);
      } else {
        setErrorMsg('Không thể lưu thông tin hồ sơ. Vui lòng thử lại sau.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="py-20 text-center text-[#5E6F66]">
        <div className="w-8 h-8 border-3 border-[#006a41] border-t-transparent rounded-full animate-spin mx-auto mb-3"></div>
        <p className="text-sm">Đang tải thông tin hồ sơ...</p>
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto">
      <div className="mb-6 flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-on-surface tracking-tight">
            {isEditing ? 'Cập Nhật Hồ Sơ Bệnh Nhân' : 'Tạo Mới Hồ Sơ Bản Thân'}
          </h1>
          <p className="text-sm text-on-surface-variant mt-1">
            {isEditing
              ? 'Chỉnh sửa thông tin liên hệ và hành chính của hồ sơ'
              : 'Tạo hồ sơ y tế đại diện chính cho tài khoản của bạn'}
          </p>
        </div>
        <Link
          to="/profiles"
          className="h-9 px-3.5 rounded-xl bg-surface-container-low text-on-surface-variant hover:bg-surface-container hover:text-on-surface text-xs font-semibold flex items-center gap-1 transition-colors shrink-0"
        >
          <span className="material-symbols-outlined text-[18px]">arrow_back</span>
          <span>Quay lại</span>
        </Link>
      </div>

      {isVerified && (
        <div className="mb-6 p-4 rounded-xl bg-secondary-container/50 flex items-start gap-3 text-sm text-[#005231]">
          <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">verified</span>
          <div>
            <p className="font-semibold mb-0.5">Hồ sơ đã được xác minh danh tính tại quầy.</p>
            <p className="text-xs leading-relaxed">
              Theo quy định bảo mật y tế, bạn chỉ có thể tự cập nhật số điện thoại và địa chỉ liên lạc. Để điều chỉnh họ tên, ngày sinh, giới tính hoặc số CCCD, vui lòng mang giấy tờ gốc đến quầy tiếp tân cơ sở y tế để được cập nhật.
            </p>
          </div>
        </div>
      )}

      {errorMsg && (
        <div className="mb-6 p-4 rounded-xl bg-error-container flex items-start gap-3 text-sm text-[#93000a]">
          <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
          <div className="flex-1">{errorMsg}</div>
        </div>
      )}

      <form
        onSubmit={handleSubmit}
        className="bg-surface-container-lowest rounded-2xl border border-surface-container-highest/70 shadow-[0_1px_12px_rgba(15,31,24,0.05)] p-6 sm:p-8 space-y-5"
      >
        <div>
          <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="edit-name">
            Họ và tên đầy đủ <span className="text-error">*</span>
          </label>
          <input
            id="edit-name"
            type="text"
            disabled={isVerified}
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="NGUYỄN VĂN AN"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm transition-colors ${
              isVerified
                ? 'bg-surface-container-low text-outline cursor-not-allowed border-transparent'
                : 'bg-surface-container-low/55 border-outline-variant/75 focus:bg-white focus:border-primary'
            } ${fieldErrors.fullName ? 'border-error bg-error-container/40' : ''} focus:outline-none focus:ring-2 focus:ring-primary/20`}
          />
          {fieldErrors.fullName && <p className="text-xs text-error mt-1">{fieldErrors.fullName}</p>}
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="edit-dob">
              Ngày sinh <span className="text-error">*</span>
            </label>
            <input
              id="edit-dob"
              type="date"
              disabled={isVerified}
              value={dateOfBirth}
              onChange={(e) => setDateOfBirth(e.target.value)}
              className={`w-full h-11 px-3.5 rounded-xl border text-sm transition-colors ${
                isVerified
                  ? 'bg-surface-container-low text-outline cursor-not-allowed border-transparent'
                  : 'bg-surface-container-low/55 border-outline-variant/75 focus:bg-white focus:border-primary'
              } ${fieldErrors.dateOfBirth ? 'border-error bg-error-container/40' : ''} focus:outline-none focus:ring-2 focus:ring-primary/20`}
            />
            {fieldErrors.dateOfBirth && <p className="text-xs text-error mt-1">{fieldErrors.dateOfBirth}</p>}
          </div>

          <div>
            <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="edit-gender">
              Giới tính <span className="text-error">*</span>
            </label>
            <select
              id="edit-gender"
              disabled={isVerified}
              value={gender}
              onChange={(e) => setGender(e.target.value as Gender)}
              className={`w-full h-11 px-3.5 rounded-xl border text-sm transition-colors ${
                isVerified
                  ? 'bg-surface-container-low text-outline cursor-not-allowed border-transparent'
                  : 'bg-surface-container-low/55 border-outline-variant/75 focus:bg-white focus:border-primary'
              } focus:outline-none focus:ring-2 focus:ring-primary/20`}
            >
              <option value="Male">Nam</option>
              <option value="Female">Nữ</option>
              <option value="Other">Khác</option>
            </select>
          </div>
        </div>

        <div>
          <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="edit-cccd">
            Số CCCD / Mã định danh cá nhân (cho phép để trống)
          </label>
          <input
            id="edit-cccd"
            type="text"
            maxLength={12}
            disabled={isVerified}
            value={nationalId}
            onChange={(e) => setNationalId(e.target.value)}
            placeholder="12 chữ số"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm transition-colors ${
              isVerified
                ? 'bg-surface-container-low text-outline cursor-not-allowed border-transparent'
                : 'bg-surface-container-low/55 border-outline-variant/75 focus:bg-white focus:border-primary'
            } ${fieldErrors.nationalId ? 'border-error bg-error-container/40' : ''} focus:outline-none focus:ring-2 focus:ring-primary/20`}
          />
          {fieldErrors.nationalId && <p className="text-xs text-error mt-1">{fieldErrors.nationalId}</p>}
        </div>

        <div>
          <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="edit-phone">
            Số điện thoại liên hệ <span className="text-error">*</span>
          </label>
          <input
            id="edit-phone"
            type="tel"
            value={phoneNumber}
            onChange={(e) => setPhoneNumber(e.target.value)}
            placeholder="0912345678"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
              fieldErrors.phoneNumber ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
            } focus:outline-none focus:ring-2 focus:ring-primary/20`}
          />
          {fieldErrors.phoneNumber && <p className="text-xs text-error mt-1">{fieldErrors.phoneNumber}</p>}
        </div>

        <div>
          <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="edit-address">
            Địa chỉ thường trú / Nơi ở hiện tại <span className="text-error">*</span>
          </label>
          <input
            id="edit-address"
            type="text"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/TP"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
              fieldErrors.address ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
            } focus:outline-none focus:ring-2 focus:ring-primary/20`}
          />
          {fieldErrors.address && <p className="text-xs text-error mt-1">{fieldErrors.address}</p>}
        </div>

        <div className="pt-4 border-t border-surface-container-low flex items-center justify-end gap-3">
          <Link
            to="/profiles"
            className="h-11 px-5 rounded-xl bg-surface-container-low text-on-surface-variant hover:bg-surface-container text-sm font-semibold flex items-center transition-colors"
          >
            Hủy
          </Link>
          <button
            type="submit"
            disabled={isSubmitting}
            className="h-11 px-6 rounded-xl bg-primary text-white text-sm font-semibold hover:bg-[#005231] transition-colors flex items-center gap-2 shadow-[0_2px_8px_rgba(0,106,65,0.2)] cursor-pointer disabled:opacity-50"
          >
            {isSubmitting ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                <span>Đang lưu...</span>
              </>
            ) : (
              <>
                <span className="material-symbols-outlined text-[18px]">save</span>
                <span>Lưu Thay Đổi</span>
              </>
            )}
          </button>
        </div>
      </form>
    </div>
  );
};
