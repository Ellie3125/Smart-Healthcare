import React, { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import {
  Banner,
  Button,
  Card,
  ConfirmDialog,
  ErrorState,
  FormField,
  MoneyText,
  Skeleton,
  Textarea,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/money';
import { extractPatientError } from '../../shared/lib/errors';

/**
 * P15 — Hủy lịch khám: luôn hiển thị số tiền hoàn chính xác tính từ Backend trước khi xác nhận + Idempotency-Key chống double-click.
 */
export const CancelAppointmentPage: React.FC = () => {
  const { id = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const [reason, setReason] = useState('Thay đổi kế hoạch cá nhân');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [idemKey] = useState(() => createIdempotencyKey('cancel-apt'));

  const previewQuery = useQuery({
    queryKey: ['patient', 'cancel-preview', id],
    queryFn: () => bookingApi.previewCancelRefund(id),
    enabled: Boolean(id),
  });

  const cancelMutation = useMutation({
    mutationFn: () => bookingApi.cancelAppointment(id, reason.trim() || 'Bệnh nhân chủ động hủy', idemKey),
    onSuccess: (res) => {
      pushToast(`Đã hủy lịch khám. Số tiền dự kiến hoàn: ${res.refundAmount.toLocaleString('vi-VN')} ₫`, 'success');
      navigate('/refunds');
    },
    onError: (err) => {
      pushToast(extractPatientError(err).message, 'danger');
    },
  });

  if (previewQuery.isLoading) {
    return (
      <Card title="Đang tính số tiền hoàn từ máy chủ">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (previewQuery.isError || !previewQuery.data) {
    return (
      <ErrorState
        message={extractPatientError(previewQuery.error).message}
        onRetry={() => previewQuery.refetch()}
      />
    );
  }

  const preview = previewQuery.data;

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <Link to="/appointments" className="inline-flex items-center gap-1.5 text-sm font-medium text-[#006a41] hover:underline">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Quay lại lịch khám của tôi
      </Link>

      <Card title="Hủy lịch khám & Xem trước hoàn tiền">
        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-xl bg-[#f8fbf9] p-4">
              <p className="text-xs text-[#3e4941]">Phí đã thanh toán</p>
              <p className="mt-1 text-lg font-semibold text-[#0f1f18]">
                <MoneyText amount={preview.originalAmount} />
              </p>
            </div>
            <div className="rounded-xl bg-[#e5f8ed] p-4">
              <p className="text-xs text-[#005231]">Số tiền hoàn dự kiến ({preview.refundRatePercent}%)</p>
              <p className="mt-1 text-xl font-bold text-[#006a41]">
                <MoneyText amount={preview.refundAmount} />
              </p>
            </div>
          </div>

          <Banner tone={preview.eligibleToCancel ? 'info' : 'warning'} title="Chính sách áp dụng">
            {preview.policyExplanation}
          </Banner>

          {preview.eligibleToCancel ? (
            <>
              <FormField label="Lý do hủy lịch" required>
                {(props) => (
                  <Textarea
                    {...props}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder="Nhập lý do hủy lịch..."
                  />
                )}
              </FormField>

              <div className="flex justify-end gap-2">
                <Link to="/appointments">
                  <Button variant="outline">Giữ lại lịch</Button>
                </Link>
                <Button
                  variant="danger"
                  disabled={!reason.trim() || cancelMutation.isPending}
                  onClick={() => setConfirmOpen(true)}
                >
                  Tiếp tục hủy lịch
                </Button>
              </div>
            </>
          ) : null}
        </div>
      </Card>

      <ConfirmDialog
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={() => cancelMutation.mutate()}
        loading={cancelMutation.isPending}
        confirmVariant="danger"
        confirmLabel="Xác nhận hủy vĩnh viễn"
        title="Xác nhận hủy lịch khám"
        message={
          <p>
            Sau khi xác nhận, suất khám sẽ được trả lại hệ thống và tạo yêu cầu hoàn tiền{' '}
            <MoneyText amount={preview.refundAmount} />.
          </p>
        }
      />
    </div>
  );
};
