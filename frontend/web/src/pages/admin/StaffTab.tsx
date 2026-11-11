import React, { useEffect, useState } from 'react';
import { apiClient } from '../../api/client';
import { StaffAdminDto } from '../../types/admin';

export const StaffTab: React.FC = () => {
  const [staffList, setStaffList] = useState<StaffAdminDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [activeFilter, setActiveFilter] = useState<string>('');

  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [editTarget, setEditTarget] = useState<StaffAdminDto | null>(null);
  const [editFullName, setEditFullName] = useState('');
  const [editIsActive, setEditIsActive] = useState(true);
  const [resetTarget, setResetTarget] = useState<StaffAdminDto | null>(null);
  const [newResetPassword, setNewResetPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Form states
  const [staffCode, setStaffCode] = useState('');
  const [fullName, setFullName] = useState('');
  const [staffType, setStaffType] = useState('Doctor');
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(searchTerm.trim());
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchTerm]);

  const fetchStaff = async () => {
    setIsLoading(true);
    try {
      const params = new URLSearchParams();
      if (typeFilter) params.append('staffType', typeFilter);
      if (activeFilter !== '') params.append('isActive', activeFilter);
      if (debouncedSearch) params.append('searchTerm', debouncedSearch);
      const res = await apiClient.get<StaffAdminDto[]>(`/api/staff/admin/staff?${params.toString()}`);
      setStaffList(res.data);
    } catch {
      setErrorMsg('Không thể tải danh sách nhân sự.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchStaff();
  }, [typeFilter, activeFilter, debouncedSearch]);

  const extractApiError = (err: unknown, fallback: string) => {
    const errorObj = err as {
      response?: {
        data?: {
          detail?: string;
          title?: string;
          errors?: Record<string, string[]>;
        };
      };
    };
    const data = errorObj.response?.data;
    if (data?.detail) return data.detail;
    if (data?.errors) {
      const firstErr = Object.values(data.errors).flat()[0];
      if (firstErr) return firstErr;
    }
    if (data?.title) return data.title;
    return fallback;
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setErrorMsg(null);
    try {
      await apiClient.post('/api/staff/admin/staff', {
        staffCode: staffCode.trim(),
        fullName: fullName.trim(),
        staffType,
        userName: userName.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim(),
        password,
      });
      setSuccessMsg(`Tạo nhân viên "${fullName.trim()}" thành công kèm tài khoản AppUser!`);
      setShowCreateModal(false);
      resetForm();
      fetchStaff();
    } catch (err: unknown) {
      setErrorMsg(extractApiError(err, 'Lỗi khi tạo nhân viên.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleUpdateStaff = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editTarget || !editFullName.trim()) return;
    setIsSubmitting(true);
    setErrorMsg(null);
    try {
      await apiClient.put(`/api/staff/admin/staff/${editTarget.id}`, {
        fullName: editFullName.trim(),
        isActive: editIsActive,
      });
      setSuccessMsg(`Đã cập nhật thông tin nhân viên "${editFullName.trim()}".`);
      setEditTarget(null);
      fetchStaff();
    } catch (err: unknown) {
      setErrorMsg(extractApiError(err, 'Không thể cập nhật thông tin nhân viên.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleActive = async (staff: StaffAdminDto) => {
    const nextStatus = !staff.isActive;
    const confirmText = nextStatus
      ? `Kích hoạt lại nhân viên "${staff.fullName}" và tài khoản đăng nhập tương ứng?`
      : `Vô hiệu hóa nhân viên "${staff.fullName}"? Tài khoản AppUser tương ứng cũng sẽ bị khóa.`;

    if (!window.confirm(confirmText)) return;

    try {
      await apiClient.post(`/api/staff/admin/staff/${staff.id}/toggle-active?isActive=${nextStatus}`);
      setSuccessMsg(`Đã ${nextStatus ? 'kích hoạt' : 'vô hiệu hóa'} nhân viên ${staff.fullName}.`);
      fetchStaff();
    } catch {
      setErrorMsg('Không thể cập nhật trạng thái nhân viên.');
    }
  };

  const resetForm = () => {
    setStaffCode('');
    setFullName('');
    setStaffType('Doctor');
    setUserName('');
    setEmail('');
    setPhoneNumber('');
    setPassword('');
  };

  const filteredStaff = staffList.filter((s) => {
    const matchSearch =
      s.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      s.staffCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
      s.email.toLowerCase().includes(searchTerm.toLowerCase());
    return matchSearch;
  });

  const getStaffTypeBadge = (type: string) => {
    switch (type) {
      case 'Doctor':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-[#E8F5EE] text-[#006a41] border border-[#DDE7E1]">Bác sĩ</span>;
      case 'Nurse':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-[#E0F2FE] text-[#0369A1] border border-[#BAE6FD]">Điều dưỡng</span>;
      case 'Receptionist':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-[#FEF3C7] text-[#B45309] border border-[#FDE68A]">Tiếp tân</span>;
      case 'Admin':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-[#F3E8FF] text-[#7E22CE] border border-[#E9D5FF]">Quản trị viên</span>;
      default:
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-gray-100 text-gray-700">{type}</span>;
    }
  };

  return (
    <div className="space-y-5">
      {/* Search & Actions Bar */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
        <div className="flex flex-wrap items-center gap-3 w-full sm:w-auto">
          <div className="relative flex-1 sm:w-64">
            <span className="material-symbols-outlined absolute left-3 top-2.5 text-[#5E6F66] text-[18px]">search</span>
            <input
              type="text"
              placeholder="Tìm theo tên, mã NV, email..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full h-9 pl-9 pr-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:outline-none focus:ring-2 focus:ring-[#006a41]/20"
            />
          </div>

          <select
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:outline-none focus:ring-2 focus:ring-[#006a41]/20"
          >
            <option value="">Tất cả loại nhân viên</option>
            <option value="Doctor">Bác sĩ</option>
            <option value="Nurse">Điều dưỡng</option>
            <option value="Receptionist">Tiếp tân</option>
            <option value="Admin">Quản trị viên</option>
          </select>

          <select
            value={activeFilter}
            onChange={(e) => setActiveFilter(e.target.value)}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:outline-none focus:ring-2 focus:ring-[#006a41]/20"
          >
            <option value="">Tất cả trạng thái</option>
            <option value="true">Đang hoạt động</option>
            <option value="false">Đã vô hiệu hóa</option>
          </select>
        </div>

        <button
          onClick={() => {
            resetForm();
            setShowCreateModal(true);
          }}
          className="h-9 px-4 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] transition-colors flex items-center gap-1.5 shadow-sm shrink-0"
        >
          <span className="material-symbols-outlined text-[18px]">person_add</span>
          <span>Tạo Nhân Viên Mới</span>
        </button>
      </div>

      {successMsg && (
        <div className="p-3 rounded-lg bg-[#ECFDF5] border border-[#A7F3D0] text-xs text-[#065F46] flex items-center justify-between">
          <span>{successMsg}</span>
          <button onClick={() => setSuccessMsg(null)} className="text-[#065F46] hover:text-black">
            <span className="material-symbols-outlined text-[16px]">close</span>
          </button>
        </div>
      )}

      {errorMsg && (
        <div className="p-3 rounded-lg bg-[#FEE2E2] border border-[#FECACA] text-xs text-[#B91C1C] flex items-center justify-between">
          <span>{errorMsg}</span>
          <button onClick={() => setErrorMsg(null)} className="text-[#B91C1C] hover:text-black">
            <span className="material-symbols-outlined text-[16px]">close</span>
          </button>
        </div>
      )}

      {/* Staff Table */}
      <div className="bg-white rounded-xl border border-[#DDE7E1] shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="py-16 text-center text-xs text-[#5E6F66]">Đang tải danh sách nhân viên...</div>
        ) : filteredStaff.length === 0 ? (
          <div className="py-16 text-center text-xs text-[#5E6F66]">Không tìm thấy nhân viên nào phù hợp.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-[#F7FAF8] border-b border-[#DDE7E1] text-[#5E6F66] font-semibold uppercase tracking-wider">
                  <th className="py-3 px-4">Mã NV</th>
                  <th className="py-3 px-4">Họ và Tên</th>
                  <th className="py-3 px-4">Chức danh</th>
                  <th className="py-3 px-4">Tài khoản (AppUser)</th>
                  <th className="py-3 px-4">Email</th>
                  <th className="py-3 px-4">Số điện thoại</th>
                  <th className="py-3 px-4">Trạng thái</th>
                  <th className="py-3 px-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {filteredStaff.map((s) => (
                  <tr key={s.id} className="hover:bg-[#F9FBFA] transition-colors">
                    <td className="py-3 px-4 font-mono font-bold text-[#006a41]">{s.staffCode}</td>
                    <td className="py-3 px-4 font-semibold text-[#1C2B24]">{s.fullName}</td>
                    <td className="py-3 px-4">{getStaffTypeBadge(s.staffType)}</td>
                    <td className="py-3 px-4 font-mono text-[#5E6F66]">{s.userName}</td>
                    <td className="py-3 px-4 text-[#5E6F66]">{s.email}</td>
                    <td className="py-3 px-4 text-[#5E6F66]">{s.phoneNumber || '-'}</td>
                    <td className="py-3 px-4">
                      {s.isActive ? (
                        <span className="inline-flex items-center gap-1 text-[#047857] font-semibold">
                          <span className="w-2 h-2 rounded-full bg-[#10B981]"></span>
                          Hoạt động
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-[#9CA3AF] font-semibold">
                          <span className="w-2 h-2 rounded-full bg-gray-400"></span>
                          Vô hiệu hóa
                        </span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          onClick={() => {
                            setEditTarget(s);
                            setEditFullName(s.fullName);
                            setEditIsActive(s.isActive);
                            setErrorMsg(null);
                          }}
                          className="h-7 px-2.5 rounded text-[11px] font-semibold border border-[#DDE7E1] text-[#1C2B24] hover:bg-[#F1F4F2] transition-colors"
                          title="Chỉnh sửa thông tin nhân viên"
                        >
                          Sửa
                        </button>
                        <button
                          onClick={() => {
                            setResetTarget(s);
                            setNewResetPassword('');
                          }}
                          className="h-7 px-2.5 rounded text-[11px] font-semibold border border-[#DDE7E1] text-[#006a41] hover:bg-[#E8F5EE] transition-colors"
                          title="Đặt lại mật khẩu đăng nhập"
                        >
                          Đặt lại MK
                        </button>
                        <button
                          onClick={() => handleToggleActive(s)}
                          className={`h-7 px-2.5 rounded text-[11px] font-semibold border transition-colors ${
                            s.isActive
                              ? 'border-[#FECACA] text-[#B91C1C] hover:bg-[#FEE2E2]'
                              : 'border-[#A7F3D0] text-[#047857] hover:bg-[#ECFDF5]'
                          }`}
                        >
                          {s.isActive ? 'Khóa' : 'Kích hoạt'}
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

      {/* Modal Tạo Nhân Viên */}
      {showCreateModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-lg p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <h3 className="text-base font-bold text-[#1C2B24]">Tạo Mới Nhân Sự & Tài Khoản AppUser</h3>
              <button onClick={() => setShowCreateModal(false)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <form onSubmit={handleCreate} className="space-y-3.5">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Mã nhân viên (UK)</label>
                  <input
                    type="text"
                    required
                    placeholder="VD: DOC01"
                    value={staffCode}
                    onChange={(e) => setStaffCode(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Loại nhân sự</label>
                  <select
                    value={staffType}
                    onChange={(e) => setStaffType(e.target.value)}
                    className="w-full h-9 px-2.5 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  >
                    <option value="Doctor">Doctor (Bác sĩ)</option>
                    <option value="Nurse">Nurse (Điều dưỡng)</option>
                    <option value="Receptionist">Receptionist (Tiếp tân)</option>
                    <option value="Admin">Admin (Quản trị viên)</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Họ và tên đầy đủ</label>
                <input
                  type="text"
                  required
                  placeholder="BS. Trần Minh Quân"
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Tên đăng nhập AppUser</label>
                  <input
                    type="text"
                    required
                    placeholder="quan.tran"
                    value={userName}
                    onChange={(e) => setUserName(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Mật khẩu ban đầu</label>
                  <input
                    type="password"
                    required
                    placeholder="Tối thiểu 6 ký tự"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Email nội bộ</label>
                  <input
                    type="email"
                    required
                    placeholder="quan.tran@hospital.vn"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Số điện thoại</label>
                  <input
                    type="tel"
                    required
                    placeholder="0912345678"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  />
                </div>
              </div>

              <div className="p-3 bg-[#E8F5EE] rounded-lg border border-[#DDE7E1] text-[11px] text-[#5E6F66]">
                Tạo nhân viên sẽ tự động tạo một tài khoản <strong>AppUser</strong> tương ứng trong schema <code>identity</code> với mật khẩu băm BCrypt.
              </div>

              <div className="pt-3 border-t border-[#DDE7E1] flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="h-9 px-4 rounded-lg border border-[#DDE7E1] text-xs font-semibold hover:bg-gray-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="h-9 px-5 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] disabled:opacity-50"
                >
                  {isSubmitting ? 'Đang tạo...' : 'Lưu Nhân Viên'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Đặt Lại Mật Khẩu */}
      {resetTarget && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <div>
                <h3 className="text-base font-bold text-[#1C2B24]">Đặt Lại Mật Khẩu Nhân Sự</h3>
                <p className="text-xs text-[#5E6F66]">
                  {resetTarget.fullName} ({resetTarget.staffCode})
                </p>
              </div>
              <button onClick={() => setResetTarget(null)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <form
              onSubmit={async (e) => {
                e.preventDefault();
                setIsSubmitting(true);
                setErrorMsg(null);
                try {
                  await apiClient.post(`/api/staff/admin/staff/${resetTarget.id}/reset-password`, {
                    newPassword: newResetPassword,
                  });
                  setSuccessMsg(`Đã đặt lại mật khẩu cho "${resetTarget.fullName}".`);
                  setResetTarget(null);
                  setNewResetPassword('');
                } catch (err: unknown) {
                  const errorObj = err as { response?: { data?: { detail?: string } } };
                  setErrorMsg(errorObj.response?.data?.detail || 'Không thể đặt lại mật khẩu.');
                } finally {
                  setIsSubmitting(false);
                }
              }}
              className="space-y-4"
            >
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Mật khẩu mới</label>
                <input
                  type="password"
                  required
                  minLength={6}
                  placeholder="Tối thiểu 6 ký tự"
                  value={newResetPassword}
                  onChange={(e) => setNewResetPassword(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div className="pt-3 border-t border-[#DDE7E1] flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setResetTarget(null)}
                  className="h-9 px-4 rounded-lg border border-[#DDE7E1] text-xs font-semibold hover:bg-gray-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="h-9 px-5 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] disabled:opacity-50"
                >
                  {isSubmitting ? 'Đang lưu...' : 'Xác Nhận Đặt Lại'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Chỉnh Sửa Nhân Viên */}
      {editTarget && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <div>
                <h3 className="text-base font-bold text-[#1C2B24]">Cập Nhật Thông Tin Nhân Sự</h3>
                <p className="text-xs text-[#5E6F66]">
                  {editTarget.staffCode} • {editTarget.userName} ({editTarget.staffType})
                </p>
              </div>
              <button onClick={() => setEditTarget(null)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <form onSubmit={handleUpdateStaff} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Họ và tên đầy đủ</label>
                <input
                  type="text"
                  required
                  value={editFullName}
                  onChange={(e) => setEditFullName(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div className="p-3 bg-[#F7FAF8] rounded-lg border border-[#DDE7E1]">
                <label className="flex items-center gap-2 cursor-pointer text-xs font-semibold text-[#1C2B24]">
                  <input
                    type="checkbox"
                    checked={editIsActive}
                    onChange={(e) => setEditIsActive(e.target.checked)}
                    className="rounded border-[#DDE7E1] text-[#006a41] focus:ring-[#006a41]"
                  />
                  <span>Tài khoản đang hoạt động (IsActive)</span>
                </label>
                <p className="text-[11px] text-[#5E6F66] mt-1">
                  Bỏ chọn sẽ vô hiệu hóa nhân viên và khóa tài khoản AppUser tương ứng.
                </p>
              </div>

              <div className="pt-3 border-t border-[#DDE7E1] flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setEditTarget(null)}
                  className="h-9 px-4 rounded-lg border border-[#DDE7E1] text-xs font-semibold hover:bg-gray-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="h-9 px-5 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] disabled:opacity-50"
                >
                  {isSubmitting ? 'Đang lưu...' : 'Lưu Thay Đổi'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
