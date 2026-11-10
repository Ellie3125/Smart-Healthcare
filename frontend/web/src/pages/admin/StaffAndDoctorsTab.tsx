import React, { useState, useEffect, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import {
  StaffDto,
  DoctorViewDto,
  StaffType,
  SpecialtyDto,
  DoctorRankDto,
  RoomDto,
} from '../../types/admin';
import {
  getStaffList,
  createStaff,
  updateStaff,
  getDoctors,
  createDoctor,
  updateDoctor,
  getSpecialties,
  getDoctorRanks,
  getRooms,
} from '../../api/mockAdminApi';

export const StaffAndDoctorsTab: React.FC = () => {
  const [subTab, setSubTab] = useState<'staff' | 'doctors'>('staff');
  const [staffList, setStaffList] = useState<StaffDto[]>([]);
  const [doctors, setDoctors] = useState<DoctorViewDto[]>([]);
  const [specialties, setSpecialties] = useState<SpecialtyDto[]>([]);
  const [ranks, setRanks] = useState<DoctorRankDto[]>([]);
  const [rooms, setRooms] = useState<RoomDto[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Staff modal
  const [isStaffModalOpen, setIsStaffModalOpen] = useState(false);
  const [editingStaff, setEditingStaff] = useState<StaffDto | null>(null);

  // Doctor modal
  const [isDoctorModalOpen, setIsDoctorModalOpen] = useState(false);
  const [editingDoctor, setEditingDoctor] = useState<DoctorViewDto | null>(null);

  const {
    register: registerStaff,
    handleSubmit: handleSubmitStaff,
    reset: resetStaff,
    formState: { errors: errorsStaff },
  } = useForm<{ staffCode: string; fullName: string; staffType: StaffType; isActive: boolean }>();

  const {
    register: registerDoc,
    handleSubmit: handleSubmitDoc,
    reset: resetDoc,
  } = useForm<{
    staffId: string;
    specialtyId: string;
    doctorRankId: string;
    defaultRoomId: string;
    biography: string;
    isActive: boolean;
  }>();

  const loadAll = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [sData, dData, spData, rkData, rmData] = await Promise.all([
        getStaffList(),
        getDoctors(),
        getSpecialties(),
        getDoctorRanks(),
        getRooms(),
      ]);
      setStaffList(sData);
      setDoctors(dData);
      setSpecialties(spData);
      setRanks(rkData);
      setRooms(rmData);
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      setError(errorObj.message || 'Lỗi khi tải danh sách nhân sự.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadAll();
  }, [loadAll]);

  // Open modal Staff
  const openCreateStaff = () => {
    setEditingStaff(null);
    resetStaff({
      staffCode: '',
      fullName: '',
      staffType: 'Doctor',
      isActive: true,
    });
    setIsStaffModalOpen(true);
  };

  const openEditStaff = (st: StaffDto) => {
    setEditingStaff(st);
    resetStaff({
      staffCode: st.staffCode,
      fullName: st.fullName,
      staffType: st.staffType,
      isActive: st.isActive,
    });
    setIsStaffModalOpen(true);
  };

  const onSubmitStaff = async (data: { staffCode: string; fullName: string; staffType: StaffType; isActive: boolean }) => {
    try {
      if (editingStaff) {
        await updateStaff(editingStaff.id, data);
      } else {
        await createStaff(data);
      }
      setIsStaffModalOpen(false);
      await loadAll();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      alert(errorObj.message || 'Lỗi khi lưu nhân viên.');
    }
  };

  // Open modal Doctor
  const openConfigureDoctor = (staffItem?: StaffDto, existingDoc?: DoctorViewDto) => {
    if (existingDoc) {
      setEditingDoctor(existingDoc);
      resetDoc({
        staffId: existingDoc.staffId,
        specialtyId: existingDoc.specialtyId,
        doctorRankId: existingDoc.doctorRankId,
        defaultRoomId: existingDoc.defaultRoomId || '',
        biography: existingDoc.biography || '',
        isActive: existingDoc.isActive,
      });
    } else if (staffItem) {
      setEditingDoctor(null);
      resetDoc({
        staffId: staffItem.id,
        specialtyId: specialties[0]?.id || '',
        doctorRankId: ranks[0]?.id || '',
        defaultRoomId: rooms[0]?.id || '',
        biography: '',
        isActive: true,
      });
    }
    setIsDoctorModalOpen(true);
  };

  const onSubmitDoctor = async (data: {
    staffId: string;
    specialtyId: string;
    doctorRankId: string;
    defaultRoomId: string;
    biography: string;
    isActive: boolean;
  }) => {
    try {
      if (editingDoctor) {
        await updateDoctor(editingDoctor.id, {
          specialtyId: data.specialtyId,
          doctorRankId: data.doctorRankId,
          defaultRoomId: data.defaultRoomId || null,
          biography: data.biography,
          isActive: data.isActive,
        });
      } else {
        await createDoctor({
          staffId: data.staffId,
          specialtyId: data.specialtyId,
          doctorRankId: data.doctorRankId,
          defaultRoomId: data.defaultRoomId || null,
          biography: data.biography,
          isActive: data.isActive,
        });
      }
      setIsDoctorModalOpen(false);
      await loadAll();
    } catch (err: unknown) {
      const errorObj = err as { message?: string };
      alert(errorObj.message || 'Lỗi khi lưu thông tin bác sĩ.');
    }
  };

  const getStaffTypeBadge = (type: StaffType) => {
    switch (type) {
      case 'Doctor':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-800 border border-emerald-200">Bác sĩ</span>;
      case 'Nurse':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-cyan-50 text-cyan-800 border border-cyan-200">Điều dưỡng</span>;
      case 'Receptionist':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-amber-50 text-amber-800 border border-amber-200">Tiếp tân</span>;
      case 'Technician':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-violet-50 text-violet-800 border border-violet-200">Kỹ thuật viên CLS</span>;
    }
  };

  if (loading) {
    return (
      <div className="p-12 text-center bg-white rounded-xl border border-[#DDE7E1]">
        <div className="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin mb-3"></div>
        <p className="text-sm text-[#5E6F66]">Đang tải dữ liệu nhân sự &amp; bác sĩ...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6 bg-[#FDE8E8] text-[#D32F2F] rounded-xl border border-[#F8B4B4] flex items-center justify-between">
        <span>{error}</span>
        <button onClick={loadAll} className="px-3 py-1 bg-[#D32F2F] text-white rounded text-xs font-semibold">
          Thử lại
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Sub tabs */}
      <div className="flex items-center gap-2 border-b border-[#DDE7E1] pb-2">
        <button
          onClick={() => setSubTab('staff')}
          className={`px-4 py-2 rounded-lg text-sm font-semibold transition-all ${
            subTab === 'staff'
              ? 'bg-[#E8F5EE] text-[#006a41]'
              : 'text-[#5E6F66] hover:text-[#1C2B24] hover:bg-gray-100'
          }`}
        >
          <span className="flex items-center gap-1.5">
            <span className="material-symbols-outlined text-[18px]">badge</span>
            Tất cả Nhân sự ({staffList.length})
          </span>
        </button>
        <button
          onClick={() => setSubTab('doctors')}
          className={`px-4 py-2 rounded-lg text-sm font-semibold transition-all ${
            subTab === 'doctors'
              ? 'bg-[#E8F5EE] text-[#006a41]'
              : 'text-[#5E6F66] hover:text-[#1C2B24] hover:bg-gray-100'
          }`}
        >
          <span className="flex items-center gap-1.5">
            <span className="material-symbols-outlined text-[18px]">stethoscope</span>
            Hồ sơ Bác sĩ chuyên khoa ({doctors.length})
          </span>
        </button>
      </div>

      {/* Staff View */}
      {subTab === 'staff' && (
        <div className="bg-white rounded-xl border border-[#DDE7E1] p-6 shadow-sm">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 mb-4 border-b border-[#DDE7E1] gap-3">
            <div>
              <h2 className="text-lg font-bold text-[#1C2B24] flex items-center gap-2">
                <span className="material-symbols-outlined text-primary text-[22px]">badge</span>
                Danh sách Nhân viên y tế (Staff)
              </h2>
              <p className="text-xs text-[#5E6F66] mt-0.5">
                Quản lý bác sĩ, điều dưỡng, tiếp tân và kỹ thuật viên vận hành hệ thống.
              </p>
            </div>
            <button
              onClick={openCreateStaff}
              className="h-9 px-4 rounded-lg bg-[#006a41] text-white hover:bg-[#005231] font-medium text-xs flex items-center gap-1.5 shadow-sm transition-colors"
            >
              <span className="material-symbols-outlined text-[16px]">add</span>
              <span>Thêm Nhân viên mới</span>
            </button>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm border-collapse">
              <thead>
                <tr className="bg-[#F7FAF8] text-[#5E6F66] text-xs uppercase tracking-wider border-b border-[#DDE7E1]">
                  <th className="py-3 px-4 font-semibold">Mã NV</th>
                  <th className="py-3 px-4 font-semibold">Họ và tên</th>
                  <th className="py-3 px-4 font-semibold">Loại nhân sự</th>
                  <th className="py-3 px-4 font-semibold text-center">Trạng thái</th>
                  <th className="py-3 px-4 font-semibold text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#DDE7E1]">
                {staffList.map((item) => {
                  const isDoctor = item.staffType === 'Doctor';
                  const hasDoctorRecord = doctors.some((d) => d.staffId === item.id);

                  return (
                    <tr key={item.id} className="hover:bg-[#F7FAF8] transition-colors">
                      <td className="py-3 px-4 font-data-tabular font-bold text-[#006a41]">{item.staffCode}</td>
                      <td className="py-3 px-4 font-medium text-[#1C2B24]">{item.fullName}</td>
                      <td className="py-3 px-4">{getStaffTypeBadge(item.staffType)}</td>
                      <td className="py-3 px-4 text-center">
                        <span
                          className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                            item.isActive
                              ? 'bg-secondary-container text-on-secondary-container'
                              : 'bg-gray-100 text-gray-600'
                          }`}
                        >
                          {item.isActive ? 'Đang công tác' : 'Nghỉ việc'}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-right space-x-2">
                        {isDoctor && (
                          <button
                            onClick={() => {
                              const existing = doctors.find((d) => d.staffId === item.id);
                              openConfigureDoctor(item, existing);
                            }}
                            className="text-emerald-700 hover:underline text-xs font-semibold inline-flex items-center gap-0.5"
                          >
                            <span className="material-symbols-outlined text-[15px]">settings</span>
                            {hasDoctorRecord ? 'Cấu hình BS' : 'Gắn hồ sơ BS'}
                          </button>
                        )}
                        <button
                          onClick={() => openEditStaff(item)}
                          className="text-primary hover:underline text-xs font-semibold inline-flex items-center gap-0.5"
                        >
                          <span className="material-symbols-outlined text-[15px]">edit</span>
                          Sửa
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Doctors View */}
      {subTab === 'doctors' && (
        <div className="bg-white rounded-xl border border-[#DDE7E1] p-6 shadow-sm">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 mb-4 border-b border-[#DDE7E1] gap-3">
            <div>
              <h2 className="text-lg font-bold text-[#1C2B24] flex items-center gap-2">
                <span className="material-symbols-outlined text-primary text-[22px]">stethoscope</span>
                Danh sách Bác sĩ (Doctor Profile)
              </h2>
              <p className="text-xs text-[#5E6F66] mt-0.5">
                Bác sĩ được liên kết với Chuyên khoa, Cấp bậc chuyên môn và Phòng khám mặc định.
              </p>
            </div>
          </div>

          {doctors.length === 0 ? (
            <div className="p-8 text-center text-[#5E6F66] border border-dashed rounded-lg">
              Chưa có hồ sơ bác sĩ nào được cấu hình.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm border-collapse">
                <thead>
                  <tr className="bg-[#F7FAF8] text-[#5E6F66] text-xs uppercase tracking-wider border-b border-[#DDE7E1]">
                    <th className="py-3 px-4 font-semibold">Bác sĩ</th>
                    <th className="py-3 px-4 font-semibold">Chuyên khoa</th>
                    <th className="py-3 px-4 font-semibold">Cấp bậc</th>
                    <th className="py-3 px-4 font-semibold">Phòng mặc định</th>
                    <th className="py-3 px-4 font-semibold text-center">Trạng thái</th>
                    <th className="py-3 px-4 font-semibold text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#DDE7E1]">
                  {doctors.map((item) => (
                    <tr key={item.id} className="hover:bg-[#F7FAF8] transition-colors">
                      <td className="py-3 px-4">
                        <div className="font-semibold text-[#1C2B24]">{item.fullName}</div>
                        <div className="text-xs text-[#5E6F66] font-data-tabular">Mã NV: {item.staffCode}</div>
                      </td>
                      <td className="py-3 px-4">
                        <span className="font-medium text-[#006a41]">{item.specialtyName}</span>
                        <span className="text-xs text-[#5E6F66] block font-data-tabular">({item.specialtyCode})</span>
                      </td>
                      <td className="py-3 px-4 font-medium text-[#1C2B24]">{item.rankName}</td>
                      <td className="py-3 px-4 font-data-tabular font-medium text-primary">
                        {item.defaultRoomNumber ? `Phòng ${item.defaultRoomNumber}` : '—'}
                      </td>
                      <td className="py-3 px-4 text-center">
                        <span
                          className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                            item.isActive
                              ? 'bg-secondary-container text-on-secondary-container'
                              : 'bg-gray-100 text-gray-600'
                          }`}
                        >
                          {item.isActive ? 'Sẵn sàng khám' : 'Tạm ngưng'}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-right">
                        <button
                          onClick={() => openConfigureDoctor(undefined, item)}
                          className="text-primary hover:underline text-xs font-semibold inline-flex items-center gap-1"
                        >
                          <span className="material-symbols-outlined text-[15px]">edit</span>
                          Cấu hình
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Modal Staff */}
      {isStaffModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 border border-[#DDE7E1]">
            <h3 className="text-base font-bold text-[#1C2B24] mb-3">
              {editingStaff ? 'Cập nhật Nhân viên' : 'Thêm Nhân viên mới'}
            </h3>
            <form onSubmit={handleSubmitStaff(onSubmitStaff)} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                    Mã nhân viên <span className="text-red-500">*</span>
                  </label>
                  <input
                    {...registerStaff('staffCode', { required: 'Nhập mã NV' })}
                    disabled={!!editingStaff}
                    className={`w-full h-10 px-3 rounded-lg border text-sm uppercase ${
                      editingStaff
                        ? 'bg-gray-100 text-gray-500 border-gray-300'
                        : 'border-[#DDE7E1] focus:border-primary outline-none'
                    }`}
                    placeholder="VD: BS03, DD02, TN02..."
                  />
                  {errorsStaff.staffCode && <p className="text-xs text-red-500 mt-1">{errorsStaff.staffCode.message}</p>}
                </div>

                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                    Loại nhân sự (StaffType) <span className="text-red-500">*</span>
                  </label>
                  <select
                    {...registerStaff('staffType', { required: 'Chọn loại nhân sự' })}
                    className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  >
                    <option value="Doctor">Doctor (Bác sĩ)</option>
                    <option value="Nurse">Nurse (Điều dưỡng)</option>
                    <option value="Receptionist">Receptionist (Tiếp tân)</option>
                    <option value="Technician">Technician (Kỹ thuật viên)</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Họ và tên nhân viên <span className="text-red-500">*</span>
                </label>
                <input
                  {...registerStaff('fullName', { required: 'Nhập họ và tên' })}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  placeholder="VD: BS.CKI Hoàng Văn Lâm"
                />
                {errorsStaff.fullName && <p className="text-xs text-red-500 mt-1">{errorsStaff.fullName.message}</p>}
              </div>

              <div className="flex items-center gap-2">
                <input
                  {...registerStaff('isActive')}
                  type="checkbox"
                  id="staffIsActive"
                  className="w-4 h-4 rounded text-primary accent-primary"
                />
                <label htmlFor="staffIsActive" className="text-sm text-[#1C2B24] cursor-pointer">
                  Đang công tác
                </label>
              </div>

              <div className="pt-3 flex items-center justify-end gap-2 border-t border-[#DDE7E1]">
                <button
                  type="button"
                  onClick={() => setIsStaffModalOpen(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-[#5E6F66] hover:bg-gray-100"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-[#006a41] hover:bg-[#005231] text-white rounded-lg text-sm font-semibold shadow-sm"
                >
                  Lưu Nhân sự
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Doctor Profile Configuration */}
      {isDoctorModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 border border-[#DDE7E1]">
            <h3 className="text-base font-bold text-[#1C2B24] mb-3">
              Cấu hình Hồ sơ Bác sĩ
            </h3>
            <form onSubmit={handleSubmitDoc(onSubmitDoctor)} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Nhân sự bác sĩ <span className="text-red-500">*</span>
                </label>
                <select
                  {...registerDoc('staffId', { required: 'Chọn nhân sự bác sĩ' })}
                  disabled={!!editingDoctor}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none disabled:bg-gray-100"
                >
                  {staffList
                    .filter((s) => s.staffType === 'Doctor')
                    .map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.staffCode} - {s.fullName}
                      </option>
                    ))}
                </select>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                    Chuyên khoa <span className="text-red-500">*</span>
                  </label>
                  <select
                    {...registerDoc('specialtyId', { required: 'Chọn chuyên khoa' })}
                    className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  >
                    {specialties.map((sp) => (
                      <option key={sp.id} value={sp.id}>
                        {sp.specialtyName}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                    Cấp bậc <span className="text-red-500">*</span>
                  </label>
                  <select
                    {...registerDoc('doctorRankId', { required: 'Chọn cấp bậc' })}
                    className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  >
                    {ranks.map((rk) => (
                      <option key={rk.id} value={rk.id}>
                        {rk.rankName}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Phòng khám mặc định
                </label>
                <select
                  {...registerDoc('defaultRoomId')}
                  className="w-full h-10 px-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                >
                  <option value="">-- Chưa gán phòng cố định --</option>
                  {rooms.map((rm) => (
                    <option key={rm.id} value={rm.id}>
                      Phòng {rm.roomNumber} - {rm.roomName} ({rm.roomType})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[#1C2B24] mb-1">
                  Tiểu sử &amp; Quá trình công tác
                </label>
                <textarea
                  {...registerDoc('biography')}
                  rows={3}
                  className="w-full p-3 rounded-lg border border-[#DDE7E1] text-sm focus:border-primary outline-none"
                  placeholder="Kinh nghiệm chuyên khoa, thành tích, quá trình đào tạo..."
                />
              </div>

              <div className="flex items-center gap-2">
                <input
                  {...registerDoc('isActive')}
                  type="checkbox"
                  id="docIsActive"
                  className="w-4 h-4 rounded text-primary accent-primary"
                />
                <label htmlFor="docIsActive" className="text-sm text-[#1C2B24] cursor-pointer">
                  Kích hoạt hồ sơ bác sĩ (Sẵn sàng mở lịch khám)
                </label>
              </div>

              <div className="pt-3 flex items-center justify-end gap-2 border-t border-[#DDE7E1]">
                <button
                  type="button"
                  onClick={() => setIsDoctorModalOpen(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-[#5E6F66] hover:bg-gray-100"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-[#006a41] hover:bg-[#005231] text-white rounded-lg text-sm font-semibold shadow-sm"
                >
                  Lưu Hồ sơ Bác sĩ
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
