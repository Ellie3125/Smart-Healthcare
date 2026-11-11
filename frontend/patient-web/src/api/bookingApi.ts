import { patientGet, patientPost } from '../shared/lib/http';
import { PatientApiError } from '../shared/lib/errors';
import type {
  AppointmentTicketDetail,
  BookingAttemptDetail,
  BookingOperationProgress,
  BookingProgress,
  CancelRefundPreview,
  DoctorDaySlotSummary,
  DoctorRankOption,
  DoctorSearchItem,
  DoctorSearchPage,
  HoldBookingRequest,
  HoldResult,
  PaymentStarted,
  PatientAppointmentItem,
  RescheduleOption,
  SpecialtyOption,
  ReturnVerifyState,
  VnpayCheckoutResult,
  VnpayReturnVerifyResult,
} from '../types/booking';
import type { PublicDisplayBoardDto } from '../types/assistant';

/**
 * Scheduling & Booking API (Patient Web).
 * Các endpoint W1 nối trực tiếp contract `docs/api/scheduling.md`;
 * Các endpoint W2/W3 đánh dấu `// TODO: nối contract thật` và chạy đầy đủ qua MSW.
 */
/** Khóa sessionStorage giữ PaymentIntent đang chờ, để màn Return URL hỏi lại trạng thái từ Backend. */
export const PENDING_PAYMENT_KEY = 'patient_pending_payment';

// Dạng thật của `GET /api/scheduling/patient/doctors` (`docs/api/scheduling.md` §4.3).
interface ApiDoctorItem {
  doctorId: string;
  fullName: string;
  doctorRankId: string;
  specialtyId: string;
  availableSlots: number;
  examFee: number;
  totalSlots?: number;
  doctorRankName?: string | null;
  specialtyName?: string | null;
}

interface ApiDoctorPage {
  serviceDate: string;
  page: number;
  pageSize: number;
  hasMore: boolean;
  items: ApiDoctorItem[];
}

interface ApiPaymentStatus {
  paymentIntentId: string;
  intentNumber: string;
  amount: number;
  status: 'Pending' | 'Processing' | 'Succeeded' | 'Failed' | 'Unknown' | 'Expired' | 'Cancelled';
  paidAtUtc: string | null;
}

const toDoctorItem = (x: ApiDoctorItem, serviceDate: string): DoctorSearchItem => ({
  doctorId: x.doctorId,
  fullName: x.fullName,
  doctorRankId: x.doctorRankId,
  doctorRankName: x.doctorRankName ?? '',
  specialtyId: x.specialtyId,
  specialtyName: x.specialtyName ?? '',
  consultationFee: x.examFee,
  serviceDate,
  roomCode: '',
  roomName: '',
  totalSlots: x.totalSlots ?? x.availableSlots,
  remainingSlots: x.availableSlots,
  // Backend chỉ trả phiên còn nhận đặt; hết suất là availableSlots = 0.
  status: x.availableSlots > 0 ? 'Open' : 'Full',
});

const RETURN_STATES: Record<ApiPaymentStatus['status'], { state: ReturnVerifyState; message: string }> = {
  Pending: { state: 'Verifying', message: 'Hệ thống đang chờ kết quả từ cổng thanh toán VNPAY.' },
  Processing: { state: 'Verifying', message: 'Hệ thống đang xác minh giao dịch với cổng thanh toán VNPAY.' },
  Succeeded: { state: 'Paid', message: 'Thanh toán thành công. Lịch khám sẽ hiện trong mục Lịch của tôi sau khi được xác nhận.' },
  Failed: { state: 'Failed', message: 'Giao dịch thanh toán không thành công.' },
  Cancelled: { state: 'Failed', message: 'Giao dịch thanh toán đã bị hủy.' },
  Expired: { state: 'Expired', message: 'Giao dịch đã quá thời gian thanh toán.' },
  Unknown: { state: 'Unknown', message: 'Chưa xác định được trạng thái giao dịch. Vui lòng kiểm tra lại sau ít phút.' },
};

async function searchDoctors(params: {
  serviceDate: string;
  specialtyId?: string;
  doctorRankId?: string;
  page?: number;
  pageSize?: number;
}): Promise<DoctorSearchPage> {
  const res = await patientGet<ApiDoctorPage>('/api/scheduling/patient/doctors', { params });
  return {
    page: res.page,
    pageSize: res.pageSize,
    hasMore: res.hasMore,
    items: res.items.map((x) => toDoctorItem(x, res.serviceDate)),
  };
}

