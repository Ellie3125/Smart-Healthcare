import React, { useEffect, useState } from "react";
import { apiClient } from "../../api/client";
import {
  PagedResult,
  VerificationDetailDto,
  VerificationItemDto,
} from "../../types/verification";

type ReceptionTab = "verifications" | "checkin";

export const ReceptionPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<ReceptionTab>("verifications");

  // Verification Queue States
  const [items, setItems] = useState<VerificationItemDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [statusFilter, setStatusFilter] = useState("Pending");
  const [searchTerm, setSearchTerm] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Detail & Action Modals
  const [detailItem, setDetailItem] = useState<VerificationDetailDto | null>(
    null,
  );
  const [loadingDetail, setLoadingDetail] = useState(false);

  const [reasonModal, setReasonModal] = useState<{
    mode: "reject" | "revoke";
    item: VerificationItemDto;
  } | null>(null);
  const [reasonText, setReasonText] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Edit Patient Info Modal (PUT /api/staff/patients/{patientId})
  const [editTarget, setEditTarget] = useState<VerificationItemDto | null>(
    null,
  );
  const [editFullName, setEditFullName] = useState("");
  const [editDob, setEditDob] = useState("");
  const [editGender, setEditGender] = useState("Male");
  const [editNationalId, setEditNationalId] = useState("");
  const [editPhone, setEditPhone] = useState("");
  const [editAddress, setEditAddress] = useState("");
  const [editReason, setEditReason] = useState("");

  const fetchVerifications = async (targetPage = page) => {
    setIsLoading(true);
    setErrorMsg(null);
    try {
      const params = new URLSearchParams();
      if (statusFilter) params.append("status", statusFilter);
      if (searchTerm.trim()) params.append("searchTerm", searchTerm.trim());
      params.append("page", String(targetPage));
      params.append("pageSize", String(pageSize));

      const res = await apiClient.get<PagedResult<VerificationItemDto>>(
        `/api/staff/verifications?${params}`,
      );
      setItems(res.data.items);
      setTotalCount(res.data.totalCount);
    } catch {
      setErrorMsg("Không thể tải danh sách hồ sơ xác minh.");
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchVerifications(page);
  }, [statusFilter, page]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    fetchVerifications(1);
  };

  const handleOpenDetail = async (item: VerificationItemDto) => {
    setLoadingDetail(true);
    setErrorMsg(null);
    try {
      const res = await apiClient.get<VerificationDetailDto>(
        `/api/staff/verifications/${item.representativeId}`,
      );
      setDetailItem(res.data);
    } catch {
      setErrorMsg("Không thể tải chi tiết người đại diện.");
    } finally {
      setLoadingDetail(false);
    }
  };

  const handleVerify = async (item: VerificationItemDto) => {
    setIsSubmitting(true);
    setErrorMsg(null);
    setSuccessMsg(null);
    try {
      await apiClient.post(
        `/api/staff/verifications/${item.representativeId}/verify`,
      );
      setSuccessMsg(`Đã xác minh hồ sơ "${item.patientFullName}".`);
      setDetailItem(null);
      await fetchVerifications(page);
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(
        errorObj.response?.data?.detail || "Không thể xác minh hồ sơ.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleSubmitReasonAction = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reasonModal) return;
    if (!reasonText.trim()) {
      setErrorMsg("Vui lòng nhập lý do thao tác.");
      return;
    }

    setIsSubmitting(true);
    setErrorMsg(null);
    setSuccessMsg(null);
    try {
      const endpoint =
        reasonModal.mode === "reject"
          ? `/api/staff/verifications/${reasonModal.item.representativeId}/reject`
          : `/api/staff/verifications/${reasonModal.item.representativeId}/revoke`;
      await apiClient.post(endpoint, { reason: reasonText.trim() });
      setSuccessMsg(
        reasonModal.mode === "reject"
          ? `Đã từ chối yêu cầu xác minh cho "${reasonModal.item.patientFullName}".`
          : `Đã thu hồi quyền đại diện của hồ sơ "${reasonModal.item.patientFullName}".`,
      );
      setReasonModal(null);
      setReasonText("");
      setDetailItem(null);
      await fetchVerifications(page);
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(
        errorObj.response?.data?.detail ||
          "Không thể thực hiện chuyển trạng thái.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenEditPatient = (item: VerificationItemDto) => {
    setEditTarget(item);
    setEditFullName(item.patientFullName);
    setEditDob(item.dateOfBirth);
    setEditGender(item.gender);
    setEditNationalId("");
    setEditPhone(item.phoneNumber);
    setEditAddress(item.address);
    setEditReason("");
    setErrorMsg(null);
  };

  const handleSaveEditPatient = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editTarget) return;
    if (!editReason.trim()) {
      setErrorMsg("Tiếp tân sửa hồ sơ bắt buộc phải nhập lý do ghi AuditLog.");
      return;
    }

    setIsSubmitting(true);
    setErrorMsg(null);
    setSuccessMsg(null);
    try {
      await apiClient.put(`/api/staff/patients/${editTarget.patientId}`, {
        fullName: editFullName.trim(),
        dateOfBirth: editDob,
        gender: editGender,
        nationalId: editNationalId.trim() || null,
        phoneNumber: editPhone.trim(),
        address: editAddress.trim(),
        reason: editReason.trim(),
        keepExistingNationalId: !editNationalId.trim(),
      });
      setSuccessMsg(
        `Đã cập nhật hồ sơ bệnh nhân "${editFullName.trim()}" và ghi nhận AuditLog.`,
      );
      setEditTarget(null);
      await fetchVerifications(page);
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(
        errorObj.response?.data?.detail ||
          "Không thể cập nhật thông tin bệnh nhân.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Verified":
        return (
          <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-[#D1FAE5] text-[#047857]">
            Đã xác minh
          </span>
        );
      case "Pending":
        return (
          <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-[#FEF3C7] text-[#B45309]">
            Chờ xác minh
          </span>
        );
      case "Rejected":
        return (
          <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-[#FEE2E2] text-[#B91C1C]">
            Đã từ chối
          </span>
        );
      case "Revoked":
        return (
          <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-gray-100 text-gray-600">
            Đã thu hồi
          </span>
        );
      default:
        return (
          <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-gray-100 text-gray-600">
            {status}
          </span>
        );
    }
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // Week 1 mock check-in data preserved in second tab
  const mockTickets = [
    {
      ticketNumber: "A-001",
      patientName: "Nguyễn Văn Bệnh Nhân",
      serviceTime: "07:00",
      status: "Chờ tiếp đón",
    },
    {
      ticketNumber: "A-002",
      patientName: "Lê Hoàng Minh",
      serviceTime: "07:15",
      status: "Đã check-in",
    },
    {
      ticketNumber: "A-003",
      patientName: "Phạm Thị Mai",
      serviceTime: "07:30",
      status: "Chờ tiếp đón",
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div>
          <h1 className="font-headline-md text-headline-md font-bold text-[#1C2B24]">
            Quầy Tiếp Đón &amp; Xác Minh Hồ Sơ Bệnh Nhân
          </h1>
          <p className="font-body-md text-[#5E6F66] mt-1 text-sm">
            Đối chiếu thẻ CCCD vật lý tại quầy, xác minh người đại diện và điều
            chỉnh nhân khẩu hồ sơ đã xác minh.
          </p>
        </div>

        <div className="flex rounded-lg border border-[#DDE7E1] bg-white p-1 shrink-0">
          <button
            type="button"
            onClick={() => setActiveTab("verifications")}
            className={`px-3.5 py-1.5 rounded-md text-xs font-semibold transition-colors ${
              activeTab === "verifications"
                ? "bg-[#006a41] text-white"
                : "text-[#5E6F66] hover:text-[#1C2B24]"
            }`}
          >
            Hàng đợi xác minh CCCD
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("checkin")}
            className={`px-3.5 py-1.5 rounded-md text-xs font-semibold transition-colors ${
              activeTab === "checkin"
                ? "bg-[#006a41] text-white"
                : "text-[#5E6F66] hover:text-[#1C2B24]"
            }`}
          >
            Tiếp đón khám bệnh
          </button>
        </div>
      </div>

      {activeTab === "verifications" ? (
        <div className="space-y-4">
          {/* Filters */}
          <form
            onSubmit={handleSearch}
            className="bg-white p-4 rounded-xl border border-[#DDE7E1] shadow-sm flex flex-wrap gap-3 items-center justify-between"
          >
            <div className="flex flex-wrap items-center gap-3 flex-1">
              <div className="relative min-w-[240px] flex-1 max-w-md">
                <span className="material-symbols-outlined text-[18px] text-[#5E6F66] absolute left-3 top-2.5">
                  search
                </span>
                <input
                  type="text"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  placeholder="Tìm theo họ tên, số CCCD hoặc SĐT..."
                  className="w-full h-9 pl-9 pr-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <select
                value={statusFilter}
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setPage(1);
                }}
                className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:ring-2 focus:ring-[#006a41]/20"
              >
                <option value="Pending">Chờ xác minh (Pending)</option>
                <option value="Verified">Đã xác minh (Verified)</option>
                <option value="Rejected">Đã từ chối (Rejected)</option>
                <option value="Revoked">Đã thu hồi (Revoked)</option>
                <option value="">Tất cả trạng thái</option>
              </select>

              <button
                type="submit"
                className="h-9 px-4 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654]"
              >
                Tìm kiếm
              </button>
            </div>

            <div className="text-xs text-[#5E6F66]">
              Tổng số: <strong className="text-[#1C2B24]">{totalCount}</strong>{" "}
              hồ sơ
            </div>
          </form>

          {successMsg && (
            <div className="p-3 rounded-lg bg-[#ECFDF5] border border-[#A7F3D0] text-xs text-[#065F46] flex items-center justify-between">
              <span>{successMsg}</span>
              <button
                onClick={() => setSuccessMsg(null)}
                className="text-xs font-bold"
              >
                ✕
              </button>
            </div>
          )}

          {errorMsg && (
            <div className="p-3 rounded-lg bg-[#FEE2E2] border border-[#FECACA] text-xs text-[#B91C1C] flex items-center justify-between">
              <span>{errorMsg}</span>
              <button
                onClick={() => setErrorMsg(null)}
                className="text-xs font-bold"
              >
                ✕
              </button>
            </div>
          )}

          {/* Table */}
          <div className="bg-white rounded-xl border border-[#DDE7E1] overflow-hidden shadow-sm">
            {isLoading ? (
              <div className="py-12 text-center text-xs text-[#5E6F66]">
                Đang tải danh sách xác minh...
              </div>
            ) : items.length === 0 ? (
              <div className="py-12 text-center text-xs text-[#5E6F66]">
                Không có hồ sơ nào phù hợp điều kiện lọc.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr className="bg-[#F7FAF8] border-b border-[#DDE7E1] text-[#5E6F66] font-semibold uppercase tracking-wider">
                      <th className="py-3 px-4">Bệnh Nhân</th>
                      <th className="py-3 px-4">Ngày Sinh / GT</th>
                      <th className="py-3 px-4">CCCD (Che)</th>
                      <th className="py-3 px-4">SĐT &amp; Địa Chỉ</th>
                      <th className="py-3 px-4">Quan Hệ</th>
                      <th className="py-3 px-4">Trạng Thái</th>
                      <th className="py-3 px-4 text-right">Thao Tác</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#DDE7E1]">
                    {items.map((item) => (
                      <tr
                        key={item.representativeId}
                        className="hover:bg-[#F9FBFA]"
                      >
                        <td className="py-3 px-4 font-semibold text-[#1C2B24]">
                          {item.patientFullName}
                        </td>
                        <td className="py-3 px-4 text-[#5E6F66]">
                          <div>{item.dateOfBirth}</div>
                          <div className="text-[11px]">
                            {item.gender === "Male"
                              ? "Nam"
                              : item.gender === "Female"
                                ? "Nữ"
                                : "Khác"}
                          </div>
                        </td>
                        <td className="py-3 px-4 font-mono text-[#1C2B24]">
                          {item.maskedNationalId || "Chưa khai"}
                        </td>
                        <td className="py-3 px-4 text-[#5E6F66] max-w-[220px]">
                          <div className="font-mono text-[#1C2B24]">
                            {item.phoneNumber}
                          </div>
                          <div className="truncate text-[11px]">
                            {item.address}
                          </div>
                        </td>
                        <td className="py-3 px-4 font-medium text-[#1C2B24]">
                          {item.relationship}
                        </td>
                        <td className="py-3 px-4">
                          {getStatusBadge(item.verificationStatus)}
                        </td>
                        <td className="py-3 px-4 text-right">
                          <div className="inline-flex flex-wrap justify-end gap-1.5">
                            <button
                              type="button"
                              onClick={() => handleOpenDetail(item)}
                              className="h-7 px-2.5 rounded border border-[#DDE7E1] text-[#1C2B24] hover:bg-gray-50 font-semibold"
                            >
                              Chi tiết
                            </button>
                            {item.verificationStatus === "Pending" && (
                              <>
                                <button
                                  type="button"
                                  disabled={isSubmitting}
                                  onClick={() => handleVerify(item)}
                                  className="h-7 px-2.5 rounded bg-[#006a41] text-white hover:bg-[#028654] font-semibold disabled:opacity-50"
                                >
                                  Xác minh
                                </button>
                                <button
                                  type="button"
                                  onClick={() => {
                                    setReasonText("");
                                    setReasonModal({ mode: "reject", item });
                                  }}
                                  className="h-7 px-2.5 rounded border border-[#FECACA] text-[#B91C1C] hover:bg-[#FEE2E2] font-semibold"
                                >
                                  Từ chối
                                </button>
                              </>
                            )}
                            {item.verificationStatus === "Verified" && (
                              <>
                                <button
                                  type="button"
                                  onClick={() => handleOpenEditPatient(item)}
                                  className="h-7 px-2.5 rounded border border-[#BFDBFE] text-[#1D4ED8] hover:bg-[#EFF6FF] font-semibold"
                                >
                                  Sửa hồ sơ
                                </button>
                                <button
                                  type="button"
                                  onClick={() => {
                                    setReasonText("");
                                    setReasonModal({ mode: "revoke", item });
                                  }}
                                  className="h-7 px-2.5 rounded border border-[#FECACA] text-[#B91C1C] hover:bg-[#FEE2E2] font-semibold"
                                >
                                  Thu hồi
                                </button>
                              </>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="px-4 py-3 border-t border-[#DDE7E1] flex items-center justify-between text-xs text-[#5E6F66]">
                <span>
                  Trang {page} / {totalPages}
                </span>
                <div className="flex gap-2">
                  <button
                    type="button"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    className="h-8 px-3 rounded border border-[#DDE7E1] disabled:opacity-40"
                  >
                    Trước
                  </button>
                  <button
                    type="button"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    className="h-8 px-3 rounded border border-[#DDE7E1] disabled:opacity-40"
                  >
                    Sau
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      ) : (
        <div className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="bg-white p-5 rounded-xl border border-[#DDE7E1] shadow-sm">
              <span className="text-[12px] font-semibold text-[#5E6F66] uppercase">
                Số vé trong ngày
              </span>
              <div className="text-3xl font-bold text-[#006a41] mt-2">
                24 / 30
              </div>
              <span className="text-xs text-[#5E6F66] mt-1 block">
                Hạn mức phiên sáng (POL-01)
              </span>
            </div>
            <div className="bg-white p-5 rounded-xl border border-[#DDE7E1] shadow-sm">
              <span className="text-[12px] font-semibold text-[#5E6F66] uppercase">
                Đã check-in
              </span>
              <div className="text-3xl font-bold text-[#1C2B24] mt-2">18</div>
              <span className="text-xs text-[#5E6F66] mt-1 block">
                Sẵn sàng phân luồng đo sinh hiệu
              </span>
            </div>
            <div className="bg-white p-5 rounded-xl border border-[#DDE7E1] shadow-sm">
              <span className="text-[12px] font-semibold text-[#5E6F66] uppercase">
                Thời gian khóa đăng ký
              </span>
              <div className="text-3xl font-bold text-[#D32F2F] mt-2">
                09:30
              </div>
              <span className="text-xs text-[#5E6F66] mt-1 block">
                BookingCutoff ngày D (POL-02)
              </span>
            </div>
          </div>

          <div className="bg-white rounded-xl border border-[#DDE7E1] overflow-hidden shadow-sm">
            <div className="p-4 border-b border-[#DDE7E1] flex items-center justify-between">
              <h2 className="font-semibold text-[#1C2B24]">
                Danh sách bệnh nhân đăng ký hôm nay
              </h2>
              <span className="text-xs text-[#5E6F66]">
                Dữ liệu mẫu check-in (module Scheduling phụ trách tuần sau)
              </span>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead className="bg-[#F7FAF8] text-[#5E6F66] border-b border-[#DDE7E1]">
                  <tr>
                    <th className="py-3 px-4 font-semibold">Số vé</th>
                    <th className="py-3 px-4 font-semibold">Họ và tên</th>
                    <th className="py-3 px-4 font-semibold">Giờ hẹn</th>
                    <th className="py-3 px-4 font-semibold">Trạng thái</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#DDE7E1]">
                  {mockTickets.map((t) => (
                    <tr key={t.ticketNumber} className="hover:bg-[#F7FAF8]/60">
                      <td className="py-3 px-4 font-bold text-[#006a41]">
                        {t.ticketNumber}
                      </td>
                      <td className="py-3 px-4 font-medium text-[#1C2B24]">
                        {t.patientName}
                      </td>
                      <td className="py-3 px-4 text-[#5E6F66]">
                        {t.serviceTime}
                      </td>
                      <td className="py-3 px-4">
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-[#E8F5EE] text-[#006a41]">
                          {t.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* Modal Detail */}
      {(detailItem || loadingDetail) && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-lg p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <h3 className="text-base font-bold text-[#1C2B24]">
                Chi Tiết Hồ Sơ &amp; Người Đại Diện
              </h3>
              <button
                onClick={() => setDetailItem(null)}
                className="text-gray-400 hover:text-black"
              >
                <span className="material-symbols-outlined text-[20px]">
                  close
                </span>
              </button>
            </div>
            {loadingDetail ? (
              <div className="py-8 text-center text-xs text-[#5E6F66]">
                Đang tải chi tiết...
              </div>
            ) : (
              detailItem && (
                <div className="space-y-3 text-xs">
                  <div className="p-3.5 rounded-xl bg-[#F7FAF8] border border-[#DDE7E1] space-y-1.5">
                    <div className="font-bold text-[#006a41] mb-1">
                      Thông tin bệnh nhân
                    </div>
                    <div>
                      <strong>Họ tên:</strong> {detailItem.patientFullName}
                    </div>
                    <div>
                      <strong>Ngày sinh:</strong> {detailItem.dateOfBirth} •{" "}
                      <strong>Giới tính:</strong> {detailItem.gender}
                    </div>
                    <div>
                      <strong>CCCD (che 8 số đầu):</strong>{" "}
                      <span className="font-mono">
                        {detailItem.maskedNationalId || "Chưa khai"}
                      </span>
                    </div>
                    <div>
                      <strong>SĐT:</strong> {detailItem.phoneNumber}
                    </div>
                    <div>
                      <strong>Địa chỉ:</strong> {detailItem.address}
                    </div>
                  </div>
                  <div className="p-3.5 rounded-xl bg-white border border-[#DDE7E1] space-y-1.5">
                    <div className="font-bold text-[#1C2B24] mb-1">
                      Tài khoản người đại diện ({detailItem.relationship})
                    </div>
                    <div>
                      <strong>Tài khoản:</strong>{" "}
                      <span className="font-mono">
                        {detailItem.representativeUserName}
                      </span>
                    </div>
                    <div>
                      <strong>Email:</strong>{" "}
                      {detailItem.representativeEmail || "—"} •{" "}
                      <strong>SĐT:</strong>{" "}
                      {detailItem.representativePhoneNumber || "—"}
                    </div>
                    <div>
                      <strong>Trạng thái:</strong>{" "}
                      {getStatusBadge(detailItem.verificationStatus)}
                    </div>
                  </div>
                </div>
              )
            )}
          </div>
        </div>
      )}

      {/* Modal Reject / Revoke */}
      {reasonModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <h3 className="text-base font-bold text-[#1C2B24]">
                {reasonModal.mode === "reject"
                  ? "Từ Chối Xác Minh Hồ Sơ"
                  : "Thu Hồi Quyền Đại Diện"}
              </h3>
              <button
                onClick={() => setReasonModal(null)}
                className="text-gray-400 hover:text-black"
              >
                <span className="material-symbols-outlined text-[20px]">
                  close
                </span>
              </button>
            </div>
            <form onSubmit={handleSubmitReasonAction} className="space-y-4">
              <p className="text-xs text-[#5E6F66]">
                Hồ sơ:{" "}
                <strong className="text-[#1C2B24]">
                  {reasonModal.item.patientFullName}
                </strong>
              </p>
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Lý do bắt buộc (lưu AuditLog){" "}
                  <span className="text-red-500">*</span>
                </label>
                <textarea
                  rows={3}
                  required
                  value={reasonText}
                  onChange={(e) => setReasonText(e.target.value)}
                  placeholder="Nhập lý do từ chối hoặc thu hồi..."
                  className="w-full p-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setReasonModal(null)}
                  className="h-9 px-4 rounded-lg border border-[#DDE7E1] text-xs font-semibold"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="h-9 px-4 rounded-lg bg-[#D32F2F] text-white text-xs font-semibold hover:bg-[#B91C1C] disabled:opacity-50"
                >
                  {isSubmitting ? "Đang xử lý..." : "Xác nhận"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Edit Verified Patient */}
      {editTarget && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-lg p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <h3 className="text-base font-bold text-[#1C2B24]">
                Sửa Hồ Sơ Đã Xác Minh (Tại Quầy)
              </h3>
              <button
                onClick={() => setEditTarget(null)}
                className="text-gray-400 hover:text-black"
              >
                <span className="material-symbols-outlined text-[20px]">
                  close
                </span>
              </button>
            </div>
            <form onSubmit={handleSaveEditPatient} className="space-y-3">
              <div>
                <label className="block text-xs font-semibold mb-1">
                  Họ và tên
                </label>
                <input
                  type="text"
                  required
                  value={editFullName}
                  onChange={(e) => setEditFullName(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold mb-1">
                    Ngày sinh
                  </label>
                  <input
                    type="date"
                    required
                    value={editDob}
                    onChange={(e) => setEditDob(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold mb-1">
                    Giới tính
                  </label>
                  <select
                    value={editGender}
                    onChange={(e) => setEditGender(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs"
                  >
                    <option value="Male">Nam</option>
                    <option value="Female">Nữ</option>
                    <option value="Other">Khác</option>
                  </select>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold mb-1">
                    CCCD mới (12 số, để trống giữ{" "}
                    {editTarget.maskedNationalId || "nguyên"})
                  </label>
                  <input
                    type="text"
                    maxLength={12}
                    value={editNationalId}
                    onChange={(e) => setEditNationalId(e.target.value)}
                    placeholder={
                      editTarget.maskedNationalId
                        ? `Hiện tại: ${editTarget.maskedNationalId}`
                        : "12 chữ số"
                    }
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs font-mono"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold mb-1">
                    Số điện thoại
                  </label>
                  <input
                    type="text"
                    required
                    value={editPhone}
                    onChange={(e) => setEditPhone(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs"
                  />
                </div>
              </div>
              <div>
                <label className="block text-xs font-semibold mb-1">
                  Địa chỉ
                </label>
                <input
                  type="text"
                  required
                  value={editAddress}
                  onChange={(e) => setEditAddress(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold mb-1">
                  Lý do chỉnh sửa (bắt buộc lưu AuditLog){" "}
                  <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={editReason}
                  onChange={(e) => setEditReason(e.target.value)}
                  placeholder="VD: Đối chiếu CCCD gốc điều chỉnh sai ngày sinh"
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs"
                />
              </div>
              <div className="pt-2 border-t border-[#DDE7E1] flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setEditTarget(null)}
                  className="h-9 px-4 rounded-lg border border-[#DDE7E1] text-xs font-semibold"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="h-9 px-4 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] disabled:opacity-50"
                >
                  {isSubmitting ? "Đang lưu..." : "Lưu điều chỉnh"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
