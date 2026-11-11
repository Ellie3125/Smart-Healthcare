import React, { useEffect, useState } from 'react';
import { apiClient } from '../../api/client';
import { SpecialtyAdminDto } from '../../types/admin';

export const SpecialtiesTab: React.FC = () => {
  const [specialties, setSpecialties] = useState<SpecialtyAdminDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const [showModal, setShowModal] = useState(false);
  const [editingSpecialty, setEditingSpecialty] = useState<SpecialtyAdminDto | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Form states
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  const fetchSpecialties = async () => {
    setIsLoading(true);
    try {
      const res = await apiClient.get<SpecialtyAdminDto[]>('/api/staff/admin/specialties');
      setSpecialties(res.data);
    } catch {
      setErrorMsg('Không thể tải danh sách chuyên khoa.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchSpecialties();
  }, []);

  const handleOpenCreate = () => {
    setEditingSpecialty(null);
    setCode('');
    setName('');
    setDescription('');
    setShowModal(true);
  };

  const handleOpenEdit = (s: SpecialtyAdminDto) => {
    setEditingSpecialty(s);
    setCode(s.code);
    setName(s.name);
    setDescription(s.description || '');
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setErrorMsg(null);

    try {
      if (editingSpecialty) {
        await apiClient.put(`/api/staff/admin/specialties/${editingSpecialty.id}`, {
          name: name.trim(),
          description: description.trim() || undefined,
          isActive: editingSpecialty.isActive,
        });
        setSuccessMsg(`Cập nhật chuyên khoa "${name}" thành công!`);
      } else {
        await apiClient.post('/api/staff/admin/specialties', {
          code: code.trim(),
          name: name.trim(),
          description: description.trim() || undefined,
        });
        setSuccessMsg(`Tạo chuyên khoa "${name}" (${code.trim()}) thành công!`);
      }
      setShowModal(false);
      fetchSpecialties();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(errorObj.response?.data?.detail || 'Thao tác không thành công.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleActive = async (s: SpecialtyAdminDto) => {
    const nextStatus = !s.isActive;
    const confirmPrompt = nextStatus
      ? `Kích hoạt lại chuyên khoa "${s.name}"?`
      : `Vô hiệu hóa chuyên khoa "${s.name}"? Cảnh báo: Các bác sĩ đang hoạt động thuộc chuyên khoa này sẽ không được gợi ý khi đặt lịch.`;

    if (!window.confirm(confirmPrompt)) return;

    try {
      await apiClient.put(`/api/staff/admin/specialties/${s.id}`, {
        name: s.name,
        description: s.description,
        isActive: nextStatus,
      });
      setSuccessMsg(`Đã ${nextStatus ? 'kích hoạt' : 'vô hiệu hóa'} chuyên khoa ${s.name}.`);
      fetchSpecialties();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(errorObj.response?.data?.detail || 'Không thể đổi trạng thái chuyên khoa.');
    }
  };

  return (
    <div className="space-y-5">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-sm font-bold text-[#1C2B24]">Danh Mục Chuyên Khoa Y Tế</h2>
          <p className="text-xs text-[#5E6F66]">
            Mã chuyên khoa (SpecialtyCode) là định danh cố định phục vụ điều hướng khám & AI chatbot gợi ý khoa khám.
          </p>
        </div>
        <button
          onClick={handleOpenCreate}
          className="h-9 px-4 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] transition-colors flex items-center gap-1.5 shadow-sm"
        >
          <span className="material-symbols-outlined text-[18px]">add</span>
          <span>Tạo Chuyên Khoa Mới</span>
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
          <div className="py-16 text-center text-xs text-[#5E6F66]">Đang tải danh sách chuyên khoa...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-[#F7FAF8] border-b border-[#DDE7E1] text-[#5E6F66] font-semibold uppercase tracking-wider">
                  <th className="py-3 px-4">Mã Chuyên Khoa</th>
                  <th className="py-3 px-4">Tên Chuyên Khoa</th>
                  <th className="py-3 px-4">Mô tả / Phạm vi chẩn đoán</th>
                  <th className="py-3 px-4">Trạng thái</th>
                  <th className="py-3 px-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {specialties.map((s) => (
                  <tr key={s.id} className="hover:bg-[#F9FBFA] transition-colors">
                    <td className="py-3 px-4 font-mono font-bold text-[#006a41]">{s.code}</td>
                    <td className="py-3 px-4 font-semibold text-[#1C2B24]">{s.name}</td>
                    <td className="py-3 px-4 text-[#5E6F66] max-w-sm">{s.description || '-'}</td>
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
                          onClick={() => handleOpenEdit(s)}
                          className="h-7 px-2.5 rounded border border-[#DDE7E1] hover:bg-gray-50 text-[11px] font-semibold text-[#1C2B24]"
                        >
                          Sửa
                        </button>
                        <button
                          onClick={() => handleToggleActive(s)}
                          className={`h-7 px-2.5 rounded text-[11px] font-semibold border transition-colors ${
                            s.isActive
                              ? 'border-[#FECACA] text-[#B91C1C] hover:bg-[#FEE2E2]'
                              : 'border-[#A7F3D0] text-[#047857] hover:bg-[#ECFDF5]'
                          }`}
                        >
                          {s.isActive ? 'Khóa' : 'Bật'}
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

      {/* Modal Tạo/Sửa Chuyên Khoa */}
      {showModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <h3 className="text-base font-bold text-[#1C2B24]">
                {editingSpecialty ? `Chỉnh Sửa Chuyên Khoa` : 'Tạo Mới Chuyên Khoa'}
              </h3>
              <button onClick={() => setShowModal(false)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-3.5">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Mã Chuyên Khoa (SpecialtyCode) <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  disabled={!!editingSpecialty}
                  placeholder="VD: CAR, DER, INT..."
                  value={code}
                  onChange={(e) => setCode(e.target.value.toUpperCase())}
                  className={`w-full h-9 px-3 rounded-lg border text-xs font-mono uppercase ${
                    editingSpecialty
                      ? 'bg-gray-100 text-gray-500 cursor-not-allowed border-gray-200'
                      : 'bg-white border-[#DDE7E1] focus:ring-2 focus:ring-[#006a41]/20'
                  }`}
                />
                {editingSpecialty ? (
                  <p className="text-[11px] text-amber-600 mt-1 flex items-center gap-1 font-medium">
                    <span className="material-symbols-outlined text-[14px]">lock</span>
                    Mã cố định vì chatbot tham chiếu gợi ý chuyên khoa.
                  </p>
                ) : (
                  <p className="text-[11px] text-[#5E6F66] mt-1">
                    Lưu ý: Mã chuyên khoa không thể thay đổi sau khi tạo.
                  </p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Tên Chuyên Khoa <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="Khoa Tim Mạch, Da Liễu..."
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Mô tả chi tiết</label>
                <textarea
                  rows={3}
                  placeholder="Phạm vi tiếp nhận, triệu chứng khám chữa bệnh..."
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  className="w-full p-2.5 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                ></textarea>
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
                  {isSubmitting ? 'Đang lưu...' : 'Lưu Chuyên Khoa'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
