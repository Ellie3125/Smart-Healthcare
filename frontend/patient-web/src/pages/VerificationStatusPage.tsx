import React, { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { apiClient } from '../api/client';
import { PatientProfileDto } from '../types/patient';

export const VerificationStatusPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const [profile, setProfile] = useState<PatientProfileDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isResubmitting, setIsResubmitting] = useState(false);

  useEffect(() => {
    const loadProfile = async () => {
      setIsLoading(true);
      setErrorMsg(null);
      try {
        const res = await apiClient.get<PatientProfileDto>(`/api/patient/profiles/${id}`);
        setProfile(res.data);
      } catch {
        try {
          const listRes = await apiClient.get<PatientProfileDto[]>('/api/patient/profiles');
          const matched = listRes.data.find((item) => item.patientId === id);
          if (matched) {
            setProfile(matched);
          } else {
            setErrorMsg('Không tìm thấy hồ sơ bệnh nhân.');
          }
        } catch {
          setErrorMsg('Không thể tải thông tin xác minh hồ sơ.');
        }
      } finally {
        setIsLoading(false);
      }
    };
    if (id) loadProfile();
  }, [id]);

  const handleResubmit = async () => {
    if (!profile) return;
    setIsResubmitting(true);
    setErrorMsg(null);
    try {
      const res = await apiClient.post<PatientProfileDto>(`/api/patient/profiles/${profile.patientId}/resubmit`);
      setProfile(res.data);
    } catch {
      setErrorMsg('Không thể gửi lại yêu cầu xác minh lúc này.');
    } finally {
      setIsResubmitting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="py-20 text-center text-[#5E6F66]">
        <div className="w-8 h-8 border-3 border-[#006a41] border-t-transparent rounded-full animate-spin mx-auto mb-3"></div>
        <p className="text-sm">Đang tải trạng thái xác minh...</p>
      </div>
    );
  }

  if (errorMsg || !profile) {
    return (
      <div className="max-w-2xl mx-auto p-6 bg-white rounded-2xl border border-[#DDE7E1] text-center">
        <p className="text-sm text-red-600 mb-4">{errorMsg || 'Không tìm thấy hồ sơ.'}</p>
        <Link to="/profiles" className="text-sm font-semibold text-[#006a41] hover:underline">
          Quay lại danh sách hồ sơ
        </Link>
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-on-surface tracking-tight">Trạng Thái Xác Minh Danh Tính</h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Hồ sơ y tế của: <strong className="text-on-surface">{profile.fullName}</strong>
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

      {/* Status Progress Card */}
      <div className="bg-surface-container-lowest rounded-2xl border border-surface-container-highest/70 shadow-[0_1px_12px_rgba(15,31,24,0.05)] p-6 sm:p-8">
        <div className="flex items-center gap-4 mb-6 pb-6 border-b border-surface-container-low">
          <div
            className={`w-14 h-14 rounded-2xl flex items-center justify-center shrink-0 ${
              profile.verificationStatus === 'Verified'
                ? 'bg-secondary-container text-[#005231]'
                : profile.verificationStatus === 'Pending'
                ? 'bg-[#fef3c7] text-[#9a5b00]'
                : profile.verificationStatus === 'Rejected'
                ? 'bg-error-container text-[#93000a]'
                : 'bg-surface-container-low text-on-surface-variant'
            }`}
          >
            <span className="material-symbols-outlined text-[32px]">
              {profile.verificationStatus === 'Verified'
                ? 'verified'
                : profile.verificationStatus === 'Pending'
                ? 'pending_actions'
                : profile.verificationStatus === 'Rejected'
                ? 'cancel'
                : 'block'}
            </span>
          </div>
          <div>
            <h2 className="text-lg font-bold text-on-surface">
              {profile.verificationStatus === 'Verified'
                ? 'Đã Xác Thực Danh Tính Thành Công'
                : profile.verificationStatus === 'Pending'
                ? 'Đang Chờ Xác Minh Tại Quầy Tiếp Tân'
                : profile.verificationStatus === 'Rejected'
                ? 'Yêu Cầu Xác Thực Bị Từ Chối'
                : 'Quyền Đại Diện Đã Thu Hồi'}
            </h2>
            <p className="text-sm text-on-surface-variant mt-0.5">
              Mã hồ sơ: <span className="font-mono text-xs font-semibold tabular-nums">{profile.patientId}</span>
            </p>
          </div>
        </div>

        {/* Feature Permission Matrix */}
        <h3 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-3">
          Quyền Hạn Sử Dụng Dịch Vụ Theo Trạng Thái
        </h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5 mb-6">
          <div
            className={`p-4 rounded-xl flex items-start gap-3 ${
              profile.verificationStatus === 'Pending' || profile.verificationStatus === 'Verified'
                ? 'bg-secondary-container/50 text-[#005231]'
                : 'bg-surface-container-low text-outline opacity-65'
            }`}
          >
            <span className="material-symbols-outlined text-[22px] shrink-0">check_circle</span>
            <div>
              <h4 className="text-sm font-semibold">Đặt lịch &amp; Thanh toán khám</h4>
              <p className="text-xs mt-0.5 opacity-90">
                Được phép đăng ký ca khám bác sĩ, thanh toán viện phí trực tuyến và nhận vé khám số.
              </p>
            </div>
          </div>

          <div
            className={`p-4 rounded-xl flex items-start gap-3 ${
              profile.verificationStatus === 'Verified'
                ? 'bg-secondary-container/50 text-[#005231]'
                : 'bg-surface-container-low text-on-surface-variant'
            }`}
          >
            <span className="material-symbols-outlined text-[22px] shrink-0">
              {profile.verificationStatus === 'Verified' ? 'check_circle' : 'lock'}
            </span>
            <div>
              <h4 className="text-sm font-semibold">Xem Kết quả CLS &amp; Bệnh án</h4>
              <p className="text-xs mt-0.5 opacity-90">
                {profile.verificationStatus === 'Verified'
                  ? 'Đã mở khóa toàn bộ kết quả xét nghiệm, chẩn đoán hình ảnh và lịch sử khám.'
                  : 'Bị khóa. Cần đối chiếu thẻ gốc tại quầy tiếp tân để bảo vệ bí mật y tế bệnh nhân.'}
              </p>
            </div>
          </div>
        </div>

        {/* Verification Instructions */}
        {profile.verificationStatus === 'Pending' && (
          <div className="bg-surface-container-low/75 p-5 rounded-xl space-y-3">
            <h4 className="text-sm font-bold text-primary flex items-center gap-1.5">
              <span className="material-symbols-outlined text-[18px]">checklist</span>
              <span>Hướng Dẫn Xác Minh Tại Cơ Sở Y Tế (FR01)</span>
            </h4>
            <ul className="text-xs text-on-surface-variant space-y-2 list-disc pl-5 leading-relaxed">
              <li>
                <strong className="text-on-surface">Bản thân bệnh nhân:</strong> Mang theo bản gốc Căn cước công dân (CCCD gắn chip) hoặc VNeID mức 2 đến quầy tiếp tân số 01 - 04 khi đến khám.
              </li>
              <li>
                <strong className="text-on-surface">Trẻ em dưới 14 tuổi:</strong> Người giám hộ mang theo Giấy khai sinh hoặc thẻ BHYT có ảnh để tiếp tân đối chiếu.
              </li>
              <li>
                <strong className="text-on-surface">Người thân khác:</strong> Xuất trình giấy tờ chứng minh quan hệ gia đình (sổ hộ khẩu, giấy đăng ký kết hôn hoặc giấy ủy quyền hợp pháp).
              </li>
              <li>
                <strong className="text-on-surface">Lưu ý quan trọng:</strong> Hệ thống Smart Healthcare tuyệt đối <em>không lưu trữ và không yêu cầu ảnh chụp thẻ CCCD</em> qua mạng nhằm bảo vệ thông tin riêng tư của người bệnh.
              </li>
            </ul>
          </div>
        )}

        {profile.verificationStatus === 'Rejected' && (
          <div className="p-4 rounded-xl bg-error-container/75 text-sm text-[#93000a] space-y-3">
            <div>
              <p className="font-semibold">Lý do từ chối: {profile.rejectionReason || 'Thông tin giấy tờ không khớp'}</p>
              <p className="text-xs mt-1">
                Vui lòng kiểm tra, chỉnh sửa lại thông tin hồ sơ (nếu cần) rồi nhấn gửi lại xác minh, sau đó mang CCCD gốc đến quầy tiếp tân để đối chiếu.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Link
                to={`/profiles/${profile.patientId}/edit`}
                className="h-9 px-3.5 rounded-xl bg-white text-[#93000a] text-xs font-semibold inline-flex items-center hover:bg-surface-container-low shadow-sm"
              >
                Chỉnh sửa hồ sơ
              </Link>
              <button
                type="button"
                disabled={isResubmitting}
                onClick={handleResubmit}
                className="h-9 px-4 rounded-xl bg-primary text-white text-xs font-semibold hover:bg-[#005231] disabled:opacity-50"
              >
                {isResubmitting ? 'Đang gửi lại...' : 'Gửi lại xác minh'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
