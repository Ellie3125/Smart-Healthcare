import { patientDownloadBlob, patientGet, patientPost } from '../shared/lib/http';
import type {
  ClsPaymentBatch,
  EncounterCostSummary,
  PatientEncounterDetail,
  PatientEncounterSummary,
  PatientPrescriptionDetail,
  TodayJourneyDto,
} from '../types/clinical';
import type { VnpayCheckoutResult } from '../types/booking';
import { PatientApiError } from '../shared/lib/errors';

interface ApiPrescription {
  prescriptionCode: string;
  generalInstructions: string;
  issuedAtUtc: string | null;
  publishedAtUtc: string | null;
  items: {
    id: string;
    medicationNameSnapshot: string;
    activeIngredientSnapshot: string;
    dosage: string;
    frequency: string;
    durationDays: number;
    quantity: number;
    usageInstructions: string;
  }[];
}

// PatientClsBatch của Billing (Billing.Application/PatientClsPaymentService.cs); enum có thể về dạng số.
const CLS_BATCH_STATES = ['PendingPayment', 'Paid', 'Expired', 'Cancelled'] as const;
const CHARGE_STATES = ['Pending', 'PartiallyPaid', 'Paid', 'Cancelled', 'Waived'] as const;
type ChargeState = (typeof CHARGE_STATES)[number];

interface ApiClsBatch {
  serviceOrderId: string;
  encounterId: string;
  orderedAtUtc: string;
  payByUtc: string;
  status: ClsPaymentBatch['status'] | number;
  totalAmount: number;
  outstanding: number;
  items: {
    chargeId: string;
    serviceName: string;
    unitPrice: number;
    totalAmount: number;
    status: ChargeState | number;
  }[];
}

const enumName = <T extends string>(names: readonly T[], value: T | number): T =>
  typeof value === 'number' ? names[value] : value;

const ITEM_STATUS: Record<ChargeState, ClsPaymentBatch['items'][number]['status']> = {
  Pending: 'PendingPayment',
  PartiallyPaid: 'PendingPayment',
  Paid: 'Paid',
  Cancelled: 'Cancelled',
  Waived: 'Cancelled',
};

// Billing không trả mã lịch hẹn, mã dịch vụ, tên phòng (thuộc Scheduling/Clinical) nên các trường đó để trống.
const toClsBatch = (b: ApiClsBatch): ClsPaymentBatch => ({
  batchId: b.serviceOrderId,
  encounterId: b.encounterId,
  appointmentCode: '',
  createdAt: b.orderedAtUtc,
  expiresAt: b.payByUtc,
  status: enumName(CLS_BATCH_STATES, b.status),
  totalGrossAmount: b.totalAmount,
  totalWaiverAmount: 0,
  totalPayableAmount: b.outstanding,
  items: b.items.map((i) => ({
    id: i.chargeId,
    serviceCode: '',
    serviceName: i.serviceName,
    roomName: '',
    unitPrice: i.unitPrice,
    waiverAmount: 0,
    netPrice: i.totalAmount,
    status: ITEM_STATUS[enumName(CHARGE_STATES, i.status)],
  })),
});

/**
 * Clinical, CLS Billing, Refunds & Records API (Patient Web).
 * Endpoint đánh dấu `// TODO: nối contract thật` gọi thẳng Backend; route chưa có thì trả lỗi tới khi Backend làm xong.
 */
export const clinicalApi = {
  // TODO: nối contract thật P16 Hành trình hôm nay
  getTodayJourney: (patientProfileId?: string) =>
    patientGet<TodayJourneyDto>('/api/clinical/patient/today-journey', {
      params: patientProfileId ? { patientProfileId } : undefined,
    }),

  // P17: Billing bắt buộc patientId vì một tài khoản quản lý nhiều hồ sơ (PatientPaymentEndpoints.ListClsBatchesAsync).
  listClsPaymentBatches: async (patientId: string) =>
    (await patientGet<ApiClsBatch[]>('/api/billing/patient/cls-batches', { params: { patientId } })).map(toClsBatch),

  createClsVnpayCheckout: (batchId: string, idempotencyKey: string) =>
    patientPost<VnpayCheckoutResult, Record<string, never>>(
      `/api/billing/patient/cls-batches/${batchId}/vnpay-checkout`,
      {},
      { idempotencyKey },
    ),

  // TODO: nối contract thật P18 Tổng quan chi phí đợt khám (5 chỉ số)
  getEncounterCostSummary: (encounterId: string) =>
    patientGet<EncounterCostSummary>(`/api/billing/patient/encounters/${encounterId}/cost-summary`),

  // Đã nối endpoint ghép lịch sử khám kênh Patient qua DocumentsReporting (A)
  listEncounters: (params?: { patientProfileId?: string; page?: number; pageSize?: number }) =>
    patientGet<{ items: PatientEncounterSummary[]; hasMore: boolean; blockedPendingProfile?: boolean }>(
      '/api/documents/patient/encounters',
      { params },
    ),

  getEncounterDetail: (encounterId: string) =>
    patientGet<PatientEncounterDetail>(`/api/documents/patient/encounters/${encounterId}`),

  // Tải file CLS thông qua stream/blob của Backend, tuyệt đối không trả/render trực tiếp URL object storage
  downloadClsAttachment: (resultId: string, fileId: string) =>
    patientDownloadBlob(`/api/documents/patient/cls-results/${resultId}/files/${fileId}/download`),

  // Clinical trả mảng đơn đã phát hành của lượt khám; trang hiển thị đơn mới nhất.
  getPrescription: async (encounterId: string): Promise<PatientPrescriptionDetail> => {
    const list = await patientGet<ApiPrescription[]>(`/api/clinical/patient/encounters/${encounterId}/prescriptions`);
    const rx = [...list].sort((x, y) => (y.issuedAtUtc ?? '').localeCompare(x.issuedAtUtc ?? ''))[0];
    if (!rx) throw new PatientApiError(404, 'NOT_FOUND', 'Lượt khám này chưa có đơn thuốc được công bố.');
    return {
      encounterId,
      encounterCode: rx.prescriptionCode,
      patientName: '',
      doctorName: '',
      issuedAt: rx.issuedAtUtc ?? rx.publishedAtUtc ?? '',
      disclaimer: 'Hệ thống bệnh viện không bán thuốc trực tuyến. Đơn thuốc chỉ có giá trị mua tại các nhà thuốc đạt chuẩn GPP.',
      notes: rx.generalInstructions || undefined,
      items: rx.items.map((i) => ({
        id: i.id,
        drugName: i.medicationNameSnapshot,
        activeIngredient: i.activeIngredientSnapshot,
        dosage: i.dosage,
        frequency: i.frequency,
        durationDays: i.durationDays,
        quantity: i.quantity,
        unit: '',
        usageInstruction: i.usageInstructions,
      })),
    };
  },
};
