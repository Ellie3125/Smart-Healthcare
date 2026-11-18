import { apiDownloadBlob, apiGet, apiPost, apiPut, apiUpload } from '../shared/lib/http';
import { ApiHttpError } from '../shared/lib/errors';
import type {
  ClsRoomQueueItem,
  ClsRoomResultDraftDto,
  DoctorEncounterWorkspaceDto,
  DoctorQueueEntry,
  DrugCatalogItem,
  Icd10CodeOption,
  MedicalServiceCatalogItem,
  NurseQueueItem,
  VitalsRecordDto,
} from '../types/staffWorkflow';

interface ApiRoomQueueItem {
  queueTicketId: string;
  ticketNumber: string;
  sequenceNumber: number;
  ticketType: 'InitialConsultation' | 'ReturnConsultation' | 'ClsService' | string;
  queueStatus: string;
  calledAtUtc: string | null;
  encounterId: string | null;
  serviceOrderItemId: string | null;
  patientName: string;
}

interface ApiCalledTicket {
  queueTicketId: string;
  ticketNumber: string;
}

/**
 * Phòng làm việc hôm nay của người đăng nhập (`GET /api/scheduling/staff/my-rooms`):
 * RoomAccount theo token, Doctor theo phân công ngày, Nurse theo ca trực.
 * shortcut: dùng phòng đầu tiên; thêm chọn phòng khi một người trực nhiều phòng cùng lúc.
 */
const myRoomId = async (): Promise<string> => {
  const rooms = await apiGet<{ roomId: string; roomName: string }[]>('/api/scheduling/staff/my-rooms');
  if (!rooms.length) throw new ApiHttpError(403, 'NO_ROOM_TODAY', 'Bạn chưa được phân công phòng làm việc hôm nay.');
  return rooms[0].roomId;
};

const roomQueue = async (roomId: string) =>
  (await apiGet<{ items: ApiRoomQueueItem[] }>(`/api/scheduling/staff/rooms/${roomId}/queue`, { params: { pageSize: 100 } })).items;

// Server chọn lượt tiếp theo của phòng; 204 khi hàng chờ trống.
const callNext = async (roomId: string): Promise<ApiCalledTicket> => {
  const res = await apiPost<ApiCalledTicket | '', Record<string, never>>(`/api/scheduling/staff/rooms/${roomId}/call-next`, {});
  if (!res) throw new ApiHttpError(409, 'QUEUE_EMPTY', 'Hàng chờ đang trống.');
  return res;
};

const queueStatus = (s: string) => (s === 'Finished' ? 'Completed' : s);

/**
 * Clinical Staff API: Điều dưỡng (NU01–NU02), Bác sĩ (DR01–DR10), Phòng CLS (CL01–CL04).
 */
