import { vi } from 'vitest';
import { apiClient } from '../api/client';
import { getVNDateISO } from '../shared/lib/datetime';
import type {
  AppointmentTicketDetail,
  BookingAttemptDetail,
  DoctorRankOption,
  DoctorSearchItem,
  PatientAppointmentItem,
  RescheduleOption,
  SpecialtyOption,
} from '../types/booking';
import type {
  EncounterCostSummary,
  PatientEncounterDetail,
  PatientEncounterSummary,
  PatientPrescriptionDetail,
} from '../types/clinical';
import type {
  ChatMessageDto,
  PatientNotificationItem,
  PublicDisplayBoardDto,
} from '../types/assistant';

export interface RouteHandlerResult {
  status?: number;
  data?: unknown;
  headers?: Record<string, string>;
}

export type RouteHandler = (
  url: string,
  body?: unknown,
  params?: Record<string, string>,
  query?: URLSearchParams,
) => RouteHandlerResult | Promise<RouteHandlerResult>;

interface OverrideEntry {
  method: string;
  pattern: string;
  handler: RouteHandler;
}

export function matchPath(pattern: string, pathname: string): Record<string, string> | null {
  const patternParts = pattern.split('?')[0].split('/').filter(Boolean);
  const pathParts = pathname.split('?')[0].split('/').filter(Boolean);
  if (patternParts.length !== pathParts.length) return null;
  const params: Record<string, string> = {};
  for (let i = 0; i < patternParts.length; i++) {
    if (patternParts[i].startsWith(':')) {
      params[patternParts[i].slice(1)] = pathParts[i];
    } else if (patternParts[i] !== pathParts[i]) {
      return null;
    }
  }
  return params;
}

export const MOCK_SPECIALTIES: SpecialtyOption[] = [
  { id: 'spec-1', code: 'NOI', name: 'Nội tổng quát' },
  { id: 'spec-2', code: 'TIM', name: 'Tim mạch' },
  { id: 'spec-3', code: 'NHI', name: 'Nhi khoa' },
  { id: 'spec-4', code: 'DALIEU', name: 'Da liễu' },
];

export const MOCK_RANKS: DoctorRankOption[] = [
  { id: 'rank-1', code: 'BS', name: 'Bác sĩ CKI', consultationFee: 150000 },
  { id: 'rank-2', code: 'THS', name: 'ThS. Bác sĩ', consultationFee: 200000 },
  { id: 'rank-3', code: 'PGS', name: 'PGS.TS. Bác sĩ', consultationFee: 350000 },
];

export const MOCK_PROFILES = [
  {
    id: 'prof-self',
    fullName: 'Nguyễn Minh Tâm',
    relationship: 'Bản thân',
    verificationStatus: 'Verified' as const,
    dateOfBirth: '1992-04-18',
    phone: '0901234567',
  },
  {
    id: 'prof-mom',
    fullName: 'Lê Thị Hoa',
    relationship: 'Mẹ',
    verificationStatus: 'Verified' as const,
    dateOfBirth: '1965-08-12',
    phone: '0902345678',
  },
  {
    id: 'prof-pending',
    fullName: 'Nguyễn Bảo Khang',
    relationship: 'Con',
    verificationStatus: 'Pending' as const,
    dateOfBirth: '2018-02-10',
    phone: '0901234567',
  },
];

