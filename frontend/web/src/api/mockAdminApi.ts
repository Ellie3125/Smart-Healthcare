import {
  SpecialtyDto,
  DoctorRankDto,
  RoomDto,
  StaffDto,
  DoctorDto,
  DoctorViewDto,
  RoomAccountDto,
  RoomAccountViewDto,
  RoomDeviceSessionDto,
  StaffShiftAssignmentDto,
  StaffShiftAssignmentViewDto,
} from '../types/admin';

// TODO(backend): Đây là lớp mock tách biệt cho các endpoint quản trị Identity & Patients chưa có ở backend.
// Khi backend hoàn thiện các controller /api/identity-patients/admin/..., gỡ bỏ file này và chuyển sang gọi apiClient.

const STORAGE_KEY_PREFIX = 'sh_admin_mock_';

function loadOrSeed<T>(key: string, initial: T): T {
  const raw = localStorage.getItem(STORAGE_KEY_PREFIX + key);
  if (!raw) {
    localStorage.setItem(STORAGE_KEY_PREFIX + key, JSON.stringify(initial));
    return initial;
  }
  try {
    return JSON.parse(raw);
  } catch {
    return initial;
  }
}

function save<T>(key: string, data: T): void {
  localStorage.setItem(STORAGE_KEY_PREFIX + key, JSON.stringify(data));
}

// 1. Initial Seeds
const SEED_SPECIALTIES: SpecialtyDto[] = [
  {
    id: 'spec-01',
    specialtyCode: 'XQUANG',
    specialtyName: 'Chẩn đoán hình ảnh & X-Quang',
    description: 'Chẩn đoán hình ảnh kỹ thuật số, X-Quang can thiệp và kiểm tra định kỳ.',
    isActive: true,
  },
  {
    id: 'spec-02',
    specialtyCode: 'NOI_TQ',
    specialtyName: 'Nội tổng quát',
    description: 'Khám và điều trị các bệnh lý nội khoa tổng quát người lớn.',
    isActive: true,
  },
  {
    id: 'spec-03',
    specialtyCode: 'NGOAI_TQ',
    specialtyName: 'Ngoại tổng quát',
    description: 'Tiểu phẫu, khám tiền phẫu và theo dõi sau phẫu thuật.',
    isActive: true,
  },
  {
    id: 'spec-04',
    specialtyCode: 'NHI',
    specialtyName: 'Nhi khoa',
    description: 'Chăm sóc và điều trị chuyên sâu cho trẻ sơ sinh và trẻ nhỏ.',
    isActive: true,
  },
  {
    id: 'spec-05',
    specialtyCode: 'TIM_MACH',
    specialtyName: 'Tim mạch can thiệp',
    description: 'Tầm soát xơ vữa, tăng huyết áp, siêu âm tim Doppler.',
    isActive: true,
  },
];

const SEED_DOCTOR_RANKS: DoctorRankDto[] = [
  { id: 'rank-01', rankCode: 'SpecialistI', rankName: 'Bác sĩ Chuyên khoa I (BS.CKI)', displayOrder: 1, isActive: true },
  { id: 'rank-02', rankCode: 'SpecialistII', rankName: 'Bác sĩ Chuyên khoa II (BS.CKII)', displayOrder: 2, isActive: true },
  { id: 'rank-03', rankCode: 'Master', rankName: 'Thạc sĩ Y khoa (ThS.BS)', displayOrder: 3, isActive: true },
  { id: 'rank-04', rankCode: 'Doctor', rankName: 'Bác sĩ Đa khoa (BS)', displayOrder: 4, isActive: true },
  { id: 'rank-05', rankCode: 'AssociateProf', rankName: 'Phó Giáo sư (PGS.TS.BS)', displayOrder: 5, isActive: true },
  { id: 'rank-06', rankCode: 'Professor', rankName: 'Giáo sư (GS.TS.BS)', displayOrder: 6, isActive: true },
];

