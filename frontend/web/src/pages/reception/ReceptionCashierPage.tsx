import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banknote, Printer } from 'lucide-react';
import { receptionApi } from '../../api/receptionApi';
import {
  Banner,
  Button,
  Card,
  ConfirmDialog,
  DataTable,
  DateTimeText,
  EmptyState,
  ErrorState,
  FormField,
  Input,
  MoneyText,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { CashierPayableItem } from '../../types/staffWorkflow';

/**
 * RC04 — Quầy thu ngân tiền mặt: số tiền phải thu CHỈ ĐỌC (không có input sửa giá).
 */
export const ReceptionCashierPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [keyword, setKeyword] = useState('');
  const [confirmItem, setConfirmItem] = useState<CashierPayableItem | null>(null);

  const query = useQuery({
    queryKey: ['reception', 'cashier-items', keyword],
    queryFn: () => receptionApi.listCashierItems(keyword || undefined),
  });

  const collectMutation = useMutation({
    mutationFn: (item: CashierPayableItem) =>
      receptionApi.collectCashPayment(item.id, createIdempotencyKey(`cash-${item.id}`)),
    onSuccess: (res) => {
      pushToast(`Đã thu tiền mặt thành công. Số biên lai: ${res.receiptCode}`, 'success');
      setConfirmItem(null);
      queryClient.invalidateQueries({ queryKey: ['reception', 'cashier-items'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Quầy thu tiền mặt & In biên lai (RC04)</h1>
        <p className="text-xs text-[#44544c]">Số tiền cần thu được khóa theo bảng giá hệ thống, thu ngân không được chỉnh sửa số tiền.</p>
      </div>

      <Banner tone="info">
        Khoản phải thu hiển thị ở trạng thái chỉ đọc để phòng ngừa sai lệch tiền mặt cuối ca.
      </Banner>

      <Card>
        <FormField label="Tìm theo tên bệnh nhân hoặc mã đợt thu">
          {(props) => (
            <Input
              {...props}
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              placeholder="VD: CLS-BATCH-09..."
            />
          )}
        </FormField>
      </Card>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          empty={<EmptyState title="Không có khoản thu tiền mặt" />}
          columns={[
            { key: 'ref', header: 'Mã khoản thu', render: (r) => <span className="font-mono font-semibold">{r.referenceCode}</span> },
            { key: 'patient', header: 'Bệnh nhân', render: (r) => r.patientName },
            { key: 'desc', header: 'Nội dung', render: (r) => r.description },
            {
              key: 'amount',
              header: 'Số tiền cố định (Chỉ đọc)',
              align: 'right',
              render: (r) => <MoneyText amount={r.amount} className="text-[#006a41]" />,
            },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
            {
              key: 'act',
              header: 'Thao tác',
              render: (r) =>
                r.status === 'PendingPayment' ? (
                  <Button
                    size="sm"
                    variant="primary"
                    leftIcon={<Banknote className="h-3.5 w-3.5" />}
                    onClick={() => setConfirmItem(r)}
                  >
                    Thu tiền mặt
                  </Button>
                ) : (
                  <div className="flex items-center gap-2 text-xs text-[#44544c]">
                    <span>{r.receiptCode} (<DateTimeText value={r.paidAt} mode="time" />)</span>
                    <Button size="sm" variant="outline" leftIcon={<Printer className="h-3.5 w-3.5" />} onClick={() => window.print()}>
                      In BL
                    </Button>
                  </div>
                ),
            },
          ]}
        />
      )}

      <ConfirmDialog
        open={Boolean(confirmItem)}
        onClose={() => setConfirmItem(null)}
        onConfirm={() => {
          if (confirmItem) collectMutation.mutate(confirmItem);
        }}
        loading={collectMutation.isPending}
        title="Xác nhận thu tiền mặt"
        message={
          confirmItem ? (
            <p>
              Xác nhận đã nhận đủ <MoneyText amount={confirmItem.amount} /> từ bệnh nhân{' '}
              <strong>{confirmItem.patientName}</strong> cho khoản <strong>{confirmItem.referenceCode}</strong>?
            </p>
          ) : null
        }
      />
    </div>
  );
};
