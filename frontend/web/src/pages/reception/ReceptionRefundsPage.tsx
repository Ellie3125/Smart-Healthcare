import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banknote } from 'lucide-react';
import { receptionApi } from '../../api/receptionApi';
import {
  Button,
  Card,
  ConfirmDialog,
  DataTable,
  EmptyState,
  ErrorState,
  MoneyText,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';

export const ReceptionRefundsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [selected, setSelected] = useState<{ id: string; referenceCode: string; patientName: string; amount: number } | null>(null);

  const query = useQuery({
    queryKey: ['reception', 'cash-refunds'],
    queryFn: () => receptionApi.listApprovedCashRefunds(),
  });

  const disburseMutation = useMutation({
    mutationFn: (id: string) =>
      receptionApi.disburseCashRefund(id, createIdempotencyKey(`disburse-${id}`)),
    onSuccess: (res) => {
      pushToast(`Đã chi hoàn tiền mặt thành công. Số phiếu chi: ${res.voucherCode}`, 'success');
      setSelected(null);
      queryClient.invalidateQueries({ queryKey: ['reception', 'cash-refunds'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Chi hoàn tiền mặt tại quầy (RC08)</h1>
        <p className="text-xs text-[#44544c]">Thực hiện chi tiền mặt cho các yêu cầu hoàn tiền đã được Quản trị viên phê duyệt.</p>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          empty={<EmptyState title="Không có phiếu chờ chi tiền mặt" />}
          columns={[
            { key: 'ref', header: 'Mã hoàn tiền', render: (r) => <span className="font-mono font-semibold">{r.referenceCode}</span> },
            { key: 'patient', header: 'Bệnh nhân', render: (r) => r.patientName },
            { key: 'amt', header: 'Số tiền chi', align: 'right', render: (r) => <MoneyText amount={r.amount} /> },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
            {
              key: 'act',
              header: 'Thao tác',
              render: (r) =>
                r.status === 'Approved' ? (
                  <Button
                    size="sm"
                    variant="primary"
                    leftIcon={<Banknote className="h-3.5 w-3.5" />}
                    onClick={() => setSelected(r)}
                  >
                    Chi tiền mặt & In phiếu
                  </Button>
                ) : (
                  <span className="text-xs text-[#006a41]">Đã hoàn tất</span>
                ),
            },
          ]}
        />
      )}

      <ConfirmDialog
        open={Boolean(selected)}
        onClose={() => setSelected(null)}
        onConfirm={() => {
          if (selected) disburseMutation.mutate(selected.id);
        }}
        loading={disburseMutation.isPending}
        title="Xác nhận chi hoàn tiền mặt"
        message={
          selected ? (
            <p>
              Xác nhận chi trả <MoneyText amount={selected.amount} /> tiền mặt cho bệnh nhân{' '}
              <strong>{selected.patientName}</strong> ({selected.referenceCode})?
            </p>
          ) : null
        }
      />
    </div>
  );
};
