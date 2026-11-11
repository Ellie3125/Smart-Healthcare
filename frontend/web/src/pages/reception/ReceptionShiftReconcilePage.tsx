import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { receptionApi } from '../../api/receptionApi';
import {
  Banner,
  Button,
  Card,
  DateTimeText,
  ErrorState,
  FormField,
  Input,
  MoneyText,
  Skeleton,
  Textarea,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';

export const ReceptionShiftReconcilePage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [actualCash, setActualCash] = useState('');
  const [note, setNote] = useState('');

  const query = useQuery({
    queryKey: ['reception', 'shift-summary'],
    queryFn: () => receptionApi.getShiftSummary(),
  });

  const closeMutation = useMutation({
    mutationFn: () =>
      receptionApi.closeCashierShift(
        {
          actualCashCounted: Number(actualCash),
          discrepancyNote: note.trim(),
        },
        createIdempotencyKey('close-shift'),
      ),
    onSuccess: () => {
      pushToast('Đã chốt ca thu ngân cuối ngày thành công.', 'success');
      queryClient.invalidateQueries({ queryKey: ['reception', 'shift-summary'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  if (query.isLoading) {
    return (
      <Card title="Đối soát chốt ca cuối ngày">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (query.isError || !query.data) {
    return <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />;
  }

  const s = query.data;
  const parsedActual = actualCash === '' ? s.netExpectedCash : Number(actualCash);
  const diff = parsedActual - s.netExpectedCash;

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Chốt ca thu ngân cuối ngày (RC09)</h1>
        <p className="text-xs text-[#44544c]">
          Nhân sự trực ca: <strong>{s.cashierName}</strong> • Ngày <DateTimeText value={s.shiftDate} mode="date" />
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <Card>
          <p className="text-xs text-[#44544c]">Tổng thu tiền mặt (hệ thống)</p>
          <p className="mt-1 text-xl font-bold text-[#006a41]"><MoneyText amount={s.expectedCashCollected} /></p>
        </Card>
        <Card>
          <p className="text-xs text-[#44544c]">Tổng chi hoàn tiền mặt</p>
          <p className="mt-1 text-xl font-bold text-[#ba1a1a]"><MoneyText amount={s.expectedCashRefunded} /></p>
        </Card>
        <Card>
          <p className="text-xs text-[#44544c]">Tồn quỹ ròng lý thuyết</p>
          <p className="mt-1 text-2xl font-bold text-[#0f1f18]"><MoneyText amount={s.netExpectedCash} /></p>
        </Card>
      </div>

      {s.isClosed ? (
        <Banner tone="success" title="Ca thu ngân đã được chốt">
          Thời gian chốt: <DateTimeText value={s.closedAt} />
        </Banner>
      ) : (
        <Card title="Kiểm đếm thực tế & Xác nhận bàn giao">
          <div className="space-y-4">
            <FormField label="Tiền mặt thực đếm tại két (VNĐ)" required>
              {(props) => (
                <Input
                  {...props}
                  type="number"
                  value={actualCash}
                  onChange={(e) => setActualCash(e.target.value)}
                  placeholder={String(s.netExpectedCash)}
                />
              )}
            </FormField>

            {actualCash !== '' && diff !== 0 ? (
              <Banner tone="warning" title="Phát hiện chênh lệch tiền mặt">
                Chênh lệch thực đếm so với sổ sách: <MoneyText amount={diff} signed />. Vui lòng ghi rõ lý do bên dưới.
              </Banner>
            ) : null}

            <FormField label="Ghi chú bàn giao / Giải trình chênh lệch" required={actualCash !== '' && diff !== 0}>
              {(props) => (
                <Textarea
                  {...props}
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder="Ghi nhận tình trạng két tiền cuối ca..."
                />
              )}
            </FormField>

            <div className="flex justify-end">
              <Button
                variant="primary"
                loading={closeMutation.isPending}
                disabled={actualCash === '' || (diff !== 0 && !note.trim())}
                onClick={() => closeMutation.mutate()}
              >
                Xác nhận chốt ca
              </Button>
            </div>
          </div>
        </Card>
      )}
    </div>
  );
};
