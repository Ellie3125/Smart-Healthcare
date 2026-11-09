import React, { useState, useEffect, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { RoomDto, RoomType } from '../../types/admin';
import { getRooms, createRoom, updateRoom } from '../../api/mockAdminApi';

export const RoomsTab: React.FC = () => {
  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingRoom, setEditingRoom] = useState<RoomDto | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<Omit<RoomDto, 'id'>>();

  const loadRooms = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getRooms();
      setRooms(data);
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      setError(errorObj.message || 'Lỗi khi tải danh sách phòng.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadRooms();
  }, [loadRooms]);

  const openCreate = () => {
    setEditingRoom(null);
    reset({
      roomNumber: '',
      roomName: '',
      roomType: 'Clinical',
      departmentName: 'Khoa Khám Bệnh',
      isActive: true,
    });
    setIsModalOpen(true);
  };

  const openEdit = (room: RoomDto) => {
    setEditingRoom(room);
    reset({
      roomNumber: room.roomNumber,
      roomName: room.roomName,
      roomType: room.roomType,
      departmentName: room.departmentName,
      isActive: room.isActive,
    });
    setIsModalOpen(true);
  };

  const onSubmit = async (data: Omit<RoomDto, 'id'>) => {
    try {
      if (editingRoom) {
        await updateRoom(editingRoom.id, data);
      } else {
        await createRoom(data);
      }
      setIsModalOpen(false);
      await loadRooms();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      alert(errorObj.message || 'Lỗi khi lưu phòng.');
    }
  };

  const getRoomTypeBadge = (type: RoomType) => {
    switch (type) {
      case 'Clinical':
        return <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-700">Khám Lâm sàng</span>;
      case 'XRay':
        return <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-purple-50 text-purple-700">Chụp X-Quang</span>;
      case 'Ultrasound':
        return <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700">Siêu âm CLS</span>;
      case 'Laboratory':
        return <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-amber-50 text-amber-700">Xét nghiệm</span>;
      case 'Endoscopy':
        return <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-rose-50 text-rose-700">Nội soi</span>;
    }
  };

  if (loading) {
    return (
      <div className="p-12 text-center bg-white rounded-xl border border-[#DDE7E1]">
        <div className="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin mb-3"></div>
        <p className="text-sm text-[#5E6F66]">Đang tải danh sách phòng khám &amp; cận lâm sàng...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6 bg-[#FDE8E8] text-[#D32F2F] rounded-xl border border-[#F8B4B4] flex items-center justify-between">
        <span>{error}</span>
        <button onClick={loadRooms} className="px-3 py-1 bg-[#D32F2F] text-white rounded text-xs font-semibold">
          Thử lại
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 mb-4 border-b border-[#DDE7E1] gap-3">
          <div>
            <h2 className="text-lg font-bold text-[#1C2B24] flex items-center gap-2">
              <span className="material-symbols-outlined text-primary text-[22px]">meeting_room</span>
              Quản lý Phòng Khám &amp; Phòng Cận Lâm Sàng (Room)
            </h2>
            <p className="text-xs text-[#5E6F66] mt-0.5">
              Cấu hình vị trí phòng, loại hình dịch vụ (Lâm sàng, Xét nghiệm, X-Quang, Siêu âm).
            </p>
          </div>
          <button
            onClick={openCreate}
            className="h-9 px-4 rounded-lg bg-[#006a41] text-white hover:bg-[#005231] font-medium text-xs flex items-center gap-1.5 shadow-sm transition-colors"
          >
            <span className="material-symbols-outlined text-[16px]">add</span>
            <span>Thêm Phòng mới</span>
          </button>
        </div>

        {rooms.length === 0 ? (
          <div className="p-8 text-center text-[#5E6F66] border border-dashed rounded-lg">
            Chưa có phòng nào được tạo.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm border-collapse">
              <thead>
                <tr className="bg-[#F7FAF8] text-[#5E6F66] text-xs uppercase tracking-wider border-b border-[#DDE7E1]">
                  <th className="py-3 px-4 font-semibold">Số phòng</th>
                  <th className="py-3 px-4 font-semibold">Tên phòng</th>
                  <th className="py-3 px-4 font-semibold">Phân loại phòng</th>
                  <th className="py-3 px-4 font-semibold">Khoa / Bộ phận</th>
                  <th className="py-3 px-4 font-semibold text-center">Trạng thái</th>
                  <th className="py-3 px-4 font-semibold text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {rooms.map((item) => (
                  <tr key={item.id} className="hover:bg-[#F7FAF8] transition-colors">
                    <td className="py-3 px-4 font-data-tabular font-bold text-[#006a41] text-base">
                      {item.roomNumber}
                    </td>
                    <td className="py-3 px-4 font-medium text-[#1C2B24]">{item.roomName}</td>
                    <td className="py-3 px-4">{getRoomTypeBadge(item.roomType)}</td>
                    <td className="py-3 px-4 text-[#5E6F66] text-xs font-medium">{item.departmentName}</td>
                    <td className="py-3 px-4 text-center">
                      <span
                        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                          item.isActive
                            ? 'bg-secondary-container text-on-secondary-container'
                            : 'bg-gray-100 text-gray-600'
                        }`}
                      >
                        {item.isActive ? 'Sẵn sàng' : 'Tạm khóa'}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-right">
                      <button
                        onClick={() => openEdit(item)}
                        className="text-primary hover:underline text-xs font-semibold inline-flex items-center gap-1"
                      >
                        <span className="material-symbols-outlined text-[15px]">edit</span>
                        Sửa
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Modal Room */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 border border-[#DDE7E1]">
            <h3 className="text-base font-bold text-[#1C2B24] mb-3">
              {editingRoom ? 'Cập nhật thông tin Phòng' : 'Thêm Phòng mới'}
            </h3>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                    Số phòng <span className="text-red-500">*</span>
                  </label>
                  <input
                    {...register('roomNumber', { required: 'Nhập số phòng' })}
                    className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none font-data-tabular"
                    placeholder="VD: 101, 201..."
                  />
                  {errors.roomNumber && <p className="text-xs text-red-500 mt-1">{errors.roomNumber.message}</p>}
                </div>

                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                    Loại phòng (RoomType) <span className="text-red-500">*</span>
                  </label>
                  <select
                    {...register('roomType', { required: 'Chọn loại phòng' })}
                    className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  >
                    <option value="Clinical">Clinical (Phòng khám Lâm sàng)</option>
                    <option value="XRay">XRay (Chụp X-Quang)</option>
                    <option value="Ultrasound">Ultrasound (Siêu âm)</option>
                    <option value="Laboratory">Laboratory (Xét nghiệm)</option>
                    <option value="Endoscopy">Endoscopy (Nội soi)</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Tên phòng khám / Dịch vụ <span className="text-red-500">*</span>
                </label>
                <input
                  {...register('roomName', { required: 'Nhập tên phòng' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  placeholder="VD: Phòng khám Nội 01, Phòng X-Quang KTS 01..."
                />
                {errors.roomName && <p className="text-xs text-red-500 mt-1">{errors.roomName.message}</p>}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Khoa / Bộ phận quản lý <span className="text-red-500">*</span>
                </label>
                <input
                  {...register('departmentName', { required: 'Nhập tên khoa' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  placeholder="VD: Khoa Khám Bệnh, Khoa Cận Lâm Sàng..."
                />
              </div>

              <div className="flex items-center gap-2">
                <input
                  {...register('isActive')}
                  type="checkbox"
                  id="roomIsActive"
                  className="w-4 h-4 rounded text-primary accent-primary"
                />
                <label htmlFor="roomIsActive" className="text-sm text-[#1C2B24] cursor-pointer">
                  Kích hoạt phòng hoạt động
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
                  Lưu Phòng
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
