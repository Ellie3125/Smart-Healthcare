import React, { useState, useEffect, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { useAuth } from '../../hooks/useAuth';
import {
  PatientProfileView,
  CreateRelativeProfileFormInput,
  Relationship,
  VerificationStatus,
} from '../../types/patient';
import {
  mockGetPatientProfiles,
  mockCreateRelativeProfile,
  mockUpdatePatientProfile,
} from '../../api/mockPatientApi';

export const PatientProfilesPage: React.FC = () => {
  const { user, logout } = useAuth();

  // State management (Loading, Empty, Error, Success)
  const [profiles, setProfiles] = useState<PatientProfileView[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [selectedProfileId, setSelectedProfileId] = useState<string | null>(null);

  // Form editing mode
  const [editingPatientId, setEditingPatientId] = useState<string | null>(null);
  const [isSubmittingForm, setIsSubmittingForm] = useState<boolean>(false);

  // Toast notification
  const [toast, setToast] = useState<{ show: boolean; title: string; message: string }>({
    show: false,
    title: '',
    message: '',
  });

  const showToast = (title: string, message: string) => {
    setToast({ show: true, title, message });
    setTimeout(() => {
      setToast((prev) => ({ ...prev, show: false }));
    }, 4000);
  };

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    formState: { errors },
  } = useForm<CreateRelativeProfileFormInput>({
    defaultValues: {
      relationship: 'Child',
      gender: 'Male',
      fullName: '',
      dateOfBirth: '',
      phoneNumber: '',
      nationalId: '',
      address: '',
    },
  });

  const loadProfiles = useCallback(async () => {
    setIsLoading(true);
    setErrorMessage(null);
    try {
      const data = await mockGetPatientProfiles();
      setProfiles(data);
      if (data.length > 0 && !selectedProfileId) {
        // Mặc định chọn hồ sơ Bản thân hoặc hồ sơ hoạt động đầu tiên
        const selfProfile = data.find((p) => p.isSelf) || data[0];
        setSelectedProfileId(selfProfile.patient.id);
      }
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      setErrorMessage(errorObj.message || 'Không thể tải danh sách hồ sơ y tế.');
    } finally {
      setIsLoading(false);
    }
  }, [selectedProfileId]);

  useEffect(() => {
    loadProfiles();
  }, [loadProfiles]);

  const handleSelectProfile = (profile: PatientProfileView) => {
    if (profile.verificationStatus === 'Rejected' || profile.verificationStatus === 'Revoked') {
      showToast('Hồ sơ vô hiệu', 'Hồ sơ này đã bị vô hiệu hoá và không thể sử dụng để đặt khám.');
      return;
    }
    setSelectedProfileId(profile.patient.id);
    showToast('Đã chọn người khám', `Hồ sơ ${profile.patient.fullName} được kích hoạt cho lượt khám tiếp theo.`);
  };

  const startEditProfile = (profile: PatientProfileView, e: React.MouseEvent) => {
    e.stopPropagation();
    if (profile.verificationStatus === 'Rejected' || profile.verificationStatus === 'Revoked') {
      return;
    }
    setEditingPatientId(profile.patient.id);
    setValue('fullName', profile.patient.fullName);
    setValue('relationship', profile.relationship);
    setValue('dateOfBirth', profile.patient.dateOfBirth);
    setValue('gender', profile.patient.gender);
    setValue('phoneNumber', profile.patient.phoneNumber);
    setValue('nationalId', profile.patient.nationalId || '');
    setValue('address', profile.patient.address || '');

    const formEl = document.getElementById('side-form-container');
    if (formEl) {
      formEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  };

  const handleCancelEdit = () => {
    setEditingPatientId(null);
    reset({
      relationship: 'Child',
      gender: 'Male',
      fullName: '',
      dateOfBirth: '',
      phoneNumber: '',
      nationalId: '',
      address: '',
    });
  };

  const onSubmitForm = async (data: CreateRelativeProfileFormInput) => {
    setIsSubmittingForm(true);
    try {
      if (editingPatientId) {
        // Cập nhật
        await mockUpdatePatientProfile(editingPatientId, data);
        showToast('Thành công', `Đã cập nhật hồ sơ của "${data.fullName}".`);
      } else {
        // Tạo mới
        const created = await mockCreateRelativeProfile(data);
        showToast(
          'Đã lưu hồ sơ mới',
          `Hồ sơ "${created.patient.fullName}" đã được thêm vào hệ thống với trạng thái Chờ xác minh (Pending).`
        );
        setSelectedProfileId(created.patient.id);
      }
      handleCancelEdit();
      await loadProfiles();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      showToast('Có lỗi xảy ra', errorObj.message || 'Không thể lưu hồ sơ.');
    } finally {
      setIsSubmittingForm(false);
    }
  };

  const getRelationshipLabel = (rel: Relationship, isSelf: boolean) => {
    if (isSelf) return 'Bản thân';
    switch (rel) {
      case 'Parent':
        return 'Bố / Mẹ';
      case 'Child':
        return 'Con ruột / Con nuôi';
      case 'Spouse':
        return 'Vợ / Chồng';
      case 'Other':
        return 'Người thân / Giám hộ';
      default:
        return rel;
    }
  };

  const calculateAge = (dobString: string): number => {
    if (!dobString) return 0;
    const dob = new Date(dobString);
    const diffMs = Date.now() - dob.getTime();
    const ageDt = new Date(diffMs);
    return Math.abs(ageDt.getUTCFullYear() - 1970);
  };

  const renderVerificationBadge = (status: VerificationStatus) => {
    switch (status) {
      case 'Verified':
        return (
          <span className="inline-flex items-center gap-1 bg-secondary-container text-on-secondary-container text-xs px-2.5 py-0.5 rounded-full font-semibold">
            <span className="material-symbols-outlined text-[13px]">verified</span>
            Đã xác minh
          </span>
        );
      case 'Pending':
        return (
          <span
            className="inline-flex items-center gap-1 bg-amber-50 text-amber-800 border border-amber-200 text-xs px-2.5 py-0.5 rounded-full font-medium"
            title="Hồ sơ chưa được xác minh: bạn có thể đặt lịch, thanh toán, xem vé; chỉ xem được kết quả và lịch sử sức khỏe sau khi tiếp tân đối chiếu giấy tờ gốc tại quầy."
          >
            <span className="material-symbols-outlined text-[13px] text-amber-600">hourglass_top</span>
            Chờ xác minh (Pending)
          </span>
        );
      case 'Rejected':
        return (
          <span className="inline-flex items-center gap-1 bg-red-50 text-red-700 border border-red-200 text-xs px-2.5 py-0.5 rounded-full font-medium">
            <span className="material-symbols-outlined text-[13px] text-red-600">cancel</span>
            Bị từ chối (Vô hiệu)
          </span>
        );
      case 'Revoked':
        return (
          <span className="inline-flex items-center gap-1 bg-gray-100 text-gray-600 border border-gray-300 text-xs px-2.5 py-0.5 rounded-full font-medium">
            <span className="material-symbols-outlined text-[13px] text-gray-500">block</span>
            Đã thu hồi (Vô hiệu)
          </span>
        );
    }
  };

  return (
    <div className="bg-surface font-body-md text-on-surface antialiased min-h-screen flex flex-col justify-between">
      {/* Header */}
      <header className="fixed top-0 w-full z-50 bg-surface/90 backdrop-blur-md shadow-[0_1px_8px_rgba(0,0,0,0.04)] border-b border-[#DDE7E1]">
        <div className="h-16 max-w-[1280px] mx-auto px-margin lg:px-margin-desktop flex items-center justify-between gap-space-md">
          <div className="flex items-center gap-space-sm">
            <div className="w-9 h-9 rounded-full bg-secondary-container flex items-center justify-center relative">
              <span className="material-symbols-outlined text-primary text-[20px]">health_and_safety</span>
              <span className="absolute -top-0.5 -right-0.5 w-3.5 h-3.5 bg-surface-container-lowest rounded-full flex items-center justify-center">
                <span className="material-symbols-outlined text-tertiary text-[11px] font-bold">add</span>
              </span>
            </div>
            <div className="flex flex-col">
              <span className="font-headline-sm text-headline-sm font-semibold tracking-tight text-on-surface leading-none">
                Smart<span className="text-primary ml-1">Healthcare</span>
              </span>
              <span className="font-label-sm text-label-sm text-on-surface-variant">
                Cổng Bệnh Nhân Trực Tuyến
              </span>
            </div>
          </div>

          <div className="flex items-center gap-space-lg">
            <a
              className="hidden sm:flex items-center gap-space-xs text-on-surface-variant hover:text-primary transition-colors"
              href="tel:19006868"
            >
              <span className="material-symbols-outlined text-[18px] text-primary">support_agent</span>
              <div className="flex flex-col text-left">
                <span className="font-label-sm text-label-sm text-on-surface-variant leading-none">Tổng đài 24/7</span>
                <span className="font-label-md text-label-md font-semibold text-primary leading-none mt-0.5">
                  1900 6868
                </span>
              </div>
            </a>

            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-on-surface hidden md:inline">
                {user?.userName}
              </span>
              <button
                onClick={logout}
                className="flex items-center gap-1.5 text-xs font-semibold text-tertiary hover:bg-tertiary/10 px-3 py-1.5 rounded-lg border border-tertiary/20 transition-colors"
                title="Đăng xuất khỏi tài khoản"
              >
                <span className="material-symbols-outlined text-[16px]">logout</span>
                <span>Đăng xuất</span>
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Body */}
      <main className="w-full pt-20 bg-surface flex-1">
        <div className="max-w-[1280px] mx-auto px-margin lg:px-margin-desktop py-space-xl">
          <div className="flex flex-col w-full">
            {/* Stepper Header */}
            <div className="w-full bg-surface-container-lowest rounded-xl shadow-sm p-space-lg mb-space-xl border border-[#DDE7E1]">
              <div className="max-w-2xl mx-auto">
                <div className="relative flex items-center justify-between">
                  <div className="absolute left-1/4 right-1/4 top-5 h-0.5 bg-surface-variant z-0"></div>
                  <div className="absolute left-1/4 w-1/2 top-5 h-0.5 bg-primary z-0"></div>

                  {/* Step 1 */}
                  <div className="relative z-10 flex flex-col items-center text-center">
                    <div className="w-10 h-10 rounded-full bg-primary flex items-center justify-center text-on-primary shadow-sm mb-space-xs">
                      <span className="material-symbols-outlined text-[20px] font-bold">check</span>
                    </div>
                    <span className="font-label-md text-label-md font-semibold text-primary">1. Tài khoản</span>
                    <span className="font-label-sm text-label-sm text-on-surface-variant flex items-center gap-0.5 mt-0.5">
                      <span className="material-symbols-outlined text-[14px] text-primary">verified</span>
                      Đã xác thực
                    </span>
                  </div>

                  {/* Step 2 */}
                  <div className="relative z-10 flex flex-col items-center text-center">
                    <div className="w-10 h-10 rounded-full bg-surface-container-lowest shadow-md flex items-center justify-center text-primary mb-space-xs relative ring-4 ring-secondary-container">
                      <div className="w-3.5 h-3.5 rounded-full bg-primary animate-pulse"></div>
                    </div>
                    <span className="font-label-md text-label-md font-bold text-primary">2. Hồ sơ khám</span>
                    <span className="font-label-sm text-label-sm text-on-surface-variant mt-0.5">
                      Thiết lập người khám bệnh
                    </span>
                  </div>
                </div>
              </div>
            </div>

            {/* Headline Banner */}
            <div className="flex flex-col md:flex-row md:items-end justify-between mb-space-lg">
              <div>
                <div className="inline-flex items-center gap-space-xs text-on-surface-variant font-label-sm text-label-sm mb-space-xs">
                  <span className="material-symbols-outlined text-[16px]">local_hospital</span>
                  <span>Phòng khám Đa khoa Tiêu chuẩn Quốc tế Smart Healthcare</span>
                  <span>/</span>
                  <span className="text-primary font-medium">Hồ sơ khám bệnh</span>
                </div>
                <h1 className="font-headline-lg text-headline-lg font-bold text-on-surface tracking-tight">
                  Quản lý danh sách hồ sơ khám
                </h1>
                <p className="font-body-md text-body-md text-on-surface-variant mt-1">
                  Chọn người cần khám hoặc tạo hồ sơ người thân (con nhỏ, bố mẹ, vợ/chồng) để liên kết y bạ điện tử.
                </p>
              </div>

              <div className="mt-4 md:mt-0 flex items-center gap-space-sm bg-surface-container-low px-space-md py-2 rounded-lg text-on-secondary-container border border-[#DDE7E1]">
                <span className="material-symbols-outlined text-primary text-[20px]">encrypted</span>
                <div className="text-left leading-tight">
                  <span className="font-label-sm text-label-sm font-semibold block text-primary">Mã hóa y tế cấp độ 3</span>
                  <span className="text-[11px] text-on-surface-variant">Bảo vệ thông tin cá nhân &amp; Y bạ</span>
                </div>
              </div>
            </div>

            {/* Mandatory Business Rule Alert Banner */}
            <div className="mb-6 p-4 rounded-xl bg-amber-50 border border-amber-200 text-amber-900 flex items-start gap-3 shadow-sm">
              <span className="material-symbols-outlined text-amber-700 text-[24px] shrink-0 mt-0.5">info</span>
              <div className="text-sm leading-relaxed">
                <strong className="font-semibold block mb-0.5">Quy định về xác minh giấy tờ tùy thân:</strong>
                Hệ thống <strong>không yêu cầu và không hỗ trợ tải ảnh CCCD/giấy tờ</strong> lên mạng. Với hồ sơ ở trạng thái <em>Chờ xác minh (Pending)</em>, bạn hoàn toàn có thể đặt lịch hẹn, thanh toán và lấy vé khám; kết quả xét nghiệm và lịch sử sức khỏe chỉ hiển thị sau khi tiếp tân đối chiếu giấy tờ gốc trực tiếp tại quầy tiếp đón.
              </div>
            </div>

            {/* 4 States handling: Loading, Error, Empty, Success */}
            {isLoading && (
              <div className="p-12 text-center bg-white rounded-xl border border-[#DDE7E1] my-4 shadow-sm">
                <div className="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin mb-3"></div>
                <p className="text-sm font-medium text-on-surface-variant">Đang tải danh sách hồ sơ y tế...</p>
              </div>
            )}

            {errorMessage && !isLoading && (
              <div className="p-6 bg-[#FDE8E8] text-[#D32F2F] rounded-xl border border-[#F8B4B4] my-4 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <span className="material-symbols-outlined text-[24px]">error</span>
                  <span className="text-sm font-medium">{errorMessage}</span>
                </div>
                <button
                  onClick={loadProfiles}
                  className="px-3 py-1.5 bg-[#D32F2F] text-white rounded-lg text-xs font-semibold hover:opacity-90"
                >
                  Thử lại
                </button>
              </div>
            )}

            {!isLoading && !errorMessage && profiles.length === 0 && (
              <div className="p-12 text-center bg-white rounded-xl border border-[#DDE7E1] my-4">
                <span className="material-symbols-outlined text-[48px] text-outline mb-2">folder_off</span>
                <h3 className="text-base font-semibold text-on-surface">Chưa có hồ sơ nào</h3>
                <p className="text-xs text-on-surface-variant mt-1">Vui lòng điền biểu mẫu bên phải để tạo hồ sơ đầu tiên.</p>
              </div>
            )}

            {!isLoading && !errorMessage && profiles.length > 0 && (
              <div className="grid grid-cols-1 lg:grid-cols-12 gap-space-xl items-start">
                {/* LEFT COLUMN: Profile Cards (7 cols) */}
                <div className="lg:col-span-7 flex flex-col gap-space-md">
                  <div className="flex items-center justify-between pb-1">
                    <div className="flex items-center gap-space-xs">
                      <span className="font-label-md text-label-md font-semibold text-on-surface">
                        Danh sách hồ sơ thành viên
                      </span>
                      <span className="bg-surface-container px-2 py-0.5 rounded-full font-label-sm text-label-sm text-primary font-semibold">
                        {profiles.length} hồ sơ
                      </span>
                    </div>
                    <span className="font-label-sm text-label-sm text-on-surface-variant">
                      Nhấp để kích hoạt hồ sơ đặt khám
                    </span>
                  </div>

                  {/* Cards List */}
                  {profiles.map((item) => {
                    const isSelected = selectedProfileId === item.patient.id;
                    const isInvalidated =
                      item.verificationStatus === 'Rejected' || item.verificationStatus === 'Revoked';
                    const age = calculateAge(item.patient.dateOfBirth);

                    return (
                      <div
                        key={item.patient.id}
                        onClick={() => handleSelectProfile(item)}
                        className={`relative p-space-lg rounded-xl shadow-sm transition-all duration-200 border ${
                          isInvalidated
                            ? 'opacity-60 bg-gray-50 border-gray-200 cursor-not-allowed'
                            : isSelected
                            ? 'bg-surface-container-low border-primary/40 shadow-md cursor-pointer'
                            : 'bg-surface-container-lowest border-[#DDE7E1] hover:border-primary/30 hover:shadow cursor-pointer'
                        }`}
                      >
                        {/* Selected accent strip */}
                        {isSelected && !isInvalidated && (
                          <div className="absolute left-0 top-0 bottom-0 w-1.5 bg-primary rounded-l-xl"></div>
                        )}

                        <div className="flex items-start justify-between">
                          <div className="flex items-start gap-space-md">
                            {/* Avatar */}
                            <div className="relative w-14 h-14 rounded-full bg-surface-container-lowest border border-[#DDE7E1] flex items-center justify-center shrink-0">
                              <span className="material-symbols-outlined text-primary text-[32px]">
                                {item.isSelf
                                  ? 'account_circle'
                                  : item.relationship === 'Child'
                                  ? 'child_care'
                                  : item.relationship === 'Parent'
                                  ? 'elderly'
                                  : 'person'}
                              </span>
                            </div>

                            {/* Details */}
                            <div className="flex flex-col">
                              <div className="flex items-center gap-space-sm flex-wrap">
                                <span className="font-headline-sm text-headline-sm font-bold text-on-surface">
                                  {item.patient.fullName}
                                </span>

                                {/* Relationship Chip */}
                                <span className="inline-flex items-center gap-1 bg-surface-container text-on-surface-variant font-label-sm text-label-sm px-2.5 py-0.5 rounded-full font-medium">
                                  <span className="material-symbols-outlined text-[13px]">family_restroom</span>
                                  {getRelationshipLabel(item.relationship, item.isSelf)}
                                </span>

                                {/* Verification Status Chip */}
                                {renderVerificationBadge(item.verificationStatus)}
                              </div>

                              {/* Metadata Grid */}
                              <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-space-lg gap-y-1.5 mt-space-sm font-body-md text-body-md text-on-surface-variant">
                                <div className="flex items-center gap-1.5">
                                  <span className="material-symbols-outlined text-[16px] text-outline">cake</span>
                                  <span>
                                    Ngày sinh: <strong className="text-on-surface font-semibold">{item.patient.dateOfBirth}</strong>{' '}
                                    ({age}t)
                                  </span>
                                </div>
                                <div className="flex items-center gap-1.5">
                                  <span className="material-symbols-outlined text-[16px] text-outline">wc</span>
                                  <span>
                                    Giới tính: <strong className="text-on-surface font-semibold">
                                      {item.patient.gender === 'Male' ? 'Nam' : item.patient.gender === 'Female' ? 'Nữ' : 'Khác'}
                                    </strong>
                                  </span>
                                </div>
                                <div className="flex items-center gap-1.5">
                                  <span className="material-symbols-outlined text-[16px] text-outline">call</span>
                                  <span>
                                    SĐT: <strong className="text-on-surface font-semibold">{item.patient.phoneNumber}</strong>
                                  </span>
                                </div>
                                <div className="flex items-center gap-1.5">
                                  <span className="material-symbols-outlined text-[16px] text-outline">badge</span>
                                  <span>
                                    CCCD:{' '}
                                    {item.patient.nationalId ? (
                                      <strong className="text-on-surface font-medium font-data-tabular">
                                        {item.patient.nationalId}
                                      </strong>
                                    ) : (
                                      <em className="text-xs text-outline">Chưa cập nhật (Trẻ em/Chưa có)</em>
                                    )}
                                  </span>
                                </div>
                                <div className="flex items-center gap-1.5 sm:col-span-2">
                                  <span className="material-symbols-outlined text-[16px] text-outline">home</span>
                                  <span className="text-xs">{item.patient.address}</span>
                                </div>
                              </div>
                            </div>
                          </div>

                          {/* Right Controls */}
                          <div className="flex flex-col items-end gap-space-sm shrink-0">
                            {!isInvalidated && (
                              <div
                                className={`w-7 h-7 rounded-full flex items-center justify-center transition-colors ${
                                  isSelected
                                    ? 'bg-primary text-on-primary shadow-sm'
                                    : 'bg-surface-container text-outline'
                                }`}
                              >
                                <span className="material-symbols-outlined text-[18px]">
                                  {isSelected ? 'check' : 'radio_button_unchecked'}
                                </span>
                              </div>
                            )}

                            {/* Sửa: Ẩn nút thao tác nếu hồ sơ bị Rejected/Revoked */}
                            {!isInvalidated && (
                              <button
                                className="mt-2 inline-flex items-center gap-1 text-primary hover:text-on-secondary-container font-label-sm text-label-sm font-semibold transition-colors py-1 px-2 rounded-lg hover:bg-surface-container"
                                onClick={(e) => startEditProfile(item, e)}
                                type="button"
                              >
                                <span className="material-symbols-outlined text-[16px]">edit</span>
                                <span>Sửa</span>
                              </button>
                            )}
                          </div>
                        </div>

                        {/* Status detail callout for Pending */}
                        {item.verificationStatus === 'Pending' && (
                          <div className="mt-3 pt-2 border-t border-[#DDE7E1] text-xs text-amber-800 flex items-center gap-1.5">
                            <span className="material-symbols-outlined text-[15px]">info</span>
                            <span>Hồ sơ chưa được xác minh: bạn có thể đặt lịch, thanh toán, xem vé; chỉ xem được kết quả và lịch sử sức khỏe sau khi tiếp tân đối chiếu giấy tờ gốc tại quầy.</span>
                          </div>
                        )}
                      </div>
                    );
                  })}

                  {/* Add Profile Card Button */}
                  <button
                    className="w-full bg-surface-container-lowest/80 hover:bg-surface-container-low transition-all duration-200 p-space-lg rounded-xl shadow-sm border border-dashed border-[#006a41]/30 text-center flex flex-col sm:flex-row items-center justify-center gap-space-md group"
                    onClick={() => {
                      handleCancelEdit();
                      const formEl = document.getElementById('side-form-container');
                      if (formEl) {
                        formEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
                      }
                    }}
                    type="button"
                  >
                    <div className="w-12 h-12 rounded-full bg-secondary-container text-primary flex items-center justify-center group-hover:scale-110 transition-transform">
                      <span className="material-symbols-outlined text-[24px]">person_add</span>
                    </div>
                    <div className="flex flex-col text-left">
                      <span className="font-headline-sm text-headline-sm font-semibold text-primary group-hover:text-on-secondary-container">
                        + Thêm hồ sơ người thân mới
                      </span>
                      <span className="font-body-md text-body-md text-on-surface-variant">
                        Thêm con nhỏ, cha mẹ, vợ/chồng để theo dõi sức khỏe tập trung trên một tài khoản.
                      </span>
                    </div>
                  </button>
                </div>

                {/* RIGHT COLUMN: Form Panel (5 cols) */}
                <div
                  className="lg:col-span-5 bg-surface-container-lowest rounded-xl shadow-md p-space-lg border border-[#DDE7E1] sticky top-24"
                  id="side-form-container"
                >
                  <div className="flex items-center justify-between mb-2">
                    <h2 className="font-headline-sm text-headline-sm font-bold text-on-surface">
                      {editingPatientId ? 'Chỉnh sửa hồ sơ' : 'Thêm hồ sơ người thân mới'}
                    </h2>
                    <button
                      className="text-on-surface-variant hover:text-on-surface p-1 rounded hover:bg-surface-container"
                      onClick={handleCancelEdit}
                      title="Làm mới biểu mẫu"
                      type="button"
                    >
                      <span className="material-symbols-outlined text-[18px]">restart_alt</span>
                    </button>
                  </div>
                  <p className="font-body-md text-body-md text-on-surface-variant mb-space-lg">
                    {editingPatientId
                      ? 'Cập nhật lại thông tin cá nhân của người bệnh.'
                      : 'Thông tin sẽ được liên kết trực tiếp vào mã định danh y tế gia đình của bạn.'}
                  </p>

                  <form className="space-y-space-md" onSubmit={handleSubmit(onSubmitForm)} noValidate>
                    {/* FullName */}
                    <div className="flex flex-col gap-1.5">
                      <label className="font-label-md text-label-md font-semibold text-on-surface flex items-center justify-between" htmlFor="relative-name">
                        <span>Họ và tên người khám <span className="text-tertiary">*</span></span>
                        <span className="text-[11px] font-normal text-on-surface-variant">Theo CCCD / Khai sinh</span>
                      </label>
                      <div className="relative">
                        <span className="absolute left-3 top-1/2 -translate-y-1/2 material-symbols-outlined text-outline text-[18px]">
                          badge
                        </span>
                        <input
                          {...register('fullName', { required: 'Vui lòng nhập họ và tên' })}
                          className="w-full h-11 pl-10 pr-3 rounded-lg bg-surface-container-low text-on-surface text-sm border border-[#DDE7E1] focus:bg-white focus:outline-none focus:ring-2 focus:ring-primary"
                          id="relative-name"
                          placeholder="VD: Trần Văn Bình"
                          type="text"
                        />
                      </div>
                      {errors.fullName && <p className="text-xs text-tertiary">{errors.fullName.message}</p>}
                    </div>

                    {/* Relationship */}
                    <div className="flex flex-col gap-1.5">
                      <label className="font-label-md text-label-md font-semibold text-on-surface" htmlFor="relative-relation">
                        Mối quan hệ với chủ tài khoản <span className="text-tertiary">*</span>
                      </label>
                      <div className="relative">
                        <span className="absolute left-3 top-1/2 -translate-y-1/2 material-symbols-outlined text-outline text-[18px]">
                          groups
                        </span>
                        <select
                          {...register('relationship', { required: 'Vui lòng chọn mối quan hệ' })}
                          className="w-full h-11 pl-10 pr-8 rounded-lg bg-surface-container-low text-on-surface text-sm border border-[#DDE7E1] focus:bg-white focus:outline-none focus:ring-2 focus:ring-primary appearance-none cursor-pointer"
                          id="relative-relation"
                        >
                          <option value="Child">Con ruột / Con nuôi</option>
                          <option value="Parent">Bố / Mẹ</option>
                          <option value="Spouse">Vợ / Chồng</option>
                          <option value="Other">Người được giám hộ khác</option>
                        </select>
                        <span className="absolute right-3 top-1/2 -translate-y-1/2 material-symbols-outlined text-outline pointer-events-none text-[18px]">
                          expand_more
                        </span>
                      </div>
                    </div>

                    {/* DOB & Gender */}
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-space-md">
                      <div className="flex flex-col gap-1.5">
                        <label className="font-label-md text-label-md font-semibold text-on-surface" htmlFor="relative-dob">
                          Ngày sinh <span className="text-tertiary">*</span>
                        </label>
                        <input
                          {...register('dateOfBirth', { required: 'Vui lòng chọn ngày sinh' })}
                          className="w-full h-11 px-3 rounded-lg bg-surface-container-low text-on-surface text-sm border border-[#DDE7E1] focus:bg-white focus:outline-none focus:ring-2 focus:ring-primary"
                          id="relative-dob"
                          type="date"
                        />
                        {errors.dateOfBirth && <p className="text-xs text-tertiary">{errors.dateOfBirth.message}</p>}
                      </div>

                      <div className="flex flex-col gap-1.5">
                        <label className="font-label-md text-label-md font-semibold text-on-surface">
                          Giới tính <span className="text-tertiary">*</span>
                        </label>
                        <div className="grid grid-cols-3 bg-surface-container-low p-1 rounded-lg h-11 border border-[#DDE7E1]">
                          {(['Male', 'Female', 'Other'] as const).map((g) => (
                            <label
                              key={g}
                              className="cursor-pointer flex items-center justify-center rounded text-center text-xs font-medium text-on-surface-variant transition-colors has-[:checked]:bg-white has-[:checked]:shadow-sm has-[:checked]:text-primary has-[:checked]:font-semibold"
                            >
                              <input {...register('gender')} className="hidden" type="radio" value={g} />
                              <span>{g === 'Male' ? 'Nam' : g === 'Female' ? 'Nữ' : 'Khác'}</span>
                            </label>
                          ))}
                        </div>
                      </div>
                    </div>

                    {/* Phone */}
                    <div className="flex flex-col gap-1.5">
                      <label className="font-label-md text-label-md font-semibold text-on-surface" htmlFor="relative-phone">
                        Số điện thoại liên hệ <span className="text-tertiary">*</span>
                      </label>
                      <input
                        {...register('phoneNumber', { required: 'Vui lòng nhập số điện thoại' })}
                        className="w-full h-11 px-3 rounded-lg bg-surface-container-low text-on-surface text-sm border border-[#DDE7E1] focus:bg-white focus:outline-none focus:ring-2 focus:ring-primary"
                        id="relative-phone"
                        placeholder="VD: 0987 654 321"
                        type="tel"
                      />
                      {errors.phoneNumber && <p className="text-xs text-tertiary">{errors.phoneNumber.message}</p>}
                    </div>

                    {/* CCCD (Optional, NO FILE UPLOAD!) */}
                    <div className="flex flex-col gap-1.5">
                      <label className="font-label-md text-label-md font-semibold text-on-surface flex items-center justify-between" htmlFor="relative-cccd">
                        <span>Số thẻ Căn cước công dân (CCCD)</span>
                        <span className="text-[11px] font-normal text-on-surface-variant">(Không bắt buộc)</span>
                      </label>
                      <input
                        {...register('nationalId')}
                        className="w-full h-11 px-3 rounded-lg bg-surface-container-low text-on-surface text-sm border border-[#DDE7E1] focus:bg-white focus:outline-none focus:ring-2 focus:ring-primary font-data-tabular"
                        id="relative-cccd"
                        placeholder="12 số trên CCCD gắn chip (trẻ nhỏ để trống)"
                        type="text"
                      />
                      <span className="text-[11px] text-on-surface-variant">
                        Trẻ em chưa có CCCD được để trống. Tuyệt đối không cần tải ảnh giấy tờ.
                      </span>
                    </div>

                    {/* Address */}
                    <div className="flex flex-col gap-1.5">
                      <label className="font-label-md text-label-md font-semibold text-on-surface" htmlFor="relative-address">
                        Địa chỉ thường trú
                      </label>
                      <input
                        {...register('address')}
                        className="w-full h-11 px-3 rounded-lg bg-surface-container-low text-on-surface text-sm border border-[#DDE7E1] focus:bg-white focus:outline-none focus:ring-2 focus:ring-primary"
                        id="relative-address"
                        placeholder="Số nhà, đường, phường/xã..."
                        type="text"
                      />
                    </div>

                    {/* Submit Actions */}
                    <div className="pt-2 flex flex-col gap-2">
                      <button
                        disabled={isSubmittingForm}
                        className="w-full h-11 rounded-lg bg-[#006a41] text-white hover:bg-[#005231] font-semibold text-sm flex items-center justify-center gap-2 transition-colors shadow-sm disabled:opacity-60"
                        type="submit"
                      >
                        <span className="material-symbols-outlined text-[18px]">save</span>
                        <span>{isSubmittingForm ? 'Đang lưu...' : editingPatientId ? 'Lưu cập nhật hồ sơ' : 'Lưu thông tin hồ sơ mới'}</span>
                      </button>

                      {editingPatientId && (
                        <button
                          type="button"
                          onClick={handleCancelEdit}
                          className="w-full h-10 rounded-lg border border-[#DDE7E1] text-sm font-medium hover:bg-surface-container text-on-surface-variant"
                        >
                          Hủy chỉnh sửa
                        </button>
                      )}

                      <p className="text-[11px] text-on-surface-variant text-center leading-normal mt-1">
                        Hồ sơ mới tạo sẽ ở trạng thái Chờ xác minh (Pending) cho đến khi đối chiếu giấy tờ gốc tại bệnh viện.
                      </p>
                    </div>
                  </form>
                </div>
              </div>
            )}

            {/* Action & Footer Banner */}
            <div className="mt-space-2xl pt-space-lg bg-surface-container-lowest rounded-xl shadow-sm p-space-lg border border-[#DDE7E1]">
              <div className="flex flex-col sm:flex-row items-center justify-between gap-space-lg">
                <button
                  type="button"
                  onClick={() => showToast('Thông báo', 'Bạn đang ở trang quản lý hồ sơ khám của tài khoản.')}
                  className="w-full sm:w-auto h-12 px-space-xl rounded-lg bg-surface-container-low hover:bg-surface-variant text-on-surface font-label-md text-label-md font-semibold flex items-center justify-center gap-space-xs transition-colors"
                >
                  <span className="material-symbols-outlined text-[20px]">refresh</span>
                  <span>Làm mới danh sách</span>
                </button>

                <div className="flex items-center gap-space-xs text-on-surface-variant font-label-sm text-label-sm text-center sm:text-left">
                  <span className="material-symbols-outlined text-primary text-[18px]">verified_user</span>
                  <span>Mỗi hồ sơ chỉ xem được bởi tài khoản được ủy quyền &amp; mã hóa lưu trữ an toàn.</span>
                </div>

                <div className="w-full sm:w-auto min-w-[220px] h-12 px-space-xl rounded-lg bg-primary/10 text-primary border border-primary/20 font-label-md text-label-md font-bold flex items-center justify-center gap-space-sm">
                  <span>Hồ sơ đã chọn sẵn sàng</span>
                  <span className="material-symbols-outlined text-[20px]">check_circle</span>
                </div>
              </div>
            </div>

            {/* Toast Notification */}
            {toast.show && (
              <div className="fixed bottom-6 right-6 max-w-md bg-white text-on-surface rounded-xl shadow-xl p-space-md z-50 border border-[#DDE7E1] animate-bounce-short">
                <div className="flex items-start gap-space-sm">
                  <div className="w-8 h-8 rounded-full bg-secondary-container text-primary flex items-center justify-center shrink-0">
                    <span className="material-symbols-outlined text-[18px]">notifications</span>
                  </div>
                  <div className="flex flex-col">
                    <span className="font-label-md text-label-md font-bold text-on-surface">{toast.title}</span>
                    <span className="font-body-md text-body-md text-on-surface-variant mt-0.5 text-xs">
                      {toast.message}
                    </span>
                  </div>
                  <button
                    className="text-outline hover:text-on-surface ml-auto"
                    onClick={() => setToast((prev) => ({ ...prev, show: false }))}
                  >
                    <span className="material-symbols-outlined text-[18px]">close</span>
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </main>

      {/* Footer */}
      <footer className="w-full bg-surface-container-low py-space-xl mt-auto shadow-[0_1px_8px_rgba(0,0,0,0.02)] border-t border-[#DDE7E1]">
        <div className="max-w-[1280px] mx-auto px-margin lg:px-margin-desktop flex flex-col md:flex-row items-center justify-between gap-space-md text-on-surface-variant">
          <div className="flex items-center gap-space-sm">
            <div className="w-6 h-6 rounded-full bg-primary flex items-center justify-center">
              <span className="material-symbols-outlined text-on-primary text-[14px]">verified</span>
            </div>
            <p className="font-label-sm text-label-sm">
              © 2026 Smart Healthcare Vietnam. Tiêu chuẩn Bộ Y Tế. Bảo mật dữ liệu y tế cá nhân.
            </p>
          </div>
          <div className="flex items-center gap-space-lg font-label-sm text-label-sm">
            <span>Chính sách bảo mật</span>
            <span>Điều khoản sử dụng</span>
            <span>Hướng dẫn tiếp nhận</span>
          </div>
        </div>
      </footer>
    </div>
  );
};
