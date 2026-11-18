import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiClient } from '../api/client';
import { PatientProfileDto, VerificationStatus, Relationship } from '../types/patient';

export const ProfilesPage: React.FC = () => {
  const [profiles, setProfiles] = useState<PatientProfileDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const fetchProfiles = async () => {
    setIsLoading(true);
    setErrorMsg(null);
    try {
      const res = await apiClient.get<PatientProfileDto[]>('/api/patient/profiles');
      setProfiles(res.data);
    } catch {
      setErrorMsg('Không thể tải danh sách hồ sơ. Vui lòng thử lại sau.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchProfiles();
  }, []);

  const getRelationshipName = (rel: Relationship) => {
    switch (rel) {
      case 'Self':
        return 'Bản thân (Chủ tài khoản)';
      case 'Child':
        return 'Con ruột / Con nuôi';
      case 'Parent':
        return 'Bố mẹ ruột / Bố mẹ vợ/chồng';
      case 'Spouse':
        return 'Vợ / Chồng';
      case 'Other':
        return 'Người thân khác / Được giám hộ';
      default:
        return rel;
    }
  };

  const getStatusBadge = (status: VerificationStatus) => {
    switch (status) {
      case 'Verified':
        return (
          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-[#D1FAE5] text-[#047857] border border-[#A7F3D0]">
            <span className="material-symbols-outlined text-[16px]">verified</span>
            Đã xác thực
          </span>
        );
      case 'Pending':
        return (
          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-[#FEF3C7] text-[#B45309] border border-[#FDE68A]">
            <span className="material-symbols-outlined text-[16px]">schedule</span>
            Chờ xác minh tại quầy
          </span>
        );
      case 'Rejected':
        return (
          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-[#FEE2E2] text-[#B91C1C] border border-[#FECACA]">
            <span className="material-symbols-outlined text-[16px]">cancel</span>
            Từ chối xác thực
          </span>
        );
      case 'Revoked':
        return (
          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-[#F3F4F6] text-[#4B5563] border border-[#E5E7EB]">
            <span className="material-symbols-outlined text-[16px]">block</span>
            Đã thu hồi
          </span>
        );
    }
  };

  const renderStatusExplanation = (status: VerificationStatus, rejectionReason?: string | null) => {
    switch (status) {
      case 'Pending':
        return (
          <div className="mt-3 p-3 bg-[#FFFBEB] rounded-lg border border-[#FDE68A] text-xs text-[#92400E] flex items-start gap-2">
            <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">info</span>
            <div>
              <p className="font-semibold mb-0.5">Quyền hạn hiện tại: Đặt lịch, thanh toán và xem vé khám.</p>
              <p>Để xem kết quả xét nghiệm và lịch sử sức khỏe, vui lòng mang giấy tờ gốc (CCCD/Giấy khai sinh) đến quầy tiếp tân để đối chiếu xác nhận.</p>
            </div>
          </div>
        );
      case 'Verified':
        return (
          <div className="mt-3 p-3 bg-[#ECFDF5] rounded-lg border border-[#A7F3D0] text-xs text-[#065F46] flex items-start gap-2">
            <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">check_circle</span>
            <p>Hồ sơ đã được tiếp tân đối chiếu thẻ thật. Bạn có đầy đủ quyền xem lịch sử khám và kết quả cận lâm sàng.</p>
          </div>
        );
      case 'Rejected':
        return (
          <div className="mt-3 p-3 bg-[#FEF2F2] rounded-lg border border-[#FECACA] text-xs text-[#991B1B] flex items-start gap-2">
            <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">warning</span>
            <div>
              <p className="font-semibold mb-0.5">Hồ sơ không thể truy cập do tiếp tân từ chối xác nhận.</p>
              {rejectionReason && <p className="mb-1">Lý do: <em>{rejectionReason}</em></p>}
              <p>Vui lòng liên hệ quầy tiếp tân cơ sở y tế để được hỗ trợ hoặc cập nhật lại thông tin.</p>
            </div>
          </div>
        );
      case 'Revoked':
        return (
          <div className="mt-3 p-3 bg-[#F9FAFB] rounded-lg border border-[#E5E7EB] text-xs text-[#4B5563] flex items-start gap-2">
            <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">block</span>
            <p>Quyền đại diện cho hồ sơ này đã bị thu hồi bởi quản trị viên hoặc nhân viên y tế. Bạn không còn quyền truy cập dữ liệu hồ sơ này.</p>
          </div>
        );
    }
  };

  const handleUnlink = async (patientId: string, fullName: string) => {
    if (!window.confirm(`Bạn có chắc chắn muốn hủy liên kết với hồ sơ "${fullName}" khỏi tài khoản này?`)) {
      return;
    }
    try {
      await apiClient.post(`/api/patient/profiles/${patientId}/unlink`);
      fetchProfiles();
    } catch {
      alert('Không thể hủy liên kết hồ sơ. Vui lòng thử lại sau.');
    }
  };

  const handleResubmit = async (patientId: string, fullName: string) => {
    if (!window.confirm(`Gửi lại yêu cầu xác minh hồ sơ "${fullName}"?`)) {
      return;
    }
    try {
      await apiClient.post(`/api/patient/profiles/${patientId}/resubmit`);
      fetchProfiles();
    } catch {
      alert('Không thể gửi lại yêu cầu xác minh. Vui lòng thử lại sau.');
    }
  };

  return (
    <div className="space-y-6">
      {/* Header Bar */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface-container-lowest p-6 rounded-2xl border border-surface-container-highest/70 shadow-[0_1px_10px_rgba(15,31,24,0.04)]">
        <div>
          <span className="inline-flex items-center gap-1 rounded-full bg-secondary-container/75 px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-wide text-[#005231] mb-2">
            Hồ sơ y bạ gia đình
          </span>
          <h1 className="text-2xl font-bold text-on-surface tracking-tight">Danh Sách Hồ Sơ Của Tôi</h1>
        </div>
        <div className="flex flex-wrap items-center gap-2.5 w-full sm:w-auto">
          <Link
            to="/booking"
            className="flex-1 sm:flex-none h-10 px-4 rounded-xl border border-surface-container-highest bg-surface-container-low text-on-surface font-semibold text-sm hover:bg-surface-container transition-colors flex items-center justify-center gap-2"
          >
            <span className="material-symbols-outlined text-[19px] text-primary">calendar_add_on</span>
            <span>Đặt lịch khám</span>
          </Link>
          <Link
            to="/profiles/add-relative"
            className="flex-1 sm:flex-none h-10 px-4 rounded-xl bg-primary text-white font-semibold text-sm hover:bg-[#005231] transition-colors flex items-center justify-center gap-2 shadow-[0_2px_8px_rgba(0,106,65,0.2)]"
          >
            <span className="material-symbols-outlined text-[20px]">person_add</span>
            <span>Thêm Người Thân</span>
          </Link>
        </div>
      </div>

      {errorMsg && (
        <div className="p-4 rounded-xl bg-error-container border border-[#ffb4ab]/75 flex items-start gap-3 text-sm text-[#93000a]">
          <span className="material-symbols-outlined text-[20px] shrink-0 mt-0.5">error</span>
          <div className="flex-1">{errorMsg}</div>
        </div>
      )}

      {/* Profile Cards */}
      {isLoading ? (
        <div className="py-16 text-center text-on-surface-variant">
          <div className="w-8 h-8 border-[3px] border-primary border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm">Đang tải danh sách hồ sơ y tế...</p>
        </div>
      ) : profiles.length === 0 ? (
        <div className="p-10 text-center bg-surface-container-lowest rounded-2xl border-2 border-dashed border-outline-variant/80 shadow-sm">
          <div className="w-16 h-16 rounded-2xl bg-surface-container-low flex items-center justify-center text-primary mx-auto mb-4">
            <span className="material-symbols-outlined text-[36px]">contact_page</span>
          </div>
          <h2 className="text-lg font-bold text-on-surface mb-6">Chưa có hồ sơ y tế nào</h2>
          <div className="flex justify-center gap-3">
            <Link
              to="/profiles/new"
              className="h-10 px-4 rounded-xl bg-primary text-white font-semibold text-sm hover:bg-[#005231] transition-colors flex items-center gap-2 shadow-sm"
            >
              <span className="material-symbols-outlined text-[18px]">add</span>
              <span>Tạo Hồ Sơ Bản Thân</span>
            </Link>
            <Link
              to="/profiles/add-relative"
              className="h-10 px-4 rounded-xl border border-surface-container-highest bg-white text-on-surface font-semibold text-sm hover:bg-surface-container-low transition-colors flex items-center gap-2"
            >
              <span className="material-symbols-outlined text-[18px]">group_add</span>
              <span>Thêm Người Thân</span>
            </Link>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          {profiles.map((p) => {
            const isAccessible = p.verificationStatus === 'Pending' || p.verificationStatus === 'Verified';
            return (
              <div
                key={p.patientId}
                className="bg-surface-container-lowest rounded-2xl border border-surface-container-highest/70 shadow-[0_1px_10px_rgba(15,31,24,0.04)] p-5 sm:p-6 flex flex-col justify-between hover:border-primary/40 hover:shadow-md transition-all"
              >
                <div>
                  {/* Top: Name & Status */}
                  <div className="flex items-start justify-between gap-3 mb-3">
                    <div className="flex items-center gap-3">
                      <div className="w-12 h-12 rounded-xl bg-secondary-container/70 flex items-center justify-center text-[#005231] shrink-0 font-bold text-lg">
                        {p.fullName.charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <h2 className="text-lg font-bold text-on-surface leading-snug">{p.fullName}</h2>
                        <span className="inline-block mt-0.5 text-xs font-semibold text-primary bg-surface-container-low px-2.5 py-0.5 rounded-full">
                          {getRelationshipName(p.relationship)}
                        </span>
                      </div>
                    </div>
                    {getStatusBadge(p.verificationStatus)}
                  </div>

                  {/* Information Details */}
                  <div className="space-y-2 text-xs sm:text-sm text-on-surface-variant bg-surface-container-low/65 p-4 rounded-xl mt-3">
                    <div className="flex justify-between">
                      <span>Ngày sinh:</span>
                      <span className="font-semibold text-on-surface tabular-nums">{p.dateOfBirth}</span>
                    </div>
                    <div className="flex justify-between">
                      <span>Giới tính:</span>
                      <span className="font-semibold text-on-surface">
                        {p.gender === 'Male' ? 'Nam' : p.gender === 'Female' ? 'Nữ' : 'Khác'}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span>Số CCCD / Mã ĐD:</span>
                      <span className="font-semibold text-on-surface tabular-nums">
                        {p.nationalId ? p.nationalId : <em className="text-outline font-normal">Chưa khai báo</em>}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span>Số điện thoại:</span>
                      <span className="font-semibold text-on-surface tabular-nums">{p.phoneNumber}</span>
                    </div>
                    <div className="flex justify-between gap-3">
                      <span>Địa chỉ:</span>
                      <span className="font-semibold text-on-surface truncate max-w-[220px]" title={p.address}>
                        {p.address}
                      </span>
                    </div>
                  </div>

                  {/* Status explanation alert */}
                  {renderStatusExplanation(p.verificationStatus, p.rejectionReason)}
                </div>

                {/* Bottom Actions */}
                <div className="mt-5 pt-4 border-t border-surface-container-low flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    {isAccessible && (
                      <Link
                        to={`/profiles/${p.patientId}/edit`}
                        className="h-9 px-3.5 rounded-xl bg-surface-container-low text-on-surface hover:bg-surface-container text-xs font-semibold flex items-center gap-1.5 transition-colors"
                      >
                        <span className="material-symbols-outlined text-[16px]">edit</span>
                        <span>Sửa</span>
                      </Link>
                    )}
                    {p.verificationStatus === 'Rejected' && (
                      <button
                        onClick={() => handleResubmit(p.patientId, p.fullName)}
                        className="h-9 px-3.5 rounded-xl bg-primary text-white hover:bg-[#005231] text-xs font-semibold flex items-center gap-1.5 transition-colors shadow-sm"
                      >
                        <span className="material-symbols-outlined text-[16px]">refresh</span>
                        <span>Gửi lại xác minh</span>
                      </button>
                    )}
                    <Link
                      to={`/profiles/${p.patientId}/verification`}
                      className="h-9 px-3.5 rounded-xl bg-secondary-container/60 text-[#005231] hover:bg-secondary-container text-xs font-semibold flex items-center gap-1.5 transition-colors"
                    >
                      <span className="material-symbols-outlined text-[16px]">verified_user</span>
                      <span>Xác minh</span>
                    </Link>
                  </div>

                  {p.relationship !== 'Self' && (
                    <button
                      onClick={() => handleUnlink(p.patientId, p.fullName)}
                      className="h-9 px-3 rounded-xl text-tertiary hover:bg-error-container text-xs font-medium flex items-center gap-1 transition-colors"
                      title="Hủy quản lý hồ sơ này"
                    >
                      <span className="material-symbols-outlined text-[16px]">person_remove</span>
                      <span className="hidden sm:inline">Hủy liên kết</span>
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
