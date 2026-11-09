import {
  PatientProfileView,
  CreateRelativeProfileFormInput,
  RegisterPatientRequest,
  LoginResponse,
} from '../types/patient';

// TODO(backend): Đây là lớp mock tạm thời cho các endpoint chưa có trên backend.
// Khi backend bổ sung controller /api/identity-patients/..., chuyển các hàm này sang gọi apiClient.

const STORAGE_KEY = 'sh_mock_patient_profiles_v1';

const INITIAL_PROFILES: PatientProfileView[] = [
  {
    isSelf: true,
    relationship: 'Self',
    verificationStatus: 'Verified',
    patient: {
      id: 'p-001-self',
      ownerUserId: 'u-patient-001',
      fullName: 'Nguyễn Minh Anh',
      dateOfBirth: '1992-08-14',
      gender: 'Male',
      nationalId: '001092004521',
      phoneNumber: '0912 345 678',
      address: 'Số 45 Tràng Tiền, Quận Hoàn Kiếm, Hà Nội',
      createdAtUtc: '2025-01-01T08:00:00Z',
    },
    representative: null,
  },
  {
    isSelf: false,
    relationship: 'Parent',
    verificationStatus: 'Verified',
    patient: {
      id: 'p-002-mother',
      ownerUserId: null,
      fullName: 'Nguyễn Thị Lan',
      dateOfBirth: '1962-05-20',
      gender: 'Female',
      nationalId: '',
      phoneNumber: '0912 345 678',
      address: 'Số 45 Tràng Tiền, Quận Hoàn Kiếm, Hà Nội',
      createdAtUtc: '2025-01-05T09:30:00Z',
    },
    representative: {
      id: 'rep-002',
      appUserId: 'u-patient-001',
      patientId: 'p-002-mother',
      relationship: 'Parent',
      verificationStatus: 'Verified',
      isActive: true,
      createdAtUtc: '2025-01-05T09:30:00Z',
    },
  },
  {
    isSelf: false,
    relationship: 'Child',
    verificationStatus: 'Pending',
    patient: {
      id: 'p-003-child',
      ownerUserId: null,
      fullName: 'Nguyễn Bảo An',
      dateOfBirth: '2018-11-05',
      gender: 'Male',
      nationalId: '',
      phoneNumber: '0912 345 678',
      address: 'Số 45 Tràng Tiền, Quận Hoàn Kiếm, Hà Nội',
      createdAtUtc: '2025-02-10T14:15:00Z',
    },
    representative: {
      id: 'rep-003',
      appUserId: 'u-patient-001',
      patientId: 'p-003-child',
      relationship: 'Child',
      verificationStatus: 'Pending',
      isActive: true,
      createdAtUtc: '2025-02-10T14:15:00Z',
    },
  },
];

function getStoredProfiles(): PatientProfileView[] {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(INITIAL_PROFILES));
    return INITIAL_PROFILES;
  }
  try {
    return JSON.parse(raw);
  } catch {
    return INITIAL_PROFILES;
  }
}

function saveProfiles(profiles: PatientProfileView[]) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(profiles));
}

// TODO(backend): Thay thế bằng POST /auth/patient/register
export async function mockRegisterPatient(req: RegisterPatientRequest): Promise<LoginResponse> {
  await new Promise((res) => setTimeout(res, 300));
  const newUserId = 'u-' + Math.random().toString(36).substring(2, 9);
  const selfPatientId = 'p-' + Math.random().toString(36).substring(2, 9);

  const selfProfile: PatientProfileView = {
    isSelf: true,
    relationship: 'Self',
    verificationStatus: 'Pending', // Hồ sơ mới tạo bắt đầu ở trạng thái Pending
    patient: {
      id: selfPatientId,
      ownerUserId: newUserId,
      fullName: req.fullName,
      dateOfBirth: req.dateOfBirth,
      gender: req.gender,
      nationalId: req.nationalId || null,
      phoneNumber: req.phoneNumber,
      address: req.address,
      createdAtUtc: new Date().toISOString(),
    },
  };

  const current = getStoredProfiles();
  saveProfiles([selfProfile, ...current.filter((p) => !p.isSelf)]);

  return {
    token: 'mock-patient-jwt-token-' + newUserId,
    role: 'Patient',
    userId: newUserId,
    userName: req.userName,
  };
}