export const bookingApi = {
  // TODO: nối contract thật cho danh mục chuyên khoa & học hàm/học vị công khai phía bệnh nhân
  listSpecialties: () => patientGet<SpecialtyOption[]>('/api/identity/public/specialties'),
  listDoctorRanks: () => patientGet<DoctorRankOption[]>('/api/identity/public/doctor-ranks'),

  // Real W1 Contract (`docs/api/scheduling.md`)
  searchDoctors,

  // shortcut: Backend chưa có route chi tiết ngày của bác sĩ, tìm trong 50 bác sĩ đầu của ngày; đổi khi có route riêng
  getDoctorDayDetail: async (doctorId: string, serviceDate: string): Promise<DoctorDaySlotSummary> => {
    const page = await searchDoctors({ serviceDate, pageSize: 50 });
    const doctor = page.items.find((x) => x.doctorId === doctorId);
    if (!doctor) {
      throw new PatientApiError(404, 'NOT_FOUND', 'Bác sĩ không còn nhận đặt lịch trong ngày đã chọn.');
    }
    return {
      doctorId: doctor.doctorId,
      fullName: doctor.fullName,
      doctorRankName: doctor.doctorRankName,
      specialtyName: doctor.specialtyName,
      serviceDate: doctor.serviceDate,
      roomName: doctor.roomName,
      consultationFee: doctor.consultationFee,
      totalSlots: doctor.totalSlots,
      remainingSlots: doctor.remainingSlots,
      onlineBookedCount: doctor.totalSlots - doctor.remainingSlots,
      walkInReservedNote: `${doctor.totalSlots} suất/ngày dùng chung cho đặt online và đăng ký trực tiếp tại quầy.`,
      status: doctor.status,
      sameDayBookedByProfileIds: [],
    };
  },

  // `docs/api/scheduling.md` §4.5: tạo tiến trình đặt lịch cho một hồ sơ
  createAttempt: (patientId: string, serviceDate: string, idempotencyKey: string) =>
    patientPost<{ bookingAttemptId: string }, { patientId: string; serviceDate: string }>(
      '/api/scheduling/patient/booking-attempts',
      { patientId, serviceDate },
      { idempotencyKey },
    ),

  // `docs/api/scheduling.md` §4.4: giữ chỗ 5 phút với Idempotency-Key
  createHold: (bookingAttemptId: string, body: HoldBookingRequest, idempotencyKey: string) =>
    patientPost<HoldResult, HoldBookingRequest>(
      `/api/scheduling/patient/booking-attempts/${bookingAttemptId}/hold`,
      body,
      { idempotencyKey },
    ),

  getBookingAttempt: (bookingAttemptId: string) =>
    patientGet<BookingProgress>(`/api/scheduling/patient/booking-attempts/${bookingAttemptId}`),

  getOperationProgress: (operationId: string) =>
    patientGet<BookingOperationProgress>(`/api/scheduling/patient/operations/${operationId}`),

  // `docs/api/scheduling.md` §4.5: tạo yêu cầu thanh toán phí khám (gọi lại trả cùng yêu cầu), rồi lấy liên kết VNPAY ở Billing
  createVnpayCheckout: async (bookingAttemptId: string): Promise<VnpayCheckoutResult & { paymentIntentId: string }> => {
    const payment = await patientPost<PaymentStarted>(
      `/api/scheduling/patient/booking-attempts/${bookingAttemptId}/payment`,
    );
    const checkout = await patientPost<{ paymentUrl: string; expiresAtUtc: string }>(
      `/api/billing/patient/payments/${payment.paymentIntentId}/checkout`,
    );
    return {
      paymentIntentId: payment.paymentIntentId,
      paymentUrl: checkout.paymentUrl,
      vnpTxnRef: '',
      expiresAt: checkout.expiresAtUtc,
    };
  },

  // Trạng thái thanh toán do Backend ghi nhận (IPN là nguồn sự thật), không đọc kết quả từ query string
  verifyVnpayReturn: async (paymentIntentId: string): Promise<VnpayReturnVerifyResult> => {
    const res = await patientGet<ApiPaymentStatus>(`/api/billing/patient/payments/${paymentIntentId}`);
    const mapped = RETURN_STATES[res.status] ?? RETURN_STATES.Unknown;
    return {
      state: mapped.state,
      vnpTxnRef: res.intentNumber,
      amount: res.amount,
      paidAt: res.paidAtUtc ?? undefined,
      message: mapped.message,
      retryAllowed: mapped.state === 'Failed' || mapped.state === 'Unknown',
    };
  },

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
