import { apiGet, apiPost, apiPut } from '../shared/lib/http';
import { receptionApi } from './receptionApi';
import type {
  AdminRefundItem,
  AuditLogEntry,
  ChatbotSpecialtyConfigItem,
  ClsWaiverRequestItem,
  DoctorDayQuotaItem,
  HospitalDeviceItem,
  MedicalServiceCatalogItem,
  OperationalExceptionItem,
  PaymentReconcileRow,
  PriceListItem,
  ReportSnapshotDto,
  RolePermissionMatrixRow,
  ServiceStationItem,
  StaffNotificationItem,
} from '../types/staffWorkflow';

/**
 * Admin Operational, Quota, Finance, Audit & Config API.
 * Nối real endpoint Scheduling W1 (`DoctorDayEndpoints`) + `// TODO: nối contract thật` cho phần mở rộng.
 */
interface ApiMedicalService {
  id: string;
  serviceCode: string;
  serviceName: string;
  serviceCategory: string;
  defaultRoomId: string;
  isActive: boolean;
  rooms: { roomId: string; isActive: boolean }[];
}

interface ApiRefund {
  refundRequestId: string;
  status: string;
  reason: string;
  amount: number;
  chargeName: string;
  originalTransactionReference: string;
  payoutChannel: string;
  requestedAtUtc: string;
}

const PAYMENT_PURPOSE = ['ExamFee', 'ClsBatch'];

// Danh sách phòng (Identity admin) để chọn phòng cho quầy và dịch vụ CLS.
const listRooms = async () =>
  (await apiGet<{ id: string; roomNumber: string; roomName: string; isActive: boolean }[]>('/api/staff/admin/rooms')).map((r) => ({
    id: r.id,
    name: `${r.roomNumber} — ${r.roomName}`,
    isActive: r.isActive,
  }));