// TODO(backend): Thay thế bằng GET /api/identity-patients/patients/me
export async function mockGetPatientProfiles(): Promise<PatientProfileView[]> {
  await new Promise((res) => setTimeout(res, 200));
  return getStoredProfiles();
}

// TODO(backend): Thay thế bằng POST /api/identity-patients/patients
export async function mockCreateRelativeProfile(
  input: CreateRelativeProfileFormInput
): Promise<PatientProfileView> {
  await new Promise((res) => setTimeout(res, 250));
  const newPatientId = 'p-' + Math.random().toString(36).substring(2, 9);
  const newRepId = 'rep-' + Math.random().toString(36).substring(2, 9);

  // Quy tắc nghiệp vụ: Hồ sơ mới tạo luôn ở trạng thái Pending
  const newProfile: PatientProfileView = {
    isSelf: false,
    relationship: input.relationship,
    verificationStatus: 'Pending',
    patient: {
      id: newPatientId,
      ownerUserId: null,
      fullName: input.fullName.trim(),
      dateOfBirth: input.dateOfBirth,
      gender: input.gender,
      nationalId: input.nationalId?.trim() || null,
      phoneNumber: input.phoneNumber.trim(),
      address: input.address?.trim() || 'Theo hộ gia đình chủ tài khoản',
      createdAtUtc: new Date().toISOString(),
    },
    representative: {
      id: newRepId,
      appUserId: 'u-patient-001',
      patientId: newPatientId,
      relationship: input.relationship,
      verificationStatus: 'Pending',
      isActive: true,
      createdAtUtc: new Date().toISOString(),
    },
  };

  const current = getStoredProfiles();
  const updated = [...current, newProfile];
  saveProfiles(updated);
  return newProfile;
}

// TODO(backend): Thay thế bằng PUT /api/identity-patients/patients/{id}
export async function mockUpdatePatientProfile(
  patientId: string,
  input: Partial<CreateRelativeProfileFormInput>
): Promise<PatientProfileView> {
  await new Promise((res) => setTimeout(res, 250));
  const current = getStoredProfiles();
  const targetIndex = current.findIndex((p) => p.patient.id === patientId);
  if (targetIndex === -1) {
    throw new Error('Không tìm thấy hồ sơ bệnh nhân cần cập nhật.');
  }

  const existing = current[targetIndex];
  // Không cho sửa hồ sơ nếu đã bị Rejected hoặc Revoked
  if (existing.verificationStatus === 'Rejected' || existing.verificationStatus === 'Revoked') {
    throw new Error('Hồ sơ đã bị vô hiệu hoá, không thể chỉnh sửa.');
  }

  const updatedProfile: PatientProfileView = {
    ...existing,
    relationship: input.relationship || existing.relationship,
    patient: {
      ...existing.patient,
      fullName: input.fullName !== undefined ? input.fullName.trim() : existing.patient.fullName,
      dateOfBirth: input.dateOfBirth || existing.patient.dateOfBirth,
      gender: input.gender || existing.patient.gender,
      nationalId: input.nationalId !== undefined ? (input.nationalId.trim() || null) : existing.patient.nationalId,
      phoneNumber: input.phoneNumber !== undefined ? input.phoneNumber.trim() : existing.patient.phoneNumber,
      address: input.address !== undefined ? input.address.trim() : existing.patient.address,
    },
  };

  current[targetIndex] = updatedProfile;
  saveProfiles(current);
  return updatedProfile;
}