export function createPatientRealStore() {
  const today = getVNDateISO();
  const tomorrowDate = new Date(Date.now() + 24 * 60 * 60 * 1000);
  const tomorrow = getVNDateISO(tomorrowDate);

  const doctors: DoctorSearchItem[] = [
    {
      doctorId: 'doc-1',
      fullName: 'BS.CKI Trần Quốc Khánh',
      doctorRankId: 'rank-1',
      doctorRankName: 'Bác sĩ CKI',
      specialtyId: 'spec-1',
      specialtyName: 'Nội tổng quát',
      consultationFee: 150000,
      serviceDate: today,
      roomCode: 'P201',
      roomName: 'Phòng khám P201 - Tầng 2',
      totalSlots: 30,
      remainingSlots: 8,
      status: 'Open',
      bio: '12 năm kinh nghiệm khám chữa bệnh Nội tổng quát và quản lý bệnh mạn tính.',
    },
    {
      doctorId: 'doc-2',
      fullName: 'ThS.BS Phạm Thu Hà',
      doctorRankId: 'rank-2',
      doctorRankName: 'ThS. Bác sĩ',
      specialtyId: 'spec-2',
      specialtyName: 'Tim mạch',
      consultationFee: 200000,
      serviceDate: today,
      roomCode: 'P204',
      roomName: 'Phòng khám P204 - Tầng 2',
      totalSlots: 30,
      remainingSlots: 5,
      status: 'Open',
      bio: 'Chuyên sâu Tăng huyết áp, Rối loạn lipid máu và Siêu âm tim.',
    },
    {
      doctorId: 'doc-3',
      fullName: 'PGS.TS Lê Hoàng Nam',
      doctorRankId: 'rank-3',
      doctorRankName: 'PGS.TS. Bác sĩ',
      specialtyId: 'spec-2',
      specialtyName: 'Tim mạch',
      consultationFee: 350000,
      serviceDate: tomorrow,
      roomCode: 'P301',
      roomName: 'Phòng khám P301 - Tầng 3',
      totalSlots: 30,
      remainingSlots: 0,
      status: 'Full',
      bio: 'Chuyên gia đầu ngành Tim mạch can thiệp và Suy tim.',
    },
    {
      doctorId: 'doc-4',
      fullName: 'BS.CKI Võ Ngọc Linh',
      doctorRankId: 'rank-1',
      doctorRankName: 'Bác sĩ CKI',
      specialtyId: 'spec-3',
      specialtyName: 'Nhi khoa',
      consultationFee: 150000,
      serviceDate: tomorrow,
      roomCode: 'P108',
      roomName: 'Phòng khám P108 - Tầng 1',
      totalSlots: 30,
      remainingSlots: 14,
      status: 'Open',
      bio: 'Khám hô hấp nhi, tiêu hóa và tư vấn dinh dưỡng.',
    },
  ];

  const attempts: Record<string, BookingAttemptDetail> = {
    'attempt-recover-1': {
      bookingAttemptId: 'attempt-recover-1',
      status: 'Hold',
      doctorId: 'doc-2',
      doctorName: 'ThS.BS Phạm Thu Hà',
      specialtyName: 'Tim mạch',
      serviceDate: today,
      roomName: 'Phòng khám P204 - Tầng 2',
      patientProfileId: 'prof-self',
      patientName: 'Nguyễn Minh Tâm',
      amount: 200000,
      expiresAt: new Date(Date.now() + 4 * 60 * 1000).toISOString(),
    },
  };

  const appointments: PatientAppointmentItem[] = [
    {
      id: 'apt-1',
      appointmentCode: 'SH-261111-001',
      patientProfileId: 'prof-self',
      patientName: 'Nguyễn Minh Tâm',
      doctorId: 'doc-1',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      doctorRankName: 'Bác sĩ CKI',
      specialtyName: 'Nội tổng quát',
      roomName: 'Phòng khám P201 - Tầng 2',
      serviceDate: today,
      consultationFee: 150000,
      status: 'CheckedIn',
      queueNumber: 12,
      canCancel: false,
      canReschedule: false,
    },
    {
      id: 'apt-2',
      appointmentCode: 'SH-261112-008',
      patientProfileId: 'prof-mom',
      patientName: 'Lê Thị Hoa',
      doctorId: 'doc-4',
      doctorName: 'BS.CKI Võ Ngọc Linh',
      doctorRankName: 'Bác sĩ CKI',
      specialtyName: 'Nhi khoa',
      roomName: 'Phòng khám P108 - Tầng 1',
      serviceDate: tomorrow,
      consultationFee: 150000,
      status: 'Confirmed',
      queueNumber: null,
      canCancel: true,
      canReschedule: true,
    },
  ];

  const encounters: PatientEncounterSummary[] = [
    {
      id: 'enc-today-1',
      encounterCode: 'HS-20261111-004',
      serviceDate: today,
      patientProfileId: 'prof-self',
      patientName: 'Nguyễn Minh Tâm',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'InProgress',
      isPublished: false,
      primaryDiagnosis: undefined,
      hasClsResults: false,
      hasPrescription: false,
    },
    {
      id: 'enc-hist-1',
      encounterCode: 'HS-20261015-092',
      serviceDate: '2026-10-15',
      patientProfileId: 'prof-self',
      patientName: 'Nguyễn Minh Tâm',
      doctorName: 'ThS.BS Phạm Thu Hà',
      specialtyName: 'Tim mạch',
      status: 'Published',
      isPublished: true,
      primaryDiagnosis: 'Tăng huyết áp vô căn (nguyên phát)',
      hasClsResults: true,
      hasPrescription: true,
    },
  ];

  const encounterDetails: Record<string, PatientEncounterDetail> = {
    'enc-today-1': {
      id: 'enc-today-1',
      encounterCode: 'HS-20261111-004',
      serviceDate: today,
      patientName: 'Nguyễn Minh Tâm',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'InProgress',
      isPublished: false,
      version: 1,
      hasPrescription: false,
      vitals: {
        systolicBp: 134,
        diastolicBp: 84,
        heartRate: 76,
        spo2: 99,
        temperatureC: 36.7,
        weightKg: 68,
        heightCm: 172,
        recordedAt: new Date(Date.now() - 20 * 60 * 1000).toISOString(),
      },
      clsResults: [],
    },
    'enc-hist-1': {
      id: 'enc-hist-1',
      encounterCode: 'HS-20261015-092',
      serviceDate: '2026-10-15',
      patientName: 'Nguyễn Minh Tâm',
      doctorName: 'ThS.BS Phạm Thu Hà',
      specialtyName: 'Tim mạch',
      status: 'Published',
      isPublished: true,
      version: 2,
      hasPrescription: true,
      vitals: {
        systolicBp: 138,
        diastolicBp: 88,
        heartRate: 78,
        spo2: 98,
        temperatureC: 36.6,
        weightKg: 68,
        heightCm: 172,
        recordedAt: '2026-10-15T08:20:00Z',
      },
      symptoms: 'Thỉnh thoảng hồi hộp, đau đầu nhẹ buổi sáng khi thức dậy.',
      clinicalNotes: 'Tim đều T1, T2 rõ; không phù ngoại vi; phổi thông khí tốt.',
      diagnoses: [{ icdCode: 'I10', description: 'Tăng huyết áp vô căn (nguyên phát)', isPrimary: true }],
      conclusion: 'Tăng huyết áp vô căn độ 1 (theo ESC/ESH). Chỉ định điều trị nội khoa ngoại trú kết hợp thay đổi lối sống.',
      followUpInstruction: 'Uống thuốc đúng giờ, hạn chế ăn mặn (<5g muối/ngày), đo HA sáng-tối và tái khám sau 30 ngày.',
      clsResults: [
        {
          id: 'cls-ord-01',
          serviceCode: 'XN-SH',
          serviceName: 'Sinh hóa máu cơ bản (Glucose, Ure, Creatinin, Điện giải đồ)',
          status: 'Published',
          summaryText: 'Glucose máu lúc đói 5.4 mmol/L, Creatinin 82 µmol/L, eGFR > 90. Các chỉ số trong giới hạn sinh lý.',
          technicalConclusion: 'Chức năng thận và đường huyết bình thường.',
          readingDoctorName: 'BS.CKII Đỗ Văn Hùng',
          publishedAt: '2026-10-15T10:15:00Z',
          attachments: [
            {
              fileId: 'cls-f-1',
              fileName: 'Phieu-ket-qua-sinh-hoa-20261015.pdf',
              mimeType: 'application/pdf',
              sizeBytes: 102400,
              uploadedAt: '2026-10-15T09:00:00Z',
            },
          ],
        },
      ],
    },
  };

  const prescription: PatientPrescriptionDetail = {
    encounterId: 'enc-hist-1',
    encounterCode: 'HS-20261015-092',
    patientName: 'Nguyễn Minh Tâm',
    doctorName: 'ThS.BS Phạm Thu Hà',
    issuedAt: '2026-10-15T10:30:00Z',
    disclaimer: 'Hệ thống bệnh viện không bán thuốc trực tuyến. Đơn thuốc chỉ có giá trị mua tại các nhà thuốc đạt chuẩn GPP.',
    items: [
      {
        id: 'rx-item-1',
        drugName: 'Amlodipin 5mg',
        activeIngredient: 'Amlodipine besylate',
        dosage: '1 viên/lần',
        frequency: '1 lần/ngày (uống buổi sáng sau ăn)',
        durationDays: 30,
        quantity: 30,
        unit: 'Viên',
        usageInstruction: 'Uống nguyên viên với một cốc nước lọc.',
      },
    ],
  };

  const costSummary: EncounterCostSummary = {
    encounterId: 'enc-hist-1',
    serviceDate: '2026-10-15',
    doctorName: 'ThS.BS Phạm Thu Hà',
    specialtyName: 'Tim mạch',
    totalPayable: 550000,
    totalPaid: 520000,
    totalApprovedRefund: 50000,
    totalCompletedRefund: 50000,
    netHeld: 470000,
    totalWaived: 30000,
    lineItems: [
      {
        id: 'li-1',
        category: 'Consultation',
        description: 'Khám chuyên khoa Tim mạch',
        grossAmount: 200000,
        waiverAmount: 0,
        netAmount: 200000,
        status: 'Paid',
      },
      {
        id: 'li-2',
        category: 'Cls',
        description: 'Siêu âm tim Doppler màu (miễn giảm 30.000 ₫)',
        grossAmount: 350000,
        waiverAmount: 30000,
        netAmount: 320000,
        status: 'Paid',
      },
    ],
  };

  const notifications: PatientNotificationItem[] = [
    {
      id: 'noti-1',
      title: 'Mời bạn thanh toán chỉ định cận lâm sàng',
      body: 'Bạn có 2 dịch vụ CLS cần thanh toán trong 15 phút để vào phòng thực hiện.',
      category: 'Payment',
      status: 'Sent',
      createdAt: new Date(Date.now() - 4 * 60 * 1000).toISOString(),
      targetPath: '/cls-payments',
    },
  ];

  const displayBoard: PublicDisplayBoardDto = {
    hospitalName: 'Bệnh viện Đa khoa Smart Healthcare',
    generatedAt: new Date().toISOString(),
    rooms: [
      {
        roomCode: 'P201',
        roomName: 'Phòng khám P201 - Tầng 2',
        stationType: 'Clinic',
        currentTicketNumber: 'A-012',
        waitingTicketNumbers: ['A-013', 'A-014', 'A-015'],
        updatedAt: new Date().toISOString(),
      },
    ],
  };

  const chatMessages: ChatMessageDto[] = [
    {
      id: 'msg-welcome',
      role: 'assistant',
      content: 'Xin chào! Tôi là trợ lý ảo Smart Healthcare. Bạn cần hỗ trợ gì hôm nay?',
      createdAt: new Date().toISOString(),
    },
  ];

  const tickets: Record<string, AppointmentTicketDetail> = {
    'apt-1': {
      appointmentId: 'apt-1',
      appointmentCode: 'SH-261111-001',
      qrPayload: 'SH:APT:SH-261111-001:P201:A-012',
      patientName: 'Nguyễn Minh Tâm',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      serviceDate: today,
      roomName: 'Phòng khám P201 - Tầng 2',
      status: 'CheckedIn',
      queueNumber: 12,
      checkInInstruction: 'Bạn đã check-in thành công.',
    },
    'apt-2': {
      appointmentId: 'apt-2',
      appointmentCode: 'SH-261112-008',
      qrPayload: 'SH:APT:SH-261112-008:P108',
      patientName: 'Lê Thị Hoa',
      doctorName: 'BS.CKI Võ Ngọc Linh',
      specialtyName: 'Nhi khoa',
      serviceDate: tomorrow,
      roomName: 'Phòng khám P108 - Tầng 1',
      status: 'Confirmed',
      queueNumber: null,
      checkInInstruction: 'Vui lòng có mặt trước giờ khám.',
    },
  };

  return {
    today,
    tomorrow,
    doctors,
    attempts,
    appointments,
    encounters,
    encounterDetails,
    prescription,
    costSummary,
    notifications,
    displayBoard,
    chatMessages,
    tickets,
    vnpayVerifyPollCount: {} as Record<string, number>,
  };
}

