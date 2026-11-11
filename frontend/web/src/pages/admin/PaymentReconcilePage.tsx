import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { RefreshCw } from 'lucide-react';
import { adminOpsApi } from '../../api/adminOpsApi';
import {
  Banner,
  Button,
  Card,
  DataTable,
  DateTimeText,
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

/**
 * AD13 — Đối soát giao dịch VNPAY (`querydr`).
 * QUY TẮC CỨNG: Tuyệt đối KHÔNG có nút "Đánh dấu đã trả" thủ công. Mọi cập nhật trạng thái chỉ đến từ `querydr`/IPN.
 */
export const PaymentReconcilePage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [manualTxnRef, setManualTxnRef] = useState('');

  const query = useQuery({
    queryKey: ['admin', 'reconcile'],
    queryFn: () => adminOpsApi.listPaymentReconcile(),
  });

  const queryDrMutation = useMutation({
    mutationFn: (txnRef: string) =>
      adminOpsApi.queryVnpayDr(txnRef, createIdempotencyKey(`qdr-${txnRef}`)),
    onSuccess: (res) => {
      pushToast(`Đã tra cứu cổng VNPAY cho ${res.vnpTxnRef}: ${res.gatewayStatus}`, 'success');
      setManualTxnRef('');
      queryClient.invalidateQueries({ queryKey: ['admin', 'reconcile'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Đối soát thanh toán VNPAY (AD13)</h1>
        <p className="text-xs text-[#44544c]">
          Sử dụng truy vấn chính thức `querydr` để đồng bộ kết quả từ VNPAY. Không hỗ trợ sửa tay trạng thái thanh toán.
        </p>
      </div>

      <Banner tone="info">
        Để bảo toàn tính toàn vẹn tài chính, hệ thống không cung cấp thao tác xác nhận thanh toán thủ công. Vui lòng dùng nút Kiểm tra `querydr`.
      </Banner>

      <Card title="Tra cứu trực tiếp theo mã tham chiếu VNPAY (`vnp_TxnRef`)">
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            if (manualTxnRef.trim()) queryDrMutation.mutate(manualTxnRef.trim());
          }}
        >
          <div className="min-w-[260px] flex-1">
            <FormField label="Mã giao dịch VNPAY">
              {(props) => (
                <Input
                  {...props}
                  value={manualTxnRef}
                  onChange={(e) => setManualTxnRef(e.target.value)}
                  placeholder="VD: VNP-20261111-9012"
                />
              )}
            </FormField>
          </div>
          <Button
            type="submit"
            variant="primary"
            leftIcon={<RefreshCw className="h-4 w-4" />}
            loading={queryDrMutation.isPending}
            disabled={!manualTxnRef.trim()}
          >
            Kiểm tra querydr
          </Button>
        </form>
      </Card>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          columns={[
            { key: 'txn', header: 'Mã VNPAY', render: (r) => <span className="font-mono font-semibold">{r.vnpTxnRef}</span> },
            { key: 'code', header: 'Mã lịch / CLS', render: (r) => r.appointmentOrBatchCode },
            { key: 'amount', header: 'Số tiền', align: 'right', render: (r) => <MoneyText amount={r.amount} /> },
            { key: 'local', header: 'Trạng thái nội bộ', render: (r) => <StatusPill status={r.localStatus} /> },
            { key: 'gw', header: 'Đối soát cổng VNPAY', render: (r) => <StatusPill status={r.gatewayStatus} /> },
            { key: 'checked', header: 'Kiểm tra lúc', render: (r) => <DateTimeText value={r.checkedAt} /> },
            {
              key: 'act',
              header: 'Truy vấn cổng',
              render: (r) => (
                <Button
                  size="sm"
                  variant="secondary"
                  leftIcon={<RefreshCw className="h-3.5 w-3.5" />}
                  loading={queryDrMutation.isPending}
                  onClick={() => queryDrMutation.mutate(r.vnpTxnRef)}
                >
                  Kiểm tra querydr
                </Button>
              ),
            },
          ]}
        />
      )}
    </div>
  );
};
