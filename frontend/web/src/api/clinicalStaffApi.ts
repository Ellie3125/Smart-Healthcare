import { apiDownloadBlob, apiGet, apiPost, apiPut } from '../shared/lib/http';
import { ApiHttpError } from '../shared/lib/errors';
import { createIdempotencyKey } from '../shared/lib/idempotency';
import type {
  ClsRoomQueueItem,
  ClsRoomResultDraftDto,
  DoctorClsResultView,
  DoctorEncounterWorkspaceDto,
  DoctorQueueEntry,
  DrugCatalogItem,
  Icd10CodeOption,
  MedicalServiceCatalogItem,
  NurseQueueItem,
  VitalsRecordDto,
} from '../types/staffWorkflow';

// Số vé hiển thị: backend trả displayNumber dạng "PK01-001"/"PK01-R001"; bản cũ chưa có thì dùng ticketNumber.
interface ApiTicketNumber {
  ticketNumber: string;
  displayNumber?: string | null;
}
const shown = (t: ApiTicketNumber) => t.displayNumber || t.ticketNumber;

interface ApiRoomQueueItem extends ApiTicketNumber {
  queueTicketId: string;
  sequenceNumber: number;
  ticketType: 'InitialConsultation' | 'ReturnConsultation' | 'ClsService' | string;
  queueStatus: string;
  calledAtUtc: string | null;
  encounterId: string | null;
  serviceOrderItemId: string | null;
  patientName: string;
}

interface ApiCalledTicket extends ApiTicketNumber {
  queueTicketId: string;
}

/**
 * Phòng làm việc hôm nay của người đăng nhập (`GET /api/scheduling/staff/my-rooms`):
 * RoomAccount theo token, Doctor theo phân công ngày, Nurse theo ca trực.
 * shortcut: dùng phòng đầu tiên; thêm chọn phòng khi một người trực nhiều phòng cùng lúc.
 */
export interface StaffRoom {
  roomId: string;
  roomName: string;
  stations: { stationId: string; stationCode: string; stationName: string }[];
}

const myRoom = async (): Promise<StaffRoom> => {
  const rooms = await apiGet<StaffRoom[]>('/api/scheduling/staff/my-rooms');
  if (!rooms.length) throw new ApiHttpError(403, 'NO_ROOM_TODAY', 'Bạn chưa được phân công phòng làm việc hôm nay.');
  return rooms[0];
};
const myRoomId = async () => (await myRoom()).roomId;

const roomQueue = async (roomId: string) =>
  (await apiGet<{ items: ApiRoomQueueItem[] }>(`/api/scheduling/staff/rooms/${roomId}/queue`, { params: { pageSize: 100 } })).items;

// Server chọn lượt tiếp theo của phòng; 204 khi hàng chờ trống.
// Phòng có nhiều vị trí phục vụ phải gửi stationId (409 StationRequired).
const callNext = async (roomId: string, stationId?: string): Promise<ApiCalledTicket> => {
  const res = await apiPost<ApiCalledTicket | '', { stationId?: string }>(
    `/api/scheduling/staff/rooms/${roomId}/call-next`,
    stationId ? { stationId } : {},
  );
  if (!res) throw new ApiHttpError(409, 'QUEUE_EMPTY', 'Hàng chờ đang trống.');
  return res;
};

// Bệnh án bác sĩ (`docs/api/clinical.md` §bệnh án): version base64, ghi là thay toàn bộ nội dung.
interface ApiDiagnosis {
  diagnosisType: 'Initial' | 'Final';
  icD10Code: string;
  diagnosisName: string;
  isPrimary: boolean;
  clinicalNotes: string | null;
}

const RECORD_FIELDS = [
  'chiefComplaint',
  'symptoms',
  'medicalHistory',
  'clinicalExaminationNotes',
  'initialClinicalImpression',
  'postCLSConclusion',
  'treatmentPlan',
  'followUpInstructions',
] as const;
type RecordContent = Partial<Record<(typeof RECORD_FIELDS)[number], string | null>>;

interface ApiRecordState {
  record: RecordContent & { status: string };
  diagnoses: ApiDiagnosis[];
  version: string;
}