class PatientApiMock {
  private store = createPatientRealStore();
  private overrides: OverrideEntry[] = [];

  constructor() {
    this.install();
  }

  reset() {
    this.store = createPatientRealStore();
    this.overrides = [];
  }

  override(method: string, pattern: string, handler: RouteHandler) {
    this.overrides.unshift({ method: method.toUpperCase(), pattern, handler });
  }

  private install() {
    vi.spyOn(apiClient, 'get').mockImplementation(async (url: string, config?: any) => {
      return this.handleRequest('GET', url, undefined, config);
    });

    vi.spyOn(apiClient, 'post').mockImplementation(async (url: string, body?: any, config?: any) => {
      return this.handleRequest('POST', url, body, config);
    });

    vi.spyOn(apiClient, 'put').mockImplementation(async (url: string, body?: any, config?: any) => {
      return this.handleRequest('PUT', url, body, config);
    });

    vi.spyOn(apiClient, 'delete').mockImplementation(async (url: string, config?: any) => {
      return this.handleRequest('DELETE', url, undefined, config);
    });
  }

  private async handleRequest(method: string, rawUrl: string, body?: unknown, config?: any): Promise<any> {
    const [pathname, queryString] = (rawUrl || '').split('?');
    const query = new URLSearchParams(queryString || '');
    if (config?.params) {
      for (const [k, v] of Object.entries(config.params)) {
        if (v !== undefined && v !== null) {
          query.set(k, String(v));
        }
      }
    }

    // 1. Check custom test overrides
    for (const ov of this.overrides) {
      if (ov.method === method) {
        const params = matchPath(ov.pattern, pathname);
        if (params !== null) {
          const res = await ov.handler(rawUrl, body, params, query);
          return this.respond(res, config, rawUrl);
        }
      }
    }

    // 2. Default real backend schema handlers
    const res = await this.defaultDispatch(method, pathname, body, query);
    return this.respond(res, config, rawUrl);
  }