const SEED_ROOMS: RoomDto[] = [
  { id: 'room-101', roomNumber: '101', roomName: 'Phòng khám Nội 01', roomType: 'Clinical', departmentName: 'Khoa Khám Bệnh', isActive: true },
  { id: 'room-102', roomNumber: '102', roomName: 'Phòng khám Ngoại 01', roomType: 'Clinical', departmentName: 'Khoa Khám Bệnh', isActive: true },
  { id: 'room-103', roomNumber: '103', roomName: 'Phòng khám Nhi 01', roomType: 'Clinical', departmentName: 'Khoa Nhi', isActive: true },
  { id: 'room-201', roomNumber: '201', roomName: 'Phòng X-Quang KTS 01', roomType: 'XRay', departmentName: 'Khoa Cận Lâm Sàng', isActive: true },
  { id: 'room-202', roomNumber: '202', roomName: 'Phòng Siêu âm 4D', roomType: 'Ultrasound', departmentName: 'Khoa Cận Lâm Sàng', isActive: true },
  { id: 'room-301', roomNumber: '301', roomName: 'Phòng Xét nghiệm Hóa sinh', roomType: 'Laboratory', departmentName: 'Khoa Xét Nghiệm', isActive: true },
];

const SEED_STAFFS: StaffDto[] = [
  { id: 'staff-01', appUserId: 'u-bs01', staffCode: 'BS01', fullName: 'BS.CKI Nguyễn Văn Hùng', staffType: 'Doctor', isActive: true, createdAtUtc: '2025-01-01T08:00:00Z' },
  { id: 'staff-02', appUserId: 'u-bs02', staffCode: 'BS02', fullName: 'ThS.BS Trần Mai Phương', staffType: 'Doctor', isActive: true, createdAtUtc: '2025-01-01T08:00:00Z' },
  { id: 'staff-03', appUserId: 'u-dd01', staffCode: 'DD01', fullName: 'Lê Thị Thúy', staffType: 'Nurse', isActive: true, createdAtUtc: '2025-01-01T08:00:00Z' },
  { id: 'staff-04', appUserId: 'u-tn01', staffCode: 'TN01', fullName: 'Phạm Thanh Tâm', staffType: 'Receptionist', isActive: true, createdAtUtc: '2025-01-01T08:00:00Z' },
  { id: 'staff-05', appUserId: 'u-ktv01', staffCode: 'KTV01', fullName: 'Đỗ Quốc Huy', staffType: 'Technician', isActive: true, createdAtUtc: '2025-01-01T08:00:00Z' },
];

const SEED_DOCTORS: DoctorDto[] = [
  { id: 'doc-01', staffId: 'staff-01', specialtyId: 'spec-01', doctorRankId: 'rank-01', defaultRoomId: 'room-201', biography: 'Hơn 12 năm kinh nghiệm trong chẩn đoán hình ảnh can thiệp.', isActive: true },
  { id: 'doc-02', staffId: 'staff-02', specialtyId: 'spec-02', doctorRankId: 'rank-03', defaultRoomId: 'room-101', biography: 'Tốt nghiệp Thạc sĩ ĐH Y Dược, chuyên sâu quản lý đái tháo đường & tăng huyết áp.', isActive: true },
];

const SEED_ROOM_ACCOUNTS: RoomAccountDto[] = [
  { id: 'ra-01', appUserId: 'u-room-201', roomId: 'room-201', accountCode: 'phong_xq01', isActive: true, createdAtUtc: '2025-01-01T08:00:00Z' },
  { id: 'ra-02', appUserId: 'u-room-301', roomId: 'room-301', accountCode: 'phong_xnh01', isActive: true, createdAtUtc: '2025-01-01T08:00:00Z' },
];

const SEED_SESSIONS: RoomDeviceSessionDto[] = [
  {
    id: 'sess-01',
    roomAccountId: 'ra-01',
    deviceId: 'PC-XRAY-STATION-01',
    deviceIpAddress: '192.168.1.140',
    loginAtUtc: new Date(Date.now() - 3600000 * 2).toISOString(),
    logoutAtUtc: null,
    isActive: true,
  },
];

