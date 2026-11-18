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
  FormField,
  MoneyText,
  Select,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { patientGet } from '../../shared/lib/http';
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

  const [selectedProfileId, setSelectedProfileId] = useState<string>('');

  const profilesQuery = useQuery({
    queryKey: ['patient', 'profiles'],
    queryFn: () => patientGet<{ patientId: string; fullName: string; relationship: string }[]>('/api/patient/profiles'),
  });
  const profiles = profilesQuery.data ?? [];
  const activeId = profiles.find((p) => p.patientId === selectedProfileId)?.patientId ?? profiles[0]?.patientId ?? '';

  const batchesQuery = useQuery({
    queryKey: ['patient', 'cls-batches', activeId],
    queryFn: () => clinicalApi.listClsPaymentBatches(activeId),
    enabled: Boolean(activeId),
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

  if (profilesQuery.isLoading || batchesQuery.isLoading) {
    return (
      <Card title="Thanh toán chỉ định cận lâm sàng">
        <Skeleton rows={4} />
      </Card>
    );
  }

  const failed = profilesQuery.isError ? profilesQuery : batchesQuery.isError ? batchesQuery : null;
  if (failed) {
    return <ErrorState message={extractPatientError(failed.error).message} onRetry={() => failed.refetch()} />;
  }

  const batches = batchesQuery.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container/75 px-3 py-0.5 text-xs font-semibold text-[#005231] mb-2">
            Thanh toán chỉ định trong 15 phút
          </span>
          <h1 className="text-2xl sm:text-3xl font-bold text-on-surface tracking-tight">
            Thanh toán Cận lâm sàng (CLS)
          </h1>
          <p className="mt-1 text-sm text-on-surface-variant">
            Các đợt chỉ định cần thanh toán trong 15 phút để phòng xét nghiệm/CĐHA tiếp nhận thực hiện.
          </p>
        </div>
      </div>

      {profiles.length > 1 ? (
        <Card>
          <FormField label="Hồ sơ người khám">
            {(props) => (
              <Select
                {...props}
                value={activeId}
                onChange={(e) => setSelectedProfileId(e.target.value)}
                options={profiles.map((p) => ({ value: p.patientId, label: `${p.fullName} (${p.relationship})` }))}
              />
            )}
          </FormField>
        </Card>
      ) : null}

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
                title={batch.appointmentCode ? `Đợt chỉ định ${batch.appointmentCode}` : 'Đợt chỉ định cận lâm sàng'}
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
                <div className="space-y-2.5">
                  {batch.items.map((item) => (
                    <div
                      key={item.id}
                      className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-surface-container-low/75 px-4 py-3 text-sm"
                    >
                      <div>
                        <p className="font-semibold text-on-surface">{item.serviceName}</p>
                        <p className="text-xs text-on-surface-variant">
                          {[
                            item.serviceCode,
                            item.roomName,
                            item.waiverAmount > 0 ? `Miễn giảm ${item.waiverAmount.toLocaleString('vi-VN')} ₫` : '',
                          ]
                            .filter(Boolean)
                            .join(' • ')}
                        </p>
                      </div>
                      <MoneyText amount={item.netPrice} className="font-semibold text-primary" />
                    </div>
                  ))}
                </div>

                <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-surface-container-low pt-4">
                  <div className="text-sm">
                    <span className="text-on-surface-variant">Tổng cần thanh toán: </span>
                    <MoneyText amount={batch.totalPayableAmount} className="text-xl font-bold text-primary" />
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
