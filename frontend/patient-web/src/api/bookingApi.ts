import { patientGet, patientPost } from '../shared/lib/http';
import type {
  AppointmentTicketDetail,
  BookingAttemptDetail,
  BookingOperationProgress,
  CancelRefundPreview,
  DoctorDaySlotSummary,
  DoctorRankOption,
  DoctorSearchPage,
  HoldBookingRequest,
  HoldResponse,
  OperationAccepted,
  PatientAppointmentItem,
  RescheduleOption,
  SpecialtyOption,
  VnpayCheckoutResult,
  VnpayReturnVerifyResult,
} from '../types/booking';
import type { PublicDisplayBoardDto } from '../types/assistant';

/**
 * Scheduling & Booking API (Patient Web).
 * Các endpoint W1 nối trực tiếp contract `docs/api/scheduling.md`;
 * Các endpoint W2/W3 đánh dấu `// TODO: nối contract thật` và chạy đầy đủ qua MSW.
 */
export const bookingApi = {
  // TODO: nối contract thật cho danh mục chuyên khoa & học hàm/học vị công khai phía bệnh nhân
  listSpecialties: () => patientGet<SpecialtyOption[]>('/api/identity/public/specialties'),
  listDoctorRanks: () => patientGet<DoctorRankOption[]>('/api/identity/public/doctor-ranks'),

  // Real W1 Contract (`docs/api/scheduling.md`)
  searchDoctors: (params: {
    serviceDate: string;
    specialtyId?: string;
    doctorRankId?: string;
    page?: number;
    pageSize?: number;
  }) => patientGet<DoctorSearchPage>('/api/scheduling/patient/doctors', { params }),

  // TODO: nối contract thật chi tiết DoctorDay (30 slot model)
  getDoctorDayDetail: (doctorId: string, serviceDate: string) =>
    patientGet<DoctorDaySlotSummary>(`/api/scheduling/patient/doctors/${doctorId}/${serviceDate}`),

  // Real W1 Contract (`docs/api/scheduling.md`) - giữ chỗ 5 phút với Idempotency-Key
  createHold: (bookingAttemptId: string, body: HoldBookingRequest, idempotencyKey: string) =>
    patientPost<HoldResponse | OperationAccepted, HoldBookingRequest>(
      `/api/scheduling/patient/booking-attempts/${bookingAttemptId}/hold`,
      body,
      { idempotencyKey },
    ),

  getBookingAttempt: (bookingAttemptId: string) =>
    patientGet<BookingAttemptDetail>(`/api/scheduling/patient/booking-attempts/${bookingAttemptId}`),

  getOperationProgress: (operationId: string) =>
    patientGet<BookingOperationProgress>(`/api/scheduling/patient/operations/${operationId}`),

  // TODO: nối contract thật tạo phiên thanh toán VNPAY & xác minh Return URL
  createVnpayCheckout: (bookingAttemptId: string, idempotencyKey: string) =>
    patientPost<VnpayCheckoutResult, Record<string, never>>(
      `/api/billing/patient/booking-attempts/${bookingAttemptId}/vnpay-checkout`,
      {},
      { idempotencyKey },
    ),

  verifyVnpayReturn: (queryParams: Record<string, string>) =>
    patientGet<VnpayReturnVerifyResult>('/api/billing/patient/payments/vnpay/return-status', {
      params: queryParams,
    }),

  // TODO: nối contract thật recovery BookingAttempt đang dang dở
  listRecoverableAttempts: () =>
    patientGet<BookingAttemptDetail[]>('/api/scheduling/patient/booking-attempts/recoverable'),

  // TODO: nối contract thật P12-P15 Lịch của tôi, Vé QR, Đổi lịch, Hủy lịch
  listMyAppointments: (params?: { status?: string; page?: number; pageSize?: number }) =>
    patientGet<{ items: PatientAppointmentItem[]; hasMore: boolean }>(
      '/api/scheduling/patient/appointments',
      { params },
    ),

  getAppointmentTicket: (appointmentId: string) =>
    patientGet<AppointmentTicketDetail>(`/api/scheduling/patient/appointments/${appointmentId}/ticket`),

  listRescheduleOptions: (appointmentId: string, targetDate: string) =>
    patientGet<RescheduleOption[]>(
      `/api/scheduling/patient/appointments/${appointmentId}/reschedule-options`,
      { params: { targetDate } },
    ),

  rescheduleAppointment: (
    appointmentId: string,
    body: { targetDoctorId: string; targetServiceDate: string },
    idempotencyKey: string,
  ) =>
    patientPost<PatientAppointmentItem, { targetDoctorId: string; targetServiceDate: string }>(
      `/api/scheduling/patient/appointments/${appointmentId}/reschedule`,
      body,
      { idempotencyKey },
    ),

  previewCancelRefund: (appointmentId: string) =>
    patientGet<CancelRefundPreview>(`/api/scheduling/patient/appointments/${appointmentId}/cancel-preview`),

  cancelAppointment: (appointmentId: string, reason: string, idempotencyKey: string) =>
    patientPost<{ appointmentId: string; refundAmount: number; refundRequestId?: string }, { reason: string }>(
      `/api/scheduling/patient/appointments/${appointmentId}/cancel`,
      { reason },
      { idempotencyKey },
    ),

  // TODO: nối contract thật TV01 màn công khai không chứa PII
  getPublicDisplayBoard: () => patientGet<PublicDisplayBoardDto>('/api/scheduling/public/display'),
};
