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
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl sm:text-2xl font-bold text-[#1C2B24]">Trạng Thái Xác Minh Danh Tính</h1>
          <p className="text-sm text-[#5E6F66] mt-1">
            Hồ sơ y tế của: <strong className="text-[#1C2B24]">{profile.fullName}</strong>
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

      {/* Status Progress Card */}
      <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-sm p-6 sm:p-8">
        <div className="flex items-center gap-4 mb-6 pb-6 border-b border-[#DDE7E1]">
          <div
            className={`w-14 h-14 rounded-2xl flex items-center justify-center shrink-0 ${
              profile.verificationStatus === 'Verified'
                ? 'bg-[#D1FAE5] text-[#047857]'
                : profile.verificationStatus === 'Pending'
                ? 'bg-[#FEF3C7] text-[#B45309]'
                : profile.verificationStatus === 'Rejected'
                ? 'bg-[#FEE2E2] text-[#B91C1C]'
                : 'bg-[#F3F4F6] text-[#4B5563]'
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
            <div className="flex items-center gap-2">
              <h2 className="text-lg font-bold text-[#1C2B24]">
                {profile.verificationStatus === 'Verified'
                  ? 'Đã Xác Thực Danh Tính Thành Công'
                  : profile.verificationStatus === 'Pending'
                  ? 'Đang Chờ Xác Minh Tại Quầy Tiếp Tân'
                  : profile.verificationStatus === 'Rejected'
                  ? 'Yêu Cầu Xác Thực Bị Từ Chối'
                  : 'Quyền Đại Diện Đã Thu Hồi'}
              </h2>
            </div>
            <p className="text-sm text-[#5E6F66] mt-0.5">
              Mã hồ sơ: <span className="font-mono text-xs font-semibold">{profile.patientId}</span>
            </p>
          </div>
        </div>

        {/* Feature Permission Matrix */}
        <h3 className="text-sm font-bold text-[#1C2B24] uppercase tracking-wider mb-3">
          Quyền Hạn Sử Dụng Dịch Vụ Theo Trạng Thái
        </h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-6">
          <div
            className={`p-4 rounded-xl border flex items-start gap-3 ${
              profile.verificationStatus === 'Pending' || profile.verificationStatus === 'Verified'
                ? 'bg-[#ECFDF5] border-[#A7F3D0] text-[#065F46]'
                : 'bg-gray-50 border-gray-200 text-gray-400 opacity-60'
            }`}
          >
            <span className="material-symbols-outlined text-[22px] shrink-0">check_circle</span>
            <div>
              <h4 className="text-sm font-semibold">Đặt lịch & Thanh toán khám</h4>
              <p className="text-xs mt-0.5">Được phép đăng ký ca khám bác sĩ, thanh toán viện phí trực tuyến và nhận vé khám số.</p>
            </div>
          </div>

          <div
            className={`p-4 rounded-xl border flex items-start gap-3 ${
              profile.verificationStatus === 'Verified'
                ? 'bg-[#ECFDF5] border-[#A7F3D0] text-[#065F46]'
                : 'bg-gray-50 border-gray-200 text-gray-500'
            }`}
          >
            <span className="material-symbols-outlined text-[22px] shrink-0">
              {profile.verificationStatus === 'Verified' ? 'check_circle' : 'lock'}
            </span>
            <div>
              <h4 className="text-sm font-semibold">Xem Kết quả CLS & Bệnh án</h4>
              <p className="text-xs mt-0.5">
                {profile.verificationStatus === 'Verified'
                  ? 'Đã mở khóa toàn bộ kết quả xét nghiệm, chẩn đoán hình ảnh và lịch sử khám.'
                  : 'Bị khóa. Cần đối chiếu thẻ gốc tại quầy tiếp tân để bảo vệ bí mật y tế bệnh nhân.'}
              </p>
            </div>
          </div>
        </div>

        {/* Verification Instructions */}
        {profile.verificationStatus === 'Pending' && (
          <div className="bg-[#F7FAF8] p-5 rounded-xl border border-[#DDE7E1] space-y-3">
            <h4 className="text-sm font-bold text-[#006a41] flex items-center gap-1.5">
              <span className="material-symbols-outlined text-[18px]">checklist</span>
              <span>Hướng Dẫn Xác Minh Tại Cơ Sở Y Tế (FR01)</span>
            </h4>
            <ul className="text-xs text-[#5E6F66] space-y-2 list-disc pl-5 leading-relaxed">
              <li>
                <strong>Bản thân bệnh nhân:</strong> Mang theo bản gốc Căn cước công dân (CCCD gắn chip) hoặc VNeID mức 2 đến quầy tiếp tân số 01 - 04 khi đến khám.
              </li>
              <li>
                <strong>Trẻ em dưới 14 tuổi:</strong> Người giám hộ mang theo Giấy khai sinh hoặc thẻ BHYT có ảnh để tiếp tân đối chiếu.
              </li>
              <li>
                <strong>Người thân khác:</strong> Xuất trình giấy tờ chứng minh quan hệ gia đình (sổ hộ khẩu, giấy đăng ký kết hôn hoặc giấy ủy quyền hợp pháp).
              </li>
              <li>
                <strong>Lưu ý quan trọng:</strong> Hệ thống Smart Healthcare tuyệt đối <em>không lưu trữ và không yêu cầu ảnh chụp thẻ CCCD</em> qua mạng nhằm bảo vệ thông tin riêng tư của người bệnh.
              </li>
            </ul>
          </div>
        )}

        {profile.verificationStatus === 'Rejected' && (
          <div className="p-4 rounded-xl bg-[#FEF2F2] border border-[#FECACA] text-sm text-[#991B1B] space-y-3">
            <div>
              <p className="font-semibold">Lý do từ chối: {profile.rejectionReason || 'Thông tin giấy tờ không khớp'}</p>
              <p className="text-xs mt-1">
                Vui lòng kiểm tra, chỉnh sửa lại thông tin hồ sơ (nếu cần) rồi nhấn gửi lại xác minh, sau đó mang CCCD gốc đến quầy tiếp tân để đối chiếu.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Link
                to={`/profiles/${profile.patientId}/edit`}
                className="h-9 px-3.5 rounded-lg border border-[#FCA5A5] bg-white text-[#991B1B] text-xs font-semibold inline-flex items-center hover:bg-[#FEF2F2]"
              >
                Chỉnh sửa hồ sơ
              </Link>
              <button
                type="button"
                disabled={isResubmitting}
                onClick={handleResubmit}
                className="h-9 px-4 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] disabled:opacity-50"
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
