import React, { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import { AlertTriangle, ClipboardList } from 'lucide-react';
import { clinicalStaffApi } from '../../api/clinicalStaffApi';
import {
  Banner,
  Button,
  Card,
  ConfirmDialog,
  ErrorState,
  FormField,
  Input,
  Skeleton,
  Textarea,
  VersionConflictDialog,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { VitalsRecordDto } from '../../types/staffWorkflow';

/**
 * NU02 — Nhập sinh hiệu điều dưỡng:
 * - Cảnh báo giá trị vượt ngưỡng sinh lý
 * - Cảnh báo khi còn ô chỉ số bỏ trống trước khi bấm Hoàn tất
 * - Optimistic concurrency `version` + `VersionConflictDialog` khi 409
 */
export const NursePage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const encounterId = searchParams.get('encounterId') || 'enc-nurse-1';
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const [form, setForm] = useState<VitalsRecordDto | null>(null);
  const [emptyConfirmOpen, setEmptyConfirmOpen] = useState(false);
  const [conflictState, setConflictState] = useState<{ open: boolean; currentVersion?: number; detail?: string }>({
    open: false,
  });

  const query = useQuery({
    queryKey: ['staff', 'nurse-vitals', encounterId],
    queryFn: () => clinicalStaffApi.getVitalsForm(encounterId),
  });

  useEffect(() => {
    if (query.data) setForm(query.data);
  }, [query.data]);

  const saveMutation = useMutation({
    mutationFn: (payload: VitalsRecordDto) =>
      clinicalStaffApi.saveVitals(encounterId, payload, createIdempotencyKey(`vitals-${encounterId}`)),
    onSuccess: (updated) => {
      setForm(updated);
      setEmptyConfirmOpen(false);
      pushToast('Đã lưu phiếu sinh hiệu và chuyển bệnh nhân sang hàng chờ bác sĩ.', 'success');
      queryClient.invalidateQueries({ queryKey: ['staff', 'nurse-queue'] });
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

  const warnings = useMemo(() => {
    if (!form) return [];
    const list: string[] = [];
    const sys = Number(form.systolicBp);
    const dia = Number(form.diastolicBp);
    const hr = Number(form.heartRate);
    const temp = Number(form.temperatureC);
    const spo2 = Number(form.spo2);
    if (form.systolicBp && (sys >= 140 || sys < 90)) list.push(`Huyết áp tâm thu bất thường (${sys} mmHg)`);
    if (form.diastolicBp && (dia >= 90 || dia < 60)) list.push(`Huyết áp tâm trương bất thường (${dia} mmHg)`);
    if (form.heartRate && (hr < 50 || hr > 110)) list.push(`Mạch ngoài khoảng tham chiếu (${hr} l/p)`);
    if (form.temperatureC && temp >= 38) list.push(`Sốt cao cần lưu ý (${temp} °C)`);
    if (form.spo2 && spo2 < 94) list.push(`SpO2 thấp (< 94%: ${spo2}%) — Cân nhắc ưu tiên khám`);
    return list;
  }, [form]);

  const emptyFields = useMemo(() => {
    if (!form) return [];
    const labels: [keyof VitalsRecordDto, string][] = [
      ['systolicBp', 'Huyết áp tâm thu'],
      ['diastolicBp', 'Huyết áp tâm trương'],
      ['heartRate', 'Mạch'],
      ['temperatureC', 'Nhiệt độ'],
      ['spo2', 'SpO2'],
      ['respiratoryRate', 'Nhịp thở'],
      ['weightKg', 'Cân nặng'],
      ['heightCm', 'Chiều cao'],
    ];
    return labels.filter(([k]) => !String(form[k] ?? '').trim()).map(([, label]) => label);
  }, [form]);

  const bmiText = useMemo(() => {
    if (!form) return '—';
    const w = Number(form.weightKg);
    const h = Number(form.heightCm) / 100;
    if (!w || !h) return '—';
    return (w / (h * h)).toFixed(1);
  }, [form]);

  if (query.isLoading || !form) {
    return (
      <Card title="Đo sinh hiệu điều dưỡng">
        <Skeleton rows={5} />
      </Card>
    );
  }

  if (query.isError) {
    return <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />;
  }

  const updateField = (key: keyof VitalsRecordDto, val: string) => {
    setForm((prev) => (prev ? { ...prev, [key]: val } : prev));
  };

  const handleSubmitClick = () => {
    if (emptyFields.length > 0) {
      setEmptyConfirmOpen(true);
      return;
    }
    saveMutation.mutate(form);
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">
            Ghi nhận sinh hiệu (NU02) — Vé {form.ticketNumber}
          </h1>
          <p className="text-xs text-[#44544c]">
            Bệnh nhân: <strong>{form.patientName}</strong> • Phiên bản dữ liệu v{form.version}
          </p>
        </div>
        <Link to="/nurse/queue">
          <Button variant="outline" size="sm" leftIcon={<ClipboardList className="h-4 w-4" />}>
            Mở hàng chờ điều dưỡng
          </Button>
        </Link>
      </div>

      {warnings.length > 0 ? (
        <Banner tone="danger" title="Cảnh báo chỉ số sinh hiệu bất thường">
          <ul className="list-disc pl-4">
            {warnings.map((w) => (
              <li key={w}>{w}</li>
            ))}
          </ul>
        </Banner>
      ) : null}

      <Card title="Thông số sinh hiệu">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <FormField label="Huyết áp tâm thu (mmHg)">
            {(props) => <Input {...props} type="number" value={form.systolicBp} onChange={(e) => updateField('systolicBp', e.target.value)} />}
          </FormField>
          <FormField label="Huyết áp tâm trương (mmHg)">
            {(props) => <Input {...props} type="number" value={form.diastolicBp} onChange={(e) => updateField('diastolicBp', e.target.value)} />}
          </FormField>
          <FormField label="Mạch (lần/phút)">
            {(props) => <Input {...props} type="number" value={form.heartRate} onChange={(e) => updateField('heartRate', e.target.value)} />}
          </FormField>
          <FormField label="Nhiệt độ (°C)">
            {(props) => <Input {...props} type="number" step="0.1" value={form.temperatureC} onChange={(e) => updateField('temperatureC', e.target.value)} />}
          </FormField>
          <FormField label="SpO2 (%)">
            {(props) => <Input {...props} type="number" value={form.spo2} onChange={(e) => updateField('spo2', e.target.value)} />}
          </FormField>
          <FormField label="Nhịp thở (lần/phút)">
            {(props) => <Input {...props} type="number" value={form.respiratoryRate} onChange={(e) => updateField('respiratoryRate', e.target.value)} />}
          </FormField>
          <FormField label="Cân nặng (kg)">
            {(props) => <Input {...props} type="number" step="0.1" value={form.weightKg} onChange={(e) => updateField('weightKg', e.target.value)} />}
          </FormField>
          <FormField label="Chiều cao (cm)" hint={`BMI tự tính: ${bmiText}`}>
            {(props) => <Input {...props} type="number" value={form.heightCm} onChange={(e) => updateField('heightCm', e.target.value)} />}
          </FormField>
        </div>

        <div className="mt-4">
          <FormField label="Ghi chú điều dưỡng (dị ứng, tình trạng cấp tính nếu có)">
            {(props) => <Textarea {...props} value={form.nurseNote} onChange={(e) => updateField('nurseNote', e.target.value)} />}
          </FormField>
        </div>

        <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-[#e9f2ed] pt-4">
          {emptyFields.length > 0 ? (
            <span className="inline-flex items-center gap-1.5 text-xs font-medium text-[#9a5b00]">
              <AlertTriangle className="h-4 w-4" aria-hidden="true" />
              Còn {emptyFields.length} ô chỉ số chưa nhập
            </span>
          ) : (
            <span className="text-xs text-[#006a41]">Đã điền đầy đủ toàn bộ chỉ số.</span>
          )}
          <Button variant="primary" loading={saveMutation.isPending} onClick={handleSubmitClick}>
            Hoàn tất & Lưu sinh hiệu
          </Button>
        </div>
      </Card>

      <ConfirmDialog
        open={emptyConfirmOpen}
        onClose={() => setEmptyConfirmOpen(false)}
        onConfirm={() => saveMutation.mutate(form)}
        loading={saveMutation.isPending}
        confirmVariant="warning"
        title="Vẫn còn chỉ số sinh hiệu để trống"
        message={
          <div className="space-y-2">
            <p>Các trường sau hiện đang để trống: <strong>{emptyFields.join(', ')}</strong>.</p>
            <p>Bạn có chắc chắn muốn hoàn tất và lưu phiếu sinh hiệu này không?</p>
          </div>
        }
      />

      <VersionConflictDialog
        open={conflictState.open}
        onClose={() => setConflictState({ open: false })}
        onReloadLatest={() => {
          setConflictState({ open: false });
          query.refetch();
        }}
        entityName="Phiếu sinh hiệu"
        currentVersion={conflictState.currentVersion}
        serverDetail={conflictState.detail}
      />
    </div>
  );
};
