export interface StaffNotificationItem {
  id: string;
  title: string;
  body: string;
  category: 'Queue' | 'Session' | 'Finance' | 'Exception' | 'System';
  status: 'Sent' | 'Read';
  createdAt: string;
  targetPath?: string;
}

export interface ServiceStationItem {
  id: string;
  code: string;
  name: string;
  stationType: 'Reception' | 'Cashier' | 'Vitals' | 'QueueDisplay';
  floor: string;
  isActive: boolean;
}

export interface DoctorDayQuotaItem {
  id: string;
  doctorId: string;
  doctorName: string;
  doctorRankName: string;
  specialtyName: string;
  roomCode: string;
  roomName: string;
  serviceDate: string;
  totalSlots: number;
  onlineBookedCount: number;
  walkInBookedCount: number;
  remainingSlots: number;
  status: 'Open' | 'Closed';
  version: number;
}

export interface MedicalServiceCatalogItem {
  id: string;
  code: string;
  name: string;
  category: 'Lab' | 'Imaging' | 'Functional';
  roomId: string;
  roomName: string;
  hasActiveRoom: boolean;
  unitPrice: number;
  isActive: boolean;
}

export interface PriceListItem {
  id: string;
  code: string;
  name: string;
  effectiveFrom: string;
  effectiveTo?: string;
  consultationPrices: { rankName: string; price: number }[];
  isActive: boolean;
}

export interface HospitalDeviceItem {
  id: string;
  deviceCode: string;
  deviceName: string;
  roomOrStationName: string;
  assignedDate: string;
  accountUsername?: string;
  status: 'Active' | 'Inactive';
}

export interface RolePermissionMatrixRow {
  role: string;
  capabilities: string[];
  restrictedNote: string;
}

export interface OperationalExceptionItem {
  id: string;
  type: 'CheckInExpired' | 'VnpayPendingTimeout' | 'DoctorSessionCancelled' | 'ClsDelay';
  referenceCode: string;
  patientName: string;
  phoneMasked: string;
  detail: string;
  status: 'Open' | 'Resolved';
  createdAt: string;
}

export interface AdminRefundItem {
  id: string;
  referenceCode: string;
  patientName: string;
  reason: string;
  amount: number;
  payoutChannel: string;
  status: 'Pending' | 'Approved' | 'Refunded' | 'Rejected';
  createdAt: string;
  version: number;
}

export interface PaymentReconcileRow {
  id: string;
  vnpTxnRef: string;
  appointmentOrBatchCode: string;
  amount: number;
  localStatus: 'PendingPayment' | 'Paid' | 'Failed' | 'Unknown';
  gatewayStatus: 'Matched' | 'Mismatch' | 'Unknown';
  checkedAt?: string;
}

export interface ReportSnapshotDto {
  dateFrom: string;
  dateTo: string;
  totalAppointments: number;
  checkedInCount: number;
  cancelledCount: number;
  totalRevenue: number;
  totalRefunded: number;
  dailySeries: {
    date: string;
    bookings: number;
    checkedIn: number;
    revenue: number;
  }[];
}

export interface AuditLogEntry {
  id: string;
  occurredAt: string;
  actorName: string;
  actorRole: string;
  action: string;
  entityType: string;
  entityId: string;
  patientCccd?: string;
  patientPhone?: string;
  beforeJson: Record<string, unknown> | null;
  afterJson: Record<string, unknown> | null;
}

export interface ClsWaiverRequestItem {
  id: string;
  encounterCode: string;
  patientName: string;
  serviceName: string;
  originalPrice: number;
  requestedWaiverAmount: number;
  reason: string;
  status: 'Pending' | 'Approved' | 'Rejected';
}

export interface ChatbotSpecialtyConfigItem {
  id: string;
  specialtyName: string;
  symptomKeywords: string[];
  redFlagKeywords: string[];
  disclaimerText: string;
  isEnabled: boolean;
}

// Reception DTOs
export interface ReceptionSearchResult {
  id: string;
  patientName: string;
  phone: string;
  cccdMasked: string;
  dateOfBirth: string;
  verificationStatus: 'Pending' | 'Verified' | 'Rejected';
  appointmentId?: string;
  appointmentCode?: string;
  doctorName?: string;
  serviceDate?: string;
  appointmentStatus?: string;
  checkInDeadline?: string;
  isPastCheckInDeadline?: boolean;
  queueNumber?: number | null;
}

