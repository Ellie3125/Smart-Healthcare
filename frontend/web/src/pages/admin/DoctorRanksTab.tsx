import React, { useEffect, useState } from 'react';
import { apiClient } from '../../api/client';
import { DoctorRankAdminDto } from '../../types/admin';

export const DoctorRanksTab: React.FC = () => {
  const [ranks, setRanks] = useState<DoctorRankAdminDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const [showModal, setShowModal] = useState(false);
  const [editingRank, setEditingRank] = useState<DoctorRankAdminDto | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Form states
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [displayOrder, setDisplayOrder] = useState(1);

  const fetchRanks = async () => {
    setIsLoading(true);
    try {
      const res = await apiClient.get<DoctorRankAdminDto[]>('/api/staff/admin/doctor-ranks');
      setRanks(res.data.sort((a, b) => a.displayOrder - b.displayOrder));
    } catch {
      setErrorMsg('Không thể tải danh sách cấp bậc.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchRanks();
  }, []);

  const handleOpenCreate = () => {
    setEditingRank(null);
    setCode('');
    setName('');
    setDisplayOrder(ranks.length + 1);
    setShowModal(true);
  };

  const handleOpenEdit = (r: DoctorRankAdminDto) => {
    setEditingRank(r);
    setCode(r.code);
    setName(r.name);
    setDisplayOrder(r.displayOrder);
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setErrorMsg(null);

    try {
      if (editingRank) {
        await apiClient.put(`/api/staff/admin/doctor-ranks/${editingRank.id}`, {
          name: name.trim(),
          displayOrder,
          isActive: editingRank.isActive,
        });
        setSuccessMsg(`Cập nhật cấp bậc "${name}" thành công!`);
      } else {
        await apiClient.post('/api/staff/admin/doctor-ranks', {
          code: code.trim(),
          name: name.trim(),
          displayOrder,
        });
        setSuccessMsg(`Tạo cấp bậc "${name}" (${code.trim()}) thành công!`);
      }
      setShowModal(false);
      fetchRanks();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(errorObj.response?.data?.detail || 'Thao tác không thành công.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleActive = async (r: DoctorRankAdminDto) => {
    const nextStatus = !r.isActive;
    if (!window.confirm(`${nextStatus ? 'Kích hoạt' : 'Vô hiệu hóa'} cấp bậc "${r.name}"?`)) return;

    try {
      await apiClient.put(`/api/staff/admin/doctor-ranks/${r.id}`, {
        name: r.name,
        displayOrder: r.displayOrder,
        isActive: nextStatus,
      });
      setSuccessMsg(`Đã ${nextStatus ? 'kích hoạt' : 'vô hiệu hóa'} cấp bậc ${r.name}.`);
      fetchRanks();
    } catch {
      setErrorMsg('Không thể đổi trạng thái cấp bậc.');
    }
  };

  return (
    <div className="space-y-5">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-sm font-bold text-[#1C2B24]">Danh Mục Cấp Bậc / Học Hàm Bác Sĩ</h2>
          <p className="text-xs text-[#5E6F66]">
            Mã cấp bậc (RankCode) được module Billing dùng để tính đơn giá khám bệnh ngoại trú.
          </p>
        </div>
        <button
          onClick={handleOpenCreate}
          className="h-9 px-4 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] transition-colors flex items-center gap-1.5 shadow-sm"
        >
          <span className="material-symbols-outlined text-[18px]">add</span>
          <span>Tạo Cấp Bậc Mới</span>
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

      <div className="bg-white rounded-xl border border-[#DDE7E1] shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="py-16 text-center text-xs text-[#5E6F66]">Đang tải danh sách cấp bậc...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-[#F7FAF8] border-b border-[#DDE7E1] text-[#5E6F66] font-semibold uppercase tracking-wider">
                  <th className="py-3 px-4">Thứ tự</th>
                  <th className="py-3 px-4">Mã Cấp Bậc</th>
                  <th className="py-3 px-4">Tên Cấp Bậc / Học Vị</th>
                  <th className="py-3 px-4">Trạng thái</th>
                  <th className="py-3 px-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {ranks.map((r) => (
                  <tr key={r.id} className="hover:bg-[#F9FBFA] transition-colors">
                    <td className="py-3 px-4 font-mono font-bold text-[#5E6F66]">{r.displayOrder}</td>
                    <td className="py-3 px-4 font-mono font-bold text-[#006a41]">{r.code}</td>
                    <td className="py-3 px-4 font-semibold text-[#1C2B24]">{r.name}</td>
                    <td className="py-3 px-4">
                      {r.isActive ? (
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
                          onClick={() => handleOpenEdit(r)}
                          className="h-7 px-2.5 rounded border border-[#DDE7E1] hover:bg-gray-50 text-[11px] font-semibold text-[#1C2B24]"
                        >
                          Sửa
                        </button>
                        <button
                          onClick={() => handleToggleActive(r)}
                          className={`h-7 px-2.5 rounded text-[11px] font-semibold border transition-colors ${
                            r.isActive
                              ? 'border-[#FECACA] text-[#B91C1C] hover:bg-[#FEE2E2]'
                              : 'border-[#A7F3D0] text-[#047857] hover:bg-[#ECFDF5]'
                          }`}
                        >
                          {r.isActive ? 'Khóa' : 'Bật'}
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

      {showModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <h3 className="text-base font-bold text-[#1C2B24]">
                {editingRank ? 'Chỉnh Sửa Cấp Bậc Bác Sĩ' : 'Tạo Mới Cấp Bậc Bác Sĩ'}
              </h3>
              <button onClick={() => setShowModal(false)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-3.5">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Mã Cấp Bậc (RankCode) <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  disabled={!!editingRank}
                  placeholder="SpecialistI, Master, Doctor, Professor..."
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  className={`w-full h-9 px-3 rounded-lg border text-xs font-mono ${
                    editingRank
                      ? 'bg-gray-100 text-gray-500 cursor-not-allowed border-gray-200'
                      : 'bg-white border-[#DDE7E1] focus:ring-2 focus:ring-[#006a41]/20'
                  }`}
                />
                {editingRank ? (
                  <p className="text-[11px] text-amber-600 mt-1 flex items-center gap-1 font-medium">
                    <span className="material-symbols-outlined text-[14px]">lock</span>
                    Mã cố định vì module Billing tham chiếu tính phí.
                  </p>
                ) : (
                  <p className="text-[11px] text-[#5E6F66] mt-1">
                    Ví dụ: SpecialistI, SpecialistII, Master, Doctor, AssociateProf, Professor.
                  </p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Tên Cấp Bậc / Học Vị <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="Bác sĩ Chuyên khoa I, Thạc sĩ, Giáo sư..."
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Thứ tự hiển thị (Ưu tiên)
                </label>
                <input
                  type="number"
                  required
                  min={1}
                  value={displayOrder}
                  onChange={(e) => setDisplayOrder(parseInt(e.target.value) || 1)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
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
                  {isSubmitting ? 'Đang lưu...' : 'Lưu Cấp Bậc'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
