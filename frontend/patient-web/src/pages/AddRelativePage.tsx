import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { apiClient } from '../api/client';
import { Relationship, Gender } from '../types/patient';

export const AddRelativePage: React.FC = () => {
  const navigate = useNavigate();

  const [fullName, setFullName] = useState('');
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [gender, setGender] = useState<Gender>('Male');
  const [nationalId, setNationalId] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [address, setAddress] = useState('');
  const [relationship, setRelationship] = useState<Relationship>('Child');

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const validate = (): boolean => {
    const errors: Record<string, string> = {};

    if (!fullName.trim()) errors.fullName = 'Họ và tên người thân không được để trống.';
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
      errors.nationalId = 'Số CCCD/định danh (nếu có) phải gồm đúng 12 chữ số.';
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

    const payload = {
      fullName: fullName.trim(),
      dateOfBirth: dateOfBirth,
      gender: gender,
      nationalId: nationalId.trim() || undefined,
      phoneNumber: phoneNumber.trim(),
      address: address.trim(),
      relationship: relationship,
    };

    try {
      await apiClient.post('/api/patient/profiles', payload);
      navigate('/profiles');
    } catch (err: unknown) {
      const errorObj = err as { response?: { status?: number; data?: { detail?: string; title?: string } } };
      if (errorObj.response?.status === 409) {
        setErrorMsg(errorObj.response.data?.detail || 'Số CCCD đã tồn tại trong hệ thống. Vui lòng liên hệ quầy tiếp tân.');
      } else if (errorObj.response?.data?.detail) {
        setErrorMsg(errorObj.response.data.detail);
      } else {
        setErrorMsg('Không thể thêm hồ sơ người thân. Vui lòng thử lại sau.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="max-w-2xl mx-auto">
      <div className="mb-6 flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-on-surface tracking-tight">Thêm Hồ Sơ Người Thân</h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Đại diện quản lý hồ sơ khám bệnh cho con, cha mẹ, vợ/chồng hoặc người được giám hộ
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
          <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="rel-relationship">
            Mối quan hệ với bạn <span className="text-error">*</span>
          </label>
          <select
            id="rel-relationship"
            value={relationship}
            onChange={(e) => setRelationship(e.target.value as Relationship)}
            className="w-full h-11 px-3.5 rounded-xl border border-outline-variant/75 bg-surface-container-low/55 text-sm font-medium text-on-surface transition-colors focus:bg-white focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
          >
            <option value="Child">Con ruột / Con nuôi (trẻ nhỏ chưa có CCCD có thể để trống)</option>
            <option value="Parent">Bố / Mẹ</option>
            <option value="Spouse">Vợ / Chồng</option>
            <option value="Other">Người thân khác / Được giám hộ hợp pháp</option>
          </select>
        </div>

        <div>
          <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="rel-fullname">
            Họ và tên đầy đủ người thân <span className="text-error">*</span>
          </label>
          <input
            id="rel-fullname"
            type="text"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="Ví dụ: NGUYỄN MINH AN"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
              fieldErrors.fullName ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
            } focus:outline-none focus:ring-2 focus:ring-primary/20`}
          />
          {fieldErrors.fullName && <p className="text-xs text-error mt-1">{fieldErrors.fullName}</p>}
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="rel-dob">
              Ngày sinh <span className="text-error">*</span>
            </label>
            <input
              id="rel-dob"
              type="date"
              value={dateOfBirth}
              onChange={(e) => setDateOfBirth(e.target.value)}
              className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
                fieldErrors.dateOfBirth ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
              } focus:outline-none focus:ring-2 focus:ring-primary/20`}
            />
            {fieldErrors.dateOfBirth && <p className="text-xs text-error mt-1">{fieldErrors.dateOfBirth}</p>}
          </div>

          <div>
            <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="rel-gender">
              Giới tính <span className="text-error">*</span>
            </label>
            <select
              id="rel-gender"
              value={gender}
              onChange={(e) => setGender(e.target.value as Gender)}
              className="w-full h-11 px-3.5 rounded-xl border border-outline-variant/75 bg-surface-container-low/55 focus:bg-white text-sm transition-colors focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
            >
              <option value="Male">Nam</option>
              <option value="Female">Nữ</option>
              <option value="Other">Khác</option>
            </select>
          </div>
        </div>

        <div>
          <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="rel-cccd">
            Số CCCD / Mã định danh cá nhân (để trống nếu là trẻ em chưa có CCCD)
          </label>
          <input
            id="rel-cccd"
            type="text"
            maxLength={12}
            value={nationalId}
            onChange={(e) => setNationalId(e.target.value)}
            placeholder="12 chữ số (không bắt buộc)"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
              fieldErrors.nationalId ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
            } focus:outline-none focus:ring-2 focus:ring-primary/20`}
          />
          {fieldErrors.nationalId && <p className="text-xs text-error mt-1">{fieldErrors.nationalId}</p>}
        </div>

        <div>
          <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="rel-phone">
            Số điện thoại liên lạc <span className="text-error">*</span>
          </label>
          <input
            id="rel-phone"
            type="tel"
            value={phoneNumber}
            onChange={(e) => setPhoneNumber(e.target.value)}
            placeholder="Có thể dùng số điện thoại của người đại diện"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-surface-container-low/55 focus:bg-white transition-colors ${
              fieldErrors.phoneNumber ? 'border-error bg-error-container/40' : 'border-outline-variant/75 focus:border-primary'
            } focus:outline-none focus:ring-2 focus:ring-primary/20`}
          />
          {fieldErrors.phoneNumber && <p className="text-xs text-error mt-1">{fieldErrors.phoneNumber}</p>}
        </div>

        <div>
          <label className="block text-xs font-semibold text-on-surface mb-1.5" htmlFor="rel-address">
            Địa chỉ thường trú / Nơi ở <span className="text-error">*</span>
          </label>
          <input
            id="rel-address"
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

        <div className="p-4 bg-[#fef3c7]/70 rounded-xl text-xs text-[#9a5b00] flex items-start gap-2.5">
          <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">info</span>
          <div>
            <p className="font-semibold mb-0.5">Trạng thái sau khi tạo: Chờ xác minh tại quầy (Pending)</p>
            <p>
              Hồ sơ người thân được tạo có thể đặt lịch khám ngay. Khi đến cơ sở y tế, vui lòng mang giấy khai sinh hoặc giấy tờ chứng minh quan hệ để tiếp tân xác thực danh tính đầy đủ.
            </p>
          </div>
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
                <span>Đang thêm người thân...</span>
              </>
            ) : (
              <>
                <span className="material-symbols-outlined text-[18px]">group_add</span>
                <span>Thêm Hồ Sơ Người Thân</span>
              </>
            )}
          </button>
        </div>
      </form>
    </div>
  );
};
