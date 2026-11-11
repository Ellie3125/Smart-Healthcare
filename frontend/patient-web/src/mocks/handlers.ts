import { http, HttpResponse } from 'msw';
import {
  createInitialPatientStore,
  MOCK_DOCTORS,
  MOCK_PROFILES,
  MOCK_RANKS,
  MOCK_SPECIALTIES,
} from './patientData';
import type { BookingAttemptDetail, HoldBookingRequest } from '../types/booking';
import type { ChatMessageDto } from '../types/assistant';

let store = createInitialPatientStore();
// Tiến trình đã tạo nhưng chưa giữ chỗ: attemptId -> hồ sơ được chọn.
let pendingAttempts: Record<string, string> = {};

export function resetPatientMockStore() {
  store = createInitialPatientStore();
  pendingAttempts = {};
}

export const patientHandlers = [
  http.get('/api/identity/patient/profiles', () => HttpResponse.json(MOCK_PROFILES)),

  // Dạng thật của `GET /api/patient/profiles` (PatientProfileDto).
  http.get('/api/patient/profiles', () =>
    HttpResponse.json(
      MOCK_PROFILES.map((p) => ({
        patientId: p.id,
        fullName: p.fullName,
        dateOfBirth: p.dateOfBirth,
        gender: 'Male',
        nationalId: null,
        phoneNumber: p.phone,
        address: 'Hà Nội',
        relationship: p.relationship,
        verificationStatus: p.verificationStatus,
        isActive: true,
      })),
    ),
  ),

  http.get('/api/identity/public/specialties', () => HttpResponse.json(MOCK_SPECIALTIES)),
  http.get('/api/identity/public/doctor-ranks', () => HttpResponse.json(MOCK_RANKS)),

  http.get('/api/scheduling/patient/doctors', ({ request }) => {
    const url = new URL(request.url);
    const serviceDate = url.searchParams.get('serviceDate') || store.today;
    const specialtyId = url.searchParams.get('specialtyId') || '';
    const doctorRankId = url.searchParams.get('doctorRankId') || '';

    const filtered = MOCK_DOCTORS.filter((d) => {
      if (serviceDate && d.serviceDate !== serviceDate) return false;
      if (specialtyId && d.specialtyId !== specialtyId) return false;
      if (doctorRankId && d.doctorRankId !== doctorRankId) return false;
      return true;
    });

    // Dạng thật của Backend (`docs/api/scheduling.md` §4.3).
    return HttpResponse.json({
      serviceDate,
      page: 1,
      pageSize: 20,
      hasMore: false,
      items: filtered.map((d) => ({
        doctorId: d.doctorId,
        fullName: d.fullName,
        doctorRankId: d.doctorRankId,
        specialtyId: d.specialtyId,
        availableSlots: d.remainingSlots,
        examFee: d.consultationFee,
        currency: 'VND',
        totalSlots: d.totalSlots,
        doctorRankName: d.doctorRankName,
        specialtyName: d.specialtyName,
      })),
    });
  }),

  http.post('/api/scheduling/patient/booking-attempts', async ({ request }) => {
    const body = (await request.json()) as { patientId: string; serviceDate: string };
    const attemptId = `attempt-${Date.now()}`;
    pendingAttempts[attemptId] = body.patientId;
    return HttpResponse.json({ bookingAttemptId: attemptId }, { status: 201 });
  }),

  http.post('/api/scheduling/patient/booking-attempts/:attemptId/hold', async ({ params, request }) => {
    const body = (await request.json()) as HoldBookingRequest;
    const doctor = MOCK_DOCTORS.find((d) => d.doctorId === body.doctorId);
    if (!doctor || doctor.remainingSlots <= 0) {
      return HttpResponse.json(
        { code: 'SLOT_FULL', title: 'Hết chỗ', detail: 'Bác sĩ đã hết suất khám trong ngày.' },
        { status: 409 },
      );
    }
    const profileId = pendingAttempts[String(params.attemptId)] || 'prof-self';
    const profile = MOCK_PROFILES.find((p) => p.id === profileId) ?? MOCK_PROFILES[0];

    const duplicate = store.appointments.find(
      (a) =>
        a.patientProfileId === profileId &&
        a.serviceDate === body.serviceDate &&
        a.status !== 'Cancelled',
    );
    if (duplicate) {
      return HttpResponse.json(
        { code: 'ALREADY_BOOKED_SAME_DAY', title: 'Trùng lịch', detail: 'Hồ sơ này đã có lịch trong ngày.' },
        { status: 409 },
      );
    }

    const attemptId = String(params.attemptId);
    const expiresAt = new Date(Date.now() + 5 * 60 * 1000).toISOString();
    const attempt: BookingAttemptDetail = {
      bookingAttemptId: attemptId,
      status: 'Hold',
      doctorId: doctor.doctorId,
      doctorName: doctor.fullName,
      specialtyName: doctor.specialtyName,
      serviceDate: body.serviceDate,
      roomName: doctor.roomName,
      patientProfileId: profile.id,
      patientName: profile.fullName,
      amount: doctor.consultationFee,
      expiresAt,
    };
    store.attempts[attemptId] = attempt;

    return HttpResponse.json(
      { operationId: `op-${attemptId}`, reservationId: `res-${attemptId}`, expiresAtUtc: expiresAt },
      { status: 201 },
    );
  }),

  http.get('/api/scheduling/patient/booking-attempts/recoverable', () =>
    HttpResponse.json(
      Object.values(store.attempts).filter((a) => a.status === 'Hold' || a.status === 'PendingPayment'),
    ),
  ),

  http.get('/api/scheduling/patient/booking-attempts/:attemptId', ({ params }) => {
    const attempt = store.attempts[String(params.attemptId)];
    if (!attempt) {
      return HttpResponse.json({ code: 'NOT_FOUND', detail: 'Không tìm thấy phiên giữ chỗ.' }, { status: 404 });
    }
    // Dạng thật của Backend (`docs/api/scheduling.md` §4.2).
    return HttpResponse.json({
      bookingAttemptId: attempt.bookingAttemptId,
      status:
        attempt.status === 'Hold'
          ? 'Held'
          : attempt.status === 'PendingPayment'
            ? 'WaitingPayment'
            : attempt.status === 'Confirmed'
              ? 'Confirmed'
              : 'Cancelled',
      currentStep: attempt.status,
      expiresAtUtc: attempt.expiresAt,
      allowedActions: [],
    });
  }),

  http.post('/api/scheduling/patient/booking-attempts/:attemptId/payment', ({ params }) => {
    const attemptId = String(params.attemptId);
    const attempt = store.attempts[attemptId];
    if (!attempt) {
      return HttpResponse.json({ code: 'AttemptNotFound', title: 'Không tìm thấy phiên giữ chỗ.' }, { status: 404 });
    }
    attempt.status = 'PendingPayment';
    return HttpResponse.json({
      paymentIntentId: `pi-${attemptId}`,
      amount: attempt.amount,
      currency: 'VND',
      expiresAtUtc: attempt.expiresAt,
    });
  }),

  http.post('/api/billing/patient/payments/:paymentIntentId/checkout', ({ params }) =>
    HttpResponse.json({
      paymentIntentId: String(params.paymentIntentId),
      paymentUrl: '/payment/vnpay-return?vnp_ResponseCode=00',
      amount: 150000,
      currency: 'VND',
      expiresAtUtc: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
    }),
  ),

  // Lần hỏi đầu trả Pending, từ lần hai trả Succeeded (mô phỏng IPN về sau Return URL).
  http.get('/api/billing/patient/payments/:paymentIntentId', ({ params }) => {
    const id = String(params.paymentIntentId);
    const poll = (store.vnpayVerifyPollCount[id] ?? 0) + 1;
    store.vnpayVerifyPollCount[id] = poll;
    const paid = poll > 1;
    return HttpResponse.json({
      paymentIntentId: id,
      intentNumber: `PI-${id.slice(-8).toUpperCase()}`,
      purpose: 'InitialConsultation',
      amount: 150000,
      currency: 'VND',
      status: paid ? 'Succeeded' : 'Pending',
      expiresAtUtc: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
      paidAtUtc: paid ? new Date().toISOString() : null,
      canPay: !paid,
    });
  }),

  http.get('/api/scheduling/patient/appointments', ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status') || '';
    const items = status ? store.appointments.filter((a) => a.status === status) : store.appointments;
    return HttpResponse.json({ items, hasMore: false });
  }),

  http.get('/api/scheduling/patient/appointments/:id/ticket', ({ params }) => {
    const ticket = store.tickets[String(params.id)];
    if (!ticket) {
      return HttpResponse.json({ code: 'NOT_FOUND', detail: 'Không tìm thấy vé khám.' }, { status: 404 });
    }
    return HttpResponse.json(ticket);
  }),

  http.get('/api/scheduling/patient/appointments/:id/reschedule-options', ({ params, request }) => {
    const apt = store.appointments.find((a) => a.id === params.id);
    const targetDate = new URL(request.url).searchParams.get('targetDate') || store.tomorrow;
    const fee = apt?.consultationFee ?? 150000;
    const options = MOCK_DOCTORS.map((d) => ({
      doctorId: d.doctorId,
      doctorName: d.fullName,
      doctorRankName: d.doctorRankName,
      specialtyName: d.specialtyName,
      serviceDate: targetDate,
      roomName: d.roomName,
      consultationFee: d.consultationFee,
      remainingSlots: d.remainingSlots,
      sameFeeEligible: d.consultationFee === fee && d.remainingSlots > 0,
    }));
    return HttpResponse.json(options);
  }),

  http.post('/api/scheduling/patient/appointments/:id/reschedule', async ({ params, request }) => {
    const body = (await request.json()) as { targetDoctorId: string; targetServiceDate: string };
    const apt = store.appointments.find((a) => a.id === params.id);
    const targetDoc = MOCK_DOCTORS.find((d) => d.doctorId === body.targetDoctorId);
    if (!apt || !targetDoc) {
      return HttpResponse.json({ code: 'NOT_FOUND', detail: 'Không tìm thấy lịch hoặc bác sĩ.' }, { status: 404 });
    }
    if (targetDoc.consultationFee !== apt.consultationFee) {
      return HttpResponse.json(
        { code: 'RESCHEDULE_DIFFERENT_FEE', detail: 'Chỉ hỗ trợ đổi sang bác sĩ cùng mức phí.' },
        { status: 400 },
      );
    }
    apt.doctorId = targetDoc.doctorId;
    apt.doctorName = targetDoc.fullName;
    apt.specialtyName = targetDoc.specialtyName;
    apt.roomName = targetDoc.roomName;
    apt.serviceDate = body.targetServiceDate;
    return HttpResponse.json(apt);
  }),

  http.get('/api/scheduling/patient/appointments/:id/cancel-preview', ({ params }) => {
    const apt = store.appointments.find((a) => a.id === params.id);
    if (!apt) {
      return HttpResponse.json({ code: 'NOT_FOUND', detail: 'Không tìm thấy lịch.' }, { status: 404 });
    }
    return HttpResponse.json({
      appointmentId: apt.id,
      originalAmount: apt.consultationFee,
      refundAmount: apt.canCancel ? apt.consultationFee : 0,
      refundRatePercent: apt.canCancel ? 100 : 0,
      policyExplanation: apt.canCancel
        ? 'Hủy trước 05:00 sáng ngày khám được hoàn 100% phí khám theo quy định.'
        : 'Lịch đã check-in không thể tự hủy trực tuyến.',
      eligibleToCancel: apt.canCancel,
      blockedReason: apt.cancelBlockedReason,
    });
  }),

  http.post('/api/scheduling/patient/appointments/:id/cancel', ({ params }) => {
    const apt = store.appointments.find((a) => a.id === params.id);
    if (!apt) {
      return HttpResponse.json({ code: 'NOT_FOUND', detail: 'Không tìm thấy lịch.' }, { status: 404 });
    }
    apt.status = 'Cancelled';
    apt.canCancel = false;
    apt.canReschedule = false;
    const refundId = `ref-${Date.now()}`;
    store.refunds.unshift({
      id: refundId,
      referenceCode: `RF-${Date.now().toString().slice(-6)}`,
      reason: `Hủy lịch khám ${apt.appointmentCode}`,
      amount: apt.consultationFee,
      status: 'Pending',
      payoutChannel: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    });
    return HttpResponse.json({
      appointmentId: apt.id,
      refundAmount: apt.consultationFee,
      refundRequestId: refundId,
    });
  }),

  http.get('/api/clinical/patient/today-journey', () => HttpResponse.json(store.todayJourney)),

  http.get('/api/billing/patient/cls-batches', () => HttpResponse.json(store.clsBatches)),

  http.post('/api/billing/patient/cls-batches/:batchId/vnpay-checkout', ({ params }) => {
    const txnRef = `VNP-CLS-${String(params.batchId)}`;
    return HttpResponse.json({
      paymentUrl: `/payment/vnpay-return?vnp_TxnRef=${encodeURIComponent(txnRef)}&mockState=Paid`,
      vnpTxnRef: txnRef,
      expiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
    });
  }),

  http.get('/api/billing/patient/encounters/:encounterId/cost-summary', () =>
    HttpResponse.json(store.costSummary),
  ),

  http.get('/api/billing/patient/refunds', () => HttpResponse.json(store.refunds)),

  http.post('/api/billing/patient/refunds/:refundId/payout-channel', async ({ params, request }) => {
    const body = (await request.json()) as {
      payoutChannel: 'CashCounter' | 'BankTransfer' | 'OriginalVnpay';
      bankName?: string;
      accountNumber?: string;
    };
    const item = store.refunds.find((r) => r.id === params.refundId);
    if (!item) {
      return HttpResponse.json({ code: 'NOT_FOUND', detail: 'Không tìm thấy yêu cầu hoàn.' }, { status: 404 });
    }
    item.payoutChannel = body.payoutChannel;
    item.bankAccountSummary =
      body.payoutChannel === 'BankTransfer' && body.accountNumber
        ? `${body.bankName || 'Ngân hàng'} •••${body.accountNumber.slice(-3)}`
        : body.payoutChannel === 'CashCounter'
          ? 'Nhận tiền mặt tại quầy tài chính'
          : 'Hoàn về thẻ/tài khoản VNPAY gốc';
    return HttpResponse.json(item);
  }),

  http.get('/api/clinical/patient/encounters', ({ request }) => {
    const profileId = new URL(request.url).searchParams.get('patientProfileId');
    if (profileId === 'prof-pending') {
      return HttpResponse.json(
        {
          code: 'PROFILE_PENDING_VERIFICATION',
          detail: 'Hồ sơ chưa được xác minh định danh tại quầy nên chưa thể xem lịch sử khám.',
        },
        { status: 403 },
      );
    }
    const items = profileId
      ? store.encounters.filter((e) => e.patientProfileId === profileId)
      : store.encounters;
    return HttpResponse.json({ items, hasMore: false });
  }),

  http.get('/api/clinical/patient/encounters/:encounterId', ({ params }) => {
    const detail = store.encounterDetails[String(params.encounterId)];
    if (!detail) {
      return HttpResponse.json({ code: 'NOT_FOUND', detail: 'Không tìm thấy hồ sơ khám.' }, { status: 404 });
    }
    if (!detail.isPublished) {
      return HttpResponse.json({
        ...detail,
        symptoms: undefined,
        clinicalNotes: undefined,
        diagnoses: undefined,
        conclusion: undefined,
        followUpInstruction: undefined,
        clsResults: [],
      });
    }
    return HttpResponse.json(detail);
  }),

  http.get('/api/documents/patient/cls-results/:resultId/files/:fileId/download', () => {
    const blob = new Blob(['%PDF-1.4\n% Smart Healthcare Sample CLS Report PDF\n'], {
      type: 'application/pdf',
    });
    return new HttpResponse(blob, {
      status: 200,
      headers: {
        'Content-Type': 'application/pdf',
        'Content-Disposition': 'attachment; filename="ket-qua-can-lam-sang.pdf"',
      },
    });
  }),

  http.get('/api/medication/patient/encounters/:encounterId/prescription', () =>
    HttpResponse.json(store.prescription),
  ),

  http.get('/api/notifications/patient/notifications', () =>
    HttpResponse.json({
      items: store.notifications,
      unreadCount: store.notifications.filter((n) => n.status === 'Sent').length,
      hasMore: false,
    }),
  ),

  http.post('/api/notifications/patient/notifications/:id/read', ({ params }) => {
    const target = store.notifications.find((n) => n.id === params.id);
    if (target) target.status = 'Read';
    return HttpResponse.json({ ok: true });
  }),

  http.get('/api/notifications/patient/chat/health', () =>
    HttpResponse.json({ available: true }),
  ),

  http.post('/api/notifications/patient/chat/sessions', () =>
    HttpResponse.json({
      sessionId: 'chat-session-1',
      quickSuggestions: [
        'Đau ngực khó thở cần làm gì?',
        'Tìm bác sĩ Nội tổng quát hôm nay',
        'Cách hủy hoặc đổi lịch khám',
      ],
      messages: store.chatMessages,
    }),
  ),

  http.post('/api/notifications/patient/chat/sessions/:sessionId/messages', async ({ request }) => {
    const body = (await request.json()) as { content: string };
    const text = body.content.toLowerCase();
    const userMsg: ChatMessageDto = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: body.content,
      createdAt: new Date().toISOString(),
    };
    store.chatMessages.push(userMsg);

    let reply: ChatMessageDto;
    if (text.includes('đau ngực') || text.includes('khó thở') || text.includes('115')) {
      reply = {
        id: `bot-${Date.now() + 1}`,
        role: 'assistant',
        content:
          'Triệu chứng bạn mô tả có thể là dấu hiệu cấp cứu nguy hiểm. Vui lòng gọi ngay 115 hoặc đến cơ sở cấp cứu gần nhất ngay lập tức.',
        createdAt: new Date().toISOString(),
        isRedFlagEmergency: true,
        emergencyPhone: '115',
      };
    } else if (text.includes('đặt') || text.includes('nội') || text.includes('bác sĩ')) {
      reply = {
        id: `bot-${Date.now() + 1}`,
        role: 'assistant',
        content: 'Dựa trên nhu cầu của bạn, bạn có thể tham khảo khám chuyên khoa Nội tổng quát và xác nhận giữ chỗ bên dưới.',
        createdAt: new Date().toISOString(),
        suggestedSpecialty: 'Nội tổng quát',
        disclaimer: 'Gợi ý chuyên khoa chỉ mang tính tham khảo ban đầu, không thay thế chẩn đoán của bác sĩ.',
        proposal: {
          proposalId: `prop-${Date.now()}`,
          doctorId: 'doc-1',
          doctorName: 'BS.CKI Trần Quốc Khánh',
          specialtyName: 'Nội tổng quát',
          serviceDate: store.today,
          consultationFee: 150000,
          remainingSlots: 8,
          status: 'Proposed',
        },
      };
    } else {
      reply = {
        id: `bot-${Date.now() + 1}`,
        role: 'assistant',
        content: 'Bạn có thể đổi lịch cùng mức giá trước 05:00 sáng ngày khám trong mục "Lịch của tôi".',
        createdAt: new Date().toISOString(),
        disclaimer: 'Thông tin hỗ trợ quy trình, vui lòng liên hệ tổng đài bệnh viện nếu cần trợ giúp khẩn cấp.',
      };
    }
    store.chatMessages.push(reply);
    return HttpResponse.json(reply);
  }),

  http.post('/api/scheduling/patient/proposals/:proposalId/confirm', () => {
    const attemptId = `attempt-prop-${Date.now()}`;
    store.attempts[attemptId] = {
      bookingAttemptId: attemptId,
      status: 'Hold',
      doctorId: 'doc-1',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      serviceDate: store.today,
      roomName: 'Phòng khám P201 - Tầng 2',
      patientProfileId: 'prof-self',
      patientName: 'Nguyễn Minh Tâm',
      amount: 150000,
      expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
    };
    return HttpResponse.json({ bookingAttemptId: attemptId });
  }),

  http.post('/api/scheduling/patient/proposals/:proposalId/decline', () => HttpResponse.json({ ok: true })),

  http.get('/api/scheduling/public/display', () => HttpResponse.json(store.displayBoard)),
];
