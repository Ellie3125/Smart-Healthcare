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
    <div className="space-y-5">
      <div>
        <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
          Chốt ca thu ngân cuối ngày (RC09)
        </h1>
        <p className="text-sm text-on-surface-variant">
          Nhân sự trực ca: <strong className="text-on-surface">{s.cashierName}</strong> • Ngày <DateTimeText value={s.shiftDate} mode="date" />
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <Card className="border-l-4 border-l-primary">
          <p className="text-xs font-medium uppercase tracking-wider text-on-surface-variant">Tổng thu tiền mặt (hệ thống)</p>
          <p className="font-data-tabular mt-1.5 text-2xl font-extrabold text-primary"><MoneyText amount={s.expectedCashCollected} /></p>
        </Card>
        <Card className="border-l-4 border-l-error">
          <p className="text-xs font-medium uppercase tracking-wider text-on-surface-variant">Tổng chi hoàn tiền mặt</p>
          <p className="font-data-tabular mt-1.5 text-2xl font-extrabold text-error"><MoneyText amount={s.expectedCashRefunded} /></p>
        </Card>
        <Card className="bg-surface-container-low/80">
          <p className="text-xs font-medium uppercase tracking-wider text-on-surface-variant">Tồn quỹ ròng lý thuyết</p>
          <p className="font-data-tabular mt-1.5 text-2xl font-extrabold text-on-surface"><MoneyText amount={s.netExpectedCash} /></p>
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
