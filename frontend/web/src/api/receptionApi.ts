import { apiGet, apiPost } from '../shared/lib/http';
import { getVNDateISO } from '../shared/lib/datetime';
import type {
  CashierPayableItem,
  PaymentReconcileRow,
  ReceptionCallTaskItem,
  ReceptionSearchResult,
  ShiftReconcileSummary,
} from '../types/staffWorkflow';

/**
 * Reception & Cashier API (`RC01–RC04`, `RC06–RC10`), nối route thật của Scheduling và Billing
 * (`docs/api/scheduling.md` §4.6, §4.6a, §4.7; `docs/api/billing-cashier.md`).
 * Dạng trả về của Backend được đổi sang kiểu các trang đang dùng ngay trong file này.
 */

interface ApiTodayAppointment {
  appointmentId: string;
  appointmentNumber: string;
  patientId: string;
  doctorId: string;
  assignedRoomId: string;
  serviceDate: string;
  status: string;
  checkInDeadlineUtc: string;
}

interface ApiReceptionPatient {
  patientId: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  maskedPhoneNumber: string;
  maskedNationalId: string | null;
  isVerified: boolean;
}

interface ApiStaffDoctor {
  doctorId: string;
  fullName: string;
  availableSlots: number;
  totalSlots: number;
  examFee: number;
  specialtyName: string | null;
  doctorRankName: string | null;
}

interface ApiCashierItem {
  serviceOrderId: string;
  patientId: string;
  encounterId: string;
  description: string;
  totalAmount: number;
  paidAmount: number;
  outstanding: number;
  status: number | string;
  orderedAtUtc: string;
  payByUtc: string;
}

interface ApiRefund {
  refundRequestId: string;
  status: string;
  chargeName: string;
  amount: number;
  originalTransactionReference: string;
  refundSlipNumber: string | null;
}

interface ApiCallItem {
  id: string;
  serviceDate: string;
  reason: string;
  patientName: string | null;
  maskedPhoneNumber: string | null;
}

const BATCH_STATUS = ['PendingPayment', 'Paid', 'Expired', 'Cancelled'] as const;
const RECONCILE_OUTCOME = ['PaidRecorded', 'AlreadyPaid', 'FailedRecorded', 'AlreadyFinal'];
const PAYMENT_STATE = ['Pending', 'Processing', 'Succeeded', 'Failed', 'Unknown', 'Expired', 'Cancelled'];

// Backend: Scheduled; các trang dùng tên Confirmed cho lịch chờ khám.
const appointmentStatus = (s: string) => (s === 'Scheduled' ? 'Confirmed' : s === 'Absent' ? 'NoShow' : s);

const listStaffDoctors = (serviceDate: string) =>
  apiGet<{ items: ApiStaffDoctor[] }>('/api/scheduling/staff/doctors', { params: { serviceDate, pageSize: 50 } });

const listToday = (params?: { page?: number; pageSize?: number; appointmentNumber?: string; doctorId?: string }) =>
  apiGet<{ page: number; pageSize: number; hasMore: boolean; items: ApiTodayAppointment[] }>(
    '/api/scheduling/staff/appointments/today',
    { params },
  );

/**
 * RC01: Backend tra hồ sơ chỉ theo SĐT/CCCD khớp chính xác (9–20 ký tự), tra lịch theo mã lịch hẹn;
 * không có tìm theo tên. Ô trống: toàn bộ lịch hôm nay.
 * shortcut: chỉ ghép trong 50 lịch đầu của hôm nay, tăng khi Backend trả lịch kèm hồ sơ.
 */
async function searchUnified(keyword: string): Promise<ReceptionSearchResult[]> {
  const k = keyword.replace(/\s/g, '');
  const isContact = /^\d{9,20}$/.test(k);
  const [patients, today, doctors] = await Promise.all([
    isContact ? apiGet<ApiReceptionPatient[]>('/api/scheduling/staff/reception/search', { params: { keyword: k } }) : [],
    listToday({ pageSize: 50, appointmentNumber: k && !isContact ? k : undefined }),
    listStaffDoctors(getVNDateISO()).catch(() => ({ items: [] as ApiStaffDoctor[] })),
  ]);
  const doctorName = new Map(doctors.items.map((d) => [d.doctorId, d.fullName]));
  const now = Date.now();
  const fromAppointment = (a: ApiTodayAppointment, p?: ApiReceptionPatient): ReceptionSearchResult => ({
    id: a.appointmentId,
    patientName: p?.fullName ?? '',
    phone: p?.maskedPhoneNumber ?? '',
    cccdMasked: p?.maskedNationalId ?? '',
    dateOfBirth: p?.dateOfBirth ?? '',
    verificationStatus: p ? (p.isVerified ? 'Verified' : 'Pending') : 'Verified',
    appointmentId: a.appointmentId,
    appointmentCode: a.appointmentNumber,
    doctorName: doctorName.get(a.doctorId) ?? '',
    serviceDate: a.serviceDate,
    appointmentStatus: appointmentStatus(a.status),
    checkInDeadline: a.checkInDeadlineUtc ?? undefined,
    isPastCheckInDeadline: a.status === 'Scheduled' && !!a.checkInDeadlineUtc && Date.parse(a.checkInDeadlineUtc) < now,
    queueNumber: null,
  });
  if (!isContact) return today.items.map((a) => fromAppointment(a));
  return patients.flatMap((p) => {
    const mine = today.items.filter((a) => a.patientId === p.patientId);
    if (mine.length) return mine.map((a) => fromAppointment(a, p));
    return [{
      id: p.patientId,
      patientName: p.fullName,
      phone: p.maskedPhoneNumber,
      cccdMasked: p.maskedNationalId ?? '',
      dateOfBirth: p.dateOfBirth,
      verificationStatus: p.isVerified ? 'Verified' : 'Pending',
    } satisfies ReceptionSearchResult];
  });
}

