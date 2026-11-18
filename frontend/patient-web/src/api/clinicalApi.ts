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

  // TODO: nối contract thật P17 Danh sách đợt chỉ định CLS chờ thanh toán & tạo link VNPAY
  listClsPaymentBatches: () => patientGet<ClsPaymentBatch[]>('/api/billing/patient/cls-batches'),

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
