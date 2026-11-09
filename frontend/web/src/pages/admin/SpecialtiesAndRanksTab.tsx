import React, { useState, useEffect, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { SpecialtyDto, DoctorRankDto, RankCode } from '../../types/admin';
import {
  getSpecialties,
  createSpecialty,
  updateSpecialty,
  getDoctorRanks,
  createDoctorRank,
  updateDoctorRank,
} from '../../api/mockAdminApi';

export const SpecialtiesAndRanksTab: React.FC = () => {
  const [specialties, setSpecialties] = useState<SpecialtyDto[]>([]);
  const [ranks, setRanks] = useState<DoctorRankDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Modal states for Specialty
  const [isSpecModalOpen, setIsSpecModalOpen] = useState(false);
  const [editingSpec, setEditingSpec] = useState<SpecialtyDto | null>(null);

  // Modal states for Rank
  const [isRankModalOpen, setIsRankModalOpen] = useState(false);
  const [editingRank, setEditingRank] = useState<DoctorRankDto | null>(null);

  // React Hook Form for Specialty
  const {
    register: registerSpec,
    handleSubmit: handleSubmitSpec,
    reset: resetSpec,
    formState: { errors: errorsSpec },
  } = useForm<{ specialtyCode: string; specialtyName: string; description?: string; isActive: boolean }>();

  // React Hook Form for Rank
  const {
    register: registerRank,
    handleSubmit: handleSubmitRank,
    reset: resetRank,
    formState: { errors: errorsRank },
  } = useForm<{ rankCode: RankCode; rankName: string; displayOrder: number; isActive: boolean }>();

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [specData, rankData] = await Promise.all([getSpecialties(), getDoctorRanks()]);
      setSpecialties(specData);
      setRanks(rankData);
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      setError(errorObj.message || 'Lỗi khi tải dữ liệu chuyên khoa và cấp bậc.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Open modal create Specialty
  const openCreateSpec = () => {
    setEditingSpec(null);
    resetSpec({
      specialtyCode: '',
      specialtyName: '',
      description: '',
      isActive: true,
    });
    setIsSpecModalOpen(true);
  };

  // Open modal edit Specialty
  const openEditSpec = (spec: SpecialtyDto) => {
    setEditingSpec(spec);
    resetSpec({
      specialtyCode: spec.specialtyCode,
      specialtyName: spec.specialtyName,
      description: spec.description || '',
      isActive: spec.isActive,
    });
    setIsSpecModalOpen(true);
  };

  const onSubmitSpec = async (data: { specialtyCode: string; specialtyName: string; description?: string; isActive: boolean }) => {
    try {
      if (editingSpec) {
        // Cập nhật: SpecialtyCode cố định không đổi
        await updateSpecialty(editingSpec.id, {
          specialtyName: data.specialtyName,
          description: data.description,
          isActive: data.isActive,
        });
      } else {
        await createSpecialty({
          specialtyCode: data.specialtyCode,
          specialtyName: data.specialtyName,
          description: data.description,
          isActive: data.isActive,
        });
      }
      setIsSpecModalOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      alert(errorObj.message || 'Lỗi khi lưu chuyên khoa.');
    }
  };

  // Open modal create Rank
  const openCreateRank = () => {
    setEditingRank(null);
    resetRank({
      rankCode: 'Doctor',
      rankName: '',
      displayOrder: (ranks.length + 1),
      isActive: true,
    });
    setIsRankModalOpen(true);
  };

  // Open modal edit Rank
  const openEditRank = (rank: DoctorRankDto) => {
    setEditingRank(rank);
    resetRank({
      rankCode: rank.rankCode,
      rankName: rank.rankName,
      displayOrder: rank.displayOrder,
      isActive: rank.isActive,
    });
    setIsRankModalOpen(true);
  };

  const onSubmitRank = async (data: { rankCode: RankCode; rankName: string; displayOrder: number; isActive: boolean }) => {
    try {
      if (editingRank) {
        await updateDoctorRank(editingRank.id, data);
      } else {
        await createDoctorRank(data);
      }
      setIsRankModalOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      alert(errorObj.message || 'Lỗi khi lưu cấp bậc.');
    }
  };

  if (loading) {
    return (
      <div className="p-12 text-center bg-white rounded-xl border border-[#DDE7E1]">
        <div className="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin mb-3"></div>
        <p className="text-sm text-[#5E6F66]">Đang tải danh mục chuyên khoa và cấp bậc bác sĩ...</p>
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
    <div className="space-y-8">
      {/* 1. Specialties Section */}
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 mb-4 border-b border-[#DDE7E1] gap-3">
          <div>
            <h2 className="text-lg font-bold text-[#1C2B24] flex items-center gap-2">
              <span className="material-symbols-outlined text-primary text-[22px]">medical_services</span>
              Danh mục Chuyên khoa (Specialty)
            </h2>
            <p className="text-xs text-[#5E6F66] mt-0.5">
              Mã chuyên khoa (SpecialtyCode) là khóa cố định tham chiếu bởi chatbot và đặt lịch.
            </p>
          </div>
          <button
            onClick={openCreateSpec}
            className="h-9 px-4 rounded-lg bg-[#006a41] text-white hover:bg-[#005231] font-medium text-xs flex items-center gap-1.5 shadow-sm transition-colors"
          >
            <span className="material-symbols-outlined text-[16px]">add</span>
            <span>Thêm Chuyên khoa</span>
          </button>
        </div>

        {specialties.length === 0 ? (
          <div className="p-8 text-center text-[#5E6F66] border border-dashed rounded-lg">
            Chưa có chuyên khoa nào.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm border-collapse">
              <thead>
                <tr className="bg-[#F7FAF8] text-[#5E6F66] text-xs uppercase tracking-wider border-b border-[#DDE7E1]">
                  <th className="py-3 px-4 font-semibold">Mã Chuyên khoa (Code)</th>
                  <th className="py-3 px-4 font-semibold">Tên Chuyên khoa</th>
                  <th className="py-3 px-4 font-semibold">Mô tả nghiệp vụ</th>
                  <th className="py-3 px-4 font-semibold text-center">Trạng thái</th>
                  <th className="py-3 px-4 font-semibold text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {specialties.map((item) => (
                  <tr key={item.id} className="hover:bg-[#F7FAF8] transition-colors">
                    <td className="py-3 px-4 font-data-tabular font-semibold text-[#006a41]">
                      {item.specialtyCode}
                    </td>
                    <td className="py-3 px-4 font-medium text-[#1C2B24]">{item.specialtyName}</td>
                    <td className="py-3 px-4 text-[#5E6F66] text-xs max-w-md truncate">
                      {item.description || '—'}
                    </td>
                    <td className="py-3 px-4 text-center">
                      <span
                        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                          item.isActive
                            ? 'bg-secondary-container text-on-secondary-container'
                            : 'bg-gray-100 text-gray-600'
                        }`}
                      >
                        {item.isActive ? 'Hoạt động' : 'Tạm ngưng'}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-right">
                      <button
                        onClick={() => openEditSpec(item)}
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

      {/* 2. Doctor Ranks Section */}
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 mb-4 border-b border-[#DDE7E1] gap-3">
          <div>
            <h2 className="text-lg font-bold text-[#1C2B24] flex items-center gap-2">
              <span className="material-symbols-outlined text-primary text-[22px]">workspace_premium</span>
              Danh mục Cấp bậc Bác sĩ (DoctorRank)
            </h2>
            <p className="text-xs text-[#5E6F66] mt-0.5">
              Phân định mức chuyên môn (BS.CKI, BS.CKII, ThS, PGS, GS...) quyết định giá khám và bảng danh mục.
            </p>
          </div>
          <button
            onClick={openCreateRank}
            className="h-9 px-4 rounded-lg bg-[#006a41] text-white hover:bg-[#005231] font-medium text-xs flex items-center gap-1.5 shadow-sm transition-colors"
          >
            <span className="material-symbols-outlined text-[16px]">add</span>
            <span>Thêm Cấp bậc</span>
          </button>
        </div>

        {ranks.length === 0 ? (
          <div className="p-8 text-center text-[#5E6F66] border border-dashed rounded-lg">
            Chưa có cấp bậc bác sĩ nào.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm border-collapse">
              <thead>
                <tr className="bg-[#F7FAF8] text-[#5E6F66] text-xs uppercase tracking-wider border-b border-[#DDE7E1]">
                  <th className="py-3 px-4 font-semibold text-center w-16">Thứ tự</th>
                  <th className="py-3 px-4 font-semibold">Mã định danh (Enum)</th>
                  <th className="py-3 px-4 font-semibold">Tên cấp bậc hiển thị</th>
                  <th className="py-3 px-4 font-semibold text-center">Trạng thái</th>
                  <th className="py-3 px-4 font-semibold text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {ranks.map((item) => (
                  <tr key={item.id} className="hover:bg-[#F7FAF8] transition-colors">
                    <td className="py-3 px-4 text-center font-data-tabular font-bold text-[#5E6F66]">
                      {item.displayOrder}
                    </td>
                    <td className="py-3 px-4 font-data-tabular font-medium text-[#1C2B24]">{item.rankCode}</td>
                    <td className="py-3 px-4 font-semibold text-[#006a41]">{item.rankName}</td>
                    <td className="py-3 px-4 text-center">
                      <span
                        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                          item.isActive
                            ? 'bg-secondary-container text-on-secondary-container'
                            : 'bg-gray-100 text-gray-600'
                        }`}
                      >
                        {item.isActive ? 'Hoạt động' : 'Ẩn'}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-right">
                      <button
                        onClick={() => openEditRank(item)}
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

      {/* Modal Specialty */}
      {isSpecModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 border border-[#DDE7E1]">
            <h3 className="text-base font-bold text-[#1C2B24] mb-3">
              {editingSpec ? 'Cập nhật Chuyên khoa' : 'Thêm Chuyên khoa mới'}
            </h3>
            <form onSubmit={handleSubmitSpec(onSubmitSpec)} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Mã Chuyên khoa (SpecialtyCode) <span className="text-red-500">*</span>
                </label>
                <input
                  {...registerSpec('specialtyCode', { required: 'Vui lòng nhập mã chuyên khoa' })}
                  disabled={!!editingSpec} // Quy tắc: KHÔNG cho đổi mã sau khi tạo!
                  className={`w-full h-10 px-3 rounded-lg border text-sm uppercase ${
                    editingSpec
                      ? 'bg-gray-100 border-gray-300 text-gray-500 cursor-not-allowed'
                      : 'border-[#DDE7E1] focus:border-primary outline-none'
                  }`}
                  placeholder="VD: XQUANG, NOI_TQ, TIM_MACH..."
                />
                {editingSpec && (
                  <p className="text-[11px] text-amber-700 mt-1">
                    * Mã chuyên khoa là khóa cố định tham chiếu logic liên module và không được sửa sau khi tạo.
                  </p>
                )}
                {errorsSpec.specialtyCode && (
                  <p className="text-xs text-red-500 mt-1">{errorsSpec.specialtyCode.message}</p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Tên Chuyên khoa <span className="text-red-500">*</span>
                </label>
                <input
                  {...registerSpec('specialtyName', { required: 'Vui lòng nhập tên chuyên khoa' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  placeholder="VD: Chẩn đoán hình ảnh, Khoa Nhi..."
                />
                {errorsSpec.specialtyName && (
                  <p className="text-xs text-red-500 mt-1">{errorsSpec.specialtyName.message}</p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">Mô tả</label>
                <textarea
                  {...registerSpec('description')}
                  rows={3}
                  className="w-full p-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  placeholder="Mô tả phạm vi khám và chẩn đoán của chuyên khoa..."
                />
              </div>

              <div className="flex items-center gap-2">
                <input
                  {...registerSpec('isActive')}
                  type="checkbox"
                  id="specIsActive"
                  className="w-4 h-4 rounded text-primary accent-primary"
                />
                <label htmlFor="specIsActive" className="text-sm text-[#1C2B24] cursor-pointer">
                  Kích hoạt hoạt động
                </label>
              </div>

              <div className="pt-3 flex items-center justify-end gap-2 border-t border-[#DDE7E1]">
                <button
                  type="button"
                  onClick={() => setIsSpecModalOpen(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-[#5E6F66] hover:bg-gray-100"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-[#006a41] hover:bg-[#005231] text-white rounded-lg text-sm font-semibold shadow-sm"
                >
                  Lưu Chuyên khoa
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal DoctorRank */}
      {isRankModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 border border-[#DDE7E1]">
            <h3 className="text-base font-bold text-[#1C2B24] mb-3">
              {editingRank ? 'Cập nhật Cấp bậc Bác sĩ' : 'Thêm Cấp bậc mới'}
            </h3>
            <form onSubmit={handleSubmitRank(onSubmitRank)} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Mã Rank (Enum) <span className="text-red-500">*</span>
                </label>
                <select
                  {...registerRank('rankCode', { required: 'Vui lòng chọn mã cấp bậc' })}
                  disabled={!!editingRank}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                >
                  <option value="SpecialistI">SpecialistI (BS.CKI)</option>
                  <option value="SpecialistII">SpecialistII (BS.CKII)</option>
                  <option value="Master">Master (ThS.BS)</option>
                  <option value="Doctor">Doctor (BS Đa khoa)</option>
                  <option value="AssociateProf">AssociateProf (PGS)</option>
                  <option value="Professor">Professor (GS)</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Tên hiển thị cấp bậc <span className="text-red-500">*</span>
                </label>
                <input
                  {...registerRank('rankName', { required: 'Vui lòng nhập tên hiển thị' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  placeholder="VD: Bác sĩ Chuyên khoa I (BS.CKI)"
                />
                {errorsRank.rankName && (
                  <p className="text-xs text-red-500 mt-1">{errorsRank.rankName.message}</p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Thứ tự hiển thị (DisplayOrder) <span className="text-red-500">*</span>
                </label>
                <input
                  {...registerRank('displayOrder', { valueAsNumber: true, required: 'Nhập thứ tự' })}
                  type="number"
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none font-data-tabular"
                />
              </div>

              <div className="flex items-center gap-2">
                <input
                  {...registerRank('isActive')}
                  type="checkbox"
                  id="rankIsActive"
                  className="w-4 h-4 rounded text-primary accent-primary"
                />
                <label htmlFor="rankIsActive" className="text-sm text-[#1C2B24] cursor-pointer">
                  Kích hoạt cấp bậc
                </label>
              </div>

              <div className="pt-3 flex items-center justify-end gap-2 border-t border-[#DDE7E1]">
                <button
                  type="button"
                  onClick={() => setIsRankModalOpen(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-[#5E6F66] hover:bg-gray-100"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-[#006a41] hover:bg-[#005231] text-white rounded-lg text-sm font-semibold shadow-sm"
                >
                  Lưu Cấp bậc
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
