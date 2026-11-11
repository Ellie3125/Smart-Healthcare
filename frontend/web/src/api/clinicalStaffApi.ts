import { apiDownloadBlob, apiGet, apiPost, apiPut, apiUpload } from '../shared/lib/http';
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

/**
 * Clinical Staff API: Điều dưỡng (NU01–NU02), Bác sĩ (DR01–DR10), Phòng CLS (CL01–CL04).
 */
export const clinicalStaffApi = {
  // NU01 + NU02 Nurse
  // TODO: nối contract thật hàng chờ điều dưỡng & lưu sinh hiệu
  listNurseQueue: () => apiGet<NurseQueueItem[]>('/api/clinical/staff/nurse/queue'),
  callNursePatient: (encounterId: string) =>
    apiPost<NurseQueueItem, Record<string, never>>(`/api/clinical/staff/nurse/queue/${encounterId}/call`, {}),
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
  getDoctorQueues: () =>
    apiGet<{
      initialQueue: DoctorQueueEntry[];
      clsReturnQueue: DoctorQueueEntry[];
      activeEncounterId?: string;
    }>('/api/clinical/staff/doctor/queues'),
  callNextDoctorPatient: (queueKind: 'Initial' | 'ClsReturn', idempotencyKey: string) =>
    apiPost<{ encounterId: string; ticketNumber: string }, { queueKind: 'Initial' | 'ClsReturn' }>(
      '/api/clinical/staff/doctor/call-next',
      { queueKind },
      { idempotencyKey },
    ),
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
  listRoomQueue: () => apiGet<ClsRoomQueueItem[]>('/api/clinical/staff/room/queue'),
  roomQueueAction: (
    orderItemId: string,
    action: 'Call' | 'ConfirmPresence' | 'Defer' | 'Start' | 'FinishProcedure',
    version: number,
  ) =>
    apiPost<ClsRoomQueueItem, { action: string; version: number }>(
      `/api/clinical/staff/room/queue/${orderItemId}/action`,
      { action, version },
      { version },
    ),
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
