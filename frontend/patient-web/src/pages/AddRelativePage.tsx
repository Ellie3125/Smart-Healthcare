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
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl sm:text-2xl font-bold text-[#1C2B24]">Thêm Hồ Sơ Người Thân</h1>
          <p className="text-sm text-[#5E6F66] mt-1">
            Đại diện quản lý hồ sơ khám bệnh cho con, cha mẹ, vợ/chồng hoặc người được giám hộ
          </p>
        </div>
        <Link
          to="/profiles"
          className="h-9 px-3 rounded-lg border border-[#DDE7E1] text-[#5E6F66] hover:bg-[#F1F4F2] text-xs font-semibold flex items-center gap-1 transition-colors"
        >
          <span className="material-symbols-outlined text-[18px]">arrow_back</span>
          <span>Quay lại</span>
        </Link>
      </div>

      {errorMsg && (
        <div className="mb-6 p-4 rounded-xl bg-[#FEE2E2] border border-[#FECACA] flex items-start gap-3 text-sm text-[#B91C1C]">
          <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
          <div className="flex-1">{errorMsg}</div>
        </div>
      )}

      <form onSubmit={handleSubmit} className="bg-white rounded-2xl border border-[#DDE7E1] shadow-sm p-6 sm:p-8 space-y-5">
        <div>
          <label className="block text-xs font-semibold text-[#1C2B24] mb-1.5">
            Mối quan hệ với bạn <span className="text-red-500">*</span>
          </label>
          <select
            value={relationship}
            onChange={(e) => setRelationship(e.target.value as Relationship)}
            className="w-full h-11 px-3.5 rounded-xl border border-[#DDE7E1] bg-white text-sm focus:outline-none focus:ring-2 focus:ring-[#006a41]/20 font-medium text-[#1C2B24]"
          >
            <option value="Child">Con ruột / Con nuôi (trẻ nhỏ chưa có CCCD có thể để trống)</option>
            <option value="Parent">Bố / Mẹ</option>
            <option value="Spouse">Vợ / Chồng</option>
            <option value="Other">Người thân khác / Được giám hộ hợp pháp</option>
          </select>
        </div>

        <div>
          <label className="block text-xs font-semibold text-[#1C2B24] mb-1.5" htmlFor="rel-fullname">
            Họ và tên đầy đủ người thân <span className="text-red-500">*</span>
          </label>
          <input
            id="rel-fullname"
            type="text"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="Ví dụ: NGUYỄN MINH AN"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white ${
              fieldErrors.fullName ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
            } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
          />
          {fieldErrors.fullName && <p className="text-xs text-red-500 mt-1">{fieldErrors.fullName}</p>}
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-semibold text-[#1C2B24] mb-1.5" htmlFor="rel-dob">
              Ngày sinh <span className="text-red-500">*</span>
            </label>
            <input
              id="rel-dob"
              type="date"
              value={dateOfBirth}
              onChange={(e) => setDateOfBirth(e.target.value)}
              className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white ${
                fieldErrors.dateOfBirth ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
              } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
            />
            {fieldErrors.dateOfBirth && <p className="text-xs text-red-500 mt-1">{fieldErrors.dateOfBirth}</p>}
          </div>

          <div>
            <label className="block text-xs font-semibold text-[#1C2B24] mb-1.5">
              Giới tính <span className="text-red-500">*</span>
            </label>
            <select
              value={gender}
              onChange={(e) => setGender(e.target.value as Gender)}
              className="w-full h-11 px-3.5 rounded-xl border border-[#DDE7E1] bg-white text-sm focus:outline-none focus:ring-2 focus:ring-[#006a41]/20"
            >
              <option value="Male">Nam</option>
              <option value="Female">Nữ</option>
              <option value="Other">Khác</option>
            </select>
          </div>
        </div>

        <div>
          <label className="block text-xs font-semibold text-[#1C2B24] mb-1.5" htmlFor="rel-cccd">
            Số CCCD / Mã định danh cá nhân (để trống nếu là trẻ em chưa có CCCD)
          </label>
          <input
            id="rel-cccd"
            type="text"
            maxLength={12}
            value={nationalId}
            onChange={(e) => setNationalId(e.target.value)}
            placeholder="12 chữ số (không bắt buộc)"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white ${
              fieldErrors.nationalId ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
            } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
          />
          {fieldErrors.nationalId && <p className="text-xs text-red-500 mt-1">{fieldErrors.nationalId}</p>}
        </div>

        <div>
          <label className="block text-xs font-semibold text-[#1C2B24] mb-1.5" htmlFor="rel-phone">
            Số điện thoại liên lạc <span className="text-red-500">*</span>
          </label>
          <input
            id="rel-phone"
            type="tel"
            value={phoneNumber}
            onChange={(e) => setPhoneNumber(e.target.value)}
            placeholder="Có thể dùng số điện thoại của người đại diện"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white ${
              fieldErrors.phoneNumber ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
            } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
          />
          {fieldErrors.phoneNumber && <p className="text-xs text-red-500 mt-1">{fieldErrors.phoneNumber}</p>}
        </div>

        <div>
          <label className="block text-xs font-semibold text-[#1C2B24] mb-1.5" htmlFor="rel-address">
            Địa chỉ thường trú / Nơi ở <span className="text-red-500">*</span>
          </label>
          <input
            id="rel-address"
            type="text"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/TP"
            className={`w-full h-11 px-3.5 rounded-xl border text-sm bg-white ${
              fieldErrors.address ? 'border-red-400 bg-red-50' : 'border-[#DDE7E1]'
            } focus:outline-none focus:ring-2 focus:ring-[#006a41]/20`}
          />
          {fieldErrors.address && <p className="text-xs text-red-500 mt-1">{fieldErrors.address}</p>}
        </div>

        <div className="p-3.5 bg-[#FFFBEB] rounded-xl border border-[#FDE68A] text-xs text-[#92400E] flex items-start gap-2">
          <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">info</span>
          <div>
            <p className="font-semibold mb-0.5">Trạng thái sau khi tạo: Chờ xác minh tại quầy (Pending)</p>
            <p>Hồ sơ người thân được tạo có thể đặt lịch khám ngay. Khi đến cơ sở y tế, vui lòng mang giấy khai sinh hoặc giấy tờ chứng minh quan hệ để tiếp tân xác thực danh tính đầy đủ.</p>
          </div>
        </div>

        <div className="pt-4 border-t border-[#DDE7E1] flex items-center justify-end gap-3">
          <Link
            to="/profiles"
            className="h-11 px-5 rounded-xl border border-[#DDE7E1] text-[#5E6F66] hover:bg-[#F1F4F2] text-sm font-semibold flex items-center transition-colors"
          >
            Hủy
          </Link>
          <button
            type="submit"
            disabled={isSubmitting}
            className="h-11 px-6 rounded-xl bg-[#006a41] text-white text-sm font-semibold hover:bg-[#028654] transition-colors flex items-center gap-2 shadow-sm cursor-pointer disabled:opacity-50"
          >
            {isSubmitting ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
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