const SEED_SHIFTS: StaffShiftAssignmentDto[] = [
  {
    id: 'shift-01',
    staffId: 'staff-01',
    roomId: 'room-201',
    shiftDate: new Date().toISOString().substring(0, 10),
    shiftName: 'Morning',
    startsAtUtc: new Date().toISOString().substring(0, 10) + 'T06:30:00Z',
    endsAtUtc: new Date().toISOString().substring(0, 10) + 'T12:00:00Z',
    isShiftLeader: true,
  },
  {
    id: 'shift-02',
    staffId: 'staff-02',
    roomId: 'room-101',
    shiftDate: new Date().toISOString().substring(0, 10),
    shiftName: 'Morning',
    startsAtUtc: new Date().toISOString().substring(0, 10) + 'T06:30:00Z',
    endsAtUtc: new Date().toISOString().substring(0, 10) + 'T12:00:00Z',
    isShiftLeader: true,
  },
  {
    id: 'shift-03',
    staffId: 'staff-03',
    roomId: 'room-101',
    shiftDate: new Date().toISOString().substring(0, 10),
    shiftName: 'Morning',
    startsAtUtc: new Date().toISOString().substring(0, 10) + 'T06:30:00Z',
    endsAtUtc: new Date().toISOString().substring(0, 10) + 'T12:00:00Z',
    isShiftLeader: false,
  },
];

// Helper delay
const delay = (ms = 150) => new Promise((res) => setTimeout(res, ms));

// ==================== 1. SPECIALTIES ====================
// TODO(backend): GET /api/identity-patients/admin/specialties
export async function getSpecialties(): Promise<SpecialtyDto[]> {
  await delay();
  return loadOrSeed('specialties', SEED_SPECIALTIES);
}

// TODO(backend): POST /api/identity-patients/admin/specialties
export async function createSpecialty(data: Omit<SpecialtyDto, 'id'>): Promise<SpecialtyDto> {
  await delay();
  const list = loadOrSeed('specialties', SEED_SPECIALTIES);
  const code = data.specialtyCode.trim().toUpperCase();
  if (list.some((s) => s.specialtyCode === code)) {
    throw new Error(`Mã chuyên khoa "${code}" đã tồn tại trên hệ thống.`);
  }
  const newSpec: SpecialtyDto = {
    ...data,
    id: 'spec-' + Math.random().toString(36).substring(2, 9),
    specialtyCode: code,
  };
  save('specialties', [...list, newSpec]);
  return newSpec;
}

// TODO(backend): PUT /api/identity-patients/admin/specialties/{id}
export async function updateSpecialty(id: string, data: Pick<SpecialtyDto, 'specialtyName' | 'description' | 'isActive'>): Promise<SpecialtyDto> {
  await delay();
  const list = loadOrSeed('specialties', SEED_SPECIALTIES);
  const index = list.findIndex((s) => s.id === id);
  if (index === -1) throw new Error('Không tìm thấy chuyên khoa cần sửa.');

  // Quy tắc nghiệp vụ: Mã chuyên khoa (SpecialtyCode) là khóa cố định, KHÔNG cho đổi sau khi tạo
  const existing = list[index];
  const updated: SpecialtyDto = {
    ...existing,
    specialtyName: data.specialtyName.trim(),
    description: data.description ? data.description.trim() : null,
    isActive: data.isActive,
  };
  list[index] = updated;
  save('specialties', list);
  return updated;
}

// ==================== 2. DOCTOR RANKS ====================
// TODO(backend): GET /api/identity-patients/admin/doctor-ranks
export async function getDoctorRanks(): Promise<DoctorRankDto[]> {
  await delay();
  return loadOrSeed('doctor_ranks', SEED_DOCTOR_RANKS);
}

// TODO(backend): POST /api/identity-patients/admin/doctor-ranks
export async function createDoctorRank(data: Omit<DoctorRankDto, 'id'>): Promise<DoctorRankDto> {
  await delay();
  const list = loadOrSeed('doctor_ranks', SEED_DOCTOR_RANKS);
  const newRank: DoctorRankDto = {
    ...data,
    id: 'rank-' + Math.random().toString(36).substring(2, 9),
  };
  save('doctor_ranks', [...list, newRank].sort((a, b) => a.displayOrder - b.displayOrder));
  return newRank;
}