interface ApiServiceOrder {
  items: {
    id: string;
    serviceNameSnapshot: string;
    itemStatus: string;
    result: { resultSummary: string; detailedFindings: string; resultStatus: string } | null;
  }[];
}

interface ApiDoctorPrescription {
  items: {
    medicationId: string;
    medicationNameSnapshot: string;
    activeIngredientSnapshot: string;
    dosage: string;
    frequency: string;
    durationDays: number;
    quantity: number;
    usageInstructions: string;
  }[];
}

// Bản đã đọc gần nhất của từng lượt khám: giữ các trường form không hiển thị khi gửi lại (thay toàn bộ).
const recordCache = new Map<string, ApiRecordState>();

const ENCOUNTER_STATUS: Record<string, DoctorEncounterWorkspaceDto['status']> = {
  InProgress: 'InProgress',
  WaitingCLS: 'AwaitingCls',
  ReturningDoctor: 'ClsCompleted',
  Completed: 'Published',
  Abandoned: 'Published',
};

const clsStatus = (item: ApiServiceOrder['items'][number]): DoctorClsResultView['status'] =>
  item.result?.resultStatus === 'Published' ? 'Published'
    : item.result ? 'Draft'
      : item.itemStatus === 'Ordered' ? 'Waiting'
        : 'InProgress';

async function getDoctorWorkspace(encounterId: string): Promise<DoctorEncounterWorkspaceDto> {
  const base = `/api/clinical/staff/doctor/encounters/${encounterId}`;
  const [encounter, state, orders, prescriptions] = await Promise.all([
    apiGet<{ id: string; status: string }>(base),
    apiGet<ApiRecordState>(`${base}/record`).catch((e: unknown) => {
      if (e instanceof ApiHttpError && e.status === 404) return null;
      throw e;
    }),
    apiGet<ApiServiceOrder[]>(`${base}/service-orders`),
    apiGet<ApiDoctorPrescription[]>(`${base}/prescriptions`).catch(() => [] as ApiDoctorPrescription[]),
  ]);
  if (state) recordCache.set(encounterId, state);
  const r: RecordContent = state?.record ?? {};
  return {
    encounterId,
    encounterCode: encounterId.slice(0, 8).toUpperCase(),
    ticketNumber: '',
    patientName: '',
    patientAge: 0,
    patientGender: '',
    status: state?.record.status === 'Published' ? 'Published' : ENCOUNTER_STATUS[encounter.status] ?? 'InProgress',
    version: state?.version ?? '',
    symptoms: r.symptoms ?? '',
    clinicalNotes: r.clinicalExaminationNotes ?? '',
    diagnoses: (state?.diagnoses ?? []).map((d) => ({ code: d.icD10Code, name: d.diagnosisName })),
    conclusion: r.postCLSConclusion ?? r.initialClinicalImpression ?? '',
    followUpInstruction: r.followUpInstructions ?? '',
    clsOrders: orders.flatMap((o) => o.items).map((i) => ({
      id: i.id,
      serviceCode: '',
      serviceName: i.serviceNameSnapshot,
      status: clsStatus(i),
      summaryText: i.result?.resultSummary,
      technicalConclusion: i.result?.detailedFindings,
      attachments: [],
    })),
    prescriptionItems: prescriptions.flatMap((p) => p.items).map((i) => ({
      drugId: i.medicationId,
      drugName: i.medicationNameSnapshot,
      activeIngredient: i.activeIngredientSnapshot,
      dosage: i.dosage,
      frequency: i.frequency,
      durationDays: i.durationDays,
      quantity: i.quantity,
      unit: '',
      usageInstruction: i.usageInstructions,
    })),
    amendmentHistory: [],
  };
}

// Giữ loại/chính của chẩn đoán đã có; chẩn đoán mới là Initial, mục đầu tiên là chính.
const toApiDiagnoses = (encounterId: string, list: Icd10CodeOption[]) => {
  const known = new Map((recordCache.get(encounterId)?.diagnoses ?? []).map((d) => [d.icD10Code, d]));
  return list.map((d, i) => ({
    diagnosisType: known.get(d.code)?.diagnosisType ?? 'Initial',
    icd10Code: d.code,
    diagnosisName: d.name,
    isPrimary: known.get(d.code)?.isPrimary ?? i === 0,
    clinicalNotes: known.get(d.code)?.clinicalNotes ?? null,
  }));
};

