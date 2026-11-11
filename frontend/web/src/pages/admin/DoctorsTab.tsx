import React, { useEffect, useState } from 'react';
import { apiClient } from '../../api/client';
import { DoctorAdminDto, SpecialtyAdminDto, DoctorRankAdminDto, StaffAdminDto } from '../../types/admin';

export const DoctorsTab: React.FC = () => {
  const [doctors, setDoctors] = useState<DoctorAdminDto[]>([]);
  const [specialties, setSpecialties] = useState<SpecialtyAdminDto[]>([]);
  const [ranks, setRanks] = useState<DoctorRankAdminDto[]>([]);
  const [availableStaff, setAvailableStaff] = useState<StaffAdminDto[]>([]);

  const [isLoading, setIsLoading] = useState(true);
  const [specialtyFilter, setSpecialtyFilter] = useState('');
  const [rankFilter, setRankFilter] = useState('');
  const [activeFilter, setActiveFilter] = useState('');
  const [searchTerm, setSearchTerm] = useState('');

  const [showModal, setShowModal] = useState(false);
  const [editingDoctor, setEditingDoctor] = useState<DoctorAdminDto | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Form states
  const [staffId, setStaffId] = useState('');
  const [specialtyId, setSpecialtyId] = useState('');
  const [doctorRankId, setDoctorRankId] = useState('');
  const [biography, setBiography] = useState('');

  const loadData = async () => {
    setIsLoading(true);
    try {
      const [specRes, rankRes, staffRes] = await Promise.all([
        apiClient.get<SpecialtyAdminDto[]>('/api/staff/admin/specialties'),
        apiClient.get<DoctorRankAdminDto[]>('/api/staff/admin/doctor-ranks'),
        apiClient.get<StaffAdminDto[]>('/api/staff/admin/staff?staffType=Doctor&isActive=true'),
      ]);
      setSpecialties(specRes.data);
      setRanks(rankRes.data);
      setAvailableStaff(staffRes.data);

      const params = new URLSearchParams();
      if (specialtyFilter) params.append('specialtyId', specialtyFilter);
      if (rankFilter) params.append('rankId', rankFilter);
      if (activeFilter !== '') params.append('isActive', activeFilter);

      const docRes = await apiClient.get<DoctorAdminDto[]>(`/api/staff/admin/doctors?${params.toString()}`);
      setDoctors(docRes.data);
    } catch {
      setErrorMsg('Không thể tải dữ liệu bác sĩ.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [specialtyFilter, rankFilter, activeFilter]);

  const handleOpenCreate = () => {
    setEditingDoctor(null);
    setStaffId(availableStaff[0]?.id || '');
    setSpecialtyId(specialties[0]?.id || '');
    setDoctorRankId(ranks[0]?.id || '');
    setBiography('');
    setShowModal(true);
  };

  const handleOpenEdit = (doc: DoctorAdminDto) => {
    setEditingDoctor(doc);
    setStaffId(doc.staffId);
    setSpecialtyId(doc.specialtyId);
    setDoctorRankId(doc.doctorRankId);
    setBiography(doc.biography || '');
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setErrorMsg(null);

    try {
      if (editingDoctor) {
        await apiClient.put(`/api/staff/admin/doctors/${editingDoctor.id}`, {
          specialtyId,
          doctorRankId,
          biography: biography.trim() || undefined,
          isActive: editingDoctor.isActive,
        });
        setSuccessMsg(`Cập nhật bác sĩ "${editingDoctor.fullName}" thành công!`);
      } else {
        await apiClient.post('/api/staff/admin/doctors', {
          staffId,
          specialtyId,
          doctorRankId,
          biography: biography.trim() || undefined,
        });
        setSuccessMsg('Gán hồ sơ bác sĩ chuyên khoa thành công!');
      }
      setShowModal(false);
      loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { detail?: string } } };
      setErrorMsg(errorObj.response?.data?.detail || 'Thao tác không thành công.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleActive = async (doc: DoctorAdminDto) => {
    const nextStatus = !doc.isActive;
    if (!window.confirm(`${nextStatus ? 'Kích hoạt' : 'Tạm dừng'} hoạt động của bác sĩ "${doc.fullName}"?`)) {
      return;
    }
    try {
      await apiClient.post(`/api/staff/admin/doctors/${doc.id}/toggle-active?isActive=${nextStatus}`);
      setSuccessMsg(`Đã ${nextStatus ? 'kích hoạt' : 'tạm dừng'} bác sĩ ${doc.fullName}.`);
      loadData();
    } catch {
      setErrorMsg('Không thể đổi trạng thái bác sĩ.');
    }
  };

  const filteredDoctors = doctors.filter((d) => {
    const matchSearch =
      d.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      d.staffCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
      d.specialtyName.toLowerCase().includes(searchTerm.toLowerCase());
    return matchSearch;
  });

  return (
    <div className="space-y-5">
      {/* Search & Actions Bar */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
        <div className="flex flex-wrap items-center gap-3 w-full sm:w-auto">
          <div className="relative flex-1 sm:w-60">
            <span className="material-symbols-outlined absolute left-3 top-2.5 text-[#5E6F66] text-[18px]">search</span>
            <input
              type="text"
              placeholder="Tìm theo tên bác sĩ, mã NV..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full h-9 pl-9 pr-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:ring-2 focus:ring-[#006a41]/20"
            />
          </div>

          <select
            value={specialtyFilter}
            onChange={(e) => setSpecialtyFilter(e.target.value)}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:ring-2 focus:ring-[#006a41]/20"
          >
            <option value="">Tất cả chuyên khoa</option>
            {specialties.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name} ({s.code})
              </option>
            ))}
          </select>

          <select
            value={rankFilter}
            onChange={(e) => setRankFilter(e.target.value)}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:ring-2 focus:ring-[#006a41]/20"
          >
            <option value="">Tất cả cấp bậc</option>
            {ranks.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>

          <select
            value={activeFilter}
            onChange={(e) => setActiveFilter(e.target.value)}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] bg-white text-xs focus:ring-2 focus:ring-[#006a41]/20"
          >
            <option value="">Tất cả trạng thái</option>
            <option value="true">Đang hoạt động</option>
            <option value="false">Tạm dừng</option>
          </select>
        </div>

        <button
          onClick={handleOpenCreate}
          className="h-9 px-4 rounded-lg bg-[#006a41] text-white text-xs font-semibold hover:bg-[#028654] transition-colors flex items-center gap-1.5 shadow-sm shrink-0"
        >
          <span className="material-symbols-outlined text-[18px]">stethoscope</span>
          <span>Gán Hồ Sơ Bác Sĩ</span>
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

      {/* Doctor Cards / Table */}
      <div className="bg-white rounded-xl border border-[#DDE7E1] shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="py-16 text-center text-xs text-[#5E6F66]">Đang tải danh sách bác sĩ...</div>
        ) : filteredDoctors.length === 0 ? (
          <div className="py-16 text-center text-xs text-[#5E6F66]">Không có bác sĩ nào phù hợp.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-[#F7FAF8] border-b border-[#DDE7E1] text-[#5E6F66] font-semibold uppercase tracking-wider">
                  <th className="py-3 px-4">Mã NV</th>
                  <th className="py-3 px-4">Bác sĩ</th>
                  <th className="py-3 px-4">Cấp bậc / Học hàm</th>
                  <th className="py-3 px-4">Chuyên khoa</th>
                  <th className="py-3 px-4">Tiểu sử tóm tắt</th>
                  <th className="py-3 px-4">Trạng thái</th>
                  <th className="py-3 px-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {filteredDoctors.map((d) => (
                  <tr key={d.id} className="hover:bg-[#F9FBFA] transition-colors">
                    <td className="py-3 px-4 font-mono font-bold text-[#006a41]">{d.staffCode}</td>
                    <td className="py-3 px-4 font-semibold text-[#1C2B24]">{d.fullName}</td>
                    <td className="py-3 px-4">
                      <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-[#E0F2FE] text-[#0369A1] border border-[#BAE6FD]">
                        {d.rankName}
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-[#E8F5EE] text-[#006a41] border border-[#DDE7E1]">
                        {d.specialtyName}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-[#5E6F66] max-w-xs truncate" title={d.biography || ''}>
                      {d.biography || <em className="text-gray-400 font-normal">Chưa cập nhật</em>}
                    </td>
                    <td className="py-3 px-4">
                      {d.isActive ? (
                        <span className="inline-flex items-center gap-1 text-[#047857] font-semibold">
                          <span className="w-2 h-2 rounded-full bg-[#10B981]"></span>
                          Hoạt động
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-[#9CA3AF] font-semibold">
                          <span className="w-2 h-2 rounded-full bg-gray-400"></span>
                          Tạm dừng
                        </span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          onClick={() => handleOpenEdit(d)}
                          className="h-7 px-2 rounded border border-[#DDE7E1] hover:bg-gray-50 text-[11px] font-semibold text-[#1C2B24]"
                        >
                          Sửa
                        </button>
                        <button
                          onClick={() => handleToggleActive(d)}
                          className={`h-7 px-2 rounded text-[11px] font-semibold border transition-colors ${
                            d.isActive
                              ? 'border-[#FECACA] text-[#B91C1C] hover:bg-[#FEE2E2]'
                              : 'border-[#A7F3D0] text-[#047857] hover:bg-[#ECFDF5]'
                          }`}
                        >
                          {d.isActive ? 'Khóa' : 'Bật'}
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

      {/* Modal Gán / Sửa Bác Sĩ */}
      {showModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl border border-[#DDE7E1] shadow-xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-[#DDE7E1] pb-3">
              <h3 className="text-base font-bold text-[#1C2B24]">
                {editingDoctor ? `Cập Nhật Bác Sĩ: ${editingDoctor.fullName}` : 'Gán Hồ Sơ Bác Sĩ Chuyên Khoa'}
              </h3>
              <button onClick={() => setShowModal(false)} className="text-gray-400 hover:text-black">
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-3.5">
              {!editingDoctor && (
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                    Chọn Nhân Viên (StaffType = Doctor)
                  </label>
                  <select
                    value={staffId}
                    onChange={(e) => setStaffId(e.target.value)}
                    className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                    required
                  >
                    {availableStaff.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.fullName} ({s.staffCode})
                      </option>
                    ))}
                  </select>
                </div>
              )}

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Chuyên Khoa Phụ Trách</label>
                <select
                  value={specialtyId}
                  onChange={(e) => setSpecialtyId(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  required
                >
                  {specialties.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name} ({s.code})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Cấp Bậc / Học Hàm</label>
                <select
                  value={doctorRankId}
                  onChange={(e) => setDoctorRankId(e.target.value)}
                  className="w-full h-9 px-3 rounded-lg border border-[#DDE7E1] text-xs focus:ring-2 focus:ring-[#006a41]/20"
                  required
                >
                  {ranks.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name} ({r.code})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Tiểu Sử / Quá Trình Đào Tạo</label>
                <textarea
                  rows={3}
                  placeholder="Kinh nghiệm chuyên môn, bệnh viện công tác..."
                  value={biography}
                  onChange={(e) => setBiography(e.target.value)}
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
