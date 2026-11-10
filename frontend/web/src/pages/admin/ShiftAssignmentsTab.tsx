import React, { useState, useEffect, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import {
  StaffShiftAssignmentViewDto,
  StaffDto,
  RoomDto,
  ShiftName,
} from '../../types/admin';
import {
  getShiftAssignments,
  createShiftAssignment,
  deleteShiftAssignment,
  getStaffList,
  getRooms,
} from '../../api/mockAdminApi';

export const ShiftAssignmentsTab: React.FC = () => {
  const [selectedDate, setSelectedDate] = useState<string>(
    new Date().toISOString().substring(0, 10)
  );
  const [shifts, setShifts] = useState<StaffShiftAssignmentViewDto[]>([]);
  const [staffs, setStaffs] = useState<StaffDto[]>([]);
  const [rooms, setRooms] = useState<RoomDto[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [isModalOpen, setIsModalOpen] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
  } = useForm<{
    staffId: string;
    roomId: string;
    shiftDate: string;
    shiftName: ShiftName;
    isShiftLeader: boolean;
  }>();

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [shiftData, staffData, roomData] = await Promise.all([
        getShiftAssignments(selectedDate),
        getStaffList(),
        getRooms(),
      ]);
      setShifts(shiftData);
      setStaffs(staffData);
      setRooms(roomData);
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      setError(errorObj.message || 'Lỗi khi tải dữ liệu phân ca trực.');
    } finally {
      setLoading(false);
    }
  }, [selectedDate]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const openCreate = () => {
    reset({
      staffId: staffs[0]?.id || '',
      roomId: rooms[0]?.id || '',
      shiftDate: selectedDate,
      shiftName: 'Morning',
      isShiftLeader: false,
    });
    setIsModalOpen(true);
  };

  const onSubmit = async (data: {
    staffId: string;
    roomId: string;
    shiftDate: string;
    shiftName: ShiftName;
    isShiftLeader: boolean;
  }) => {
    try {
      // Calculate start and end UTC times based on shift
      const dateStr = data.shiftDate;
      let startH = '06:30:00Z';
      let endH = '12:00:00Z';
      if (data.shiftName === 'Afternoon') {
        startH = '12:30:00Z';
        endH = '17:30:00Z';
      } else if (data.shiftName === 'FullDay') {
        startH = '06:30:00Z';
        endH = '17:30:00Z';
      }

      await createShiftAssignment({
        staffId: data.staffId,
        roomId: data.roomId,
        shiftDate: data.shiftDate,
        shiftName: data.shiftName,
        startsAtUtc: `${dateStr}T${startH}`,
        endsAtUtc: `${dateStr}T${endH}`,
        isShiftLeader: data.isShiftLeader,
      });

      setIsModalOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      alert(errorObj.message || 'Lỗi khi phân ca trực.');
    }
  };

  const handleDelete = async (id: string, staffName: string) => {
    if (!window.confirm(`Bạn có chắc muốn hủy ca trực của nhân viên "${staffName}"?`)) {
      return;
    }
    try {
      await deleteShiftAssignment(id);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      alert(errorObj.message || 'Lỗi khi hủy ca.');
    }
  };

  const getShiftBadge = (shift: ShiftName) => {
    switch (shift) {
      case 'Morning':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-50 text-amber-800 border border-amber-200">Ca Sáng (06:30 - 12:00)</span>;
      case 'Afternoon':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-blue-50 text-blue-800 border border-blue-200">Ca Chiều (12:30 - 17:30)</span>;
      case 'FullDay':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-800 border border-emerald-200">Cả Ngày (06:30 - 17:30)</span>;
    }
  };

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6 shadow-sm">
        {/* Header & Filter */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 mb-4 border-b border-[#DDE7E1] gap-3">
          <div>
            <h2 className="text-lg font-bold text-[#1C2B24] flex items-center gap-2">
              <span className="material-symbols-outlined text-primary text-[22px]">calendar_today</span>
              Phân Ca Trực Nhân Sự &amp; Bác Sĩ (StaffShiftAssignment)
            </h2>
            <p className="text-xs text-[#5E6F66] mt-0.5">
              Phân công nhân viên y tế phụ trách phòng khám, phòng CLS theo từng ngày và buổi trực.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2 bg-[#F7FAF8] px-3 py-1.5 rounded-lg border border-[#DDE7E1]">
              <span className="material-symbols-outlined text-[18px] text-[#5E6F66]">event</span>
              <input
                type="date"
                value={selectedDate}
                onChange={(e) => setSelectedDate(e.target.value)}
                className="bg-transparent text-sm font-semibold text-[#1C2B24] outline-none cursor-pointer"
              />
            </div>

            <button
              onClick={openCreate}
              className="h-9 px-4 rounded-lg bg-[#006a41] text-white hover:bg-[#005231] font-medium text-xs flex items-center gap-1.5 shadow-sm transition-colors"
            >
              <span className="material-symbols-outlined text-[16px]">add</span>
              <span>Phân Ca Mới</span>
            </button>
          </div>
        </div>

        {loading ? (
          <div className="p-8 text-center text-[#5E6F66]">Đang tải lịch trực...</div>
        ) : error ? (
          <div className="p-4 bg-[#FDE8E8] text-[#D32F2F] rounded-lg text-sm">{error}</div>
        ) : shifts.length === 0 ? (
          <div className="p-12 text-center text-[#5E6F66] border border-dashed rounded-lg">
            <span className="material-symbols-outlined text-[40px] text-outline mb-1">event_busy</span>
            <div className="font-semibold text-sm text-[#1C2B24]">Chưa có phân ca cho ngày {selectedDate}</div>
            <p className="text-xs mt-0.5">Nhấp vào "Phân Ca Mới" để thiết lập lịch trực cho nhân sự.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm border-collapse">
              <thead>
                <tr className="bg-[#F7FAF8] text-[#5E6F66] text-xs uppercase tracking-wider border-b border-[#DDE7E1]">
                  <th className="py-3 px-4 font-semibold">Nhân viên trực</th>
                  <th className="py-3 px-4 font-semibold">Vai trò</th>
                  <th className="py-3 px-4 font-semibold">Phòng được phân công</th>
                  <th className="py-3 px-4 font-semibold">Ca trực</th>
                  <th className="py-3 px-4 font-semibold text-center">Trưởng ca</th>
                  <th className="py-3 px-4 font-semibold text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {shifts.map((item) => (
                  <tr key={item.id} className="hover:bg-[#F7FAF8] transition-colors">
                    <td className="py-3 px-4">
                      <div className="font-semibold text-[#1C2B24]">{item.staffName}</div>
                      <div className="text-xs text-[#5E6F66] font-data-tabular">Mã NV: {item.staffCode}</div>
                    </td>
                    <td className="py-3 px-4">
                      <span className="text-xs font-medium text-[#1C2B24]">{item.staffType}</span>
                    </td>
                    <td className="py-3 px-4">
                      <span className="font-semibold text-[#006a41]">
                        Phòng {item.roomNumber}
                      </span>
                      <span className="text-xs text-[#5E6F66] block">({item.roomName})</span>
                    </td>
                    <td className="py-3 px-4">{getShiftBadge(item.shiftName)}</td>
                    <td className="py-3 px-4 text-center">
                      {item.isShiftLeader ? (
                        <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-100 text-amber-900 border border-amber-300">
                          <span className="material-symbols-outlined text-[13px] text-amber-700">star</span>
                          Trưởng ca
                        </span>
                      ) : (
                        <span className="text-xs text-outline">—</span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-right">
                      <button
                        onClick={() => handleDelete(item.id, item.staffName)}
                        className="text-[#D32F2F] hover:underline text-xs font-semibold inline-flex items-center gap-0.5"
                      >
                        <span className="material-symbols-outlined text-[15px]">delete</span>
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

      {/* Modal create Shift */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 border border-[#DDE7E1]">
            <h3 className="text-base font-bold text-[#1C2B24] mb-3">
              Phân Ca Trực Mới
            </h3>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Ngày trực <span className="text-red-500">*</span>
                </label>
                <input
                  {...register('shiftDate', { required: 'Chọn ngày trực' })}
                  type="date"
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Nhân viên trực <span className="text-red-500">*</span>
                </label>
                <select
                  {...register('staffId', { required: 'Chọn nhân viên' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                >
                  {staffs.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.staffCode} - {s.fullName} ({s.staffType})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Phòng trực <span className="text-red-500">*</span>
                </label>
                <select
                  {...register('roomId', { required: 'Chọn phòng' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                >
                  {rooms.map((r) => (
                    <option key={r.id} value={r.id}>
                      Phòng {r.roomNumber} - {r.roomName} ({r.roomType})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Ca trực (ShiftName) <span className="text-red-500">*</span>
                </label>
                <select
                  {...register('shiftName', { required: 'Chọn ca trực' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                >
                  <option value="Morning">Morning (Ca Sáng: 06:30 - 12:00)</option>
                  <option value="Afternoon">Afternoon (Ca Chiều: 12:30 - 17:30)</option>
                  <option value="FullDay">FullDay (Cả Ngày: 06:30 - 17:30)</option>
                </select>
              </div>

              <div className="flex items-center gap-2">
                <input
                  {...register('isShiftLeader')}
                  type="checkbox"
                  id="isShiftLeader"
                  className="w-4 h-4 rounded text-primary accent-primary"
                />
                <label htmlFor="isShiftLeader" className="text-sm text-[#1C2B24] cursor-pointer font-medium">
                  Phụ trách Trưởng ca trực phòng này
                </label>
              </div>

              <div className="pt-3 flex items-center justify-end gap-2 border-t border-[#DDE7E1]">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-[#5E6F66] hover:bg-gray-100"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-[#006a41] hover:bg-[#005231] text-white rounded-lg text-sm font-semibold shadow-sm"
                >
                  Lưu Phân ca
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
