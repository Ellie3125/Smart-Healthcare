import { apiGet, apiPost, apiPut } from '../shared/lib/http';
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
export const adminOpsApi = {
  // S03 Notifications
  // TODO: nối contract thật thông báo nhân sự
  listNotifications: () =>
    apiGet<{ items: StaffNotificationItem[]; unreadCount: number }>('/api/notifications/staff/notifications'),
  markNotificationRead: (id: string) =>
    apiPost<void, Record<string, never>>(`/api/notifications/staff/notifications/${id}/read`, {}),

  // AD03 Service Stations
  // TODO: nối contract thật ServiceStation
  listStations: () => apiGet<ServiceStationItem[]>('/api/identity/admin/stations'),
  saveStation: (body: Omit<ServiceStationItem, 'id'> & { id?: string }) =>
    apiPost<ServiceStationItem, typeof body>('/api/identity/admin/stations', body),

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
  swapOrCancelDoctorSession: (
    doctorId: string,
    serviceDate: string,
    body: { action: 'Swap' | 'Cancel'; substituteDoctorId?: string; reason: string },
    idempotencyKey: string,
  ) =>
    apiPost<{ affectedCount: number }, typeof body>(
      `/api/scheduling/admin/doctor-days/${doctorId}/${serviceDate}/session-action`,
      body,
      { idempotencyKey },
    ),

  // AD08 Medical Services CLS
  // TODO: nối contract thật danh mục dịch vụ CLS
  listMedicalServices: () => apiGet<MedicalServiceCatalogItem[]>('/api/clinical/admin/cls-services'),
  saveMedicalService: (body: Partial<MedicalServiceCatalogItem>) =>
    apiPost<MedicalServiceCatalogItem, Partial<MedicalServiceCatalogItem>>(
      '/api/clinical/admin/cls-services',
      body,
    ),

  // AD09 Price Lists
  // TODO: nối contract thật bảng giá
  listPriceLists: () => apiGet<PriceListItem[]>('/api/billing/admin/price-lists'),
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
  listExceptions: () => apiGet<OperationalExceptionItem[]>('/api/workflow/admin/exceptions'),
  resolveException: (id: string, resolutionNote: string, idempotencyKey: string) =>
    apiPost<OperationalExceptionItem, { resolutionNote: string }>(
      `/api/workflow/admin/exceptions/${id}/resolve`,
      { resolutionNote },
      { idempotencyKey },
    ),

  // AD12 Refund Approvals
  // TODO: nối contract thật duyệt hoàn tiền
  listRefundApprovals: () => apiGet<AdminRefundItem[]>('/api/billing/admin/refunds'),
  decideRefund: (
    id: string,
    body: { decision: 'Approve' | 'Reject'; reason?: string; version: number },
    idempotencyKey: string,
  ) =>
    apiPost<AdminRefundItem, typeof body>(
      `/api/billing/admin/refunds/${id}/decision`,
      body,
      { idempotencyKey, version: body.version },
    ),

  // AD13 VNPAY Reconcile (chỉ querydr, không đánh dấu đã trả)
  // TODO: nối contract thật đối soát VNPAY
  listPaymentReconcile: () => apiGet<PaymentReconcileRow[]>('/api/billing/admin/payments/reconcile'),
  queryVnpayDr: (txnRef: string, idempotencyKey: string) =>
    apiPost<PaymentReconcileRow, { vnpTxnRef: string }>(
      '/api/billing/admin/payments/querydr',
      { vnpTxnRef: txnRef },
      { idempotencyKey },
    ),

  // AD14 Reports
  // TODO: nối contract thật báo cáo snapshot
  getReportSnapshot: (dateFrom: string, dateTo: string) =>
    apiGet<ReportSnapshotDto>('/api/analytics/admin/reports/summary', { params: { dateFrom, dateTo } }),

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
