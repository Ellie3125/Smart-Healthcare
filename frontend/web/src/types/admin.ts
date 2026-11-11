export type StaffType = 'Doctor' | 'Nurse' | 'Receptionist' | 'Technician';

export type RoomType = 'Clinical' | 'Laboratory' | 'XRay' | 'Ultrasound' | 'Endoscopy';

export type RankCode = 'SpecialistI' | 'SpecialistII' | 'Master' | 'Doctor' | 'AssociateProf' | 'Professor';

export type ShiftName = 'Morning' | 'Afternoon' | 'FullDay';

export interface StaffDto {
  id: string;
  appUserId: string;
  staffCode: string;
  fullName: string;
  staffType: StaffType;
  isActive: boolean;
  createdAtUtc: string;
}

export interface DoctorDto {
  id: string;
  staffId: string;
  specialtyId: string;
  doctorRankId: string;
  defaultRoomId?: string | null;
  biography?: string | null;
  isActive: boolean;
}

export interface DoctorViewDto {
  id: string;
  staffId: string;
  fullName: string;
  staffCode: string;
  specialtyId: string;
  specialtyCode: string;
  specialtyName: string;
  doctorRankId: string;
  rankName: string;
  defaultRoomId?: string | null;
  defaultRoomNumber?: string | null;
  biography?: string | null;
  isActive: boolean;
}

export interface SpecialtyDto {
  id: string;
  specialtyCode: string;
  specialtyName: string;
  description?: string | null;
  isActive: boolean;
}

export interface DoctorRankDto {
  id: string;
  rankCode: RankCode;
  rankName: string;
  displayOrder: number;
  isActive: boolean;
}

export interface RoomDto {
  id: string;
  roomNumber: string;
  roomName: string;
  roomType: RoomType;
  departmentName: string;
  isActive: boolean;
}

export interface RoomAccountDto {
  id: string;
  appUserId: string;
  roomId: string;
  accountCode: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface RoomAccountViewDto extends RoomAccountDto {
  roomNumber: string;
  roomName: string;
  roomType: RoomType;
  departmentName: string;
  activeSession?: RoomDeviceSessionDto | null;
}

export interface RoomDeviceSessionDto {
  id: string;
  roomAccountId: string;
  deviceId: string;
  deviceIpAddress: string;
  loginAtUtc: string;
  logoutAtUtc?: string | null;
  isActive: boolean;
}

export interface StaffShiftAssignmentDto {
  id: string;
  staffId: string;
  roomId: string;
  shiftDate: string; // YYYY-MM-DD
  shiftName: ShiftName;
  startsAtUtc: string;
  endsAtUtc: string;
  isShiftLeader: boolean;
}

export interface StaffShiftAssignmentViewDto extends StaffShiftAssignmentDto {
  staffName: string;
  staffCode: string;
  staffType: StaffType;
  roomNumber: string;
  roomName: string;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
