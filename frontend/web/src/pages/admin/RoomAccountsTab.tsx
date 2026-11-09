import React, { useState, useEffect, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { RoomAccountViewDto, RoomDto } from '../../types/admin';
import {
  getRoomAccounts,
  createRoomAccount,
  getRooms,
  terminateRoomDeviceSession,
} from '../../api/mockAdminApi';

export const RoomAccountsTab: React.FC = () => {
  const [accounts, setAccounts] = useState<RoomAccountViewDto[]>([]);
  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [isModalOpen, setIsModalOpen] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<{ roomId: string; accountCode: string }>();

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [accData, roomData] = await Promise.all([getRoomAccounts(), getRooms()]);
      setAccounts(accData);
      setRooms(roomData);
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      setError(errorObj.message || 'Lỗi khi tải danh sách tài khoản phòng CLS.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Quy tắc nghiệp vụ: Mỗi phòng đúng một tài khoản; form tạo phải chặn chọn phòng đã có tài khoản!
  const availableRooms = rooms.filter((r) => !accounts.some((a) => a.roomId === r.id));

  const openCreate = () => {
    reset({
      roomId: availableRooms[0]?.id || '',
      accountCode: '',
    });
    setIsModalOpen(true);
  };

  const onSubmit = async (data: { roomId: string; accountCode: string }) => {
    try {
      await createRoomAccount(data);
      setIsModalOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      alert(errorObj.message || 'Lỗi khi tạo tài khoản phòng.');
    }
  };

  const handleTerminateSession = async (roomAccountId: string) => {
    if (!window.confirm('Bạn có chắc chắn muốn ngắt phiên thiết bị này? Máy trạm sẽ bị đăng xuất.')) {
      return;
    }
    try {
      await terminateRoomDeviceSession(roomAccountId);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      alert(errorObj.message || 'Lỗi khi ngắt phiên.');
    }
  };

  if (loading) {
    return (
      <div className="p-12 text-center bg-white rounded-xl border border-[#DDE7E1]">
        <div className="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin mb-3"></div>
        <p className="text-sm text-[#5E6F66]">Đang tải danh sách tài khoản phòng CLS...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6 bg-[#FDE8E8] text-[#D32F2F] rounded-xl border border-[#F8B4B4] flex items-center justify-between">
        <span>{error}</span>
        <button onClick={loadData} className="px-3 py-1 bg-[#D32F2F] text-white rounded text-xs font-semibold">
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
              <span className="material-symbols-outlined text-primary text-[22px]">devices</span>
              Quản lý Tài khoản Phòng CLS &amp; Phiên Thiết bị (RoomAccount &amp; Session)
            </h2>
            <p className="text-xs text-[#5E6F66] mt-0.5">
              Quy tắc bảo mật: Mỗi phòng chỉ có đúng một tài khoản. Mỗi tài khoản tối đa một phiên thiết bị hoạt động đồng thời (1 máy 1 phiên).
            </p>
          </div>
          <button
            onClick={openCreate}
            disabled={availableRooms.length === 0}
            className="h-9 px-4 rounded-lg bg-[#006a41] text-white hover:bg-[#005231] font-medium text-xs flex items-center gap-1.5 shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            title={availableRooms.length === 0 ? 'Tất cả các phòng hiện đã có tài khoản gán.' : 'Tạo tài khoản phòng mới'}
          >
            <span className="material-symbols-outlined text-[16px]">add</span>
            <span>Tạo Tài khoản Phòng</span>
          </button>
        </div>

        {/* Security Rule Note Banner */}
        <div className="p-3 mb-4 rounded-lg bg-[#E8F5EE] border border-[#DDE7E1] text-xs text-[#006a41] flex items-center gap-2">
          <span className="material-symbols-outlined text-[18px]">security</span>
          <span>
            Hệ thống tuân thủ quy tắc <strong>một máy một phiên</strong>. Khi kỹ thuật viên đăng nhập máy mới, phiên cũ sẽ được thay thế; Admin có thể chủ động ngắt phiên tại đây.
          </span>
        </div>

        {accounts.length === 0 ? (
          <div className="p-8 text-center text-[#5E6F66] border border-dashed rounded-lg">
            Chưa có tài khoản phòng CLS nào.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm border-collapse">
              <thead>
                <tr className="bg-[#F7FAF8] text-[#5E6F66] text-xs uppercase tracking-wider border-b border-[#DDE7E1]">
                  <th className="py-3 px-4 font-semibold">Tài khoản (AccountCode)</th>
                  <th className="py-3 px-4 font-semibold">Phòng gắn kết</th>
                  <th className="py-3 px-4 font-semibold">Loại hình / Khoa</th>
                  <th className="py-3 px-4 font-semibold">Phiên thiết bị duy nhất đang chạy</th>
                  <th className="py-3 px-4 font-semibold text-center">Trạng thái TK</th>
                  <th className="py-3 px-4 font-semibold text-right">Thao tác phiên</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {accounts.map((item) => {
                  const sess = item.activeSession;

                  return (
                    <tr key={item.id} className="hover:bg-[#F7FAF8] transition-colors">
                      <td className="py-3 px-4">
                        <span className="font-data-tabular font-bold text-[#006a41] text-sm">
                          {item.accountCode}
                        </span>
                      </td>
                      <td className="py-3 px-4">
                        <div className="font-semibold text-[#1C2B24]">
                          Phòng {item.roomNumber} - {item.roomName}
                        </div>
                      </td>
                      <td className="py-3 px-4">
                        <span className="text-xs text-[#5E6F66] block">{item.departmentName}</span>
                        <span className="text-[11px] text-primary font-medium">({item.roomType})</span>
                      </td>
                      <td className="py-3 px-4">
                        {sess ? (
                          <div className="flex flex-col text-xs">
                            <span className="font-data-tabular font-semibold text-emerald-800 flex items-center gap-1">
                              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
                              {sess.deviceId}
                            </span>
                            <span className="text-[#5E6F66] text-[11px]">
                              IP: {sess.deviceIpAddress} | Đăng nhập: {new Date(sess.loginAtUtc).toLocaleTimeString()}
                            </span>
                          </div>
                        ) : (
                          <span className="text-xs text-outline italic">Không có phiên hoạt động</span>
                        )}
                      </td>
                      <td className="py-3 px-4 text-center">
                        <span
                          className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                            item.isActive
                              ? 'bg-secondary-container text-on-secondary-container'
                              : 'bg-gray-100 text-gray-600'
                          }`}
                        >
                          {item.isActive ? 'Kích hoạt' : 'Khóa'}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-right">
                        {sess ? (
                          <button
                            onClick={() => handleTerminateSession(item.id)}
                            className="px-2.5 py-1 rounded bg-[#FDE8E8] text-[#D32F2F] hover:bg-red-200 text-xs font-semibold inline-flex items-center gap-1 transition-colors"
                            title="Ngắt phiên kết nối của máy trạm này"
                          >
                            <span className="material-symbols-outlined text-[15px]">power_settings_new</span>
                            Ngắt phiên
                          </button>
                        ) : (
                          <span className="text-xs text-outline">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Modal create RoomAccount */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 border border-[#DDE7E1]">
            <h3 className="text-base font-bold text-[#1C2B24] mb-3">
              Tạo Tài khoản Phòng CLS mới
            </h3>
            <p className="text-xs text-[#5E6F66] mb-4">
              Mỗi phòng chỉ được cấp duy nhất một tài khoản đăng nhập. Các phòng đã có tài khoản sẽ không hiển thị trong danh sách chọn.
            </p>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Chọn Phòng gắn kết <span className="text-red-500">*</span>
                </label>
                <select
                  {...register('roomId', { required: 'Vui lòng chọn phòng' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                >
                  {availableRooms.map((r) => (
                    <option key={r.id} value={r.id}>
                      Phòng {r.roomNumber} - {r.roomName} ({r.roomType})
                    </option>
                  ))}
                </select>
                {availableRooms.length === 0 && (
                  <p className="text-xs text-red-500 mt-1">Tất cả các phòng hiện tại đã có tài khoản gắn kết.</p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Mã tài khoản đăng nhập (AccountCode) <span className="text-red-500">*</span>
                </label>
                <input
                  {...register('accountCode', { required: 'Nhập mã tài khoản' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none font-data-tabular lowercase"
                  placeholder="VD: phong_xq02, phong_sieuam01..."
                />
                {errors.accountCode && <p className="text-xs text-red-500 mt-1">{errors.accountCode.message}</p>}
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
                  disabled={availableRooms.length === 0}
                  className="px-4 py-2 bg-[#006a41] hover:bg-[#005231] text-white rounded-lg text-sm font-semibold shadow-sm disabled:opacity-50"
                >
                  Tạo Tài khoản
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
