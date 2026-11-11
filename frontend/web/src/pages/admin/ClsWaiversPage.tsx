import React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminOpsApi } from '../../api/adminOpsApi';
import { Button, Card, DataTable, EmptyState, ErrorState, MoneyText, Skeleton, StatusPill, useToast } from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';

export const ClsWaiversPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const query = useQuery({
    queryKey: ['admin', 'cls-waivers'],
    queryFn: () => adminOpsApi.listClsWaivers(),
  });

  const decideMutation = useMutation({
    mutationFn: ({ id, approve }: { id: string; approve: boolean }) =>
      adminOpsApi.decideClsWaiver(id, approve, createIdempotencyKey(`waiv-${id}`)),
    onSuccess: () => {
      pushToast('Đã cập nhật quyết định miễn giảm CLS.', 'success');
      queryClient.invalidateQueries({ queryKey: ['admin', 'cls-waivers'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Duyệt miễn / giảm Cận lâm sàng (AD16)</h1>
        <p className="text-xs text-[#44544c]">Xem xét đề xuất miễn/giảm phí dịch vụ CLS trước khi bệnh nhân thanh toán hoặc hoàn lại.</p>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          empty={<EmptyState title="Không có đề xuất miễn giảm CLS" />}
          columns={[
            { key: 'enc', header: 'Mã hồ sơ', render: (r) => <span className="font-mono font-semibold">{r.encounterCode}</span> },
            { key: 'patient', header: 'Bệnh nhân', render: (r) => r.patientName },
            { key: 'svc', header: 'Dịch vụ CLS', render: (r) => r.serviceName },
            { key: 'orig', header: 'Giá gốc', align: 'right', render: (r) => <MoneyText amount={r.originalPrice} /> },
            { key: 'waiv', header: 'Đề xuất giảm', align: 'right', render: (r) => <MoneyText amount={r.requestedWaiverAmount} /> },
            { key: 'reason', header: 'Lý do', render: (r) => r.reason },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
            {
              key: 'act',
              header: 'Thao tác',
              render: (r) =>
                r.status === 'Pending' ? (
                  <div className="flex gap-2">
                    <Button size="sm" variant="primary" onClick={() => decideMutation.mutate({ id: r.id, approve: true })}>
                      Duyệt
                    </Button>
                    <Button size="sm" variant="danger" onClick={() => decideMutation.mutate({ id: r.id, approve: false })}>
                      Từ chối
                    </Button>
                  </div>
                ) : (
                  <span className="text-xs text-[#44544c]">Hoàn tất</span>
                ),
            },
          ]}
        />
      )}
    </div>
  );
};
