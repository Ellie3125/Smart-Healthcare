export interface SpecialtyAdminDto {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface DoctorRankAdminDto {
  id: string;
  code: string;
  name: string;
  displayOrder: number;
  isActive: boolean;
}

export interface RoomAdminDto {
  id: string;
  roomNumber: string;
  roomName: string;
  roomType: string;
  departmentName: string;
  isActive: boolean;
}

export interface StaffAdminDto {
  id: string;
  appUserId: string;
  staffCode: string;
  fullName: string;
  staffType: string;
  userName: string;
  email: string;
  phoneNumber?: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface DoctorAdminDto {
  id: string;
  staffId: string;
  fullName: string;
  staffCode: string;
  specialtyId: string;
  specialtyName: string;
  doctorRankId: string;
  rankName: string;
  biography?: string | null;
  isActive: boolean;
}

export interface ShiftAssignmentDto {
  id: string;
  staffId: string;
  staffName: string;
  staffCode: string;
  roomId: string;
  roomNumber: string;
  roomName: string;
  shiftDate: string;
  shiftName: string;
  startsAtUtc: string;
  endsAtUtc: string;
  isShiftLeader: boolean;
}

export interface RoomSessionDto {
  sessionId: string;
  roomAccountId: string;
  deviceId: string;
  deviceIpAddress: string;
  loginAtUtc: string;
  logoutAtUtc?: string | null;
  isActive: boolean;
}

export interface RoomAccountDto {
  roomAccountId: string;
  roomId: string;
  appUserId: string;
  accountCode: string;
  userName: string;
  email: string;
  isActive: boolean;
  createdAtUtc: string;
}