// TODO(backend): PUT /api/identity-patients/admin/doctor-ranks/{id}
export async function updateDoctorRank(id: string, data: Partial<Omit<DoctorRankDto, 'id'>>): Promise<DoctorRankDto> {
  await delay();
  const list = loadOrSeed('doctor_ranks', SEED_DOCTOR_RANKS);
  const index = list.findIndex((r) => r.id === id);
  if (index === -1) throw new Error('Không tìm thấy cấp bậc bác sĩ.');
  const updated: DoctorRankDto = { ...list[index], ...data };
  list[index] = updated;
  save('doctor_ranks', list.sort((a, b) => a.displayOrder - b.displayOrder));
  return updated;
}

// ==================== 3. ROOMS ====================
// TODO(backend): GET /api/identity-patients/admin/rooms
export async function getRooms(): Promise<RoomDto[]> {
  await delay();
  return loadOrSeed('rooms', SEED_ROOMS);
}

// TODO(backend): POST /api/identity-patients/admin/rooms
export async function createRoom(data: Omit<RoomDto, 'id'>): Promise<RoomDto> {
  await delay();
  const list = loadOrSeed('rooms', SEED_ROOMS);
  if (list.some((r) => r.roomNumber.toLowerCase() === data.roomNumber.trim().toLowerCase())) {
    throw new Error(`Số phòng "${data.roomNumber}" đã tồn tại.`);
  }
  const newRoom: RoomDto = {
    ...data,
    id: 'room-' + Math.random().toString(36).substring(2, 9),
    roomNumber: data.roomNumber.trim(),
    roomName: data.roomName.trim(),
  };
  save('rooms', [...list, newRoom]);
  return newRoom;
}

// TODO(backend): PUT /api/identity-patients/admin/rooms/{id}
export async function updateRoom(id: string, data: Partial<Omit<RoomDto, 'id'>>): Promise<RoomDto> {
  await delay();
  const list = loadOrSeed('rooms', SEED_ROOMS);
  const index = list.findIndex((r) => r.id === id);
  if (index === -1) throw new Error('Không tìm thấy phòng.');
  const updated: RoomDto = { ...list[index], ...data };
  list[index] = updated;
  save('rooms', list);
  return updated;
}

// ==================== 4. STAFF & DOCTORS ====================
// TODO(backend): GET /api/identity-patients/admin/staff
export async function getStaffList(): Promise<StaffDto[]> {
  await delay();
  return loadOrSeed('staffs', SEED_STAFFS);
}

// TODO(backend): POST /api/identity-patients/admin/staff
export async function createStaff(data: Omit<StaffDto, 'id' | 'createdAtUtc' | 'appUserId'>): Promise<StaffDto> {
  await delay();
  const list = loadOrSeed('staffs', SEED_STAFFS);
  const code = data.staffCode.trim().toUpperCase();
  if (list.some((s) => s.staffCode === code)) {
    throw new Error(`Mã nhân viên "${code}" đã được sử dụng.`);
  }
  const newStaff: StaffDto = {
    ...data,
    id: 'staff-' + Math.random().toString(36).substring(2, 9),
    appUserId: 'u-' + code.toLowerCase(),
    staffCode: code,
    fullName: data.fullName.trim(),
    createdAtUtc: new Date().toISOString(),
  };
  save('staffs', [...list, newStaff]);
  return newStaff;
}

// TODO(backend): PUT /api/identity-patients/admin/staff/{id}
export async function updateStaff(id: string, data: Partial<Omit<StaffDto, 'id' | 'createdAtUtc' | 'appUserId'>>): Promise<StaffDto> {
  await delay();
  const list = loadOrSeed('staffs', SEED_STAFFS);
  const index = list.findIndex((s) => s.id === id);
  if (index === -1) throw new Error('Không tìm thấy nhân viên.');
  const updated: StaffDto = { ...list[index], ...data };
  list[index] = updated;
  save('staffs', list);
  return updated;
}

