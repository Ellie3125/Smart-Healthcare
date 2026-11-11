export interface SpecialtyOption {
  id: string;
  code: string;
  name: string;
}

export interface DoctorRankOption {
  id: string;
  code: string;
  name: string;
  consultationFee: number;
}

export interface DoctorSearchItem {
  doctorId: string;
  fullName: string;
  doctorRankId: string;
  doctorRankName: string;
  specialtyId: string;
  specialtyName: string;
  consultationFee: number;
  serviceDate: string;
  roomCode: string;
  roomName: string;
  totalSlots: number;
  remainingSlots: number;
  status: 'Open' | 'Closed' | 'Full';
  bio?: string;
}

export interface DoctorSearchPage {
  items: DoctorSearchItem[];
  page: number;
  pageSize: number;
  hasMore: boolean;
}

export interface DoctorDaySlotSummary {
  doctorId: string;
  fullName: string;
  doctorRankName: string;
  specialtyName: string;
  serviceDate: string;
  roomName: string;
  consultationFee: number;
  totalSlots: number;
  remainingSlots: number;
  onlineBookedCount: number;
  walkInReservedNote: string;
  status: 'Open' | 'Closed' | 'Full';
  sameDayBookedByProfileIds: string[];
}

export interface HoldBookingRequest {
  doctorId: string;
  serviceDate: string;
  patientProfileId?: string;
}

export interface HoldResponse {
  bookingAttemptId: string;
  doctorId: string;
  doctorName: string;
  specialtyName: string;
  roomName: string;
  serviceDate: string;
  patientProfileId: string;
  patientName: string;
  amount: number;
  expiresAt: string;
  status: 'Hold' | 'PendingPayment' | 'Confirmed' | 'Expired';
}

export interface OperationAccepted {
  operationId: string;
  bookingAttemptId: string;
}

export interface BookingOperationProgress {
  operationId: string;
  bookingAttemptId: string;
  status: 'Pending' | 'Succeeded' | 'Failed';
  errorCode?: string;
  errorMessage?: string;
  hold?: HoldResponse;
}

export interface BookingAttemptDetail {
  bookingAttemptId: string;
  status: 'Hold' | 'PendingPayment' | 'Confirmed' | 'Expired' | 'Failed';
  doctorId: string;
  doctorName: string;
  specialtyName: string;
  serviceDate: string;
  roomName: string;
  patientProfileId: string;
  patientName: string;
  amount: number;
  expiresAt: string;
  vnpayTxnRef?: string;
  appointmentId?: string;
  failureReason?: string;
}

export interface VnpayCheckoutResult {
  paymentUrl: string;
  vnpTxnRef: string;
  expiresAt: string;
}

export type ReturnVerifyState =
  | 'Verifying'
  | 'Paid'
  | 'Failed'
  | 'Expired'
  | 'AlreadyProcessed'
  | 'Unknown';

export interface VnpayReturnVerifyResult {
  state: ReturnVerifyState;
  vnpTxnRef: string;
  appointmentId?: string;
  clsOrderBatchId?: string;
  amount?: number;
  paidAt?: string;
  message: string;
  retryAllowed?: boolean;
}

export interface PatientAppointmentItem {
  id: string;
  appointmentCode: string;
  patientProfileId: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  doctorRankName: string;
  specialtyName: string;
  roomName: string;
  serviceDate: string;
  consultationFee: number;
  status: 'Confirmed' | 'CheckedIn' | 'Completed' | 'Cancelled' | 'NoShow' | 'PendingPayment';
  queueNumber?: number | null;
  checkedInAt?: string | null;
  canReschedule: boolean;
  canCancel: boolean;
  rescheduleBlockedReason?: string;
  cancelBlockedReason?: string;
}

export interface AppointmentTicketDetail {
  appointmentId: string;
  appointmentCode: string;
  qrPayload: string;
  patientName: string;
  doctorName: string;
  specialtyName: string;
  serviceDate: string;
  roomName: string;
  status: PatientAppointmentItem['status'];
  queueNumber?: number | null;
  checkInInstruction: string;
}

export interface RescheduleOption {
  doctorId: string;
  doctorName: string;
  doctorRankName: string;
  specialtyName: string;
  serviceDate: string;
  roomName: string;
  consultationFee: number;
  remainingSlots: number;
  sameFeeEligible: boolean;
}

export interface CancelRefundPreview {
  appointmentId: string;
  originalAmount: number;
  refundAmount: number;
  refundRatePercent: number;
  policyExplanation: string;
  eligibleToCancel: boolean;
  blockedReason?: string;
}
