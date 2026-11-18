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

  getPrescription: (encounterId: string) =>
    patientGet<PatientPrescriptionDetail>(`/api/medication/patient/encounters/${encounterId}/prescription`),
};
