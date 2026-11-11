import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { XCircle } from 'lucide-react';
import { receptionApi } from '../../api/receptionApi';
import {
  Button,
  Card,
  DataTable,
  Dialog,
  ErrorState,
  FormField,
  Skeleton,
  StatusPill,
  Textarea,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { ReceptionSearchResult } from '../../types/staffWorkflow';

/**
 * RC07 — Hủy lịch tại quầy: nếu lịch đang ở trạng thái `CheckedIn` BẮT BUỘC nhập lý do rõ ràng.
 */
export const ReceptionCancelPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [target, setTarget] = useState<ReceptionSearchResult | null>(null);
  const [reason, setReason] = useState('');

  const query = useQuery({
    queryKey: ['reception', 'cancel-candidates'],
    queryFn: () => receptionApi.searchUnified(''),
  });

  const cancelMutation = useMutation({
    mutationFn: () =>
      receptionApi.cancelAtCounter(
        target!.appointmentId!,
        {
          reason: reason.trim(),
          isCheckedIn: target!.appointmentStatus === 'CheckedIn',
        },
        createIdempotencyKey(`rc-cancel-${target!.appointmentId}`),
      ),
    onSuccess: (res) => {
      pushToast(`Đã hủy lịch tại quầy. Dự kiến hoàn: ${res.refundAmount.toLocaleString('vi-VN')} ₫`, 'success');
      setTarget(null);
      setReason('');
      queryClient.invalidateQueries({ queryKey: ['reception'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  const rows = (query.data ?? []).filter((r) => r.appointmentId && r.appointmentStatus !== 'Cancelled');

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Hủy lịch tại quầy (RC07)</h1>
        <p className="text-xs text-[#44544c]">Hủy lịch đã check-in bắt buộc ghi rõ lý do nghiệp vụ để phục vụ kiểm toán hoàn tiền.</p>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.appointmentId!}
          rows={rows}
          columns={[
            { key: 'code', header: 'Mã lịch', render: (r) => <span className="font-mono font-semibold">{r.appointmentCode}</span> },
            { key: 'patient', header: 'Bệnh nhân', render: (r) => `${r.patientName} (${r.phone})` },
            { key: 'doctor', header: 'Bác sĩ', render: (r) => r.doctorName || '—' },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.appointmentStatus || 'Confirmed'} /> },
            {
              key: 'act',
              header: 'Thao tác',
              render: (r) => (
                <Button size="sm" variant="danger" leftIcon={<XCircle className="h-3.5 w-3.5" />} onClick={() => setTarget(r)}>
                  Hủy lịch
                </Button>
              ),
            },
          ]}
        />
      )}

      <Dialog
        open={Boolean(target)}
        onClose={() => setTarget(null)}
        title={`Hủy lịch ${target?.appointmentCode}`}
        description={
          target?.appointmentStatus === 'CheckedIn'
            ? 'Cảnh báo: Bệnh nhân ĐÃ CHECK-IN — Bắt buộc nhập lý do hủy rõ ràng.'
            : undefined
        }
        footer={
          <>
            <Button variant="outline" onClick={() => setTarget(null)}>Quay lại</Button>
            <Button
              variant="danger"
              loading={cancelMutation.isPending}
              disabled={target?.appointmentStatus === 'CheckedIn' ? !reason.trim() : false}
              onClick={() => cancelMutation.mutate()}
            >
              Xác nhận hủy
            </Button>
          </>
        }
      >
        <FormField
          label="Lý do hủy lịch"
          required={target?.appointmentStatus === 'CheckedIn'}
          error={target?.appointmentStatus === 'CheckedIn' && !reason.trim() ? 'Lịch đã check-in bắt buộc nhập lý do' : undefined}
        >
          {(props) => (
            <Textarea
              {...props}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="VD: Bệnh nhân đột xuất không thể chờ khám..."
            />
          )}
        </FormField>
      </Dialog>
    </div>
  );
};