export const clinicalStaffApi = {
  // NU01 + NU02 Nurse
  // TODO: nối contract thật hàng chờ điều dưỡng & lưu sinh hiệu
  // NU01: hàng chờ phòng điều dưỡng đang trực (ClinicalStaffListEndpoints), gọi lượt bằng call-next của phòng.
  listNurseQueue: async (): Promise<NurseQueueItem[]> => {
    const roomId = await myRoomId();
    const page = await apiGet<{ items: {
      encounterId: string;
      ticketNumber: string;
      patientName: string;
      specialtyName: string | null;
      queueStatus: string;
    }[] }>('/api/clinical/staff/nurse/queue', { params: { roomId, pageSize: 100 } });
    return page.items.map((x) => ({
      encounterId: x.encounterId,
      ticketNumber: x.ticketNumber,
      patientName: x.patientName,
      yearOfBirth: 0,
      doctorName: '',
      specialtyName: x.specialtyName ?? '',
      status: queueStatus(x.queueStatus) as NurseQueueItem['status'],
      version: 0,
    }));
  },
  callNursePatient: async () => callNext(await myRoomId()),
  getVitalsForm: (encounterId: string) =>
    apiGet<VitalsRecordDto>(`/api/clinical/staff/nurse/encounters/${encounterId}/vitals`),
  saveVitals: (encounterId: string, body: VitalsRecordDto, idempotencyKey: string) =>
    apiPut<VitalsRecordDto, VitalsRecordDto>(
      `/api/clinical/staff/nurse/encounters/${encounterId}/vitals`,
      body,
      { idempotencyKey, version: body.version },
    ),

  // DR01–DR10 Doctor
  // TODO: nối contract thật 2 hàng chờ bác sĩ + chỉ có nút "Gọi người tiếp theo"
  // DR01: một hàng chờ của phòng bác sĩ, tách theo loại vé (khám ban đầu / quay lại sau CLS).
  getDoctorQueues: async () => {
    const items = (await roomQueue(await myRoomId())).filter((t) => t.encounterId);
    const entry = (t: ApiRoomQueueItem): DoctorQueueEntry => ({
      encounterId: t.encounterId!,
      ticketNumber: t.ticketNumber,
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
    };
  },
  // Server chọn lượt (trộn hai loại vé theo thứ tự), không chọn theo queueKind.
  callNextDoctorPatient: async (_queueKind: 'Initial' | 'ClsReturn', _idempotencyKey?: string) => {
    const roomId = await myRoomId();
    const called = await callNext(roomId);
    const ticket = (await roomQueue(roomId)).find((t) => t.queueTicketId === called.queueTicketId);
    return { encounterId: ticket?.encounterId ?? '', ticketNumber: called.ticketNumber };
  },
  getDoctorWorkspace: (encounterId: string) =>
    apiGet<DoctorEncounterWorkspaceDto>(`/api/clinical/staff/doctor/encounters/${encounterId}`),
  searchIcd10: (q: string) =>
    apiGet<Icd10CodeOption[]>('/api/clinical/staff/icd10', { params: { q } }),
  searchDrugs: (q: string) =>
    apiGet<DrugCatalogItem[]>('/api/medication/staff/drugs', { params: { q } }),
  listOrderableClsServices: () =>
    apiGet<MedicalServiceCatalogItem[]>('/api/clinical/staff/doctor/cls-services'),

  saveDoctorDraft: (
    encounterId: string,
    body: Pick<
      DoctorEncounterWorkspaceDto,
      'symptoms' | 'clinicalNotes' | 'diagnoses' | 'conclusion' | 'followUpInstruction' | 'version' | 'prescriptionItems'
    >,
  ) =>
    apiPut<DoctorEncounterWorkspaceDto, typeof body>(
      `/api/clinical/staff/doctor/encounters/${encounterId}/draft`,
      body,
      { version: body.version },
    ),

  orderClsServices: (encounterId: string, serviceIds: string[], idempotencyKey: string) =>
    apiPost<DoctorEncounterWorkspaceDto, { serviceIds: string[] }>(
      `/api/clinical/staff/doctor/encounters/${encounterId}/cls-orders`,
      { serviceIds },
      { idempotencyKey },
    ),

  publishEncounter: (encounterId: string, version: number, idempotencyKey: string) =>
    apiPost<DoctorEncounterWorkspaceDto, { version: number }>(
      `/api/clinical/staff/doctor/encounters/${encounterId}/publish`,
      { version },
      { idempotencyKey, version },
    ),

  amendEncounter: (
    encounterId: string,
    body: { version: number; amendedConclusion: string; amendReason: string },
    idempotencyKey: string,
  ) =>
    apiPost<DoctorEncounterWorkspaceDto, typeof body>(
      `/api/clinical/staff/doctor/encounters/${encounterId}/amend`,
      body,
      { idempotencyKey, version: body.version },
    ),

  listDoctorRecords: (keyword?: string) =>
    apiGet<{ id: string; encounterCode: string; patientName: string; serviceDate: string; diagnosis: string; status: string }[]>(
      '/api/clinical/staff/doctor/records',
      { params: keyword ? { keyword } : undefined },
    ),

  downloadDoctorClsAttachment: (resultId: string, fileId: string) =>
    apiDownloadBlob(`/api/documents/staff/cls-results/${resultId}/files/${fileId}/download`),

  // CL01–CL04 Room CLS
  // TODO: nối contract thật hàng chờ phòng CLS + upload draft + phát hành kết quả
  // CL01: vé CLS đang chờ của phòng + vé đã làm xong còn chờ phát hành kết quả (kể cả ngày trước).
  listRoomQueue: async (): Promise<ClsRoomQueueItem[]> => {
    const roomId = await myRoomId();
    const [queue, pending] = await Promise.all([
      roomQueue(roomId),
      apiGet<{ items: { queueTicketId: string; ticketNumber: string; serviceOrderItemId: string; completedAtUtc: string | null }[] }>(
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
        row(t.queueTicketId, t.ticketNumber, t.serviceOrderItemId!, t.queueStatus, t.patientName, 'NotStarted', t.calledAtUtc ?? '')),
      ...pending.items.map((p) => row(p.queueTicketId, p.ticketNumber, p.serviceOrderItemId, 'Completed', '', 'Draft', p.completedAtUtc ?? '')),
    ];
  },
  // Call: server gọi lượt kế tiếp của phòng; các thao tác khác theo vé (Scheduling §4.8).
  roomQueueAction: async (queueTicketId: string, action: 'Call' | 'ConfirmPresence' | 'Defer' | 'Start' | 'FinishProcedure') => {
    if (action === 'Call') return callNext(await myRoomId());
    const path = action === 'Defer' ? 'requeue' : action === 'FinishProcedure' ? 'finish' : 'start';
    return apiPost<ApiCalledTicket, Record<string, never>>(`/api/scheduling/staff/tickets/${queueTicketId}/${path}`, {});
  },
  getRoomResultDraft: (orderItemId: string) =>
    apiGet<ClsRoomResultDraftDto>(`/api/clinical/staff/room/results/${orderItemId}`),
  saveRoomResultDraft: (
    orderItemId: string,
    body: { summaryText: string; technicalConclusion: string; readingDoctorName: string; version: number },
  ) =>
    apiPut<ClsRoomResultDraftDto, typeof body>(
      `/api/clinical/staff/room/results/${orderItemId}`,
      body,
      { version: body.version },
    ),
  uploadRoomDraftFile: (orderItemId: string, file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return apiUpload<ClsRoomResultDraftDto>(`/api/documents/staff/room/results/${orderItemId}/files`, fd);
  },
  publishRoomResult: (orderItemId: string, version: number, idempotencyKey: string) =>
    apiPost<ClsRoomResultDraftDto, { version: number }>(
      `/api/clinical/staff/room/results/${orderItemId}/publish`,
      { version },
      { idempotencyKey, version },
    ),
};
