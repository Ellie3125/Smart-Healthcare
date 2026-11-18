import { vi } from 'vitest';
import { apiClient } from '../api/client';
import { getVNDateISO } from '../shared/lib/datetime';
import type {
  AuditLogEntry,
  CashierPayableItem,
  ClsRoomResultDraftDto,
  DoctorDayQuotaItem,
  DoctorEncounterWorkspaceDto,
  DoctorQueueEntry,
  DrugCatalogItem,
  Icd10CodeOption,
  NurseQueueItem,
  PaymentReconcileRow,
  ReceptionSearchResult,
  ServiceStationItem,
  ShiftReconcileSummary,
  StaffNotificationItem,
  VitalsRecordDto,
} from '../types/staffWorkflow';
import type { ResultAttachmentDto } from '../api/documentsApi';

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

export function createStaffRealStore() {
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
      vnpTxnRef: 'VNP-20261111-9902',
      appointmentOrBatchCode: 'CLS-BATCH-09',
      amount: 420000,
      localStatus: 'PendingPayment',
      gatewayStatus: 'Unknown',
    },
  ];

  const auditLogs: AuditLogEntry[] = [
    {
      id: 'log-1',
      occurredAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
      actorName: 'Nguyễn Thu Trang (Thu ngân)',
      actorRole: 'Cashier',
      action: 'CollectCashPayment',
      entityType: 'ClsBatch',
      entityId: 'CLS-BATCH-09',
      patientCccd: '001092000789',
      patientPhone: '0901234321',
      beforeJson: { status: 'PendingPayment' },
      afterJson: { status: 'Paid', amount: 420000 },
    },
    {
      id: 'log-2',
      occurredAt: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
      actorName: 'Lê Văn An (Tiếp đón)',
      actorRole: 'Receptionist',
      action: 'CheckInAppointment',
      entityType: 'Appointment',
      entityId: 'SH-261111-001',
      patientCccd: '001092000789',
      patientPhone: '0901234321',
      beforeJson: { status: 'Confirmed' },
      afterJson: { status: 'CheckedIn', queueNumber: 12 },
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
      checkInDeadline: '07:30',
      isPastCheckInDeadline: true,
      queueNumber: null,
    },
  ];

  const cashierItems: CashierPayableItem[] = [
    {
      id: 'c-1',
      referenceCode: 'CLS-BATCH-09',
      patientName: 'Nguyễn Minh Tâm',
      itemType: 'ClsBatch',
      description: 'Chỉ định 2 xét nghiệm bổ sung: Huyết học & Sinh hóa',
      amount: 420000,
      status: 'PendingPayment',
    },
  ];

  const shiftSummary: ShiftReconcileSummary = {
    shiftDate: today,
    cashierName: 'Nguyễn Thu Trang',
    expectedCashCollected: 1500000,
    expectedCashRefunded: 0,
    netExpectedCash: 1500000,
    isClosed: false,
  };

  const nurseQueue: NurseQueueItem[] = [
    {
      encounterId: 'enc-nurse-1',
      ticketNumber: 'A-012',
      patientName: 'Nguyễn Minh Tâm',
      yearOfBirth: 1992,
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'InProgress',
      version: 1,
    },
    {
      encounterId: 'enc-nurse-2',
      ticketNumber: 'A-014',
      patientName: 'Vũ Thị Lan',
      yearOfBirth: 1985,
      doctorName: 'BS.CKI Trần Quốc Khánh',
      specialtyName: 'Nội tổng quát',
      status: 'Called',
      version: 1,
    },
  ];

  const vitalsForms: Record<string, VitalsRecordDto> = {
    'enc-nurse-2': {
      encounterId: 'enc-nurse-2',
      ticketNumber: 'A-014',
      patientName: 'Vũ Thị Lan',
      version: 1,
      systolicBp: '120',
      diastolicBp: '80',
      heartRate: '75',
      temperatureC: '36.5',
      spo2: '',
      respiratoryRate: '',
      weightKg: '55',
      heightCm: '160',
      nurseNote: '',
    },
  };

  const doctorInitialQueue: DoctorQueueEntry[] = [
    {
      encounterId: 'enc-doc-1',
      ticketNumber: 'A-012',
      patientName: 'Nguyễn Minh Tâm',
      queueKind: 'Initial',
      status: 'Waiting',
      checkedInAt: '08:10',
    },
  ];

  const doctorClsReturnQueue: DoctorQueueEntry[] = [
    {
      encounterId: 'enc-doc-ret-1',
      ticketNumber: 'A-008',
      patientName: 'Huỳnh Thị Hương',
      queueKind: 'ClsReturn',
      status: 'AwaitingCls',
      checkedInAt: '07:45',
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
  };

  const roomDrafts: Record<string, ClsRoomResultDraftDto> = {
    'cls-ord-1': {
      orderItemId: 'cls-ord-1',
      ticketNumber: 'CLS-021',
      patientName: 'Nguyễn Minh Tâm',
      serviceCode: 'XN-CTM',
      serviceName: 'Tổng phân tích tế bào máu',
      resultStatus: 'Draft',
      summaryText: '',
      technicalConclusion: '',
      readingDoctorName: 'BS.CKII Đỗ Văn Hùng',
      availableShiftDoctors: ['BS.CKII Đỗ Văn Hùng', 'ThS.BS Lê Minh Ngọc'],
      version: 1,
      draftFiles: [],
    },
  };

  return {
    today,
    notifications,
    stations,
    doctorDays,
    reconcileRows,
    auditLogs,
    receptionRecords,
    cashierItems,
    shiftSummary,
    nurseQueue,
    vitalsForms,
    doctorInitialQueue,
    doctorClsReturnQueue,
    doctorWorkspaces,
    roomDrafts,
  };
}