// TODO(backend): GET /api/identity-patients/admin/doctors
export async function getDoctors(): Promise<DoctorViewDto[]> {
  await delay();
  const doctors = loadOrSeed('doctors', SEED_DOCTORS);
  const staffs = loadOrSeed('staffs', SEED_STAFFS);
  const specialties = loadOrSeed('specialties', SEED_SPECIALTIES);
  const ranks = loadOrSeed('doctor_ranks', SEED_DOCTOR_RANKS);
  const rooms = loadOrSeed('rooms', SEED_ROOMS);

  return doctors.map((d) => {
    const s = staffs.find((st) => st.id === d.staffId);
    const spec = specialties.find((sp) => sp.id === d.specialtyId);
    const rk = ranks.find((r) => r.id === d.doctorRankId);
    const rm = rooms.find((rm) => rm.id === d.defaultRoomId);

    return {
      id: d.id,
      staffId: d.staffId,
      fullName: s?.fullName || 'Bác sĩ không tên',
      staffCode: s?.staffCode || '',
      specialtyId: d.specialtyId,
      specialtyCode: spec?.specialtyCode || '',
      specialtyName: spec?.specialtyName || 'Chưa gán',
      doctorRankId: d.doctorRankId,
      rankName: rk?.rankName || 'Chưa gán',
      defaultRoomId: d.defaultRoomId,
      defaultRoomNumber: rm?.roomNumber,
      biography: d.biography,
      isActive: d.isActive && (s?.isActive ?? true),
    };
  });
}

// TODO(backend): POST /api/identity-patients/admin/doctors
export async function createDoctor(data: Omit<DoctorDto, 'id'>): Promise<DoctorDto> {
  await delay();
  const doctors = loadOrSeed('doctors', SEED_DOCTORS);
  if (doctors.some((d) => d.staffId === data.staffId)) {
    throw new Error('Nhân viên này đã được cấu hình hồ sơ bác sĩ.');
  }
  const newDoc: DoctorDto = {
    ...data,
    id: 'doc-' + Math.random().toString(36).substring(2, 9),
  };
  save('doctors', [...doctors, newDoc]);
  return newDoc;
}

// TODO(backend): PUT /api/identity-patients/admin/doctors/{id}
export async function updateDoctor(id: string, data: Partial<Omit<DoctorDto, 'id' | 'staffId'>>): Promise<DoctorDto> {
  await delay();
  const doctors = loadOrSeed('doctors', SEED_DOCTORS);
  const index = doctors.findIndex((d) => d.id === id);
  if (index === -1) throw new Error('Không tìm thấy thông tin bác sĩ.');
  const updated: DoctorDto = { ...doctors[index], ...data };
  doctors[index] = updated;
  save('doctors', doctors);
  return updated;
}

// ==================== 5. ROOM ACCOUNTS & DEVICE SESSIONS ====================
// TODO(backend): GET /api/identity-patients/admin/room-accounts
export async function getRoomAccounts(): Promise<RoomAccountViewDto[]> {
  await delay();
  const accounts = loadOrSeed('room_accounts', SEED_ROOM_ACCOUNTS);
  const rooms = loadOrSeed('rooms', SEED_ROOMS);
  const sessions = loadOrSeed('room_sessions', SEED_SESSIONS);

  return accounts.map((ra) => {
    const rm = rooms.find((r) => r.id === ra.roomId);
    // Quy tắc nghiệp vụ: Mỗi tài khoản phòng tối đa MỘT phiên hoạt động (active session)
    const activeSess = sessions.find((s) => s.roomAccountId === ra.id && s.isActive);

    return {
      ...ra,
      roomNumber: rm?.roomNumber || 'N/A',
      roomName: rm?.roomName || 'N/A',
      roomType: rm?.roomType || 'Clinical',
      departmentName: rm?.departmentName || 'N/A',
      activeSession: activeSess || null,
    };
  });
}