export const adminOpsApi = {
  listRooms,

  // S03 Notifications
  // TODO: nối contract thật thông báo nhân sự
  listNotifications: () =>
    apiGet<{ items: StaffNotificationItem[]; unreadCount: number }>('/api/notifications/staff/notifications'),
  markNotificationRead: (id: string) =>
    apiPost<void, Record<string, never>>(`/api/notifications/staff/notifications/${id}/read`, {}),

  // AD03 Service Stations
  // Quầy/trạm thuộc Scheduling, gắn với một phòng; không có loại quầy hay tầng.
  listStations: async (): Promise<ServiceStationItem[]> => {
    const [page, rooms] = await Promise.all([
      apiGet<{ items: { id: string; roomId: string; stationCode: string; stationName: string; isActive: boolean }[] }>(
        '/api/scheduling/admin/service-stations',
        { params: { pageSize: 100 } },
      ),
      listRooms(),
    ]);
    const roomName = new Map(rooms.map((r) => [r.id, r.name]));
    return page.items.map((s) => ({
      id: s.id,
      code: s.stationCode,
      name: s.stationName,
      roomId: s.roomId,
      roomName: roomName.get(s.roomId) ?? '',
      isActive: s.isActive,
    }));
  },
  saveStation: (body: { roomId: string; code: string; name: string; isActive: boolean }) =>
    apiPost<unknown, unknown>('/api/scheduling/admin/service-stations', {
      roomId: body.roomId,
      stationCode: body.code,
      stationName: body.name,
      isActive: body.isActive,
    }),

  // AD06 + AD07 Doctor Day Quotas (Real W1 endpoints + TODO list/swap/cancel)
  openDoctorDay: (doctorId: string, serviceDate: string) =>
    apiPost<DoctorDayQuotaItem, Record<string, never>>(
      `/api/scheduling/admin/doctor-days/${doctorId}/${serviceDate}`,
      {},
    ),
  closeDoctorDay: (doctorId: string, serviceDate: string) =>
    apiPost<void, Record<string, never>>(
      `/api/scheduling/admin/doctor-days/${doctorId}/${serviceDate}/close`,
      {},
    ),

  // TODO: nối contract thật AD06 danh sách quota + AD07 chuyển/hủy phiên trước 06:30
  listDoctorDayQuotas: (serviceDate: string) =>
    apiGet<DoctorDayQuotaItem[]>('/api/scheduling/admin/doctor-days', { params: { serviceDate } }),
  // AD07: đổi bác sĩ hoặc hủy phiên; response không có số lịch bị ảnh hưởng.
  swapOrCancelDoctorSession: async (
    doctorId: string,
    serviceDate: string,
    body: { action: 'Swap' | 'Cancel'; substituteDoctorId?: string; reason: string },
  ) => {
    const base = `/api/scheduling/admin/doctor-days/${doctorId}/${serviceDate}`;
    if (body.action === 'Swap')
      await apiPost(`${base}/replace-doctor`, { replacementDoctorId: body.substituteDoctorId, reason: body.reason });
    else await apiPost(`${base}/cancel-session`, { reason: body.reason });
  },

  // AD08 Medical Services CLS
  // AD08: danh mục dịch vụ CLS thuộc Scheduling; giá CLS nằm ở Billing, không sửa ở đây.
  listMedicalServices: async (): Promise<MedicalServiceCatalogItem[]> => {
    const [page, rooms] = await Promise.all([
      apiGet<{ items: ApiMedicalService[] }>('/api/scheduling/admin/medical-services', { params: { pageSize: 100 } }),
      listRooms(),
    ]);
    const roomName = new Map(rooms.map((r) => [r.id, r.name]));
    return page.items.map((s) => ({
      id: s.id,
      code: s.serviceCode,
      name: s.serviceName,
      category: s.serviceCategory,
      roomId: s.defaultRoomId,
      roomName: roomName.get(s.defaultRoomId) ?? '',
      hasActiveRoom: s.rooms.some((r) => r.isActive),
      unitPrice: 0,
      isActive: s.isActive,
    }));
  },
  saveMedicalService: (body: { code: string; name: string; category: string; roomId: string }) =>
    apiPost<unknown, unknown>('/api/scheduling/admin/medical-services', {
      serviceCode: body.code,
      serviceName: body.name,
      serviceCategory: body.category,
      defaultRoomId: body.roomId,
      isMandatoryDefault: false,
      isActive: true,
    }),

  // AD09 Price Lists
  // TODO: nối contract thật bảng giá
  listPriceLists: async () =>
    (await apiGet<{ items: PriceListItem[] }>('/api/billing/admin/price-lists', { params: { pageSize: 100 } })).items,
  createPriceList: (body: Omit<PriceListItem, 'id'>) =>
    apiPost<PriceListItem, Omit<PriceListItem, 'id'>>('/api/billing/admin/price-lists', body),

  // AD10 Permissions Matrix & Devices
  // TODO: nối contract thật phân quyền & thiết bị ngày
  getPermissionMatrix: () => apiGet<RolePermissionMatrixRow[]>('/api/identity/admin/permissions-matrix'),
  listDevices: (date: string) => apiGet<HospitalDeviceItem[]>('/api/identity/admin/devices', { params: { date } }),
  assignDevice: (deviceId: string, accountUsername: string) =>
    apiPut<HospitalDeviceItem, { accountUsername: string }>(
      `/api/identity/admin/devices/${deviceId}/assignment`,
      { accountUsername },
    ),

  // AD11 Exceptions
  // TODO: nối contract thật trung tâm ngoại lệ
  // AD11: hiện chỉ xử lý được lỗi Outbox (thử gửi lại); nguồn khác chỉ xem.
  listExceptions: async (): Promise<OperationalExceptionItem[]> =>
    (await apiGet<{ items: {
      id: string;
      source: string;
      kind: string;
      status: string;
      targetEntityId: string;
      createdAtUtc: string;
      retryCount: number | null;
      canRetryOutbox: boolean;
    }[] }>('/api/workflow/admin/exceptions')).items.map((e) => ({
      id: e.id,
      type: e.kind as OperationalExceptionItem['type'],
      referenceCode: e.targetEntityId.slice(0, 8).toUpperCase(),
      patientName: '',
      phoneMasked: '',
      detail: `${e.source} • ${e.kind}${e.retryCount ? ` • đã thử ${e.retryCount} lần` : ''}`,
      status: e.status === 'Resolved' ? 'Resolved' : 'Open',
      createdAt: e.createdAtUtc,
      canRetry: e.canRetryOutbox,
    })),
  resolveException: (id: string, resolutionNote: string, idempotencyKey: string) =>
    apiPost<{ status: string }, { resolutionNote: string }>(
      `/api/workflow/admin/exceptions/outbox/${id}/retry`,
      { resolutionNote },
      { idempotencyKey },
    ),

  // AD12 Refund Approvals
  // TODO: nối contract thật duyệt hoàn tiền
  // AD12: RefundView; backend không trả tên bệnh nhân, cột hiện tên khoản được hoàn.
  listRefundApprovals: async (): Promise<AdminRefundItem[]> =>
    (await apiGet<ApiRefund[]>('/api/billing/admin/refunds')).map((r) => ({
      id: r.refundRequestId,
      referenceCode: r.originalTransactionReference,
      patientName: r.chargeName,
      reason: r.reason,
      amount: r.amount,
      payoutChannel: r.payoutChannel,
      status: (r.status === 'Settled' ? 'Refunded' : r.status) as AdminRefundItem['status'],
      createdAt: r.requestedAtUtc,
      version: 0,
    })),
  decideRefund: (id: string, body: { decision: 'Approve' | 'Reject'; reason?: string }, idempotencyKey: string) =>
    apiPost<unknown, { decision: string; note?: string }>(
      `/api/billing/admin/refunds/${id}/decision`,
      { decision: body.decision, note: body.reason },
      { idempotencyKey },
    ),

  // AD13 VNPAY Reconcile (chỉ querydr, không đánh dấu đã trả)
  // TODO: nối contract thật đối soát VNPAY
  // AD13: yêu cầu thanh toán cần xem lại (Unknown); querydr dùng chung route ReceptionistOrAdmin.
  listPaymentReconcile: async (): Promise<PaymentReconcileRow[]> =>
    (await apiGet<{
      transactionReference: string;
      paymentIntentId: string;
      intentNumber: string;
      purpose: number | string;
      amount: number;
      createdAtUtc: string;
    }[]>('/api/billing/admin/payments/reconcile')).map((p) => ({
      id: p.paymentIntentId,
      vnpTxnRef: p.transactionReference,
      appointmentOrBatchCode: `${p.intentNumber} (${typeof p.purpose === 'number' ? PAYMENT_PURPOSE[p.purpose] ?? p.purpose : p.purpose})`,
      amount: p.amount,
      localStatus: 'Unknown',
      gatewayStatus: 'Unknown',
      checkedAt: p.createdAtUtc,
    })),
  queryVnpayDr: (txnRef: string) => receptionApi.lookupPaymentTxn(txnRef),

  // AD14 Reports
  // TODO: nối contract thật báo cáo snapshot
  getReportSnapshot: (dateFrom: string, dateTo: string) =>
    apiGet<ReportSnapshotDto>('/api/documents/admin/reports/summary', { params: { dateFrom, dateTo } }),

  // AD15 Audit Logs
  // TODO: nối contract thật AuditLog
  listAuditLogs: (params?: { keyword?: string; page?: number }) =>
    apiGet<{ items: AuditLogEntry[]; hasMore: boolean }>('/api/identity/admin/audit-logs', { params }),

  // AD16 CLS Waivers
  // TODO: nối contract thật duyệt miễn giảm CLS
  listClsWaivers: () => apiGet<ClsWaiverRequestItem[]>('/api/billing/admin/cls-waivers'),
  decideClsWaiver: (id: string, approve: boolean, idempotencyKey: string) =>
    apiPost<ClsWaiverRequestItem, { approve: boolean }>(
      `/api/billing/admin/cls-waivers/${id}/decision`,
      { approve },
      { idempotencyKey },
    ),

  // AD17 Chatbot Specialty Config
  // TODO: nối contract thật cấu hình chatbot
  listChatbotConfigs: () => apiGet<ChatbotSpecialtyConfigItem[]>('/api/notifications/admin/chatbot-config'),
  saveChatbotConfig: (item: ChatbotSpecialtyConfigItem) =>
    apiPut<ChatbotSpecialtyConfigItem, ChatbotSpecialtyConfigItem>(
      `/api/notifications/admin/chatbot-config/${item.id}`,
      item,
    ),
};
