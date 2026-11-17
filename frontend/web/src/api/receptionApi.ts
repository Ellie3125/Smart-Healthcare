import { apiGet, apiPost } from '../shared/lib/http';
import type {
  CashierPayableItem,
  PaymentReconcileRow,
  ReceptionCallTaskItem,
  ReceptionSearchResult,
  ShiftReconcileSummary,
} from '../types/staffWorkflow';

/**
 * Reception & Cashier API (`RC01–RC04`, `RC06–RC10`).
 * Lưu ý: `RC05` tiếp tục giữ nguyên trong `ReceptionPage.tsx`.
 */
export const receptionApi = {
  // TODO: nối contract thật RC01 tìm kiếm hợp nhất
  searchUnified: (keyword: string) =>
    apiGet<ReceptionSearchResult[]>('/api/scheduling/staff/reception/search', { params: { keyword } }),

  // TODO: nối contract thật RC02 walk-in dùng chung 30 slot
  listWalkInDoctors: (serviceDate: string) =>
    apiGet<
      {
        doctorId: string;
        doctorName: string;
        specialtyName: string;
        roomName: string;
        remainingSlots: number;
        consultationFee: number;
      }[]
    >('/api/scheduling/staff/walk-in/doctors', { params: { serviceDate } }),

  registerWalkIn: (
    body: {
      fullName: string;
      phone: string;
      dateOfBirth: string;
      cccd: string;
      doctorId: string;
      serviceDate: string;
      collectCashNow: boolean;
    },
    idempotencyKey: string,
  ) =>
    apiPost<{ appointmentCode: string; ticketNumber: string; queueNumber: number }, typeof body>(
      '/api/scheduling/staff/walk-in',
      body,
      { idempotencyKey },
    ),

  // TODO: nối contract thật RC03 check-in + đối chiếu CCCD + CheckInDeadline
  checkInAppointment: (
    appointmentId: string,
    body: { identityDocumentVerified: boolean; note?: string },
    idempotencyKey: string,
  ) =>
    apiPost<{ appointmentId: string; queueNumber: number; roomName: string }, typeof body>(
      `/api/scheduling/staff/appointments/${appointmentId}/checkin`,
      body,
      { idempotencyKey },
    ),

  // TODO: nối contract thật RC04 thu tiền mặt (số tiền read-only do server quy định)
  listCashierItems: (keyword?: string) =>
    apiGet<CashierPayableItem[]>('/api/billing/staff/cashier/items', { params: keyword ? { keyword } : undefined }),

  collectCashPayment: (itemId: string, idempotencyKey: string) =>
    apiPost<CashierPayableItem, Record<string, never>>(
      `/api/billing/staff/cashier/items/${itemId}/collect-cash`,
      {},
      { idempotencyKey },
    ),

  // TODO: nối contract thật RC06 tra cứu giao dịch VNPAY (`querydr`)
  lookupPaymentTxn: (txnRef: string) =>
    apiPost<PaymentReconcileRow, { vnpTxnRef: string }>(
      '/api/billing/staff/payments/querydr',
      { vnpTxnRef: txnRef },
    ),

  // TODO: nối contract thật RC07 hủy lịch tại quầy (CheckedIn bắt buộc lý do)
  cancelAtCounter: (
    appointmentId: string,
    body: { reason: string; isCheckedIn: boolean },
    idempotencyKey: string,
  ) =>
    apiPost<{ refundAmount: number }, typeof body>(
      `/api/scheduling/staff/appointments/${appointmentId}/cancel`,
      body,
      { idempotencyKey },
    ),

  // TODO: nối contract thật RC08 chi hoàn tiền mặt tại quầy
  listApprovedCashRefunds: () =>
    apiGet<
      { id: string; referenceCode: string; patientName: string; amount: number; status: 'Approved' | 'Refunded' }[]
    >('/api/billing/staff/cash-refunds'),

  disburseCashRefund: (refundId: string, idempotencyKey: string) =>
    apiPost<{ id: string; voucherCode: string }, Record<string, never>>(
      `/api/billing/staff/cash-refunds/${refundId}/disburse`,
      {},
      { idempotencyKey },
    ),

  // TODO: nối contract thật RC09 chốt ca cuối ngày
  getShiftSummary: () => apiGet<ShiftReconcileSummary>('/api/billing/staff/cashier/shift-summary'),

  closeCashierShift: (
    body: { actualCashCounted: number; discrepancyNote: string },
    idempotencyKey: string,
  ) =>
    apiPost<ShiftReconcileSummary, typeof body>('/api/billing/staff/cashier/close-shift', body, {
      idempotencyKey,
    }),

  // TODO: nối contract thật RC10 danh sách cần gọi
  listCallTasks: () => apiGet<ReceptionCallTaskItem[]>('/api/scheduling/staff/call-list'),

  updateCallTask: (taskId: string, body: { status: 'Called' | 'Resolved'; note: string }) =>
    apiPost<ReceptionCallTaskItem, typeof body>(`/api/scheduling/staff/call-list/${taskId}/update`, body),
};