// TODO(backend): POST /api/identity-patients/admin/room-accounts
export async function createRoomAccount(data: { roomId: string; accountCode: string }): Promise<RoomAccountDto> {
  await delay();
  const accounts = loadOrSeed('room_accounts', SEED_ROOM_ACCOUNTS);

  // Quy tắc nghiệp vụ: Mỗi phòng đúng một tài khoản; form tạo phải chặn chọn phòng đã có tài khoản
  if (accounts.some((a) => a.roomId === data.roomId)) {
    throw new Error('Phòng này đã được gán một tài khoản phòng CLS. Mỗi phòng chỉ có đúng một tài khoản.');
  }

  const code = data.accountCode.trim().toLowerCase();
  if (accounts.some((a) => a.accountCode.toLowerCase() === code)) {
    throw new Error(`Tên tài khoản "${code}" đã tồn tại.`);
  }

  const newAccount: RoomAccountDto = {
    id: 'ra-' + Math.random().toString(36).substring(2, 9),
    appUserId: 'u-' + code,
    roomId: data.roomId,
    accountCode: code,
    isActive: true,
    createdAtUtc: new Date().toISOString(),
  };

  save('room_accounts', [...accounts, newAccount]);
  return newAccount;
}

// TODO(backend): POST /api/identity-patients/admin/room-accounts/{id}/terminate-session
export async function terminateRoomDeviceSession(roomAccountId: string): Promise<void> {
  await delay();
  const sessions = loadOrSeed('room_sessions', SEED_SESSIONS);
  const updated = sessions.map((s) => {
    if (s.roomAccountId === roomAccountId && s.isActive) {
      return { ...s, isActive: false, logoutAtUtc: new Date().toISOString() };
    }
    return s;
  });
  save('room_sessions', updated);
}

// ==================== 6. STAFF SHIFT ASSIGNMENTS ====================
// TODO(backend): GET /api/identity-patients/admin/shift-assignments
export async function getShiftAssignments(date?: string): Promise<StaffShiftAssignmentViewDto[]> {
  await delay();
  let shifts = loadOrSeed('shifts', SEED_SHIFTS);
  if (date) {
    shifts = shifts.filter((s) => s.shiftDate === date);
  }
  const staffs = loadOrSeed('staffs', SEED_STAFFS);
  const rooms = loadOrSeed('rooms', SEED_ROOMS);

  return shifts.map((sh) => {
    const st = staffs.find((s) => s.id === sh.staffId);
    const rm = rooms.find((r) => r.id === sh.roomId);

    return {
      ...sh,
      staffName: st?.fullName || 'N/A',
      staffCode: st?.staffCode || '',
      staffType: st?.staffType || 'Doctor',
      roomNumber: rm?.roomNumber || 'N/A',
      roomName: rm?.roomName || 'N/A',
    };
  });
}

// TODO(backend): POST /api/identity-patients/admin/shift-assignments
export async function createShiftAssignment(data: Omit<StaffShiftAssignmentDto, 'id'>): Promise<StaffShiftAssignmentDto> {
  await delay();
  const shifts = loadOrSeed('shifts', SEED_SHIFTS);

  // Kiểm tra trùng ca cùng nhân viên trong cùng ngày và buổi
  const hasConflict = shifts.some(
    (s) => s.staffId === data.staffId && s.shiftDate === data.shiftDate && s.shiftName === data.shiftName
  );
  if (hasConflict) {
    throw new Error('Nhân viên này đã được phân ca trực trong cùng khung giờ của ngày đã chọn.');
  }

  const newShift: StaffShiftAssignmentDto = {
    ...data,
    id: 'shift-' + Math.random().toString(36).substring(2, 9),
  };
  save('shifts', [...shifts, newShift]);
  return newShift;
}

// TODO(backend): DELETE /api/identity-patients/admin/shift-assignments/{id}
export async function deleteShiftAssignment(id: string): Promise<void> {
  await delay();
  const shifts = loadOrSeed('shifts', SEED_SHIFTS);
  save('shifts', shifts.filter((s) => s.id !== id));
}
