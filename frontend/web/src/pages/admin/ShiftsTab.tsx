import React, { useEffect, useState } from 'react';
import { apiClient } from '../../api/client';
import { ShiftAssignmentDto, RoomAdminDto, StaffAdminDto } from '../../types/admin';

export const ShiftsTab: React.FC = () => {
  const [shifts, setShifts] = useState<ShiftAssignmentDto[]>([]);
  const [rooms, setRooms] = useState<RoomAdminDto[]>([]);
  const [staffList, setStaffList] = useState<StaffAdminDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Filters
  const [viewMode, setViewMode] = useState<'day' | 'week' | 'month'>('day');
  const [selectedDate, setSelectedDate] = useState<string>(() => {
    const today = new Date();
    return today.toISOString().split('T')[0];
  });
  const [roomIdFilter, setRoomIdFilter] = useState('');
  const [staffIdFilter, setStaffIdFilter] = useState('');

  // Modal
  const [showModal, setShowModal] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Form states
  const [formStaffId, setFormStaffId] = useState('');
  const [formRoomId, setFormRoomId] = useState('');
  const [formDate, setFormDate] = useState(selectedDate);
  const [formShiftName, setFormShiftName] = useState('Morning');
  const [formStartTime, setFormStartTime] = useState('07:30');
  const [formEndTime, setFormEndTime] = useState('11:30');
  const [formIsLeader, setFormIsLeader] = useState(false);

  const getDateRange = (mode: 'day' | 'week' | 'month', anchorDate: string) => {
    const [y, m, d] = anchorDate.split('-').map(Number);
    if (!y || !m || !d) return null;
    const fmt = (dt: Date) => dt.toISOString().split('T')[0];

    if (mode === 'week') {
      const base = new Date(Date.UTC(y, m - 1, d));
      const dayOfWeek = base.getUTCDay();
      const diffToMonday = dayOfWeek === 0 ? -6 : 1 - dayOfWeek;
      const monday = new Date(Date.UTC(y, m - 1, d + diffToMonday));
      const sunday = new Date(Date.UTC(y, m - 1, d + diffToMonday + 6));
      return { fromDate: fmt(monday), toDate: fmt(sunday) };
    }

    if (mode === 'month') {
      const firstDay = new Date(Date.UTC(y, m - 1, 1));
      const lastDay = new Date(Date.UTC(y, m, 0));
      return { fromDate: fmt(firstDay), toDate: fmt(lastDay) };
    }

    return null;
  };

  const activeRange = getDateRange(viewMode, selectedDate);

  const loadData = async () => {
    setIsLoading(true);
    try {
      const [roomRes, staffRes] = await Promise.all([
        apiClient.get<RoomAdminDto[]>('/api/staff/admin/rooms?isActive=true'),
        apiClient.get<StaffAdminDto[]>('/api/staff/admin/staff?isActive=true'),
      ]);
      setRooms(roomRes.data);
      setStaffList(staffRes.data);

      const params = new URLSearchParams();
      if (viewMode === 'day') {
        if (selectedDate) params.append('date', selectedDate);
      } else if (activeRange) {
        params.append('fromDate', activeRange.fromDate);
        params.append('toDate', activeRange.toDate);
      }
      if (roomIdFilter) params.append('roomId', roomIdFilter);
      if (staffIdFilter) params.append('staffId', staffIdFilter);

      const shiftRes = await apiClient.get<ShiftAssignmentDto[]>(`/api/staff/admin/shift-assignments?${params.toString()}`);
      setShifts(shiftRes.data);
    } catch {
      setErrorMsg('Không thể tải dữ liệu lịch phân ca trực.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [viewMode, selectedDate, roomIdFilter, staffIdFilter]);

  const handleOpenCreate = () => {
    setFormStaffId(staffList[0]?.id || '');
    setFormRoomId(rooms[0]?.id || '');
    setFormDate(selectedDate);
    setFormShiftName('Morning');
    setFormStartTime('07:30');
    setFormEndTime('11:30');
    setFormIsLeader(false);
    setShowModal(true);
  };

  const handleCreateShift = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setErrorMsg(null);

    const startsAtUtc = new Date(`${formDate}T${formStartTime}:00Z`).toISOString();
    const endsAtUtc = new Date(`${formDate}T${formEndTime}:00Z`).toISOString();

    if (new Date(endsAtUtc) <= new Date(startsAtUtc)) {
      setErrorMsg('Thời gian kết thúc ca trực phải sau thời gian bắt đầu.');
      setIsSubmitting(false);
      return;
    }

    try {
      await apiClient.post('/api/staff/admin/shift-assignments', {
        staffId: formStaffId,
        roomId: formRoomId,
        shiftDate: formDate,
        shiftName: formShiftName,
        startsAtUtc,
        endsAtUtc,
        isShiftLeader: formIsLeader,
      });
      setSuccessMsg('Gán ca trực cho nhân viên thành công!');
      setShowModal(false);
      loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(
        errorObj.response?.data?.detail ||
          'Không thể gán ca trực. Nhân viên có thể bị trùng lịch tại phòng khác, hoặc phòng đã có Trưởng ca trực.'
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteShift = async (s: ShiftAssignmentDto) => {
    if (!window.confirm(`Hủy ca trực của nhân viên "${s.staffName}" tại phòng "${s.roomNumber}" ngày ${s.shiftDate}?`)) {
      return;
    }
    try {
      await apiClient.delete(`/api/staff/admin/shift-assignments/${s.id}`);
      setSuccessMsg('Đã hủy phân công ca trực.');
      loadData();
    } catch {
      setErrorMsg('Không thể hủy ca trực.');
    }
  };

  const getShiftBadge = (shiftName: string) => {
    switch (shiftName) {
      case 'Morning':
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-[#FEF3C7] text-[#B45309] border border-[#FDE68A]">Ca Sáng</span>;
      case 'Afternoon':
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-[#E0F2FE] text-[#0369A1] border border-[#BAE6FD]">Ca Chiều</span>;
      case 'FullDay':
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-[#E8F5EE] text-[#006a41] border border-[#DDE7E1]">Cả Ngày</span>;
      default:
        return <span className="px-2 py-0.5 rounded text-xs font-semibold bg-gray-100 text-gray-700">{shiftName}</span>;
    }
  };

  return (
    <div className="space-y-5">
      {/* Search & Date Controls */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
        <div className="flex flex-wrap items-center gap-3 w-full sm:w-auto">
          <div className="inline-flex rounded-lg border border-[#DDE7E1] bg-white p-0.5 text-xs">
            {(
              [
                { key: 'day', label: 'Theo ngày' },
                { key: 'week', label: 'Theo tuần' },
                { key: 'month', label: 'Theo tháng' },
              ] as const
            ).map((opt) => (
              <button
                key={opt.key}
                type="button"
                onClick={() => setViewMode(opt.key)}
                className={`px-2.5 py-1 rounded-md font-semibold transition-colors ${
                  viewMode === opt.key ? 'bg-[#006a41] text-white' : 'text-[#5E6F66] hover:text-[#1C2B24]'
                }`}
              >
                {opt.label}
              </button>
            ))}
          </div>

          <div className="flex items-center gap-1.5 bg-white border border-[#DDE7E1] px-3 py-1 rounded-lg">
            <span className="material-symbols-outlined text-[#5E6F66] text-[18px]">calendar_today</span>
            <input
              type="date"
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
              className="text-xs bg-transparent focus:outline-none font-semibold text-[#1C2B24]"
            />
          </div>

          {viewMode !== 'day' && activeRange && (
            <span className="text-[11px] font-medium px-2.5 py-1 rounded-md bg-[#E8F5EE] text-[#006a41] border border-[#DDE7E1]">
              {activeRange.fromDate} → {activeRange.toDate}
            </span>
          )}

          <select
            value={roomIdFilter}
            onChange={(e) => setRoomIdFilter(e.target.value)}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:ring-2 focus:ring-[#006a41]/20"
          >
            <option value="">Tất cả phòng</option>
            {rooms.map((r) => (
              <option key={r.id} value={r.id}>
                {r.roomNumber} - {r.roomName}
              </option>
            ))}
          </select>

          <select
            value={staffIdFilter}
            onChange={(e) => setStaffIdFilter(e.target.value)}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:ring-2 focus:ring-[#006a41]/20"
          >
            <option value="">Tất cả nhân viên</option>
            {staffList.map((st) => (
              <option key={st.id} value={st.id}>
                {st.fullName} ({st.staffCode})
              </option>
            ))}
          </select>
        </div>

        <button
          onClick={handleOpenCreate}
          className="h-9 px-4 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] transition-colors flex items-center gap-1.5 shadow-sm shrink-0"
        >
          <span className="material-symbols-outlined text-[18px]">add_task</span>
          <span>Phân Công Ca Trực</span>
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

      {/* Shifts Table */}
      <div className="bg-white rounded-xl border border-[#DDE7E1] shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="py-16 text-center text-xs text-[#5E6F66]">Đang tải lịch phân ca trực...</div>
        ) : shifts.length === 0 ? (
          <div className="py-16 text-center text-xs text-[#5E6F66]">
            {viewMode === 'day'
              ? `Không có ca trực nào được phân công trong ngày ${selectedDate}.`
              : `Không có ca trực nào được phân công từ ${activeRange?.fromDate ?? selectedDate} đến ${activeRange?.toDate ?? selectedDate}.`}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-[#F7FAF8] border-b border-[#DDE7E1] text-[#5E6F66] font-semibold uppercase tracking-wider">
                  <th className="py-3 px-4">Ngày Trực</th>
                  <th className="py-3 px-4">Phòng Khám / CLS</th>
                  <th className="py-3 px-4">Nhân Viên Trực</th>
                  <th className="py-3 px-4">Ca Trực</th>
                  <th className="py-3 px-4">Khung Giờ (UTC)</th>
                  <th className="py-3 px-4">Trách Nhiệm</th>
                  <th className="py-3 px-4 text-right">Thao Tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {shifts.map((s) => (
                  <tr key={s.id} className="hover:bg-[#F9FBFA] transition-colors">
                    <td className="py-3 px-4 font-semibold text-[#1C2B24]">{s.shiftDate}</td>
                    <td className="py-3 px-4">
                      <span className="font-mono font-bold text-[#006a41] mr-1.5">{s.roomNumber}</span>
                      <span className="text-[#5E6F66]">{s.roomName}</span>
                    </td>
                    <td className="py-3 px-4">
                      <div className="font-semibold text-[#1C2B24]">{s.staffName}</div>
                      <div className="text-[10px] text-[#5E6F66] font-mono">{s.staffCode}</div>
                    </td>
                    <td className="py-3 px-4">{getShiftBadge(s.shiftName)}</td>
                    <td className="py-3 px-4 text-[#5E6F66] font-mono">
                      {new Date(s.startsAtUtc).toLocaleTimeString('vi-VN', {
                        hour: '2-digit',
                        minute: '2-digit',
                        timeZone: 'UTC',
                      })}{' '}
                      -{' '}
                      {new Date(s.endsAtUtc).toLocaleTimeString('vi-VN', {
                        hour: '2-digit',
                        minute: '2-digit',
                        timeZone: 'UTC',
                      })}
                    </td>
                    <td className="py-3 px-4">
                      {s.isShiftLeader ? (
                        <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-[#FEF3C7] text-[#B45309] border border-[#FDE68A]">
                          <span className="material-symbols-outlined text-[14px]">star</span>
                          Trưởng Ca Trực
                        </span>
                      ) : (
                        <span className="text-gray-400 text-xs">Thành viên ca</span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-right">
                      <button
                        onClick={() => handleDeleteShift(s)}
                        className="h-7 px-2.5 rounded border border-[#FECACA] text-[#B91C1C] hover:bg-[#FEE2E2] text-[11px] font-semibold transition-colors"
                        title="Hủy phân công ca trực này"
                      >
                        Hủy ca
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Modal Gán Ca Trực */}
      {showModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <h3 className="text-base font-bold text-[#1C2B24]">Phân Công Ca Trực Nhân Viên</h3>
              <button onClick={() => setShowModal(false)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <form onSubmit={handleCreateShift} className="space-y-3.5">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Nhân viên y tế</label>
                <select
                  value={formStaffId}
                  onChange={(e) => setFormStaffId(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  required
                >
                  {staffList.map((st) => (
                    <option key={st.id} value={st.id}>
                      {st.fullName} ({st.staffCode} - {st.staffType})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Phòng Khám / CLS</label>
                <select
                  value={formRoomId}
                  onChange={(e) => setFormRoomId(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  required
                >
                  {rooms.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.roomNumber} - {r.roomName} ({r.roomType})
                    </option>
                  ))}
                </select>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Ngày trực</label>
                  <input
                    type="date"
                    required
                    value={formDate}
                    onChange={(e) => setFormDate(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Buổi trực</label>
                  <select
                    value={formShiftName}
                    onChange={(e) => setFormShiftName(e.target.value)}
                    className="w-full h-9 px-2.5 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  >
                    <option value="Morning">Morning (Sáng: 07:30 - 11:30)</option>
                    <option value="Afternoon">Afternoon (Chiều: 13:00 - 17:00)</option>
                    <option value="FullDay">FullDay (Cả ngày: 07:30 - 17:00)</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Giờ bắt đầu (UTC)</label>
                  <input
                    type="time"
                    required
                    value={formStartTime}
                    onChange={(e) => setFormStartTime(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Giờ kết thúc (UTC)</label>
                  <input
                    type="time"
                    required
                    value={formEndTime}
                    onChange={(e) => setFormEndTime(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  />
                </div>
              </div>

              <div className="p-3 bg-[#FFFBEB] rounded-lg border border-[#FDE68A]">
                <label className="flex items-center gap-2 cursor-pointer text-xs font-semibold text-[#92400E]">
                  <input
                    type="checkbox"
                    checked={formIsLeader}
                    onChange={(e) => setFormIsLeader(e.target.checked)}
                    className="rounded border-[#DDE7E1] text-[#006a41] focus:ring-[#006a41]"
                  />
                  <span>Chỉ định làm Người Phụ Trách Ca (Trưởng ca)</span>
                </label>
                <p className="text-[11px] text-[#92400E] mt-1">
                  Quy tắc: Mỗi phòng trong cùng một ca chỉ có tối đa 01 người phụ trách ca trực.
                </p>
              </div>

              <div className="pt-3 border-t border-[#DDE7E1] flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="h-9 px-4 rounded-lg border border-[#DDE7E1] text-xs font-semibold hover:bg-gray-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="h-9 px-5 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] disabled:opacity-50"
                >
                  {isSubmitting ? 'Đang gán...' : 'Xác Nhận Gán Ca'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
