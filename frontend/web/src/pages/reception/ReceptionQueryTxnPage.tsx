import React, { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { RefreshCw } from 'lucide-react';
import { receptionApi } from '../../api/receptionApi';
import {
  Button,
  Card,
  DateTimeText,
  FormField,
  Input,
  MoneyText,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { PaymentReconcileRow } from '../../types/staffWorkflow';

export const ReceptionQueryTxnPage: React.FC = () => {
  const { pushToast } = useToast();
  const [txnRef, setTxnRef] = useState('');
  const [result, setResult] = useState<PaymentReconcileRow | null>(null);

  const mutation = useMutation({
    mutationFn: () => receptionApi.lookupPaymentTxn(txnRef.trim()),
    onSuccess: (res) => {
      setResult(res);
      pushToast(`Đã tra cứu trạng thái giao dịch ${res.vnpTxnRef}.`, 'success');
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Tra cứu giao dịch VNPAY tại quầy (RC06)</h1>
        <p className="text-xs text-[#44544c]">Hỗ trợ kiểm tra nhanh kết quả thanh toán qua `querydr` khi bệnh nhân báo đã trừ tiền.</p>
      </div>

      <Card>
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            if (txnRef.trim()) mutation.mutate();
          }}
        >
          <div className="min-w-[260px] flex-1">
            <FormField label="Mã tham chiếu giao dịch VNPAY (`vnp_TxnRef`)" required>
              {(props) => (
                <Input
                  {...props}
                  value={txnRef}
                  onChange={(e) => setTxnRef(e.target.value)}
                  placeholder="VD: VNP-20261111-9012"
                />
              )}
            </FormField>
          </div>
          <Button
            type="submit"
            variant="primary"
            leftIcon={<RefreshCw className="h-4 w-4" />}
            loading={mutation.isPending}
            disabled={!txnRef.trim()}
          >
            Kiểm tra VNPAY (querydr)
          </Button>
        </form>
      </Card>

      {result ? (
        <Card
          title={`Kết quả giao dịch ${result.vnpTxnRef}`}
          actions={<StatusPill status={result.gatewayStatus} />}
        >
          <dl className="grid gap-3 text-sm sm:grid-cols-3">
            <div>
              <dt className="text-xs text-[#44544c]">Mã lịch / CLS</dt>
              <dd className="font-semibold">{result.appointmentOrBatchCode}</dd>
            </div>
            <div>
              <dt className="text-xs text-[#44544c]">Số tiền</dt>
              <dd><MoneyText amount={result.amount} /></dd>
            </div>
            <div>
              <dt className="text-xs text-[#44544c]">Thời điểm kiểm tra</dt>
              <dd><DateTimeText value={result.checkedAt} /></dd>
            </div>
          </dl>
        </Card>
      ) : null}
    </div>
  );
};