const cachedContent = (encounterId: string): RecordContent => {
  const r: RecordContent = recordCache.get(encounterId)?.record ?? {};
  return Object.fromEntries(RECORD_FIELDS.map((f) => [f, r[f] ?? null]));
};

const queueStatus = (s: string) => (s === 'Finished' ? 'Completed' : s);

/**
 * Clinical Staff API: Điều dưỡng (NU01–NU02), Bác sĩ (DR01–DR10), Phòng CLS (CL01–CL04).
 */
export const clinicalStaffApi = {
  // NU01 + NU02 Nurse
  // TODO: nối contract thật hàng chờ điều dưỡng & lưu sinh hiệu
  // NU01: hàng chờ phòng điều dưỡng đang trực (ClinicalStaffListEndpoints), chỉ đọc; bác sĩ/phòng gọi số.
  listNurseQueue: async (): Promise<NurseQueueItem[]> => {
    const roomId = await myRoomId();
    const page = await apiGet<{ items: (ApiTicketNumber & {
      encounterId: string;
      patientName: string;
      specialtyName: string | null;
      queueStatus: string;
    })[] }>('/api/clinical/staff/nurse/queue', { params: { roomId, pageSize: 100 } });
    return page.items.map((x) => ({
      encounterId: x.encounterId,
      ticketNumber: shown(x),
      patientName: x.patientName,
      yearOfBirth: 0,
      doctorName: '',
      specialtyName: x.specialtyName ?? '',
      status: queueStatus(x.queueStatus) as NurseQueueItem['status'],
      version: 0,
    }));
  },
  // NU02: sinh hiệu chỉ ghi thêm, không có route đọc form; vé/tên lấy từ hàng chờ điều dưỡng.
  getVitalsForm: async (encounterId: string): Promise<VitalsRecordDto> => {
    const row = (await clinicalStaffApi.listNurseQueue()).find((q) => q.encounterId === encounterId);
    return {
      encounterId,
      ticketNumber: row?.ticketNumber ?? '',
      patientName: row?.patientName ?? '',
      version: 0,
      systolicBp: '',
      diastolicBp: '',
      heartRate: '',
      temperatureC: '',
      spo2: '',
      respiratoryRate: '',
      weightKg: '',
      heightCm: '',
      nurseNote: '',
    };
  },
  // Gửi số hoặc null (không chuỗi rỗng); backend kiểm ngưỡng và độ chính xác.
  saveVitals: async (encounterId: string, body: VitalsRecordDto, idempotencyKey: string): Promise<VitalsRecordDto> => {
    const num = (v: string) => (v.trim() === '' ? null : Number(v));
    await apiPost<unknown, unknown>(
      `/api/clinical/staff/nurse/encounters/${encounterId}/vitals`,
      {
        weightKg: num(body.weightKg),
        heightCm: num(body.heightCm),
        systolicBp: num(body.systolicBp),
        diastolicBp: num(body.diastolicBp),
        heartRate: num(body.heartRate),
        temperatureC: num(body.temperatureC),
        spo2: num(body.spo2),
        respiratoryRate: num(body.respiratoryRate),
        nurseNote: body.nurseNote.trim() || null,
      },
      { idempotencyKey },
    );
    return body;
  },

  // DR01–DR10 Doctor
  // TODO: nối contract thật 2 hàng chờ bác sĩ + chỉ có nút "Gọi người tiếp theo"
  // DR01: một hàng chờ của phòng bác sĩ, tách theo loại vé (khám ban đầu / quay lại sau CLS).
  getDoctorQueues: async () => {
    const items = (await roomQueue(await myRoomId())).filter((t) => t.encounterId);
    const entry = (t: ApiRoomQueueItem): DoctorQueueEntry => ({
      encounterId: t.encounterId!,
      ticketNumber: shown(t),
      patientName: t.patientName,
      queueKind: t.ticketType === 'ReturnConsultation' ? 'ClsReturn' : 'Initial',
      status: t.queueStatus === 'Waiting' || t.queueStatus === 'Deferred' ? 'Waiting' : 'InService',
      checkedInAt: t.calledAtUtc ?? '',
    });
    const active = items.find((t) => t.queueStatus === 'InProgress') ?? items.find((t) => t.queueStatus === 'Called');
    return {
      initialQueue: items.filter((t) => t.ticketType !== 'ReturnConsultation').map(entry),
      clsReturnQueue: items.filter((t) => t.ticketType === 'ReturnConsultation').map(entry),
      activeEncounterId: active?.encounterId ?? undefined,
      // Vé đang ở bàn bác sĩ: Called → bắt đầu khám, InProgress → xong lượt (giải phóng bệnh nhân sang CLS hoặc kết thúc).
      activeTicket: active
        ? { queueTicketId: active.queueTicketId, ticketNumber: shown(active), patientName: active.patientName, status: active.queueStatus }
        : undefined,
    };
  },
  startTicket: (queueTicketId: string) => apiPost<ApiCalledTicket, Record<string, never>>(`/api/scheduling/staff/tickets/${queueTicketId}/start`, {}),
  finishTicket: (queueTicketId: string) => apiPost<ApiCalledTicket, Record<string, never>>(`/api/scheduling/staff/tickets/${queueTicketId}/finish`, {}),
  // Server chọn lượt (trộn hai loại vé theo thứ tự), không chọn theo queueKind.
  callNextDoctorPatient: async (_queueKind: 'Initial' | 'ClsReturn', _idempotencyKey?: string) => {
    const roomId = await myRoomId();
    const called = await callNext(roomId);
    const ticket = (await roomQueue(roomId)).find((t) => t.queueTicketId === called.queueTicketId);
    return { encounterId: ticket?.encounterId ?? '', ticketNumber: shown(called) };
  },
  getDoctorWorkspace,
  // Danh mục thuốc đang hoạt động (Medication #160), tối đa 20 mục.
  searchDrugs: (q: string) =>
    apiGet<DrugCatalogItem[]>('/api/medication-catalog/staff/drugs', { params: { q } }),
  // Danh mục dịch vụ CLS thuộc Scheduling; không có giá và tên phòng.
  listOrderableClsServices: async (): Promise<MedicalServiceCatalogItem[]> =>
    (await apiGet<{ items: {
      id: string;
      serviceCode: string;
      serviceName: string;
      serviceCategory: string;
      defaultRoomId: string;
      isActive: boolean;
      rooms: { roomId: string; isActive: boolean }[];
    }[] }>('/api/scheduling/staff/medical-services', { params: { pageSize: 100 } })).items
      .filter((s) => s.isActive)
      .map((s) => ({
        id: s.id,
        code: s.serviceCode,
        name: s.serviceName,
        category: s.serviceCategory as MedicalServiceCatalogItem['category'],
        roomId: s.defaultRoomId,
        roomName: '',
        hasActiveRoom: s.rooms.some((r) => r.isActive),
        unitPrice: 0,
        isActive: s.isActive,
      })),

  // Lưu nháp: gửi lại toàn bộ nội dung (trường không có trên form lấy từ bản đã đọc). Không lưu đơn thuốc.
  saveDoctorDraft: async (
    encounterId: string,
    body: Pick<DoctorEncounterWorkspaceDto, 'symptoms' | 'clinicalNotes' | 'diagnoses' | 'conclusion' | 'followUpInstruction' | 'version'>,
  ) => {
    await apiPut<ApiRecordState, unknown>(
      `/api/clinical/staff/doctor/encounters/${encounterId}/draft`,
      {
        ...cachedContent(encounterId),
        version: body.version,
        symptoms: body.symptoms || null,
        clinicalExaminationNotes: body.clinicalNotes || null,
        postCLSConclusion: body.conclusion || null,
        followUpInstructions: body.followUpInstruction || null,
        diagnoses: toApiDiagnoses(encounterId, body.diagnoses),
      },
      { idempotencyKey: createIdempotencyKey(`draft-${encounterId}`) },
    );
    return getDoctorWorkspace(encounterId);
  },

  // Response là đợt chỉ định + vé, đọc lại màn khám sau khi tạo.
  orderClsServices: async (encounterId: string, serviceIds: string[], idempotencyKey: string) => {
    await apiPost<unknown, { serviceIds: string[] }>(
      `/api/clinical/staff/doctor/encounters/${encounterId}/cls-orders`,
      { serviceIds },
      { idempotencyKey },
    );
    return getDoctorWorkspace(encounterId);
  },

  publishEncounter: async (encounterId: string, version: string, idempotencyKey: string) => {
    await apiPost<unknown, { version: string }>(
      `/api/clinical/staff/doctor/encounters/${encounterId}/publish`,
      { version },
      { idempotencyKey },
    );
    return getDoctorWorkspace(encounterId);
  },

  // Bổ sung sau phát hành: cùng cấu trúc nháp, đổi kết luận và thêm lý do.
  amendEncounter: async (
    encounterId: string,
    body: { version: string; amendedConclusion: string; amendReason: string },
    idempotencyKey: string,
  ) => {
    const cached = recordCache.get(encounterId);
    await apiPost<unknown, unknown>(
      `/api/clinical/staff/doctor/encounters/${encounterId}/amend`,
      {
        ...cachedContent(encounterId),
        version: body.version,
        postCLSConclusion: body.amendedConclusion || null,
        reason: body.amendReason,
        diagnoses: (cached?.diagnoses ?? []).map((d) => ({
          diagnosisType: d.diagnosisType,
          icd10Code: d.icD10Code,
          diagnosisName: d.diagnosisName,
          isPrimary: d.isPrimary,
          clinicalNotes: d.clinicalNotes,
        })),
      },
      { idempotencyKey },
    );
    return getDoctorWorkspace(encounterId);
  },

  // DR10: backend chỉ lọc theo đúng mã lượt khám (GUID); từ khóa khác lọc trong 100 hồ sơ gần nhất phía web.
  listDoctorRecords: async (keyword?: string) => {
    const k = keyword?.trim() ?? '';
    const isId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(k);
    const page = await apiGet<{ items: { id: string; patientName: string; serviceDate: string; diagnosis: string; status: string }[] }>(
      '/api/clinical/staff/doctor/records',
      { params: { keyword: isId ? k : undefined, pageSize: 100 } },
    );
    const needle = k.toLowerCase();
    return page.items
      .map((r) => ({ ...r, encounterCode: r.id.slice(0, 8).toUpperCase() }))
      .filter((r) => isId || !needle || `${r.encounterCode} ${r.patientName} ${r.diagnosis}`.toLowerCase().includes(needle));
  },

  downloadDoctorClsAttachment: (resultId: string, fileId: string) =>
    apiDownloadBlob(`/api/documents/staff/cls-results/${resultId}/files/${fileId}/download`),

  // CL01–CL04 Room CLS
  // TODO: nối contract thật hàng chờ phòng CLS + upload draft + phát hành kết quả
  // CL01: vé CLS đang chờ của phòng + vé đã làm xong còn chờ phát hành kết quả (kể cả ngày trước).
  listRoomQueue: async (): Promise<ClsRoomQueueItem[]> => {
    const roomId = await myRoomId();
    const [queue, pending] = await Promise.all([
      roomQueue(roomId),
      apiGet<{ items: (ApiTicketNumber & { queueTicketId: string; serviceOrderItemId: string; completedAtUtc: string | null })[] }>(
        `/api/scheduling/staff/rooms/${roomId}/pending-results`,
        { params: { pageSize: 100 } },
      ),
    ]);
    const row = (queueTicketId: string, ticketNumber: string, orderItemId: string, status: string, patientName: string,
      resultStatus: ClsRoomQueueItem['resultStatus'], date: string): ClsRoomQueueItem => ({
      orderItemId,
      queueTicketId,
      ticketNumber,
      patientName,
      serviceCode: '',
      serviceName: '',
      serviceDate: date,
      queueStatus: queueStatus(status) as ClsRoomQueueItem['queueStatus'],
      resultStatus,
      readingDoctorName: '',
      version: 0,
    });
    return [
      ...queue.filter((t) => t.serviceOrderItemId).map((t) =>
        row(t.queueTicketId, shown(t), t.serviceOrderItemId!, t.queueStatus, t.patientName, 'NotStarted', t.calledAtUtc ?? '')),
      ...pending.items.map((p) => row(p.queueTicketId, shown(p), p.serviceOrderItemId, 'Completed', '', 'Draft', p.completedAtUtc ?? '')),
    ];
  },
  listDiagnosedResults: async (): Promise<ClsRoomQueueItem[]> => {
    const all = await clinicalStaffApi.listRoomQueue();
    return all.filter((r) => r.resultStatus !== 'NotStarted' || r.queueStatus === 'Completed');
  },
  // Call: server gọi lượt kế tiếp của phòng; các thao tác khác theo vé (Scheduling §4.8).
  getMyRoom: myRoom,
  roomQueueAction: async (
    queueTicketId: string,
    action: 'Call' | 'ConfirmPresence' | 'Defer' | 'Start' | 'FinishProcedure',
    stationId?: string,
  ) => {
    if (action === 'Call') return callNext(await myRoomId(), stationId);
    const path = action === 'Defer' ? 'requeue' : action === 'FinishProcedure' ? 'finish' : 'start';
    return apiPost<ApiCalledTicket, Record<string, never>>(`/api/scheduling/staff/tickets/${queueTicketId}/${path}`, {});
  },
  // CL02: draft envelope (contract Clinical §7); bác sĩ đọc chọn theo id trong readingDoctors.
  getRoomResultDraft: async (orderItemId: string): Promise<ClsRoomResultDraftDto> => {
    const d = await apiGet<{
      orderItemId: string;
      queueTicketId: string;
      resultStatus: 'Draft' | 'Published';
      summaryText: string;
      technicalConclusion: string;
      version: string;
      readingDoctorId?: string | null;
      readingDoctors?: { doctorId: string; fullName: string }[];
    }>(`/api/clinical/staff/room/results/${orderItemId}`).catch((e: unknown) => {
      // Phòng không xem lại kết quả đã phát hành (contract §7): hiện trạng thái khóa thay vì lỗi.
      if (e instanceof ApiHttpError && e.status === 409 && e.code === 'RESULTALREADYPUBLISHED')
        return { orderItemId, queueTicketId: '', resultStatus: 'Published' as const, summaryText: '', technicalConclusion: '', version: '', readingDoctorId: null, readingDoctors: [] };
      throw e;
    });
    // Envelope không có tên bệnh nhân (contract §7); số vé lấy từ danh sách chờ kết quả của phòng để đối chiếu.
    const ticketNumber = await myRoomId()
      .then((roomId) => apiGet<{ items: (ApiTicketNumber & { queueTicketId: string })[] }>(
        `/api/scheduling/staff/rooms/${roomId}/pending-results`, { params: { pageSize: 100 } }))
      .then((p) => { const t = p.items.find((i) => i.queueTicketId === d.queueTicketId); return t ? shown(t) : ''; })
      .catch(() => '');
    return {
      orderItemId: d.orderItemId,
      queueTicketId: d.queueTicketId,
      ticketNumber,
      patientName: '',
      serviceCode: '',
      serviceName: 'Dịch vụ CLS',
      resultStatus: d.resultStatus,
      summaryText: d.summaryText ?? '',
      technicalConclusion: d.technicalConclusion ?? '',
      readingDoctorName: d.readingDoctorId ?? '',
      availableShiftDoctors: [],
      readingDoctors: d.readingDoctors ?? [],
      version: d.version,
      draftFiles: [],
    };
  },
  saveRoomResultDraft: (
    orderItemId: string,
    body: { summaryText: string; technicalConclusion: string; readingDoctorId: string; version: string },
  ) =>
    apiPut<unknown, unknown>(`/api/clinical/staff/room/results/${orderItemId}`, {
      summaryText: body.summaryText,
      technicalConclusion: body.technicalConclusion,
      version: body.version,
      readingDoctorId: body.readingDoctorId || null,
    }),
  publishRoomResult: (orderItemId: string, version: string, idempotencyKey: string, readingDoctorId?: string) =>
    apiPost<unknown, unknown>(
      `/api/clinical/staff/room/results/${orderItemId}/publish`,
      { version, readingDoctorId: readingDoctorId || null },
      { idempotencyKey },
    ),
};
