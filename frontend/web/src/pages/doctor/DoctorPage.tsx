import React, { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { CheckCircle2, FilePlus2, FlaskConical, History, PhoneForwarded, Pill, Save, Send } from 'lucide-react';
import { clinicalStaffApi } from '../../api/clinicalStaffApi';
import {
  Banner,
  Button,
  Card,
  ConfirmDialog,
  DateTimeText,
  Dialog,
  ErrorState,
  FormField,
  Input,
  Skeleton,
  StatusPill,
  Tabs,
  Textarea,
  VersionConflictDialog,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';
import type {
  DoctorEncounterWorkspaceDto,
  DoctorPrescriptionRow,
  Icd10CodeOption,
} from '../../types/staffWorkflow';

/**
 * DR01–DR08 & DR10 — Bàn làm việc Bác sĩ:
 * - Tách 2 hàng đợi ("Khám đầu" & "Quay lại"), CHỈ có nút "Gọi người tiếp theo" do Backend chọn tuần tự.
 * - Auto-draft, ICD-10 search, chỉ định CLS (chỉ phòng đang hoạt động), Phát hành + Bổ sung có Diff.
 * - Kê đơn Full-Text Search (không tồn kho, không nút mua, không tương tác thuốc giả lập).
 * - Optimistic concurrency 409 qua VersionConflictDialog.
 */
export const DoctorPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [activeEncounterId, setActiveEncounterId] = useState<string>('enc-doc-1');
  const [tab, setTab] = useState<'exam' | 'cls' | 'rx' | 'amend'>('exam');
  const [draft, setDraft] = useState<DoctorEncounterWorkspaceDto | null>(null);
  const [draftSavedHint, setDraftSavedHint] = useState<string>('');
  const [icdQuery, setIcdQuery] = useState('');
  const [drugQuery, setDrugQuery] = useState('');
  const [selectedClsIds, setSelectedClsIds] = useState<string[]>([]);
  const [publishConfirmOpen, setPublishConfirmOpen] = useState(false);
  const [amendModalOpen, setAmendModalOpen] = useState(false);
  const [amendConclusion, setAmendConclusion] = useState('');
  const [amendReason, setAmendReason] = useState('');
  const [conflictState, setConflictState] = useState<{ open: boolean; currentVersion?: number; detail?: string }>({
    open: false,
  });

  const queuesQuery = useQuery({
    queryKey: ['staff', 'doctor-queues'],
    queryFn: () => clinicalStaffApi.getDoctorQueues(),
  });

  const wsQuery = useQuery({
    queryKey: ['staff', 'doctor-workspace', activeEncounterId],
    queryFn: () => clinicalStaffApi.getDoctorWorkspace(activeEncounterId),
    enabled: Boolean(activeEncounterId),
  });

  const icdSearchQuery = useQuery({
    queryKey: ['staff', 'icd10', icdQuery],
    queryFn: () => clinicalStaffApi.searchIcd10(icdQuery),
  });

  const drugSearchQuery = useQuery({
    queryKey: ['staff', 'drugs', drugQuery],
    queryFn: () => clinicalStaffApi.searchDrugs(drugQuery),
  });

  const clsServicesQuery = useQuery({
    queryKey: ['staff', 'doctor-cls-services'],
    queryFn: () => clinicalStaffApi.listOrderableClsServices(),
  });

  useEffect(() => {
    if (wsQuery.data) {
      setDraft(wsQuery.data);
      setAmendConclusion(wsQuery.data.conclusion || '');
    }
  }, [wsQuery.data]);

  const callNextMutation = useMutation({
    mutationFn: (queueKind: 'Initial' | 'ClsReturn') =>
      clinicalStaffApi.callNextDoctorPatient(queueKind, createIdempotencyKey(`doc-call-${queueKind}`)),
    onSuccess: (res) => {
      setActiveEncounterId(res.encounterId);
      pushToast(`Đang mời bệnh nhân vé ${res.ticketNumber} vào phòng khám.`, 'success');
      queryClient.invalidateQueries({ queryKey: ['staff', 'doctor-queues'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  const saveDraftMutation = useMutation({
    mutationFn: (payload: DoctorEncounterWorkspaceDto) =>
      clinicalStaffApi.saveDoctorDraft(payload.encounterId, {
        symptoms: payload.symptoms,
        clinicalNotes: payload.clinicalNotes,
        diagnoses: payload.diagnoses,
        conclusion: payload.conclusion,
        followUpInstruction: payload.followUpInstruction,
        version: payload.version,
        prescriptionItems: payload.prescriptionItems,
      }),
    onSuccess: (updated) => {
      setDraft(updated);
      setDraftSavedHint(`Đã lưu nháp lúc ${new Date().toLocaleTimeString('vi-VN')} (v${updated.version})`);
    },
    onError: (err) => {
      const parsed = extractFriendlyError(err);
      if (parsed.isConflict) {
        setConflictState({ open: true, currentVersion: parsed.currentVersion, detail: parsed.message });
      } else {
        pushToast(parsed.message, 'danger');
      }
    },
  });

  const orderClsMutation = useMutation({
    mutationFn: () =>
      clinicalStaffApi.orderClsServices(
        activeEncounterId,
        selectedClsIds,
        createIdempotencyKey(`cls-ord-${activeEncounterId}`),
      ),
    onSuccess: (updated) => {
      setDraft(updated);
      setSelectedClsIds([]);
      pushToast('Đã phát sinh đợt chỉ định CLS cho bệnh nhân.', 'success');
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  const publishMutation = useMutation({
    mutationFn: () =>
      clinicalStaffApi.publishEncounter(
        activeEncounterId,
        draft!.version,
        createIdempotencyKey(`pub-${activeEncounterId}`),
      ),
    onSuccess: (updated) => {
      setDraft(updated);
      setPublishConfirmOpen(false);
      pushToast('Đã phát hành hồ sơ bệnh án cho bệnh nhân.', 'success');
      queryClient.invalidateQueries({ queryKey: ['staff', 'doctor-queues'] });
    },
    onError: (err) => {
      const parsed = extractFriendlyError(err);
      if (parsed.isConflict) {
        setPublishConfirmOpen(false);
        setConflictState({ open: true, currentVersion: parsed.currentVersion, detail: parsed.message });
      } else {
        pushToast(parsed.message, 'danger');
      }
    },
  });

  const amendMutation = useMutation({
    mutationFn: () =>
      clinicalStaffApi.amendEncounter(
        activeEncounterId,
        {
          version: draft!.version,
          amendedConclusion: amendConclusion,
          amendReason,
        },
        createIdempotencyKey(`amend-${activeEncounterId}`),
      ),
    onSuccess: (updated) => {
      setDraft(updated);
      setAmendModalOpen(false);
      setAmendReason('');
      pushToast('Đã bổ sung hồ sơ sau phát hành.', 'success');
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  // Auto-draft nhẹ sau khi ngừng gõ 1.8 giây
  useEffect(() => {
    if (!draft || !wsQuery.data || draft.status === 'Published') return;
    if (draft.symptoms === wsQuery.data.symptoms && draft.conclusion === wsQuery.data.conclusion) return;
    const t = window.setTimeout(() => {
      saveDraftMutation.mutate(draft);
    }, 1800);
    return () => window.clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft?.symptoms, draft?.conclusion]);

  const initialQueue = queuesQuery.data?.initialQueue ?? [];
  const clsReturnQueue = queuesQuery.data?.clsReturnQueue ?? [];

  const addIcd = (codeObj: Icd10CodeOption) => {
    setDraft((prev) => {
      if (!prev || prev.diagnoses.some((d) => d.code === codeObj.code)) return prev;
      return { ...prev, diagnoses: [...prev.diagnoses, codeObj] };
    });
  };

  const addDrug = (d: { id: string; name: string; activeIngredient: string; defaultDosage: string; unit: string }) => {
    setDraft((prev) => {
      if (!prev) return prev;
      const nextRow: DoctorPrescriptionRow = {
        drugId: d.id,
        drugName: d.name,
        activeIngredient: d.activeIngredient,
        dosage: d.defaultDosage,
        frequency: 'Ngày 1 lần',
        durationDays: 14,
        quantity: 14,
        unit: d.unit,
        usageInstruction: d.defaultDosage,
      };
      return { ...prev, prescriptionItems: [...prev.prescriptionItems, nextRow] };
    });
  };

  const isPublished = draft?.status === 'Published' || draft?.status === 'Amended';

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Bàn làm việc Bác sĩ (DR01–DR10)</h1>
          <p className="text-xs text-[#44544c]">Hàng đợi gọi tuần tự theo hệ thống, lưu nháp tự động và kiểm soát phiên bản v409.</p>
        </div>
        <Link to="/doctor/records">
          <Button variant="outline" size="sm" leftIcon={<History className="h-4 w-4" />}>
            Lịch sử khám (Chỉ đọc)
          </Button>
        </Link>
      </div>

      {/* DR01 Dual Queue — CHỈ có nút "Gọi người tiếp theo" do Backend chọn */}
      <div className="grid gap-4 md:grid-cols-2">
        <Card
          title={`Hàng đợi Khám đầu (${initialQueue.length})`}
          actions={
            <Button
              size="sm"
              variant="primary"
              leftIcon={<PhoneForwarded className="h-3.5 w-3.5" />}
              loading={callNextMutation.isPending}
              onClick={() => callNextMutation.mutate('Initial')}
            >
              Gọi người tiếp theo
            </Button>
          }
        >
          <ul className="space-y-1.5 text-xs">
            {initialQueue.map((q) => (
              <li key={q.encounterId} className="flex items-center justify-between rounded-lg bg-[#f4f8f6] px-3 py-2">
                <span>
                  <strong className="font-mono text-[#006a41]">{q.ticketNumber}</strong> — {q.patientName}
                </span>
                <StatusPill status={q.status} />
              </li>
            ))}
          </ul>
        </Card>

        <Card
          title={`Hàng đợi Quay lại sau CLS (${clsReturnQueue.length})`}
          actions={
            <Button
              size="sm"
              variant="secondary"
              leftIcon={<PhoneForwarded className="h-3.5 w-3.5" />}
              loading={callNextMutation.isPending}
              onClick={() => callNextMutation.mutate('ClsReturn')}
            >
              Gọi người tiếp theo
            </Button>
          }
        >
          <ul className="space-y-1.5 text-xs">
            {clsReturnQueue.map((q) => (
              <li key={q.encounterId} className="flex items-center justify-between rounded-lg bg-[#f4f8f6] px-3 py-2">
                <span>
                  <strong className="font-mono text-[#006a41]">{q.ticketNumber}</strong> — {q.patientName}
                </span>
                <StatusPill status={q.status} />
              </li>
            ))}
          </ul>
        </Card>
      </div>

      {wsQuery.isLoading || !draft ? (
        <Card><Skeleton rows={5} /></Card>
      ) : wsQuery.isError ? (
        <ErrorState message={extractFriendlyError(wsQuery.error).message} onRetry={() => wsQuery.refetch()} />
      ) : (
        <Card
          title={`Ca đang xử lý: ${draft.ticketNumber} — ${draft.patientName} (${draft.patientGender}, ${draft.patientAge} tuổi)`}
          subtitle={`${draft.encounterCode} • ${draft.vitalsSummary || 'Chưa có sinh hiệu'} • ${draftSavedHint || `Phiên bản v${draft.version}`}`}
          actions={
            <div className="flex flex-wrap items-center gap-2">
              <StatusPill status={draft.status} />
              <Button
                size="sm"
                variant="outline"
                leftIcon={<Save className="h-3.5 w-3.5" />}
                loading={saveDraftMutation.isPending}
                disabled={isPublished}
                onClick={() => saveDraftMutation.mutate(draft)}
              >
                Lưu nháp
              </Button>
              {!isPublished ? (
                <Button
                  size="sm"
                  variant="primary"
                  leftIcon={<Send className="h-3.5 w-3.5" />}
                  onClick={() => setPublishConfirmOpen(true)}
                >
                  Phát hành hồ sơ
                </Button>
              ) : (
                <Button size="sm" variant="warning" onClick={() => setAmendModalOpen(true)}>
                  Bổ sung sau phát hành
                </Button>
              )}
            </div>
          }
        >
          <Tabs
            activeId={tab}
            onChange={(id) => setTab(id as typeof tab)}
            tabs={[
              { id: 'exam', label: 'Khám & ICD-10' },
              { id: 'cls', label: 'Chỉ định & KQ CLS', count: draft.clsOrders.length },
              { id: 'rx', label: 'Kê đơn thuốc', count: draft.prescriptionItems.length },
              { id: 'amend', label: 'Lịch sử bổ sung', count: draft.amendmentHistory.length },
            ]}
          />

          {tab === 'exam' ? (
            <div className="mt-4 space-y-4">
              <div className="grid gap-4 md:grid-cols-2">
                <FormField label="Triệu chứng cơ năng">
                  {(props) => (
                    <Textarea
                      {...props}
                      disabled={isPublished}
                      value={draft.symptoms}
                      onChange={(e) => setDraft((p) => (p ? { ...p, symptoms: e.target.value } : p))}
                    />
                  )}
                </FormField>
                <FormField label="Khám thực thể lâm sàng">
                  {(props) => (
                    <Textarea
                      {...props}
                      disabled={isPublished}
                      value={draft.clinicalNotes}
                      onChange={(e) => setDraft((p) => (p ? { ...p, clinicalNotes: e.target.value } : p))}
                    />
                  )}
                </FormField>
              </div>

              <div className="space-y-2">
                <FormField label="Tìm mã bệnh ICD-10">
                  {(props) => (
                    <Input
                      {...props}
                      disabled={isPublished}
                      value={icdQuery}
                      onChange={(e) => setIcdQuery(e.target.value)}
                      placeholder="Nhập mã (I10, K29...) hoặc tên bệnh"
                    />
                  )}
                </FormField>
                <div className="flex flex-wrap gap-2">
                  {(icdSearchQuery.data ?? []).map((item) => (
                    <Button
                      key={item.code}
                      size="sm"
                      variant="outline"
                      disabled={isPublished}
                      onClick={() => addIcd(item)}
                    >
                      + {item.code} ({item.name})
                    </Button>
                  ))}
                </div>
                <div className="flex flex-wrap gap-2 pt-1">
                  {draft.diagnoses.map((d) => (
                    <span
                      key={d.code}
                      className="rounded-full bg-[#d4f7e2] px-3 py-1 text-xs font-medium text-[#005231]"
                    >
                      {d.code} — {d.name}
                    </span>
                  ))}
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <FormField label="Kết luận chẩn đoán">
                  {(props) => (
                    <Textarea
                      {...props}
                      disabled={isPublished}
                      value={draft.conclusion}
                      onChange={(e) => setDraft((p) => (p ? { ...p, conclusion: e.target.value } : p))}
                    />
                  )}
                </FormField>
                <FormField label="Lời dặn theo dõi / tái khám">
                  {(props) => (
                    <Textarea
                      {...props}
                      disabled={isPublished}
                      value={draft.followUpInstruction}
                      onChange={(e) => setDraft((p) => (p ? { ...p, followUpInstruction: e.target.value } : p))}
                    />
                  )}
                </FormField>
              </div>
            </div>
          ) : null}

          {tab === 'cls' ? (
            <div className="mt-4 space-y-5">
              <div>
                <h3 className="mb-2 text-sm font-semibold text-[#0f1f18]">Chỉ định Cận lâm sàng mới (chỉ dịch vụ có phòng mở)</h3>
                <div className="grid gap-2 sm:grid-cols-2">
                  {(clsServicesQuery.data ?? []).map((svc) => {
                    const checked = selectedClsIds.includes(svc.id);
                    return (
                      <label
                        key={svc.id}
                        className={`flex items-center justify-between gap-2 rounded-xl border p-3 text-xs ${
                          svc.hasActiveRoom ? 'border-[#cddbd3] bg-white' : 'border-[#ffb4ab] bg-[#fff5f4] opacity-70'
                        }`}
                      >
                        <div className="flex items-center gap-2">
                          <input
                            type="checkbox"
                            disabled={!svc.hasActiveRoom || isPublished}
                            checked={checked}
                            onChange={(e) =>
                              setSelectedClsIds((prev) =>
                                e.target.checked ? [...prev, svc.id] : prev.filter((id) => id !== svc.id),
                              )
                            }
                          />
                          <span>
                            <strong>{svc.code}</strong> — {svc.name} ({svc.roomName})
                          </span>
                        </div>
                        {!svc.hasActiveRoom ? <span className="text-[#ba1a1a]">Ngưng phòng</span> : null}
                      </label>
                    );
                  })}
                </div>
                <div className="mt-3 flex justify-end">
                  <Button
                    variant="primary"
                    size="sm"
                    leftIcon={<FlaskConical className="h-4 w-4" />}
                    disabled={selectedClsIds.length === 0 || isPublished}
                    loading={orderClsMutation.isPending}
                    onClick={() => orderClsMutation.mutate()}
                  >
                    Tạo chỉ định CLS ({selectedClsIds.length})
                  </Button>
                </div>
              </div>

              <div className="space-y-2">
                <h3 className="text-sm font-semibold text-[#0f1f18]">Theo dõi & Kết quả CLS</h3>
                {draft.clsOrders.map((ord) => (
                  <div key={ord.id} className="rounded-xl border border-[#cddbd3] p-3 text-xs">
                    <div className="flex items-center justify-between">
                      <strong>{ord.serviceName} ({ord.serviceCode})</strong>
                      <StatusPill status={ord.status} />
                    </div>
                    {ord.summaryText ? <p className="mt-1 text-[#0f1f18]">{ord.summaryText}</p> : null}
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {tab === 'rx' ? (
            <div className="mt-4 space-y-4">
              <FormField label="Tìm kiếm thuốc từ danh mục chuẩn (Full-Text Search)">
                {(props) => (
                  <Input
                    {...props}
                    disabled={isPublished}
                    value={drugQuery}
                    onChange={(e) => setDrugQuery(e.target.value)}
                    placeholder="Nhập tên thuốc hoặc hoạt chất..."
                  />
                )}
              </FormField>

              <div className="flex flex-wrap gap-2">
                {(drugSearchQuery.data ?? []).map((drug) => (
                  <Button
                    key={drug.id}
                    size="sm"
                    variant="outline"
                    leftIcon={<Pill className="h-3.5 w-3.5" />}
                    disabled={isPublished}
                    onClick={() => addDrug(drug)}
                  >
                    + {drug.name}
                  </Button>
                ))}
              </div>

              <div className="space-y-2">
                {draft.prescriptionItems.map((line, idx) => (
                  <div key={`${line.drugId}-${idx}`} className="rounded-xl border border-[#cddbd3] bg-[#f8fbf9] p-3 text-xs">
                    <div className="flex items-center justify-between">
                      <strong>{idx + 1}. {line.drugName} ({line.activeIngredient})</strong>
                      <span>{line.quantity} {line.unit}</span>
                    </div>
                    <p className="mt-1 text-[#44544c]">{line.dosage} • {line.frequency} • {line.durationDays} ngày</p>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {tab === 'amend' ? (
            <div className="mt-4 space-y-3">
              {draft.amendmentHistory.length === 0 ? (
                <p className="text-xs text-[#44544c]">Chưa có bổ sung nào sau phát hành.</p>
              ) : (
                draft.amendmentHistory.map((h) => (
                  <div key={h.version} className="rounded-xl border border-[#fcd34d] bg-[#fef3c7]/40 p-3 text-xs">
                    <p className="font-semibold">
                      Phiên bản v{h.version} • <DateTimeText value={h.amendedAt} />
                    </p>
                    <p className="mt-1 font-mono text-[#0f1f18]">{h.summaryDiff}</p>
                  </div>
                ))
              )}
            </div>
          ) : null}
        </Card>
      )}

      <ConfirmDialog
        open={publishConfirmOpen}
        onClose={() => setPublishConfirmOpen(false)}
        onConfirm={() => publishMutation.mutate()}
        loading={publishMutation.isPending}
        title="Xác nhận phát hành hồ sơ bệnh án"
        confirmLabel="Phát hành chính thức"
        message={
          <div className="flex items-start gap-2">
            <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-[#006a41]" aria-hidden="true" />
            <span>
              Sau khi phát hành, kết luận khám và đơn thuốc sẽ hiển thị ngay cho bệnh nhân trên cổng thông tin. Mọi chỉnh sửa tiếp theo sẽ được ghi nhận thành bản bổ sung có vết.
            </span>
          </div>
        }
      />

      <Dialog
        open={amendModalOpen}
        onClose={() => setAmendModalOpen(false)}
        title="Bổ sung hồ sơ sau phát hành (DR08)"
        footer={
          <>
            <Button variant="outline" onClick={() => setAmendModalOpen(false)}>Hủy</Button>
            <Button
              variant="warning"
              leftIcon={<FilePlus2 className="h-4 w-4" />}
              loading={amendMutation.isPending}
              disabled={!amendConclusion.trim() || !amendReason.trim()}
              onClick={() => amendMutation.mutate()}
            >
              Lưu bản bổ sung
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <Banner tone="info">Bản kết luận trước: {draft?.conclusion || '—'}</Banner>
          <FormField label="Kết luận cập nhật mới" required>
            {(props) => <Textarea {...props} value={amendConclusion} onChange={(e) => setAmendConclusion(e.target.value)} />}
          </FormField>
          <FormField label="Lý do bổ sung" required>
            {(props) => <Textarea {...props} value={amendReason} onChange={(e) => setAmendReason(e.target.value)} placeholder="Ghi rõ lý do y khoa..." />}
          </FormField>
        </div>
      </Dialog>

      <VersionConflictDialog
        open={conflictState.open}
        onClose={() => setConflictState({ open: false })}
        onReloadLatest={() => {
          setConflictState({ open: false });
          wsQuery.refetch();
        }}
        entityName="Hồ sơ khám bệnh"
        currentVersion={conflictState.currentVersion}
        serverDetail={conflictState.detail}
      />
    </div>
  );
};
