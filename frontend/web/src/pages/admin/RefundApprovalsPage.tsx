import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, XCircle } from 'lucide-react';
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
  MoneyText,
  Skeleton,
  StatusPill,
  Textarea,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { AdminRefundItem } from '../../types/staffWorkflow';

export const RefundApprovalsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [rejectItem, setRejectItem] = useState<AdminRefundItem | null>(null);
  const [rejectReason, setRejectReason] = useState('');

  const query = useQuery({
    queryKey: ['admin', 'refunds'],
    queryFn: () => adminOpsApi.listRefundApprovals(),
  });

  const decideMutation = useMutation({
    mutationFn: ({ item, decision, reason }: { item: AdminRefundItem; decision: 'Approve' | 'Reject'; reason?: string }) =>
      adminOpsApi.decideRefund(
        item.id,
        { decision, reason, version: item.version },
        createIdempotencyKey(`admin-ref-${item.id}`),
      ),
    onSuccess: () => {
      pushToast('Đã cập nhật trạng thái duyệt hoàn tiền.', 'success');
      setRejectItem(null);
      setRejectReason('');
      queryClient.invalidateQueries({ queryKey: ['admin', 'refunds'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Duyệt yêu cầu hoàn tiền (AD12)</h1>
        <p className="text-xs text-[#44544c]">Số tiền hoàn do hệ thống tính toán theo chính sách hủy lịch; Quản trị viên phê duyệt hoặc từ chối kèm lý do.</p>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          empty={<EmptyState title="Không có yêu cầu hoàn tiền" />}
          columns={[
            { key: 'ref', header: 'Mã yêu cầu', render: (r) => <span className="font-mono font-semibold">{r.referenceCode}</span> },
            { key: 'patient', header: 'Bệnh nhân', render: (r) => r.patientName },
            { key: 'reason', header: 'Lý do', render: (r) => r.reason },
            { key: 'amount', header: 'Số tiền hoàn', align: 'right', render: (r) => <MoneyText amount={r.amount} /> },
            { key: 'created', header: 'Ngày tạo', render: (r) => <DateTimeText value={r.createdAt} /> },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
            {
              key: 'act',
              header: 'Thao tác',
              render: (r) =>
                r.status === 'Pending' ? (
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      variant="primary"
                      leftIcon={<CheckCircle2 className="h-3.5 w-3.5" />}
                      loading={decideMutation.isPending}
                      onClick={() => decideMutation.mutate({ item: r, decision: 'Approve' })}
                    >
                      Duyệt
                    </Button>
                    <Button
                      size="sm"
                      variant="danger"
                      leftIcon={<XCircle className="h-3.5 w-3.5" />}
                      onClick={() => setRejectItem(r)}
                    >
                      Từ chối
                    </Button>
                  </div>
                ) : (
                  <span className="text-xs text-[#44544c]">Đã xử lý</span>
                ),
            },
          ]}
        />
      )}

      <Dialog
        open={Boolean(rejectItem)}
        onClose={() => setRejectItem(null)}
        title={`Từ chối hoàn tiền ${rejectItem?.referenceCode}`}
        footer={
          <>
            <Button variant="outline" onClick={() => setRejectItem(null)}>Hủy</Button>
            <Button
              variant="danger"
              loading={decideMutation.isPending}
              disabled={!rejectReason.trim()}
              onClick={() => rejectItem && decideMutation.mutate({ item: rejectItem, decision: 'Reject', reason: rejectReason.trim() })}
            >
              Xác nhận từ chối
            </Button>
          </>
        }
      >
        <FormField label="Lý do từ chối" required>
          {(props) => (
            <Textarea
              {...props}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="Nhập lý do rõ ràng..."
            />
          )}
        </FormField>
      </Dialog>
    </div>
  );
};
