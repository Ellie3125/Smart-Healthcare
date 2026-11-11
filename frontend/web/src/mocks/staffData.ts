import { getVNDateISO } from '../shared/lib/datetime';
import type {
  AdminRefundItem,
  AuditLogEntry,
  CashierPayableItem,
  ChatbotSpecialtyConfigItem,
  ClsRoomQueueItem,
  ClsRoomResultDraftDto,
  ClsWaiverRequestItem,
  DoctorDayQuotaItem,
  DoctorEncounterWorkspaceDto,
  DoctorQueueEntry,
  DrugCatalogItem,
  HospitalDeviceItem,
  Icd10CodeOption,
  MedicalServiceCatalogItem,
  NurseQueueItem,
  OperationalExceptionItem,
  PaymentReconcileRow,
  PriceListItem,
  ReceptionCallTaskItem,
  ReceptionSearchResult,
  RolePermissionMatrixRow,
  ServiceStationItem,
  ShiftReconcileSummary,
  StaffNotificationItem,
  VitalsRecordDto,
} from '../types/staffWorkflow';

export const MOCK_ICD10: Icd10CodeOption[] = [
  { code: 'I10', name: 'Tăng huyết áp vô căn (nguyên phát)' },
  { code: 'E11', name: 'Đái tháo đường không phụ thuộc insulin (type 2)' },
  { code: 'J06.9', name: 'Nhiễm khuẩn hô hấp trên cấp tính' },
  { code: 'K29.7', name: 'Viêm dạ dày, không đặc hiệu' },
];

export const MOCK_DRUGS: DrugCatalogItem[] = [
  { id: 'drug-1', name: 'Amlodipin 5mg', activeIngredient: 'Amlodipine', defaultDosage: '1 viên/lần', unit: 'Viên' },
  { id: 'drug-2', name: 'Paracetamol 500mg', activeIngredient: 'Paracetamol', defaultDosage: '1 viên/lần khi sốt >= 38.5°C', unit: 'Viên' },
  { id: 'drug-3', name: 'Omeprazol 20mg', activeIngredient: 'Omeprazole', defaultDosage: '1 viên trước ăn sáng 30 phút', unit: 'Viên' },
  { id: 'drug-4', name: 'Atorvastatin 10mg', activeIngredient: 'Atorvastatin', defaultDosage: '1 viên buổi tối', unit: 'Viên' },
];

