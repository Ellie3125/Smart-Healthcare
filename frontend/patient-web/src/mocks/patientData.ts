import { getVNDateISO } from '../shared/lib/datetime';
import type {
  AppointmentTicketDetail,
  BookingAttemptDetail,
  DoctorRankOption,
  DoctorSearchItem,
  PatientAppointmentItem,
  SpecialtyOption,
} from '../types/booking';
import type {
  ClsPaymentBatch,
  EncounterCostSummary,
  PatientEncounterDetail,
  PatientEncounterSummary,
  PatientPrescriptionDetail,
  RefundRequestItem,
  TodayJourneyDto,
} from '../types/clinical';
import type {
  ChatMessageDto,
  PatientNotificationItem,
  PublicDisplayBoardDto,
} from '../types/assistant';

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

const today = getVNDateISO();
const tomorrowDate = new Date(Date.now() + 24 * 60 * 60 * 1000);
const tomorrow = getVNDateISO(tomorrowDate);

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

export const MOCK_DOCTORS: DoctorSearchItem[] = [
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

export function createInitialPatientStore() {
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
      checkedInAt: new Date(Date.now() - 45 * 60 * 1000).toISOString(),
      canReschedule: false,
      canCancel: false,
      rescheduleBlockedReason: 'Lịch đã check-in tại quầy không thể tự đổi trực tuyến.',
      cancelBlockedReason: 'Vui lòng liên hệ quầy tiếp đón nếu cần hủy lịch sau khi check-in.',
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
      checkedInAt: null,
      canReschedule: true,
      canCancel: true,
    },
  ];

  const clsBatches: ClsPaymentBatch[] = [
    {
      batchId: 'batch-1',
      encounterId: 'enc-today-1',
      appointmentCode: 'SH-261111-001',
      createdAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
      expiresAt: new Date(Date.now() + 14 * 60 * 1000).toISOString(),
      status: 'PendingPayment',
      totalGrossAmount: 420000,
      totalWaiverAmount: 50000,
      totalPayableAmount: 370000,
      items: [
        {
          id: 'cli-1',
          serviceCode: 'XN-CTM',
          serviceName: 'Tổng phân tích tế bào máu ngoại vi',
          roomName: 'Phòng Xét nghiệm 01',
          unitPrice: 120000,
          waiverAmount: 0,
          netPrice: 120000,
          status: 'PendingPayment',
        },
        {
          id: 'cli-2',
          serviceCode: 'SA-TIM',
          serviceName: 'Siêu âm Doppler tim',
          roomName: 'Phòng Siêu âm 02',
          unitPrice: 300000,
          waiverAmount: 50000,
          netPrice: 250000,
          status: 'PendingPayment',
        },
      ],
    },
  ];

  const refunds: RefundRequestItem[] = [
    {
      id: 'ref-1',
      referenceCode: 'RF-20261110-003',
      reason: 'Hủy lịch khám trước 05:00 sáng ngày khám',
      amount: 150000,
      status: 'Approved',
      payoutChannel: null,
      createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
      updatedAt: new Date(Date.now() - 20 * 60 * 60 * 1000).toISOString(),
    },
  ];

  const encounters: PatientEncounterSummary[] = [
    {
      id: 'enc-hist-1',
      encounterCode: 'HS-20261015-019',
      patientProfileId: 'prof-self',
      patientName: 'Nguyễn Minh Tâm',
      serviceDate: '2026-10-15',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'Published',
      isPublished: true,
      primaryDiagnosis: 'I10 - Tăng huyết áp vô căn',
      hasClsResults: true,
      hasPrescription: true,
    },
    {
      id: 'enc-today-1',
      encounterCode: 'HS-20261111-004',
      patientProfileId: 'prof-self',
      patientName: 'Nguyễn Minh Tâm',
      serviceDate: today,
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'AwaitingCls',
      isPublished: false,
      hasClsResults: false,
      hasPrescription: false,
    },
  ];

  const encounterDetails: Record<string, PatientEncounterDetail> = {
    'enc-hist-1': {
      id: 'enc-hist-1',
      encounterCode: 'HS-20261015-019',
      serviceDate: '2026-10-15',
      patientName: 'Nguyễn Minh Tâm',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'Published',
      isPublished: true,
      version: 2,
      publishedAt: '2026-10-15T10:35:00+07:00',
      vitals: {
        systolicBp: 138,
        diastolicBp: 86,
        heartRate: 78,
        temperatureC: 36.7,
        spo2: 98,
        weightKg: 68,
        heightCm: 171,
        recordedAt: '2026-10-15T08:15:00+07:00',
      },
      symptoms: 'Đau đầu nhẹ vùng chẩm buổi sáng, hồi hộp thoáng qua.',
      clinicalNotes: 'Nhịp tim đều, phổi trong, không phù ngoại biên.',
      diagnoses: [{ icdCode: 'I10', description: 'Tăng huyết áp vô căn (nguyên phát)', isPrimary: true }],
      conclusion: 'Tăng huyết áp độ I ổn định, tiếp tục điều trị nội khoa và tái khám sau 30 ngày.',
      followUpInstruction: 'Đo huyết áp sáng tối hằng ngày, hạn chế muối < 5g/ngày.',
      clsResults: [
        {
          id: 'cls-res-1',
          serviceCode: 'XN-SHM',
          serviceName: 'Sinh hóa máu cơ bản (Glucose, Lipid, Creatinin)',
          status: 'Published',
          summaryText: 'Glucose đói 5.4 mmol/L, Cholesterol toàn phần 5.6 mmol/L (tăng nhẹ).',
          technicalConclusion: 'Rối loạn lipid máu mức độ nhẹ.',
          readingDoctorName: 'BS.CKII Đỗ Văn Hùng',
          publishedAt: '2026-10-15T09:50:00+07:00',
          attachments: [
            {
              fileId: 'file-pdf-1',
              fileName: 'Phieu-ket-qua-sinh-hoa-20261015.pdf',
              mimeType: 'application/pdf',
              sizeBytes: 245120,
              uploadedAt: '2026-10-15T09:48:00+07:00',
            },
          ],
        },
      ],
      hasPrescription: true,
    },
    'enc-today-1': {
      id: 'enc-today-1',
      encounterCode: 'HS-20261111-004',
      serviceDate: today,
      patientName: 'Nguyễn Minh Tâm',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'AwaitingCls',
      isPublished: false,
      version: 1,
      clsResults: [],
      hasPrescription: false,
    },
  };

  const prescription: PatientPrescriptionDetail = {
    encounterId: 'enc-hist-1',
    encounterCode: 'HS-20261015-019',
    patientName: 'Nguyễn Minh Tâm',
    doctorName: 'BS.CKI Trần Quốc Khánh',
    issuedAt: '2026-10-15T10:35:00+07:00',
    disclaimer:
      'Đơn thuốc chỉ có giá trị tham khảo và mua thuốc tại nhà thuốc hợp pháp theo tư vấn dược sĩ. Hệ thống bệnh viện không bán thuốc trực tuyến.',
    notes: 'Tái khám mang theo sổ theo dõi huyết áp.',
    items: [
      {
        id: 'rx-1',
        drugName: 'Amlodipin 5mg',
        activeIngredient: 'Amlodipine besilate',
        dosage: '1 viên/lần',
        frequency: 'Ngày 1 lần buổi sáng sau ăn',
        durationDays: 30,
        quantity: 30,
        unit: 'Viên',
        usageInstruction: 'Uống nguyên viên với nước lọc vào khung giờ cố định.',
      },
      {
        id: 'rx-2',
        drugName: 'Atorvastatin 10mg',
        activeIngredient: 'Atorvastatin',
        dosage: '1 viên/lần',
        frequency: 'Ngày 1 lần buổi tối',
        durationDays: 30,
        quantity: 30,
        unit: 'Viên',
        usageInstruction: 'Duy trì chế độ ăn giảm mỡ bão hòa.',
      },
    ],
  };

  const costSummary: EncounterCostSummary = {
    encounterId: 'enc-hist-1',
    serviceDate: '2026-10-15',
    doctorName: 'BS.CKI Trần Quốc Khánh',
    specialtyName: 'Nội tổng quát',
    totalPaid: 520000,
    totalApprovedRefund: 150000,
    netHeld: 370000,
    totalWaived: 50000,
    totalCompletedRefund: 0,
    lineItems: [
      {
        id: 'line-1',
        category: 'Khám lâm sàng',
        description: 'Khám Nội tổng quát (BS.CKI)',
        grossAmount: 150000,
        waiverAmount: 0,
        netAmount: 150000,
        status: 'Paid',
      },
      {
        id: 'line-2',
        category: 'Cận lâm sàng',
        description: 'Gói xét nghiệm & siêu âm tim',
        grossAmount: 420000,
        waiverAmount: 50000,
        netAmount: 370000,
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
    {
      id: 'noti-2',
      title: 'Đã xác nhận check-in quầy tiếp đón',
      body: 'Số thứ tự của bạn tại Phòng khám P201 là A-012.',
      category: 'Queue',
      status: 'Read',
      createdAt: new Date(Date.now() - 45 * 60 * 1000).toISOString(),
      targetPath: '/today-journey',
    },
  ];

  const todayJourney: TodayJourneyDto = {
    patientProfileId: 'prof-self',
    patientName: 'Nguyễn Minh Tâm',
    serviceDate: today,
    appointmentId: 'apt-1',
    encounterStatus: 'AwaitingCls',
    currentCallText: 'Mời số A-012 thanh toán chỉ định CLS và di chuyển sang Phòng Xét nghiệm 01.',
    unpaidClsCount: 2,
    steps: [
      {
        id: 'step-checkin',
        title: 'Check-in tại quầy tiếp đón',
        location: 'Quầy Q01 - Sảnh chính',
        status: 'Completed',
        queueNumber: 'A-012',
        detail: 'Đã đối chiếu giấy tờ tùy thân.',
      },
      {
        id: 'step-vitals',
        title: 'Đo sinh hiệu điều dưỡng',
        location: 'Bàn điều dưỡng S01',
        status: 'Completed',
        detail: 'HA 134/84 mmHg, Mạch 76 l/p, SpO2 99%.',
      },
      {
        id: 'step-clinic',
        title: 'Khám bác sĩ chuyên khoa',
        location: 'Phòng khám P201 - Tầng 2',
        status: 'InProgress',
        queueNumber: 'A-012',
        detail: 'Bác sĩ đã chỉ định 2 cận lâm sàng bổ sung.',
      },
      {
        id: 'step-cls',
        title: 'Thực hiện Cận lâm sàng',
        location: 'Phòng Xét nghiệm 01 & Siêu âm 02',
        status: 'Waiting',
        detail: 'Đang chờ thanh toán đợt chỉ định CLS.',
      },
    ],
  };

  const displayBoard: PublicDisplayBoardDto = {
    hospitalName: 'Bệnh viện Đa khoa Smart Healthcare',
    generatedAt: new Date().toISOString(),
    rooms: [
      {
        roomCode: 'P201',
        roomName: 'Phòng khám P201 - Nội tổng quát',
        stationType: 'Clinic',
        currentTicketNumber: 'A-012',
        waitingTicketNumbers: ['A-013', 'A-014', 'A-015'],
        updatedAt: new Date().toISOString(),
      },
      {
        roomCode: 'P204',
        roomName: 'Phòng khám P204 - Tim mạch',
        stationType: 'Clinic',
        currentTicketNumber: 'B-007',
        waitingTicketNumbers: ['B-008', 'B-009'],
        updatedAt: new Date().toISOString(),
      },
      {
        roomCode: 'XN01',
        roomName: 'Phòng Xét nghiệm 01',
        stationType: 'Cls',
        currentTicketNumber: 'CLS-021',
        waitingTicketNumbers: ['CLS-022', 'CLS-023'],
        updatedAt: new Date().toISOString(),
      },
    ],
  };

  const chatMessages: ChatMessageDto[] = [
    {
      id: 'msg-welcome',
      role: 'assistant',
      content: 'Xin chào! Tôi là trợ lý ảo Smart Healthcare. Bạn cần hỗ trợ tìm bác sĩ, chuẩn bị đi khám hay xem lịch trình?',
      createdAt: new Date(Date.now() - 60 * 1000).toISOString(),
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
      checkInInstruction: 'Bạn đã check-in thành công. Số thứ tự của bạn là A-012.',
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
      checkInInstruction: 'Vui lòng có mặt trước giờ khám ít nhất 15 phút và xuất trình QR cùng giấy tờ tùy thân tại quầy tiếp đón để nhận số thứ tự.',
    },
  };

  return {
    today,
    tomorrow,
    attempts,
    appointments,
    clsBatches,
    refunds,
    encounters,
    encounterDetails,
    prescription,
    costSummary,
    notifications,
    todayJourney,
    displayBoard,
    chatMessages,
    tickets,
    vnpayVerifyPollCount: {} as Record<string, number>,
  };
}
