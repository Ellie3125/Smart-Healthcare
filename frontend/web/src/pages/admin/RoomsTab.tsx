import React, { useEffect, useState } from 'react';
import { apiClient } from '../../api/client';
import { RoomAccountDto, RoomAdminDto, RoomSessionDto } from '../../types/admin';

export const RoomsTab: React.FC = () => {
  const [rooms, setRooms] = useState<RoomAdminDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [roomTypeFilter, setRoomTypeFilter] = useState('');
  const [activeFilter, setActiveFilter] = useState('');

  // Modals
  const [showRoomModal, setShowRoomModal] = useState(false);
  const [editingRoom, setEditingRoom] = useState<RoomAdminDto | null>(null);

  const [showAccountModal, setShowAccountModal] = useState(false);
  const [selectedRoomForAccount, setSelectedRoomForAccount] = useState<RoomAdminDto | null>(null);
  const [roomAccount, setRoomAccount] = useState<RoomAccountDto | null>(null);
  const [loadingAccount, setLoadingAccount] = useState(false);
  const [newRoomPassword, setNewRoomPassword] = useState('Hospital@2026');

  const [showSessionsModal, setShowSessionsModal] = useState(false);
  const [selectedRoomForSessions, setSelectedRoomForSessions] = useState<RoomAdminDto | null>(null);
  const [sessions, setSessions] = useState<RoomSessionDto[]>([]);
  const [loadingSessions, setLoadingSessions] = useState(false);
  const [revokingSessionId, setRevokingSessionId] = useState<string | null>(null);

  // Form states - Room
  const [roomNumber, setRoomNumber] = useState('');
  const [roomName, setRoomName] = useState('');
  const [roomType, setRoomType] = useState('Clinical');
  const [departmentName, setDepartmentName] = useState('');

  // Form states - RoomAccount
  const [accountCode, setAccountCode] = useState('');
  const [accountUserName, setAccountUserName] = useState('');
  const [accountEmail, setAccountEmail] = useState('');
  const [accountPassword, setAccountPassword] = useState('');

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const fetchRooms = async () => {
    setIsLoading(true);
    try {
      const params = new URLSearchParams();
      if (roomTypeFilter) params.append('roomType', roomTypeFilter);
      if (activeFilter !== '') params.append('isActive', activeFilter);

      const res = await apiClient.get<RoomAdminDto[]>(`/api/staff/admin/rooms?${params.toString()}`);
      setRooms(res.data);
    } catch {
      setErrorMsg('Không thể tải danh sách phòng khám & CLS.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchRooms();
  }, [roomTypeFilter, activeFilter]);

  const handleOpenCreateRoom = () => {
    setEditingRoom(null);
    setRoomNumber('');
    setRoomName('');
    setRoomType('Clinical');
    setDepartmentName('Khoa Khám Bệnh');
    setShowRoomModal(true);
  };

  const handleOpenEditRoom = (r: RoomAdminDto) => {
    setEditingRoom(r);
    setRoomNumber(r.roomNumber);
    setRoomName(r.roomName);
    setRoomType(r.roomType);
    setDepartmentName(r.departmentName);
    setShowRoomModal(true);
  };

  const handleSaveRoom = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setErrorMsg(null);

    try {
      if (editingRoom) {
        await apiClient.put(`/api/staff/admin/rooms/${editingRoom.id}`, {
          roomName: roomName.trim(),
          roomType,
          departmentName: departmentName.trim(),
          isActive: editingRoom.isActive,
        });
        setSuccessMsg(`Cập nhật phòng "${roomName}" thành công!`);
      } else {
        await apiClient.post('/api/staff/admin/rooms', {
          roomNumber: roomNumber.trim(),
          roomName: roomName.trim(),
          roomType,
          departmentName: departmentName.trim(),
        });
        setSuccessMsg(`Tạo mới phòng ${roomNumber.trim()} - ${roomName.trim()} thành công!`);
      }
      setShowRoomModal(false);
      fetchRooms();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(errorObj.response?.data?.detail || 'Thao tác không thành công.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleRoomActive = async (r: RoomAdminDto) => {
    const nextStatus = !r.isActive;
    if (!window.confirm(`${nextStatus ? 'Mở lại' : 'Tạm dừng hoạt động'} phòng ${r.roomNumber}? Dữ liệu liên kết ở các module khác không bị xóa.`)) {
      return;
    }
    try {
      await apiClient.put(`/api/staff/admin/rooms/${r.id}`, {
        roomName: r.roomName,
        roomType: r.roomType,
        departmentName: r.departmentName,
        isActive: nextStatus,
      });
      setSuccessMsg(`Đã ${nextStatus ? 'mở lại' : 'tạm dừng'} phòng ${r.roomNumber}.`);
      fetchRooms();
    } catch {
      setErrorMsg('Không thể cập nhật trạng thái phòng.');
    }
  };

  const handleOpenCreateAccount = async (r: RoomAdminDto) => {
    setSelectedRoomForAccount(r);
    setRoomAccount(null);
    setLoadingAccount(true);
    setNewRoomPassword('Hospital@2026');
    setAccountCode(`ACC_${r.roomNumber.replace(/\s+/g, '_')}`);
    setAccountUserName(`room_${r.roomNumber.toLowerCase().replace(/[^a-z0-9]/g, '_')}`);
    setAccountEmail(`room.${r.roomNumber.toLowerCase().replace(/[^a-z0-9]/g, '')}@hospital.vn`);
    setAccountPassword('Hospital@2026');
    setErrorMsg(null);
    setShowAccountModal(true);

    try {
      const res = await apiClient.get<RoomAccountDto>(`/api/staff/admin/rooms/${r.id}/account`);
      setRoomAccount(res.data);
    } catch {
      setRoomAccount(null);
    } finally {
      setLoadingAccount(false);
    }
  };

  const handleToggleRoomAccountActive = async () => {
    if (!roomAccount || !selectedRoomForAccount) return;
    setIsSubmitting(true);
    setErrorMsg(null);
    try {
      const nextStatus = !roomAccount.isActive;
      const res = await apiClient.post<RoomAccountDto>(
        `/api/staff/admin/rooms/accounts/${roomAccount.roomAccountId}/toggle-active?isActive=${nextStatus}`
      );
      setRoomAccount(res.data);
      setSuccessMsg(
        `Đã ${nextStatus ? 'kích hoạt' : 'vô hiệu hóa'} tài khoản phòng "${selectedRoomForAccount.roomNumber}".`
      );
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(errorObj.response?.data?.detail || 'Không thể đổi trạng thái tài khoản phòng.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResetRoomPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!roomAccount || !selectedRoomForAccount) return;
    if (newRoomPassword.trim().length < 6) {
      setErrorMsg('Mật khẩu mới phải có ít nhất 6 ký tự.');
      return;
    }

    setIsSubmitting(true);
    setErrorMsg(null);
    try {
      await apiClient.post(`/api/staff/admin/rooms/accounts/${roomAccount.roomAccountId}/reset-password`, {
        newPassword: newRoomPassword,
      });
      setSuccessMsg(`Đã đặt lại mật khẩu cho tài khoản phòng "${roomAccount.userName}".`);
      setShowAccountModal(false);
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(errorObj.response?.data?.detail || 'Không thể đặt lại mật khẩu tài khoản phòng.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleSaveAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedRoomForAccount) return;

    setIsSubmitting(true);
    setErrorMsg(null);
    try {
      await apiClient.post(`/api/staff/admin/rooms/${selectedRoomForAccount.id}/account`, {
        accountCode: accountCode.trim(),
        userName: accountUserName.trim(),
        email: accountEmail.trim(),
        password: accountPassword,
      });
      setSuccessMsg(`Tạo tài khoản phòng cho "${selectedRoomForAccount.roomName}" thành công!`);
      setShowAccountModal(false);
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(errorObj.response?.data?.detail || 'Lỗi khi tạo tài khoản phòng (mỗi phòng tối đa 1 tài khoản).');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenSessions = async (r: RoomAdminDto) => {
    setSelectedRoomForSessions(r);
    setShowSessionsModal(true);
    setLoadingSessions(true);
    try {
      const res = await apiClient.get<RoomSessionDto[]>(`/api/staff/admin/rooms/${r.id}/sessions`);
      setSessions(res.data);
    } catch {
      setErrorMsg('Không thể tải danh sách phiên thiết bị phòng.');
    } finally {
      setLoadingSessions(false);
    }
  };

  const handleRevokeSession = async (ses: RoomSessionDto) => {
    if (!selectedRoomForSessions) return;
    setRevokingSessionId(ses.sessionId);
    setErrorMsg(null);
    try {
      await apiClient.post(`/api/staff/admin/rooms/sessions/${ses.sessionId}/revoke`, {
        reason: 'Admin buộc đăng xuất phiên thiết bị phòng từ cổng quản trị',
      });
      setSuccessMsg(`Đã buộc đăng xuất thiết bị ${ses.deviceId}.`);
      const res = await apiClient.get<RoomSessionDto[]>(
        `/api/staff/admin/rooms/${selectedRoomForSessions.id}/sessions`
      );
      setSessions(res.data);
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(errorObj.response?.data?.detail || 'Không thể buộc đăng xuất phiên thiết bị.');
    } finally {
      setRevokingSessionId(null);
    }
  };

  const getRoomTypeBadge = (type: string) => {
    switch (type) {
      case 'Clinical':
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-[#E8F5EE] text-[#006a41] border border-[#DDE7E1]">Khám Lâm Sàng</span>;
      case 'Laboratory':
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-[#E0F2FE] text-[#0369A1] border border-[#BAE6FD]">Xét Nghiệm</span>;
      case 'XRay':
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-[#FEF3C7] text-[#B45309] border border-[#FDE68A]">X-Quang</span>;
      case 'Ultrasound':
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-[#F3E8FF] text-[#7E22CE] border border-[#E9D5FF]">Siêu Âm</span>;
      case 'Endoscopy':
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-[#FCE7F3] text-[#BE185D] border border-[#FBCFE8]">Nội Soi</span>;
      default:
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-gray-100 text-gray-700">{type}</span>;
    }
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <select
            value={roomTypeFilter}
            onChange={(e) => setRoomTypeFilter(e.target.value)}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:ring-2 focus:ring-[#006a41]/20"
          >
            <option value="">Tất cả loại phòng</option>
            <option value="Clinical">Khám Lâm Sàng</option>
            <option value="Laboratory">Xét Nghiệm (Lab)</option>
            <option value="XRay">Chụp X-Quang</option>
            <option value="Ultrasound">Siêu Âm</option>
            <option value="Endoscopy">Nội Soi</option>
          </select>

          <select
            value={activeFilter}
            onChange={(e) => setActiveFilter(e.target.value)}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:ring-2 focus:ring-[#006a41]/20"
          >
            <option value="">Tất cả trạng thái</option>
            <option value="true">Đang hoạt động</option>
            <option value="false">Tạm dừng hoạt động</option>
          </select>
        </div>

        <button
          onClick={handleOpenCreateRoom}
          className="h-9 px-4 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] transition-colors flex items-center gap-1.5 shadow-sm shrink-0"
        >
          <span className="material-symbols-outlined text-[18px]">meeting_room</span>
          <span>Tạo Phòng Mới</span>
        </button>
      </div>

      {successMsg && (
        <div className="p-3 rounded-lg bg-[#ECFDF5] border border-[#A7F3D0] text-xs text-[#065F46] flex items-center justify-between">
          <span>{successMsg}</span>
          <button onClick={() => setSuccessMsg(null)}>
            <span className="material-symbols-outlined text-[16px]">close</span>
          </button>
        </div>
      )}

      {errorMsg && (
        <div className="p-3 rounded-lg bg-[#FEE2E2] border border-[#FECACA] text-xs text-[#B91C1C] flex items-center justify-between">
          <span>{errorMsg}</span>
          <button onClick={() => setErrorMsg(null)}>
            <span className="material-symbols-outlined text-[16px]">close</span>
          </button>
        </div>
      )}

      {/* Rooms Table */}
      <div className="bg-white rounded-xl border border-[#DDE7E1] shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="py-16 text-center text-xs text-[#5E6F66]">Đang tải danh sách phòng...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-[#F7FAF8] border-b border-[#DDE7E1] text-[#5E6F66] font-semibold uppercase tracking-wider">
                  <th className="py-3 px-4">Số Phòng</th>
                  <th className="py-3 px-4">Tên Phòng Khám / Dịch Vụ</th>
                  <th className="py-3 px-4">Loại Phòng</th>
                  <th className="py-3 px-4">Khoa Trực Thuộc</th>
                  <th className="py-3 px-4">Trạng thái</th>
                  <th className="py-3 px-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {rooms.map((r) => (
                  <tr key={r.id} className="hover:bg-[#F9FBFA] transition-colors">
                    <td className="py-3 px-4 font-mono font-bold text-[#006a41]">{r.roomNumber}</td>
                    <td className="py-3 px-4 font-semibold text-[#1C2B24]">{r.roomName}</td>
                    <td className="py-3 px-4">{getRoomTypeBadge(r.roomType)}</td>
                    <td className="py-3 px-4 text-[#5E6F66]">{r.departmentName}</td>
                    <td className="py-3 px-4">
                      {r.isActive ? (
                        <span className="inline-flex items-center gap-1 text-[#047857] font-semibold">
                          <span className="w-2 h-2 rounded-full bg-[#10B981]"></span>
                          Hoạt động
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-[#9CA3AF] font-semibold">
                          <span className="w-2 h-2 rounded-full bg-gray-400"></span>
                          Tạm đóng
                        </span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          onClick={() => handleOpenCreateAccount(r)}
                          className="h-7 px-2 rounded border border-[#DDE7E1] hover:bg-[#E8F5EE] text-[11px] font-semibold text-[#006a41] flex items-center gap-1"
                          title="Tạo tài khoản đăng nhập cho thiết bị phòng"
                        >
                          <span className="material-symbols-outlined text-[14px]">key</span>
                          <span>Tài khoản</span>
                        </button>
                        <button
                          onClick={() => handleOpenSessions(r)}
                          className="h-7 px-2 rounded border border-[#DDE7E1] hover:bg-gray-50 text-[11px] font-semibold text-[#5E6F66] flex items-center gap-1"
                          title="Xem phiên thiết bị đăng nhập"
                        >
                          <span className="material-symbols-outlined text-[14px]">devices</span>
                          <span>Phiên</span>
                        </button>
                        <button
                          onClick={() => handleOpenEditRoom(r)}
                          className="h-7 px-2 rounded border border-[#DDE7E1] hover:bg-gray-50 text-[11px] font-semibold text-[#1C2B24]"
                        >
                          Sửa
                        </button>
                        <button
                          onClick={() => handleToggleRoomActive(r)}
                          className={`h-7 px-2 rounded text-[11px] font-semibold border transition-colors ${
                            r.isActive
                              ? 'border-[#FECACA] text-[#B91C1C] hover:bg-[#FEE2E2]'
                              : 'border-[#A7F3D0] text-[#047857] hover:bg-[#ECFDF5]'
                          }`}
                        >
                          {r.isActive ? 'Khóa' : 'Mở'}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Modal Tạo/Sửa Phòng */}
      {showRoomModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <h3 className="text-base font-bold text-[#1C2B24]">
                {editingRoom ? `Sửa Phòng: ${editingRoom.roomNumber}` : 'Tạo Mới Phòng'}
              </h3>
              <button onClick={() => setShowRoomModal(false)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <form onSubmit={handleSaveRoom} className="space-y-3.5">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Số Phòng (RoomNumber UK) <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  disabled={!!editingRoom}
                  placeholder="P101, P102, LAB01, XRAY01..."
                  value={roomNumber}
                  onChange={(e) => setRoomNumber(e.target.value)}
                  className={`w-full h-9 px-3 rounded-lg border text-xs font-mono uppercase ${
                    editingRoom ? 'bg-gray-100 text-gray-500 border-gray-200' : 'bg-white border-[#DDE7E1]'
                  }`}
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Tên Phòng Hiển Thị <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="Phòng Khám Nội 1, Phòng Chụp X-Quang..."
                  value={roomName}
                  onChange={(e) => setRoomName(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Loại Phòng</label>
                  <select
                    value={roomType}
                    onChange={(e) => setRoomType(e.target.value)}
                    className="w-full h-9 px-2.5 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  >
                    <option value="Clinical">Clinical (Khám Lâm Sàng)</option>
                    <option value="Laboratory">Laboratory (Xét Nghiệm)</option>
                    <option value="XRay">XRay (X-Quang)</option>
                    <option value="Ultrasound">Ultrasound (Siêu Âm)</option>
                    <option value="Endoscopy">Endoscopy (Nội Soi)</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Khoa Trực Thuộc</label>
                  <input
                    type="text"
                    required
                    placeholder="Khoa Khám Bệnh"
                    value={departmentName}
                    onChange={(e) => setDepartmentName(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  />
                </div>
              </div>

              <div className="pt-3 border-t border-[#DDE7E1] flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowRoomModal(false)}
                  className="h-9 px-4 rounded-lg border border-[#DDE7E1] text-xs font-semibold hover:bg-gray-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="h-9 px-5 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] disabled:opacity-50"
                >
                  {isSubmitting ? 'Đang lưu...' : 'Lưu Phòng'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Tạo Tài Khoản Phòng RoomAccount */}
      {showAccountModal && selectedRoomForAccount && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <div>
                <h3 className="text-base font-bold text-[#1C2B24]">
                  {roomAccount ? 'Quản Lý Tài Khoản Phòng (RoomAccount)' : 'Tạo Tài Khoản Phòng (RoomAccount)'}
                </h3>
                <p className="text-xs text-[#5E6F66]">
                  Phòng: <strong>{selectedRoomForAccount.roomNumber} - {selectedRoomForAccount.roomName}</strong>
                </p>
              </div>
              <button onClick={() => setShowAccountModal(false)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            {loadingAccount ? (
              <div className="py-8 text-center text-xs text-[#5E6F66]">Đang kiểm tra tài khoản phòng...</div>
            ) : roomAccount ? (
              <div className="space-y-4">
                <div className="p-3.5 rounded-xl bg-[#F7FAF8] border border-[#DDE7E1] space-y-2 text-xs">
                  <div className="flex justify-between">
                    <span className="text-[#5E6F66]">Mã tài khoản:</span>
                    <span className="font-mono font-bold text-[#1C2B24]">{roomAccount.accountCode}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-[#5E6F66]">Tên đăng nhập:</span>
                    <span className="font-mono font-semibold text-[#1C2B24]">{roomAccount.userName}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-[#5E6F66]">Email phòng:</span>
                    <span className="text-[#1C2B24]">{roomAccount.email}</span>
                  </div>
                  <div className="flex justify-between items-center pt-1 border-t border-[#DDE7E1]">
                    <span className="text-[#5E6F66]">Trạng thái:</span>
                    <div className="flex items-center gap-2">
                      {roomAccount.isActive ? (
                        <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-[#D1FAE5] text-[#047857]">
                          Đang Hoạt Động
                        </span>
                      ) : (
                        <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-[#FEE2E2] text-[#B91C1C]">
                          Đã Vô Hiệu Hóa
                        </span>
                      )}
                      <button
                        type="button"
                        disabled={isSubmitting}
                        onClick={handleToggleRoomAccountActive}
                        className="h-7 px-2.5 rounded border border-[#DDE7E1] bg-white text-[11px] font-semibold hover:bg-gray-50 disabled:opacity-50"
                      >
                        {roomAccount.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'}
                      </button>
                    </div>
                  </div>
                </div>

                <form onSubmit={handleResetRoomPassword} className="space-y-3 border-t border-[#DDE7E1] pt-3">
                  <div>
                    <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                      Đặt Lại Mật Khẩu Mới (tối thiểu 6 ký tự)
                    </label>
                    <input
                      type="text"
                      required
                      minLength={6}
                      value={newRoomPassword}
                      onChange={(e) => setNewRoomPassword(e.target.value)}
                      className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs font-mono focus:ring-2 focus:ring-[#006a41]/20"
                    />
                  </div>
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={() => setShowAccountModal(false)}
                      className="h-9 px-4 rounded-lg border border-[#DDE7E1] text-xs font-semibold hover:bg-gray-50"
                    >
                      Đóng
                    </button>
                    <button
                      type="submit"
                      disabled={isSubmitting}
                      className="h-9 px-4 rounded-lg bg-[#D97706] text-white text-xs font-semibold hover:bg-[#B45309] disabled:opacity-50"
                    >
                      {isSubmitting ? 'Đang xử lý...' : 'Đặt Lại Mật Khẩu'}
                    </button>
                  </div>
                </form>
              </div>
            ) : (
            <form onSubmit={handleSaveAccount} className="space-y-3.5">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Mã Tài Khoản (AccountCode UK)</label>
                <input
                  type="text"
                  required
                  value={accountCode}
                  onChange={(e) => setAccountCode(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs font-mono uppercase focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Tên Đăng Nhập AppUser</label>
                <input
                  type="text"
                  required
                  value={accountUserName}
                  onChange={(e) => setAccountUserName(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs font-mono focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Email Riêng Cho Phòng (UK)</label>
                <input
                  type="email"
                  required
                  value={accountEmail}
                  onChange={(e) => setAccountEmail(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Mật Khẩu Đăng Nhập</label>
                <input
                  type="password"
                  required
                  value={accountPassword}
                  onChange={(e) => setAccountPassword(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div className="p-3 bg-[#E8F5EE] rounded-lg border border-[#DDE7E1] text-[11px] text-[#5E6F66] leading-relaxed">
                Mỗi phòng chỉ gắn đúng 1 tài khoản RoomAccount. Tài khoản phòng không liên kết với bảng Staff.
              </div>

              <div className="pt-3 border-t border-[#DDE7E1] flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowAccountModal(false)}
                  className="h-9 px-4 rounded-lg border border-[#DDE7E1] text-xs font-semibold hover:bg-gray-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="h-9 px-5 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] disabled:opacity-50"
                >
                  {isSubmitting ? 'Đang tạo...' : 'Tạo Tài Khoản Phòng'}
                </button>
              </div>
            </form>
            )}
          </div>
        </div>
      )}

      {/* Modal Xem Phiên Thiết Bị RoomDeviceSession */}
      {showSessionsModal && selectedRoomForSessions && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-xl p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <div>
                <h3 className="text-base font-bold text-[#1C2B24]">Phiên Thiết Bị (RoomDeviceSession)</h3>
                <p className="text-xs text-[#5E6F66]">
                  Phòng: <strong>{selectedRoomForSessions.roomNumber} - {selectedRoomForSessions.roomName}</strong>
                </p>
              </div>
              <button onClick={() => setShowSessionsModal(false)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            {loadingSessions ? (
              <div className="py-10 text-center text-xs text-[#5E6F66]">Đang tải danh sách phiên thiết bị...</div>
            ) : sessions.length === 0 ? (
              <div className="py-10 text-center text-xs text-[#5E6F66]">
                Chưa có thiết bị nào đăng nhập vào tài khoản phòng này.
              </div>
            ) : (
              <div className="overflow-x-auto max-h-72">
                <table className="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr className="bg-[#F7FAF8] border-b border-[#DDE7E1] text-[#5E6F66] font-semibold">
                      <th className="py-2.5 px-3">Mã Thiết Bị</th>
                      <th className="py-2.5 px-3">Địa Chỉ IP</th>
                      <th className="py-2.5 px-3">Thời Điểm Đăng Nhập</th>
                      <th className="py-2.5 px-3">Trạng Thái</th>
                      <th className="py-2.5 px-3 text-right">Thao Tác</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#DDE7E1]">
                    {sessions.map((ses) => (
                      <tr key={ses.sessionId} className="hover:bg-gray-50">
                        <td className="py-2 px-3 font-mono font-bold text-[#1C2B24]">{ses.deviceId}</td>
                        <td className="py-2 px-3 font-mono text-[#5E6F66]">{ses.deviceIpAddress}</td>
                        <td className="py-2 px-3 text-[#5E6F66]">{new Date(ses.loginAtUtc).toLocaleString('vi-VN')}</td>
                        <td className="py-2 px-3">
                          {ses.isActive ? (
                            <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-[#D1FAE5] text-[#047857]">
                              Đang Hoạt Động
                            </span>
                          ) : (
                            <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-gray-100 text-gray-500">
                              Đã Đăng Xuất
                            </span>
                          )}
                        </td>
                        <td className="py-2 px-3 text-right">
                          {ses.isActive ? (
                            <button
                              type="button"
                              disabled={revokingSessionId === ses.sessionId}
                              onClick={() => handleRevokeSession(ses)}
                              className="h-7 px-2.5 rounded border border-[#FECACA] text-[#B91C1C] hover:bg-[#FEE2E2] text-[11px] font-semibold disabled:opacity-50"
                            >
                              {revokingSessionId === ses.sessionId ? 'Đang đóng...' : 'Buộc đăng xuất'}
                            </button>
                          ) : (
                            <span className="text-[11px] text-gray-400">
                              {ses.logoutAtUtc ? new Date(ses.logoutAtUtc).toLocaleTimeString('vi-VN') : '—'}
                            </span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <div className="p-3 bg-[#FFFBEB] rounded-lg border border-[#FDE68A] text-[11px] text-[#92400E] leading-relaxed">
              Theo quy định bảo mật (SEC-02): Mỗi tài khoản phòng chỉ có tối đa 01 phiên hoạt động duy nhất. Khi một thiết bị mới đăng nhập, phiên cũ sẽ tự động bị đóng và ghi nhận vào AuditLog.
            </div>

            <div className="pt-3 border-t border-[#DDE7E1] flex justify-end">
              <button
                type="button"
                onClick={() => setShowSessionsModal(false)}
                className="h-9 px-4 rounded-lg bg-gray-100 text-[#1C2B24] text-xs font-semibold hover:bg-gray-200"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