  private respond(res: RouteHandlerResult, config: any, url: string) {
    const status = res.status ?? 200;
    if (status >= 400) {
      const err: any = new Error(`Request failed with status code ${status}`);
      err.name = 'AxiosError';
      err.isAxiosError = true;
      err.response = {
        status,
        statusText: status === 404 ? 'Not Found' : status === 409 ? 'Conflict' : 'Error',
        headers: res.headers ?? {},
        config: config ?? { url },
        data: res.data,
      };
      return Promise.reject(err);
    }
    return {
      data: res.data,
      status,
      statusText: 'OK',
      headers: res.headers ?? { 'content-type': 'application/json' },
      config: config ?? { url },
    };
  }

  private defaultDispatch(
    method: string,
    pathname: string,
    body: any,
    query: URLSearchParams,
  ): RouteHandlerResult {
    // Auth / Me
    if (pathname === '/me') {
      return { data: { userId: 'u-patient-1', userName: 'nguyenvana', role: 'Patient' } };
    }

    // Profiles for identity
    if (pathname === '/api/identity/patient/profiles') {
      return { data: MOCK_PROFILES };
    }

    // PatientProfileDto for patient portal
    if (pathname === '/api/patient/profiles') {
      return {
        data: MOCK_PROFILES.map((p) => ({
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
      };
    }

    // Public catalogs
    if (pathname === '/api/identity/public/specialties') {
      return { data: MOCK_SPECIALTIES };
    }
    if (pathname === '/api/identity/public/doctor-ranks') {
      return { data: MOCK_RANKS };
    }

    // Search doctors
    if (pathname === '/api/scheduling/patient/doctors') {
      const serviceDate = query.get('serviceDate') || this.store.today;
      const specialtyId = query.get('specialtyId') || '';
      const doctorRankId = query.get('doctorRankId') || '';

      const items = this.store.doctors
        .filter((d) => {
          if (specialtyId && d.specialtyId !== specialtyId) return false;
          if (doctorRankId && d.doctorRankId !== doctorRankId) return false;
          return true;
        })
        .map((d) => ({
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
        }));

      return {
        data: {
          serviceDate,
          page: 1,
          pageSize: 20,
          hasMore: false,
          items,
        },
      };
    }

    // Booking attempt payment & checkout
    const paymentMatch = matchPath('/api/scheduling/patient/booking-attempts/:id/payment', pathname);
    if (paymentMatch && method === 'POST') {
      return {
        data: {
          paymentIntentId: `pi-${paymentMatch.id}`,
          intentNumber: `PI-${paymentMatch.id}`,
          amount: 200000,
        },
      };
    }

    const checkoutMatch = matchPath('/api/billing/patient/payments/:id/checkout', pathname);
    if (checkoutMatch && method === 'POST') {
      return {
        data: {
          paymentUrl: '/payment/vnpay-return',
          expiresAtUtc: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
        },
      };
    }

    // Get booking attempt (Held with expiresAtUtc)
    const attemptMatch = matchPath('/api/scheduling/patient/booking-attempts/:id', pathname);
    if (attemptMatch && method === 'GET') {
      const att = this.store.attempts[attemptMatch.id];
      const expiresAt = att?.expiresAt ?? new Date(Date.now() + 5 * 60 * 1000).toISOString();
      return {
        data: {
          bookingAttemptId: attemptMatch.id,
          status: 'Held',
          currentStep: 'WaitingPayment',
          expiresAtUtc: expiresAt,
          allowedActions: ['SelectDoctor', 'Pay', 'Cancel'],
        },
      };
    }

    // Payments detail
    const piMatch = matchPath('/api/billing/patient/payments/:id', pathname);
    if (piMatch && method === 'GET') {
      const pid = piMatch.id;
      if (pid === 'pi-so-01') {
        const count = (this.store.vnpayVerifyPollCount[pid] = (this.store.vnpayVerifyPollCount[pid] || 0) + 1);
        if (count === 1) {
          return {
            data: {
              paymentIntentId: pid,
              intentNumber: 'PI-SO-01',
              amount: 150000,
              status: 0, // Pending -> Verifying
              paidAtUtc: null,
            },
          };
        }
        return {
          data: {
            paymentIntentId: pid,
            intentNumber: 'PI-SO-01',
            amount: 150000,
            status: 2, // Succeeded -> Paid
            paidAtUtc: new Date().toISOString(),
          },
        };
      }
      return {
        data: {
          paymentIntentId: pid,
          intentNumber: `PI-${pid}`,
          amount: 150000,
          status: 'Pending',
          paidAtUtc: null,
        },
      };
    }

    // Reschedule options
    const reschedOptsMatch = matchPath('/api/scheduling/patient/appointments/:id/reschedule-options', pathname);
    if (reschedOptsMatch) {
      const options: RescheduleOption[] = [
        {
          doctorId: 'doc-1',
          doctorName: 'BS.CKI Trần Quốc Khánh',
          doctorRankName: 'Bác sĩ CKI',
          specialtyName: 'Nội tổng quát',
          roomName: 'Phòng khám P201',
          serviceDate: this.store.tomorrow,
          consultationFee: 200000,
          remainingSlots: 8,
          sameFeeEligible: false,
        },
        {
          doctorId: 'doc-4',
          doctorName: 'BS.CKI Võ Ngọc Linh',
          doctorRankName: 'Bác sĩ CKI',
          specialtyName: 'Nhi khoa',
          roomName: 'Phòng khám P108',
          serviceDate: this.store.tomorrow,
          consultationFee: 150000,
          remainingSlots: 14,
          sameFeeEligible: true,
        },
      ];
      return { data: options };
    }

    // Cancel refund preview
    const cancelPrevMatch = matchPath('/api/scheduling/patient/appointments/:id/cancel-preview', pathname);
    if (cancelPrevMatch) {
      return {
        data: {
          appointmentId: cancelPrevMatch.id,
          originalAmount: 150000,
          refundAmount: 150000,
          refundRatePercent: 100,
          policyExplanation: 'Hủy trước 12h ngày khám được hoàn 100%.',
          eligibleToCancel: true,
        },
      };
    }

    // Appointments list
    if (pathname === '/api/scheduling/patient/appointments') {
      return {
        data: {
          items: this.store.appointments,
          hasMore: false,
        },
      };
    }

    // Appointment detail
    const aptMatch = matchPath('/api/scheduling/patient/appointments/:id', pathname);
    if (aptMatch && method === 'GET') {
      const apt = this.store.appointments.find((a) => a.id === aptMatch.id) || this.store.tickets[aptMatch.id];
      if (apt) return { data: apt };
      return {
        data: {
          id: aptMatch.id,
          appointmentCode: 'SH-261112-008',
          patientProfileId: 'prof-mom',
          patientName: 'Lê Thị Hoa',
          doctorId: 'doc-4',
          doctorName: 'BS.CKI Võ Ngọc Linh',
          doctorRankName: 'Bác sĩ CKI',
          specialtyName: 'Nhi khoa',
          roomName: 'Phòng khám P108 - Tầng 1',
          serviceDate: this.store.tomorrow,
          consultationFee: 150000,
          status: 'Confirmed',
          queueNumber: null,
          canCancel: true,
          canReschedule: true,
        },
      };
    }

    // Encounter cost summary
    if (
      matchPath('/api/billing/patient/encounters/:id/cost-summary', pathname) ||
      matchPath('/api/clinical/patient/encounters/:id/cost', pathname)
    ) {
      return { data: this.store.costSummary };
    }

    // Prescription detail
    const rxMatch =
      matchPath('/api/clinical/patient/encounters/:id/prescription', pathname) ||
      matchPath('/api/medication/patient/encounters/:id/prescription', pathname);
    if (rxMatch) {
      return { data: this.store.prescription };
    }

    // Encounters list
    if (pathname === '/api/clinical/patient/encounters') {
      const profileId = query.get('patientProfileId');
      if (profileId === 'prof-pending') {
        return { data: { items: [], hasMore: false, blockedPendingProfile: true } };
      }
      return { data: { items: this.store.encounters, hasMore: false } };
    }

    // Encounter detail
    const encMatch = matchPath('/api/clinical/patient/encounters/:id', pathname);
    if (encMatch && method === 'GET') {
      const enc = this.store.encounterDetails[encMatch.id];
      if (enc) return { data: enc };
      return {
        data: {
          id: encMatch.id,
          encounterCode: 'HS-SAMPLE',
          serviceDate: this.store.today,
          patientName: 'Nguyễn Minh Tâm',
          doctorName: 'BS.CKI Trần Quốc Khánh',
          specialtyName: 'Nội tổng quát',
          status: 'InProgress',
          isPublished: false,
          version: 1,
          hasPrescription: false,
          clsResults: [],
        },
      };
    }

    // File download
    if (pathname.includes('/download')) {
      return {
        data: new Blob(['%PDF-1.4\n% Sample CLS Report PDF\n'], { type: 'application/pdf' }),
        headers: {
          'content-type': 'application/pdf',
          'content-disposition': 'attachment; filename="Phieu-ket-qua-sinh-hoa-20261015.pdf"',
        },
      };
    }

    // Chat health
    if (pathname === '/api/notifications/patient/chat/health') {
      return { data: { available: true } };
    }

    // Chat sessions
    if (pathname === '/api/notifications/patient/chat/sessions' && method === 'POST') {
      return {
        data: {
          sessionId: 'chat-session-1',
          quickSuggestions: ['Đau ngực khó thở cần làm gì?'],
          messages: this.store.chatMessages,
        },
      };
    }

    const chatMsgMatch = matchPath('/api/notifications/patient/chat/sessions/:id/messages', pathname);
    if (chatMsgMatch && method === 'POST') {
      const text = (body?.content || '').toLowerCase();
      if (text.includes('đau ngực') || text.includes('khó thở') || text.includes('115')) {
        return {
          data: {
            id: `bot-${Date.now()}`,
            role: 'assistant',
            content:
              'Triệu chứng bạn mô tả có thể là dấu hiệu cấp cứu nguy hiểm. Vui lòng gọi ngay 115 hoặc đến cơ sở cấp cứu gần nhất ngay lập tức.',
            createdAt: new Date().toISOString(),
            isRedFlagEmergency: true,
            emergencyPhone: '115',
          },
        };
      }
      return {
        data: {
          id: `bot-${Date.now()}`,
          role: 'assistant',
          content: 'Cảm ơn bạn đã liên hệ.',
          createdAt: new Date().toISOString(),
        },
      };
    }

    // Public display
    if (pathname === '/api/scheduling/public/display') {
      return { data: this.store.displayBoard };
    }

    return { status: 404, data: { code: 'NOT_FOUND', detail: `Endpoint not found: ${pathname}` } };
  }
}

export const patientApiMock = new PatientApiMock();
