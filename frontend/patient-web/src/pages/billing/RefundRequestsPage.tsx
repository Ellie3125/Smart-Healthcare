import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { clinicalApi } from '../../api/clinicalApi';
import {
  Banner,
  Button,
  Card,
  DateTimeText,
  Dialog,
  EmptyState,
  ErrorState,
  FormField,
  Input,
  MoneyText,
  Select,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/money';
import { extractPatientError } from '../../shared/lib/errors';
import type { RefundPayoutChannel, RefundRequestItem } from '../../types/clinical';

/**
 * P19 — Yêu cầu hoàn tiền: chỉ chọn kênh nhận tiền theo chính sách, tuyệt đối KHÔNG có upload chứng từ/biên lai.
 */
export const RefundRequestsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [editingItem, setEditingItem] = useState<RefundRequestItem | null>(null);
  const [channel, setChannel] = useState<RefundPayoutChannel>('OriginalVnpay');
  const [bankName, setBankName] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [accountHolder, setAccountHolder] = useState('');

  const query = useQuery({
    queryKey: ['patient', 'refunds'],
    queryFn: () => clinicalApi.listRefundRequests(),
  });

  const channelMutation = useMutation({
    mutationFn: (item: RefundRequestItem) =>
      clinicalApi.selectRefundChannel(
        item.id,
        {
          payoutChannel: channel,
          ...(channel === 'BankTransfer' ? { bankName, accountNumber, accountHolder } : {}),
        },
        createIdempotencyKey(`refund-channel-${item.id}`),
      ),
    onSuccess: () => {
      pushToast('Đã lưu kênh nhận hoàn tiền.', 'success');
      setEditingItem(null);
      queryClient.invalidateQueries({ queryKey: ['patient', 'refunds'] });
    },
    onError: (err) => {
      pushToast(extractPatientError(err).message, 'danger');
    },
  });

  if (query.isLoading) {
    return (
      <Card title="Yêu cầu hoàn tiền">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (query.isError) {
    return <ErrorState message={extractPatientError(query.error).message} onRetry={() => query.refetch()} />;
  }

  const refunds = query.data ?? [];

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-[#0f1f18]">Theo dõi & Nhận hoàn tiền</h1>
        <p className="mt-1 text-sm text-[#3e4941]">
          Kiểm tra tiến độ duyệt hoàn và chọn kênh nhận tiền phù hợp. Không cần tải lên sao kê hay hóa đơn giấy.
        </p>
      </div>

      {refunds.length === 0 ? (
        <EmptyState title="Chưa có yêu cầu hoàn tiền nào" />
      ) : (
        <div className="space-y-4">
          {refunds.map((rf) => (
            <Card key={rf.id}>
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="space-y-1.5">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-xs font-semibold text-[#006a41]">{rf.referenceCode}</span>
                    <StatusPill status={rf.status} />
                  </div>
                  <p className="font-semibold text-[#0f1f18]">{rf.reason}</p>
                  <p className="text-sm text-[#3e4941]">
                    Ngày tạo: <DateTimeText value={rf.createdAt} /> • Kênh nhận:{' '}
                    <strong>{rf.bankAccountSummary || 'Chưa chọn kênh nhận'}</strong>
                  </p>
                  {rf.rejectionReason ? (
                    <Banner tone="danger">Lý do từ chối: {rf.rejectionReason}</Banner>
                  ) : null}
                </div>
                <div className="flex flex-col items-end gap-2">
                  <MoneyText amount={rf.amount} className="text-xl text-[#006a41]" />
                  {(rf.status === 'Pending' || rf.status === 'Approved') ? (
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => {
                        setEditingItem(rf);
                        setChannel(rf.payoutChannel || 'OriginalVnpay');
                      }}
                    >
                      {rf.payoutChannel ? 'Đổi kênh nhận' : 'Chọn kênh nhận tiền'}
                    </Button>
                  ) : null}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}

      <Dialog
        open={Boolean(editingItem)}
        onClose={() => setEditingItem(null)}
        title="Chọn kênh nhận hoàn tiền"
        footer={
          <>
            <Button variant="outline" onClick={() => setEditingItem(null)}>
              Hủy
            </Button>
            <Button
              variant="primary"
              loading={channelMutation.isPending}
              disabled={channel === 'BankTransfer' && (!bankName.trim() || !accountNumber.trim())}
              onClick={() => editingItem && channelMutation.mutate(editingItem)}
            >
              Lưu kênh nhận tiền
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <FormField label="Phương thức nhận tiền" required>
            {(props) => (
              <Select
                {...props}
                value={channel}
                onChange={(e) => setChannel(e.target.value as RefundPayoutChannel)}
                options={[
                  { value: 'OriginalVnpay', label: 'Hoàn về nguồn thanh toán VNPAY ban đầu' },
                  { value: 'CashCounter', label: 'Nhận tiền mặt trực tiếp tại quầy bệnh viện' },
                  { value: 'BankTransfer', label: 'Chuyển khoản ngân hàng' },
                ]}
              />
            )}
          </FormField>

          {channel === 'BankTransfer' ? (
            <div className="space-y-3 rounded-xl bg-[#f8fbf9] p-3.5">
              <FormField label="Tên ngân hàng" required>
                {(props) => (
                  <Input
                    {...props}
                    value={bankName}
                    onChange={(e) => setBankName(e.target.value)}
                    placeholder="VD: Vietcombank, BIDV..."
                  />
                )}
              </FormField>
              <FormField label="Số tài khoản" required>
                {(props) => (
                  <Input
                    {...props}
                    value={accountNumber}
                    onChange={(e) => setAccountNumber(e.target.value)}
                    placeholder="Nhập số tài khoản nhận tiền"
                  />
                )}
              </FormField>
              <FormField label="Chủ tài khoản">
                {(props) => (
                  <Input
                    {...props}
                    value={accountHolder}
                    onChange={(e) => setAccountHolder(e.target.value)}
                    placeholder="Tên chủ tài khoản không dấu"
                  />
                )}
              </FormField>
            </div>
          ) : null}
        </div>
      </Dialog>
    </div>
  );
};