class StaffApiMock {
  private store = createStaffRealStore();
  private overrides: OverrideEntry[] = [];

  constructor() {
    this.install();
  }

  reset() {
    this.store = createStaffRealStore();
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
    _body: any,
    query: URLSearchParams,
  ): RouteHandlerResult {
    // Auth
    if (pathname === '/me') {
      return { data: { userId: 'u-admin-1', userName: 'admin', role: 'Admin' } };
    }

    // Doctor day quotas
    if (pathname === '/api/scheduling/admin/doctor-days') {
      const date = query.get('serviceDate') || this.store.today;
      return { data: this.store.doctorDays.map((d) => ({ ...d, serviceDate: date })) };
    }

    // Payment reconcile
    if (pathname === '/api/billing/admin/payments/reconcile') {
      return { data: this.store.reconcileRows };
    }

    // Audit logs
    if (pathname === '/api/identity/admin/audit-logs') {
      return { data: { items: this.store.auditLogs, hasMore: false } };
    }

    // Reception search (searchUnified)
    // Dạng Backend §4.6a dựng từ dữ liệu mẫu dạng trang.
    if (pathname === '/api/scheduling/staff/reception/search') {
      return {
        data: this.store.receptionRecords.map((r) => ({
          patientId: `pat-${r.id}`,
          fullName: r.patientName,
          dateOfBirth: r.dateOfBirth,
          gender: 'Male',
          maskedPhoneNumber: r.phone,
          maskedNationalId: r.cccdMasked,
          isVerified: r.verificationStatus === 'Verified',
        })),
      };
    }

    if (pathname === '/api/scheduling/staff/doctors') {
      const names = Array.from(new Set(this.store.receptionRecords.map((r) => r.doctorName ?? '')));
      return {
        data: {
          serviceDate: query.get('serviceDate'),
          page: 1,
          pageSize: 50,
          hasMore: false,
          items: names.map((n, i) => ({
            doctorId: `doc-${i}`,
            fullName: n,
            doctorRankId: 'rank-1',
            specialtyId: 'spec-1',
            availableSlots: 10,
            totalSlots: 30,
            examFee: 150000,
            currency: 'VND',
            doctorRankName: 'Bác sĩ CKI',
            specialtyName: 'Nội tổng quát',
          })),
        },
      };
    }

    // Cashier items
    if (pathname === '/api/billing/staff/cashier/items') {
      return {
        data: this.store.cashierItems.map((c) => ({
          serviceOrderId: c.id,
          patientId: 'pat-recp-1',
          encounterId: 'enc-001',
          description: `${c.referenceCode}: ${c.description}`,
          totalAmount: c.amount,
          paidAmount: c.status === 'Paid' ? c.amount : 0,
          outstanding: c.status === 'Paid' ? 0 : c.amount,
          status: c.status === 'Paid' ? 1 : 0,
          orderedAtUtc: new Date().toISOString(),
          payByUtc: new Date().toISOString(),
        })),
      };
    }

    // Shift summary
    if (pathname === '/api/billing/staff/cashier/shift-summary') {
      return { data: this.store.shiftSummary };
    }

    // Reception cancel at counter
    const cancelAtCounterMatch = matchPath('/api/scheduling/staff/appointments/:id/cancel', pathname);
    if (cancelAtCounterMatch && method === 'POST') {
      return { data: { refundAmount: 150000 } };
    }

    // Check-in appointment (hỗ trợ cả /checkin và /check-in)
    const checkinMatch =
      matchPath('/api/scheduling/staff/appointments/:id/checkin', pathname) ||
      matchPath('/api/scheduling/staff/appointments/:id/check-in', pathname);
    if (checkinMatch && method === 'POST') {
      return {
        data: {
          appointmentId: checkinMatch.id,
          patientId: 'pat-001',
          doctorId: 'doc-001',
          serviceDate: '2026-11-17',
          checkedInAtUtc: new Date().toISOString(),
          alreadyCheckedIn: false,
          queueTicketId: 'ticket-001',
          ticketNumber: 'A-015',
          sequenceNumber: 15,
          encounterId: 'enc-001',
          queueNumber: 15,
          roomName: 'Phòng khám P201',
        },
      };
    }

    // Today appointments list
    if (pathname === '/api/scheduling/staff/appointments/today' && method === 'GET') {
      const names = Array.from(new Set(this.store.receptionRecords.map((r) => r.doctorName ?? '')));
      const code = query.get('appointmentNumber');
      const items = this.store.receptionRecords
        .filter((r) => r.appointmentId && (!code || r.appointmentCode === code))
        .map((r) => ({
          appointmentId: r.appointmentId!,
          appointmentNumber: r.appointmentCode!,
          patientId: `pat-${r.id}`,
          doctorId: `doc-${names.indexOf(r.doctorName ?? '')}`,
          assignedRoomId: 'room-001',
          serviceDate: r.serviceDate!,
          status: r.appointmentStatus === 'Confirmed' ? 'Scheduled' : r.appointmentStatus!,
          checkInDeadlineUtc: new Date(Date.now() + (r.isPastCheckInDeadline ? -10 : 30) * 60 * 1000).toISOString(),
        }));
      return { data: { page: 1, pageSize: 50, hasMore: false, items } };
    }

    // Nurse queue & vitals
    if (pathname === '/api/clinical/staff/nurse/queue') {
      return { data: this.store.nurseQueue };
    }

    const nurseVitalsMatch = matchPath('/api/clinical/staff/nurse/encounters/:encounterId/vitals', pathname);
    if (nurseVitalsMatch && method === 'GET') {
      const v = this.store.vitalsForms[nurseVitalsMatch.encounterId] ?? {
        encounterId: nurseVitalsMatch.encounterId,
        ticketNumber: 'A-014',
        patientName: 'Vũ Thị Lan',
        version: 1,
        systolicBp: '120',
        diastolicBp: '80',
        heartRate: '75',
        temperatureC: '36.5',
        spo2: '',
        respiratoryRate: '',
        weightKg: '55',
        heightCm: '160',
        nurseNote: '',
      };
      return { data: v };
    }

    // Doctor queues & workspace
    if (pathname === '/api/clinical/staff/doctor/queues') {
      return {
        data: {
          initialQueue: this.store.doctorInitialQueue,
          clsReturnQueue: this.store.doctorClsReturnQueue,
          activeEncounterId: 'enc-doc-1',
        },
      };
    }

    const docWsMatch = matchPath('/api/clinical/staff/doctor/encounters/:encounterId', pathname);
    if (docWsMatch && method === 'GET') {
      return { data: this.store.doctorWorkspaces['enc-doc-1'] };
    }

    // Room results
    const roomResMatch = matchPath('/api/clinical/staff/room/results/:orderItemId', pathname);
    if (roomResMatch && method === 'GET') {
      const draft = this.store.roomDrafts[roomResMatch.orderItemId] ?? this.store.roomDrafts['cls-ord-1'];
      return { data: draft };
    }

    // Room ticket files
    const ticketFilesMatch = matchPath('/api/documents/staff/room/tickets/:queueTicketId/files', pathname);
    if (ticketFilesMatch && method === 'GET') {
      const draft = this.store.roomDrafts[ticketFilesMatch.queueTicketId] ?? this.store.roomDrafts['cls-ord-1'];
      const files: ResultAttachmentDto[] = (draft?.draftFiles ?? []).map((f) => ({
        id: f.fileId,
        clsResultId: ticketFilesMatch.queueTicketId,
        queueTicketId: ticketFilesMatch.queueTicketId,
        encounterId: 'enc-1',
        patientId: 'p-1',
        fileName: f.fileName,
        contentType: f.mimeType,
        sizeBytes: f.sizeBytes,
        sha256Hash: `hash-${f.fileId}-sha256`,
        attachmentVersion: 1,
        uploadStatus: 'Uploaded',
        isPublished: draft?.resultStatus === 'Published',
        uploadedAtUtc: new Date().toISOString(),
      }));
      return { data: files };
    }

    return { status: 404, data: { code: 'NOT_FOUND', detail: `Endpoint not found: ${pathname}` } };
  }
}

export const staffApiMock = new StaffApiMock();