const checkIn = (appointmentId: string) =>
  apiPost<{
    appointmentId: string;
    patientId: string;
    doctorId: string;
    serviceDate: string;
    checkedInAtUtc: string;
    alreadyCheckedIn: boolean;
    queueTicketId: string;
    ticketNumber: string;
    displayNumber?: string | null;
    sequenceNumber: number;
    encounterId: string;
  }, Record<string, never>>(`/api/scheduling/staff/appointments/${appointmentId}/check-in`, {});

export const receptionApi = {
  searchUnified,

  // §4.6a: hồ sơ khớp chính xác SĐT/CCCD, dùng cho đăng ký walk-in.
  searchPatients: async (keyword: string) =>
    (await apiGet<ApiReceptionPatient[]>('/api/scheduling/staff/reception/search', { params: { keyword } })).map((p) => ({
      patientId: p.patientId,
      fullName: p.fullName,
      dateOfBirth: p.dateOfBirth,
      phone: p.maskedPhoneNumber,
      cccdMasked: p.maskedNationalId ?? '',
    })),

  // RC02: bác sĩ hôm nay còn suất, dùng chung quota với đặt online (`GET /staff/doctors`).
  listWalkInDoctors: async (serviceDate: string) =>
    (await listStaffDoctors(serviceDate)).items.map((d) => ({
      doctorId: d.doctorId,
      doctorName: d.fullName,
      specialtyName: d.specialtyName ?? '',
      roomName: '',
      remainingSlots: d.availableSlots,
      totalSlots: d.totalSlots,
      consultationFee: d.examFee,
    })),

  // RC02: Backend nhận hồ sơ đã có (tra theo SĐT/CCCD), giữ chỗ, thu tiền mặt phí khám và check-in ngay.
  registerWalkIn: async (body: { patientId: string; doctorId: string }, idempotencyKey: string) => {
    const res = await apiPost<{
      appointmentNumber: string | null;
      checkIn: { ticketNumber: string; displayNumber?: string | null; sequenceNumber: number } | null;
      receipt: { receiptNumber: string; amount: number } | null;
    }, typeof body>('/api/scheduling/staff/walk-in', body, { idempotencyKey });
    return {
      appointmentCode: res.appointmentNumber ?? '',
      ticketNumber: res.checkIn?.displayNumber || res.checkIn?.ticketNumber || '',
      queueNumber: res.checkIn?.sequenceNumber ?? 0,
      receiptNumber: res.receipt?.receiptNumber ?? '',
    };
  },

  // Contract thật CheckInEndpoints.cs (/api/scheduling/staff/appointments/today & check-in)
  listTodayAppointments: listToday,
  checkIn,

  // RC03: tiếp tân đối chiếu giấy tờ ngoài hệ thống; API không nhận body, idempotent theo lịch hẹn.
  checkInAppointment: async (appointmentId: string) => {
    const res = await checkIn(appointmentId);
    return { appointmentId: res.appointmentId, queueNumber: res.displayNumber || res.ticketNumber, roomName: '' };
  },

  // RC04: đợt CLS trong ngày còn phải thu; Backend không trả tên bệnh nhân, lọc theo nội dung phía web.
  listCashierItems: async (keyword?: string): Promise<CashierPayableItem[]> => {
    const items = await apiGet<ApiCashierItem[]>('/api/billing/staff/cashier/items');
    const k = keyword?.trim().toLowerCase();
    return items
      .map((x) => {
        const status = typeof x.status === 'number' ? BATCH_STATUS[x.status] : x.status;
        return {
          id: x.serviceOrderId,
          referenceCode: x.serviceOrderId.slice(0, 8).toUpperCase(),
          patientName: '',
          itemType: 'ClsBatch' as const,
          description: x.description,
          amount: x.outstanding > 0 ? x.outstanding : x.totalAmount,
          status: (status === 'PendingPayment' && x.outstanding > 0 ? 'PendingPayment' : 'Paid') as CashierPayableItem['status'],
        };
      })
      .filter((x) => !k || x.description.toLowerCase().includes(k) || x.referenceCode.toLowerCase().includes(k));
  },

  // RC04: id là ServiceOrderId của đợt CLS.
  collectCashPayment: async (serviceOrderId: string, idempotencyKey: string) => {
    const res = await apiPost<{ receiptNumber: string; amount: number; collectedAtUtc: string }, Record<string, never>>(
      `/api/billing/staff/cashier/cls-batches/${serviceOrderId}/collect-cash`,
      {},
      { idempotencyKey },
    );
    return { receiptCode: res.receiptNumber, amount: res.amount, paidAt: res.collectedAtUtc };
  },

  // RC06: hỏi VNPAY theo vnp_TxnRef; cổng báo đã thu thì Backend ghi nhận như IPN.
  lookupPaymentTxn: async (txnRef: string): Promise<PaymentReconcileRow> => {
    const res = await apiPost<{
      transactionReference: string;
      outcome: number | string;
      status: number | string | null;
      amount: number | null;
    }, { transactionReference: string }>('/api/billing/staff/payments/querydr', { transactionReference: txnRef });
    const outcome = typeof res.outcome === 'number' ? RECONCILE_OUTCOME[res.outcome] : res.outcome;
    const state = typeof res.status === 'number' ? PAYMENT_STATE[res.status] : res.status;
    return {
      id: res.transactionReference,
      vnpTxnRef: res.transactionReference,
      appointmentOrBatchCode: '',
      amount: res.amount ?? 0,
      localStatus: state === 'Succeeded' ? 'Paid' : state === 'Failed' ? 'Failed' : state === 'Unknown' ? 'Unknown' : 'PendingPayment',
      gatewayStatus: outcome === 'PaidRecorded' || outcome === 'AlreadyPaid' || outcome === 'FailedRecorded' ? 'Matched' : 'Unknown',
      checkedAt: new Date().toISOString(),
    };
  },

  // RC07: Backend bắt buộc lý do khi lịch đã CheckedIn; 202 khi xử lý nền thì chưa có số tiền.
  cancelAtCounter: async (appointmentId: string, body: { reason: string; isCheckedIn: boolean }, idempotencyKey: string) => {
    const res = await apiPost<{ refundAmount?: number }, { reason: string }>(
      `/api/scheduling/staff/appointments/${appointmentId}/cancel`,
      { reason: body.reason },
      { idempotencyKey },
    );
    return { refundAmount: res.refundAmount ?? 0 };
  },

  // RC08: phiếu hoàn đã duyệt chờ chi tiền mặt; Backend không trả tên bệnh nhân.
  listApprovedCashRefunds: async () =>
    (await apiGet<ApiRefund[]>('/api/billing/staff/cash-refunds')).map((r) => ({
      id: r.refundRequestId,
      referenceCode: r.refundSlipNumber ?? r.originalTransactionReference,
      patientName: r.chargeName,
      amount: r.amount,
      status: (r.status === 'Settled' ? 'Refunded' : 'Approved') as 'Approved' | 'Refunded',
    })),

  disburseCashRefund: async (refundRequestId: string, idempotencyKey: string) => {
    const res = await apiPost<{ refundSlipNumber: string; refundRequestId: string }, Record<string, never>>(
      `/api/billing/staff/cash-refunds/${refundRequestId}/disburse`,
      {},
      { idempotencyKey },
    );
    return { id: res.refundRequestId, voucherCode: res.refundSlipNumber };
  },

  // RC09 chốt ca cuối ngày (Billing #166)
  getShiftSummary: () => apiGet<ShiftReconcileSummary>('/api/billing/staff/cashier/shift-summary'),

  closeCashierShift: (
    body: { actualCashCounted: number; discrepancyNote: string },
    idempotencyKey: string,
  ) =>
    apiPost<ShiftReconcileSummary, typeof body>('/api/billing/staff/cashier/close-shift', body, {
      idempotencyKey,
    }),

  // RC10: chỉ đọc. Chưa có nơi lưu trạng thái cuộc gọi nên không có thao tác cập nhật.
  listCallTasks: async (): Promise<ReceptionCallTaskItem[]> =>
    (await apiGet<{ items: ApiCallItem[] }>('/api/scheduling/staff/call-list', { params: { pageSize: 50 } })).items.map((c) => ({
      id: c.id,
      patientName: c.patientName ?? '',
      phone: c.maskedPhoneNumber ?? '',
      reason: c.reason,
      doctorName: '',
      serviceDate: c.serviceDate,
      status: 'Pending',
    })),
};
