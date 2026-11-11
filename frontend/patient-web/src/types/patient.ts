export type VerificationStatus = 'Pending' | 'Verified' | 'Rejected' | 'Revoked';
export type Relationship = 'Self' | 'Child' | 'Parent' | 'Spouse' | 'Other';
export type Gender = 'Male' | 'Female' | 'Other';

export interface PatientProfileDto {
  patientId: string;
  representativeId?: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  nationalId?: string | null;
  phoneNumber: string;
  address: string;
  relationship: Relationship;
  verificationStatus: VerificationStatus;
  isActive?: boolean;
  isRepresentativeActive?: boolean;
  createdAtUtc?: string;
  verifiedAtUtc?: string | null;
  rejectionReason?: string | null;
}

export interface CreatePatientProfileRequest {
  fullName: string;
  dateOfBirth: string;
  gender: Gender;
  nationalId?: string;
  phoneNumber: string;
  address: string;
  relationship: Relationship;
}

export interface UpdatePatientProfileRequest {
  phoneNumber: string;
  address: string;
  fullName?: string;
  dateOfBirth?: string;
  gender?: Gender;
  nationalId?: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  extensions?: Record<string, unknown>;
}
