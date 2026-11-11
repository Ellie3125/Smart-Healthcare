import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminOpsApi } from '../../api/adminOpsApi';
import {
  Button,
  Card,
  DataTable,
  DateTimeText,
  Dialog,
  EmptyState,
  ErrorState,
  FormField,
  Skeleton,
  StatusPill,
  Textarea,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { OperationalExceptionItem } from '../../types/staffWorkflow';

export const ExceptionsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [selected, setSelected] = useState<OperationalExceptionItem | null>(null);
  const [note, setNote] = useState('');

  const query = useQuery({
    queryKey: ['admin', 'exceptions'],
    queryFn: () => adminOpsApi.listExceptions(),
  });

  const resolveMutation = useMutation({
    mutationFn: () =>
      adminOpsApi.resolveException(
        selected!.id,
        note.trim(),
        createIdempotencyKey(`exc-${selected!.id}`),
      ),
    onSuccess: () => {
      pushToast('Đã ghi nhận hướng xử lý ngoại lệ.', 'success');
      setSelected(null);
      setNote('');
      queryClient.invalidateQueries({ queryKey: ['admin', 'exceptions'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Trung tâm xử lý ngoại lệ (AD11)</h1>
        <p className="text-xs text-[#44544c]">Tiếp nhận và ghi nhận xử lý ngoại lệ check-in quá hạn, treo giao dịch hoặc hủy phiên.</p>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          empty={<EmptyState title="Không có ngoại lệ tồn đọng" />}
          columns={[
            { key: 'ref', header: 'Mã tham chiếu', render: (r) => <span className="font-mono font-semibold">{r.referenceCode}</span> },
            { key: 'patient', header: 'Bệnh nhân', render: (r) => `${r.patientName} (${r.phoneMasked})` },
            { key: 'detail', header: 'Mô tả ngoại lệ', render: (r) => r.detail },
            { key: 'time', header: 'Thời điểm', render: (r) => <DateTimeText value={r.createdAt} /> },
            {
              key: 'status',
              header: 'Trạng thái',
              render: (r) => <StatusPill status={r.status === 'Open' ? 'Pending' : 'Completed'} labelOverride={r.status === 'Open' ? 'Đang mở' : 'Đã xử lý'} />,
            },
            {
              key: 'act',
              header: 'Xử lý',
              render: (r) =>
                r.status === 'Open' ? (
                  <Button size="sm" variant="primary" onClick={() => setSelected(r)}>
                    Ghi nhận xử lý
                  </Button>
                ) : (
                  <span className="text-xs text-[#44544c]">Hoàn tất</span>
                ),
            },
          ]}
        />
      )}

      <Dialog
        open={Boolean(selected)}
        onClose={() => setSelected(null)}
        title={`Xử lý ngoại lệ ${selected?.referenceCode}`}
        footer={
          <>
            <Button variant="outline" onClick={() => setSelected(null)}>Đóng</Button>
            <Button variant="primary" loading={resolveMutation.isPending} disabled={!note.trim()} onClick={() => resolveMutation.mutate()}>
              Đánh dấu đã xử lý
            </Button>
          </>
        }
      >
        <FormField label="Ghi chú biên bản xử lý" required>
          {(props) => (
            <Textarea
              {...props}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="Ghi nhận kết quả phối hợp với quầy/bác sĩ..."
            />
          )}
        </FormField>
      </Dialog>
    </div>
  );
};
