export interface VerificationItemDto {
  representativeId: string;
  patientId: string;
  appUserId: string;
  patientFullName: string;
  dateOfBirth: string;
  gender: string;
  maskedNationalId?: string | null;
  phoneNumber: string;
  address: string;
  relationship: string;
  verificationStatus: 'Pending' | 'Verified' | 'Rejected' | 'Revoked';
  createdAtUtc: string;
  revokedAtUtc?: string | null;
}

export interface VerificationDetailDto extends VerificationItemDto {
  isActive: boolean;
  patientCreatedAtUtc: string;
  representativeUserName: string;
  representativeEmail?: string | null;
  representativePhoneNumber?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
