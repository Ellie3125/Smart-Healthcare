import { http, HttpResponse } from 'msw';
import { createInitialStaffStore, MOCK_DRUGS, MOCK_ICD10 } from './staffData';
import type { DoctorEncounterWorkspaceDto, VitalsRecordDto } from '../types/staffWorkflow';

let store = createInitialStaffStore();

export function resetStaffMockStore() {
  store = createInitialStaffStore();
}

export const staffHandlers = [
  // S03 Notifications
  http.get('/api/notifications/staff/notifications', () =>
    HttpResponse.json({
      items: store.notifications,
      unreadCount: store.notifications.filter((n) => n.status === 'Sent').length,
    }),
  ),

  http.post('/api/notifications/staff/notifications/:id/read', ({ params }) => {
    const found = store.notifications.find((n) => n.id === params.id);
    if (found) found.status = 'Read';
    return HttpResponse.json({ ok: true });
  }),

  // AD03 ServiceStations
  http.get('/api/identity/admin/stations', () => HttpResponse.json(store.stations)),
  http.post('/api/identity/admin/stations', async ({ request }) => {
    const body = (await request.json()) as {
      id?: string;
      code: string;
      name: string;
      stationType: 'Reception' | 'Cashier' | 'Vitals' | 'QueueDisplay';
      floor: string;
      isActive: boolean;
    };
    const item = { ...body, id: body.id || `st-${Date.now()}` };
    const idx = store.stations.findIndex((s) => s.id === item.id);
    if (idx >= 0) store.stations[idx] = item;
    else store.stations.push(item);
    return HttpResponse.json(item, { status: 201 });
  }),

  // AD06 + AD07 Doctor Days
  http.get('/api/scheduling/admin/doctor-days', ({ request }) => {
    const date = new URL(request.url).searchParams.get('serviceDate') || store.today;
    return HttpResponse.json(store.doctorDays.map((d) => ({ ...d, serviceDate: date })));
  }),

  http.post('/api/scheduling/admin/doctor-days/:doctorId/:serviceDate', ({ params }) => {
    const found = store.doctorDays.find((d) => d.doctorId === params.doctorId);
    if (found) {
      found.status = 'Open';
      return HttpResponse.json(found, { status: 201 });
    }
    return HttpResponse.json({ code: 'NOT_FOUND', detail: 'Không tìm thấy bác sĩ' }, { status: 404 });
  }),

  http.post('/api/scheduling/admin/doctor-days/:doctorId/:serviceDate/close', ({ params }) => {
    const found = store.doctorDays.find((d) => d.doctorId === params.doctorId);
    if (found) found.status = 'Closed';
    return new HttpResponse(null, { status: 204 });
  }),

  http.post('/api/scheduling/admin/doctor-days/:doctorId/:serviceDate/session-action', async ({ params, request }) => {
    const body = (await request.json()) as { action: 'Swap' | 'Cancel'; substituteDoctorId?: string; reason: string };
    const found = store.doctorDays.find((d) => d.doctorId === params.doctorId);
    if (found && body.action === 'Cancel') {
      found.status = 'Closed';
      store.callTasks.unshift({
        id: `call-${Date.now()}`,
        patientName: 'Khách đặt phiên vừa hủy',
        phone: '0909000111',
        reason: `Hủy phiên bác sĩ (${body.reason})`,
        doctorName: found.doctorName,
        serviceDate: String(params.serviceDate),
        status: 'Pending',
      });
    }
    return HttpResponse.json({ affectedCount: found ? found.onlineBookedCount : 0 });
  }),

  // AD08 CLS Services
  http.get('/api/clinical/admin/cls-services', () => HttpResponse.json(store.medicalServices)),
  http.post('/api/clinical/admin/cls-services', async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;
    const created = {
      id: (body.id as string) || `svc-${Date.now()}`,
      code: String(body.code || 'NEW'),
      name: String(body.name || 'Dịch vụ mới'),
      category: (body.category as 'Lab' | 'Imaging' | 'Functional') || 'Lab',
      roomId: String(body.roomId || 'room-xn01'),
      roomName: String(body.roomName || 'Phòng Xét nghiệm 01'),
      hasActiveRoom: body.hasActiveRoom !== false,
      unitPrice: Number(body.unitPrice || 100000),
      isActive: body.isActive !== false,
    };
    store.medicalServices.push(created);
    return HttpResponse.json(created, { status: 201 });
  }),

  // AD09 Price Lists
  http.get('/api/billing/admin/price-lists', () => HttpResponse.json(store.priceLists)),
  http.post('/api/billing/admin/price-lists', async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;
    const next = { ...(body as unknown as (typeof store.priceLists)[number]), id: `pl-${Date.now()}` };
    store.priceLists.unshift(next);
    return HttpResponse.json(next, { status: 201 });
  }),

  // AD10 Permissions & Devices
  http.get('/api/identity/admin/permissions-matrix', () => HttpResponse.json(store.permissionMatrix)),
  http.get('/api/identity/admin/devices', () => HttpResponse.json(store.devices)),
  http.put('/api/identity/admin/devices/:deviceId/assignment', async ({ params, request }) => {
    const body = (await request.json()) as { accountUsername: string };
    const dev = store.devices.find((d) => d.id === params.deviceId);
    if (!dev) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    dev.accountUsername = body.accountUsername;
    return HttpResponse.json(dev);
  }),

  // AD11 Exceptions
  http.get('/api/workflow/admin/exceptions', () => HttpResponse.json(store.exceptions)),
  http.post('/api/workflow/admin/exceptions/:id/resolve', ({ params }) => {
    const exc = store.exceptions.find((e) => e.id === params.id);
    if (exc) exc.status = 'Resolved';
    return HttpResponse.json(exc ?? { id: params.id, status: 'Resolved' });
  }),

  // AD12 Refunds
  http.get('/api/billing/admin/refunds', () => HttpResponse.json(store.refundApprovals)),
  http.post('/api/billing/admin/refunds/:id/decision', async ({ params, request }) => {
    const body = (await request.json()) as { decision: 'Approve' | 'Reject' };
    const item = store.refundApprovals.find((r) => r.id === params.id);
    if (!item) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    item.status = body.decision === 'Approve' ? 'Approved' : 'Rejected';
    item.version += 1;
    return HttpResponse.json(item);
  }),

  // AD13 Payment Reconcile (querydr)
  http.get('/api/billing/admin/payments/reconcile', () => HttpResponse.json(store.reconcileRows)),
  http.post('/api/billing/admin/payments/querydr', async ({ request }) => {
    const body = (await request.json()) as { vnpTxnRef: string };
    const row = store.reconcileRows.find((r) => r.vnpTxnRef === body.vnpTxnRef);
    if (row) {
      row.gatewayStatus = 'Matched';
      row.localStatus = 'Paid';
      row.checkedAt = new Date().toISOString();
      return HttpResponse.json(row);
    }
    return HttpResponse.json({
      id: `rec-${Date.now()}`,
      vnpTxnRef: body.vnpTxnRef,
      appointmentOrBatchCode: 'TRA-CUU',
      amount: 150000,
      localStatus: 'Paid',
      gatewayStatus: 'Matched',
      checkedAt: new Date().toISOString(),
    });
  }),

  // AD14 Reports
  http.get('/api/analytics/admin/reports/summary', ({ request }) => {
    const url = new URL(request.url);
    return HttpResponse.json({
      dateFrom: url.searchParams.get('dateFrom') || store.today,
      dateTo: url.searchParams.get('dateTo') || store.today,
      totalAppointments: 142,
      checkedInCount: 126,
      cancelledCount: 9,
      totalRevenue: 48600000,
      totalRefunded: 1350000,
      dailySeries: [
        { date: '2026-11-09', bookings: 44, checkedIn: 39, revenue: 14200000 },
        { date: '2026-11-10', bookings: 48, checkedIn: 43, revenue: 16800000 },
        { date: '2026-11-11', bookings: 50, checkedIn: 44, revenue: 17600000 },
      ],
    });
  }),

  // AD15 Audit Logs
  http.get('/api/identity/admin/audit-logs', ({ request }) => {
    const kw = (new URL(request.url).searchParams.get('keyword') || '').toLowerCase();
    const items = kw
      ? store.auditLogs.filter(
          (l) =>
            l.actorName.toLowerCase().includes(kw) ||
            l.action.toLowerCase().includes(kw) ||
            l.entityId.toLowerCase().includes(kw),
        )
      : store.auditLogs;
    return HttpResponse.json({ items, hasMore: false });
  }),

  // AD16 CLS Waivers
  http.get('/api/billing/admin/cls-waivers', () => HttpResponse.json(store.clsWaivers)),
  http.post('/api/billing/admin/cls-waivers/:id/decision', async ({ params, request }) => {
    const body = (await request.json()) as { approve: boolean };
    const found = store.clsWaivers.find((w) => w.id === params.id);
    if (found) found.status = body.approve ? 'Approved' : 'Rejected';
    return HttpResponse.json(found ?? {});
  }),

  // AD17 Chatbot Config
  http.get('/api/notifications/admin/chatbot-config', () => HttpResponse.json(store.chatbotConfigs)),
  http.put('/api/notifications/admin/chatbot-config/:id', async ({ request }) => {
    const body = (await request.json()) as (typeof store.chatbotConfigs)[number];
    const idx = store.chatbotConfigs.findIndex((c) => c.id === body.id);
    if (idx >= 0) store.chatbotConfigs[idx] = body;
    return HttpResponse.json(body);
  }),

  // RC Reception
  http.get('/api/scheduling/staff/reception/search', ({ request }) => {
    const kw = (new URL(request.url).searchParams.get('keyword') || '').trim().toLowerCase();
    const items = kw
      ? store.receptionRecords.filter(
          (r) =>
            r.patientName.toLowerCase().includes(kw) ||
            r.phone.includes(kw) ||
            (r.appointmentCode?.toLowerCase().includes(kw) ?? false),
        )
      : store.receptionRecords;
    return HttpResponse.json(items);
  }),

  http.get('/api/scheduling/staff/walk-in/doctors', () =>
    HttpResponse.json(
      store.doctorDays.map((d) => ({
        doctorId: d.doctorId,
        doctorName: d.doctorName,
        specialtyName: d.specialtyName,
        roomName: d.roomName,
        remainingSlots: d.remainingSlots,
        consultationFee: d.doctorId === 'doc-2' ? 200000 : 150000,
      })),
    ),
  ),

  http.post('/api/scheduling/staff/walk-in', async ({ request }) => {
    const body = (await request.json()) as { doctorId: string; fullName: string };
    const doc = store.doctorDays.find((d) => d.doctorId === body.doctorId);
    if (doc && doc.remainingSlots > 0) doc.remainingSlots -= 1;
    const queueNumber = 18;
    return HttpResponse.json(
      {
        appointmentCode: `WI-${Date.now().toString().slice(-5)}`,
        ticketNumber: `A-0${queueNumber}`,
        queueNumber,
      },
      { status: 201 },
    );
  }),

  http.post('/api/scheduling/staff/appointments/:id/checkin', async ({ params, request }) => {
    const body = (await request.json()) as { identityDocumentVerified: boolean };
    const rec = store.receptionRecords.find((r) => r.appointmentId === params.id);
    if (!rec) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    if (rec.isPastCheckInDeadline) {
      return HttpResponse.json(
        { code: 'CHECKIN_DEADLINE_PASSED', detail: 'Đã quá hạn CheckInDeadline, không thể check-in thông thường.' },
        { status: 400 },
      );
    }
    if (!body.identityDocumentVerified) {
      return HttpResponse.json(
        { code: 'VALIDATION_ERROR', detail: 'Bắt buộc xác nhận đã đối chiếu CCCD/giấy tờ gốc.' },
        { status: 400 },
      );
    }
    rec.appointmentStatus = 'CheckedIn';
    rec.queueNumber = 19;
    return HttpResponse.json({ appointmentId: rec.appointmentId, queueNumber: 19, roomName: 'Phòng khám P201' });
  }),

  http.get('/api/billing/staff/cashier/items', ({ request }) => {
    const kw = (new URL(request.url).searchParams.get('keyword') || '').toLowerCase();
    const list = kw
      ? store.cashierItems.filter(
          (i) => i.patientName.toLowerCase().includes(kw) || i.referenceCode.toLowerCase().includes(kw),
        )
      : store.cashierItems;
    return HttpResponse.json(list);
  }),

  http.post('/api/billing/staff/cashier/items/:id/collect-cash', ({ params }) => {
    const item = store.cashierItems.find((i) => i.id === params.id);
    if (!item) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    item.status = 'Paid';
    item.paidAt = new Date().toISOString();
    item.receiptCode = `BL-${Date.now().toString().slice(-6)}`;
    return HttpResponse.json(item);
  }),

  http.post('/api/billing/staff/payments/querydr', async ({ request }) => {
    const body = (await request.json()) as { vnpTxnRef: string };
    return HttpResponse.json({
      id: `q-${Date.now()}`,
      vnpTxnRef: body.vnpTxnRef,
      appointmentOrBatchCode: 'SH-261111-001',
      amount: 150000,
      localStatus: 'Paid',
      gatewayStatus: 'Matched',
      checkedAt: new Date().toISOString(),
    });
  }),

  http.post('/api/scheduling/staff/appointments/:id/cancel', async ({ params, request }) => {
    const body = (await request.json()) as { reason: string; isCheckedIn: boolean };
    if (body.isCheckedIn && !body.reason.trim()) {
      return HttpResponse.json(
        { code: 'VALIDATION_ERROR', detail: 'Hủy lịch đã check-in bắt buộc nhập lý do rõ ràng.' },
        { status: 400 },
      );
    }
    const rec = store.receptionRecords.find((r) => r.appointmentId === params.id);
    if (rec) rec.appointmentStatus = 'Cancelled';
    return HttpResponse.json({ refundAmount: 150000 });
  }),

  http.get('/api/billing/staff/cash-refunds', () => HttpResponse.json(store.cashRefunds)),
  http.post('/api/billing/staff/cash-refunds/:id/disburse', ({ params }) => {
    const rf = store.cashRefunds.find((r) => r.id === params.id);
    if (rf) rf.status = 'Refunded';
    return HttpResponse.json({ id: params.id, voucherCode: `PC-${Date.now().toString().slice(-5)}` });
  }),

  http.get('/api/billing/staff/cashier/shift-summary', () => HttpResponse.json(store.shiftSummary)),
  http.post('/api/billing/staff/cashier/close-shift', () => {
    store.shiftSummary.isClosed = true;
    store.shiftSummary.closedAt = new Date().toISOString();
    return HttpResponse.json(store.shiftSummary);
  }),

  http.get('/api/scheduling/staff/call-list', () => HttpResponse.json(store.callTasks)),
  http.post('/api/scheduling/staff/call-list/:id/update', async ({ params, request }) => {
    const body = (await request.json()) as { status: 'Called' | 'Resolved'; note: string };
    const task = store.callTasks.find((t) => t.id === params.id);
    if (task) {
      task.status = body.status;
      task.note = body.note;
    }
    return HttpResponse.json(task ?? {});
  }),

  // NU Nurse
  http.get('/api/clinical/staff/nurse/queue', () => HttpResponse.json(store.nurseQueue)),
  http.post('/api/clinical/staff/nurse/queue/:encounterId/call', ({ params }) => {
    const q = store.nurseQueue.find((n) => n.encounterId === params.encounterId);
    if (q) q.status = 'Called';
    return HttpResponse.json(q ?? {});
  }),
  http.get('/api/clinical/staff/nurse/encounters/:encounterId/vitals', ({ params }) => {
    const form = store.vitalsForms[String(params.encounterId)] ?? store.vitalsForms['enc-nurse-1'];
    return HttpResponse.json(form);
  }),
  http.put('/api/clinical/staff/nurse/encounters/:encounterId/vitals', async ({ params, request }) => {
    const body = (await request.json()) as VitalsRecordDto;
    const encId = String(params.encounterId);
    const existing = store.vitalsForms[encId];
    if (existing && body.version !== existing.version) {
      return HttpResponse.json(
        { code: 'VERSION_CONFLICT', currentVersion: existing.version, detail: 'Phiếu sinh hiệu vừa được cập nhật.' },
        { status: 409 },
      );
    }
    const saved = { ...body, version: (existing?.version ?? 1) + 1 };
    store.vitalsForms[encId] = saved;
    const q = store.nurseQueue.find((n) => n.encounterId === encId);
    if (q) q.status = 'Completed';
    return HttpResponse.json(saved);
  }),

  // DR Doctor
  http.get('/api/clinical/staff/doctor/queues', () =>
    HttpResponse.json({
      initialQueue: store.doctorInitialQueue,
      clsReturnQueue: store.doctorClsReturnQueue,
      activeEncounterId: 'enc-doc-1',
    }),
  ),
  http.post('/api/clinical/staff/doctor/call-next', async ({ request }) => {
    const body = (await request.json()) as { queueKind: 'Initial' | 'ClsReturn' };
    const list = body.queueKind === 'ClsReturn' ? store.doctorClsReturnQueue : store.doctorInitialQueue;
    const first = list[0];
    if (!first) return HttpResponse.json({ code: 'NOT_FOUND', detail: 'Hàng đợi hiện đang trống.' }, { status: 404 });
    first.status = 'InService';
    return HttpResponse.json({ encounterId: first.encounterId, ticketNumber: first.ticketNumber });
  }),
  http.get('/api/clinical/staff/doctor/encounters/:encounterId', ({ params }) => {
    const ws = store.doctorWorkspaces[String(params.encounterId)] ?? store.doctorWorkspaces['enc-doc-1'];
    return HttpResponse.json(ws);
  }),
  http.get('/api/clinical/staff/icd10', ({ request }) => {
    const q = (new URL(request.url).searchParams.get('q') || '').toLowerCase();
    return HttpResponse.json(
      q ? MOCK_ICD10.filter((c) => c.code.toLowerCase().includes(q) || c.name.toLowerCase().includes(q)) : MOCK_ICD10,
    );
  }),
  http.get('/api/medication/staff/drugs', ({ request }) => {
    const q = (new URL(request.url).searchParams.get('q') || '').toLowerCase();
    return HttpResponse.json(
      q
        ? MOCK_DRUGS.filter(
            (d) => d.name.toLowerCase().includes(q) || d.activeIngredient.toLowerCase().includes(q),
          )
        : MOCK_DRUGS,
    );
  }),
  http.get('/api/clinical/staff/doctor/cls-services', () => HttpResponse.json(store.medicalServices)),
  http.put('/api/clinical/staff/doctor/encounters/:encounterId/draft', async ({ params, request }) => {
    const encId = String(params.encounterId);
    const ws = store.doctorWorkspaces[encId];
    const body = (await request.json()) as Partial<DoctorEncounterWorkspaceDto> & { version: number };
    if (!ws) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    if (body.version !== ws.version) {
      return HttpResponse.json(
        { code: 'VERSION_CONFLICT', currentVersion: ws.version, detail: 'Hồ sơ đã được cập nhật từ tab khác.' },
        { status: 409 },
      );
    }
    Object.assign(ws, body);
    ws.version += 1;
    return HttpResponse.json(ws);
  }),
  http.post('/api/clinical/staff/doctor/encounters/:encounterId/cls-orders', async ({ params, request }) => {
    const encId = String(params.encounterId);
    const body = (await request.json()) as { serviceIds: string[] };
    const ws = store.doctorWorkspaces[encId];
    if (!ws) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    body.serviceIds.forEach((sid) => {
      const svc = store.medicalServices.find((m) => m.id === sid);
      if (svc && svc.hasActiveRoom) {
        ws.clsOrders.push({
          id: `dcls-${Date.now()}-${sid}`,
          serviceCode: svc.code,
          serviceName: svc.name,
          status: 'PendingPayment',
          attachments: [],
        });
      }
    });
    ws.status = 'AwaitingCls';
    ws.version += 1;
    return HttpResponse.json(ws);
  }),
  http.post('/api/clinical/staff/doctor/encounters/:encounterId/publish', ({ params }) => {
    const ws = store.doctorWorkspaces[String(params.encounterId)];
    if (!ws) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    ws.status = 'Published';
    ws.version += 1;
    return HttpResponse.json(ws);
  }),
  http.post('/api/clinical/staff/doctor/encounters/:encounterId/amend', async ({ params, request }) => {
    const body = (await request.json()) as { amendedConclusion: string; amendReason: string };
    const ws = store.doctorWorkspaces[String(params.encounterId)];
    if (!ws) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    ws.amendmentHistory.unshift({
      version: ws.version + 1,
      amendedAt: new Date().toISOString(),
      summaryDiff: `"${ws.conclusion}" -> "${body.amendedConclusion}" (Lý do: ${body.amendReason})`,
    });
    ws.conclusion = body.amendedConclusion;
    ws.status = 'Amended';
    ws.version += 1;
    return HttpResponse.json(ws);
  }),
  http.get('/api/clinical/staff/doctor/records', () =>
    HttpResponse.json(
      Object.values(store.doctorWorkspaces).map((w) => ({
        id: w.encounterId,
        encounterCode: w.encounterCode,
        patientName: w.patientName,
        serviceDate: store.today,
        diagnosis: w.diagnoses[0]?.name || 'Đang khám',
        status: w.status,
      })),
    ),
  ),
  http.get('/api/documents/staff/cls-results/:resultId/files/:fileId/download', () => {
    const blob = new Blob(['%PDF-1.4\n% Staff Sample CLS File\n'], { type: 'application/pdf' });
    return new HttpResponse(blob, { status: 200, headers: { 'Content-Type': 'application/pdf' } });
  }),

  // CL Room CLS
  http.get('/api/clinical/staff/room/queue', () => HttpResponse.json(store.roomQueue)),
  http.post('/api/clinical/staff/room/queue/:orderItemId/action', async ({ params, request }) => {
    const body = (await request.json()) as { action: string };
    const item = store.roomQueue.find((q) => q.orderItemId === params.orderItemId);
    if (!item) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    if (body.action === 'Call') item.queueStatus = 'Called';
    else if (body.action === 'Defer') item.queueStatus = 'Deferred';
    else if (body.action === 'ConfirmPresence' || body.action === 'Start') item.queueStatus = 'InProgress';
    else if (body.action === 'FinishProcedure') item.queueStatus = 'Completed';
    item.version += 1;
    return HttpResponse.json(item);
  }),
  http.get('/api/clinical/staff/room/results/:orderItemId', ({ params }) => {
    const draft = store.roomDrafts[String(params.orderItemId)] ?? store.roomDrafts['cls-ord-1'];
    return HttpResponse.json(draft);
  }),
  http.put('/api/clinical/staff/room/results/:orderItemId', async ({ params, request }) => {
    const body = (await request.json()) as { summaryText: string; technicalConclusion: string; readingDoctorName: string };
    const draft = store.roomDrafts[String(params.orderItemId)];
    if (!draft) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    draft.summaryText = body.summaryText;
    draft.technicalConclusion = body.technicalConclusion;
    draft.readingDoctorName = body.readingDoctorName;
    draft.version += 1;
    return HttpResponse.json(draft);
  }),
  http.post('/api/documents/staff/room/results/:orderItemId/files', ({ params }) => {
    const draft = store.roomDrafts[String(params.orderItemId)];
    if (!draft) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    draft.draftFiles.push({
      fileId: `rf-${Date.now()}`,
      fileName: 'ket-qua-moi.pdf',
      mimeType: 'application/pdf',
      sizeBytes: 120000,
    });
    return HttpResponse.json(draft);
  }),
  http.post('/api/clinical/staff/room/results/:orderItemId/publish', ({ params }) => {
    const draft = store.roomDrafts[String(params.orderItemId)];
    if (!draft) return HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 });
    draft.resultStatus = 'Published';
    draft.version += 1;
    const q = store.roomQueue.find((r) => r.orderItemId === params.orderItemId);
    if (q) q.resultStatus = 'Published';
    return HttpResponse.json(draft);
  }),
];