export function createInitialStaffStore() {
  const today = getVNDateISO();

  const notifications: StaffNotificationItem[] = [
    {
      id: 'sn-1',
      title: 'Cảnh báo trễ check-in mới',
      body: 'Lịch SH-261111-009 đã quá CheckInDeadline và cần xử lý ngoại lệ.',
      category: 'Exception',
      status: 'Sent',
      createdAt: new Date(Date.now() - 8 * 60 * 1000).toISOString(),
      targetPath: '/admin/exceptions',
    },
    {
      id: 'sn-2',
      title: 'Yêu cầu hoàn tiền chờ duyệt',
      body: 'Có 1 yêu cầu hoàn tiền hủy lịch từ bệnh nhân Lê Thị Hoa.',
      category: 'Finance',
      status: 'Read',
      createdAt: new Date(Date.now() - 35 * 60 * 1000).toISOString(),
      targetPath: '/admin/refunds',
    },
  ];

  const stations: ServiceStationItem[] = [
    { id: 'st-1', code: 'Q01', name: 'Quầy tiếp đón số 1', stationType: 'Reception', floor: 'Tầng 1', isActive: true },
    { id: 'st-2', code: 'TN01', name: 'Quầy thu ngân số 1', stationType: 'Cashier', floor: 'Tầng 1', isActive: true },
    { id: 'st-3', code: 'SH01', name: 'Bàn đo sinh hiệu S01', stationType: 'Vitals', floor: 'Tầng 2', isActive: true },
  ];

  const doctorDays: DoctorDayQuotaItem[] = [
    {
      id: 'dd-1',
      doctorId: 'doc-1',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      doctorRankName: 'Bác sĩ CKI',
      specialtyName: 'Nội tổng quát',
      roomCode: 'P201',
      roomName: 'Phòng khám P201',
      serviceDate: today,
      totalSlots: 30,
      onlineBookedCount: 16,
      walkInBookedCount: 6,
      remainingSlots: 8,
      status: 'Open',
      version: 1,
    },
    {
      id: 'dd-2',
      doctorId: 'doc-2',
      doctorName: 'ThS.BS Phạm Thu Hà',
      doctorRankName: 'ThS. Bác sĩ',
      specialtyName: 'Tim mạch',
      roomCode: 'P204',
      roomName: 'Phòng khám P204',
      serviceDate: today,
      totalSlots: 30,
      onlineBookedCount: 20,
      walkInBookedCount: 5,
      remainingSlots: 5,
      status: 'Open',
      version: 1,
    },
  ];

  const medicalServices: MedicalServiceCatalogItem[] = [
    {
      id: 'svc-1',
      code: 'XN-CTM',
      name: 'Tổng phân tích tế bào máu ngoại vi',
      category: 'Lab',
      roomId: 'room-xn01',
      roomName: 'Phòng Xét nghiệm 01',
      hasActiveRoom: true,
      unitPrice: 120000,
      isActive: true,
    },
    {
      id: 'svc-2',
      code: 'SA-TIM',
      name: 'Siêu âm Doppler tim',
      category: 'Imaging',
      roomId: 'room-sa02',
      roomName: 'Phòng Siêu âm 02',
      hasActiveRoom: true,
      unitPrice: 300000,
      isActive: true,
    },
    {
      id: 'svc-3',
      code: 'CT-NGUC',
      name: 'Chụp CT lồng ngực (Đang bảo trì máy)',
      category: 'Imaging',
      roomId: 'room-ct01',
      roomName: 'Phòng CT-Scanner 01',
      hasActiveRoom: false,
      unitPrice: 1100000,
      isActive: true,
    },
  ];

  const priceLists: PriceListItem[] = [
    {
      id: 'pl-1',
      code: 'BG-2026-Q4',
      name: 'Bảng giá khám bệnh Quý 4/2026',
      effectiveFrom: '2026-10-01',
      consultationPrices: [
        { rankName: 'Bác sĩ CKI', price: 150000 },
        { rankName: 'ThS. Bác sĩ', price: 200000 },
        { rankName: 'PGS.TS. Bác sĩ', price: 350000 },
      ],
      isActive: true,
    },
  ];

  const devices: HospitalDeviceItem[] = [
    {
      id: 'dev-1',
      deviceCode: 'PC-P201',
      deviceName: 'Máy trạm Phòng P201',
      roomOrStationName: 'Phòng khám P201',
      assignedDate: today,
      accountUsername: 'room_p201',
      status: 'Active',
    },
    {
      id: 'dev-2',
      deviceCode: 'PC-XN01',
      deviceName: 'Máy trạm Phòng Xét nghiệm 01',
      roomOrStationName: 'Phòng Xét nghiệm 01',
      assignedDate: today,
      accountUsername: 'room_xn01',
      status: 'Active',
    },
  ];

  const permissionMatrix: RolePermissionMatrixRow[] = [
    { role: 'Admin', capabilities: ['Quản trị nhân sự, bác sĩ, phòng/quầy, ca trực', 'Duyệt hoàn tiền, miễn giảm CLS, đối soát & AuditLog'], restrictedNote: 'Không thao tác lâm sàng hay thu/check-in tại quầy' },
    { role: 'Receptionist', capabilities: ['Tìm kiếm, Walk-in, Check-in CCCD, Thu ngân read-only, Hủy/Chi hoàn mặt, Chốt ca, Gọi điện'], restrictedNote: 'Không sửa số tiền phải thu' },
    { role: 'Nurse', capabilities: ['Gọi lượt đo sinh hiệu, nhập & cảnh báo sinh hiệu bất thường'], restrictedNote: 'Không kê đơn hoặc kết luận chẩn đoán' },
    { role: 'Doctor', capabilities: ['Gọi người tiếp theo tuần tự, khám lâm sàng, ICD-10, chỉ định CLS, phát hành & kê đơn'], restrictedNote: 'Không xem tồn kho hay bán thuốc' },
    { role: 'RoomAccount', capabilities: ['Hàng đợi CLS phòng máy, tải tệp draft, phát hành kết quả CLS'], restrictedNote: '1 phiên thiết bị duy nhất; khóa xem sau khi Published' },
  ];

  const exceptions: OperationalExceptionItem[] = [
    {
      id: 'exc-1',
      type: 'CheckInExpired',
      referenceCode: 'SH-261111-009',
      patientName: 'Võ Văn Cường',
      phoneMasked: '*******842',
      detail: 'Quá hạn CheckInDeadline 30 phút so với khung ca sáng.',
      status: 'Open',
      createdAt: new Date(Date.now() - 25 * 60 * 1000).toISOString(),
    },
  ];

  const refundApprovals: AdminRefundItem[] = [
    {
      id: 'arf-1',
      referenceCode: 'RF-20261111-012',
      patientName: 'Lê Thị Hoa',
      reason: 'Hủy lịch trước 05:00 sáng ngày khám',
      amount: 150000,
      payoutChannel: 'OriginalVnpay',
      status: 'Pending',
      createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
      version: 1,
    },
  ];

  const reconcileRows: PaymentReconcileRow[] = [
    {
      id: 'rec-1',
      vnpTxnRef: 'VNP-20261111-8891',
      appointmentOrBatchCode: 'SH-261111-001',
      amount: 150000,
      localStatus: 'Paid',
      gatewayStatus: 'Matched',
      checkedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
    },
    {
      id: 'rec-2',
      vnpTxnRef: 'VNP-20261111-9012',
      appointmentOrBatchCode: 'CLS-BATCH-07',
      amount: 370000,
      localStatus: 'PendingPayment',
      gatewayStatus: 'Unknown',
    },
  ];

  const auditLogs: AuditLogEntry[] = [
    {
      id: 'aud-1',
      occurredAt: new Date(Date.now() - 40 * 60 * 1000).toISOString(),
      actorName: 'Nguyễn Thị Lan (Quản trị)',
      actorRole: 'Admin',
      action: 'DUYET_HOAN_TIEN',
      entityType: 'RefundRequest',
      entityId: 'RF-20261110-003',
      patientCccd: '079092001789',
      patientPhone: '0901234321',
      beforeJson: { status: 'Pending', approvedBy: null },
      afterJson: { status: 'Approved', approvedBy: 'admin_lan' },
    },
    {
      id: 'aud-2',
      occurredAt: new Date(Date.now() - 90 * 60 * 1000).toISOString(),
      actorName: 'BS.CKI Trần Quốc Khánh',
      actorRole: 'Doctor',
      action: 'BO_SUNG_HO_SO',
      entityType: 'Encounter',
      entityId: 'HS-20261015-019',
      patientCccd: '079065004123',
      patientPhone: '0918889654',
      beforeJson: { conclusion: 'Theo dõi tăng huyết áp' },
      afterJson: { conclusion: 'Tăng huyết áp độ I ổn định, tái khám sau 30 ngày' },
    },
  ];

  const clsWaivers: ClsWaiverRequestItem[] = [
    {
      id: 'waiv-1',
      encounterCode: 'HS-20261111-004',
      patientName: 'Nguyễn Minh Tâm',
      serviceName: 'Siêu âm Doppler tim',
      originalPrice: 300000,
      requestedWaiverAmount: 50000,
      reason: 'Chính sách hỗ trợ người bệnh tái khám nội viện',
      status: 'Pending',
    },
  ];

  const chatbotConfigs: ChatbotSpecialtyConfigItem[] = [
    {
      id: 'cfg-1',
      specialtyName: 'Tim mạch',
      symptomKeywords: ['hồi hộp', 'tăng huyết áp', 'nhịp tim nhanh'],
      redFlagKeywords: ['đau ngực dữ dội', 'khó thở', 'ngất xỉu'],
      disclaimerText: 'Chỉ mang tính gợi ý chuyên khoa ban đầu, không thay thế thăm khám trực tiếp.',
      isEnabled: true,
    },
  ];

  const receptionRecords: ReceptionSearchResult[] = [
    {
      id: 'recp-1',
      patientName: 'Nguyễn Minh Tâm',
      phone: '0901234567',
      cccdMasked: '*********789',
      dateOfBirth: '1992-04-18',
      verificationStatus: 'Verified',
      appointmentId: 'apt-checkedin-1',
      appointmentCode: 'SH-261111-001',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      serviceDate: today,
      appointmentStatus: 'CheckedIn',
      isPastCheckInDeadline: false,
      queueNumber: 12,
    },
    {
      id: 'recp-2',
      patientName: 'Phạm Hoàng Nam',
      phone: '0912345999',
      cccdMasked: '*********456',
      dateOfBirth: '1988-09-21',
      verificationStatus: 'Verified',
      appointmentId: 'apt-waiting-2',
      appointmentCode: 'SH-261111-014',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      serviceDate: today,
      appointmentStatus: 'Confirmed',
      checkInDeadline: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
      isPastCheckInDeadline: false,
      queueNumber: null,
    },
    {
      id: 'recp-3',
      patientName: 'Võ Văn Cường',
      phone: '0987654842',
      cccdMasked: '*********111',
      dateOfBirth: '1979-03-05',
      verificationStatus: 'Pending',
      appointmentId: 'apt-late-3',
      appointmentCode: 'SH-261111-009',
      doctorName: 'ThS.BS Phạm Thu Hà',
      serviceDate: today,
      appointmentStatus: 'Confirmed',
      checkInDeadline: new Date(Date.now() - 45 * 60 * 1000).toISOString(),
      isPastCheckInDeadline: true,
      queueNumber: null,
    },
  ];

  const cashierItems: CashierPayableItem[] = [
    {
      id: 'cash-1',
      referenceCode: 'CLS-BATCH-09',
      patientName: 'Phạm Hoàng Nam',
      itemType: 'ClsBatch',
      description: 'Xét nghiệm máu & Siêu âm ổ bụng',
      amount: 320000,
      status: 'PendingPayment',
    },
    {
      id: 'cash-2',
      referenceCode: 'WALKIN-002',
      patientName: 'Đặng Thị Mai',
      itemType: 'AppointmentFee',
      description: 'Phí khám Nội tổng quát (Walk-in)',
      amount: 150000,
      status: 'Paid',
      paidAt: new Date(Date.now() - 20 * 60 * 1000).toISOString(),
      receiptCode: 'BL-20261111-002',
    },
  ];

  const cashRefunds = [
    {
      id: 'crf-1',
      referenceCode: 'RF-CASH-001',
      patientName: 'Trần Văn B',
      amount: 150000,
      status: 'Approved' as 'Approved' | 'Refunded',
    },
  ];

  const shiftSummary: ShiftReconcileSummary = {
    shiftDate: today,
    cashierName: 'Lê Thu Trang (Thu ngân Q1)',
    expectedCashCollected: 1450000,
    expectedCashRefunded: 150000,
    netExpectedCash: 1300000,
    isClosed: false,
  };

  const callTasks: ReceptionCallTaskItem[] = [
    {
      id: 'call-1',
      patientName: 'Ngô Thị Thủy',
      phone: '0908887766',
      reason: 'Bác sĩ đổi phiên khám sáng nay (hủy trước 06:30) — cần liên hệ chuyển sang BS. Khánh',
      doctorName: 'BS.CKI Trần Quốc Khánh',
      serviceDate: today,
      status: 'Pending',
    },
  ];

  const nurseQueue: NurseQueueItem[] = [
    {
      encounterId: 'enc-nurse-1',
      ticketNumber: 'A-015',
      patientName: 'Phạm Hoàng Nam',
      yearOfBirth: 1988,
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'Waiting',
      version: 1,
    },
    {
      encounterId: 'enc-nurse-2',
      ticketNumber: 'A-016',
      patientName: 'Đặng Thị Mai',
      yearOfBirth: 1995,
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'Called',
      version: 1,
    },
  ];

  const vitalsForms: Record<string, VitalsRecordDto> = {
    'enc-nurse-1': {
      encounterId: 'enc-nurse-1',
      ticketNumber: 'A-015',
      patientName: 'Phạm Hoàng Nam',
      version: 1,
      systolicBp: '132',
      diastolicBp: '84',
      heartRate: '78',
      temperatureC: '36.8',
      spo2: '98',
      respiratoryRate: '18',
      weightKg: '72',
      heightCm: '170',
      nurseNote: '',
    },
    'enc-nurse-2': {
      encounterId: 'enc-nurse-2',
      ticketNumber: 'A-016',
      patientName: 'Đặng Thị Mai',
      yearOfBirth: 1995,
      version: 1,
      systolicBp: '',
      diastolicBp: '',
      heartRate: '',
      temperatureC: '',
      spo2: '',
      respiratoryRate: '',
      weightKg: '',
      heightCm: '',
      nurseNote: '',
    } as VitalsRecordDto,
  };

  const doctorInitialQueue: DoctorQueueEntry[] = [
    {
      encounterId: 'enc-doc-1',
      ticketNumber: 'A-012',
      patientName: 'Nguyễn Minh Tâm',
      queueKind: 'Initial',
      status: 'Waiting',
      checkedInAt: new Date(Date.now() - 35 * 60 * 1000).toISOString(),
    },
    {
      encounterId: 'enc-doc-2',
      ticketNumber: 'A-015',
      patientName: 'Phạm Hoàng Nam',
      queueKind: 'Initial',
      status: 'Waiting',
      checkedInAt: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
    },
  ];

  const doctorClsReturnQueue: DoctorQueueEntry[] = [
    {
      encounterId: 'enc-doc-ret-1',
      ticketNumber: 'A-008',
      patientName: 'Huỳnh Thị Hương',
      queueKind: 'ClsReturn',
      status: 'AwaitingCls',
      checkedInAt: new Date(Date.now() - 80 * 60 * 1000).toISOString(),
    },
  ];

  const doctorWorkspaces: Record<string, DoctorEncounterWorkspaceDto> = {
    'enc-doc-1': {
      encounterId: 'enc-doc-1',
      encounterCode: 'HS-20261111-004',
      ticketNumber: 'A-012',
      patientName: 'Nguyễn Minh Tâm',
      patientAge: 34,
      patientGender: 'Nam',
      status: 'InProgress',
      version: 2,
      vitalsSummary: 'HA 134/84 mmHg • Mạch 76 l/p • SpO2 99% • 36.7°C • 68kg',
      symptoms: 'Đau đầu nhẹ vùng chẩm, hồi hộp buổi sáng.',
      clinicalNotes: 'Nhịp tim đều, không tiếng thổi bệnh lý.',
      diagnoses: [{ code: 'I10', name: 'Tăng huyết áp vô căn (nguyên phát)' }],
      conclusion: '',
      followUpInstruction: 'Đo huyết áp hằng ngày tại nhà.',
      clsOrders: [],
      prescriptionItems: [],
      amendmentHistory: [],
    },
    'enc-doc-ret-1': {
      encounterId: 'enc-doc-ret-1',
      encounterCode: 'HS-20261111-002',
      ticketNumber: 'A-008',
      patientName: 'Huỳnh Thị Hương',
      patientAge: 46,
      patientGender: 'Nữ',
      status: 'ClsCompleted',
      version: 3,
      vitalsSummary: 'HA 124/80 mmHg • Mạch 74 l/p • SpO2 99%',
      symptoms: 'Đau thượng vị âm ỉ 2 tuần.',
      clinicalNotes: 'Ấn nhẹ thượng vị đau tức nhẹ.',
      diagnoses: [{ code: 'K29.7', name: 'Viêm dạ dày, không đặc hiệu' }],
      conclusion: 'Viêm dạ dày cấp nhẹ, điều trị PPI 14 ngày.',
      followUpInstruction: 'Kiêng cay chua, tái khám sau 2 tuần.',
      clsOrders: [
        {
          id: 'dcls-1',
          serviceCode: 'XN-CTM',
          serviceName: 'Tổng phân tích tế bào máu ngoại vi',
          status: 'Published',
          summaryText: 'Bạch cầu 7.2 G/L, Hemoglobin 132 g/L — trong giới hạn bình thường.',
          technicalConclusion: 'Không thiếu máu.',
          readingDoctorName: 'BS.CKII Đỗ Văn Hùng',
          attachments: [
            { fileId: 'df-1', fileName: 'Phieu-XN-Huong.pdf', mimeType: 'application/pdf' },
          ],
        },
      ],
      prescriptionItems: [
        {
          drugId: 'drug-3',
          drugName: 'Omeprazol 20mg',
          activeIngredient: 'Omeprazole',
          dosage: '1 viên/lần',
          frequency: 'Trước ăn sáng 30 phút',
          durationDays: 14,
          quantity: 14,
          unit: 'Viên',
          usageInstruction: 'Uống nguyên viên.',
        },
      ],
      amendmentHistory: [],
    },
  };

  const roomQueue: ClsRoomQueueItem[] = [
    {
      orderItemId: 'cls-ord-1',
      ticketNumber: 'CLS-021',
      patientName: 'Nguyễn Minh Tâm',
      serviceCode: 'XN-CTM',
      serviceName: 'Tổng phân tích tế bào máu ngoại vi',
      serviceDate: today,
      queueStatus: 'Waiting',
      resultStatus: 'NotStarted',
      readingDoctorName: 'BS.CKII Đỗ Văn Hùng',
      version: 1,
    },
    {
      orderItemId: 'cls-ord-2',
      ticketNumber: 'CLS-019',
      patientName: 'Trần Hữu Phước',
      serviceCode: 'XN-CTM',
      serviceName: 'Tổng phân tích tế bào máu ngoại vi',
      serviceDate: '2026-11-10',
      queueStatus: 'Completed',
      resultStatus: 'Draft',
      readingDoctorName: 'BS.CKII Đỗ Văn Hùng',
      version: 2,
    },
  ];

  const roomDrafts: Record<string, ClsRoomResultDraftDto> = {
    'cls-ord-1': {
      orderItemId: 'cls-ord-1',
      ticketNumber: 'CLS-021',
      patientName: 'Nguyễn Minh Tâm',
      serviceCode: 'XN-CTM',
      serviceName: 'Tổng phân tích tế bào máu ngoại vi',
      resultStatus: 'Draft',
      summaryText: '',
      technicalConclusion: '',
      readingDoctorName: 'BS.CKII Đỗ Văn Hùng',
      availableShiftDoctors: ['BS.CKII Đỗ Văn Hùng', 'ThS.BS Lê Minh Ngọc'],
      version: 1,
      draftFiles: [],
    },
    'cls-ord-2': {
      orderItemId: 'cls-ord-2',
      ticketNumber: 'CLS-019',
      patientName: 'Trần Hữu Phước',
      serviceCode: 'XN-CTM',
      serviceName: 'Tổng phân tích tế bào máu ngoại vi',
      resultStatus: 'Draft',
      summaryText: 'Bạch cầu tăng nhẹ 11.4 G/L.',
      technicalConclusion: 'Theo dõi phản ứng viêm nhẹ.',
      readingDoctorName: 'BS.CKII Đỗ Văn Hùng',
      availableShiftDoctors: ['BS.CKII Đỗ Văn Hùng', 'ThS.BS Lê Minh Ngọc'],
      version: 2,
      draftFiles: [
        { fileId: 'rf-1', fileName: 'may-xn-cls019.pdf', mimeType: 'application/pdf', sizeBytes: 180400 },
      ],
    },
  };

  return {
    today,
    notifications,
    stations,
    doctorDays,
    medicalServices,
    priceLists,
    devices,
    permissionMatrix,
    exceptions,
    refundApprovals,
    reconcileRows,
    auditLogs,
    clsWaivers,
    chatbotConfigs,
    receptionRecords,
    cashierItems,
    cashRefunds,
    shiftSummary,
    callTasks,
    nurseQueue,
    vitalsForms,
    doctorInitialQueue,
    doctorClsReturnQueue,
    doctorWorkspaces,
    roomQueue,
    roomDrafts,
  };
}
