export type Gender = 'Male' | 'Female' | 'Other';

export type Relationship = 'Self' | 'Child' | 'Parent' | 'Spouse' | 'Other';

export type VerificationStatus = 'Pending' | 'Verified' | 'Rejected' | 'Revoked';

export interface PatientDto {
  id: string;
  ownerUserId?: string | null;
  fullName: string;
  dateOfBirth: string; // ISO date string (YYYY-MM-DD)
  gender: Gender;
  nationalId?: string | null;
  phoneNumber: string;
  address: string;
  createdAtUtc: string;
}

export interface PatientRepresentativeDto {
  id: string;
  appUserId: string;
  patientId: string;
  relationship: Relationship;
  verificationStatus: VerificationStatus;
  isActive: boolean;
  createdAtUtc: string;
  revokedAtUtc?: string | null;
}

export interface PatientProfileView {
  patient: PatientDto;
  representative?: PatientRepresentativeDto | null;
  isSelf: boolean;
  relationship: Relationship;
  verificationStatus: VerificationStatus;
}

export interface LoginRequest {
  userName: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  role: string;
  userId: string;
  userName: string;
}

export interface CurrentUserDto {
  userId: string;
  userName: string;
  role: string;
}

export interface RegisterPatientRequest {
  userName: string;
  password: string;
  fullName: string;
  dateOfBirth: string;
  gender: Gender;
  nationalId?: string;
  phoneNumber: string;
  address: string;
}

export interface CreateRelativeProfileFormInput {
  fullName: string;
  relationship: Relationship;
  dateOfBirth: string;
  gender: Gender;
  phoneNumber: string;
  nationalId?: string;
  address?: string;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