export interface CashierPayableItem {
  id: string;
  referenceCode: string;
  patientName: string;
  itemType: 'AppointmentFee' | 'ClsBatch';
  description: string;
  amount: number;
  status: 'PendingPayment' | 'Paid';
  paidAt?: string;
  receiptCode?: string;
}

export interface ShiftReconcileSummary {
  shiftDate: string;
  cashierName: string;
  expectedCashCollected: number;
  expectedCashRefunded: number;
  netExpectedCash: number;
  isClosed: boolean;
  closedAt?: string;
}

export interface ReceptionCallTaskItem {
  id: string;
  patientName: string;
  phone: string;
  reason: string;
  doctorName: string;
  serviceDate: string;
  status: 'Pending' | 'Called' | 'Resolved';
  note?: string;
}

// Nurse DTOs
export interface NurseQueueItem {
  encounterId: string;
  ticketNumber: string;
  patientName: string;
  yearOfBirth: number;
  doctorName: string;
  specialtyName: string;
  status: 'Waiting' | 'Called' | 'InProgress' | 'Completed';
  version: number;
}

export interface VitalsRecordDto {
  encounterId: string;
  ticketNumber: string;
  patientName: string;
  version: number;
  systolicBp: string;
  diastolicBp: string;
  heartRate: string;
  temperatureC: string;
  spo2: string;
  respiratoryRate: string;
  weightKg: string;
  heightCm: string;
  nurseNote: string;
}

// Doctor DTOs
export interface DoctorQueueEntry {
  encounterId: string;
  ticketNumber: string;
  patientName: string;
  queueKind: 'Initial' | 'ClsReturn';
  status: 'Waiting' | 'InService' | 'AwaitingCls' | 'Published' | 'Amended';
  checkedInAt: string;
}

export interface Icd10CodeOption {
  code: string;
  name: string;
}

export interface DrugCatalogItem {
  id: string;
  name: string;
  activeIngredient: string;
  defaultDosage: string;
  unit: string;
}

export interface DoctorPrescriptionRow {
  drugId: string;
  drugName: string;
  activeIngredient: string;
  dosage: string;
  frequency: string;
  durationDays: number;
  quantity: number;
  unit: string;
  usageInstruction: string;
}

export interface DoctorClsResultView {
  id: string;
  serviceCode: string;
  serviceName: string;
  status: 'PendingPayment' | 'Waiting' | 'InProgress' | 'Draft' | 'Published';
  summaryText?: string;
  technicalConclusion?: string;
  readingDoctorName?: string;
  attachments: { fileId: string; fileName: string; mimeType: string }[];
}

export interface DoctorEncounterWorkspaceDto {
  encounterId: string;
  encounterCode: string;
  ticketNumber: string;
  patientName: string;
  patientAge: number;
  patientGender: string;
  status: 'InProgress' | 'AwaitingCls' | 'ClsCompleted' | 'Published' | 'Amended';
  version: number;
  vitalsSummary?: string;
  symptoms: string;
  clinicalNotes: string;
  diagnoses: Icd10CodeOption[];
  conclusion: string;
  followUpInstruction: string;
  clsOrders: DoctorClsResultView[];
  prescriptionItems: DoctorPrescriptionRow[];
  amendmentHistory: { version: number; amendedAt: string; summaryDiff: string }[];
}

// CLS Room DTOs
export interface ClsRoomQueueItem {
  orderItemId: string;
  ticketNumber: string;
  patientName: string;
  serviceCode: string;
  serviceName: string;
  serviceDate: string;
  queueStatus: 'Waiting' | 'Called' | 'Deferred' | 'InProgress' | 'Completed';
  resultStatus: 'NotStarted' | 'Draft' | 'Published';
  readingDoctorName: string;
  version: number;
}

export interface ClsRoomResultDraftDto {
  orderItemId: string;
  ticketNumber: string;
  patientName: string;
  serviceCode: string;
  serviceName: string;
  resultStatus: 'Draft' | 'Published';
  summaryText: string;
  technicalConclusion: string;
  readingDoctorName: string;
  availableShiftDoctors: string[];
  version: number;
  draftFiles: { fileId: string; fileName: string; mimeType: string; sizeBytes: number }[];
}
