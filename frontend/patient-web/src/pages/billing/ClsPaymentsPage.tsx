import React, { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { CreditCard } from 'lucide-react';
import { clinicalApi } from '../../api/clinicalApi';
import {
  Button,
  Card,
  Countdown,
  DateTimeText,
  EmptyState,
  ErrorState,
  MoneyText,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/money';
import { extractPatientError } from '../../shared/lib/errors';

/**
 * P17 — Thanh toán đợt chỉ định cận lâm sàng với bộ đếm 15 phút + Idempotency-Key.
 */
export const ClsPaymentsPage: React.FC = () => {
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const [expiredMap, setExpiredMap] = useState<Record<string, boolean>>({});
  const [payingBatchId, setPayingBatchId] = useState<string | null>(null);

  const batchesQuery = useQuery({
    queryKey: ['patient', 'cls-batches'],
    queryFn: () => clinicalApi.listClsPaymentBatches(),
  });

  const payMutation = useMutation({
    mutationFn: (batchId: string) => clinicalApi.createClsVnpayCheckout(batchId, createIdempotencyKey(`cls-${batchId}`)),
    onMutate: (batchId) => setPayingBatchId(batchId),
    onSuccess: (res) => {
      if (res.paymentUrl.startsWith('/')) {
        navigate(res.paymentUrl);
      } else {
        window.location.assign(res.paymentUrl);
      }
    },
    onError: (err) => {
      pushToast(extractPatientError(err).message, 'danger');
    },
    onSettled: () => setPayingBatchId(null),
  });

  if (batchesQuery.isLoading) {
    return (
      <Card title="Thanh toán chỉ định cận lâm sàng">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (batchesQuery.isError) {
    return (
      <ErrorState
        message={extractPatientError(batchesQuery.error).message}
        onRetry={() => batchesQuery.refetch()}
      />
    );
  }

  const batches = batchesQuery.data ?? [];

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-[#0f1f18]">Thanh toán Cận lâm sàng (CLS)</h1>
        <p className="mt-1 text-sm text-[#3e4941]">
          Các đợt chỉ định cần thanh toán trong 15 phút để phòng xét nghiệm/CĐHA tiếp nhận thực hiện.
        </p>
      </div>

      {batches.length === 0 ? (
        <EmptyState
          title="Không có chỉ định CLS chờ thanh toán"
          description="Khi bác sĩ phát sinh chỉ định xét nghiệm hoặc chẩn đoán hình ảnh mới, đợt thanh toán sẽ xuất hiện tại đây."
        />
      ) : (
        <div className="space-y-4">
          {batches.map((batch) => {
            const expired = expiredMap[batch.batchId] || batch.status === 'Expired';
            return (
              <Card
                key={batch.batchId}
                title={`Đợt chỉ định ${batch.appointmentCode}`}
                subtitle={
                  <>
                    Tạo lúc <DateTimeText value={batch.createdAt} />
                  </>
                }
                actions={
                  <div className="flex flex-wrap items-center gap-2">
                    <StatusPill status={expired ? 'Expired' : batch.status} />
                    {batch.status === 'PendingPayment' ? (
                      <Countdown
                        expiresAt={batch.expiresAt}
                        label="Hạn thanh toán"
                        onExpire={() => setExpiredMap((prev) => ({ ...prev, [batch.batchId]: true }))}
                      />
                    ) : null}
                  </div>
                }
              >
                <div className="space-y-3">
                  {batch.items.map((item) => (
                    <div
                      key={item.id}
                      className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#e5f8ed] bg-[#f8fbf9] px-4 py-3 text-sm"
                    >
                      <div>
                        <p className="font-semibold text-[#0f1f18]">{item.serviceName}</p>
                        <p className="text-xs text-[#3e4941]">
                          {item.serviceCode} • {item.roomName}
                          {item.waiverAmount > 0 ? ` • Miễn giảm ${item.waiverAmount.toLocaleString('vi-VN')} ₫` : ''}
                        </p>
                      </div>
                      <MoneyText amount={item.netPrice} className="text-[#006a41]" />
                    </div>
                  ))}
                </div>

                <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-[#e5f8ed] pt-4">
                  <div className="text-sm">
                    <span className="text-[#3e4941]">Tổng cần thanh toán: </span>
                    <MoneyText amount={batch.totalPayableAmount} className="text-xl text-[#006a41]" />
                  </div>
                  {batch.status === 'PendingPayment' ? (
                    <Button
                      variant="primary"
                      leftIcon={<CreditCard className="h-4 w-4" />}
                      disabled={expired}
                      loading={payingBatchId === batch.batchId}
                      onClick={() => payMutation.mutate(batch.batchId)}
                    >
                      Thanh toán qua VNPAY
                    </Button>
                  ) : null}
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
};
