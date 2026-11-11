export interface JourneyStep {
  id: string;
  title: string;
  location: string;
  status: 'Completed' | 'InProgress' | 'Waiting' | 'Called' | 'Deferred';
  queueNumber?: string;
  detail?: string;
  updatedAt?: string;
}

export interface TodayJourneyDto {
  patientProfileId: string;
  patientName: string;
  serviceDate: string;
  appointmentId?: string;
  encounterStatus?: string;
  currentCallText?: string;
  unpaidClsCount: number;
  steps: JourneyStep[];
}

export interface ClsOrderItem {
  id: string;
  serviceCode: string;
  serviceName: string;
  roomName: string;
  unitPrice: number;
  waiverAmount: number;
  netPrice: number;
  status: 'PendingPayment' | 'Paid' | 'Waiting' | 'InProgress' | 'Published' | 'Cancelled';
}

export interface ClsPaymentBatch {
  batchId: string;
  encounterId: string;
  appointmentCode: string;
  createdAt: string;
  expiresAt: string;
  status: 'PendingPayment' | 'Paid' | 'Expired' | 'Cancelled';
  totalGrossAmount: number;
  totalWaiverAmount: number;
  totalPayableAmount: number;
  items: ClsOrderItem[];
}

export interface EncounterCostSummary {
  encounterId: string;
  serviceDate: string;
  doctorName: string;
  specialtyName: string;
  totalPaid: number;
  totalApprovedRefund: number;
  netHeld: number;
  totalWaived: number;
  totalCompletedRefund: number;
  lineItems: {
    id: string;
    category: string;
    description: string;
    grossAmount: number;
    waiverAmount: number;
    netAmount: number;
    status: string;
  }[];
}

export type RefundPayoutChannel = 'CashCounter' | 'BankTransfer' | 'OriginalVnpay';

export interface RefundRequestItem {
  id: string;
  referenceCode: string;
  reason: string;
  amount: number;
  status: 'Pending' | 'Approved' | 'Refunded' | 'Rejected';
  payoutChannel?: RefundPayoutChannel | null;
  bankAccountSummary?: string | null;
  createdAt: string;
  updatedAt: string;
  rejectionReason?: string;
}

export interface PatientEncounterSummary {
  id: string;
  encounterCode: string;
  patientProfileId: string;
  patientName: string;
  serviceDate: string;
  doctorName: string;
  specialtyName: string;
  status: 'InProgress' | 'AwaitingCls' | 'ClsCompleted' | 'Published' | 'Amended';
  isPublished: boolean;
  primaryDiagnosis?: string;
  hasClsResults: boolean;
  hasPrescription: boolean;
}

export interface PatientEncounterDetail {
  id: string;
  encounterCode: string;
  serviceDate: string;
  patientName: string;
  doctorName: string;
  specialtyName: string;
  status: 'InProgress' | 'AwaitingCls' | 'ClsCompleted' | 'Published' | 'Amended';
  isPublished: boolean;
  version: number;
  publishedAt?: string;
  amendedNote?: string;
  vitals?: {
    systolicBp?: number;
    diastolicBp?: number;
    heartRate?: number;
    temperatureC?: number;
    spo2?: number;
    weightKg?: number;
    heightCm?: number;
    recordedAt?: string;
  };
  symptoms?: string;
  clinicalNotes?: string;
  diagnoses?: { icdCode: string; description: string; isPrimary: boolean }[];
  conclusion?: string;
  followUpInstruction?: string;
  clsResults: ClsResultDetail[];
  hasPrescription: boolean;
}

export interface ClsAttachmentMeta {
  fileId: string;
  fileName: string;
  mimeType: string;
  sizeBytes: number;
  uploadedAt: string;
}

export interface ClsResultDetail {
  id: string;
  serviceCode: string;
  serviceName: string;
  status: 'Draft' | 'Published' | 'Amended';
  summaryText?: string;
  technicalConclusion?: string;
  readingDoctorName?: string;
  publishedAt?: string;
  attachments: ClsAttachmentMeta[];
}

export interface PrescriptionLine {
  id: string;
  drugName: string;
  activeIngredient: string;
  dosage: string;
  frequency: string;
  durationDays: number;
  quantity: number;
  unit: string;
  usageInstruction: string;
}

export interface PatientPrescriptionDetail {
  encounterId: string;
  encounterCode: string;
  patientName: string;
  doctorName: string;
  issuedAt: string;
  disclaimer: string;
  notes?: string;
  items: PrescriptionLine[];
}
